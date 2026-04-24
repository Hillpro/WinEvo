using System.Text.Json;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Core.Tests;

public class WarningAggregatorTests
{
    private static readonly string[] s_expectedOrder = ["Z", "Y", "X"];


    [Fact]
    public void Aggregate_returns_empty_when_no_warnings()
    {
        var bundle = NewBundleWith(new());
        var result = WarningAggregator.Aggregate([], bundle, "en");
        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_resolves_each_warning_via_bundle()
    {
        var bundle = NewBundleWith(new()
        {
            ["a"] = "Alpha",
            ["b"] = "Beta",
        });
        var input = new List<ActionWarning>
        {
            new() { Severity = WarningSeverity.Info, Key = "a" },
            new() { Severity = WarningSeverity.Warning, Key = "b" },
        };

        var result = WarningAggregator.Aggregate(input, bundle, "en");

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Message);
        Assert.Equal(WarningSeverity.Info, result[0].Severity);
        Assert.Equal("Beta", result[1].Message);
        Assert.Equal(WarningSeverity.Warning, result[1].Severity);
    }

    [Fact]
    public void Aggregate_deduplicates_by_key_keeping_highest_severity()
    {
        var bundle = NewBundleWith(new() { ["dup"] = "Duplicate msg" });
        var input = new List<ActionWarning>
        {
            new() { Severity = WarningSeverity.Info, Key = "dup" },
            new() { Severity = WarningSeverity.Danger, Key = "dup" },
            new() { Severity = WarningSeverity.Warning, Key = "dup" },
        };

        var result = WarningAggregator.Aggregate(input, bundle, "en");

        Assert.Single(result);
        Assert.Equal(WarningSeverity.Danger, result[0].Severity);
    }

    [Fact]
    public void Aggregate_preserves_first_occurrence_order()
    {
        var bundle = NewBundleWith(new()
        {
            ["x"] = "X",
            ["y"] = "Y",
            ["z"] = "Z",
        });
        var input = new List<ActionWarning>
        {
            new() { Severity = WarningSeverity.Info, Key = "z" },
            new() { Severity = WarningSeverity.Info, Key = "y" },
            new() { Severity = WarningSeverity.Danger, Key = "z" }, // bumps severity but keeps z first
            new() { Severity = WarningSeverity.Info, Key = "x" },
        };

        var result = WarningAggregator.Aggregate(input, bundle, "en");

        Assert.Equal(s_expectedOrder, result.Select(r => r.Message).ToArray());
    }

    [Fact]
    public void Aggregate_passes_tokens_to_bundle()
    {
        var bundle = NewBundleWith(new() { ["t"] = "Drive {drive}" });
        var input = new List<ActionWarning>
        {
            new()
            {
                Severity = WarningSeverity.Info,
                Key = "t",
                Tokens = new Dictionary<string, string> { ["drive"] = "C:\\" },
            },
        };

        var result = WarningAggregator.Aggregate(input, bundle, "en");

        Assert.Equal("Drive C:\\", result[0].Message);
    }

    private static StringBundle NewBundleWith(Dictionary<string, string> entries)
    {
        var dir = Path.Combine(Path.GetTempPath(), "winevo-wagg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Strings.en.json"), JsonSerializer.Serialize(entries));
        return new StringBundle(dir);
    }
}
