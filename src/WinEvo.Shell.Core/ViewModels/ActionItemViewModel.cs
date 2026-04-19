using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>List-row view model for a single action in the catalog.</summary>
public sealed class ActionItemViewModel : ObservableObject
{
    public ActionItemViewModel(ActionManifest manifest, string? language)
    {
        Manifest = manifest;
        Language = language;
    }

    public ActionManifest Manifest { get; }
    public string? Language { get; }

    public string Id => Manifest.Id;
    public string DisplayName => Manifest.GetLocalizedName(Language);
    public string? DisplayDescription => Manifest.GetLocalizedDescription(Language);
    public string Category => Manifest.Category;
    public bool RequiresElevation => Manifest.Requirements.Elevation == ElevationRequirement.Required;
}
