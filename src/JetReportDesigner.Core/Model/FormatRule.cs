namespace JetReportDesigner.Core.Model;

/// <summary>Comparison operators for a <see cref="FormatRule"/>.</summary>
public enum ComparisonOp
{
    Eq,
    Ne,
    Gt,
    Ge,
    Lt,
    Le,
    Contains,
    StartsWith,
    EndsWith,
    IsEmpty,
    IsNotEmpty,
}

/// <summary>
/// One conditional-formatting rule on an element or a band: when the current row's
/// <see cref="Field"/> satisfies <see cref="Op"/> against <see cref="Value"/>, the
/// non-null properties of <see cref="Style"/> are layered on, and <see cref="Hidden"/>
/// removes the element entirely. All matching rules apply, in order.
/// </summary>
public sealed class FormatRule
{
    /// <summary>Row field to test. A <c>dataSource.field</c> form is accepted; only the field part is used.</summary>
    public string Field { get; set; } = string.Empty;

    public ComparisonOp Op { get; set; }

    /// <summary>The value compared against. Parsed as a number or date when both sides parse; otherwise text.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Style overrides applied when the rule matches. Null properties are left untouched.</summary>
    public ReportStyle Style { get; set; } = new();

    /// <summary>Hide the element when the rule matches (ignored for bands).</summary>
    public bool Hidden { get; set; }
}
