using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>
/// Regras de quando um lembrete notifica e do que acontece ao concluir/adiar. Sem estado
/// e sem relógio próprio — "agora" e o fuso vêm de fora, pra dar pra testar.
/// </summary>
internal static class ReminderSchedule
{
    /// <summary>Instante em que a ocorrência pendente notifica; nulo se concluído ou sem data.</summary>
    public static DateTimeOffset? FireAt(ReminderConfig r, TimeOnly defaultTime, TimeZoneInfo tz)
    {
        if (r.IsCompleted) return null;
        if (r.SnoozedUntil is { } snoozed) return snoozed;
        if (r.DueDate is not { } date) return null;
        return RecurrenceCalculator.Resolve(date, r.DueTime ?? defaultTime, tz);
    }

    /// <summary>Hora de notificar já chegou e essa ocorrência ainda não foi notificada.</summary>
    public static bool IsDue(ReminderConfig r, DateTimeOffset now, TimeOnly defaultTime, TimeZoneInfo tz) =>
        FireAt(r, defaultTime, tz) is { } fire && fire <= now && (r.NotifiedAt is not { } notified || notified < fire);

    /// <summary>Próximo disparo ainda não notificado, entre todos — o único que o agendador precisa esperar.</summary>
    public static DateTimeOffset? NextFire(IEnumerable<ReminderConfig> reminders, TimeOnly defaultTime, TimeZoneInfo tz)
    {
        DateTimeOffset? next = null;
        foreach (var r in reminders)
        {
            if (FireAt(r, defaultTime, tz) is not { } fire) continue;
            if (r.NotifiedAt is { } notified && notified >= fire) continue;
            if (next is null || fire < next) next = fire;
        }
        return next;
    }

    /// <summary>Lembrete vencido: a data (e a hora, se tiver) já passou.</summary>
    public static bool IsOverdue(ReminderConfig r, DateTimeOffset now, TimeZoneInfo tz)
    {
        if (r.IsCompleted || r.DueDate is not { } date) return false;
        if (r.DueTime is { } time) return RecurrenceCalculator.Resolve(date, time, tz) <= now;
        return date < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);
    }

    /// <summary>
    /// Recorrente que ficou sem concluir: pula pra ocorrência mais recente cuja hora já
    /// chegou — fica no máximo uma atrasada, e ela notifica de novo (como o "todo dia 9h"
    /// sempre fez). true se mudou.
    /// </summary>
    public static bool CatchUp(ReminderConfig r, DateTimeOffset now, TimeOnly defaultTime, TimeZoneInfo tz)
    {
        if (!r.Repeats || r.IsCompleted || r.DueDate is not { } due) return false;

        var anchor = r.Recurrence!.Anchor ?? due;
        var time = r.DueTime ?? defaultTime;
        bool changed = false;
        while (RecurrenceCalculator.NextDate(r.Recurrence, anchor, due) is { } next
               && RecurrenceCalculator.Resolve(next, time, tz) <= now)
        {
            due = next;
            changed = true;
        }

        if (changed)
        {
            r.DueDate = due;
            r.SnoozedUntil = null;
        }
        return changed;
    }

    /// <summary>Recorrente avança pra próxima ocorrência (ou encerra se a repetição acabou); o resto fica concluído.</summary>
    public static void Complete(ReminderConfig r, DateTimeOffset now)
    {
        r.SnoozedUntil = null;
        if (r.Repeats && r.DueDate is { } due
            && RecurrenceCalculator.NextDate(r.Recurrence!, r.Recurrence!.Anchor ?? due, due) is { } next)
        {
            r.DueDate = next;
            return;
        }
        r.CompletedAt = now.LocalDateTime;
    }

    public static void Snooze(ReminderConfig r, DateTimeOffset now, int minutes) => r.SnoozedUntil = now.AddMinutes(minutes);

    public static void MarkNotified(ReminderConfig r, DateTimeOffset now) => r.NotifiedAt = now;
}
