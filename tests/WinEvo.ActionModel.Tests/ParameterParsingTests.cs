using WinEvo.ActionModel;

namespace WinEvo.ActionModel.Tests;

public class ParameterParsingTests
{
    [Fact]
    public void Parses_drive_type_into_DriveParameter_and_honors_filter()
    {
        var manifest = ManifestLoader.Parse(Manifest("""
            {
              "id": "drive",
              "type": "drive",
              "name": "Drive",
              "required": true,
              "filter": { "driveType": ["fixed", "removable"] }
            }
            """));

        var p = Assert.IsType<DriveParameter>(Assert.Single(manifest.Parameters));
        Assert.Equal(["fixed", "removable"], p.AllowedDriveTypes!);
    }

    [Fact]
    public void Missing_filter_leaves_AllowedDriveTypes_null()
    {
        var manifest = ManifestLoader.Parse(Manifest("""
            {
              "id": "drive",
              "type": "drive",
              "name": "Drive",
              "required": true
            }
            """));

        var p = Assert.IsType<DriveParameter>(Assert.Single(manifest.Parameters));
        Assert.Null(p.AllowedDriveTypes);
    }

    [Fact]
    public void Filter_without_driveType_leaves_AllowedDriveTypes_null()
    {
        var manifest = ManifestLoader.Parse(Manifest("""
            {
              "id": "drive",
              "type": "drive",
              "name": "Drive",
              "required": true,
              "filter": { "someOtherKey": ["x"] }
            }
            """));

        var p = Assert.IsType<DriveParameter>(Assert.Single(manifest.Parameters));
        Assert.Null(p.AllowedDriveTypes);
    }

    [Fact]
    public void Parses_integer_type_with_min_max()
    {
        var manifest = ManifestLoader.Parse(Manifest("""
            { "id": "n", "type": "integer", "name": "N", "required": true, "min": 1, "max": 10 }
            """));

        var p = Assert.IsType<IntegerParameter>(Assert.Single(manifest.Parameters));
        Assert.Equal(1, p.Min);
        Assert.Equal(10, p.Max);
    }

    [Fact]
    public void Parses_enum_type_with_choices()
    {
        var manifest = ManifestLoader.Parse(Manifest("""
            { "id": "level", "type": "enum", "name": "Level", "required": true, "choices": ["low", "high"] }
            """));

        var p = Assert.IsType<EnumParameter>(Assert.Single(manifest.Parameters));
        Assert.Equal(["low", "high"], p.Choices);
    }

    [Fact]
    public void Unknown_type_falls_back_to_StringParameter()
    {
        var manifest = ManifestLoader.Parse(Manifest("""
            { "id": "svc", "type": "service-name", "name": "Service", "required": true }
            """));

        var p = Assert.IsType<StringParameter>(Assert.Single(manifest.Parameters));
        Assert.Equal("service-name", p.Type);
    }

    private static string Manifest(string parameter) => $$"""
        {
          "id": "test.act",
          "version": "1.0.0",
          "name": "Test",
          "category": "storage",
          "parameters": [ {{parameter}} ],
          "execution": { "steps": [ { "operation": "external-process", "path": "x.exe" } ] }
        }
        """;
}
