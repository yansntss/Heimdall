using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>Datas de ocorrência de uma regra de repetição e conversão de data+hora local num instante.</summary>
internal static class RecurrenceCalculator
{
    /// <summary>Próxima ocorrência estritamente depois de <paramref name="after"/>; nulo se não repete ou já passou do fim.</summary>
    public static DateOnly? NextDate(ReminderRecurrence rule, DateOnly anchor, DateOnly after)
    {
        DateOnly? next = rule.Kind switch
        {
            RepeatKind.Daily => after.AddDays(1),
            RepeatKind.Weekly => NextWeekly(WeekDays(rule, anchor), after),
            RepeatKind.Monthly => NextMonthly(anchor.Day, after),
            RepeatKind.Yearly => NextYearly(anchor.Month, anchor.Day, after),
            _ => null
        };
        return next is { } n && rule.Until is { } until && n > until ? null : next;
    }

    /// <summary>Primeira ocorrência em <paramref name="from"/> ou depois (inclusive).</summary>
    public static DateOnly? FirstOnOrAfter(ReminderRecurrence rule, DateOnly anchor, DateOnly from)
    {
        if (rule.Until is { } until && from > until) return null;
        return Matches(rule, anchor, from) ? from : NextDate(rule, anchor, from);
    }

    /// <summary>
    /// Instante de data+hora no fuso. Hora que não existe (adiantou o relógio no horário de
    /// verão: 02:30 num dia em que 02:00 vira 03:00) avança o tamanho do salto → 03:30.
    /// Hora que existe duas vezes (atrasou o relógio) fica com a primeira.
    /// </summary>
    public static DateTimeOffset Resolve(DateOnly date, TimeOnly time, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (tz.IsInvalidTime(local))
        {
            // Transições ficam meses longe uma da outra: 3h antes e depois pegam os dois lados do salto.
            var jump = tz.GetUtcOffset(local.AddHours(3)) - tz.GetUtcOffset(local.AddHours(-3));
            local = local.Add(jump);
        }

        if (tz.IsAmbiguousTime(local))
            return new DateTimeOffset(local, tz.GetAmbiguousTimeOffsets(local).Max());

        return new DateTimeOffset(local, tz.GetUtcOffset(local));
    }

    private static bool Matches(ReminderRecurrence rule, DateOnly anchor, DateOnly day) => rule.Kind switch
    {
        RepeatKind.Daily => true,
        RepeatKind.Weekly => WeekDays(rule, anchor).Contains(day.DayOfWeek),
        RepeatKind.Monthly => day.Day == ClampDay(day.Year, day.Month, anchor.Day),
        RepeatKind.Yearly => day.Month == anchor.Month && day.Day == ClampDay(day.Year, day.Month, anchor.Day),
        _ => false
    };

    private static IReadOnlyCollection<DayOfWeek> WeekDays(ReminderRecurrence rule, DateOnly anchor) =>
        rule.Days.Count > 0 ? rule.Days : new[] { anchor.DayOfWeek };

    private static DateOnly NextWeekly(IReadOnlyCollection<DayOfWeek> days, DateOnly after)
    {
        for (int i = 1; i <= 7; i++)
        {
            var day = after.AddDays(i);
            if (days.Contains(day.DayOfWeek)) return day;
        }
        return after.AddDays(7); // inalcançável: days nunca é vazio
    }

    /// <summary>Dia 31 vira o último dia dos meses mais curtos, sem perder o 31 nos seguintes.</summary>
    private static DateOnly NextMonthly(int dayOfMonth, DateOnly after)
    {
        var sameMonth = new DateOnly(after.Year, after.Month, ClampDay(after.Year, after.Month, dayOfMonth));
        if (sameMonth > after) return sameMonth;

        var next = new DateOnly(after.Year, after.Month, 1).AddMonths(1);
        return new DateOnly(next.Year, next.Month, ClampDay(next.Year, next.Month, dayOfMonth));
    }

    /// <summary>29/02 cai em 28/02 nos anos não bissextos.</summary>
    private static DateOnly NextYearly(int month, int dayOfMonth, DateOnly after)
    {
        var thisYear = new DateOnly(after.Year, month, ClampDay(after.Year, month, dayOfMonth));
        if (thisYear > after) return thisYear;
        return new DateOnly(after.Year + 1, month, ClampDay(after.Year + 1, month, dayOfMonth));
    }

    private static int ClampDay(int year, int month, int day) => Math.Min(day, DateTime.DaysInMonth(year, month));
}
