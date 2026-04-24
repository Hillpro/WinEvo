using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Resolves shared string-bundle keys (e.g. warning messages referenced by
/// multiple manifests) into localized text. Bundles are flat JSON dictionaries
/// shipped alongside the Shell: <c>resources/Strings.&lt;lang&gt;.json</c>
/// mapping <c>key → template</c>, where templates may embed <c>{tokenName}</c>
/// placeholders substituted at resolve time.
/// </summary>
/// <remarks>
/// Missing-key behaviour is intentionally loud but non-fatal: the raw key is
/// returned so the UI has something to show and the mistake is visible. The
/// English bundle is always loaded as a fallback layer under the requested
/// language.
/// </remarks>
public sealed partial class StringBundle
{
    private const string FallbackLanguage = "en";

    private static readonly Regex s_placeholder = new(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled);

    private readonly string _rootDirectory;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _byLanguage = new();

    public StringBundle(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string Resolve(string key, string? language, IReadOnlyDictionary<string, string>? tokens = null)
    {
        var primary = LoadLanguage(language ?? FallbackLanguage);
        var template = primary.TryGetValue(key, out var t) ? t : null;
        if (template is null && !string.Equals(language, FallbackLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = LoadLanguage(FallbackLanguage);
            _ = fallback.TryGetValue(key, out template);
        }
        template ??= key;

        if (tokens is null || tokens.Count == 0)
            return template;

        return s_placeholder.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            return tokens.TryGetValue(name, out var value) ? value : m.Value;
        });
    }

    private IReadOnlyDictionary<string, string> LoadLanguage(string language)
        => _byLanguage.GetOrAdd(language, LoadFromDisk);

    private IReadOnlyDictionary<string, string> LoadFromDisk(string language)
    {
        var path = Path.Combine(_rootDirectory, $"Strings.{language}.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>();

        using var stream = File.OpenRead(path);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return parsed ?? [];
    }

    public static string ResolveDefaultRoot()
    {
        var exeDir = AppContext.BaseDirectory;
        return Path.Combine(exeDir, "resources");
    }
}
