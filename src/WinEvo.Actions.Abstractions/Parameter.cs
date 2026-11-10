namespace WinEvo.Actions.Abstractions;

/// <summary>
/// One input an action asks the user for. Common fields live here;
/// type-specific metadata (min/max, choices, drive-type filter, …) and the
/// typed default live on the concrete subclass. The Shell picks its input
/// widget from the subclass.
/// </summary>
public abstract class Parameter
{
    /// <summary>Key under which the action reads the value from its <see cref="ActionExecution"/>.</summary>
    public required string Id { get; init; }

    public LocalizableText? Name { get; init; }
    public LocalizableText? Description { get; init; }
    public bool Required { get; init; }

    /// <summary>
    /// Value the action receives when the caller supplies none, or
    /// <see langword="null"/> when the parameter has no default.
    /// </summary>
    public abstract object? DefaultValue { get; }
}

public sealed class StringParameter : Parameter
{
    public string? Default { get; init; }

    public override object? DefaultValue => Default;
}

public sealed class BooleanParameter : Parameter
{
    public bool Default { get; init; }

    public override object? DefaultValue => Default;
}

public sealed class IntegerParameter : Parameter
{
    public long? Default { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }

    public override object? DefaultValue => Default;
}

public sealed class EnumParameter : Parameter
{
    public required IReadOnlyList<string> Choices { get; init; }

    /// <summary>One of <see cref="Choices"/>; the first choice is preselected when absent.</summary>
    public string? Default { get; init; }

    public override object? DefaultValue => Default;
}

public sealed class DriveParameter : Parameter
{
    /// <summary>
    /// Drive types the picker should expose (e.g. <c>fixed</c>, <c>removable</c>).
    /// <see langword="null"/> means no filter: show all mounted drives.
    /// </summary>
    public IReadOnlyList<string>? AllowedDriveTypes { get; init; }

    /// <summary>Always <see langword="null"/>: which drives exist is only known on the user's machine.</summary>
    public override object? DefaultValue => null;
}
