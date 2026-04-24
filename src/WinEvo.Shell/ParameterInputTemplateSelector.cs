using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell;

/// <summary>
/// Chooses the per-parameter <see cref="DataTemplate"/> based on the runtime
/// type of the bound <see cref="ParameterInputViewModel"/>. Must be
/// <c>partial</c> — WinUI 3 / C#-WinRT rely on the generated metadata stub to
/// register custom DataTemplateSelector subclasses as activatable WinRT types.
/// Adding a new parameter VM = add a property here and bind its template in
/// XAML.
/// </summary>
public sealed partial class ParameterInputTemplateSelector : DataTemplateSelector
{
    public DataTemplate? StringTemplate { get; set; }
    public DataTemplate? IntegerTemplate { get; set; }
    public DataTemplate? BooleanTemplate { get; set; }
    public DataTemplate? EnumTemplate { get; set; }
    public DataTemplate? DriveTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        DriveParameterInputViewModel => DriveTemplate,
        IntegerParameterInputViewModel => IntegerTemplate,
        BooleanParameterInputViewModel => BooleanTemplate,
        EnumParameterInputViewModel => EnumTemplate,
        StringParameterInputViewModel => StringTemplate,
        _ => StringTemplate,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
