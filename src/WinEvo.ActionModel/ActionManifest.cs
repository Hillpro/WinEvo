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
    /// How the action surfaces in the UI. <see cref="InteractionMode.Button"/>
    /// is the default and matches the original Execute-button flow.
    /// </summary>
    public InteractionMode Interaction { get; init; } = InteractionMode.Button;

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

/// <summary>
/// One declared parameter on an action manifest. Common fields live here;
/// type-specific metadata (min/max, choices, drive-type filter, …) lives on
/// the concrete subclass. <see cref="ManifestLoader"/> picks the subclass
/// based on the manifest's <c>type</c> string.
/// </summary>
public abstract class Parameter
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public JsonElement? Default { get; init; }

    /// <summary>
    /// Optional probe that seeds this parameter's initial value from live
    /// system state. When the read succeeds, it overrides
    /// <see cref="Default"/>. When the read fails or the value is absent,
    /// <see cref="Default"/> remains the seed.
    /// </summary>
    public ParameterStateProbe? State { get; init; }
}

/// <summary>
/// Catch-all parameter for types without dedicated metadata today: schema's
/// <c>string</c>, <c>wifi-profile</c>, <c>file-path</c>, <c>directory-path</c>,
/// <c>service-name</c>. When one of these grows type-specific fields, promote
/// it to its own subclass.
/// </summary>
public sealed class StringParameter : Parameter
{
}

public sealed class BooleanParameter : Parameter
{
}

public sealed class IntegerParameter : Parameter
{
    public int? Min { get; init; }
    public int? Max { get; init; }
}

public sealed class EnumParameter : Parameter
{
    public required IReadOnlyList<string> Choices { get; init; }
}

public sealed class DriveParameter : Parameter
{
    /// <summary>
    /// Drive types the picker should expose (e.g. <c>fixed</c>, <c>removable</c>).
    /// Maps to the manifest's <c>filter.driveType</c> array. <see langword="null"/>
    /// means no filter — show all mounted drives.
    /// </summary>
    public IReadOnlyList<string>? AllowedDriveTypes { get; init; }
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
    /// Opaque JSON payload carrying the operation's per-type properties
    /// (<c>path</c>, <c>args</c>, <c>key</c>, <c>value</c>, <c>data</c>,
    /// <c>timeout</c>, …). Consumed exclusively by <c>IOperationParser.Parse</c>
    /// downstream in <c>Actions.Abstractions</c>; no other consumer should
    /// read from this field. Every operation's typed shape lives on its
    /// <c>ActionOperation</c> subclass after parsing. If you find yourself
    /// reaching into this <see cref="JsonElement"/> from inside an operation
    /// body (or anywhere else in the execution path), you're bypassing the
    /// typed seam — promote the field onto the operation's subclass instead.
    /// </summary>
    public required JsonElement RawProperties { get; init; }
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

/// <summary>How an action surfaces in the UI.</summary>
public enum InteractionMode
{
    /// <summary>Classic one-shot action: parameter panel + Execute button.</summary>
    Button,

    /// <summary>Stateful on/off switch: a single ToggleSwitch that fires on flip.</summary>
    Toggle,
}

/// <summary>
/// Declares how the Shell reads the live system value that seeds a parameter's
/// initial value.
/// </summary>
public sealed class ParameterStateProbe
{
    /// <summary>Full registry path (hive + subkey), same shape as <c>registry-set</c>'s <c>key</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Name of the value to read.</summary>
    public required string Value { get; init; }

    /// <summary>Registry value kind (<c>DWORD</c>, <c>QWORD</c>, <c>STRING</c>, …). Used to validate the read result.</summary>
    public required string Type { get; init; }

    /// <summary>
    /// For boolean parameters: the registry data value that maps to
    /// <c>true</c>; any other present value maps to <c>false</c>. Defaults to
    /// a JSON number <c>1</c> when the manifest omits it — matches the common
    /// "enabled = 1" Windows convention. Manifests with inverse semantics
    /// (e.g. <c>DisableXyz = 1</c> meaning the feature is off) should set
    /// <c>trueWhen: 0</c>.
    /// </summary>
    public required JsonElement TrueWhen { get; init; }
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
