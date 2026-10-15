using System.Globalization;
using System.Text.Json.Nodes;
using Heimdall.Services.Reminders;

namespace Heimdall.Config;

/// <summary>
/// Lembretes do formato antigo (Kind Fixed/Scheduled + "HH:mm" + Recurrence Once/Daily/Weekly
/// + DoneOn) → formato novo (DueDate/DueTime + regra de repetição + CompletedAt). Roda no
/// JSON cru, antes de deserializar: o "Recurrence" antigo é string e o novo é objeto.
/// Tolerante: campo ausente ou de tipo inesperado vira o padrão, nunca derruba a leitura.
/// </summary>
internal static class ReminderMigration
{
    private static readonly string[] LegacyFields =
        { "Text", "Kind", "Time", "Date", "Days", "StartDate", "EndDate", "DoneOn", "Completed", "PlaySound" };

    public static void Migrate(JsonObject root, DateTime now)
    {
        if (root["Reminders"] is not JsonArray reminders) return;

        foreach (var node in reminders)
            if (node is JsonObject reminder && !reminder.ContainsKey("Title")) MigrateOne(reminder, now);
    }

    private static void MigrateOne(JsonObject r, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);

        r["Title"] = String(r, "Text") ?? "";
        r["Id"] ??= Guid.NewGuid().ToString("N");

        bool scheduled = string.Equals(String(r, "Kind"), "Scheduled", StringComparison.OrdinalIgnoreCase);
        string recurrence = String(r, "Recurrence") ?? "Once";
        bool completed = r["Completed"] is JsonValue c && c.TryGetValue<bool>(out bool b) && b;
        TimeOnly? time = TimeOnly.TryParseExact(String(r, "Time"), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t : null;

        r.Remove("Recurrence");

        // Fixo, ou agendado com horário inválido (nunca disparava): lembrete sem data.
        if (scheduled && time is { } at)
        {
            if (recurrence.Equals("Daily", StringComparison.OrdinalIgnoreCase)
                || recurrence.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
            {
                MigrateRecurring(r, recurrence, at, today, nowTime, now);
            }
            else
            {
                // "Uma vez": com data, é ela; sem data, disparava no primeiro HH:mm que batesse
                // (se já disparou, não dá pra saber o dia — fica hoje).
                var date = Date(r, "Date") ?? (completed || at > nowTime ? today : today.AddDays(1));
                r["DueDate"] = Iso(date);
                r["DueTime"] = at.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                if (completed) r["CompletedAt"] = now;
            }
        }

        foreach (var field in LegacyFields) r.Remove(field);
    }

    /// <summary>
    /// Pendente = próxima ocorrência ainda por vir. Antes, a de hoje contava como feita
    /// depois do horário ou concluída pela lista (DoneOn) — então pula pra seguinte.
    /// </summary>
    private static void MigrateRecurring(JsonObject r, string recurrence, TimeOnly time, DateOnly today, TimeOnly nowTime, DateTime now)
    {
        var start = Date(r, "StartDate");
        var end = Date(r, "EndDate");
        var days = (r["Days"] as JsonArray ?? new JsonArray())
            .Select(d => d is JsonValue v && v.TryGetValue<string>(out var s) && Enum.TryParse<DayOfWeek>(s, true, out var dow) ? (DayOfWeek?)dow : null)
            .Where(d => d is not null).Select(d => d!.Value).Distinct().ToList();

        bool weekly = recurrence.Equals("Weekly", StringComparison.OrdinalIgnoreCase);
        if (weekly && days.Count == 0) return; // semanal sem dia nunca disparava: fica sem data

        var rule = new ReminderRecurrence { Kind = weekly ? RepeatKind.Weekly : RepeatKind.Daily, Days = days, Until = end };
        var from = start is { } s && s > today ? s : today;
        var due = RecurrenceCalculator.FirstOnOrAfter(rule, from, from);
        if (due == today && (time <= nowTime || Date(r, "DoneOn") == today))
            due = RecurrenceCalculator.NextDate(rule, from, today);

        var recurrenceNode = new JsonObject
        {
            ["Kind"] = rule.Kind.ToString(),
            ["Days"] = new JsonArray(days.Select(d => (JsonNode)JsonValue.Create(d.ToString())!).ToArray()),
            ["Anchor"] = due is { } a ? Iso(a) : null,
            ["Until"] = end is { } u ? Iso(u) : null
        };
        r["Recurrence"] = recurrenceNode;
        r["DueTime"] = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        if (due is { } d) r["DueDate"] = Iso(d);
        else
        {
            // Já passou do fim: fica concluído, com a última data conhecida.
            r["DueDate"] = Iso(end ?? today);
            r["CompletedAt"] = now;
        }
    }

    private static string? String(JsonObject r, string key) =>
        r[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static DateOnly? Date(JsonObject r, string key) =>
        DateOnly.TryParse(String(r, key), CultureInfo.InvariantCulture, out var d) ? d : null;

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
