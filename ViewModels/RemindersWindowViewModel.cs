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
    private NewReminderViewModel? _editor;
    private bool _isNew;

    public ReminderRowViewModel(ReminderConfig reminder, ReminderStore store, Action<ReminderRowViewModel> beginEdit)
    {
        Reminder = reminder;
        _store = store;
        CompleteCommand = new RelayCommand(() => store.Complete(reminder), () => !IsNew);
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
    public NewReminderViewModel? Editor
    {
        get => _editor;
        set { if (SetProperty(ref _editor, value)) OnPropertyChanged(nameof(IsEditing)); }
    }

    public bool IsEditing => _editor is not null;

    /// <summary>
    /// Lembrete que ainda não existe (a linha em edição no topo). Ao salvar, esta mesma
    /// linha vira a linha normal do lembrete criado — o mesmo controle em outro estado.
    /// </summary>
    public bool IsNew
    {
        get => _isNew;
        set => SetProperty(ref _isNew, value);
    }

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
/// seção) e "Minhas listas" no rodapé. "Novo lembrete" põe uma linha em edição no topo da
/// lista — uma <see cref="ReminderRowViewModel"/> como as outras, em outro estado. As linhas
/// são sincronizadas no lugar a cada mudança do store (sem recriar), pra seleção, foco,
/// rolagem e uma edição em andamento sobreviverem.
/// </summary>
internal sealed class RemindersWindowViewModel : ObservableObject, IDisposable
{
    /// <summary>Chave da linha nova na sincronização (ela não tem lembrete no store ainda).</summary>
    private sealed record NewRowKey(ReminderRowViewModel Row);

    private readonly ReminderStore _store;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private ReminderSourceViewModel _active;
    private ReminderSourceViewModel? _selectedSmart;
    private ReminderSourceViewModel? _selectedList;
    private ReminderEntryViewModel? _selectedItem;
    private ReminderRowViewModel? _newRow;
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
            () => SelectedItem is ReminderRowViewModel { IsEditing: false, IsNew: false });
        NewReminderCommand = new RelayCommand(BeginNew);

        Reload();
        _store.Changed += Reload;
        // Virada do dia e horários vencendo com a janela aberta.
        _timer.Tick += (_, _) => Reload();
        _timer.Start();
    }

    public ObservableCollection<ReminderSourceViewModel> SmartLists { get; } = new();

    public ObservableCollection<ReminderSourceViewModel> UserLists { get; } = new();

    /// <summary>Linha nova (se houver) no topo, depois cabeçalhos de seção e lembretes, na ordem de exibição.</summary>
    public ObservableCollection<ReminderEntryViewModel> Items { get; } = new();

    /// <summary>Espaço na linha selecionada.</summary>
    public ICommand CompleteSelectedCommand { get; }

    /// <summary>Põe uma linha em edição no topo da lista.</summary>
    public ICommand NewReminderCommand { get; }

    /// <summary>A linha de lembrete novo em edição (topo da lista); nula fora da criação.</summary>
    public ReminderRowViewModel? NewRow
    {
        get => _newRow;
        private set
        {
            if (!SetProperty(ref _newRow, value)) return;
            OnPropertyChanged(nameof(ActiveEditor));
        }
    }

    /// <summary>A edição aberta agora (linha nova ou existente) — a view salva ela num clique fora.</summary>
    public NewReminderViewModel? ActiveEditor => _newRow?.Editor ?? EditingRow?.Editor;

    private ReminderRowViewModel? EditingRow => Items.OfType<ReminderRowViewModel>().FirstOrDefault(r => r.IsEditing && !r.IsNew);

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

    /// <summary>
    /// Salva o que estiver aberto (clique fora, janela perdeu o foco, troca de lista): a
    /// linha nova vira lembrete se tiver título e some se estiver vazia.
    /// </summary>
    public void CommitActiveEditor()
    {
        if (_newRow is not null) CommitNew(keepGoing: false);
        if (EditingRow is { } row) CommitRow(row, openNew: false);
    }

    /// <summary>Linha nova no topo da lista, já em edição (também usado pelos atalhos da barra).</summary>
    public void BeginNew()
    {
        if (EditingRow is { } row) CommitRow(row, openNew: false);
        if (_newRow is not null)
        {
            SelectedItem = _newRow;
            return;
        }

        NewRow = CreateNewRow();
        Reload();
        SelectedItem = _newRow;
    }

    /// <summary>
    /// Edita um lembrete vindo de fora (sino, chip da barra). Se ele não aparece na lista
    /// ativa, vai pra "Todos", onde todo lembrete pendente aparece.
    /// </summary>
    public void BeginEdit(ReminderConfig reminder)
    {
        if (FindRow(reminder) is null) SelectedSmart = SmartLists.First(s => s.Smart == SmartList.All);
        if (FindRow(reminder) is { } row) BeginEdit(row);
    }

    private ReminderRowViewModel? FindRow(ReminderConfig reminder) =>
        Items.OfType<ReminderRowViewModel>().FirstOrDefault(r => !r.IsNew && ReferenceEquals(r.Reminder, reminder));

    private void BeginEdit(ReminderRowViewModel row)
    {
        CommitActiveEditor();
        if (!Items.Contains(row)) return; // o commit acima pode ter tirado a linha da lista
        row.Editor = new NewReminderViewModel(_store, row.Reminder, null,
            submit: () => CommitRow(row, openNew: true),
            cancel: () => CloseRowEditor(row),
            save: () => CommitRow(row, openNew: false));
        SelectedItem = row;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    /// <summary>Título apagado = exclui (como no app de Lembretes do macOS).</summary>
    private void CommitRow(ReminderRowViewModel row, bool openNew)
    {
        if (row.Editor is not { } editor) return;
        CloseRowEditor(row);

        if (editor.IsBlank) _store.Delete(row.Reminder);
        else
        {
            editor.ApplyTo(row.Reminder);
            _store.Update(row.Reminder);
        }

        if (openNew) BeginNew();
    }

    private void CloseRowEditor(ReminderRowViewModel row)
    {
        row.Editor = null;
        OnPropertyChanged(nameof(ActiveEditor));
    }

    private ReminderRowViewModel CreateNewRow()
    {
        var row = new ReminderRowViewModel(new ReminderConfig(), _store, BeginEdit) { IsNew = true };
        row.Editor = new NewReminderViewModel(_store, null, DefaultsForActive(),
            submit: () => CommitNew(keepGoing: true), cancel: CancelNew, save: () => CommitNew(keepGoing: false));
        return row;
    }

    /// <summary>
    /// Enter salva e já abre outra linha nova no topo; clique fora salva e fecha. A linha
    /// salva continua sendo o mesmo objeto: só sai do estado "novo" e vai pro lugar dela.
    /// </summary>
    private void CommitNew(bool keepGoing)
    {
        if (_newRow is not { Editor: { } editor } row) return;
        if (editor.IsBlank)
        {
            CancelNew();
            return;
        }

        editor.ApplyTo(row.Reminder);
        row.Editor = null;
        row.IsNew = false;
        NewRow = keepGoing ? CreateNewRow() : null;
        _store.Add(row.Reminder);
        if (keepGoing) SelectedItem = _newRow;
    }

    private void CancelNew()
    {
        if (_newRow is null) return;
        NewRow = null;
        Reload();
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
        var target = WithSections(reminders, today);
        if (_newRow is not null) target.Insert(0, new NewRowKey(_newRow));
        SyncItems(target);
        IsEmpty = reminders.Count == 0 && _newRow is null;
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
                if (Items[i] is ReminderRowViewModel { IsNew: false } same) same.Refresh(showList);
                continue;
            }

            int existing = -1;
            for (int j = i + 1; j < Items.Count; j++)
                if (Matches(Items[j], target[i])) { existing = j; break; }

            if (existing >= 0) Items.Move(existing, i);
            else Items.Insert(i, Create(target[i]));
            if (Items[i] is ReminderRowViewModel { IsNew: false } row) row.Refresh(showList);
        }

        // A linha selecionada saiu (concluída): a seleção passa pra que ficou no lugar dela.
        if (_selectedItem is not null && !Items.Contains(_selectedItem))
            SelectedItem = NearestRow(Math.Max(selectedIndex, 0));
    }

    private ReminderRowViewModel? NearestRow(int index)
    {
        for (int i = Math.Min(index, Items.Count - 1); i >= 0 && i < Items.Count; i++)
            if (Items[i] is ReminderRowViewModel { IsNew: false } row) return row;
        for (int i = Math.Min(index, Items.Count - 1); i >= 0; i--)
            if (Items[i] is ReminderRowViewModel { IsNew: false } row) return row;
        return null;
    }

    private static bool Matches(ReminderEntryViewModel entry, object key) => (entry, key) switch
    {
        (ReminderSectionViewModel s, string title) => s.Title == title,
        (ReminderRowViewModel r, NewRowKey k) => ReferenceEquals(r, k.Row),
        (ReminderRowViewModel { IsNew: false } r, ReminderConfig reminder) => ReferenceEquals(r.Reminder, reminder),
        _ => false
    };

    private ReminderEntryViewModel Create(object key) => key switch
    {
        string title => new ReminderSectionViewModel(title),
        NewRowKey k => k.Row,
        ReminderConfig reminder => new ReminderRowViewModel(reminder, _store, BeginEdit),
        _ => throw new ArgumentException("Item desconhecido", nameof(key))
    };

    private void CompleteSelected()
    {
        if (SelectedItem is ReminderRowViewModel { IsNew: false } row) _store.Complete(row.Reminder);
    }

    public void Dispose()
    {
        _store.Changed -= Reload;
        _timer.Stop();
    }
}
