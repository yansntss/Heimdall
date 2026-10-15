using System.Collections.ObjectModel;
using System.Globalization;
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

/// <summary>Item da lista da janela: cabeçalho de seção ou lembrete.</summary>
internal abstract class ReminderEntryViewModel : ObservableObject;

/// <summary>Cabeçalho de seção ("ATRASADOS", "AMANHÃ"...) — não selecionável.</summary>
internal sealed class ReminderSectionViewModel(string title) : ReminderEntryViewModel
{
    public string Title { get; } = title;
}

/// <summary>Uma linha da janela de lembretes. Atualizada no lugar (<see cref="Refresh"/>) pra não perder foco/seleção.</summary>
internal sealed class ReminderRowViewModel : ReminderEntryViewModel
{
    private readonly ReminderStore _store;
    private string _title = "";
    private string _note = "";
    private string _dateText = "";
    private string _extraText = "";
    private bool _isLate;
    private bool _isFlagged;
    private bool _isHighPriority;
    private bool _canSnooze;
    private string _flagHeader = "";
    private Brush _listBrush = Brushes.Gray;
    private ReminderEditorViewModel? _editor;

    public ReminderRowViewModel(ReminderConfig reminder, ReminderStore store, Action<ReminderRowViewModel> beginEdit)
    {
        Reminder = reminder;
        _store = store;
        CompleteCommand = new RelayCommand(() => store.Complete(reminder));
        BeginEditCommand = new RelayCommand(() => beginEdit(this), () => !IsEditing);
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

    public string Note { get => _note; private set { if (SetProperty(ref _note, value)) OnPropertyChanged(nameof(HasNote)); } }

    public bool HasNote => _note.Length > 0;

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

    /// <summary>Editando inline: o título vira caixa de texto e aparecem os chips.</summary>
    public ReminderEditorViewModel? Editor
    {
        get => _editor;
        set { if (SetProperty(ref _editor, value)) OnPropertyChanged(nameof(IsEditing)); }
    }

    public bool IsEditing => _editor is not null;

    public string CompleteName => Strings.ReminderCompleteNamed(Title);
    public string SnoozeHeader { get; }

    public ICommand CompleteCommand { get; }
    public ICommand BeginEditCommand { get; }
    public ICommand SnoozeCommand { get; }
    public ICommand ToggleFlagCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <param name="showList">Numa lista inteligente o nome da lista entra no texto; dentro da própria lista, não.</param>
    public void Refresh(bool showList)
    {
        var r = Reminder;
        Title = r.Title;
        Note = r.Note?.Trim() ?? "";
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
/// Janela de lembretes: listas inteligentes no topo, lista ativa no meio (com cabeçalhos de
/// seção), linha de "Novo lembrete" embaixo dela e "Minhas listas" no rodapé. As linhas são
/// sincronizadas no lugar a cada mudança do store (sem recriar), pra seleção, foco, rolagem
/// e uma edição em andamento sobreviverem.
/// </summary>
internal sealed class RemindersWindowViewModel : ObservableObject, IDisposable
{
    private readonly ReminderStore _store;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private ReminderSourceViewModel _active;
    private ReminderSourceViewModel? _selectedSmart;
    private ReminderSourceViewModel? _selectedList;
    private ReminderEntryViewModel? _selectedItem;
    private ReminderEditorViewModel? _draft;
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

        CompleteSelectedCommand = new RelayCommand(CompleteSelected,
            () => SelectedItem is ReminderRowViewModel { IsEditing: false });
        NewReminderCommand = new RelayCommand(BeginDraft);

        Reload();
        _store.Changed += Reload;
        // Virada do dia e horários vencendo com a janela aberta.
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
    }

    public ObservableCollection<ReminderSourceViewModel> SmartLists { get; } = new();

    public ObservableCollection<ReminderSourceViewModel> UserLists { get; } = new();

    /// <summary>Cabeçalhos de seção e lembretes, na ordem de exibição.</summary>
    public ObservableCollection<ReminderEntryViewModel> Items { get; } = new();

    /// <summary>Espaço na linha selecionada.</summary>
    public ICommand CompleteSelectedCommand { get; }

    /// <summary>Abre a linha de lembrete novo embaixo da lista.</summary>
    public ICommand NewReminderCommand { get; }

    /// <summary>Lembrete novo sendo digitado; nulo = mostra o botão "Novo lembrete".</summary>
    public ReminderEditorViewModel? Draft
    {
        get => _draft;
        private set { if (SetProperty(ref _draft, value)) OnPropertyChanged(nameof(IsDrafting)); }
    }

    public bool IsDrafting => _draft is not null;

    /// <summary>A edição aberta agora (rascunho ou linha) — a view salva ela num clique fora.</summary>
    public ReminderEditorViewModel? ActiveEditor => _draft ?? EditingRow?.Editor;

    private ReminderRowViewModel? EditingRow => Items.OfType<ReminderRowViewModel>().FirstOrDefault(r => r.IsEditing);

    public ReminderSourceViewModel Active
    {
        get => _active;
        private set
        {
            if (ReferenceEquals(_active, value)) return;
            CommitActiveEditor();
            _active = value;
            OnPropertyChanged();
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

    /// <summary>Cabeçalho de seção não fica selecionado (a view nem deixa, mas por garantia).</summary>
    public ReminderEntryViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value is ReminderSectionViewModel ? _selectedItem : value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    // ---------- Edição ----------

    /// <summary>Salva o que estiver aberto (clique fora, janela perdeu o foco, troca de lista).</summary>
    public void CommitActiveEditor()
    {
        if (_draft is not null) CommitDraft(keepGoing: false);
        if (EditingRow is { } row) CommitRow(row, openDraft: false);
    }

    private void BeginEdit(ReminderRowViewModel row)
    {
        CommitActiveEditor();
        if (!Items.Contains(row)) return; // o commit acima pode ter tirado a linha da lista
        row.Editor = new ReminderEditorViewModel(_store, row.Reminder, null,
            submit: () => CommitRow(row, openDraft: true),
            cancel: () => CloseRowEditor(row));
        SelectedItem = row;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    /// <summary>Título apagado = exclui (como no app de Lembretes do macOS).</summary>
    private void CommitRow(ReminderRowViewModel row, bool openDraft)
    {
        if (row.Editor is not { } editor) return;
        CloseRowEditor(row);

        if (editor.IsBlank) _store.Delete(row.Reminder);
        else
        {
            editor.ApplyTo(row.Reminder);
            _store.Update(row.Reminder);
        }

        if (openDraft) BeginDraft();
    }

    private void CloseRowEditor(ReminderRowViewModel row)
    {
        row.Editor = null;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    private void BeginDraft()
    {
        if (EditingRow is { } row) CommitRow(row, openDraft: false);
        if (_draft is not null) return;
        Draft = NewDraft();
        OnPropertyChanged(nameof(ActiveEditor));
    }

    private ReminderEditorViewModel NewDraft() =>
        new(_store, null, DefaultsForActive(), submit: () => CommitDraft(keepGoing: true), cancel: CancelDraft);

    /// <summary>Enter salva e já abre outra linha; clique fora salva e fecha.</summary>
    private void CommitDraft(bool keepGoing)
    {
        if (_draft is not { } draft) return;
        if (draft.IsBlank)
        {
            CancelDraft();
            return;
        }

        var reminder = new ReminderConfig();
        draft.ApplyTo(reminder);
        Draft = keepGoing ? NewDraft() : null;
        OnPropertyChanged(nameof(ActiveEditor));
        _store.Add(reminder);
    }

    private void CancelDraft()
    {
        Draft = null;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    /// <summary>Lembrete novo nasce com a cara da lista onde foi criado: em Hoje tem data de hoje, em Sinalizados vem sinalizado.</summary>
    private ReminderConfig DefaultsForActive() => _active switch
    {
        { Smart: SmartList.Today or SmartList.Scheduled } => new ReminderConfig { DueDate = _store.Today },
        { Smart: SmartList.Flagged } => new ReminderConfig { Flagged = true },
        { ListId: { } id } => new ReminderConfig { ListId = id },
        _ => new ReminderConfig()
    };

    // ---------- Sincronização ----------

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

        var reminders = _active.Query(all, today);
        SyncItems(WithSections(reminders, today));
        IsEmpty = reminders.Count == 0;
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

    /// <summary>
    /// Intercala os cabeçalhos: Hoje separa ATRASADOS/HOJE (só se houver atrasado);
    /// Agendados agrupa por dia; Todos agrupa por lista; o resto vai sem cabeçalho.
    /// Itens são <see cref="string"/> (cabeçalho) ou <see cref="ReminderConfig"/>.
    /// </summary>
    private List<object> WithSections(List<ReminderConfig> reminders, DateOnly today)
    {
        Func<ReminderConfig, string>? section = _active.Smart switch
        {
            SmartList.Today when reminders.Any(r => r.DueDate < today) =>
                r => r.DueDate < today ? Strings.RemindersOverdue : Strings.RemindersToday,
            SmartList.Scheduled => r => DaySection(r.DueDate!.Value, today),
            SmartList.All when _store.Lists.Count > 1 => r => _store.ListOf(r).Name.ToUpper(SectionCulture),
            _ => null
        };

        if (_active.Smart == SmartList.All && section is not null)
        {
            // Agrupar por lista: as listas na ordem delas, cada uma na ordem de sempre.
            var order = _store.Lists.Select((l, i) => (l.Id, i)).ToDictionary(x => x.Id, x => x.i);
            reminders = reminders.OrderBy(r => order.GetValueOrDefault(_store.ListOf(r).Id)).ToList();
        }

        var result = new List<object>();
        string? current = null;
        foreach (var reminder in reminders)
        {
            if (section?.Invoke(reminder) is { } title && title != current)
            {
                result.Add(title);
                current = title;
            }
            result.Add(reminder);
        }
        return result;
    }

    private static CultureInfo SectionCulture => new(Strings.Current == AppLanguage.PtBr ? "pt-BR" : "en-US");

    private static string DaySection(DateOnly date, DateOnly today) =>
        date < today ? Strings.RemindersOverdue : RelativeDateFormatter.Format(date, null, today, Strings.Current).ToUpper(SectionCulture);

    /// <summary>Deixa <see cref="Items"/> na ordem de <paramref name="target"/> mexendo só no que mudou.</summary>
    private void SyncItems(List<object> target)
    {
        int selectedIndex = _selectedItem is null ? -1 : Items.IndexOf(_selectedItem);
        bool showList = _active.Smart is not null;

        for (int i = Items.Count - 1; i >= 0; i--)
            if (!target.Any(t => Matches(Items[i], t))) Items.RemoveAt(i);

        for (int i = 0; i < target.Count; i++)
        {
            if (i < Items.Count && Matches(Items[i], target[i]))
            {
                if (Items[i] is ReminderRowViewModel same) same.Refresh(showList);
                continue;
            }

            int existing = -1;
            for (int j = i + 1; j < Items.Count; j++)
                if (Matches(Items[j], target[i])) { existing = j; break; }

            if (existing >= 0) Items.Move(existing, i);
            else Items.Insert(i, Create(target[i]));
            if (Items[i] is ReminderRowViewModel row) row.Refresh(showList);
        }

        // A linha selecionada saiu (concluída): a seleção passa pra que ficou no lugar dela.
        if (_selectedItem is not null && !Items.Contains(_selectedItem))
            SelectedItem = NearestRow(Math.Max(selectedIndex, 0));
    }

    private ReminderRowViewModel? NearestRow(int index)
    {
        for (int i = Math.Min(index, Items.Count - 1); i >= 0 && i < Items.Count; i++)
            if (Items[i] is ReminderRowViewModel row) return row;
        for (int i = Math.Min(index, Items.Count - 1); i >= 0; i--)
            if (Items[i] is ReminderRowViewModel row) return row;
        return null;
    }

    private static bool Matches(ReminderEntryViewModel entry, object key) => (entry, key) switch
    {
        (ReminderSectionViewModel s, string title) => s.Title == title,
        (ReminderRowViewModel r, ReminderConfig reminder) => ReferenceEquals(r.Reminder, reminder),
        _ => false
    };

    private ReminderEntryViewModel Create(object key) => key switch
    {
        string title => new ReminderSectionViewModel(title),
        ReminderConfig reminder => new ReminderRowViewModel(reminder, _store, BeginEdit),
        _ => throw new ArgumentException("Item desconhecido", nameof(key))
    };

    private void CompleteSelected()
    {
        if (SelectedItem is ReminderRowViewModel row) _store.Complete(row.Reminder);
    }

    public void Dispose()
    {
        _store.Changed -= Reload;
        _timer.Stop();
    }
}
