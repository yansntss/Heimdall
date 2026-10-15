using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class ReminderScheduleTests
{
    private static readonly TimeZoneInfo Sp = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    private static readonly TimeOnly NineAm = new(9, 0);

    private static DateTimeOffset At(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, TimeSpan.FromHours(-3));

    private static ReminderConfig Daily(DateOnly due, TimeOnly time) => new()
    {
        Title = "Remédio",
        DueDate = due,
        DueTime = time,
        Recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily, Anchor = due }
    };

    [Fact]
    public void All_day_reminder_fires_at_the_default_time()
    {
        var r = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15) };
        Assert.Equal(At(2026, 10, 15, 9, 0), ReminderSchedule.FireAt(r, NineAm, Sp));
    }

    [Fact]
    public void Undated_and_completed_never_fire()
    {
        Assert.Null(ReminderSchedule.FireAt(new ReminderConfig(), NineAm, Sp));
        var done = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), CompletedAt = DateTime.Now };
        Assert.Null(ReminderSchedule.FireAt(done, NineAm, Sp));
    }

    [Fact]
    public void IsDue_once_per_occurrence()
    {
        var r = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(17, 0) };
        Assert.False(ReminderSchedule.IsDue(r, At(2026, 10, 15, 16, 59), NineAm, Sp));
        Assert.True(ReminderSchedule.IsDue(r, At(2026, 10, 15, 17, 0), NineAm, Sp));

        ReminderSchedule.MarkNotified(r, At(2026, 10, 15, 17, 0));
        Assert.False(ReminderSchedule.IsDue(r, At(2026, 10, 15, 17, 5), NineAm, Sp));
    }

    [Fact]
    public void Snooze_fires_again_after_the_delay()
    {
        var r = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(17, 0) };
        ReminderSchedule.MarkNotified(r, At(2026, 10, 15, 17, 0));
        ReminderSchedule.Snooze(r, At(2026, 10, 15, 17, 1), 10);

        Assert.False(ReminderSchedule.IsDue(r, At(2026, 10, 15, 17, 10), NineAm, Sp));
        Assert.True(ReminderSchedule.IsDue(r, At(2026, 10, 15, 17, 11), NineAm, Sp));
    }

    [Fact]
    public void Completing_a_one_off_finishes_it()
    {
        var r = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15) };
        ReminderSchedule.Complete(r, At(2026, 10, 15, 10, 0));
        Assert.True(r.IsCompleted);
    }

    [Fact]
    public void Completing_a_recurring_advances_to_the_next_occurrence()
    {
        var r = Daily(new DateOnly(2026, 10, 15), NineAm);
        ReminderSchedule.Complete(r, At(2026, 10, 15, 8, 0));
        Assert.False(r.IsCompleted);
        Assert.Equal(new DateOnly(2026, 10, 16), r.DueDate);
    }

    [Fact]
    public void Completing_the_last_occurrence_finishes_it()
    {
        var r = Daily(new DateOnly(2026, 10, 15), NineAm);
        r.Recurrence!.Until = new DateOnly(2026, 10, 15);
        ReminderSchedule.Complete(r, At(2026, 10, 15, 10, 0));
        Assert.True(r.IsCompleted);
    }

    [Fact]
    public void CatchUp_jumps_to_the_latest_missed_occurrence()
    {
        var r = Daily(new DateOnly(2026, 10, 10), NineAm);
        Assert.True(ReminderSchedule.CatchUp(r, At(2026, 10, 15, 10, 0), NineAm, Sp));
        Assert.Equal(new DateOnly(2026, 10, 15), r.DueDate);
    }

    [Fact]
    public void CatchUp_keeps_today_until_its_time()
    {
        var r = Daily(new DateOnly(2026, 10, 14), NineAm);
        Assert.False(ReminderSchedule.CatchUp(r, At(2026, 10, 15, 8, 59), NineAm, Sp));
        Assert.Equal(new DateOnly(2026, 10, 14), r.DueDate);
    }

    [Fact]
    public void Monthly_catch_up_keeps_the_31()
    {
        var r = new ReminderConfig
        {
            DueDate = new DateOnly(2026, 1, 31),
            DueTime = NineAm,
            Recurrence = new ReminderRecurrence { Kind = RepeatKind.Monthly, Anchor = new DateOnly(2026, 1, 31) }
        };
        ReminderSchedule.CatchUp(r, At(2026, 3, 31, 10, 0), NineAm, Sp);
        Assert.Equal(new DateOnly(2026, 3, 31), r.DueDate);
    }

    [Fact]
    public void NextFire_is_the_earliest_not_yet_notified()
    {
        var notified = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(8, 0), NotifiedAt = At(2026, 10, 15, 8, 0) };
        var later = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(18, 0) };
        var sooner = new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(12, 0) };

        Assert.Equal(At(2026, 10, 15, 12, 0), ReminderSchedule.NextFire(new[] { notified, later, sooner }, NineAm, Sp));
    }

    [Fact]
    public void Overdue_depends_on_having_a_time()
    {
        var now = At(2026, 10, 15, 12, 0);
        Assert.True(ReminderSchedule.IsOverdue(new ReminderConfig { DueDate = new DateOnly(2026, 10, 15), DueTime = new TimeOnly(11, 0) }, now, Sp));
        Assert.False(ReminderSchedule.IsOverdue(new ReminderConfig { DueDate = new DateOnly(2026, 10, 15) }, now, Sp));
        Assert.True(ReminderSchedule.IsOverdue(new ReminderConfig { DueDate = new DateOnly(2026, 10, 14) }, now, Sp));
    }
}
