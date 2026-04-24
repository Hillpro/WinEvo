using System.Text.Json;

namespace WinEvo.ActionModel;

/// <summary>
/// Root of a parsed action manifest. See actions/schemas/action.schema.json
/// for the source of truth; this type captures the subset of fields the
/// runtime currently uses. Unknown fields are intentionally ignored.
/// </summary>
public sealed class ActionManifest
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? Icon { get; init; }
    public string? Author { get; init; }

    public Requirements Requirements { get; init; } = new();
    public IReadOnlyList<ActionWarning> Warnings { get; init; } = [];
    public IReadOnlyList<Parameter> Parameters { get; init; } = [];
    public Execution Execution { get; init; } = new();
    public LocalizationMap Localization { get; init; } = [];

    /// <summary>
    /// Authoritative JSON source of the manifest, preserved at parse time so
    /// the Shell can forward it to the agent verbatim.
    /// </summary>
    public required string RawJson { get; init; }

    /// <summary>Returns the localized name for the requested language, falling back to the English base.</summary>
    public string GetLocalizedName(string? language)
    {
        if (language is not null
            && Localization.TryGetValue(language, out var entry)
            && entry.Name is not null)
        {
            return entry.Name;
        }
        return Name;
    }

    /// <summary>Returns the localized description, falling back to the English base (which may be null).</summary>
    public string? GetLocalizedDescription(string? language)
    {
        if (language is not null
            && Localization.TryGetValue(language, out var entry)
            && entry.Description is not null)
        {
            return entry.Description;
        }
        return Description;
    }
}

public sealed class Requirements
{
    public ElevationRequirement Elevation { get; init; } = ElevationRequirement.NotRequired;
    public int MinWindowsBuild { get; init; }
    public IReadOnlyList<string> Architectures { get; init; } = [];
}

public enum ElevationRequirement { NotRequired, Optional, Required }

public sealed class ActionWarning
{
    public required WarningSeverity Severity { get; init; }
    public required string Key { get; init; }
    public IReadOnlyDictionary<string, string> Tokens { get; init; }
        = new Dictionary<string, string>();
}

public enum WarningSeverity { Info, Warning, Danger, Critical }

public sealed class Parameter
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public JsonElement? Default { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
    public IReadOnlyList<string>? Choices { get; init; }
}

public sealed class Execution
{
    public ExecutionMode Mode { get; init; } = ExecutionMode.Sequential;
    public bool CreateRestorePoint { get; init; }
    public IReadOnlyList<ActionStep> Steps { get; init; } = [];
}

/// <summary>
/// How an action's steps are scheduled against each other.
/// Maps to the "mode" property in a manifest's "execution" object.
/// </summary>
public enum ExecutionMode
{
    /// <summary>Steps run in declaration order; stop on the first failure. (Schema value: "sequential")</summary>
    Sequential,

    /// <summary>Steps run in declaration order; failures are logged but execution continues. (Schema value: "sequential-continue-on-error")</summary>
    SequentialContinueOnError,
}

/// <summary>Base class for items in <see cref="Execution.Steps"/>.</summary>
public abstract class ActionStep
{
    public string? Id { get; init; }
    public bool ContinueOnError { get; init; }
}

/// <summary>Step that invokes an agent-side operation (e.g. registry-set).</summary>
public sealed class OperationStep : ActionStep
{
    public required string Operation { get; init; }

    /// <summary>
    /// Raw JSON of the step, preserved so each operation can read whatever
    /// keys it understands (e.g. <c>path</c>, <c>args</c>, <c>key</c>,
    /// <c>value</c>, <c>data</c>, <c>timeout</c>).
    /// </summary>
    public required JsonElement Properties { get; init; }
}

/// <summary>Step that invokes another action manifest. TODO: executor does not expand these yet.</summary>
public sealed class SubActionStep : ActionStep
{
    public required string Ref { get; init; }
    public required string MinVersion { get; init; }
    public IReadOnlyDictionary<string, JsonElement> Parameters { get; init; }
        = new Dictionary<string, JsonElement>();
}

public sealed class LocalizationMap : Dictionary<string, LocalizationEntry>
{
}

public sealed class LocalizationEntry
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, LocalizationParameterEntry>? Parameters { get; init; }
}

public sealed class LocalizationParameterEntry
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}
