using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using WinEvo.Shell.Core;
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
        // Wire crash-logging hooks before InitializeComponent so a XAML parser
        // exception during startup still leaves an artifact behind.
        //
        // Raw managed crashes (AppDomain)
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        // GC-finalised unobserved Task faults (TaskScheduler)
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        // WinUI dispatcher (Application)
        UnhandledException += OnApplicationUnhandledException;

        ShellLog.Write($"shell starting (log: {ShellLog.FilePath})");

        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var primary = await HandleSecondaryLaunchAsync();
        primary.Activated += (_, e) => OnSecondaryActivation(e);

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

        var stateLoader = new AgentParameterStateLoader(_agentLauncher);
        var parameterFactory = new ParameterInputFactory(new SystemDriveProvider(), stateLoader, dispatcherQueue);

        // TODO: detect system language / user preference instead of hard-coding English.
        var viewModel = new MainViewModel(
            catalog, _agentLauncher, language: "en", dispatcherQueue, confirmation, strings, parameterFactory);

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
            // Dispose the current Detail VM first so its in-flight state
            // probes are cancelled before the agent pipe is torn down.
            _window?.ViewModel.Detail?.Dispose();
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
    
    private void OnApplicationUnhandledException(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        ShellLog.WriteException(
            $"Application.UnhandledException (handled={e.Handled})",
            e.Exception);
        // Don't set e.Handled = true
        // Let the framework's default crash behavior continue.
    }

    private static void OnDomainUnhandledException(
        object sender,
        System.UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception
            ?? new InvalidOperationException(e.ExceptionObject?.ToString() ?? "(null)");
        ShellLog.WriteException(
            $"AppDomain.UnhandledException (terminating={e.IsTerminating})",
            ex);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        ShellLog.WriteException("TaskScheduler.UnobservedTaskException", e.Exception);
        // Deliberately do not call e.SetObserved().
        // Unobserved exceptions are fatal by default
    }

    /// <summary>
    /// Single-instance gate. Returns the primary <see cref="AppInstance"/>
    /// the caller should subscribe to for redirected activations. If the
    /// current process is a secondary launch, this method forwards the
    /// activation to the primary and terminates the process — it does not
    /// return on that path. The key is session-scoped so each user session
    /// has its own primary; a future tray-icon launcher reuses this key to
    /// surface the Shell.
    /// </summary>
    private static async Task<AppInstance> HandleSecondaryLaunchAsync()
    {
        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var key = $"WinEvo.Shell.User.{System.Diagnostics.Process.GetCurrentProcess().SessionId}";
        var primary = AppInstance.FindOrRegisterForKey(key);
        if (!primary.IsCurrent)
        {
            ShellLog.Write($"secondary launch detected; redirecting to primary (pid={primary.ProcessId})");
            await primary.RedirectActivationToAsync(activatedArgs);
            // Application.Current.Exit() is unreliable mid-launch; Microsoft's
            // own AppLifecycle sample uses Process.Kill for the same reason.
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        return primary;
    }

    /// <summary>
    /// Called when a secondary launch redirects its activation to this
    /// primary instance. Restores the window if minimized and brings it
    /// to the foreground.
    /// </summary>
    internal void OnSecondaryActivation(AppActivationArguments args)
    {
        if (_window is null) return;
        _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter
                && presenter.State == OverlappedPresenterState.Minimized)
            {
                presenter.Restore();
            }
            _window.AppWindow.Show();
            // Foreground rights default to the originally-foreground process under
            // Windows focus-stealing rules; AppInstance.RedirectActivationToAsync
            // transfers them, but make the intent explicit.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            SetForegroundWindow(hwnd);
        });
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
