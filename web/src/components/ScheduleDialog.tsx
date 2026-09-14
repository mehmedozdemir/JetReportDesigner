import { useEffect, useState } from "react";
import { AlertTriangle, CalendarClock, X } from "lucide-react";
import { api, type ScheduleFrequency } from "../api";
import type { ReportSummary } from "../types";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

const WEEKDAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

const fromHHmm = (hhmm: string): number => {
  const [h, m] = hhmm.split(":").map(Number);
  return (h || 0) * 60 + (m || 0);
};

/** Create a recurring schedule for a report — daily/weekly/monthly, a UTC time of day, and
 * what to do once each run succeeds (a fresh share link and/or an email with the file
 * attached, through the tenant's configured mail account). */
export function ScheduleDialog({ report, onClose }: { report: ReportSummary; onClose: () => void }) {
  const [format, setFormat] = useState<"pdf" | "xlsx">("pdf");
  const [frequency, setFrequency] = useState<ScheduleFrequency>("Daily");
  const [time, setTime] = useState("09:00");
  const [dayOfWeek, setDayOfWeek] = useState(1);
  const [dayOfMonth, setDayOfMonth] = useState(1);
  const [createShareLink, setCreateShareLink] = useState(true);
  const [emailRecipients, setEmailRecipients] = useState("");
  const [hasMailAccount, setHasMailAccount] = useState<boolean | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.getEmailSettings().then((s) => setHasMailAccount(!!s)).catch(() => setHasMailAccount(false));
  }, []);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.createSchedule(report.id, {
        format,
        frequency,
        minuteOfDayUtc: fromHHmm(time),
        dayOfWeek: frequency === "Weekly" ? dayOfWeek : null,
        dayOfMonth: frequency === "Monthly" ? dayOfMonth : null,
        enabled: true,
        createShareLink,
        emailRecipients: emailRecipients.trim() || null,
      });
      setSaved(true);
    } catch (e) {
      setError(msg(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal schedule-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={`Schedule ${report.name}`}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><CalendarClock /> Schedule “{report.name}”</h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        {saved ? (
          <div className="share-body">
            <p className="hint">Scheduled. Manage it from the Schedules tab.</p>
          </div>
        ) : (
          <>
            <div className="share-body">
              {error && <p className="hint" style={{ color: "var(--error)" }}>{error}</p>}

              <div className="settings-row">
                <span>Format</span>
                <div className="segmented" role="group" aria-label="Format">
                  <button className={format === "pdf" ? "on" : ""} onClick={() => setFormat("pdf")}>PDF</button>
                  <button className={format === "xlsx" ? "on" : ""} onClick={() => setFormat("xlsx")}>Excel</button>
                </div>
              </div>

              <div className="settings-row">
                <span>Repeats</span>
                <div className="segmented" role="group" aria-label="Frequency">
                  <button className={frequency === "Daily" ? "on" : ""} onClick={() => setFrequency("Daily")}>Daily</button>
                  <button className={frequency === "Weekly" ? "on" : ""} onClick={() => setFrequency("Weekly")}>Weekly</button>
                  <button className={frequency === "Monthly" ? "on" : ""} onClick={() => setFrequency("Monthly")}>Monthly</button>
                </div>
              </div>

              {frequency === "Weekly" && (
                <div className="settings-row">
                  <span>On</span>
                  <div className="settings-control">
                    <select value={dayOfWeek} onChange={(e) => setDayOfWeek(Number(e.target.value))}>
                      {WEEKDAYS.map((d, i) => (
                        <option key={d} value={i}>{d}</option>
                      ))}
                    </select>
                  </div>
                </div>
              )}

              {frequency === "Monthly" && (
                <div className="settings-row">
                  <span>On day</span>
                  <div className="settings-control">
                    <input
                      type="number"
                      className="mini-num"
                      style={{ width: 56 }}
                      min={1}
                      max={31}
                      value={dayOfMonth}
                      onChange={(e) => setDayOfMonth(Number(e.target.value))}
                    />
                    <span className="hint" style={{ margin: 0 }}>Clamped to the last day in shorter months.</span>
                  </div>
                </div>
              )}

              <div className="settings-row">
                <span>At (UTC)</span>
                <div className="settings-control">
                  <input type="time" value={time} onChange={(e) => setTime(e.target.value)} />
                </div>
              </div>

              <div className="settings-row">
                <span>Distribution</span>
                <div className="settings-control">
                  <label className="settings-check">
                    <input type="checkbox" checked={createShareLink} onChange={(e) => setCreateShareLink(e.target.checked)} />
                    Create a share link each run
                  </label>
                </div>
              </div>

              <div className="settings-row">
                <span>Email to</span>
                <div className="settings-control">
                  <input
                    value={emailRecipients}
                    placeholder="a@example.com, b@example.com"
                    onChange={(e) => setEmailRecipients(e.target.value)}
                  />
                </div>
              </div>

              {emailRecipients.trim() && hasMailAccount === false && (
                <p className="hint" style={{ color: "var(--warning)" }}>
                  <AlertTriangle size={12} style={{ verticalAlign: "-2px" }} /> No mail account is configured yet (Email tab)
                  — emailing will silently be skipped until you set one up.
                </p>
              )}
            </div>

            <footer>
              <button className="btn primary" onClick={() => void save()} disabled={saving}>
                {saving ? "Saving…" : "Create schedule"}
              </button>
            </footer>
          </>
        )}
      </div>
    </div>
  );
}
