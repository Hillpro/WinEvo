namespace WinEvo.Actions.Abstractions;

/// <summary>Outcome of a single operation step.</summary>
public sealed class OperationResult
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }

    public static OperationResult Ok(string? message = null)
        => new() { Success = true, Message = message };

    public static OperationResult Fail(string error, string? message = null)
        => new() { Success = false, Error = error, Message = message };
}
