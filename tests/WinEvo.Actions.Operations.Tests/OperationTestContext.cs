using WinEvo.Actions.Abstractions;

namespace WinEvo.Actions.Operations.Tests;

/// <summary>Small helpers used by every operation test to construct a bare <see cref="OperationContext"/>.</summary>
internal static class OperationTestContext
{
    public static OperationContext Empty() => new()
    {
        Parameters = new Dictionary<string, object?>(),
    };

    public static OperationContext WithParameters(
        IReadOnlyDictionary<string, object?> parameters,
        Action<string>? log = null) => new()
    {
        Parameters = parameters,
        LogSink = log,
    };
}
