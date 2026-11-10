using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>List-row view model for a single action in the catalog.</summary>
public sealed class ActionItemViewModel : ObservableObject
{
    public ActionItemViewModel(ActionDescriptor descriptor, CultureInfo culture)
    {
        Descriptor = descriptor;
        DisplayName = descriptor.Name.Resolve(culture);
        DisplayDescription = descriptor.Description?.Resolve(culture);
        DisplayCategory = descriptor.Category.Name.Resolve(culture);
    }

    public ActionDescriptor Descriptor { get; }

    public string Id => Descriptor.Id;
    public string DisplayName { get; }
    public string? DisplayDescription { get; }
    /// <summary>The category's id: stable, the same in every language, used to group and sort.</summary>
    public string Category => Descriptor.Category.Id;

    public string DisplayCategory { get; }
    public bool RequiresElevation => Descriptor.Elevation == ElevationRequirement.Required;
}
