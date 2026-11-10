using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

/// <summary>
/// A missing or misspelled text key does not fail at run time: the UI shows
/// the key, or silently keeps the English fallback. These tests catch that
/// when the code is built instead. They read the Shell's XAML, its
/// <c>.resw</c> files and the two projects' sources straight from the
/// repository, because the WinUI project has no test project of its own.
/// </summary>
public partial class ShellTextResourcesTests
{
    private static readonly CultureInfo s_french = CultureInfo.GetCultureInfo("fr");

    /// <summary>Keys built at run time from an id (<c>Category_storage</c>), so no source literal names them.</summary>
    private static readonly string[] s_keyPrefixesBuiltAtRunTime = ["Category_", "DriveType_"];

    private static readonly ResourceManager s_viewModelStrings = new(
        "WinEvo.Shell.Core.Strings",
        typeof(MainViewModel).Assembly);

    // ------------------------------------------------------------ view models (.resx)

    [Fact]
    public void View_model_text_is_translated_key_for_key()
    {
        Assert.Equal(ViewModelStrings(CultureInfo.InvariantCulture).Keys.Order(), ViewModelStrings(s_french).Keys.Order());
    }

    [Fact]
    public void View_model_translations_keep_the_same_placeholders()
    {
        AssertSamePlaceholders(ViewModelStrings(CultureInfo.InvariantCulture), ViewModelStrings(s_french));
    }

    [Fact]
    public void Every_view_model_text_key_used_in_code_exists()
    {
        var declared = ViewModelStrings(CultureInfo.InvariantCulture).Keys.ToHashSet();
        var used = KeysNamedIn(SourceFiles("src/WinEvo.Shell.Core", "*.cs"), ViewModelKeyLiteral());

        Assert.Empty(used.Except(declared));
    }

    [Fact]
    public void Every_view_model_text_is_used()
    {
        var used = KeysNamedIn(SourceFiles("src/WinEvo.Shell.Core", "*.cs"), ViewModelKeyLiteral());
        var unused = ViewModelStrings(CultureInfo.InvariantCulture).Keys
            .Where(key => !s_keyPrefixesBuiltAtRunTime.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .Except(used);

        Assert.Empty(unused);
    }

    // ------------------------------------------------------------ XAML and dialogs (.resw)

    [Fact]
    public void Window_text_is_translated_key_for_key()
    {
        Assert.Equal(WindowStrings("en").Keys.Order(), WindowStrings("fr").Keys.Order());
    }

    [Fact]
    public void Window_translations_keep_the_same_placeholders()
    {
        AssertSamePlaceholders(WindowStrings("en"), WindowStrings("fr"));
    }

    [Fact]
    public void Every_xUid_in_the_XAML_has_text()
    {
        var elementsWithText = WindowStrings("en").Keys
            .Where(key => key.Contains('.'))
            .Select(key => key[..key.IndexOf('.')])
            .ToHashSet();

        Assert.Empty(XamlUids().Except(elementsWithText));
    }

    [Fact]
    public void Every_window_text_is_used()
    {
        var uids = XamlUids();
        var dialogKeys = KeysNamedIn(SourceFiles("src/WinEvo.Shell", "*.cs"), DialogTextCall());

        var unused = WindowStrings("en").Keys.Where(key => key.Contains('.')
            ? !uids.Contains(key[..key.IndexOf('.')])
            : !dialogKeys.Contains(key));

        Assert.Empty(unused);
    }

    [Fact]
    public void Every_dialog_text_key_used_in_code_exists()
    {
        var declared = WindowStrings("en").Keys.ToHashSet();
        var used = KeysNamedIn(SourceFiles("src/WinEvo.Shell", "*.cs"), DialogTextCall());

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(declared));
    }

    // ------------------------------------------------------------ helpers

    private static Dictionary<string, string> ViewModelStrings(CultureInfo culture)
    {
        var declared = s_viewModelStrings.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"no view-model strings for culture '{culture.Name}'");
        return declared.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    private static Dictionary<string, string> WindowStrings(string language)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "WinEvo.Shell", "Strings", language, "Resources.resw");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")!.Value);
    }

    private static HashSet<string> XamlUids()
        => KeysNamedIn(SourceFiles("src/WinEvo.Shell", "*.xaml"), XUid());

    private static HashSet<string> KeysNamedIn(IEnumerable<string> files, Regex keyPattern)
        => files
            .SelectMany(file => keyPattern.Matches(File.ReadAllText(file)))
            .Select(match => match.Groups["key"].Value)
            .ToHashSet();

    private static IEnumerable<string> SourceFiles(string project, string pattern)
        => Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), project), pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static void AssertSamePlaceholders(Dictionary<string, string> english, Dictionary<string, string> translated)
    {
        Assert.All(english, text =>
        {
            Assert.True(translated.TryGetValue(text.Key, out var translation), $"'{text.Key}' is not translated");
            Assert.Equal(Placeholders(text.Value), Placeholders(translation!));
        });
    }

    private static IEnumerable<string> Placeholders(string text)
        => Placeholder().Matches(text).Select(match => match.Value).Order();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WinEvo.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("repository root not found from the test assembly location");
    }

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("""
        x:Uid="(?<key>\w+)"
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex XUid();

    [GeneratedRegex("""
        DialogText\.(Get|Format)\("(?<key>\w+)"
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex DialogTextCall();

    [GeneratedRegex("""
        "(?<key>(Status|Agent|Drive)_\w+)"
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex ViewModelKeyLiteral();
}
