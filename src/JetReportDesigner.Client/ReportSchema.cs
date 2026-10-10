using System.Collections.Generic;

namespace JetReportDesigner.Client
{
    /// <summary>What a report expects from the caller: its parameters and the data sources (with fields) it can be given rows for.</summary>
    public sealed class ReportSchema
    {
        public ReportSchema(string code, string name, IReadOnlyList<ReportParameterInfo> parameters, IReadOnlyList<ReportDataSourceInfo> dataSources)
        {
            Code = code;
            Name = name;
            Parameters = parameters;
            DataSources = dataSources;
        }

        public string Code { get; }

        public string Name { get; }

        public IReadOnlyList<ReportParameterInfo> Parameters { get; }

        public IReadOnlyList<ReportDataSourceInfo> DataSources { get; }
    }

    public sealed class ReportParameterInfo
    {
        public ReportParameterInfo(string name, string label, string type, string defaultValue, bool required, IReadOnlyList<string> allowedValues)
        {
            Name = name;
            Label = label;
            Type = type;
            DefaultValue = defaultValue;
            Required = required;
            AllowedValues = allowedValues;
        }

        public string Name { get; }

        public string Label { get; }

        /// <summary>string | number | boolean | date | dateTime</summary>
        public string Type { get; }

        public string DefaultValue { get; }

        public bool Required { get; }

        /// <summary>Empty when any value is allowed.</summary>
        public IReadOnlyList<string> AllowedValues { get; }
    }

    public sealed class ReportDataSourceInfo
    {
        public ReportDataSourceInfo(string name, string kind, IReadOnlyList<ReportFieldInfo> fields, string sampleRowsJson)
        {
            Name = name;
            Kind = kind;
            Fields = fields;
            SampleRowsJson = sampleRowsJson;
        }

        /// <summary>The key to pass to <see cref="ReportData.Add(string, System.Collections.IEnumerable)"/>.</summary>
        public string Name { get; }

        /// <summary>json | rest | sql</summary>
        public string Kind { get; }

        /// <summary>Declared fields plus every <c>{name.field}</c> the report actually uses.</summary>
        public IReadOnlyList<ReportFieldInfo> Fields { get; }

        /// <summary>The report's own sample rows (JSON array) for json sources; null otherwise.</summary>
        public string SampleRowsJson { get; }
    }

    public sealed class ReportFieldInfo
    {
        public ReportFieldInfo(string name, string type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }

        /// <summary>string | number | boolean | date | dateTime</summary>
        public string Type { get; }
    }
}
