using System.Globalization;
using WinEvo.ActionModel;

namespace WinEvo.ActionModel.Tests;

/// <summary>
/// Templating feeds nearly every user-authored string (registry keys/data,
/// command/powershell scripts, process args). These lock the behaviours other
/// layers silently depend on: the bool -> "true"/"false" contract that the
/// registry DWORD coercion relies on, invariant-culture number formatting,
/// and leaving an unresolved <c>{{params.x}}</c> verbatim (so a typo can't
/// silently blank a path).
/// </summary>
public class TemplatingTests
{
    [Fact]
    public void Substitutes_a_string_parameter()
    {
        Assert.Equal("Hello World", Templating.Render("Hello {{params.name}}", P(("name", "World"))));
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Boolean_renders_lowercase_true_false(bool value, string expected)
    {
        // This exact contract is what RegistrySetOperation.ParseInt32/64 coerce to 1/0.
        Assert.Equal(expected, Templating.Render("{{params.flag}}", P(("flag", value))));
    }

    [Fact]
    public void Integer_renders_with_invariant_culture()
    {
        Assert.Equal("42", Templating.Render("{{params.n}}", P(("n", 42L))));
    }

    [Fact]
    public void Double_uses_invariant_culture_decimal_point()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); // would render "0,1" if culture leaked in
        try
        {
            Assert.Equal("0.1", Templating.Render("{{params.x}}", P(("x", 0.1))));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Unresolved_parameter_is_left_verbatim()
    {
        Assert.Equal("{{params.missing}}", Templating.Render("{{params.missing}}", P()));
    }

    [Fact]
    public void Null_parameter_value_renders_empty()
    {
        Assert.Equal("", Templating.Render("{{params.n}}", P(("n", null))));
    }

    [Fact]
    public void Whitespace_inside_the_expression_is_trimmed()
    {
        Assert.Equal("v", Templating.Render("{{  params.k  }}", P(("k", "v"))));
    }

    [Fact]
    public void Expands_environment_variables()
    {
        var name = "WINEVO_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(name, "expanded");
        try
        {
            Assert.Equal("expanded", Templating.Render($"%{name}%", P()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void Empty_input_is_returned_unchanged()
    {
        Assert.Equal("", Templating.Render("", P()));
    }

    [Fact]
    public void Render_array_renders_each_element()
    {
        var rendered = Templating.RenderArray(["{{params.a}}", "static", "{{params.b}}"], P(("a", "1"), ("b", "2")));
        Assert.Equal(["1", "static", "2"], rendered);
    }

    private static Dictionary<string, object?> P(params (string key, object? value)[] entries)
        => entries.ToDictionary(e => e.key, e => e.value);
}
