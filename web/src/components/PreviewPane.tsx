import { useEffect, useState } from "react";
import { AlertTriangle, Eye, Loader2 } from "lucide-react";
import { api } from "../api";
import { useDesigner } from "../store";

export function PreviewPane({
  parameters,
  active,
}: {
  parameters: Record<string, unknown>;
  active: boolean;
}) {
  const report = useDesigner((s) => s.report);
  const [html, setHtml] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!report || !active) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .renderHtml(report, parameters)
      .then((h) => !cancelled && setHtml(h))
      .catch((e) => !cancelled && setError(String(e)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [report, parameters, active]);

  return (
    <div className="preview-wrap">
      <div className="preview-bar">
        {loading ? <Loader2 size={14} className="spin" /> : <Eye size={14} />}
        <span>{loading ? "Rendering…" : "Server-rendered preview"}</span>
      </div>
      {error && (
        <div className="error" style={{ padding: "10px 16px" }}>
          <AlertTriangle />
          <span>{error}</span>
        </div>
      )}
      <iframe title="preview" className="preview-frame" srcDoc={html} />
    </div>
  );
}
