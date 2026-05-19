using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinEvo.Shell.Core.ViewModels.Interactions;

namespace WinEvo.Shell;

/// <summary>
/// Picks the per-mode <see cref="DataTemplate"/> from the runtime type of the
/// bound <see cref="InteractionController"/>. Must be <c>partial</c> — WinUI 3 /
/// C#-WinRT rely on the generated metadata stub to register custom
/// <c>DataTemplateSelector</c> subclasses as activatable WinRT types. Adding
/// a new interaction mode = one new property here + a matching
/// <c>DataTemplate</c> in <see cref="WinEvo.Shell.Resources"/>.
/// </summary>
public sealed partial class InteractionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ButtonTemplate { get; set; }
    public DataTemplate? ToggleTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        ToggleInteractionController => ToggleTemplate,
        ButtonInteractionController => ButtonTemplate,
        _ => ButtonTemplate,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
