namespace WinEvo.Actions.Abstractions;

/// <summary>
/// Ends an action when an operation it depends on fails. Thrown by
/// <see cref="ActionExecution.RunAsync"/> after the failure has been recorded, so
/// the executor only needs to stop: the step is already recorded.
/// </summary>
public sealed class ActionStepFailedException : Exception
{
    public ActionStepFailedException(string operationId, OperationResult result)
        : base($"{operationId} failed: {result.Error}")
    {
        OperationId = operationId;
        Result = result;
    }

    public string OperationId { get; }
    public OperationResult Result { get; }
}
