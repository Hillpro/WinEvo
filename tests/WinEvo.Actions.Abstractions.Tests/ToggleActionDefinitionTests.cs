using System.Globalization;
using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Abstractions.Tests;

public class ToggleActionDefinitionTests
{
    [Fact]
    public void Descriptor_is_a_toggle_with_the_switch_as_its_only_parameter()
    {
        var action = new FakeToggle(currentlyOn: null, isOnWhenUnknown: true);

        Assert.Equal(InteractionMode.Toggle, action.Descriptor.Interaction);
        var parameter = Assert.IsType<BooleanParameter>(Assert.Single(action.Descriptor.Parameters));
        Assert.Equal(ToggleActionDefinition.IsOnParameterId, parameter.Id);
        Assert.Equal("Switch label", parameter.Name?.Resolve(CultureInfo.InvariantCulture));
        Assert.True(parameter.Default);
    }

    [Fact]
    public void Descriptor_keeps_the_identity_it_was_given()
    {
        var action = new FakeToggle(currentlyOn: null);

        Assert.Equal("test.toggle", action.Descriptor.Id);
        Assert.Equal("customization", action.Descriptor.Category.Id);
    }

    [Fact]
    public void Descriptor_that_declares_its_own_parameters_is_rejected()
    {
        var withParameter = FakeToggle.Identity with
        {
            Parameters = [new StringParameter { Id = "extra" }],
        };

        var thrown = Assert.Throws<ArgumentException>(() => new FakeToggle(currentlyOn: null, descriptor: withParameter));

        Assert.Contains("test.toggle", thrown.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Execute_applies_the_requested_switch_position(bool requested)
    {
        var action = new FakeToggle(currentlyOn: null);
        var execution = new ActionExecution(new Dictionary<string, object?> { [ToggleActionDefinition.IsOnParameterId] = requested });

        await action.ExecuteAsync(execution, TestContext.Current.CancellationToken);

        Assert.Equal(requested, action.AppliedPosition);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadState_reports_the_live_switch_position(bool currentlyOn)
    {
        var action = new FakeToggle(currentlyOn);

        var state = await action.ReadStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(currentlyOn, state[ToggleActionDefinition.IsOnParameterId]);
    }

    [Fact]
    public async Task ReadState_is_empty_when_the_position_is_unknown()
    {
        var action = new FakeToggle(currentlyOn: null);

        var state = await action.ReadStateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(state);
    }

    private sealed class FakeToggle(bool? currentlyOn, bool isOnWhenUnknown = false, ActionDescriptor? descriptor = null)
        : ToggleActionDefinition(descriptor ?? Identity, LocalizableText.Literal("Switch label"), isOnWhenUnknown: isOnWhenUnknown)
    {
        public static ActionDescriptor Identity { get; } = new()
        {
            Id = "test.toggle",
            Category = new ActionCategory { Id = "customization", Name = LocalizableText.Literal("Customization") },
            Name = LocalizableText.Literal("Test toggle"),
        };

        public bool? AppliedPosition { get; private set; }

        protected override Task<bool?> ReadIsOnAsync(CancellationToken cancellationToken) => Task.FromResult(currentlyOn);

        protected override Task ApplyAsync(bool isOn, ActionExecution execution, CancellationToken cancellationToken)
        {
            AppliedPosition = isOn;
            return Task.CompletedTask;
        }
    }
}
