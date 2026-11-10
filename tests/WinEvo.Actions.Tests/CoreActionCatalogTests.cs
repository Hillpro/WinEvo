using System.Text.RegularExpressions;

namespace WinEvo.Actions.Tests;

public partial class CoreActionCatalogTests
{
    private readonly CoreActionCatalog _catalog = new();

    [Fact]
    public void Every_action_id_is_category_dot_name_in_kebab_case()
    {
        Assert.All(_catalog.Actions, action => Assert.Matches(ActionIdPattern(), action.Descriptor.Id));
    }

    [Fact]
    public void Every_action_id_starts_with_its_category()
    {
        Assert.All(_catalog.Actions, action =>
            Assert.StartsWith(action.Descriptor.Category + ".", action.Descriptor.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Action_ids_are_unique()
    {
        var ids = _catalog.Actions.Select(action => action.Descriptor.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Find_returns_each_core_action_by_id()
    {
        Assert.All(_catalog.Actions, action => Assert.Same(action, _catalog.Find(action.Descriptor.Id)));
    }

    [Fact]
    public void Find_returns_null_for_an_unknown_id()
    {
        Assert.Null(_catalog.Find("storage.no-such-action"));
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]*\.[a-z][a-z0-9-]*$")]
    private static partial Regex ActionIdPattern();
}
