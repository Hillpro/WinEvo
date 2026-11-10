using System.Globalization;
using System.Text.Json.Nodes;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

/// <summary>
/// A live value is what the agent read from the system. Each input must
/// display it, and none may treat it as something the user did.
/// </summary>
public class ParameterLiveValueTests
{
    [Fact]
    public void Boolean_shows_the_live_value_without_reporting_a_user_change()
    {
        var toggle = new BooleanParameterInputViewModel(new BooleanParameter { Id = "isOn", Type = "boolean" }, culture: CultureInfo.InvariantCulture);
        var userChanges = 0;
        toggle.UserChangedValue += (_, _) => userChanges++;

        toggle.ShowLiveValue(JsonValue.Create(true));

        Assert.True(toggle.Value);
        Assert.Equal(0, userChanges);
    }

    [Fact]
    public void Boolean_reports_a_user_change_when_the_value_is_set_from_the_UI()
    {
        var toggle = new BooleanParameterInputViewModel(new BooleanParameter { Id = "isOn", Type = "boolean" }, culture: CultureInfo.InvariantCulture);
        var userChanges = 0;
        toggle.UserChangedValue += (_, _) => userChanges++;

        toggle.Value = true;

        Assert.Equal(1, userChanges);
    }

    [Fact]
    public void Boolean_notifies_the_binding_when_the_live_value_differs()
    {
        var toggle = new BooleanParameterInputViewModel(new BooleanParameter { Id = "isOn", Type = "boolean" }, culture: CultureInfo.InvariantCulture);
        var changedProperties = new List<string?>();
        toggle.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        toggle.ShowLiveValue(JsonValue.Create(true));

        Assert.Equal([nameof(BooleanParameterInputViewModel.Value)], changedProperties);
    }

    [Fact]
    public void String_shows_the_live_value()
    {
        var input = new StringParameterInputViewModel(new StringParameter { Id = "name", Type = "string" }, culture: CultureInfo.InvariantCulture);

        input.ShowLiveValue(JsonValue.Create("disk"));

        Assert.Equal("disk", input.Value);
    }

    [Fact]
    public void Integer_shows_the_live_value()
    {
        var input = new IntegerParameterInputViewModel(new IntegerParameter { Id = "passes", Type = "integer" }, culture: CultureInfo.InvariantCulture);

        input.ShowLiveValue(JsonValue.Create(3L));

        Assert.Equal(3, input.Value);
    }

    [Fact]
    public void Enum_selects_the_live_choice()
    {
        var input = new EnumParameterInputViewModel(
            new EnumParameter { Id = "level", Type = "enum", Choices = ["low", "medium", "high"] }, culture: CultureInfo.InvariantCulture);

        input.ShowLiveValue(JsonValue.Create("high"));

        Assert.Equal("high", input.SelectedChoice);
    }

    [Fact]
    public void Enum_rejects_a_live_value_that_is_not_a_choice()
    {
        var input = new EnumParameterInputViewModel(
            new EnumParameter { Id = "level", Type = "enum", Choices = ["low", "high"] }, culture: CultureInfo.InvariantCulture);

        var thrown = Assert.Throws<InvalidOperationException>(() => input.ShowLiveValue(JsonValue.Create("extreme")));

        Assert.Contains("'level'", thrown.Message);
        Assert.Equal("low", input.SelectedChoice);
    }

    [Fact]
    public void Drive_selects_the_live_drive_ignoring_case()
    {
        var input = new DriveParameterInputViewModel(
            new DriveParameter { Id = "drive", Type = "drive" },
            culture: CultureInfo.InvariantCulture,
            [new DriveOption(@"C:\", "System (C:)"), new DriveOption(@"D:\", "Data (D:)")]);

        input.ShowLiveValue(JsonValue.Create(@"d:\"));

        Assert.Equal(@"D:\", input.SelectedDrive?.Root);
    }

    [Fact]
    public void Drive_rejects_a_live_drive_that_is_not_available()
    {
        var input = new DriveParameterInputViewModel(
            new DriveParameter { Id = "drive", Type = "drive" },
            culture: CultureInfo.InvariantCulture,
            [new DriveOption(@"C:\", "System (C:)")]);

        Assert.Throws<InvalidOperationException>(() => input.ShowLiveValue(JsonValue.Create(@"Z:\")));
        Assert.Equal(@"C:\", input.SelectedDrive?.Root);
    }
}
