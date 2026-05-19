using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinEvo.Shell.Services;

/// <summary>
/// Minimal <c>bool → Visibility</c> converter for merged-ResourceDictionary
/// templates, which use classic <c>{Binding}</c> and so can't reach the
/// <c>x:Bind</c> helpers on <c>MainWindow</c>. Marked <c>partial</c> like the
/// other XAML-instantiated types in the Shell.
/// </summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}
