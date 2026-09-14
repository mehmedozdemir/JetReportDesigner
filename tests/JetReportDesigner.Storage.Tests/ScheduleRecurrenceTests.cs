using JetReportDesigner.Storage.Schedules;

namespace JetReportDesigner.Storage.Tests;

public sealed class ScheduleRecurrenceTests
{
    private static readonly int NineAm = 9 * 60;

    [Fact]
    public void Daily_BeforeTimeToday_RunsToday()
    {
        var after = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc); // Tue
        var next = ScheduleRecurrence.NextRun(after, "Daily", NineAm, null, null);
        Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Daily_AfterTimeToday_RunsTomorrow()
    {
        var after = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(after, "Daily", NineAm, null, null);
        Assert.Equal(new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Daily_ExactlyAtTime_RunsTomorrow()
    {
        // "strictly after" — the instant it fires shouldn't re-fire for the same moment.
        var after = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(after, "Daily", NineAm, null, null);
        Assert.Equal(new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Weekly_TargetDayLaterThisWeek_RunsThatDay()
    {
        var monday = new DateTime(2026, 3, 9, 8, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(monday, "Weekly", NineAm, (int)DayOfWeek.Friday, null);
        Assert.Equal(new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc), next); // that Friday
    }

    [Fact]
    public void Weekly_TargetDayIsTodayButTimePassed_RunsNextWeek()
    {
        var friday = new DateTime(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(friday, "Weekly", NineAm, (int)DayOfWeek.Friday, null);
        Assert.Equal(new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Weekly_TargetDayIsTodayBeforeTime_RunsToday()
    {
        var friday = new DateTime(2026, 3, 13, 8, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(friday, "Weekly", NineAm, (int)DayOfWeek.Friday, null);
        Assert.Equal(new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Weekly_TargetDayAlreadyPassedThisWeek_WrapsToNextWeek()
    {
        var thursday = new DateTime(2026, 3, 12, 8, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(thursday, "Weekly", NineAm, (int)DayOfWeek.Monday, null);
        Assert.Equal(new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc), next); // following Monday
    }

    [Fact]
    public void Monthly_DayLaterThisMonth_RunsThatDay()
    {
        var after = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(after, "Monthly", NineAm, null, 15);
        Assert.Equal(new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Monthly_DayAlreadyPassed_RunsNextMonth()
    {
        var after = new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(after, "Monthly", NineAm, null, 15);
        Assert.Equal(new DateTime(2026, 4, 15, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Monthly_Day31InFebruary_ClampsToLastDay()
    {
        var after = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc); // 2026 is not a leap year
        var next = ScheduleRecurrence.NextRun(after, "Monthly", NineAm, null, 31);
        Assert.Equal(new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Monthly_Day31InLeapFebruary_ClampsToThe29th()
    {
        var after = new DateTime(2028, 2, 1, 0, 0, 0, DateTimeKind.Utc); // 2028 is a leap year
        var next = ScheduleRecurrence.NextRun(after, "Monthly", NineAm, null, 31);
        Assert.Equal(new DateTime(2028, 2, 29, 9, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Monthly_RollsFromClampedFebruaryToUnclampedMarch()
    {
        // After firing (clamped) on Feb 28, the *next* occurrence should go back to day 31 for
        // March, which actually has one — not stay clamped forever.
        var after = new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc);
        var next = ScheduleRecurrence.NextRun(after, "Monthly", NineAm, null, 31);
        Assert.Equal(new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc), next);
    }
}
