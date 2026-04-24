using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinEvo.Shell.Core.Services;
using WinEvo.Shell.Core.ViewModels;
using WinEvo.Shell.Services;

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

    private bool _teardownStarted;
    private bool _teardownCompleted;

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
        var strings = new StringBundle(StringBundle.ResolveDefaultRoot());

        // Lazy XamlRoot capture: the lambda reads _window at call time, not at
        // construction time
        var confirmation = new ConfirmationDialogService(() => _window?.Content.XamlRoot);

        // TODO: detect system language / user preference instead of hard-coding English.
        var viewModel = new MainViewModel(
            catalog, _agentLauncher, language: "en", dispatcherQueue, confirmation, strings);

        _window = new MainWindow(viewModel);
        _window.AppWindow.Closing += OnAppWindowClosing;
        _window.Activate();

        // Kick off catalog + agent startup on the UI dispatcher so the VM's
        // UI-touching updates run on the correct thread.
        dispatcherQueue.TryEnqueue(async () =>
        {
            try { await viewModel.InitialiseCommand.ExecuteAsync(null); }
            catch { /* VM reports failures via AgentStatus */ }
        });
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Second pass: the window is allowed to close.
        if (_teardownCompleted) return;

        // Block the close *synchronously* before the first await. The framework
        // reads args.Cancel at the first suspension point, so setting it after an
        // await would let the window close while disposal is still pending.
        args.Cancel = true;

        if (_teardownStarted) return;
        _teardownStarted = true;

        // Broker mode: if an action is running, confirm before killing the agent,
        // since teardown ends the child operation abruptly.
        // TODO: aggregate IsRunning across all in-flight actions on MainViewModel
        // so executions whose Detail VM was replaced by switching selection are
        // also observed here, not just the currently-selected one.
        if (_window?.ViewModel.Detail?.IsRunning == true
            && !await ConfirmStopRunningActionAsync())
        {
            // User chose to let the action finish.
            _teardownStarted = false;
            return;
        }

        try
        {
            if (_agentLauncher is not null)
            {
                await _agentLauncher.DisposeAsync();
                _agentLauncher = null;
            }
        }
        finally
        {
            // Re-trigger the close
            _teardownCompleted = true;
            _window?.Close();
        }
    }

    private async Task<bool> ConfirmStopRunningActionAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Action in progress",
            Content = "An action is still running. Closing will stop the agent and "
                    + "may leave the system in a partial state. Continue?",
            PrimaryButtonText = "Stop and close",
            CloseButtonText = "Keep running",
            DefaultButton = ContentDialogButton.Close,
            // XamlRoot is required for ContentDialog in a WinUI 3 desktop app;
            // without it the dialog fails to present and ShowAsync throws.
            XamlRoot = _window!.Content.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
