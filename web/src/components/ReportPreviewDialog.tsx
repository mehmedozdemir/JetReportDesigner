import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, Download, Eye, Loader2, Share2, X } from "lucide-react";
import { api } from "../api";
import { downloadBlob } from "../download";
import type { ReportSummary } from "../types";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";

/** Server-rendered read-only preview of a saved report, with export and a way into the
 * share dialog — all without opening the report in the designer. */
export function ReportPreviewDialog({
  report,
  onShare,
  onClose,
}: {
  report: ReportSummary;
  onShare: () => void;
  onClose: () => void;
}) {
  const [html, setHtml] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState<"pdf" | "xlsx" | null>(null);
  const { t } = useTranslation();

  useEscapeKey(onClose);
  const dialogRef = useFocusTrap<HTMLDivElement>();

  useEffect(() => {
    let cancelled = false;
    api
      .previewSavedHtml(report.id)
      .then((h) => !cancelled && setHtml(h))
      .catch((e) => !cancelled && setError(String(e)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [report.id]);

  const exportAs = async (format: "pdf" | "xlsx") => {
    setExporting(format);
    setError(null);
    try {
      const blob = await api.exportSavedBlob(report.id, format);
      downloadBlob(blob, `${report.name || "report"}.${format}`);
    } catch (e) {
      setError(String(e));
    } finally {
      setExporting(null);
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        ref={dialogRef}
        className="modal preview-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={`Preview ${report.name}`}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><Eye /> {report.name}</h2>
          <button className="mini ghost" onClick={onClose} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        <div className="preview-dialog-body">
          {error ? (
            <div className="error" style={{ padding: "10px 16px" }}>
              <AlertTriangle />
              <span>{error}</span>
            </div>
          ) : loading ? (
            <div className="preview-dialog-loading">
              <Loader2 size={16} className="spin" /> {t("preview.rendering")}
            </div>
          ) : (
            <iframe title="preview" className="preview-frame" srcDoc={html} />
          )}
        </div>

        <footer>
          <button className="mini" onClick={() => void exportAs("pdf")} disabled={exporting !== null}>
            <Download size={13} /> {exporting === "pdf" ? t("preview.exporting") : t("preview.exportPdf")}
          </button>
          <button className="mini" onClick={() => void exportAs("xlsx")} disabled={exporting !== null}>
            <Download size={13} /> {exporting === "xlsx" ? t("preview.exporting") : t("preview.exportExcel")}
          </button>
          <button className="mini" style={{ marginLeft: "auto" }} onClick={onShare}>
            <Share2 size={13} /> {t("preview.share")}
          </button>
        </footer>
      </div>
    </div>
  );
}
