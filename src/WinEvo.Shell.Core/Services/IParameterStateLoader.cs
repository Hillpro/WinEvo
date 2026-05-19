using System.Text.Json;
using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Reads the live system value that backs a parameter's <see cref="ParameterStateProbe"/>.
/// Implementations marshal the read through whatever transport they own
/// (today: a registry-read step against the broker agent). On any failure path —
/// agent down, key absent, parse error — the contract is to return
/// <see langword="null"/> so the caller can fall back to its own default.
/// </summary>
public interface IParameterStateLoader
{
    Task<JsonElement?> ReadAsync(ParameterStateProbe probe, CancellationToken ct);
}
