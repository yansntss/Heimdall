using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class RecurrenceSummaryTests
{
    // Quinta.
    private static readonly DateOnly Anchor = new(2026, 10, 15);
    private static readonly TimeOnly FivePm = new(17, 0);

    private static string Pt(ReminderRecurrence? rule, TimeOnly? time = null) =>
        RecurrenceSummary.Describe(rule, Anchor, time, AppLanguage.PtBr);

    [Fact]
    public void Weekly_with_days_and_time()
    {
        var rule = new ReminderRecurrence
        {
            Kind = RepeatKind.Weekly,
            Days = new() { DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Wednesday }
        };
        Assert.Equal("Toda semana, seg · qua · sex, às 17:00", Pt(rule, FivePm));
    }

    [Fact]
    public void Weekly_without_days_uses_the_anchor_day()
    {
        Assert.Equal("Toda semana, qui", Pt(new ReminderRecurrence { Kind = RepeatKind.Weekly }));
    }

    [Fact]
    public void Intervals()
    {
        Assert.Equal("A cada 3 dias", Pt(new ReminderRecurrence { Kind = RepeatKind.Daily, Interval = 3 }));
        Assert.Equal("A cada 2 semanas, dom · sáb",
            Pt(new ReminderRecurrence { Kind = RepeatKind.Weekly, Interval = 2, Days = new() { DayOfWeek.Saturday, DayOfWeek.Sunday } }));
        Assert.Equal("Todo mês, no dia 15", Pt(new ReminderRecurrence { Kind = RepeatKind.Monthly }));
        Assert.Equal("A cada 2 anos, em 15 de out", Pt(new ReminderRecurrence { Kind = RepeatKind.Yearly, Interval = 2 }));
    }

    [Fact]
    public void Ends()
    {
        Assert.Equal("Todo dia, às 17:00, até 20 de dez",
            Pt(new ReminderRecurrence { Kind = RepeatKind.Daily, Until = new DateOnly(2026, 12, 20) }, FivePm));
        Assert.Equal("Todo dia, 5 vezes", Pt(new ReminderRecurrence { Kind = RepeatKind.Daily, Count = 5 }));
    }

    [Fact]
    public void No_repeat_and_english()
    {
        Assert.Equal("Não repete", Pt(null));
        Assert.Equal("Every 2 weeks, Mon · Wed, at 17:00",
            RecurrenceSummary.Describe(new ReminderRecurrence { Kind = RepeatKind.Weekly, Interval = 2, Days = new() { DayOfWeek.Monday, DayOfWeek.Wednesday } },
                Anchor, FivePm, AppLanguage.EnUs));
    }
}
