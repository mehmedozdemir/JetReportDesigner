import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertCircle, AlertTriangle, CheckCircle2, ShieldCheck } from "lucide-react";
import { api } from "../api";
import { useDesigner } from "../store";
import type { ReportIssue } from "../types";

export function ProblemsPanel() {
  const { t } = useTranslation();
  const report = useDesigner((s) => s.report);
  const select = useDesigner((s) => s.select);
  const [issues, setIssues] = useState<ReportIssue[]>([]);

  useEffect(() => {
    if (!report) {
      setIssues([]);
      return;
    }
    let cancelled = false;
    const handle = setTimeout(() => {
      api.validate(report).then((r) => !cancelled && setIssues(r)).catch(() => undefined);
    }, 400);
    return () => {
      cancelled = true;
      clearTimeout(handle);
    };
  }, [report]);

  if (!report) return null;

  const errors = issues.filter((i) => i.severity === "error").length;
  const badgeClass = errors ? "count-badge err" : issues.length ? "count-badge warn" : "count-badge";

  return (
    <div className="panel">
      <h2>
        <ShieldCheck /> {t("problems.title")}
        {issues.length > 0 && <span className={badgeClass}>{issues.length}</span>}
      </h2>
      {issues.length === 0 ? (
        <p className="hint" style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <CheckCircle2 size={14} color="var(--success)" /> {t("problems.none")}
        </p>
      ) : (
        <ul className="problems">
          {issues.map((issue, i) => (
            <li
              key={i}
              className={issue.severity}
              onClick={() => issue.elementId && select([issue.elementId])}
              title={issue.elementId ? t("problems.selectAffected") : undefined}
            >
              {issue.severity === "error" ? <AlertCircle /> : <AlertTriangle />}
              <span>{issue.message}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
