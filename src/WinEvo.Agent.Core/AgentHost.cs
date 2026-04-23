using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Actions.Operations;
using WinEvo.Ipc;

namespace WinEvo.Agent.Core;

/// <summary>
/// Agent-side loop: hosts a named-pipe server, accepts one client at a time
/// (the Shell), and dispatches inbound <see cref="PipeMessage"/> requests to
/// the <see cref="ActionExecutor"/>. When the pipe closes, the agent exits.
/// </summary>
public sealed class AgentHost
{
    private readonly string _pipeName;
    private readonly OperationCatalog _operations;
    private readonly ActionExecutor _executor;

    public AgentHost(string pipeName, OperationCatalog? operations = null)
    {
        _pipeName = pipeName;
        _operations = operations ?? OperationCatalog.Default();
        _executor = new ActionExecutor(_operations);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        NamedPipeServerStream server;
        try
        {
            server = CreatePipeServer(_pipeName);
        }
        catch (Exception ex)
        {
            AgentLog.WriteException($"failed to create pipe '{_pipeName}'", ex);
            throw;
        }

        using (server)
        {
            AgentLog.Write($"listening on pipe '{_pipeName}' (elevated={IsElevated()})");
            Console.WriteLine($"agent: listening on pipe '{_pipeName}' (elevated={IsElevated()})");
            await RunLoopAsync(server, ct).ConfigureAwait(false);
        }
    }

    private async Task RunLoopAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
        Console.WriteLine("agent: client connected");

        try
        {
            while (server.IsConnected)
            {
                ct.ThrowIfCancellationRequested();
                var frame = await PipeFraming.ReadFrameAsync(server, ct).ConfigureAwait(false);
                if (frame is null) break;

                PipeMessage response;
                try
                {
                    var request = PipeMessageSerializer.Deserialize(frame);
                    response = await HandleAsync(request, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    response = new ErrorResponse { Message = $"{ex.GetType().Name}: {ex.Message}" };
                }

                var bytes = PipeMessageSerializer.Serialize(response);
                await PipeFraming.WriteFrameAsync(server, bytes, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* graceful */ }
        finally
        {
            Console.WriteLine("agent: client disconnected, shutting down");
        }
    }

    private async Task<PipeMessage> HandleAsync(PipeMessage request, CancellationToken ct) => request switch
    {
        HandshakeRequest hs => new HandshakeResponse
        {
            RequestId = hs.RequestId,
            AgentVersion = typeof(AgentHost).Assembly.GetName().Version?.ToString() ?? "0.1.0",
            AgentProtocolVersion = 1,
            SupportedOperations = _operations.SupportedIds.ToArray(),
        },
        ExecuteRequest exec => await HandleExecuteAsync(exec, ct).ConfigureAwait(false),
        _ => new ErrorResponse { RequestId = request.RequestId, Message = "unsupported message type" },
    };

    /// <summary>
    /// Creates the named pipe with an explicit security descriptor so an
    /// unelevated Shell (Medium integrity) can talk to a pipe created by an
    /// elevated agent (High integrity). Without this, Windows' Mandatory
    /// Integrity Control blocks writes from the Shell and the handshake hangs.
    /// Falls back to DACL-only if the kernel refuses the SACL portion.
    /// </summary>
    private static NamedPipeServerStream CreatePipeServer(string pipeName)
    {
        var includeMandatoryLabel = IsElevated();
        if (includeMandatoryLabel)
        {
            var enabled = Privileges.TryEnable(Privileges.SeSecurity);
            AgentLog.Write($"SeSecurityPrivilege enable: {enabled}");
            if (!enabled) includeMandatoryLabel = false;
        }

        try
        {
            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity: BuildPipeSecurity(includeMandatoryLabel));
        }
        catch (IOException ex) when (includeMandatoryLabel)
        {
            // If setting the mandatory label was accepted at the .NET level
            // but denied by the kernel, retry without the SACL. Same-user DACL
            // still lets the Shell connect in many environments.
            AgentLog.WriteException("pipe create with SACL failed; retrying DACL-only", ex);
            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity: BuildPipeSecurity(includeMandatoryLabel: false));
        }
    }

    private static PipeSecurity BuildPipeSecurity(bool includeMandatoryLabel)
    {
        var security = new PipeSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        var currentUser = identity.User
            ?? throw new InvalidOperationException("cannot resolve current user SID");

        // Grant the current user full duplex access — enough for same-user
        // Shell (any integrity level) to connect and exchange messages.
        security.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        if (includeMandatoryLabel)
        {
            // Drop the pipe's mandatory integrity label to Medium so an unelevated
            // Shell can write to a pipe created by this elevated agent.
            var sddl = $"D:(A;;GA;;;{currentUser.Value})S:(ML;;NW;;;ME)";
            security.SetSecurityDescriptorSddlForm(
                sddl,
                AccessControlSections.Access | AccessControlSections.Audit);
            AgentLog.Write("pipe SACL: mandatory label set to Medium");
        }

        return security;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private async Task<PipeMessage> HandleExecuteAsync(ExecuteRequest request, CancellationToken ct)
    {
        var manifest = ManifestLoader.Parse(request.Manifest);

        var rawParams = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in request.Parameters)
            rawParams[k] = JsonSerializer.SerializeToElement(v);

        var bound = ActionExecutor.BindParameters(manifest, rawParams);
        return await _executor.ExecuteAsync(manifest, bound, request.RequestId, ct).ConfigureAwait(false);
    }
}
