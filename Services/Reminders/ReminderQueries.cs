using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>Listas inteligentes da janela de lembretes.</summary>
internal enum SmartList { Today, Scheduled, Flagged, All }

/// <summary>O que entra em cada lista e em que ordem. Funções puras sobre a coleção do config.</summary>
internal static class ReminderQueries
{
    /// <summary>
    /// Hoje = pendentes com data até hoje. Atrasados primeiro (são de datas anteriores);
    /// no mesmo dia, "dia todo" antes dos com hora, e com hora pelo horário.
    /// </summary>
    public static List<ReminderConfig> Today(IEnumerable<ReminderConfig> all, DateOnly today) =>
        Order(all.Where(r => !r.IsCompleted && r.DueDate is { } d && d <= today));

    public static List<ReminderConfig> Scheduled(IEnumerable<ReminderConfig> all) =>
        Order(all.Where(r => !r.IsCompleted && r.DueDate is not null));

    public static List<ReminderConfig> Flagged(IEnumerable<ReminderConfig> all) =>
        Order(all.Where(r => !r.IsCompleted && r.Flagged));

    public static List<ReminderConfig> All(IEnumerable<ReminderConfig> all) =>
        Order(all.Where(r => !r.IsCompleted));

    public static List<ReminderConfig> InList(IEnumerable<ReminderConfig> all, string listId) =>
        Order(all.Where(r => !r.IsCompleted && r.ListId == listId));

    public static List<ReminderConfig> Get(SmartList list, IEnumerable<ReminderConfig> all, DateOnly today) => list switch
    {
        SmartList.Today => Today(all, today),
        SmartList.Scheduled => Scheduled(all),
        SmartList.Flagged => Flagged(all),
        _ => All(all)
    };

    /// <summary>Concluídos (quando "mostrar concluídos" está ligado), mais recente primeiro.</summary>
    public static List<ReminderConfig> Completed(IEnumerable<ReminderConfig> all) =>
        all.Where(r => r.IsCompleted).OrderByDescending(r => r.CompletedAt).ToList();

    /// <summary>
    /// Com data antes dos sem data; por data; "dia todo" antes dos com hora; por hora.
    /// Empate: maior prioridade, depois sinalizado, depois o mais antigo.
    /// </summary>
    public static List<ReminderConfig> Order(IEnumerable<ReminderConfig> reminders) => reminders
        .OrderBy(r => r.DueDate is null)
        .ThenBy(r => r.DueDate ?? DateOnly.MaxValue)
        .ThenBy(r => r.DueTime is not null)
        .ThenBy(r => r.DueTime ?? TimeOnly.MinValue)
        .ThenByDescending(r => r.Priority)
        .ThenBy(r => r.Flagged ? 0 : 1)
        .ThenBy(r => r.CreatedAt ?? DateTime.MinValue)
        .ToList();
}
