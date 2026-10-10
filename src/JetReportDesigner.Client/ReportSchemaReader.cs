using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JetReportDesigner.Client
{
    internal static class ReportSchemaReader
    {
        // {source.field} as used by labels, fields, tables, charts, barcodes and image sources.
        private static readonly Regex Token = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

        public static ReportSchema Read(string responseJson)
        {
            using (var doc = JsonDocument.Parse(responseJson))
            {
                var def = doc.RootElement.GetProperty("definition");

                var used = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (Match m in Token.Matches(def.GetRawText()))
                {
                    var src = m.Groups[1].Value;
                    List<string> list;
                    if (!used.TryGetValue(src, out list)) used[src] = list = new List<string>();
                    if (!list.Contains(m.Groups[2].Value)) list.Add(m.Groups[2].Value);
                }

                var sources = new List<ReportDataSourceInfo>();
                JsonElement dsArray;
                if (def.TryGetProperty("dataSources", out dsArray) && dsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ds in dsArray.EnumerateArray())
                    {
                        var name = Str(ds, "name");
                        var kind = Str(ds, "kind") ?? "json";
                        var fields = new List<ReportFieldInfo>();
                        JsonElement declared;
                        if (ds.TryGetProperty("fields", out declared) && declared.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var f in declared.EnumerateArray())
                            {
                                Add(fields, Str(f, "name"), Str(f, "type"));
                            }
                        }

                        string sample = null;
                        JsonElement jsonCfg;
                        if (kind.Equals("json", StringComparison.OrdinalIgnoreCase) && ds.TryGetProperty("json", out jsonCfg))
                        {
                            sample = Str(jsonCfg, "inlineData");
                            TryAddSampleKeys(fields, sample);
                        }

                        List<string> usedFields;
                        if (name != null && used.TryGetValue(name, out usedFields))
                        {
                            foreach (var u in usedFields) Add(fields, u, null);
                        }

                        sources.Add(new ReportDataSourceInfo(name, kind, fields, sample));
                    }
                }

                var parameters = new List<ReportParameterInfo>();
                JsonElement pArray;
                if (def.TryGetProperty("parameters", out pArray) && pArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pArray.EnumerateArray())
                    {
                        var allowed = new List<string>();
                        JsonElement av;
                        if (p.TryGetProperty("allowedValues", out av) && av.ValueKind == JsonValueKind.Array)
                        {
                            allowed.AddRange(av.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText()));
                        }

                        JsonElement dv;
                        string defaultValue = null;
                        if (p.TryGetProperty("defaultValue", out dv) && dv.ValueKind != JsonValueKind.Null)
                        {
                            defaultValue = dv.ValueKind == JsonValueKind.String ? dv.GetString() : dv.GetRawText();
                        }

                        JsonElement req;
                        parameters.Add(new ReportParameterInfo(
                            Str(p, "name"),
                            Str(p, "label") ?? Str(p, "name"),
                            Str(p, "type") ?? "string",
                            defaultValue,
                            p.TryGetProperty("required", out req) && req.ValueKind == JsonValueKind.True,
                            allowed));
                    }
                }

                return new ReportSchema(Str(def, "code"), Str(def, "name"), parameters, sources);
            }
        }

        private static void Add(List<ReportFieldInfo> fields, string name, string type)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (fields.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal))) return;
            fields.Add(new ReportFieldInfo(name, type ?? "string"));
        }

        private static void TryAddSampleKeys(List<ReportFieldInfo> fields, string sample)
        {
            if (string.IsNullOrWhiteSpace(sample)) return;
            try
            {
                using (var doc = JsonDocument.Parse(sample))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) return;
                    foreach (var row in doc.RootElement.EnumerateArray().Take(1))
                    {
                        if (row.ValueKind != JsonValueKind.Object) continue;
                        foreach (var prop in row.EnumerateObject())
                        {
                            string type;
                            switch (prop.Value.ValueKind)
                            {
                                case JsonValueKind.Number: type = "number"; break;
                                case JsonValueKind.True:
                                case JsonValueKind.False: type = "boolean"; break;
                                default: type = "string"; break;
                            }

                            Add(fields, prop.Name, type);
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        private static string Str(JsonElement e, string name)
        {
            JsonElement v;
            return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
    }
}
