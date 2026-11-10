using System.Collections.ObjectModel;

namespace WinEvo.Actions.Abstractions;

/// <summary>
/// An action that is a single on/off switch over a piece of system state.
/// The switch is the action's only input: this base class declares it on the
/// descriptor, so a toggle action cannot exist without exactly one boolean
/// parameter for the Shell's <c>ToggleSwitch</c> to bind to.
/// </summary>
public abstract class ToggleActionDefinition : ActionDefinition
{
    /// <summary>Id of the boolean parameter carrying the requested switch position.</summary>
    public const string IsOnParameterId = "isOn";

    /// <param name="descriptor">The action's identity and display text. Must not declare parameters.</param>
    /// <param name="switchLabel">Label shown next to the switch.</param>
    /// <param name="switchDescription">Optional explanation of what on and off mean.</param>
    /// <param name="isOnWhenUnknown">Switch position shown when the live state cannot be read.</param>
    protected ToggleActionDefinition(
        ActionDescriptor descriptor,
        LocalizableText switchLabel,
        LocalizableText? switchDescription = null,
        bool isOnWhenUnknown = false)
    {
        if (descriptor.Parameters.Count != 0)
        {
            throw new ArgumentException(
                $"toggle action '{descriptor.Id}' must not declare parameters; the switch is its only input",
                nameof(descriptor));
        }

        Descriptor = descriptor with
        {
            Interaction = InteractionMode.Toggle,
            Parameters =
            [
                new BooleanParameter
                {
                    Id = IsOnParameterId,
                    Name = switchLabel,
                    Description = switchDescription,
                    Default = isOnWhenUnknown,
                },
            ],
        };
    }

    public sealed override ActionDescriptor Descriptor { get; }

    public sealed override Task ExecuteAsync(ActionExecution execution, CancellationToken cancellationToken)
        => ApplyAsync(execution.GetBoolean(IsOnParameterId), execution, cancellationToken);

    public sealed override async Task<IReadOnlyDictionary<string, object?>> ReadStateAsync(CancellationToken cancellationToken)
    {
        var isOn = await ReadIsOnAsync(cancellationToken).ConfigureAwait(false);
        return isOn is null
            ? ReadOnlyDictionary<string, object?>.Empty
            : new Dictionary<string, object?> { [IsOnParameterId] = isOn.Value };
    }

    /// <summary>Current switch position on this machine, or <see langword="null"/> when it cannot be determined.</summary>
    protected abstract Task<bool?> ReadIsOnAsync(CancellationToken cancellationToken);

    /// <summary>Moves the system to the requested switch position.</summary>
    protected abstract Task ApplyAsync(bool isOn, ActionExecution execution, CancellationToken cancellationToken);
}
