using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.UI;
using Heimdall.UI.Popups;

namespace Heimdall.ViewModels;

/// <summary>Uma linha da lista de hoje, com as ações do menu de clique direito.</summary>
internal sealed class ReminderItemViewModel : ObservableObject
{
    private bool _isDone;

    public ReminderItemViewModel(ReminderConfig reminder, ReminderStore store, Action<ReminderItemViewModel> complete, Action<ReminderConfig> edit)
    {
        Reminder = reminder;
        Text = reminder.Title;
        TimeText = reminder.DueTime?.ToString("HH:mm") ?? "";
        IsFlagged = reminder.Flagged;
        PriorityMarker = ReminderDisplay.PriorityMarker(reminder.Priority);
        PriorityName = Strings.ReminderPriorityName(reminder.Priority);
        IsOverdue = reminder.DueDate is { } date && date < store.Today;
        IsLate = ReminderSchedule.IsOverdue(reminder, store.Now, store.TimeZone);

        var list = store.ListOf(reminder);
        ListBrush = ReminderDisplay.ListBrush(list.Color);
        Meta = BuildMeta(reminder, store, list);

        CompleteName = Strings.ReminderCompleteNamed(Text);
        SnoozeHeader = Strings.ReminderSnoozeMinutes(store.Options.SnoozeMinutes);
        FlagHeader = reminder.Flagged ? Strings.ReminderUnflag : Strings.ReminderFlag;

        CompleteCommand = new RelayCommand(() => complete(this), () => !IsDone);
        EditCommand = new RelayCommand(() => edit(reminder));
        SnoozeCommand = new RelayCommand(() => store.Snooze(reminder));
        ToggleFlagCommand = new RelayCommand(() =>
        {
            reminder.Flagged = !reminder.Flagged;
            store.Update(reminder);
        });
        DeleteCommand = new RelayCommand(() => store.Delete(reminder));
    }

    public ReminderConfig Reminder { get; }
    public string Text { get; }
    public string Meta { get; }
    public string TimeText { get; }
    public bool IsFlagged { get; }
    public string PriorityMarker { get; }
    public string PriorityName { get; }
    public bool IsOverdue { get; }

    /// <summary>A hora já passou (ou o dia, se não tem hora) — hora em destaque de alerta.</summary>
    public bool IsLate { get; }

    /// <summary>Adiar só faz sentido no que já venceu — num futuro, anteciparia o aviso.</summary>
    public bool CanSnooze => IsLate;

    public Brush ListBrush { get; }
    public string CompleteName { get; }
    public string SnoozeHeader { get; }
    public string FlagHeader { get; }

    public ICommand CompleteCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SnoozeCommand { get; }
    public ICommand ToggleFlagCommand { get; }
    public ICommand DeleteCommand { get; }

    public bool IsDone
    {
        get => _isDone;
        set => SetProperty(ref _isDone, value);
    }

    /// <summary>"Ontem · Todo dia · Trabalho" — quando, repetição e lista (só se houver mais de uma).</summary>
    private static string BuildMeta(ReminderConfig reminder, ReminderStore store, ReminderListConfig list)
    {
        var parts = new List<string>();

        if (reminder.DueDate is not { } date) parts.Add(Strings.ReminderMetaFixed);
        else if (date < store.Today) parts.Add(RelativeDateFormatter.Format(date, null, store.Today, Strings.Current));
        else parts.Add(reminder.DueTime is null ? Strings.ReminderMetaAllDay : Strings.ReminderMetaToday);

        if (ReminderDisplay.RecurrenceLabel(reminder) is { } recurrence) parts.Add(recurrence);
        if (store.Lists.Count > 1) parts.Add(list.Name);
        return string.Join(" · ", parts);
    }
}

/// <summary>Dropdown do sino: atrasados, hoje (com os sem data) e "Novo lembrete".</summary>
internal sealed class RemindersFlyoutViewModel : ObservableObject, IDisposable
{
    private readonly AppConfig _cfg;
    private readonly FrameworkElement _anchor;
    private readonly ReminderStore _store = ReminderStore.Instance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private string _pendingText = "";
    private bool _isEmpty;
    private bool _hasOverdue;
    private string _emptyNextText = "";

    public RemindersFlyoutViewModel(AppConfig cfg, FrameworkElement anchor)
    {
        _cfg = cfg;
        _anchor = anchor;
        NewReminderCommand = new RelayCommand(() =>
        {
            FlyoutWindow.CloseCurrent();
            ReminderActions.OpenNew();
        });
        OpenAllCommand = new RelayCommand(() =>
        {
            FlyoutWindow.CloseCurrent();
            ((App)Application.Current).OpenReminders();
        });

        Reload();
        _store.Changed += Reload;
        // Virada do dia, horários vencendo e recorrentes avançando com o dropdown aberto.
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
    }

    public ObservableCollection<ReminderItemViewModel> Overdue { get; } = new();

    public ObservableCollection<ReminderItemViewModel> Items { get; } = new();

    public ICommand NewReminderCommand { get; }

    /// <summary>"Ver todos": abre a janela de lembretes.</summary>
    public ICommand OpenAllCommand { get; }

    public string PendingText
    {
        get => _pendingText;
        private set => SetProperty(ref _pendingText, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    /// <summary>Segunda linha do "Tudo em ordem": o próximo lembrete futuro.</summary>
    public string EmptyNextText
    {
        get => _emptyNextText;
        private set => SetProperty(ref _emptyNextText, value);
    }

    public bool HasOverdue
    {
        get => _hasOverdue;
        private set => SetProperty(ref _hasOverdue, value);
    }

    private void Reload()
    {
        Overdue.Clear();
        Items.Clear();
        foreach (var reminder in _store.TodayAgenda())
        {
            var item = new ReminderItemViewModel(reminder, _store, Complete, Edit);
            (item.IsOverdue ? Overdue : Items).Add(item);
        }

        int total = Overdue.Count + Items.Count;
        HasOverdue = Overdue.Count > 0;
        IsEmpty = total == 0;
        EmptyNextText = IsEmpty ? ReminderDisplay.NextUpText(_store) : "";
        PendingText = Strings.RemindersPending(total);
    }

    private void Complete(ReminderItemViewModel item)
    {
        if (item.IsDone) return;
        item.IsDone = true;
        // Dispara Changed → Reload: concluído sai da lista, recorrente some até a próxima.
        _store.Complete(item.Reminder);
    }

    /// <summary>Fecha o dropdown e edita na janela de lembretes, com a linha já em edição.</summary>
    private static void Edit(ReminderConfig reminder)
    {
        FlyoutWindow.CloseCurrent();
        ReminderActions.OpenEdit(reminder);
    }

    public void Dispose()
    {
        _store.Changed -= Reload;
        _timer.Stop();
    }
}
