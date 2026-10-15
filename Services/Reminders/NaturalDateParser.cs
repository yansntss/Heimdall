using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>O que o parser achou no título: o texto que sobra e a data/hora/repetição tiradas dele.</summary>
internal sealed record ParsedReminder(string Title, DateOnly? Date, TimeOnly? Time, ReminderRecurrence? Recurrence)
{
    public bool Found => Date is not null || Time is not null || Recurrence is not null;
}

/// <summary>
/// Data e repetição em português no título do lembrete: "amanhã 14h", "sexta 19:00",
/// "todo dia 9h", "toda segunda e quarta 8h", "dia 20 às 10h", "todo dia 10". O trecho
/// reconhecido sai do texto. Conservador de propósito: número solto ("às 3", "2 pães")
/// não vira hora — só "14h", "14:30", "14 horas", "meio-dia".
/// </summary>
internal static class NaturalDateParser
{
    private const string WeekDay = "(segunda|terca|quarta|quinta|sexta|sabado|domingo)";
    private const string WeekDayWord = WeekDay + "s?(?:-feiras?)?";

    private static readonly Dictionary<string, DayOfWeek> WeekDays = new()
    {
        ["segunda"] = DayOfWeek.Monday, ["terca"] = DayOfWeek.Tuesday, ["quarta"] = DayOfWeek.Wednesday,
        ["quinta"] = DayOfWeek.Thursday, ["sexta"] = DayOfWeek.Friday, ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday
    };

    private const RegexOptions Opts = RegexOptions.CultureInvariant;

    // Repetição
    private static readonly Regex MonthlyOnDay = new(@"\b(?:todo|todos os) dias? (\d{1,2})\b(?!\s*(?:h|:|horas?))", Opts);
    private static readonly Regex Daily = new(@"\b(?:todo dia|todos os dias|diariamente)\b", Opts);
    private static readonly Regex Weekly = new(
        @"\b(?:todas as|todos os|toda|todo)\s+" + WeekDayWord + @"(?:(?:\s*,\s*|\s+e\s+)(?:as\s+|os\s+)?" + WeekDayWord + @")*", Opts);
    private static readonly Regex Monthly = new(@"\b(?:todo mes|todos os meses|mensalmente)\b", Opts);
    private static readonly Regex Yearly = new(@"\b(?:todo ano|todos os anos|anualmente)\b", Opts);
    private static readonly Regex WeekDayInside = new(WeekDay, Opts);

    // Hora
    private static readonly Regex HourH = new(@"\b(?:as\s+|a\s+)?(\d{1,2})h(\d{2})?\b", Opts);
    private static readonly Regex HourColon = new(@"\b(?:as\s+|a\s+)?(\d{1,2}):(\d{2})\b", Opts);
    private static readonly Regex HourWord = new(@"\b(?:as\s+|a\s+)?(\d{1,2})\s+horas?\b", Opts);
    private static readonly Regex Noon = new(@"\b(?:ao\s+)?meio[- ]dia\b", Opts);
    private static readonly Regex Midnight = new(@"\b(?:a\s+)?meia[- ]noite\b", Opts);

    // Data
    private static readonly Regex AfterTomorrow = new(@"\bdepois de amanha\b", Opts);
    private static readonly Regex Tomorrow = new(@"\bamanha\b", Opts);
    private static readonly Regex Today = new(@"\bhoje\b", Opts);
    private static readonly Regex NextWeek = new(@"\b(?:semana que vem|(?:na )?proxima semana)\b", Opts);
    private static readonly Regex DayOfMonth = new(@"\b(?:no )?dia (\d{1,2})(?:/(\d{1,2}))?\b", Opts);
    private static readonly Regex SlashDate = new(@"\b(\d{1,2})/(\d{1,2})(?:/(\d{2}|\d{4}))?\b", Opts);
    private static readonly Regex OnWeekDay = new(@"\b(?:(?:na|no|nesta|neste)\s+)?(?:(?:proxima|proximo)\s+)?" + WeekDay + @"(?:-feira)?\b", Opts);

    private static readonly Regex Connector = new(@"^(?:as|a|no|na|em|de|para|pra|,|-)\s+|\s+(?:as|a|no|na|em|de|para|pra|,|-)$", Opts);

    public static ParsedReminder Parse(string text, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);
        var normalized = Normalize(text);
        var removed = new bool[text.Length];

        Match? Take(Regex regex)
        {
            var match = regex.Match(normalized);
            while (match.Success && Overlaps(match, removed)) match = match.NextMatch();
            if (!match.Success) return null;
            for (int i = match.Index; i < match.Index + match.Length; i++) removed[i] = true;
            return match;
        }

        // ---- Repetição ----
        ReminderRecurrence? recurrence = null;
        int? monthDay = null;
        if (Take(MonthlyOnDay) is { } monthlyOnDay && int.Parse(monthlyOnDay.Groups[1].Value) is >= 1 and <= 31 and var md)
        {
            recurrence = new ReminderRecurrence { Kind = RepeatKind.Monthly };
            monthDay = md;
        }
        else if (Take(Weekly) is { } weekly)
        {
            var days = WeekDayInside.Matches(weekly.Value).Select(m => WeekDays[m.Value]).Distinct().ToList();
            recurrence = new ReminderRecurrence { Kind = RepeatKind.Weekly, Days = days };
        }
        else if (Take(Daily) is not null) recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily };
        else if (Take(Monthly) is not null) recurrence = new ReminderRecurrence { Kind = RepeatKind.Monthly };
        else if (Take(Yearly) is not null) recurrence = new ReminderRecurrence { Kind = RepeatKind.Yearly };

        // ---- Hora ----
        TimeOnly? time = null;
        if (Take(HourColon) is { } colon) time = MakeTime(colon.Groups[1].Value, colon.Groups[2].Value);
        else if (Take(HourH) is { } h) time = MakeTime(h.Groups[1].Value, h.Groups[2].Success ? h.Groups[2].Value : "0");
        else if (Take(HourWord) is { } word) time = MakeTime(word.Groups[1].Value, "0");
        else if (Take(Noon) is not null) time = new TimeOnly(12, 0);
        else if (Take(Midnight) is not null) time = new TimeOnly(0, 0);

        // ---- Data ----
        DateOnly? date = null;
        if (recurrence?.Kind != RepeatKind.Weekly)
        {
            if (Take(AfterTomorrow) is not null) date = today.AddDays(2);
            else if (Take(Tomorrow) is not null) date = today.AddDays(1);
            else if (Take(Today) is not null) date = today;
            else if (Take(NextWeek) is not null) date = NextMonday(today);
            else if (Take(SlashDate) is { } slash) date = MakeDate(slash, today);
            else if (Take(DayOfMonth) is { } dayOfMonth) date = MakeDayOfMonth(dayOfMonth, today, time, nowTime);
            else if (Take(OnWeekDay) is { } weekDay) date = NextWeekDay(WeekDays[weekDay.Groups[1].Value], today, time, nowTime);
        }

        // ---- Juntando ----
        if (recurrence is not null)
        {
            var from = date ?? today;
            var anchor = monthDay is { } d ? FirstWithDay(d, from) : from;
            var first = RecurrenceCalculator.FirstOnOrAfter(recurrence, anchor, from) ?? from;
            if (first == today && time is { } t && t <= nowTime) first = RecurrenceCalculator.NextDate(recurrence, anchor, today) ?? first;
            recurrence.Anchor = anchor;
            date = first;
        }
        else if (date is null && time is { } onlyTime)
        {
            // Só a hora: hoje se ainda não passou, senão amanhã.
            date = onlyTime > nowTime ? today : today.AddDays(1);
        }

        if (date is null && time is null && recurrence is null) return new ParsedReminder(text.Trim(), null, null, null);
        return new ParsedReminder(CleanTitle(text, removed), date, time, recurrence);
    }

    /// <summary>Minúsculas sem acento, caractere a caractere — os índices batem com o texto original.</summary>
    private static string Normalize(string text)
    {
        var chars = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            var decomposed = char.ToLowerInvariant(text[i]).ToString().Normalize(NormalizationForm.FormD);
            chars[i] = decomposed[0];
        }
        return new string(chars);
    }

    private static bool Overlaps(Match match, bool[] removed)
    {
        for (int i = match.Index; i < match.Index + match.Length; i++)
            if (removed[i]) return true;
        return false;
    }

    private static string CleanTitle(string text, bool[] removed)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++) sb.Append(removed[i] ? ' ' : text[i]);
        var title = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();

        // Preposição que ficou pendurada na ponta ("ligar pra mãe às" → "ligar pra mãe").
        // Casa no texto normalizado e corta no original (mesmos índices).
        while (Connector.Match(Normalize(title)) is { Success: true } match)
            title = (title[..match.Index] + title[(match.Index + match.Length)..]).Trim();
        return title;
    }

    private static TimeOnly? MakeTime(string hour, string minute)
    {
        int h = int.Parse(hour, CultureInfo.InvariantCulture);
        int m = int.Parse(minute, CultureInfo.InvariantCulture);
        return h <= 23 && m <= 59 ? new TimeOnly(h, m) : null;
    }

    private static DateOnly? MakeDate(Match slash, DateOnly today)
    {
        int day = int.Parse(slash.Groups[1].Value, CultureInfo.InvariantCulture);
        int month = int.Parse(slash.Groups[2].Value, CultureInfo.InvariantCulture);
        if (month is < 1 or > 12) return null;

        int year = today.Year;
        if (slash.Groups[3].Success)
        {
            year = int.Parse(slash.Groups[3].Value, CultureInfo.InvariantCulture);
            if (year < 100) year += 2000;
        }
        if (day < 1 || day > DateTime.DaysInMonth(year, month)) return null;

        var date = new DateOnly(year, month, day);
        // Sem ano e já passou: é do ano que vem.
        if (!slash.Groups[3].Success && date < today && day <= DateTime.DaysInMonth(year + 1, month)) date = date.AddYears(1);
        return date;
    }

    /// <summary>"dia 20": este mês se ainda não passou (ou é hoje e a hora não passou), senão o próximo.</summary>
    private static DateOnly? MakeDayOfMonth(Match match, DateOnly today, TimeOnly? time, TimeOnly nowTime)
    {
        int day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        if (day is < 1 or > 31) return null;

        if (match.Groups[2].Success)
        {
            int month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (month is < 1 or > 12 || day > DateTime.DaysInMonth(today.Year, month)) return null;
            var explicitDate = new DateOnly(today.Year, month, day);
            return explicitDate < today ? explicitDate.AddYears(1) : explicitDate;
        }

        var candidate = FirstWithDay(day, today);
        if (candidate == today && time is { } t && t <= nowTime) candidate = FirstWithDay(day, today.AddDays(1));
        return candidate;
    }

    /// <summary>Primeira data ≥ <paramref name="from"/> com esse dia do mês (dia 31 cai no último dia dos meses curtos).</summary>
    private static DateOnly FirstWithDay(int day, DateOnly from)
    {
        var month = new DateOnly(from.Year, from.Month, 1);
        for (int i = 0; i < 13; i++, month = month.AddMonths(1))
        {
            var candidate = new DateOnly(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
            if (candidate >= from) return candidate;
        }
        return from;
    }

    /// <summary>"sexta": a próxima sexta — hoje, se hoje é sexta e a hora ainda não passou.</summary>
    private static DateOnly NextWeekDay(DayOfWeek day, DateOnly today, TimeOnly? time, TimeOnly nowTime)
    {
        int ahead = ((int)day - (int)today.DayOfWeek + 7) % 7;
        if (ahead == 0 && time is { } t && t <= nowTime) ahead = 7;
        return today.AddDays(ahead);
    }

    private static DateOnly NextMonday(DateOnly today)
    {
        int ahead = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(ahead == 0 ? 7 : ahead);
    }
}
