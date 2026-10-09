using System;

namespace JetReportDesigner.Client
{
    /// <summary>A report this application can render.</summary>
    public sealed class ReportInfo
    {
        public ReportInfo(Guid id, string code, string name)
        {
            Id = id;
            Code = code;
            Name = name;
        }

        public Guid Id { get; }

        /// <summary>The stable unique name to call the report by (lowercase, no spaces), e.g. <c>barkod-rapor-claude</c>.</summary>
        public string Code { get; }

        /// <summary>The display name shown in the designer. It can be renamed at any time; the code stays.</summary>
        public string Name { get; }
    }
}
