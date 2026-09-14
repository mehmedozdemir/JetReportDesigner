import { useEffect, useState } from "react";
import { AlertTriangle, CalendarClock, Loader2, Trash2 } from "lucide-react";
import { api, type ReportSchedule } from "../api";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));
const WEEKDAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

function describe(s: ReportSchedule): string {
  const pad = (n: number) => n.toString().padStart(2, "0");
  const time = `${pad(Math.floor(s.minuteOfDayUtc / 60))}:${pad(s.minuteOfDayUtc % 60)} UTC`;
  if (s.frequency === "Weekly") return `Weekly on ${WEEKDAYS[s.dayOfWeek ?? 0]} at ${time}`;
  if (s.frequency === "Monthly") return `Monthly on day ${s.dayOfMonth ?? 1} at ${time}`;
  return `Daily at ${time}`;
}

/** Manage every recurring report schedule — tenant-wide, Designer-only, reached from the
 * Start screen like Team/Email. Creating a new one happens from a report's context menu
 * ("Schedule…"); this page is for reviewing, pausing, and removing existing ones. */
export function SchedulesPage() {
  const [schedules, setSchedules] = useState<ReportSchedule[] | null>(null);
  const [err, setErr] = useState<string | null>(null);

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
            {schedules.map((s) => (
              <tr key={s.id}>
                <td className="drive-table-name">
                  {s.reportName} <span className="chip">{s.format}</span>
                </td>
                <td>{describe(s)}</td>
                <td>
                  {[s.createShareLink && "link", s.emailRecipients && "email"].filter(Boolean).join(" + ") || "history only"}
                </td>
                <td>{new Date(s.nextRunAtUtc).toLocaleString()}</td>
                <td>{s.lastRunAtUtc ? new Date(s.lastRunAtUtc).toLocaleString() : "—"}</td>
                <td>
                  <label className="settings-check">
                    <input type="checkbox" checked={s.enabled} onChange={() => void toggle(s)} />
                  </label>
                </td>
                <td>
                  <button className="mini danger" title="Delete schedule" aria-label="Delete schedule" onClick={() => void remove(s.id)}>
                    <Trash2 size={13} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
