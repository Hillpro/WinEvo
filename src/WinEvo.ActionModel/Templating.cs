using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WinEvo.ActionModel;

/// <summary>
/// Minimal template-expression renderer for operation-step properties.
/// Supports <c>{{params.id}}</c> substitutions and Windows environment
/// variable expansion (<c>%SystemRoot%</c>, etc.).
/// TODO: add template functions (<c>drive()</c>, <c>basename()</c>, <c>dirname()</c>)
/// as documented in action-authoring.md.
/// </summary>
public static class Templating
{
    private static readonly Regex s_expression = new(
        @"\{\{\s*(?<expr>[^{}]+?)\s*\}\}",
        RegexOptions.Compiled);

    public static string Render(string input, IReadOnlyDictionary<string, object?> parameters)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var expanded = Environment.ExpandEnvironmentVariables(input);
        return s_expression.Replace(expanded, match =>
        {
            var expr = match.Groups["expr"].Value;
            return Evaluate(expr, parameters) ?? match.Value;
        });
    }

    private static string? Evaluate(string expression, IReadOnlyDictionary<string, object?> parameters)
    {
        if (expression.StartsWith("params.", StringComparison.Ordinal))
        {
            var key = expression["params.".Length..].Trim();
            if (parameters.TryGetValue(key, out var value))
                return FormatValue(value);
        }

        // TODO: drive(...), basename(...), dirname(...) function dispatch.
        return null;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    public static string[] RenderArray(IEnumerable<string> items, IReadOnlyDictionary<string, object?> parameters)
    {
        var list = new List<string>();
        foreach (var item in items)
            list.Add(Render(item, parameters));
        return list.ToArray();
    }
}
