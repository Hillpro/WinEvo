using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions;

/// <summary>
/// The categories the core actions are grouped under. Adding one = a property
/// here + its name in <c>Strings.resx</c> and <c>Strings.fr.resx</c>.
/// </summary>
internal static class Categories
{
    public static ActionCategory Customization { get; } = new()
    {
        Id = "customization",
        Name = ActionText.For("Category_Customization"),
    };

    public static ActionCategory Storage { get; } = new()
    {
        Id = "storage",
        Name = ActionText.For("Category_Storage"),
    };
}
