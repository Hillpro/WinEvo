using System.Globalization;
using WinEvo.Actions.Abstractions;
using WinEvo.Shell.Core.Services;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

public class ParameterInputFactoryTests
{
    [Fact]
    public void Creates_drive_picker_for_DriveParameter_and_honors_filter()
    {
        var drives = new FakeDriveProvider(
            new DriveOption("C:\\", "C:  System"),
            new DriveOption("D:\\", "D:  Data"));
        var factory = new ParameterInputFactory(drives);

        var vm = factory.Create(
            new DriveParameter { Id = "drive", Required = true, AllowedDriveTypes = ["fixed"] },
            culture: CultureInfo.InvariantCulture);

        var drive = Assert.IsType<DriveParameterInputViewModel>(vm);
        Assert.Equal(2, drive.DriveOptions.Count);
        Assert.Equal(["fixed"], drives.LastRequestedTypes);
        Assert.Equal("C:\\", drive.SelectedDrive?.Root);
    }

    [Fact]
    public void Creates_integer_picker_for_IntegerParameter_with_min_max()
    {
        var vm = Factory().Create(
            new IntegerParameter { Id = "n", Required = true, Min = 1, Max = 10 },
            culture: CultureInfo.InvariantCulture);

        var integer = Assert.IsType<IntegerParameterInputViewModel>(vm);
        Assert.Equal(1d, integer.Minimum);
        Assert.Equal(10d, integer.Maximum);
        Assert.False(integer.HasValue);                 // no default → NaN
        Assert.Null(integer.ToJsonValue());
    }

    [Fact]
    public void Integer_default_is_honored()
    {
        var vm = (IntegerParameterInputViewModel)Factory().Create(
            new IntegerParameter { Id = "n", Default = 5 },
            culture: CultureInfo.InvariantCulture);

        Assert.True(vm.HasValue);
        Assert.Equal(5d, vm.Value);
        Assert.Equal("5", vm.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Creates_boolean_picker_for_BooleanParameter_always_has_value()
    {
        var vm = Factory().Create(
            new BooleanParameter { Id = "b" },
            culture: CultureInfo.InvariantCulture);

        var boolean = Assert.IsType<BooleanParameterInputViewModel>(vm);
        Assert.False(boolean.Value);
        Assert.True(boolean.HasValue);
        Assert.Equal("false", boolean.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Creates_enum_picker_for_EnumParameter_and_preselects_first_choice()
    {
        var vm = Factory().Create(
            new EnumParameter { Id = "level", Choices = ["low", "medium", "high"] },
            culture: CultureInfo.InvariantCulture);

        var enumVm = Assert.IsType<EnumParameterInputViewModel>(vm);
        Assert.Equal(["low", "medium", "high"], enumVm.Choices);
        Assert.Equal("low", enumVm.SelectedChoice);
        Assert.Equal("\"low\"", enumVm.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Enum_default_picks_matching_choice()
    {
        var vm = (EnumParameterInputViewModel)Factory().Create(
            new EnumParameter
            {
                Id = "level",
                Choices = ["low", "medium", "high"],
                Default = "medium",
            },
            culture: CultureInfo.InvariantCulture);

        Assert.Equal("medium", vm.SelectedChoice);
    }

    [Fact]
    public void Creates_text_box_for_StringParameter()
    {
        var vm = Factory().Create(
            new StringParameter { Id = "name" },
            culture: CultureInfo.InvariantCulture);

        Assert.IsType<StringParameterInputViewModel>(vm);
    }

    [Fact]
    public void String_input_emits_what_the_user_typed()
    {
        var vm = Factory().Create(
            new StringParameter { Id = "svc" },
            culture: CultureInfo.InvariantCulture);

        var str = Assert.IsType<StringParameterInputViewModel>(vm);
        str.Value = "wuauserv";
        Assert.True(str.HasValue);
        Assert.Equal("\"wuauserv\"", str.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Drive_VM_reports_no_value_when_list_is_empty()
    {
        var vm = (DriveParameterInputViewModel)Factory().Create(
            new DriveParameter { Id = "drive" },
            culture: CultureInfo.InvariantCulture);

        Assert.False(vm.HasValue);
        Assert.Null(vm.ToJsonValue());
    }

    private static ParameterInputFactory Factory()
        => new(new FakeDriveProvider());

    private sealed class FakeDriveProvider(params DriveOption[] drives) : IDriveProvider
    {
        public IReadOnlyList<string>? LastRequestedTypes { get; private set; }

        public IReadOnlyList<DriveOption> Enumerate(IReadOnlyList<string>? allowedTypes)
        {
            LastRequestedTypes = allowedTypes;
            return drives;
        }
    }
}
