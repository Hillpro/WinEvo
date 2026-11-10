using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>List-row view model for a single action in the catalog.</summary>
public sealed class ActionItemViewModel : ObservableObject
{
    public ActionItemViewModel(ActionDescriptor descriptor, CultureInfo culture)
    {
        Descriptor = descriptor;
        DisplayName = descriptor.Name.Resolve(culture);
        DisplayDescription = descriptor.Description?.Resolve(culture);
    }

    public ActionDescriptor Descriptor { get; }

    public string Id => Descriptor.Id;
    public string DisplayName { get; }
    public string? DisplayDescription { get; }
    public string Category => Descriptor.Category;
    public bool RequiresElevation => Descriptor.Elevation == ElevationRequirement.Required;
}
