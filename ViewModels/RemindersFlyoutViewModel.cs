using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI.Popups;

namespace Heimdall.ViewModels;

/// <summary>Uma linha da lista de hoje.</summary>
internal sealed class ReminderItemViewModel : ObservableObject
{
    private bool _isDone;

    public ReminderItemViewModel(AgendaItem item, Action<ReminderItemViewModel> complete)
    {
        Reminder = item.Reminder;
        Text = item.Reminder.Text;
        TimeText = item.Time?.ToString("HH:mm") ?? "";
        Meta = item.Reminder.Kind == ReminderKind.Fixed
            ? Strings.ReminderMetaFixed
            : item.Reminder.Recurrence switch
            {
                ReminderRecurrence.Daily => Strings.ReminderMetaDaily,
                ReminderRecurrence.Weekly => Strings.ReminderMetaWeekly,
                _ => Strings.ReminderMetaOnce
            };
        _isDone = item.Done;
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
        ReminderAgenda.Changed += Reload;
        // Horários passando com o dropdown aberto viram "feitos".
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
        foreach (var item in ReminderAgenda.Today(_cfg, DateTime.Now))
            Items.Add(new ReminderItemViewModel(item, Complete));

        IsEmpty = Items.Count == 0;
        PendingText = Strings.RemindersPending(Items.Count(i => !i.IsDone));
    }

    private void Complete(ReminderItemViewModel item)
    {
        if (item.IsDone) return;
        item.IsDone = true;
        // Dispara ReminderAgenda.Changed → Reload: fixo some da lista, agendado fica marcado.
        ReminderActions.Complete(_cfg, item.Reminder);
    }

    public void Dispose()
    {
        ReminderAgenda.Changed -= Reload;
        _timer.Stop();
    }
}
