using System.Text.Json;
using WinEvo.ActionModel;
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
            new DriveParameter { Id = "drive", Type = "drive", Required = true, AllowedDriveTypes = ["fixed"] },
            language: null);

        var drive = Assert.IsType<DriveParameterInputViewModel>(vm);
        Assert.Equal(2, drive.DriveOptions.Count);
        Assert.Equal(["fixed"], drives.LastRequestedTypes);
        Assert.Equal("C:\\", drive.SelectedDrive?.Root);
    }

    [Fact]
    public void Creates_integer_picker_for_IntegerParameter_with_min_max()
    {
        var vm = Factory().Create(
            new IntegerParameter { Id = "n", Type = "integer", Required = true, Min = 1, Max = 10 },
            language: null);

        var integer = Assert.IsType<IntegerParameterInputViewModel>(vm);
        Assert.Equal(1d, integer.Minimum);
        Assert.Equal(10d, integer.Maximum);
        Assert.False(integer.HasValue);                 // no default → NaN
        Assert.Null(integer.ToJsonValue());
    }

    [Fact]
    public void Integer_default_is_honored()
    {
        using var doc = JsonDocument.Parse("5");
        var vm = (IntegerParameterInputViewModel)Factory().Create(
            new IntegerParameter { Id = "n", Type = "integer", Default = doc.RootElement.Clone() },
            language: null);

        Assert.True(vm.HasValue);
        Assert.Equal(5d, vm.Value);
        Assert.Equal("5", vm.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Creates_boolean_picker_for_BooleanParameter_always_has_value()
    {
        var vm = Factory().Create(
            new BooleanParameter { Id = "b", Type = "boolean" },
            language: null);

        var boolean = Assert.IsType<BooleanParameterInputViewModel>(vm);
        Assert.False(boolean.Value);
        Assert.True(boolean.HasValue);
        Assert.Equal("false", boolean.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Creates_enum_picker_for_EnumParameter_and_preselects_first_choice()
    {
        var vm = Factory().Create(
            new EnumParameter { Id = "level", Type = "enum", Choices = ["low", "medium", "high"] },
            language: null);

        var enumVm = Assert.IsType<EnumParameterInputViewModel>(vm);
        Assert.Equal(["low", "medium", "high"], enumVm.Choices);
        Assert.Equal("low", enumVm.SelectedChoice);
        Assert.Equal("\"low\"", enumVm.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Enum_default_picks_matching_choice()
    {
        using var doc = JsonDocument.Parse("\"medium\"");
        var vm = (EnumParameterInputViewModel)Factory().Create(
            new EnumParameter
            {
                Id = "level", Type = "enum",
                Choices = ["low", "medium", "high"],
                Default = doc.RootElement.Clone(),
            },
            language: null);

        Assert.Equal("medium", vm.SelectedChoice);
    }

    [Fact]
    public void Falls_back_to_string_picker_for_StringParameter()
    {
        var vm = Factory().Create(
            new StringParameter { Id = "name", Type = "string" },
            language: null);

        Assert.IsType<StringParameterInputViewModel>(vm);
    }

    [Fact]
    public void Unknown_string_type_also_uses_string_picker()
    {
        // wifi-profile / file-path / service-name land on StringParameter today
        // — they render as TextBoxes until a dedicated VM is added.
        var vm = Factory().Create(
            new StringParameter { Id = "svc", Type = "service-name" },
            language: null);

        var str = Assert.IsType<StringParameterInputViewModel>(vm);
        str.Value = "wuauserv";
        Assert.True(str.HasValue);
        Assert.Equal("\"wuauserv\"", str.ToJsonValue()!.ToJsonString());
    }

    [Fact]
    public void Drive_VM_reports_no_value_when_list_is_empty()
    {
        var vm = (DriveParameterInputViewModel)Factory().Create(
            new DriveParameter { Id = "drive", Type = "drive" },
            language: null);

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
