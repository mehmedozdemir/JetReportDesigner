import { useEffect, useState } from "react";
import { AlertTriangle, FileBarChart2, FileSpreadsheet, FileText } from "lucide-react";

/** The public landing page for a share link (`/share/:token`) — fully unauthenticated,
 * outside the main app shell. Read-only: a server-rendered preview plus export, never
 * the design surface. */
export function SharedReportView({ token }: { token: string }) {
  const [name, setName] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetch(`/api/share/${token}`)
      .then(async (r) => {
        if (!r.ok) {
          throw new Error(r.status === 404 ? "This link is invalid or has been revoked." : `${r.status} ${r.statusText}`);
        }
        return (await r.json()) as { reportName: string };
      })
      .then((d) => !cancelled && setName(d.reportName))
      .catch((e) => !cancelled && setError(String(e instanceof Error ? e.message : e)));
    return () => {
      cancelled = true;
    };
  }, [token]);

  if (error) {
    return (
      <div className="shared-view shared-view-message">
        <AlertTriangle />
        <p>{error}</p>
      </div>
    );
  }

  if (!name) {
    return <div className="shared-view shared-view-message">Loading…</div>;
  }

  return (
    <div className="shared-view">
      <header className="shared-view-head">
        <div className="brand">
          <FileBarChart2 size={18} />
          <span>{name}</span>
        </div>
        <div className="row">
          <a className="btn outline" href={`/api/share/${token}/render?format=pdf`}>
            <FileText size={14} /> PDF
          </a>
          <a className="btn outline" href={`/api/share/${token}/render?format=xlsx`}>
            <FileSpreadsheet size={14} /> Excel
          </a>
        </div>
      </header>
      <iframe title="Shared report preview" className="shared-view-frame" src={`/api/share/${token}/preview`} />
    </div>
  );
}
