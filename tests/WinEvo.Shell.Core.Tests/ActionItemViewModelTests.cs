using System.Globalization;
using WinEvo.Actions.Abstractions;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

public class ActionItemViewModelTests
{
    [Fact]
    public void Category_is_shown_by_the_name_it_carries()
    {
        var item = new ActionItemViewModel(Descriptor(), CultureInfo.InvariantCulture);

        Assert.Equal("Storage", item.DisplayCategory);
    }

    [Fact]
    public void Category_id_stays_available_for_grouping()
    {
        var item = new ActionItemViewModel(Descriptor(), CultureInfo.InvariantCulture);

        Assert.Equal("storage", item.Category);
    }

    [Fact]
    public void Elevation_need_is_taken_from_the_descriptor()
    {
        var elevated = Descriptor() with { Elevation = ElevationRequirement.Required };

        Assert.True(new ActionItemViewModel(elevated, CultureInfo.InvariantCulture).RequiresElevation);
        Assert.False(new ActionItemViewModel(Descriptor(), CultureInfo.InvariantCulture).RequiresElevation);
    }

    private static ActionDescriptor Descriptor() => new()
    {
        Id = "storage.test",
        Category = new ActionCategory { Id = "storage", Name = LocalizableText.Literal("Storage") },
        Name = LocalizableText.Literal("Test"),
    };
}
