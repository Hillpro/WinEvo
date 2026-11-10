namespace WinEvo.Actions.Abstractions.Tests;

/// <summary>Operation with a predetermined outcome, so tests never touch the real OS.</summary>
internal sealed class ScriptedOperation : ActionOperation
{
    private readonly Func<OperationContext, CancellationToken, Task<OperationResult>> _execute;

    private ScriptedOperation(string id, Func<OperationContext, CancellationToken, Task<OperationResult>> execute)
    {
        Id = id;
        _execute = execute;
    }

    public override string Id { get; }

    public override Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken)
        => _execute(context, cancellationToken);

    public static ScriptedOperation Succeeding(string id)
        => new(id, (_, _) => Task.FromResult(OperationResult.Ok($"{id} ok")));

    public static ScriptedOperation Failing(string id)
        => new(id, (_, _) => Task.FromResult(OperationResult.Fail($"{id} failed")));

    public static ScriptedOperation Throwing(string id, Exception exception)
        => new(id, (_, _) => throw exception);

    public static ScriptedOperation Logging(string id, string line)
        => new(id, (context, _) =>
        {
            context.Log(line);
            return Task.FromResult(OperationResult.Ok());
        });
}
