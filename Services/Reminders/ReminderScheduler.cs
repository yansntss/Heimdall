using Heimdall.Config;

namespace Heimdall.Services.Reminders;

/// <summary>Um alarme só: dispara o callback uma vez depois do atraso; armar de novo substitui o anterior.</summary>
internal interface IReminderAlarm
{
    void Set(TimeSpan delay, Action callback);
    void Cancel();
}

/// <summary>Quem mostra o aviso (o toast do Heimdall) e diz se o Windows pediu silêncio.</summary>
internal interface IReminderNotifier
{
    void Show(ReminderConfig reminder);

    /// <summary>Vários de uma vez num aviso só. <paramref name="overdue"/>: venceram com o app fechado.</summary>
    void ShowGroup(IReadOnlyList<ReminderConfig> reminders, bool overdue);

    /// <summary>Assistente de Foco / modo de apresentação ligado: segura os avisos.</summary>
    bool IsQuiet { get; }
}

/// <summary>
/// Agendador único dos lembretes: calcula o próximo disparo entre todos e arma um alarme só
/// pra ele (nada de checar lembrete por lembrete a cada minuto); a cada disparo ou mudança
/// no store, recalcula. Ao iniciar, o que venceu com o app fechado vira um aviso agrupado.
/// Com o Assistente de Foco ligado, os avisos ficam guardados e saem juntos quando ele desliga.
/// </summary>
internal sealed class ReminderScheduler : IDisposable
{
    /// <summary>Teto do alarme: relógio ajustado, suspensão etc. nunca deixam um aviso esperar mais que isso.</summary>
    internal static readonly TimeSpan MaxWait = TimeSpan.FromHours(1);

    /// <summary>Com avisos guardados (silêncio), confere de minuto em minuto se já pode mostrar.</summary>
    internal static readonly TimeSpan QuietRecheck = TimeSpan.FromMinutes(1);

    private readonly ReminderStore _store;
    private readonly IReminderNotifier _notifier;
    private readonly IReminderAlarm _alarm;
    private readonly List<ReminderConfig> _held = new();
    private bool _started;

    public ReminderScheduler(ReminderStore store, IReminderNotifier notifier, IReminderAlarm alarm)
    {
        _store = store;
        _notifier = notifier;
        _alarm = alarm;
    }

    /// <summary>Quanto falta pro alarme armado agora (pros testes e pro diagnóstico).</summary>
    public TimeSpan? ArmedFor { get; private set; }

    public void Start()
    {
        _started = true;
        _store.Changed += Reschedule;
        Fire(atStartup: true);
    }

    /// <summary>Recalcula o próximo disparo (lembrete criado/editado/adiado, relógio mudou, voltou da suspensão).</summary>
    public void Reschedule()
    {
        if (!_started) return;

        var next = ReminderSchedule.NextFire(_store.Reminders, _store.Options.DefaultTime, _store.TimeZone);
        TimeSpan? wait = next is { } at ? at - _store.Now : null;
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        if (_held.Count > 0 && (wait is null || wait > QuietRecheck)) wait = QuietRecheck;
        if (wait is null || wait > MaxWait) wait = MaxWait;

        ArmedFor = wait;
        _alarm.Set(wait.Value, () => Fire(atStartup: false));
    }

    private void Fire(bool atStartup)
    {
        // CollectDue avança recorrentes, marca como avisado e salva — o Changed que sai
        // dele já rearma o alarme; o Reschedule do fim cobre quando nada mudou.
        var due = _store.CollectDue();
        _held.AddRange(due);
        // Guardado durante o silêncio e concluído/excluído nesse meio-tempo: não avisa mais.
        _held.RemoveAll(r => r.IsCompleted || !_store.Reminders.Contains(r));

        if (_held.Count > 0 && !_notifier.IsQuiet)
        {
            var batch = _held.ToList();
            _held.Clear();
            if (batch.Count == 1) _notifier.Show(batch[0]);
            else _notifier.ShowGroup(batch, overdue: atStartup);
        }

        Reschedule();
    }

    public void Dispose()
    {
        _store.Changed -= Reschedule;
        _alarm.Cancel();
    }
}
