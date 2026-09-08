import { useEffect, useState } from "react";
import { api } from "../api";
import { useDesigner } from "../store";
import type { ReportIssue } from "../types";

export function ProblemsPanel() {
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

  return (
    <div className="panel">
      <h2>
        Problems{" "}
        {issues.length > 0 && (
          <span className={errors ? "badge err" : "badge warn"}>{issues.length}</span>
        )}
      </h2>
      {issues.length === 0 ? (
        <p className="hint">No problems.</p>
      ) : (
        <ul className="problems">
          {issues.map((issue, i) => (
            <li
              key={i}
              className={issue.severity}
              onClick={() => issue.elementId && select([issue.elementId])}
              title={issue.elementId ? "Select element" : undefined}
            >
              <span className="dot" />
              {issue.message}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
