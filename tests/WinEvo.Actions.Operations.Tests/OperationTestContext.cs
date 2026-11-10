using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>Small helper used by every operation test to construct a bare <see cref="OperationContext"/>.</summary>
internal static class OperationTestContext
{
    public static OperationContext Empty() => new();
}
