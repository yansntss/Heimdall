using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class RecurrenceCalculatorTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static ReminderRecurrence Rule(RepeatKind kind, params DayOfWeek[] days) => new() { Kind = kind, Days = days.ToList() };

    [Fact]
    public void Daily_is_the_next_day()
    {
        Assert.Equal(D(2026, 11, 1), RecurrenceCalculator.NextDate(Rule(RepeatKind.Daily), D(2026, 1, 1), D(2026, 10, 31)));
    }

    [Fact]
    public void Daily_crosses_the_year()
    {
        Assert.Equal(D(2027, 1, 1), RecurrenceCalculator.NextDate(Rule(RepeatKind.Daily), D(2026, 1, 1), D(2026, 12, 31)));
    }

    [Fact]
    public void Weekly_jumps_to_the_next_chosen_weekday()
    {
        var rule = Rule(RepeatKind.Weekly, DayOfWeek.Monday, DayOfWeek.Wednesday);
        // 2026-10-15 é quinta: próxima é segunda 19, depois quarta 21.
        Assert.Equal(D(2026, 10, 19), RecurrenceCalculator.NextDate(rule, D(2026, 10, 1), D(2026, 10, 15)));
        Assert.Equal(D(2026, 10, 21), RecurrenceCalculator.NextDate(rule, D(2026, 10, 1), D(2026, 10, 19)));
    }

    [Fact]
    public void Weekly_without_days_repeats_the_anchor_weekday()
    {
        // Âncora numa sexta (16/10/2026).
        Assert.Equal(D(2026, 10, 23), RecurrenceCalculator.NextDate(Rule(RepeatKind.Weekly), D(2026, 10, 16), D(2026, 10, 16)));
    }

    [Fact]
    public void Weekly_crosses_the_month()
    {
        var rule = Rule(RepeatKind.Weekly, DayOfWeek.Monday);
        Assert.Equal(D(2026, 11, 2), RecurrenceCalculator.NextDate(rule, D(2026, 10, 5), D(2026, 10, 26)));
    }

    [Fact]
    public void Monthly_on_31_falls_back_to_the_last_day_and_recovers()
    {
        var rule = Rule(RepeatKind.Monthly);
        var anchor = D(2026, 1, 31);
        var feb = RecurrenceCalculator.NextDate(rule, anchor, anchor);
        Assert.Equal(D(2026, 2, 28), feb);
        var mar = RecurrenceCalculator.NextDate(rule, anchor, feb!.Value);
        Assert.Equal(D(2026, 3, 31), mar);
        Assert.Equal(D(2026, 4, 30), RecurrenceCalculator.NextDate(rule, anchor, mar!.Value));
    }

    [Fact]
    public void Monthly_on_29_hits_february_29_in_leap_years()
    {
        Assert.Equal(D(2028, 2, 29), RecurrenceCalculator.NextDate(Rule(RepeatKind.Monthly), D(2027, 12, 29), D(2028, 1, 29)));
    }

    [Fact]
    public void Monthly_crosses_the_year()
    {
        Assert.Equal(D(2027, 1, 10), RecurrenceCalculator.NextDate(Rule(RepeatKind.Monthly), D(2026, 3, 10), D(2026, 12, 10)));
    }

    [Fact]
    public void Monthly_later_in_the_same_month()
    {
        Assert.Equal(D(2026, 10, 20), RecurrenceCalculator.NextDate(Rule(RepeatKind.Monthly), D(2026, 1, 20), D(2026, 10, 15)));
    }

    [Fact]
    public void Yearly_on_february_29_uses_28_outside_leap_years()
    {
        var rule = Rule(RepeatKind.Yearly);
        var anchor = D(2028, 2, 29);
        Assert.Equal(D(2029, 2, 28), RecurrenceCalculator.NextDate(rule, anchor, anchor));
        Assert.Equal(D(2032, 2, 29), RecurrenceCalculator.NextDate(rule, anchor, D(2031, 2, 28)));
    }

    [Fact]
    public void Until_stops_the_repetition()
    {
        var rule = new ReminderRecurrence { Kind = RepeatKind.Daily, Until = D(2026, 10, 20) };
        Assert.Equal(D(2026, 10, 20), RecurrenceCalculator.NextDate(rule, D(2026, 10, 1), D(2026, 10, 19)));
        Assert.Null(RecurrenceCalculator.NextDate(rule, D(2026, 10, 1), D(2026, 10, 20)));
    }

    [Fact]
    public void None_never_repeats()
    {
        Assert.Null(RecurrenceCalculator.NextDate(Rule(RepeatKind.None), D(2026, 10, 1), D(2026, 10, 1)));
    }

    [Fact]
    public void Every_two_days_keeps_the_phase_of_the_anchor()
    {
        var rule = new ReminderRecurrence { Kind = RepeatKind.Daily, Interval = 2 };
        var anchor = D(2026, 10, 15);
        Assert.Equal(D(2026, 10, 17), RecurrenceCalculator.NextDate(rule, anchor, anchor));
        Assert.Equal(D(2026, 10, 19), RecurrenceCalculator.NextDate(rule, anchor, D(2026, 10, 17)));
        // Concluído num dia fora de fase: vai pro próximo da série, não pro dia seguinte.
        Assert.Equal(D(2026, 10, 19), RecurrenceCalculator.NextDate(rule, anchor, D(2026, 10, 18)));
    }

    [Fact]
    public void Every_two_weeks_on_monday_and_wednesday()
    {
        var rule = Rule(RepeatKind.Weekly, DayOfWeek.Monday, DayOfWeek.Wednesday);
        rule.Interval = 2;
        var anchor = D(2026, 10, 19); // segunda
        Assert.Equal(D(2026, 10, 21), RecurrenceCalculator.NextDate(rule, anchor, anchor));
        Assert.Equal(D(2026, 11, 2), RecurrenceCalculator.NextDate(rule, anchor, D(2026, 10, 21)));
        Assert.Equal(D(2026, 11, 4), RecurrenceCalculator.NextDate(rule, anchor, D(2026, 11, 2)));
    }

    [Fact]
    public void Weekdays_skip_the_weekend()
    {
        var rule = Rule(RepeatKind.Weekly, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        Assert.Equal(D(2026, 10, 19), RecurrenceCalculator.NextDate(rule, D(2026, 10, 1), D(2026, 10, 16)));
    }

    [Fact]
    public void Every_three_months_keeps_the_31()
    {
        var rule = new ReminderRecurrence { Kind = RepeatKind.Monthly, Interval = 3 };
        var anchor = D(2026, 1, 31);
        var apr = RecurrenceCalculator.NextDate(rule, anchor, anchor);
        Assert.Equal(D(2026, 4, 30), apr);
        Assert.Equal(D(2026, 7, 31), RecurrenceCalculator.NextDate(rule, anchor, apr!.Value));
    }

    [Fact]
    public void Every_two_years()
    {
        var rule = new ReminderRecurrence { Kind = RepeatKind.Yearly, Interval = 2 };
        Assert.Equal(D(2028, 3, 1), RecurrenceCalculator.NextDate(rule, D(2026, 3, 1), D(2026, 3, 1)));
    }

    [Fact]
    public void After_n_times_counts_the_anchor()
    {
        var rule = new ReminderRecurrence { Kind = RepeatKind.Daily, Count = 3 };
        var anchor = D(2026, 10, 15);
        Assert.Equal(D(2026, 10, 16), RecurrenceCalculator.NextDate(rule, anchor, anchor));
        Assert.Equal(D(2026, 10, 17), RecurrenceCalculator.NextDate(rule, anchor, D(2026, 10, 16)));
        Assert.Null(RecurrenceCalculator.NextDate(rule, anchor, D(2026, 10, 17)));
    }

    [Fact]
    public void After_n_times_with_weekdays()
    {
        var rule = Rule(RepeatKind.Weekly, DayOfWeek.Monday, DayOfWeek.Friday);
        rule.Count = 2;
        var anchor = D(2026, 10, 16); // sexta
        Assert.Equal(D(2026, 10, 19), RecurrenceCalculator.NextDate(rule, anchor, anchor));
        Assert.Null(RecurrenceCalculator.NextDate(rule, anchor, D(2026, 10, 19)));
    }

    [Fact]
    public void FirstOnOrAfter_respects_the_phase()
    {
        var rule = new ReminderRecurrence { Kind = RepeatKind.Daily, Interval = 3 };
        Assert.Equal(D(2026, 10, 18), RecurrenceCalculator.FirstOnOrAfter(rule, D(2026, 10, 15), D(2026, 10, 16)));
    }

    [Fact]
    public void FirstOnOrAfter_includes_the_day_itself_when_it_matches()
    {
        var rule = Rule(RepeatKind.Weekly, DayOfWeek.Thursday);
        Assert.Equal(D(2026, 10, 15), RecurrenceCalculator.FirstOnOrAfter(rule, D(2026, 10, 1), D(2026, 10, 15)));
        Assert.Equal(D(2026, 10, 22), RecurrenceCalculator.FirstOnOrAfter(rule, D(2026, 10, 1), D(2026, 10, 16)));
    }
}

/// <summary>Horário de verão com fusos que ainda têm (o Brasil não tem desde 2019).</summary>
public class DaylightSavingTests
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void Time_skipped_by_spring_forward_moves_ahead_by_the_gap()
    {
        // 08/03/2026, EUA: 02:00 vira 03:00 — 02:30 não existe.
        var fire = RecurrenceCalculator.Resolve(new DateOnly(2026, 3, 8), new TimeOnly(2, 30), Eastern);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-4)), fire);
    }

    [Fact]
    public void Time_repeated_by_fall_back_uses_the_first_one()
    {
        // 01/11/2026, EUA: 02:00 volta pra 01:00 — 01:30 acontece duas vezes.
        var fire = RecurrenceCalculator.Resolve(new DateOnly(2026, 11, 1), new TimeOnly(1, 30), Eastern);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4)), fire);
    }

    [Fact]
    public void Europe_spring_forward()
    {
        // 29/03/2026, Europa central: 02:00 vira 03:00.
        var fire = RecurrenceCalculator.Resolve(new DateOnly(2026, 3, 29), new TimeOnly(2, 30), Berlin);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 3, 30, 0, TimeSpan.FromHours(2)), fire);
    }

    [Fact]
    public void Daily_reminder_keeps_the_wall_clock_time_across_the_change()
    {
        var before = RecurrenceCalculator.Resolve(new DateOnly(2026, 3, 7), new TimeOnly(9, 0), Eastern);
        var after = RecurrenceCalculator.Resolve(new DateOnly(2026, 3, 9), new TimeOnly(9, 0), Eastern);
        Assert.Equal(14, before.UtcDateTime.Hour);
        Assert.Equal(13, after.UtcDateTime.Hour);
        Assert.Equal(9, after.Hour);
    }

    [Fact]
    public void CatchUp_across_spring_forward_lands_on_the_valid_occurrence()
    {
        var r = new ReminderConfig
        {
            DueDate = new DateOnly(2026, 3, 7),
            DueTime = new TimeOnly(2, 30),
            Recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily, Anchor = new DateOnly(2026, 3, 7) }
        };
        // Agora: 08/03 às 03:45 locais (07:45Z) — a das 02:30 desse dia virou 03:30, já passou.
        var now = new DateTimeOffset(2026, 3, 8, 7, 45, 0, TimeSpan.Zero);
        Assert.True(ReminderSchedule.CatchUp(r, now, new TimeOnly(9, 0), Eastern));
        Assert.Equal(new DateOnly(2026, 3, 8), r.DueDate);
    }
}
