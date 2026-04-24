using System.Text.Json;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.Tests;

public class StringBundleTests
{
    [Fact]
    public void Resolve_returns_template_from_requested_language()
    {
        using var dir = TempDir.Create();
        WriteBundle(dir, "en", new() { ["greeting"] = "Hello" });
        WriteBundle(dir, "fr", new() { ["greeting"] = "Bonjour" });

        var bundle = new StringBundle(dir.Path);

        Assert.Equal("Bonjour", bundle.Resolve("greeting", "fr"));
        Assert.Equal("Hello", bundle.Resolve("greeting", "en"));
    }

    [Fact]
    public void Resolve_falls_back_to_english_when_key_missing_in_requested_language()
    {
        using var dir = TempDir.Create();
        WriteBundle(dir, "en", new() { ["only_en"] = "English only" });
        WriteBundle(dir, "fr", new() { ["other"] = "Autre" });

        var bundle = new StringBundle(dir.Path);

        Assert.Equal("English only", bundle.Resolve("only_en", "fr"));
    }

    [Fact]
    public void Resolve_returns_raw_key_when_missing_from_every_bundle()
    {
        using var dir = TempDir.Create();
        WriteBundle(dir, "en", new() { ["a"] = "A" });
        var bundle = new StringBundle(dir.Path);

        Assert.Equal("unknown.key", bundle.Resolve("unknown.key", "en"));
    }

    [Fact]
    public void Resolve_substitutes_named_tokens()
    {
        using var dir = TempDir.Create();
        WriteBundle(dir, "en", new() { ["wipe"] = "Wipes drive {drive} with {tool}." });
        var bundle = new StringBundle(dir.Path);

        var result = bundle.Resolve("wipe", "en", new Dictionary<string, string>
        {
            ["drive"] = "C:\\",
            ["tool"] = "cipher",
        });
        Assert.Equal("Wipes drive C:\\ with cipher.", result);
    }

    [Fact]
    public void Resolve_leaves_unknown_token_placeholders_intact()
    {
        using var dir = TempDir.Create();
        WriteBundle(dir, "en", new() { ["t"] = "Known {a} unknown {b}." });
        var bundle = new StringBundle(dir.Path);

        var result = bundle.Resolve("t", "en", new Dictionary<string, string> { ["a"] = "X" });
        Assert.Equal("Known X unknown {b}.", result);
    }

    [Fact]
    public void Resolve_tolerates_missing_language_file()
    {
        using var dir = TempDir.Create();
        WriteBundle(dir, "en", new() { ["k"] = "V" });
        var bundle = new StringBundle(dir.Path);

        Assert.Equal("V", bundle.Resolve("k", "de"));
    }

    private static void WriteBundle(TempDir dir, string language, Dictionary<string, string> entries)
    {
        var path = Path.Combine(dir.Path, $"Strings.{language}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(entries));
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; }
    private TempDir(string path) => Path = path;

    public static TempDir Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winevo-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TempDir(path);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
