import { useEffect, useState } from "react";
import { AlertTriangle, CalendarClock, CheckCircle2, Loader2, Pencil, Trash2 } from "lucide-react";
import { api, type ReportSchedule } from "../api";
import { hhmm, utcToLocal } from "../scheduleTime";
import { ConfirmButton } from "./ConfirmButton";
import { ScheduleDialog } from "./ScheduleDialog";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));
const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

/** Recurrence description in the viewer's own timezone — the API stores everything in UTC
 * (see scheduleTime.ts), so day-of-week/day-of-month can shift by one relative to what's
 * stored, not just the time. */
function describe(s: ReportSchedule): string {
  const local = utcToLocal({ minuteOfDayUtc: s.minuteOfDayUtc, dayOfWeek: s.dayOfWeek ?? null, dayOfMonth: s.dayOfMonth ?? null });
  const time = hhmm(local.minuteOfDay);
  if (s.frequency === "Weekly") return `Weekly on ${WEEKDAYS[local.dayOfWeek ?? 0]} at ${time}`;
  if (s.frequency === "Monthly") return `Monthly on day ${local.dayOfMonth ?? 1} at ${time}`;
  return `Daily at ${time}`;
}

/** Manage every recurring report schedule — tenant-wide, Designer-only, reached from the
 * Start screen like Team/Email. Creating a new one happens from a report's context menu
 * ("Schedule…"); this page is for reviewing, pausing, and removing existing ones. */
export function SchedulesPage() {
  const [schedules, setSchedules] = useState<ReportSchedule[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [editing, setEditing] = useState<ReportSchedule | null>(null);

  const refresh = () => api.listSchedules().then(setSchedules).catch((e) => setErr(msg(e)));
  useEffect(() => {
    void refresh();
  }, []);

  const toggle = async (s: ReportSchedule) => {
    setErr(null);
    try {
      await api.updateSchedule(s.id, { ...s, enabled: !s.enabled });
      await refresh();
    } catch (e) {
      setErr(msg(e));
    }
  };

  const remove = async (id: string) => {
    setErr(null);
    try {
      await api.deleteSchedule(id);
      await refresh();
    } catch (e) {
      setErr(msg(e));
    }
  };

  if (!schedules) {
    return (
      <section className="start-section">
        <div className="share-loading">
          <Loader2 size={16} className="spin" />
        </div>
      </section>
    );
  }

  return (
    <section className="start-section">
      <h3>Schedules</h3>
      <p className="hint">
        Right-click a report in the list and choose "Schedule…" to set up a new recurring run.
      </p>

      {err && (
        <div className="error small">
          <AlertTriangle /> <span>{err}</span>
        </div>
      )}

      {schedules.length === 0 ? (
        <div className="start-empty">
          <CalendarClock />
          <div>No schedules yet.</div>
        </div>
      ) : (
        <table className="drive-table">
          <thead>
            <tr>
              <th>Report</th>
              <th>Recurrence</th>
              <th>Distribution</th>
              <th>Next run</th>
              <th>Last run</th>
              <th>Enabled</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {schedules.map((s) => {
              const distributionLabel =
                [s.createShareLink && "link", s.emailRecipients && "email"].filter(Boolean).join(" + ") || "history only";
              return (
                <tr key={s.id}>
                  <td className="drive-table-name">
                    {s.reportName} <span className="chip">{s.format}</span>
                  </td>
                  <td>{describe(s)}</td>
                  <td>
                    <span className="row" style={{ gap: 4 }}>
                      {distributionLabel}
                      {s.lastDistributionAtUtc &&
                        (s.lastDistributionError ? (
                          <span title={`Last attempt failed: ${s.lastDistributionError}`}>
                            <AlertTriangle size={13} style={{ color: "var(--error)" }} />
                          </span>
                        ) : (
                          <span title="Last attempt delivered">
                            <CheckCircle2 size={13} style={{ color: "var(--success)" }} />
                          </span>
                        ))}
                    </span>
                  </td>
                  <td>{new Date(s.nextRunAtUtc).toLocaleString()}</td>
                  <td>{s.lastRunAtUtc ? new Date(s.lastRunAtUtc).toLocaleString() : "—"}</td>
                  <td>
                    <label className="settings-check">
                      <input type="checkbox" checked={s.enabled} onChange={() => void toggle(s)} />
                    </label>
                  </td>
                  <td>
                    <div className="row" style={{ gap: 4 }}>
                      <button className="mini" title="Edit schedule" aria-label="Edit schedule" onClick={() => setEditing(s)}>
                        <Pencil size={13} />
                      </button>
                      <ConfirmButton
                        icon={Trash2}
                        title="Delete schedule"
                        confirmLabel="Delete"
                        onConfirm={() => void remove(s.id)}
                      />
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      {editing && (
        <ScheduleDialog
          reportId={editing.reportId}
          reportName={editing.reportName}
          schedule={editing}
          onClose={() => setEditing(null)}
          onSaved={refresh}
        />
      )}
    </section>
  );
}
