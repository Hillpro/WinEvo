using System.Text.Json;

namespace WinEvo.ActionModel;

/// <summary>
/// Parses an action manifest JSON document into the strongly-typed model.
/// Lenient — unknown fields are ignored so manifests can grow without breaking
/// older runtimes. TODO: full JSON Schema validation via JsonSchema.Net.
/// </summary>
public static class ManifestLoader
{
    private static readonly JsonDocumentOptions s_documentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Loads and parses a manifest from a JSON file on disk.</summary>
    public static async Task<ActionManifest> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream, s_documentOptions, ct).ConfigureAwait(false);
        return Parse(doc.RootElement);
    }

    /// <summary>Parses a manifest from a JSON string.</summary>
    public static ActionManifest Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, s_documentOptions);
        return Parse(doc.RootElement);
    }

    /// <summary>
    /// Parses a manifest from an already-loaded <see cref="JsonElement"/>.
    /// Properties that own a <see cref="JsonElement"/> (e.g. step properties,
    /// parameter defaults) are cloned so the returned manifest remains valid
    /// after the caller disposes its <see cref="JsonDocument"/>.
    /// </summary>
    public static ActionManifest Parse(JsonElement root)
    {
        var manifest = new ActionManifest
        {
            Id = RequireString(root, "id"),
            Version = RequireString(root, "version"),
            Name = RequireString(root, "name"),
            Description = OptionalString(root, "description"),
            Category = RequireString(root, "category"),
            Tags = ParseStringArray(root, "tags"),
            Icon = OptionalString(root, "icon"),
            Author = OptionalString(root, "author"),
            Requirements = ParseRequirements(root),
            Warnings = ParseWarnings(root),
            Parameters = ParseParameters(root),
            Execution = ParseExecution(root),
            Localization = ParseLocalization(root),
        };
        return manifest;
    }

    private static Requirements ParseRequirements(JsonElement root)
    {
        if (!root.TryGetProperty("requirements", out var req))
            return new Requirements();

        return new Requirements
        {
            Elevation = OptionalString(req, "elevation") switch
            {
                "required" => ElevationRequirement.Required,
                "optional" => ElevationRequirement.Optional,
                _ => ElevationRequirement.NotRequired,
            },
            MinWindowsBuild = req.TryGetProperty("minWindowsBuild", out var b) ? b.GetInt32() : 0,
            Architectures = ParseStringArray(req, "architectures"),
        };
    }

    private static List<ActionWarning> ParseWarnings(JsonElement root)
    {
        if (!root.TryGetProperty("warnings", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<ActionWarning>();
        foreach (var el in arr.EnumerateArray())
        {
            result.Add(new ActionWarning
            {
                Severity = OptionalString(el, "severity") switch
                {
                    "critical" => WarningSeverity.Critical,
                    "danger" => WarningSeverity.Danger,
                    "warning" => WarningSeverity.Warning,
                    _ => WarningSeverity.Info,
                },
                Key = RequireString(el, "key"),
            });
        }
        return result;
    }

    private static List<Parameter> ParseParameters(JsonElement root)
    {
        if (!root.TryGetProperty("parameters", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<Parameter>();
        foreach (var el in arr.EnumerateArray())
        {
            result.Add(new Parameter
            {
                Id = RequireString(el, "id"),
                Type = RequireString(el, "type"),
                Name = OptionalString(el, "name"),
                Description = OptionalString(el, "description"),
                Required = el.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.True,
                Default = el.TryGetProperty("default", out var d) ? d.Clone() : null,
                Min = el.TryGetProperty("min", out var mn) && mn.ValueKind == JsonValueKind.Number ? mn.GetInt32() : null,
                Max = el.TryGetProperty("max", out var mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetInt32() : null,
                Choices = el.TryGetProperty("choices", out var ch) && ch.ValueKind == JsonValueKind.Array
                    ? ch.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                    : null,
            });
        }
        return result;
    }

    private static Execution ParseExecution(JsonElement root)
    {
        if (!root.TryGetProperty("execution", out var exec))
            return new Execution();

        return new Execution
        {
            Mode = OptionalString(exec, "mode") switch
            {
                "sequential-continue-on-error" => ExecutionMode.SequentialContinueOnError,
                _ => ExecutionMode.Sequential,
            },
            CreateRestorePoint = exec.TryGetProperty("createRestorePoint", out var crp) && crp.ValueKind == JsonValueKind.True,
            Steps = ParseSteps(exec),
        };
    }

    private static List<ActionStep> ParseSteps(JsonElement exec)
    {
        if (!exec.TryGetProperty("steps", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<ActionStep>();
        foreach (var el in arr.EnumerateArray())
            result.Add(ParseStep(el));
        return result;
    }

    private static ActionStep ParseStep(JsonElement el)
    {
        var kind = OptionalString(el, "kind");

        // Infer kind when omitted: operation step if it has "operation", sub-action if it has "ref".
        if (kind is null)
        {
            if (el.TryGetProperty("operation", out _)) kind = "operation";
            else if (el.TryGetProperty("ref", out _)) kind = "sub-action";
            else throw new JsonException($"Step has neither 'operation' nor 'ref': {el.GetRawText()}");
        }

        var id = OptionalString(el, "id");
        var continueOnError = el.TryGetProperty("continueOnError", out var c) && c.ValueKind == JsonValueKind.True;

        return kind switch
        {
            "operation" => new OperationStep
            {
                Id = id,
                ContinueOnError = continueOnError,
                Operation = RequireString(el, "operation"),
                Properties = el.Clone(),
            },
            "sub-action" => new SubActionStep
            {
                Id = id,
                ContinueOnError = continueOnError,
                Ref = RequireString(el, "ref"),
                MinVersion = RequireString(el, "minVersion"),
                Parameters = el.TryGetProperty("parameters", out var p) && p.ValueKind == JsonValueKind.Object
                    ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
                    : new Dictionary<string, JsonElement>(),
            },
            _ => throw new JsonException($"Unknown step kind '{kind}'"),
        };
    }

    private static LocalizationMap ParseLocalization(JsonElement root)
    {
        var map = new LocalizationMap();
        if (!root.TryGetProperty("localization", out var loc) || loc.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var prop in loc.EnumerateObject())
        {
            var entry = prop.Value;
            Dictionary<string, LocalizationParameterEntry>? paramMap = null;
            if (entry.TryGetProperty("parameters", out var pe) && pe.ValueKind == JsonValueKind.Object)
            {
                paramMap = new Dictionary<string, LocalizationParameterEntry>();
                foreach (var pp in pe.EnumerateObject())
                {
                    paramMap[pp.Name] = new LocalizationParameterEntry
                    {
                        Name = OptionalString(pp.Value, "name"),
                        Description = OptionalString(pp.Value, "description"),
                    };
                }
            }

            map[prop.Name] = new LocalizationEntry
            {
                Name = OptionalString(entry, "name"),
                Description = OptionalString(entry, "description"),
                Parameters = paramMap,
            };
        }
        return map;
    }

    private static string[] ParseStringArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
    }

    private static string RequireString(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new JsonException($"Missing or non-string required property '{property}'");
        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement el, string property)
        => el.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
