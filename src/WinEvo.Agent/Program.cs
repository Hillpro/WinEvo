using Microsoft.Extensions.Hosting;

namespace WinEvo.Agent;

/// <summary>
/// WinEvo agent entry point. Same binary, multiple modes selected via argv:
///   --service      : run as a Windows Service (LocalSystem).
///   --broker       : run as a UAC-elevated user-session process; exits when idle.
///   --install      : install the Windows Service (requires admin).
///   --uninstall    : remove the Windows Service (requires admin).
/// TODO: mode dispatch, IPC bootstrap, and action runtime wiring.
/// </summary>
internal static class Program
{
    private const int ExitCodeSuccess = 0;

    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // TODO: parse mode from args, wire gRPC server on named pipe,
        // register operations, register service lifetime if running as Windows Service.

        using var host = builder.Build();
        await host.RunAsync();
        return ExitCodeSuccess;
    }
}
