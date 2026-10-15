using System.Globalization;
using Heimdall.Config;

namespace Heimdall.Services;

/// <summary>Um lembrete na lista de hoje — fixo (sem hora) ou agendado pra hoje.</summary>
internal sealed record AgendaItem(ReminderConfig Reminder, TimeOnly? Time, bool Done);

/// <summary>
/// O que conta como "lembrete de hoje" — base do badge do sino e da lista de lembretes.
/// Fixos sempre entram (pendentes até serem concluídos, quando saem do config); agendados
/// entram se caem hoje, e contam como feitos depois que o horário passou.
/// </summary>
internal static class ReminderAgenda
{
    public static List<AgendaItem> Today(AppConfig cfg, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);
        var items = new List<AgendaItem>();

        foreach (var r in cfg.Reminders)
        {
            if (string.IsNullOrWhiteSpace(r.Text)) continue;

            if (r.Kind == ReminderKind.Fixed)
            {
                items.Add(new AgendaItem(r, null, Done: false));
                continue;
            }

            if (!TimeOnly.TryParseExact(r.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                continue;

            bool occursToday = r.Recurrence switch
            {
                // Sem data: dispara no primeiro HH:mm que bater — depois de disparar
                // (Completed) não dá pra saber se foi hoje, então sai da lista.
                ReminderRecurrence.Once => r.Date is { } date ? date == today : !r.Completed,
                ReminderRecurrence.Daily => InRange(r, today),
                ReminderRecurrence.Weekly => r.Days.Contains(now.DayOfWeek) && InRange(r, today),
                _ => false
            };
            if (!occursToday) continue;

            bool done = time <= nowTime || (r.Recurrence == ReminderRecurrence.Once && r.Completed);
            items.Add(new AgendaItem(r, time, done));
        }

        // Fixos primeiro (sem hora), depois por horário.
        return items.OrderBy(i => i.Time ?? TimeOnly.MinValue).ToList();
    }

    public static int PendingCount(AppConfig cfg, DateTime now) => Today(cfg, now).Count(i => !i.Done);

    private static bool InRange(ReminderConfig r, DateOnly day) =>
        (r.StartDate is not { } start || day >= start) && (r.EndDate is not { } end || day <= end);
}
