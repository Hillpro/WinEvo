using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Loads action manifests from the <c>actions/</c> folder shipped next to the
/// Shell executable. Later phases will add the user-added catalog
/// (<c>%LOCALAPPDATA%\WinEvo\Actions\</c>) and remote catalog updates.
/// </summary>
public sealed class ActionCatalog
{
    private readonly string _rootDirectory;
    private readonly List<ActionManifest> _manifests = new();

    public ActionCatalog(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public IReadOnlyList<ActionManifest> Manifests => _manifests;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _manifests.Clear();
        if (!Directory.Exists(_rootDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(_rootDirectory, "*.json", SearchOption.AllDirectories))
        {
            // Skip the schemas/ subfolder — those are not action manifests.
            if (Path.GetDirectoryName(file)?.EndsWith("schemas", StringComparison.OrdinalIgnoreCase) == true)
                continue;

            try
            {
                var manifest = await ManifestLoader.LoadAsync(file, ct).ConfigureAwait(false);
                _manifests.Add(manifest);
            }
            catch (Exception ex)
            {
                // Log and skip — a single malformed manifest shouldn't block the whole catalog.
                ShellLog.WriteException($"catalog: failed to load '{file}'", ex);
            }
        }
    }

    /// <summary>
    /// Resolves the actions directory by trying the path next to the Shell
    /// executable first, then walking upward looking for a repo-relative
    /// <c>actions/</c> folder to support "dotnet run" dev scenarios.
    /// </summary>
    public static string ResolveDefaultRoot()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "actions"),
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var up = 0; up < 8 && dir is not null; up++, dir = dir.Parent)
        {
            candidates.Add(Path.Combine(dir.FullName, "actions"));
        }

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                Directory.EnumerateFiles(candidate, "*.json", SearchOption.AllDirectories).Any())
            {
                return candidate;
            }
        }

        return candidates[0];
    }
}
