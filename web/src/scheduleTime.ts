/** The API stores every schedule in UTC (minuteOfDayUtc + a UTC dayOfWeek/dayOfMonth) — see
 * ScheduleRecurrence on the backend. Users think in their own wall-clock time, though, so the
 * schedule UI converts at the edges: local in the form, UTC on the wire.
 *
 * The day-of-month conversion is anchored to the current real month (via plain Date setters,
 * which normalize month-end rollover correctly) — exact for the common case, and still
 * reasonable at the rare edges where a local/UTC day boundary crossing lands on a day-of-month
 * near a month's end (the backend's own clamping-to-last-day behavior absorbs most of that). */

export interface LocalScheduleTime {
  minuteOfDay: number; // 0–1439, local wall clock
  dayOfWeek: number | null; // 0 (Sun) – 6 (Sat), local
  dayOfMonth: number | null; // 1–31, local
}

export interface UtcScheduleTime {
  minuteOfDayUtc: number;
  dayOfWeek: number | null;
  dayOfMonth: number | null;
}

export function localToUtc(local: LocalScheduleTime): UtcScheduleTime {
  const d = new Date();
  d.setHours(Math.floor(local.minuteOfDay / 60), local.minuteOfDay % 60, 0, 0);
  if (local.dayOfWeek != null) {
    d.setDate(d.getDate() + ((local.dayOfWeek - d.getDay() + 7) % 7));
  } else if (local.dayOfMonth != null) {
    d.setDate(local.dayOfMonth);
  }
  return {
    minuteOfDayUtc: d.getUTCHours() * 60 + d.getUTCMinutes(),
    dayOfWeek: local.dayOfWeek != null ? d.getUTCDay() : null,
    dayOfMonth: local.dayOfMonth != null ? d.getUTCDate() : null,
  };
}

export function utcToLocal(utc: UtcScheduleTime): LocalScheduleTime {
  const d = new Date();
  d.setUTCHours(Math.floor(utc.minuteOfDayUtc / 60), utc.minuteOfDayUtc % 60, 0, 0);
  if (utc.dayOfWeek != null) {
    d.setUTCDate(d.getUTCDate() + ((utc.dayOfWeek - d.getUTCDay() + 7) % 7));
  } else if (utc.dayOfMonth != null) {
    d.setUTCDate(utc.dayOfMonth);
  }
  return {
    minuteOfDay: d.getHours() * 60 + d.getMinutes(),
    dayOfWeek: utc.dayOfWeek != null ? d.getDay() : null,
    dayOfMonth: utc.dayOfMonth != null ? d.getDate() : null,
  };
}

export function hhmm(minuteOfDay: number): string {
  const h = Math.floor(minuteOfDay / 60)
    .toString()
    .padStart(2, "0");
  const m = (minuteOfDay % 60).toString().padStart(2, "0");
  return `${h}:${m}`;
}
