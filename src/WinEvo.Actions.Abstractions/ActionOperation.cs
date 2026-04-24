using WinEvo.ActionModel;

namespace WinEvo.Actions.Abstractions;

/// <summary>
/// A single operation invocation inside an action manifest. Concrete subclasses
/// carry the typed data for their operation (e.g. <c>RegistrySetOperation</c>
/// carries <c>Key</c>, <c>Value</c>, <c>DataType</c>, <c>Data</c>) and
/// implement <see cref="ExecuteAsync"/> directly against those fields.
///
/// <para>
/// Concrete types are produced by <see cref="IOperationParser"/> from the
/// manifest's raw (<c>operation</c> id + JSON properties) pair, so each
/// subclass is responsible for its own schema-binding via a
/// <c>static FromJson(JsonElement)</c> factory that the parser invokes.
/// </para>
///
/// <para>
/// TODO: finalize cancellation, progress streaming, and undo hooks as the
/// action model evolves.
/// </para>
/// </summary>
public abstract class ActionOperation
{
    /// <summary>Operation id as it appears in action manifests, e.g. "registry-set".</summary>
    public abstract string Id { get; }

    /// <summary>Execute the operation against its typed fields and the supplied context.</summary>
    public abstract Task<OperationResult> ExecuteAsync(OperationContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Helper for typed subclasses: renders a manifest-string property's
    /// <c>{{params.X}}</c> and <c>%EnvVar%</c> expressions against the bound
    /// parameters. Use for every user-visible string field that the author
    /// may have templated (e.g. <c>Key</c>, <c>Path</c>, individual
    /// <c>Args</c>).
    /// </summary>
    protected static string RenderProperty(string template, OperationContext context)
        => Templating.Render(template, context.Parameters);
}
