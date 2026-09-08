import { useEffect, useState } from "react";
import { api } from "../api";
import { useDesigner } from "../store";

export function PreviewPane() {
  const report = useDesigner((s) => s.report);
  const [html, setHtml] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!report) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    api
      .renderHtml(report)
      .then((h) => !cancelled && setHtml(h))
      .catch((e) => !cancelled && setError(String(e)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [report]);

  return (
    <div className="preview-wrap">
      {loading && <div className="hint">Rendering…</div>}
      {error && <div className="error">{error}</div>}
      <iframe title="preview" className="preview-frame" srcDoc={html} />
    </div>
  );
}
