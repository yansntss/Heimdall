using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>Datas de ocorrência de uma regra de repetição e conversão de data+hora local num instante.</summary>
internal static class RecurrenceCalculator
{
    /// <summary>
    /// Próxima ocorrência estritamente depois de <paramref name="after"/>; nulo se não repete,
    /// passou do fim (<see cref="ReminderRecurrence.Until"/>) ou já deu as N vezes
    /// (<see cref="ReminderRecurrence.Count"/>).
    /// </summary>
    public static DateOnly? NextDate(ReminderRecurrence rule, DateOnly anchor, DateOnly after) =>
        WithinLimits(rule, anchor, RawNext(rule, anchor, after));

    /// <summary>Primeira ocorrência em <paramref name="from"/> ou depois (inclusive).</summary>
    public static DateOnly? FirstOnOrAfter(ReminderRecurrence rule, DateOnly anchor, DateOnly from) =>
        Matches(rule, anchor, from) ? WithinLimits(rule, anchor, from) : NextDate(rule, anchor, from);

    /// <summary>Próxima data que bate com a regra (dia certo e na fase do "a cada N"), sem olhar os limites.</summary>
    private static DateOnly? RawNext(ReminderRecurrence rule, DateOnly anchor, DateOnly after)
    {
        if (rule.Kind == RepeatKind.None) return null;

        // Antes da âncora não tem fase definida: a série começa nela.
        if (after < anchor.AddDays(-1)) after = anchor.AddDays(-1);

        var candidate = after;
        // A cada passo cai num dia válido da regra; com intervalo > 1, pula os fora de fase.
        // 5000 passos cobre "a cada 99 anos" com folga — só pra nunca travar com regra estranha.
        for (int i = 0; i < 5000; i++)
        {
            candidate = SimpleNext(rule, anchor, candidate);
            if (InPhase(rule, anchor, candidate)) return candidate;
        }
        return null;
    }

    private static DateOnly SimpleNext(ReminderRecurrence rule, DateOnly anchor, DateOnly after) => rule.Kind switch
    {
        RepeatKind.Daily => after.AddDays(1),
        RepeatKind.Weekly => NextWeekly(WeekDays(rule, anchor), after),
        RepeatKind.Monthly => NextMonthly(anchor.Day, after),
        _ => NextYearly(anchor.Month, anchor.Day, after)
    };

    /// <summary>"A cada N": a distância da âncora (em dias, semanas, meses ou anos) é múltipla de N.</summary>
    private static bool InPhase(ReminderRecurrence rule, DateOnly anchor, DateOnly date)
    {
        int every = Math.Max(1, rule.Interval);
        if (every == 1) return date >= anchor;
        if (date < anchor) return false;

        int distance = rule.Kind switch
        {
            RepeatKind.Daily => date.DayNumber - anchor.DayNumber,
            RepeatKind.Weekly => WeekIndex(date) - WeekIndex(anchor),
            RepeatKind.Monthly => (date.Year - anchor.Year) * 12 + date.Month - anchor.Month,
            _ => date.Year - anchor.Year
        };
        return distance % every == 0;
    }

    /// <summary>Semanas contadas de segunda a domingo — "a cada 2 semanas, seg e qua" fica na mesma semana.</summary>
    private static int WeekIndex(DateOnly date) => (date.DayNumber - ((int)date.DayOfWeek + 6) % 7) / 7;

    /// <summary>Corta pelo fim (data) e pela contagem ("após N vezes", contando a âncora como a 1ª).</summary>
    private static DateOnly? WithinLimits(ReminderRecurrence rule, DateOnly anchor, DateOnly? date)
    {
        if (date is not { } d) return null;
        if (rule.Until is { } until && d > until) return null;
        if (rule.Count is { } count)
        {
            if (count < 1) return null;
            // Conta as ocorrências da âncora até d; passou de N, acabou.
            int index = 0;
            DateOnly? current = Matches(rule, anchor, anchor) ? anchor : RawNext(rule, anchor, anchor);
            while (current is { } c && c < d && index < count)
            {
                index++;
                current = RawNext(rule, anchor, c);
            }
            if (index >= count) return null;
        }
        return d;
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
    } && InPhase(rule, anchor, day);

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
