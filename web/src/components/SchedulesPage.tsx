import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import { AlertTriangle, CalendarClock, CheckCircle2, Loader2, Pencil, Trash2 } from "lucide-react";
import { api, type ReportSchedule } from "../api";
import { hhmm, utcToLocal } from "../scheduleTime";
import { ConfirmButton } from "./ConfirmButton";
import { PageHeader } from "./PageHeader";
import { SortableTh, useSort } from "./SortableTh";
import { ScheduleDialog } from "./ScheduleDialog";

const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Weekday names come from the browser's own locale data rather than a hard-coded list, so they
 * follow whichever language is selected without us translating seven more strings per language. */
function weekdayName(dayOfWeek: number, language: string): string {
  // 2026-09-13 was a Sunday, so adding the index lands on the right day.
  return new Date(Date.UTC(2026, 8, 13 + dayOfWeek)).toLocaleDateString(language, { weekday: "short", timeZone: "UTC" });
}

/** Recurrence description in the viewer's own timezone — the API stores everything in UTC
 * (see scheduleTime.ts), so day-of-week/day-of-month can shift by one relative to what's
 * stored, not just the time. */
function describe(s: ReportSchedule, t: TFunction, language: string): string {
  const local = utcToLocal({ minuteOfDayUtc: s.minuteOfDayUtc, dayOfWeek: s.dayOfWeek ?? null, dayOfMonth: s.dayOfMonth ?? null });
  const time = hhmm(local.minuteOfDay);
  if (s.frequency === "Weekly") return t("schedules.weekly", { day: weekdayName(local.dayOfWeek ?? 0, language), time });
  if (s.frequency === "Monthly") return t("schedules.monthly", { day: local.dayOfMonth ?? 1, time });
  return t("schedules.daily", { time });
}

/** Manage every recurring report schedule — tenant-wide, Designer-only, reached from the
 * Start screen like Team/Email. Creating a new one happens from a report's context menu
 * ("Schedule…"); this page is for reviewing, pausing, and removing existing ones. */
export function SchedulesPage() {
  const { t, i18n } = useTranslation();
  const [schedules, setSchedules] = useState<ReportSchedule[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [editing, setEditing] = useState<ReportSchedule | null>(null);
  const { sort, toggle: toggleSort, apply } = useSort<"reportName" | "nextRunAtUtc" | "lastRunAtUtc">({
    key: "nextRunAtUtc",
    dir: "asc",
  });

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
      <PageHeader
        title={t("schedules.title")}
        description={t("schedules.description")}
      />

      {err && (
        <div className="error small">
          <AlertTriangle /> <span>{err}</span>
        </div>
      )}

      {schedules.length === 0 ? (
        <div className="start-empty">
          <CalendarClock />
          <div>{t("schedules.empty")}</div>
        </div>
      ) : (
        <div className="data-card">
        <table className="drive-table">
          <thead>
            <tr>
              <SortableTh column="reportName" sort={sort} onToggle={toggleSort}>{t("jobs.report")}</SortableTh>
              <th>{t("schedules.recurrence")}</th>
              <th>{t("schedules.distribution")}</th>
              <SortableTh column="nextRunAtUtc" sort={sort} onToggle={toggleSort}>{t("schedules.nextRun")}</SortableTh>
              <SortableTh column="lastRunAtUtc" sort={sort} onToggle={toggleSort}>{t("schedules.lastRun")}</SortableTh>
              <th>{t("schedules.enabled")}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {apply(schedules, (s, key) => s[key]).map((s) => {
              const distributionLabel =
                [s.createShareLink && "link", s.emailRecipients && "email"].filter(Boolean).join(" + ") || t("schedules.historyOnly");
              return (
                <tr key={s.id}>
                  <td className="drive-table-name">
                    {s.reportName} <span className="chip">{s.format}</span>
                  </td>
                  <td>{describe(s, t, i18n.language)}</td>
                  <td>
                    <span className="row" style={{ gap: 4 }}>
                      {distributionLabel}
                      {s.lastDistributionAtUtc &&
                        (s.lastDistributionError ? (
                          <span title={t("schedules.failedLast", { error: s.lastDistributionError })}>
                            <AlertTriangle size={13} style={{ color: "var(--error)" }} />
                          </span>
                        ) : (
                          <span title={t("schedules.deliveredLast")}>
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
                      <button className="mini" title={t("schedules.editSchedule")} aria-label={t("schedules.editSchedule")} onClick={() => setEditing(s)}>
                        <Pencil size={13} />
                      </button>
                      <ConfirmButton
                        icon={Trash2}
                        title={t("schedules.deleteSchedule")}
                        confirmLabel={t("common.delete")}
                        onConfirm={() => void remove(s.id)}
                      />
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        </div>
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
