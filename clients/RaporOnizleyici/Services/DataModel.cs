using System.Globalization;
using System.Text;
using System.Text.Json;
using JetReportDesigner.Client;

namespace RaporOnizleyici.Services;

/// <summary>The rows the user is editing for one data source of the selected report.</summary>
public sealed class SourceModel
{
    public SourceModel(ReportDataSourceInfo info)
    {
        Name = info.Name;
        Kind = info.Kind;
        foreach (var f in info.Fields)
        {
            Columns.Add(f.Name);
            Types[f.Name] = f.Type;
        }

        Send = string.Equals(Kind, "json", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(info.SampleRowsJson))
        {
            TryLoadJson(info.SampleRowsJson, out _);
        }

        if (Rows.Count == 0)
        {
            Rows.Add(NewRow());
        }
    }

    public string Name { get; }

    public string Kind { get; }

    /// <summary>When off, the report keeps using its own data for this source (sample rows, or its SQL / REST query).</summary>
    public bool Send { get; set; }

    public List<string> Columns { get; } = [];

    public Dictionary<string, string> Types { get; } = new(StringComparer.Ordinal);

    public List<Dictionary<string, string>> Rows { get; } = [];

    public Dictionary<string, string> NewRow() => Columns.ToDictionary(c => c, _ => string.Empty, StringComparer.Ordinal);

    /// <summary>Field names that hold a picture (passport photo, logo, signature): edited with a file picker, sent as a data URI.</summary>
    public bool IsImageField(string column)
    {
        var n = column.ToLowerInvariant();
        return n.Contains("foto") || n.Contains("photo") || n.Contains("image") || n.Contains("resim")
            || n.Contains("logo") || n.Contains("imza") || n.Contains("signature");
    }

    public bool TryLoadJson(string json, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "JSON bir nesne dizisi olmalı: [ { ... }, { ... } ]";
                return false;
            }

            var rows = new List<Dictionary<string, string>>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    error = "Dizideki her öğe bir nesne olmalı.";
                    return false;
                }

                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var p in item.EnumerateObject())
                {
                    if (!Columns.Contains(p.Name))
                    {
                        Columns.Add(p.Name);
                    }

                    row[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString() ?? string.Empty,
                        JsonValueKind.Null => string.Empty,
                        _ => p.Value.GetRawText(),
                    };
                }

                rows.Add(row);
            }

            foreach (var r in rows)
            {
                foreach (var c in Columns)
                {
                    r.TryAdd(c, string.Empty);
                }
            }

            Rows.Clear();
            Rows.AddRange(rows);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>The rows as a JSON array, typed by the field type (numbers and booleans are not quoted). Empty cells are left out.</summary>
    public string ToJson(bool indented)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = indented, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartArray();
            foreach (var row in Rows)
            {
                w.WriteStartObject();
                foreach (var c in Columns)
                {
                    var v = row.TryGetValue(c, out var s) ? s : string.Empty;
                    if (string.IsNullOrEmpty(v))
                    {
                        continue;
                    }

                    var type = Types.TryGetValue(c, out var t) ? t.ToLowerInvariant() : "string";
                    if (type == "number" && TryParseNumber(v, out var d))
                    {
                        w.WriteNumber(c, d);
                    }
                    else if (type == "boolean" && bool.TryParse(v, out var b))
                    {
                        w.WriteBoolean(c, b);
                    }
                    else
                    {
                        w.WriteString(c, v);
                    }
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        || double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
