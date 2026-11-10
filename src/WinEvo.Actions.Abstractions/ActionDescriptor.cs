namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Everything the Shell needs to list an action and render its inputs:
/// identity, display text, declared parameters, warnings and elevation need.
/// Plain data: the behaviour lives on the <see cref="ActionDefinition"/> that
/// owns it.
/// </summary>
public sealed record ActionDescriptor
{
    /// <summary>Stable identifier, <c>category.action-name</c>. Sent over IPC to select the action to run.</summary>
    public required string Id { get; init; }

    public required ActionCategory Category { get; init; }
    public required LocalizableText Name { get; init; }
    public LocalizableText? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? Icon { get; init; }

    /// <summary>GitHub handle of the contributor who wrote the action.</summary>
    public string? Author { get; init; }

    public ElevationRequirement Elevation { get; init; } = ElevationRequirement.NotRequired;
    public InteractionMode Interaction { get; init; } = InteractionMode.Button;
    public IReadOnlyList<ActionWarning> Warnings { get; init; } = [];
    public IReadOnlyList<Parameter> Parameters { get; init; } = [];
}

public enum ElevationRequirement { NotRequired, Optional, Required }

/// <summary>How an action surfaces in the UI.</summary>
public enum InteractionMode
{
    /// <summary>Classic one-shot action: parameter panel + Execute button.</summary>
    Button,

    /// <summary>Stateful on/off switch: a single ToggleSwitch that fires on flip.</summary>
    Toggle,
}
