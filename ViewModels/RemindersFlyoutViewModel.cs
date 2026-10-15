using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.UI.Popups;

namespace Heimdall.ViewModels;

/// <summary>Uma linha da lista de hoje.</summary>
internal sealed class ReminderItemViewModel : ObservableObject
{
    private bool _isDone;

    public ReminderItemViewModel(ReminderConfig reminder, DateOnly today, Action<ReminderItemViewModel> complete)
    {
        Reminder = reminder;
        Text = reminder.Title;
        TimeText = reminder.DueTime?.ToString("HH:mm") ?? "";
        Meta = reminder.DueDate switch
        {
            null => Strings.ReminderMetaFixed,
            { } date when date < today => RelativeDateFormatter.Format(date, null, today, Strings.Current),
            _ => reminder.Recurrence?.Kind switch
            {
                RepeatKind.Daily => Strings.ReminderMetaDaily,
                RepeatKind.Weekly => Strings.ReminderMetaWeekly,
                RepeatKind.Monthly => Strings.ReminderMetaMonthly,
                RepeatKind.Yearly => Strings.ReminderMetaYearly,
                _ => Strings.ReminderMetaOnce
            }
        };
        CompleteName = Strings.ReminderCompleteNamed(Text);
        CompleteCommand = new RelayCommand(() => complete(this), () => !IsDone);
    }

    public ReminderConfig Reminder { get; }
    public string Text { get; }
    public string Meta { get; }
    public string TimeText { get; }
    public string CompleteName { get; }
    public ICommand CompleteCommand { get; }

    public bool IsDone
    {
        get => _isDone;
        set => SetProperty(ref _isDone, value);
    }
}

/// <summary>Dropdown do sino: lembretes de hoje com checkbox, e "Novo lembrete".</summary>
internal sealed class RemindersFlyoutViewModel : ObservableObject, IDisposable
{
    private readonly AppConfig _cfg;
    private readonly ReminderStore _store = ReminderStore.Instance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private string _pendingText = "";
    private bool _isEmpty;

    public RemindersFlyoutViewModel(AppConfig cfg, FrameworkElement anchor)
    {
        _cfg = cfg;
        NewReminderCommand = new RelayCommand(() =>
        {
            FlyoutWindow.CloseCurrent();
            ReminderActions.OpenQuickAdd(_cfg, anchor);
        });

        Reload();
        _store.Changed += Reload;
        // Virada do dia (e recorrentes avançando) com o dropdown aberto.
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
    }

    public ObservableCollection<ReminderItemViewModel> Items { get; } = new();

    public ICommand NewReminderCommand { get; }

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

    private void Reload()
    {
        Items.Clear();
        var today = _store.Today;
        foreach (var reminder in _store.TodayAgenda())
            Items.Add(new ReminderItemViewModel(reminder, today, Complete));

        IsEmpty = Items.Count == 0;
        PendingText = Strings.RemindersPending(Items.Count(i => !i.IsDone));
    }

    private void Complete(ReminderItemViewModel item)
    {
        if (item.IsDone) return;
        item.IsDone = true;
        // Dispara Changed → Reload: concluído sai da lista, recorrente some até a próxima.
        _store.Complete(item.Reminder);
    }

    public void Dispose()
    {
        _store.Changed -= Reload;
        _timer.Stop();
    }
}
