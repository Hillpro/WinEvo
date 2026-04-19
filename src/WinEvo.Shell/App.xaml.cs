using Microsoft.UI.Xaml;

namespace WinEvo.Shell;

/// <summary>
/// WinUI 3 application entry point. TODO: wire the DI container, IPC client,
/// and viewmodel services in Shell.Core.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
