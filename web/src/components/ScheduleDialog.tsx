import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { AlertTriangle, CalendarClock, X } from "lucide-react";
import { api, type ReportSchedule, type ScheduleFrequency } from "../api";
import { hhmm, localToUtc, utcToLocal, weekdayNames } from "../scheduleTime";
import { useEscapeKey } from "../useEscapeKey";
import { useFocusTrap } from "../useFocusTrap";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

const fromHHmm = (hm: string): number => {
  const [h, m] = hm.split(":").map(Number);
  return (h || 0) * 60 + (m || 0);
};

/** Create (or edit) a recurring schedule for a report — daily/weekly/monthly, a time of day
 * (entered and shown in the viewer's own timezone, converted to/from UTC at the edges — see
 * scheduleTime.ts, the API itself only knows UTC), and what to do once each run succeeds (a
 * fresh share link and/or an email with the file attached, through the tenant's mail account).
 * Pass `schedule` to edit an existing one in place instead of creating a new one. */
export function ScheduleDialog({
  reportId,
  reportName,
  schedule,
  onClose,
  onSaved,
}: {
  reportId: string;
  reportName: string;
  schedule?: ReportSchedule;
  onClose: () => void;
  onSaved?: () => void;
}) {
  const editing = !!schedule;
  const initialLocal = schedule
    ? utcToLocal({ minuteOfDayUtc: schedule.minuteOfDayUtc, dayOfWeek: schedule.dayOfWeek ?? null, dayOfMonth: schedule.dayOfMonth ?? null })
    : null;

  const [format, setFormat] = useState<"pdf" | "xlsx">(schedule?.format ?? "pdf");
  const [frequency, setFrequency] = useState<ScheduleFrequency>(schedule?.frequency ?? "Daily");
  const [time, setTime] = useState(hhmm(initialLocal?.minuteOfDay ?? 9 * 60));
  const [dayOfWeek, setDayOfWeek] = useState(initialLocal?.dayOfWeek ?? 1);
  const [dayOfMonth, setDayOfMonth] = useState(initialLocal?.dayOfMonth ?? 1);
  const [createShareLink, setCreateShareLink] = useState(schedule?.createShareLink ?? true);
  const [emailRecipients, setEmailRecipients] = useState(schedule?.emailRecipients ?? "");
  const [hasMailAccount, setHasMailAccount] = useState<boolean | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { t, i18n } = useTranslation();

  useEscapeKey(onClose);
  const dialogRef = useFocusTrap<HTMLDivElement>();

  useEffect(() => {
    api.getEmailSettings().then((s) => setHasMailAccount(!!s)).catch(() => setHasMailAccount(false));
  }, []);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const utc = localToUtc({
        minuteOfDay: fromHHmm(time),
        dayOfWeek: frequency === "Weekly" ? dayOfWeek : null,
        dayOfMonth: frequency === "Monthly" ? dayOfMonth : null,
      });
      const fields = {
        format,
        frequency,
        minuteOfDayUtc: utc.minuteOfDayUtc,
        dayOfWeek: utc.dayOfWeek,
        dayOfMonth: utc.dayOfMonth,
        enabled: schedule?.enabled ?? true,
        createShareLink,
        emailRecipients: emailRecipients.trim() || null,
      };
      if (schedule) {
        await api.updateSchedule(schedule.id, fields);
      } else {
        await api.createSchedule(reportId, fields);
      }
      setSaved(true);
      onSaved?.();
    } catch (e) {
      setError(msg(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        ref={dialogRef}
        className="modal schedule-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={editing ? t("schedule.editTitle", { name: reportName }) : t("schedule.createTitle", { name: reportName })}
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2><CalendarClock /> {editing ? t("schedule.editTitle", { name: reportName }) : t("schedule.createTitle", { name: reportName })}</h2>
          <button className="mini ghost" onClick={onClose} aria-label={t("common.close")}>
            <X />
          </button>
        </header>

        {saved ? (
          <div className="share-body">
            <p className="hint">{editing ? t("schedule.updated") : t("schedule.created")} {t("schedule.manageHint")}</p>
          </div>
        ) : (
          <>
            <div className="share-body">
              {error && <p className="hint" style={{ color: "var(--error)" }}>{error}</p>}

              <div className="settings-row">
                <span>{t("schedule.format")}</span>
                <div className="segmented" role="group" aria-label={t("schedule.format")}>
                  <button className={format === "pdf" ? "on" : ""} onClick={() => setFormat("pdf")}>PDF</button>
                  <button className={format === "xlsx" ? "on" : ""} onClick={() => setFormat("xlsx")}>Excel</button>
                </div>
              </div>

              <div className="settings-row">
                <span>{t("schedule.repeats")}</span>
                <div className="segmented" role="group" aria-label={t("schedule.frequency")}>
                  <button className={frequency === "Daily" ? "on" : ""} onClick={() => setFrequency("Daily")}>{t("schedule.daily")}</button>
                  <button className={frequency === "Weekly" ? "on" : ""} onClick={() => setFrequency("Weekly")}>{t("schedule.weekly")}</button>
                  <button className={frequency === "Monthly" ? "on" : ""} onClick={() => setFrequency("Monthly")}>{t("schedule.monthly")}</button>
                </div>
              </div>

              {frequency === "Weekly" && (
                <div className="settings-row">
                  <span>{t("schedule.on")}</span>
                  <div className="settings-control">
                    <select value={dayOfWeek} onChange={(e) => setDayOfWeek(Number(e.target.value))}>
                      {weekdayNames(i18n.language).map((d, i) => (
                        <option key={d} value={i}>{d}</option>
                      ))}
                    </select>
                  </div>
                </div>
              )}

              {frequency === "Monthly" && (
                <div className="settings-row">
                  <span>{t("schedule.onDay")}</span>
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
                    <span className="hint" style={{ margin: 0 }}>{t("schedule.clampHint")}</span>
                  </div>
                </div>
              )}

              <div className="settings-row">
                <span>{t("schedule.at")}</span>
                <div className="settings-control">
                  <input type="time" value={time} onChange={(e) => setTime(e.target.value)} />
                  <span className="hint" style={{ margin: 0 }}>
                    {t("schedule.yourTime", { zone: Intl.DateTimeFormat().resolvedOptions().timeZone })}
                  </span>
                </div>
              </div>

              <div className="settings-row">
                <span>{t("schedule.distribution")}</span>
                <div className="settings-control">
                  <label className="settings-check">
                    <input type="checkbox" checked={createShareLink} onChange={(e) => setCreateShareLink(e.target.checked)} />
                    {t("schedule.shareEachRun")}
                  </label>
                </div>
              </div>

              <div className="settings-row">
                <span>{t("schedule.emailTo")}</span>
                <div className="settings-control">
                  <input
                    value={emailRecipients}
                    placeholder={t("schedule.recipientsPlaceholder")}
                    onChange={(e) => setEmailRecipients(e.target.value)}
                  />
                </div>
              </div>

              {emailRecipients.trim() && hasMailAccount === false && (
                <p className="hint" style={{ color: "var(--warning)" }}>
                  <AlertTriangle size={12} style={{ verticalAlign: "-2px" }} /> {t("schedule.noMailAccount")}
                </p>
              )}
            </div>

            <footer>
              <button className="btn primary" onClick={() => void save()} disabled={saving}>
                {saving ? t("common.saving") : editing ? t("schedule.saveChanges") : t("schedule.create")}
              </button>
            </footer>
          </>
        )}
      </div>
    </div>
  );
}
