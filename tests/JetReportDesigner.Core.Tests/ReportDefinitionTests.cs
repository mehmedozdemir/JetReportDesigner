using FluentValidation;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Schema;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Core.Validation;

namespace JetReportDesigner.Core.Tests;

public class ReportDefinitionTests
{
    private static ReportDefinition SampleFreeReport() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Invoice",
        LayoutMode = LayoutMode.Free,
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "orders",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = """[{"id":1,"total":9.5}]""" },
                Fields = [new DataField { Name = "id", Type = FieldType.Number }],
            },
        ],
        Body = new ReportBody
        {
            Height = 1000,
            Elements =
            [
                new ReportElement
                {
                    Id = "title",
                    Type = ElementType.Label,
                    Text = "Invoice",
                    Bounds = new Bounds { X = 40, Y = 40, Width = 200, Height = 24 },
                    Style = new ReportStyle { Font = new FontSpec { Size = 16, Bold = true } },
                },
                new ReportElement
                {
                    Id = "total",
                    Type = ElementType.Field,
                    Value = "{orders.total}",
                    Format = "n2",
                    Bounds = new Bounds { X = 40, Y = 80, Width = 120, Height = 18 },
                },
            ],
        },
    };

    [Fact]
    public void RoundTrips_Through_Json_ByteForByte()
    {
        var original = SampleFreeReport();

        var json = ReportJson.Serialize(original);
        var restored = ReportJson.Deserialize(json);
        var reserialized = ReportJson.Serialize(restored);

        Assert.Equal(json, reserialized);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Body!.Elements.Count, restored.Body!.Elements.Count);
        Assert.Equal("{orders.total}", restored.Body.Elements[1].Value);
        Assert.Equal(LayoutMode.Free, restored.LayoutMode);
    }

    [Fact]
    public void Json_Uses_CamelCase_And_String_Enums()
    {
        var json = ReportJson.Serialize(SampleFreeReport());

        Assert.Contains("\"layoutMode\":\"free\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"label\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"LayoutMode\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validator_Accepts_Valid_Free_Report()
    {
        var result = await new ReportDefinitionValidator().ValidateAsync(SampleFreeReport());
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task Validator_Rejects_Free_Report_With_Bands()
    {
        var report = SampleFreeReport();
        report.Bands.Add(new Band { Type = BandType.Detail, Height = 20 });

        var result = await new ReportDefinitionValidator().ValidateAsync(report);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validator_Rejects_Banded_Report_With_Unknown_Band_DataSource()
    {
        var report = new ReportDefinition
        {
            Name = "Bad",
            LayoutMode = LayoutMode.Banded,
            Bands = [new Band { Type = BandType.Detail, Height = 20, DataSource = "missing" }],
        };

        var result = await new ReportDefinitionValidator().ValidateAsync(report);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("data source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Embedded_Schema_Is_Present_And_Wellformed()
    {
        var schema = ReportDefinitionSchema.Json;

        Assert.Contains("\"title\": \"ReportDefinition\"", schema, StringComparison.Ordinal);
        using var doc = System.Text.Json.JsonDocument.Parse(schema);
        Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ValidateAndThrow_Throws_ValidationException_On_Invalid()
    {
        var invalid = new ReportDefinition { Name = "", LayoutMode = LayoutMode.Free };

        Assert.Throws<ValidationException>(() => new ReportDefinitionValidator().ValidateAndThrow(invalid));
    }
}
