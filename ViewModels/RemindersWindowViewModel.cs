using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;

namespace Heimdall.ViewModels;

/// <summary>Uma lista navegável: inteligente (Hoje, Agendados...) ou do usuário.</summary>
internal sealed class ReminderSourceViewModel : ObservableObject
{
    private int _count;
    private string _name;
    private Brush _brush;

    public ReminderSourceViewModel(SmartList smart, string name, string iconKey, Brush brush)
    {
        Smart = smart;
        _name = name;
        IconKey = iconKey;
        _brush = brush;
    }

    public ReminderSourceViewModel(ReminderListConfig list)
    {
        ListId = list.Id;
        _name = list.Name;
        IconKey = "H.Icon.ListChecks";
        _brush = ReminderDisplay.ListBrush(list.Color);
    }

    public SmartList? Smart { get; }
    public string? ListId { get; }
    public string IconKey { get; }

    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) OnPropertyChanged(nameof(AccessibleName)); }
    }

    public Brush Brush
    {
        get => _brush;
        set => SetProperty(ref _brush, value);
    }

    public int Count
    {
        get => _count;
        set { if (SetProperty(ref _count, value)) OnPropertyChanged(nameof(AccessibleName)); }
    }

    public string AccessibleName => Strings.ReminderListCount(Name, Count);

    public List<ReminderConfig> Query(IEnumerable<ReminderConfig> all, DateOnly today) =>
        Smart is { } smart ? ReminderQueries.Get(smart, all, today) : ReminderQueries.InList(all, ListId!);
}

/// <summary>Uma linha da janela de lembretes. Atualizada no lugar (<see cref="Refresh"/>) pra não perder foco/seleção.</summary>
internal sealed class ReminderRowViewModel : ObservableObject
{
    private readonly ReminderStore _store;
    private string _title = "";
    private string _dateText = "";
    private string _extraText = "";
    private bool _isLate;
    private bool _isFlagged;
    private bool _isHighPriority;
    private bool _canSnooze;
    private string _flagHeader = "";
    private Brush _listBrush = Brushes.Gray;

    public ReminderRowViewModel(ReminderConfig reminder, ReminderStore store)
    {
        Reminder = reminder;
        _store = store;
        CompleteCommand = new RelayCommand(() => store.Complete(reminder));
        SnoozeCommand = new RelayCommand(() => store.Snooze(reminder));
        ToggleFlagCommand = new RelayCommand(() =>
        {
            reminder.Flagged = !reminder.Flagged;
            store.Update(reminder);
        });
        DeleteCommand = new RelayCommand(() => store.Delete(reminder));
        SnoozeHeader = Strings.ReminderSnoozeMinutes(store.Options.SnoozeMinutes);
    }

    public ReminderConfig Reminder { get; }

    public string Title { get => _title; private set { if (SetProperty(ref _title, value)) OnPropertyChanged(nameof(CompleteName)); } }

    /// <summary>"Hoje, 17:00", "Sexta", "Sem data".</summary>
    public string DateText { get => _dateText; private set => SetProperty(ref _dateText, value); }

    /// <summary>" · Todo dia · Trabalho" — repetição e, numa lista inteligente, a lista.</summary>
    public string ExtraText { get => _extraText; private set => SetProperty(ref _extraText, value); }

    public bool IsLate { get => _isLate; private set { if (SetProperty(ref _isLate, value)) CanSnooze = value; } }
    public bool IsFlagged { get => _isFlagged; private set => SetProperty(ref _isFlagged, value); }
    public bool IsHighPriority { get => _isHighPriority; private set => SetProperty(ref _isHighPriority, value); }

    /// <summary>Adiar só faz sentido no que já venceu — num futuro, anteciparia o aviso.</summary>
    public bool CanSnooze { get => _canSnooze; private set => SetProperty(ref _canSnooze, value); }

    public string FlagHeader { get => _flagHeader; private set => SetProperty(ref _flagHeader, value); }
    public Brush ListBrush { get => _listBrush; private set => SetProperty(ref _listBrush, value); }

    public string CompleteName => Strings.ReminderCompleteNamed(Title);
    public string SnoozeHeader { get; }

    public ICommand CompleteCommand { get; }
    public ICommand SnoozeCommand { get; }
    public ICommand ToggleFlagCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <param name="showList">Numa lista inteligente o nome da lista entra no texto; dentro da própria lista, não.</param>
    public void Refresh(bool showList)
    {
        var r = Reminder;
        Title = r.Title;
        DateText = r.DueDate is { } date
            ? RelativeDateFormatter.Format(date, r.DueTime, _store.Today, Strings.Current)
            : Strings.ReminderNoDate;

        var extra = new List<string>();
        if (ReminderDisplay.RecurrenceLabel(r) is { } recurrence) extra.Add(recurrence);
        var list = _store.ListOf(r);
        if (showList && _store.Lists.Count > 1) extra.Add(list.Name);
        ExtraText = extra.Count > 0 ? " · " + string.Join(" · ", extra) : "";

        IsLate = ReminderSchedule.IsOverdue(r, _store.Now, _store.TimeZone);
        IsFlagged = r.Flagged;
        IsHighPriority = r.Priority == ReminderPriority.High;
        FlagHeader = r.Flagged ? Strings.ReminderUnflag : Strings.ReminderFlag;
        ListBrush = ReminderDisplay.ListBrush(list.Color);
    }
}

/// <summary>
/// Janela de lembretes: listas inteligentes no topo, lista ativa no meio, "Minhas listas"
/// no rodapé. As linhas são sincronizadas no lugar a cada mudança do store (sem recriar),
/// pra seleção, foco e rolagem sobreviverem a um "concluir".
/// </summary>
internal sealed class RemindersWindowViewModel : ObservableObject, IDisposable
{
    private readonly ReminderStore _store;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private ReminderSourceViewModel _active;
    private ReminderSourceViewModel? _selectedSmart;
    private ReminderSourceViewModel? _selectedList;
    private ReminderRowViewModel? _selectedItem;
    private bool _isEmpty;

    /// <param name="themeBrush">Pincel de um token do tema ("H.Brush.Accent") — vem da view, que é quem conhece os recursos.</param>
    public RemindersWindowViewModel(ReminderStore store, Func<string, Brush> themeBrush)
    {
        _store = store;
        SmartLists.Add(new ReminderSourceViewModel(SmartList.Today, Strings.SmartListToday, "H.Icon.Calendar", themeBrush("H.Brush.Accent")));
        SmartLists.Add(new ReminderSourceViewModel(SmartList.Scheduled, Strings.SmartListScheduled, "H.Icon.Clock", themeBrush("H.Brush.Danger")));
        SmartLists.Add(new ReminderSourceViewModel(SmartList.Flagged, Strings.SmartListFlagged, "H.Icon.Flag", themeBrush("H.Brush.Accent")));
        SmartLists.Add(new ReminderSourceViewModel(SmartList.All, Strings.SmartListAll, "H.Icon.ListChecks", themeBrush("H.Brush.TextSecondary")));
        _active = _selectedSmart = SmartLists[0];

        CompleteSelectedCommand = new RelayCommand(CompleteSelected, () => SelectedItem is not null);

        Reload();
        _store.Changed += Reload;
        // Virada do dia e horários vencendo com a janela aberta.
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
    }

    public ObservableCollection<ReminderSourceViewModel> SmartLists { get; } = new();

    public ObservableCollection<ReminderSourceViewModel> UserLists { get; } = new();

    public ObservableCollection<ReminderRowViewModel> Items { get; } = new();

    /// <summary>Espaço na linha selecionada.</summary>
    public ICommand CompleteSelectedCommand { get; }

    public ReminderSourceViewModel Active
    {
        get => _active;
        private set
        {
            if (!SetProperty(ref _active, value)) return;
            Items.Clear();
            Reload();
        }
    }

    /// <summary>Lista inteligente selecionada; escolher uma tira a seleção de "Minhas listas" (e vice-versa).</summary>
    public ReminderSourceViewModel? SelectedSmart
    {
        get => _selectedSmart;
        set
        {
            if (!SetProperty(ref _selectedSmart, value) || value is null) return;
            SelectedList = null;
            Active = value;
        }
    }

    public ReminderSourceViewModel? SelectedList
    {
        get => _selectedList;
        set
        {
            if (!SetProperty(ref _selectedList, value) || value is null) return;
            SelectedSmart = null;
            Active = value;
        }
    }

    public ReminderRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private void Reload()
    {
        var all = _store.Reminders.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();
        var today = _store.Today;

        foreach (var smart in SmartLists) smart.Count = smart.Query(all, today).Count;
        SyncUserLists(all);

        // A lista ativa foi excluída: volta pra Hoje.
        if (_active.ListId is { } id && UserLists.All(l => l.ListId != id))
        {
            SelectedSmart = SmartLists[0];
            return;
        }

        SyncItems(_active.Query(all, today));
        IsEmpty = Items.Count == 0;
    }

    private void SyncUserLists(List<ReminderConfig> all)
    {
        var lists = _store.Lists;
        for (int i = UserLists.Count - 1; i >= 0; i--)
            if (lists.All(l => l.Id != UserLists[i].ListId)) UserLists.RemoveAt(i);

        for (int i = 0; i < lists.Count; i++)
        {
            var list = lists[i];
            var vm = UserLists.FirstOrDefault(l => l.ListId == list.Id);
            if (vm is null) UserLists.Insert(Math.Min(i, UserLists.Count), vm = new ReminderSourceViewModel(list));
            vm.Name = list.Name;
            vm.Brush = ReminderDisplay.ListBrush(list.Color);
            vm.Count = ReminderQueries.InList(all, list.Id).Count;
        }
    }

    /// <summary>Deixa <see cref="Items"/> na ordem de <paramref name="target"/> mexendo só no que mudou.</summary>
    private void SyncItems(List<ReminderConfig> target)
    {
        int selectedIndex = _selectedItem is null ? -1 : Items.IndexOf(_selectedItem);
        bool showList = _active.Smart is not null;
        var wanted = target.ToHashSet();

        for (int i = Items.Count - 1; i >= 0; i--)
            if (!wanted.Contains(Items[i].Reminder)) Items.RemoveAt(i);

        for (int i = 0; i < target.Count; i++)
        {
            if (i < Items.Count && ReferenceEquals(Items[i].Reminder, target[i]))
            {
                Items[i].Refresh(showList);
                continue;
            }

            int existing = -1;
            for (int j = i + 1; j < Items.Count; j++)
                if (ReferenceEquals(Items[j].Reminder, target[i])) { existing = j; break; }

            if (existing >= 0) Items.Move(existing, i);
            else Items.Insert(i, new ReminderRowViewModel(target[i], _store));
            Items[i].Refresh(showList);
        }

        // A linha selecionada saiu (concluída): a seleção passa pra que ficou no lugar dela.
        if (_selectedItem is not null && !Items.Contains(_selectedItem))
            SelectedItem = Items.Count == 0 ? null : Items[Math.Clamp(selectedIndex, 0, Items.Count - 1)];
    }

    private void CompleteSelected()
    {
        if (SelectedItem is { } row) _store.Complete(row.Reminder);
    }

    public void Dispose()
    {
        _store.Changed -= Reload;
        _timer.Stop();
    }
}
