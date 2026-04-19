namespace WinEvo.Tray;

/// <summary>
/// WinEvo tray helper. Separate WinForms process so the main WinUI 3 Shell
/// can fully exit on window close while the tray icon stays resident (~25 MB).
/// Clicking the tray icon relaunches the Shell via Process.Start.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
