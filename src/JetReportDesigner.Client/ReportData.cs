using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;

namespace JetReportDesigner.Client
{
    /// <summary>
    /// The rows to put into a report, keyed by the report's data source name. Whatever you add replaces
    /// that data source's sample rows for this one render. Accepts any list of objects, a
    /// <see cref="DataTable"/>, or a ready JSON array.
    /// </summary>
    /// <example>
    /// <code>
    /// var data = new ReportData().Add("data", new[] { new { Adi = "Ahmet", Barkod = "2750365698456" } });
    /// </code>
    /// </example>
    public sealed class ReportData
    {
        internal Dictionary<string, JsonElement> Sources { get; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        /// <summary>Adds rows from any enumerable of objects (POCOs, anonymous types, dictionaries).</summary>
        public ReportData Add(string dataSource, IEnumerable rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (rows is DataTable table) return Add(dataSource, table);

            Sources[Name(dataSource)] = ToElement(rows);
            return this;
        }

        public ReportData Add(string dataSource, DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var rows = new List<Dictionary<string, object>>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                var item = new Dictionary<string, object>(table.Columns.Count);
                foreach (DataColumn column in table.Columns)
                {
                    var value = row[column];
                    item[column.ColumnName] = value is DBNull ? null : value;
                }

                rows.Add(item);
            }

            Sources[Name(dataSource)] = ToElement(rows);
            return this;
        }

        /// <summary>Adds rows from a JSON array of objects, as-is.</summary>
        public ReportData AddJson(string dataSource, string jsonArray)
        {
            if (jsonArray == null) throw new ArgumentNullException(nameof(jsonArray));
            using (var doc = JsonDocument.Parse(jsonArray))
            {
                Sources[Name(dataSource)] = doc.RootElement.Clone();
            }

            return this;
        }

        private static string Name(string dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource)) throw new ArgumentException("Data source name is required.", nameof(dataSource));
            return dataSource;
        }

        private static JsonElement ToElement(object rows)
        {
            // Property names are kept exactly as given: the report binds to {source.FieldName}.
            using (var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(rows)))
            {
                return doc.RootElement.Clone();
            }
        }
    }
}
