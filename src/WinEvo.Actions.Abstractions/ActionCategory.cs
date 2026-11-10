namespace WinEvo.Actions.Abstractions;

/// <summary>
/// A group of related actions, as the user sees it in the action list. The
/// name travels with the category so that whoever introduces a category also
/// supplies what to call it.
/// </summary>
public sealed class ActionCategory
{
    /// <summary>Lowercase word, stable. Every action id in this category starts with it: <c>storage.wipe-free-space</c>.</summary>
    public required string Id { get; init; }

    public required LocalizableText Name { get; init; }
}
