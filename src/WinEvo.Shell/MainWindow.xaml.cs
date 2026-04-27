using Microsoft.UI.Xaml;
using WinEvo.Shell.Core.ViewModels;
using WinEvo.Shell.Views;

namespace WinEvo.Shell;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "WinEvo";
    }

    public MainViewModel ViewModel { get; }

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
    }

    // --- x:Bind helpers for Visibility; kept instance methods so generated code can find them.

    public Visibility DetailVisibility(ActionDetailViewModel? detail)
        => detail is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailInverseVisibility(ActionDetailViewModel? detail)
        => detail is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ElevationVisibility(ActionDetailViewModel? detail)
        => detail?.Item.RequiresElevation == true ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ParametersVisibility(ActionDetailViewModel? detail)
        => detail?.Parameters.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ResultDetailVisibility(ActionDetailViewModel? detail)
        => string.IsNullOrEmpty(detail?.ResultDetail) ? Visibility.Collapsed : Visibility.Visible;
}
