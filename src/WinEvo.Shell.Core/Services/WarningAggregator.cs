using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Collapses a manifest's warnings into the list to display. Deduplicates by
/// key (max severity wins per key) and preserves first-occurrence order so
/// authors control the reading order.
/// </summary>
/// <remarks>
/// TODO: when sub-action execution lands, extend to walk the composite graph
/// and aggregate across all reachable manifests.
/// </remarks>
public static class WarningAggregator
{
    public static IReadOnlyList<ResolvedWarning> Aggregate(
        IReadOnlyList<ActionWarning> warnings,
        StringBundle bundle,
        string? language)
    {
        if (warnings.Count == 0)
            return [];

        var byKey = new Dictionary<string, (int Order, ActionWarning Warning)>(StringComparer.Ordinal);
        var order = 0;
        foreach (var w in warnings)
        {
            if (byKey.TryGetValue(w.Key, out var existing))
            {
                if (w.Severity > existing.Warning.Severity)
                    byKey[w.Key] = (existing.Order, w);
            }
            else
            {
                byKey[w.Key] = (order++, w);
            }
        }

        return byKey.Values
            .OrderBy(e => e.Order)
            .Select(e => new ResolvedWarning(
                e.Warning.Severity,
                bundle.Resolve(e.Warning.Key, language, e.Warning.Tokens)))
            .ToList();
    }
}
