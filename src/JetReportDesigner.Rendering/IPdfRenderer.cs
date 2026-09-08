namespace JetReportDesigner.Rendering;

/// <summary>
/// Renders a <see cref="RenderDocument"/> to a PDF byte stream. Phase 0 ships two
/// implementations (QuestPDF, MigraDoc/PdfSharp) for the engine-selection spike
/// documented in <c>docs/04-pdf-motoru-karari.md</c>; one is kept afterwards.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>Stable identifier of the engine, e.g. "questpdf" or "migradoc".</summary>
    string EngineName { get; }

    byte[] Render(RenderDocument document);
}

/// <summary>Shared unit conversion. Model units are 1/96 inch; PDF is 1/72 inch (points).</summary>
public static class RenderUnits
{
    public const double PxToPt = 72.0 / 96.0;

    public static double ToPoints(double px) => px * PxToPt;
}
