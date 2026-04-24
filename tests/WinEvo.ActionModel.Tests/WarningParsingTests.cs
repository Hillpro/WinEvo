using WinEvo.ActionModel;

namespace WinEvo.ActionModel.Tests;

public class WarningParsingTests
{
    [Fact]
    public void Parses_severity_and_key()
    {
        var json = """
        {
          "id": "test.act",
          "version": "1.0.0",
          "name": "Test",
          "category": "customization",
          "warnings": [
            { "severity": "danger", "key": "foo.bar" }
          ],
          "execution": { "steps": [ { "operation": "registry-set" } ] }
        }
        """;

        var manifest = ManifestLoader.Parse(json);

        var w = Assert.Single(manifest.Warnings);
        Assert.Equal(WarningSeverity.Danger, w.Severity);
        Assert.Equal("foo.bar", w.Key);
        Assert.Empty(w.Tokens);
    }

    [Fact]
    public void Parses_tokens_map()
    {
        var json = """
        {
          "id": "test.act",
          "version": "1.0.0",
          "name": "Test",
          "category": "storage",
          "warnings": [
            {
              "severity": "warning",
              "key": "storage.wipe",
              "tokens": { "drive": "C:\\", "tool": "cipher" }
            }
          ],
          "execution": { "steps": [ { "operation": "external-process" } ] }
        }
        """;

        var manifest = ManifestLoader.Parse(json);

        var w = Assert.Single(manifest.Warnings);
        Assert.Equal(2, w.Tokens.Count);
        Assert.Equal("C:\\", w.Tokens["drive"]);
        Assert.Equal("cipher", w.Tokens["tool"]);
    }

    [Fact]
    public void Unknown_severity_defaults_to_info()
    {
        var json = """
        {
          "id": "test.act",
          "version": "1.0.0",
          "name": "Test",
          "category": "customization",
          "warnings": [ { "severity": "bogus", "key": "k" } ],
          "execution": { "steps": [ { "operation": "registry-set" } ] }
        }
        """;

        var manifest = ManifestLoader.Parse(json);

        Assert.Equal(WarningSeverity.Info, manifest.Warnings[0].Severity);
    }
}
