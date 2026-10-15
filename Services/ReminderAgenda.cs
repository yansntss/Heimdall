using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Services;

/// <summary>
/// O que conta como "lembrete de hoje" — base do badge do sino e da lista de lembretes.
/// Sem data (fixos na barra) sempre entram, primeiro; depois a lista Hoje, com os
/// atrasados. Concluído sai — recorrente concluído some até a próxima ocorrência.
/// </summary>
internal static class ReminderAgenda
{
    public static List<ReminderConfig> Today(AppConfig cfg, DateTime now)
    {
        var pending = cfg.Reminders.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();
        var undated = ReminderQueries.Order(pending.Where(r => !r.IsCompleted && r.DueDate is null));
        return undated.Concat(ReminderQueries.Today(pending, DateOnly.FromDateTime(now))).ToList();
    }

    public static int PendingCount(AppConfig cfg, DateTime now) => Today(cfg, now).Count;

    /// <summary>Algum lembrete foi criado, concluído ou excluído — badge e lista se atualizam na hora, sem esperar o timer.</summary>
    public static event Action? Changed;

    public static void NotifyChanged() => Changed?.Invoke();
}
