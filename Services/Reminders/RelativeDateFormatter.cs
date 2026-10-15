using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>
/// Data de lembrete do jeito que se fala: "Hoje, 17:00", "Amanhã", "Sexta, 19:00",
/// "20 out, 10:00". Idioma explícito (não <see cref="Strings.Current"/>) pra dar pra
/// testar os dois sem estado global.
/// </summary>
internal static class RelativeDateFormatter
{
    private static readonly string[] MonthsPt = { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };
    private static readonly string[] MonthsEn = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    private static readonly string[] DaysPt = { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };
    private static readonly string[] DaysEn = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    public static string Format(DateOnly date, TimeOnly? time, DateOnly today, AppLanguage lang)
    {
        bool pt = lang == AppLanguage.PtBr;
        int days = date.DayNumber - today.DayNumber;

        string day = days switch
        {
            0 => pt ? "Hoje" : "Today",
            1 => pt ? "Amanhã" : "Tomorrow",
            -1 => pt ? "Ontem" : "Yesterday",
            > 1 and < 7 => (pt ? DaysPt : DaysEn)[(int)date.DayOfWeek],
            _ => FormatDate(date, includeYear: date.Year != today.Year, pt)
        };

        return time is { } t ? $"{day}, {t:HH\\:mm}" : day;
    }

    /// <summary>"20 out" / "Oct 20", com o ano só quando não é o corrente.</summary>
    private static string FormatDate(DateOnly date, bool includeYear, bool pt)
    {
        string text = pt ? $"{date.Day} {MonthsPt[date.Month - 1]}" : $"{MonthsEn[date.Month - 1]} {date.Day}";
        return includeYear ? $"{text} {date.Year}" : text;
    }
}
