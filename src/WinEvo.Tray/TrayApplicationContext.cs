namespace WinEvo.Tray;

/// <summary>
/// TODO:
///   - Connect to the agent over IPC to show running-task status in the menu.
///   - Launch WinEvo.exe (the Shell) on "Open".
///   - Present a Quit dialog when actions are running (Wait / Cancel / Leave-running).
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;

    public TrayApplicationContext()
    {
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WinEvo",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open WinEvo", image: null, (_, _) => { /* TODO: Process.Start("WinEvo.exe") */ });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", image: null, (_, _) => ExitThread());
        return menu;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
        base.Dispose(disposing);
    }
}
