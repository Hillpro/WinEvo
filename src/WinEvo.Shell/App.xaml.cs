using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinEvo.Shell.Core.Services;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell;

/// <summary>
/// WinUI 3 application entry point. Resolves the action catalog and agent
/// launcher, constructs the main view model, and hands it to MainWindow.
/// Agent connection is kicked off asynchronously via the VM's InitialiseCommand.
/// </summary>
public partial class App : Application
{
    private AgentLauncher? _agentLauncher;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WinUI 3 does not automatically install a SynchronizationContext for the
        // dispatcher thread in all unpackaged bootstrap scenarios. Without it,
        // `await … ConfigureAwait(true)` does not reliably resume on the UI thread
        // and any subsequent UI-object access throws RPC_E_WRONG_THREAD.
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherQueueSynchronizationContext(dispatcherQueue));

        var catalog = new ActionCatalog(ActionCatalog.ResolveDefaultRoot());
        _agentLauncher = new AgentLauncher(AgentLauncher.ResolveDefaultAgentPath());

        // TODO: detect system language / user preference instead of hard-coding English.
        var viewModel = new MainViewModel(catalog, _agentLauncher, language: "en", dispatcherQueue);

        _window = new MainWindow(viewModel);
        _window.Closed += OnWindowClosed;
        _window.Activate();

        // Kick off catalog + agent startup on the UI dispatcher so the VM's
        // UI-touching updates run on the correct thread.
        dispatcherQueue.TryEnqueue(async () =>
        {
            try { await viewModel.InitialiseCommand.ExecuteAsync(null); }
            catch { /* VM reports failures via AgentStatus */ }
        });
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_agentLauncher is not null)
        {
            await _agentLauncher.DisposeAsync();
            _agentLauncher = null;
        }
    }
}
