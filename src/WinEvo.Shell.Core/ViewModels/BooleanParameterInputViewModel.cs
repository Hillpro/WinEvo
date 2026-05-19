using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Picker for <see cref="BooleanParameter"/> — renders as a <c>ToggleSwitch</c>.
/// Booleans always have a value, so <see cref="HasValue"/> is always true.
///
/// <para>
/// When the parameter declares a <see cref="ParameterStateProbe"/> and a
/// <see cref="IParameterStateLoader"/> is injected, the VM reads the live
/// system value at construction and overrides <see cref="Value"/> on success.
/// Hydration uses the internal setter path so it does NOT raise
/// <see cref="UserChangedValue"/>; only XAML-driven flips do. That lets
/// <see cref="ToggleInteractionController"/> distinguish "Windows currently
/// looks like this" from "the user just flipped me, run the action."
/// </para>
/// </summary>
public sealed partial class BooleanParameterInputViewModel : ParameterInputViewModel, IDisposable
{
    private readonly IParameterStateLoader? _stateLoader;
    private readonly DispatcherQueue? _dispatcher;
    private readonly CancellationTokenSource? _stateLoadCts;
    private bool _value;

    public BooleanParameterInputViewModel(
        BooleanParameter parameter,
        string? language,
        IParameterStateLoader? stateLoader = null,
        DispatcherQueue? dispatcher = null)
        : base(parameter, language)
    {
        _stateLoader = stateLoader;
        _dispatcher = dispatcher;

        _value = parameter.Default is { } def
            && (def.ValueKind == JsonValueKind.True || def.ValueKind == JsonValueKind.False)
            && def.GetBoolean();

        if (parameter.State is { } probe && stateLoader is not null && dispatcher is not null)
        {
            _stateLoadCts = new CancellationTokenSource();
            _ = LoadStateAsync(probe, _stateLoadCts.Token);
        }
    }

    /// <summary>
    /// Cancels any in-flight state probe and releases its <see cref="CancellationTokenSource"/>.
    /// Called by the owning <see cref="ActionDetailViewModel"/> when the user
    /// switches selection or closes the window, so probes don't keep posting
    /// to dead VMs.
    /// </summary>
    public void Dispose()
    {
        _stateLoadCts?.Cancel();
        _stateLoadCts?.Dispose();
    }

    /// <summary>
    /// Toggle value. Setting this via XAML's TwoWay binding raises
    /// <see cref="UserChangedValue"/>; hydration from <see cref="LoadStateAsync"/>
    /// updates the backing field directly and does not.
    /// </summary>
    public bool Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            UserChangedValue?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raised when <see cref="Value"/> is changed via the public setter
    /// (i.e. XAML two-way binding). Hydration from a state probe does NOT raise
    /// this event — see <see cref="LoadStateAsync"/>.
    /// </summary>
    public event EventHandler? UserChangedValue;

    [ObservableProperty]
    public partial bool IsLoadingState { get; set; }

    public override bool HasValue => true;

    public override JsonNode? ToJsonValue() => JsonValue.Create(Value);

    private async Task LoadStateAsync(ParameterStateProbe probe, CancellationToken ct)
    {
        // Constructor guarantees both are non-null before scheduling LoadStateAsync.
        var dispatcher = _dispatcher!;
        var loader = _stateLoader!;

        await dispatcher.RunOnUiAsync(() => IsLoadingState = true).ConfigureAwait(false);
        try
        {
            var data = await loader.ReadAsync(probe, ct).ConfigureAwait(false);
            if (data is { } d)
            {
                var resolved = JsonValuesEqual(d, probe.TrueWhen);
                await dispatcher.RunOnUiAsync(() => SetValueWithoutNotifyingUser(resolved)).ConfigureAwait(false);
            }
        }
        finally
        {
            await dispatcher.RunOnUiAsync(() => IsLoadingState = false).ConfigureAwait(false);
        }
    }

    private void SetValueWithoutNotifyingUser(bool value)
    {
        if (_value == value) return;
        _value = value;
        OnPropertyChanged(nameof(Value));
        // Intentionally not raising UserChangedValue — this is hydration, not user input.
    }

    private static bool JsonValuesEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            // Numbers in the manifest can be authored as either JSON numbers
            // or strings; coerce so {"trueWhen": 1} and {"trueWhen": "1"} both work.
            if ((a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.String)
                || (a.ValueKind == JsonValueKind.String && b.ValueKind == JsonValueKind.Number))
            {
                return a.ToString() == b.ToString();
            }
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText(),
        };
    }
}
