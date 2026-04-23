using System.Security.Principal;
using WinEvo.Agent.Core;
using WinEvo.Ipc;

namespace WinEvo.Agent;

/// <summary>
/// WinEvo agent entry point. Same binary, multiple modes selected via argv:
///   --service      : run as a Windows Service (LocalSystem). TODO: not wired yet.
///   --broker       : run as a UAC-elevated user-session process; exits when the client disconnects.
///   --install      : install the Windows Service (requires admin). TODO.
///   --uninstall    : remove the Windows Service (requires admin). TODO.
/// Default mode (no args) is broker. Only broker is implemented so far.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var mode = ParseMode(args);

        using var identity = WindowsIdentity.GetCurrent();
        var elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        AgentLog.Write($"agent started — mode={mode}, elevated={elevated}, args=[{string.Join(' ', args)}]");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            return mode switch
            {
                AgentMode.Broker => await RunBrokerAsync(cts.Token).ConfigureAwait(false),
                _ => Fail($"mode '{mode}' not implemented yet"),
            };
        }
        catch (Exception ex)
        {
            AgentLog.WriteException("fatal in Main", ex);
            return 1;
        }
    }

    private static async Task<int> RunBrokerAsync(CancellationToken ct)
    {
        var host = new AgentHost(PipeNames.UserBroker);
        try
        {
            await host.RunAsync(ct).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            AgentLog.WriteException("fatal in broker", ex);
            return 1;
        }
    }

    private static int Fail(string message)
    {
        AgentLog.Write($"startup failure: {message}");
        return 2;
    }

    private static AgentMode ParseMode(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "--service": return AgentMode.Service;
                case "--broker": return AgentMode.Broker;
                case "--install": return AgentMode.Install;
                case "--uninstall": return AgentMode.Uninstall;
            }
        }
        return AgentMode.Broker;
    }

    private enum AgentMode { Broker, Service, Install, Uninstall }
}
