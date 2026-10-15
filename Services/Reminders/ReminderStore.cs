using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>
/// Dono dos lembretes enquanto o app roda: toda criação, edição, conclusão, adiamento e
/// exclusão passa por aqui, que salva e avisa (<see cref="Changed"/>) badge, listas e
/// chips de uma vez só.
///
/// Trabalha sobre o config das barras (<see cref="Attach"/> a cada reconstrução), mas
/// grava só lembretes e listas por cima do que está no disco — assim a prévia ao vivo
/// das Configurações (barras montadas de um rascunho) nunca vaza pro config.json.
/// </summary>
internal sealed class ReminderStore
{
    public static ReminderStore Instance { get; } = new(SaveToDisk, ReminderHistoryService.AppendCompleted, TimeProvider.System);

    private readonly Action<AppConfig> _save;
    private readonly Action<string, DateTime?, DateTime> _appendHistory;
    private readonly TimeProvider _clock;
    private AppConfig _cfg = new();
    private bool _paused;

    internal ReminderStore(Action<AppConfig> save, Action<string, DateTime?, DateTime> appendHistory, TimeProvider clock)
    {
        _save = save;
        _appendHistory = appendHistory;
        _clock = clock;
    }

    /// <summary>Algo mudou nos lembretes (ou o config foi trocado) — quem mostra lembretes recarrega.</summary>
    public event Action? Changed;

    public IReadOnlyList<ReminderConfig> Reminders => _cfg.Reminders;

    public IReadOnlyList<ReminderListConfig> Lists => _cfg.ReminderLists;

    public ReminderOptions Options => _cfg.ReminderOptions;

    public DateTimeOffset Now => _clock.GetLocalNow();

    public TimeZoneInfo TimeZone => _clock.LocalTimeZone;

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>"Pausar lembretes" da bandeja: as ocorrências passam sem avisar (só nesta sessão).</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            if (_paused == value) return;
            _paused = value;
            Changed?.Invoke();
        }
    }

    public void Attach(AppConfig cfg)
    {
        _cfg = cfg;
        Changed?.Invoke();
    }

    public ReminderListConfig ListOf(ReminderConfig reminder) =>
        _cfg.ReminderLists.FirstOrDefault(l => l.Id == reminder.ListId)
        ?? _cfg.ReminderLists.FirstOrDefault()
        ?? new ReminderListConfig { Id = ReminderListConfig.DefaultId };

    // ---------- Consultas ----------

    /// <summary>Lista do sino: sem data (fixos na barra) primeiro, depois Hoje com os atrasados.</summary>
    public List<ReminderConfig> TodayAgenda()
    {
        var named = _cfg.Reminders.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();
        var undated = ReminderQueries.Order(named.Where(r => !r.IsCompleted && r.DueDate is null));
        return undated.Concat(ReminderQueries.Today(named, Today)).ToList();
    }

    public int PendingToday => TodayAgenda().Count;

    /// <summary>O próximo lembrete que ainda vai avisar (estado vazio: "Próximo: ...").</summary>
    public ReminderConfig? NextUpcoming()
    {
        var now = Now;
        return _cfg.Reminders
            .Where(r => !string.IsNullOrWhiteSpace(r.Title))
            .Select(r => (Reminder: r, Fire: ReminderSchedule.FireAt(r, _cfg.ReminderOptions.DefaultTime, TimeZone)))
            .Where(x => x.Fire > now)
            .OrderBy(x => x.Fire)
            .Select(x => x.Reminder)
            .FirstOrDefault();
    }

    /// <summary>Sem data e pendentes — os chips fixos da barra.</summary>
    public List<ReminderConfig> Undated() =>
        _cfg.Reminders.Where(r => r.DueDate is null && !r.IsCompleted && !string.IsNullOrWhiteSpace(r.Title)).ToList();

    // ---------- Alterações ----------

    public void Add(ReminderConfig reminder)
    {
        reminder.CreatedAt ??= Now.LocalDateTime;
        if (!_cfg.ReminderLists.Any(l => l.Id == reminder.ListId)) reminder.ListId = ListOf(reminder).Id;
        _cfg.Reminders.Add(reminder);
        Commit();
    }

    /// <summary>Lembrete já editado no lugar (mesma referência): só salva e avisa.</summary>
    public void Update(ReminderConfig reminder) => Commit();

    /// <summary>Grava no histórico; recorrente avança pra próxima ocorrência, o resto fica concluído.</summary>
    public void Complete(ReminderConfig reminder)
    {
        var now = Now;
        _appendHistory(reminder.Title, reminder.CreatedAt, now.LocalDateTime);
        ReminderSchedule.Complete(reminder, now);
        Commit();
    }

    /// <summary>Desfaz a conclusão (checkbox desmarcado na lista de concluídos).</summary>
    public void Uncomplete(ReminderConfig reminder)
    {
        reminder.CompletedAt = null;
        Commit();
    }

    /// <summary>Adia pelos minutos das opções (ou os informados).</summary>
    public void Snooze(ReminderConfig reminder, int? minutes = null)
    {
        ReminderSchedule.Snooze(reminder, Now, minutes ?? _cfg.ReminderOptions.SnoozeMinutes);
        Commit();
    }

    public void Delete(ReminderConfig reminder)
    {
        if (_cfg.Reminders.Remove(reminder)) Commit();
    }

    // ---------- Disparo ----------

    /// <summary>
    /// Avança recorrentes atrasados e devolve o que chegou a hora de avisar, já marcando
    /// como avisado (não repete, nem entre execuções). Pausado: marca e devolve vazio.
    /// </summary>
    public List<ReminderConfig> CollectDue()
    {
        var now = Now;
        var defaultTime = _cfg.ReminderOptions.DefaultTime;
        var due = new List<ReminderConfig>();
        bool changed = false;

        foreach (var reminder in _cfg.Reminders)
        {
            changed |= ReminderSchedule.CatchUp(reminder, now, defaultTime, TimeZone);
            if (!ReminderSchedule.IsDue(reminder, now, defaultTime, TimeZone)) continue;

            ReminderSchedule.MarkNotified(reminder, now);
            changed = true;
            if (!_paused) due.Add(reminder);
        }

        if (changed) Commit();
        return due;
    }

    private void Commit()
    {
        _save(_cfg);
        Changed?.Invoke();
    }

    private static void SaveToDisk(AppConfig source)
    {
        var disk = ConfigService.Load();
        disk.Reminders = source.Reminders;
        disk.ReminderLists = source.ReminderLists;
        ConfigService.Save(disk);
    }
}
