using System.Globalization;
using WinEvo.Actions.Abstractions;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

public class ActionItemViewModelTests
{
    [Fact]
    public void Category_is_shown_by_its_name_in_the_users_language()
    {
        var english = new ActionItemViewModel(Descriptor("storage"), CultureInfo.GetCultureInfo("en"));
        var french = new ActionItemViewModel(Descriptor("storage"), CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal("Storage", english.DisplayCategory);
        Assert.Equal("Stockage", french.DisplayCategory);
    }

    [Fact]
    public void Category_without_a_name_is_shown_by_its_id()
    {
        var item = new ActionItemViewModel(Descriptor("networking"), CultureInfo.GetCultureInfo("fr"));

        Assert.Equal("networking", item.DisplayCategory);
    }

    [Fact]
    public void Category_id_stays_available_for_grouping()
    {
        var item = new ActionItemViewModel(Descriptor("storage"), CultureInfo.GetCultureInfo("fr"));

        Assert.Equal("storage", item.Category);
    }

    [Fact]
    public void Elevation_need_is_taken_from_the_descriptor()
    {
        var elevated = Descriptor("storage") with { Elevation = ElevationRequirement.Required };

        Assert.True(new ActionItemViewModel(elevated, CultureInfo.InvariantCulture).RequiresElevation);
        Assert.False(new ActionItemViewModel(Descriptor("storage"), CultureInfo.InvariantCulture).RequiresElevation);
    }

    private static ActionDescriptor Descriptor(string category) => new()
    {
        Id = $"{category}.test",
        Category = category,
        Name = LocalizableText.Literal("Test"),
    };
}
