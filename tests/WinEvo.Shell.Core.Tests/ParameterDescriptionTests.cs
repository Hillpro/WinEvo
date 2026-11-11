using System.Globalization;
using WinEvo.Actions.Abstractions;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

/// <summary>
/// Every input shows its parameter's description under it, and hides the
/// line when the parameter has none.
/// </summary>
public class ParameterDescriptionTests
{
    [Fact]
    public void Description_is_resolved_and_shown()
    {
        var input = new StringParameterInputViewModel(
            new StringParameter { Id = "name", Description = LocalizableText.Literal("What to call it.") },
            culture: CultureInfo.InvariantCulture);

        Assert.Equal("What to call it.", input.DisplayDescription);
        Assert.True(input.HasDescription);
    }

    [Fact]
    public void No_description_hides_the_line()
    {
        var input = new StringParameterInputViewModel(new StringParameter { Id = "name" }, culture: CultureInfo.InvariantCulture);

        Assert.False(input.HasDescription);
    }

    [Fact]
    public void Blank_description_hides_the_line()
    {
        var input = new StringParameterInputViewModel(
            new StringParameter { Id = "name", Description = LocalizableText.Literal("  ") },
            culture: CultureInfo.InvariantCulture);

        Assert.False(input.HasDescription);
    }
}
