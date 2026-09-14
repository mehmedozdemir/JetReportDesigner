namespace JetReportDesigner.Storage.Schedules;

/// <summary>
/// Pure recurrence math for <see cref="Entities.ReportSchedule"/> — kept separate from the
/// repository so it's trivially unit-testable. Always computes the first occurrence strictly
/// after <c>afterUtc</c>; the trigger calls it with "now" (not the schedule's own
/// <c>NextRunAtUtc</c>) each time it fires, so a server that was down for a while doesn't
/// fire a pile-up of backdated runs on restart — it just resumes from here on.
/// </summary>
public static class ScheduleRecurrence
{
    /// <param name="frequency">"Daily" | "Weekly" | "Monthly".</param>
    /// <param name="minuteOfDayUtc">0–1439.</param>
    /// <param name="dayOfWeek">0 (Sunday) – 6 (Saturday). Required for "Weekly".</param>
    /// <param name="dayOfMonth">1–31, clamped to the shorter month (e.g. 31 in February → the 28th/29th). Required for "Monthly".</param>
    public static DateTime NextRun(DateTime afterUtc, string frequency, int minuteOfDayUtc, int? dayOfWeek, int? dayOfMonth)
    {
        var time = TimeSpan.FromMinutes(minuteOfDayUtc);
        return frequency switch
        {
            "Weekly" => NextWeekly(afterUtc, time, (DayOfWeek)(dayOfWeek ?? 0)),
            "Monthly" => NextMonthly(afterUtc, time, dayOfMonth ?? 1),
            _ => NextDaily(afterUtc, time),
        };
    }

    private static DateTime NextDaily(DateTime afterUtc, TimeSpan time)
    {
        var candidate = afterUtc.Date + time;
        return candidate > afterUtc ? candidate : candidate.AddDays(1);
    }

    private static DateTime NextWeekly(DateTime afterUtc, TimeSpan time, DayOfWeek targetDay)
    {
        var candidate = afterUtc.Date + time;
        var daysUntilTarget = ((int)targetDay - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysUntilTarget);
        return candidate > afterUtc ? candidate : candidate.AddDays(7);
    }

    private static DateTime NextMonthly(DateTime afterUtc, TimeSpan time, int dayOfMonth)
    {
        var candidate = ClampedMonthDate(afterUtc.Year, afterUtc.Month, dayOfMonth) + time;
        if (candidate > afterUtc)
        {
            return candidate;
        }

        var nextMonth = new DateTime(afterUtc.Year, afterUtc.Month, 1).AddMonths(1);
        return ClampedMonthDate(nextMonth.Year, nextMonth.Month, dayOfMonth) + time;
    }

    private static DateTime ClampedMonthDate(int year, int month, int day) =>
        new(year, month, Math.Clamp(day, 1, DateTime.DaysInMonth(year, month)));
}
