using System.Globalization;
using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>
/// A repetição em uma frase — "Toda semana, seg · qua · sex, às 17:00" — pro resumo do
/// "Personalizado", que recalcula a cada mudança e evita configurar errado sem perceber.
/// </summary>
internal static class RecurrenceSummary
{
    private static readonly string[] DaysPt = { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };
    private static readonly string[] DaysEn = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public static string Describe(ReminderRecurrence? rule, DateOnly anchor, TimeOnly? time, AppLanguage lang)
    {
        bool pt = lang == AppLanguage.PtBr;
        if (rule is null || rule.Kind == RepeatKind.None) return pt ? "Não repete" : "Doesn't repeat";

        int every = Math.Max(1, rule.Interval);
        var parts = new List<string> { Every(rule.Kind, every, pt) };

        switch (rule.Kind)
        {
            case RepeatKind.Weekly:
                var days = rule.Days.Count > 0 ? rule.Days : new List<DayOfWeek> { anchor.DayOfWeek };
                // Ordem da semana começando no domingo, como nos círculos.
                parts.Add(string.Join(" · ", days.Distinct().OrderBy(d => (int)d).Select(d => (pt ? DaysPt : DaysEn)[(int)d])));
                break;
            case RepeatKind.Monthly:
                parts.Add(pt ? $"no dia {anchor.Day}" : $"on day {anchor.Day}");
                break;
            case RepeatKind.Yearly:
                parts.Add(pt ? $"em {ShortDate(anchor, pt)}" : $"on {ShortDate(anchor, pt)}");
                break;
        }

        if (time is { } t) parts.Add(pt ? $"às {t:HH\\:mm}" : $"at {t:HH\\:mm}");

        if (rule.Until is { } until) parts.Add(pt ? $"até {ShortDate(until, pt)}" : $"until {ShortDate(until, pt)}");
        else if (rule.Count is { } count) parts.Add(count == 1 ? (pt ? "1 vez" : "once") : (pt ? $"{count} vezes" : $"{count} times"));

        return string.Join(", ", parts);
    }

    /// <summary>"Toda semana" / "A cada 2 semanas".</summary>
    private static string Every(RepeatKind kind, int every, bool pt) => (kind, every == 1, pt) switch
    {
        (RepeatKind.Daily, true, true) => "Todo dia",
        (RepeatKind.Daily, false, true) => $"A cada {every} dias",
        (RepeatKind.Weekly, true, true) => "Toda semana",
        (RepeatKind.Weekly, false, true) => $"A cada {every} semanas",
        (RepeatKind.Monthly, true, true) => "Todo mês",
        (RepeatKind.Monthly, false, true) => $"A cada {every} meses",
        (_, true, true) => "Todo ano",
        (_, false, true) => $"A cada {every} anos",
        (RepeatKind.Daily, true, false) => "Every day",
        (RepeatKind.Daily, false, false) => $"Every {every} days",
        (RepeatKind.Weekly, true, false) => "Every week",
        (RepeatKind.Weekly, false, false) => $"Every {every} weeks",
        (RepeatKind.Monthly, true, false) => "Every month",
        (RepeatKind.Monthly, false, false) => $"Every {every} months",
        (_, true, false) => "Every year",
        _ => $"Every {every} years"
    };

    /// <summary>"20 de dez" / "Dec 20".</summary>
    private static string ShortDate(DateOnly date, bool pt)
    {
        var culture = new CultureInfo(pt ? "pt-BR" : "en-US");
        var month = culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month).TrimEnd('.');
        return pt ? $"{date.Day} de {month}" : $"{month} {date.Day}";
    }
}
