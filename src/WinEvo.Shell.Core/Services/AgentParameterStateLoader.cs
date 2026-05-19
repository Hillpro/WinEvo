using System.Text.Json;
using System.Text.Json.Nodes;
using WinEvo.ActionModel;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Default <see cref="IParameterStateLoader"/>: sends a single-step probe
/// manifest containing a <c>registry-read</c> through the broker agent and
/// parses the <c>{ present, kind, data }</c> JSON shape that
/// <c>RegistryReadOperation</c> emits. Returns <see langword="null"/> when the
/// agent isn't connected, the read fails, or the value is absent — callers
/// fall back to <c>parameter.Default</c>.
/// </summary>
public sealed class AgentParameterStateLoader : IParameterStateLoader
{
    private static readonly IReadOnlyDictionary<string, JsonNode?> s_noParameters
        = new Dictionary<string, JsonNode?>();

    private readonly AgentLauncher _launcher;

    public AgentParameterStateLoader(AgentLauncher launcher)
    {
        _launcher = launcher;
    }

    public async Task<JsonElement?> ReadAsync(ParameterStateProbe probe, CancellationToken ct)
    {
        var client = _launcher.Client;
        if (client is null || !client.IsConnected)
            return null;

        ExecutionResponse response;
        try
        {
            response = await client.ExecuteAsync(BuildProbeManifest(probe), s_noParameters, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }

        var step = response.StepResults.FirstOrDefault(s => s.Operation == "registry-read");
        if (step is null || !step.Success || string.IsNullOrEmpty(step.Message))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(step.Message);
            var root = doc.RootElement;
            if (!root.TryGetProperty("present", out var present) || !present.GetBoolean())
                return null;
            if (!root.TryGetProperty("data", out var data))
                return null;
            return data.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Wraps the registry-read step in the minimum manifest shape that
    /// <c>ManifestLoader</c> + the agent's <c>ActionExecutor</c> accept. The
    /// synthetic <c>id</c> (<c>internal.state-probe</c>) and the <c>category</c>
    /// pin only exist because the current parser is shape-driven and requires
    /// those fields; the agent treats this manifest as any other.
    ///
    /// <para>
    /// Future-proofing note: if JSON-Schema validation is wired on the agent
    /// (per the <c>ManifestLoader</c> TODO), this synthetic manifest will fail
    /// validation — <c>internal</c> is not in the category enum and the id
    /// pattern was authored for user-facing actions. Two options when that
    /// happens: (a) widen the schema to admit an <c>internal</c> category for
    /// system-injected manifests, or (b) introduce a dedicated IPC message
    /// type (e.g. <c>ReadStateRequest</c>) so probes bypass the manifest
    /// pipeline entirely. Option (b) is the cleaner end state but bigger;
    /// option (a) keeps the current reuse and is one schema edit.
    /// </para>
    /// </summary>
    private static JsonObject BuildProbeManifest(ParameterStateProbe probe)
        => new()
        {
            ["id"] = "internal.state-probe",
            ["version"] = "1.0.0",
            ["name"] = "state probe",
            ["category"] = "customization",
            ["execution"] = new JsonObject
            {
                ["steps"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "probe",
                        ["operation"] = "registry-read",
                        ["key"] = probe.Key,
                        ["value"] = probe.Value,
                    },
                },
            },
        };
}
