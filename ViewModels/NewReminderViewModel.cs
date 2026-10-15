using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;

namespace Heimdall.ViewModels;

/// <summary>Uma lista na página "Lista" do painel de detalhes.</summary>
internal sealed record ReminderListOption(string Id, string Name, Brush Brush);

/// <summary>Escolhas da página Repetir.</summary>
internal enum RepeatChoice { Never, Daily, Weekly, Weekdays, Monthly, Custom }

internal sealed record RepeatOption(RepeatChoice Choice, string Label);

/// <summary>Unidade do "A cada N" do Personalizado.</summary>
internal sealed record RepeatUnitOption(RepeatKind Kind, string Label);

/// <summary>Como a repetição termina (substitui os antigos "De:"/"até:").</summary>
internal enum RepeatEnd { Never, OnDate, AfterCount }

internal sealed record RepeatEndOption(RepeatEnd End, string Label);

/// <summary>Uma opção de prioridade ("Nenhuma"... "Alta").</summary>
internal sealed record PriorityOption(ReminderPriority Priority, string Label);

/// <summary>Um círculo de dia da semana do Personalizado ("S", anunciado como "Segunda").</summary>
internal sealed class WeekDayToggleViewModel(DayOfWeek day, string letter, string name, Action<DayOfWeek, bool> toggled) : ObservableObject
{
    private bool _isSelected;

    public DayOfWeek Day { get; } = day;
    public string Letter { get; } = letter;
    public string Name { get; } = name;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (SetProperty(ref _isSelected, value)) toggled(Day, value); }
    }

    /// <summary>Atualiza a partir da regra sem avisar de volta.</summary>
    internal void Sync(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;
        OnPropertyChanged(nameof(IsSelected));
    }
}

/// <summary>
/// Estado em edição de um lembrete (novo ou existente): título com o parser de linguagem
/// natural rodando ao vivo (o chip de data já mostra o que foi entendido), os chips da linha
/// e tudo que o painel de detalhes mexe. Nada vai pro store antes de <see cref="ApplyTo"/>
/// — que só acontece no Enter, no "Adicionar" ou num clique fora.
/// </summary>
internal sealed class NewReminderViewModel : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly DayOfWeek[] Weekdays =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

    private readonly ReminderStore _store;
    private readonly ReminderConfig? _source;
    private readonly string _originalTitle;

    private string _title;
    private string _note;
    private bool _showNote;
    private DateOnly? _dueDate;
    private TimeOnly? _dueTime;
    /// <summary>Regra em edição (cópia); nula = não repete.</summary>
    private ReminderRecurrence? _rule;
    /// <summary>"Personalizado" escolhido: o bloco continua aberto mesmo se a regra coincidir com uma opção pronta.</summary>
    private bool _customChosen;
    private bool _flagged;
    private ReminderPriority _priority;
    private string _listId;
    private string _timeText = "";
    private bool _dateChosenByHand;
    private bool _seededFromParsed;
    private ParsedReminder? _parsed;
    private ReminderDetailsViewModel? _details;

    /// <param name="source">Nulo = lembrete novo, com os padrões de <paramref name="defaults"/>.</param>
    /// <param name="submit">Enter: salva (e abre outra linha).</param>
    /// <param name="cancel">Esc: descarta.</param>
    /// <param name="save">"Adicionar"/"Salvar" do painel de detalhes: salva e fecha a linha.</param>
    public NewReminderViewModel(ReminderStore store, ReminderConfig? source, ReminderConfig? defaults,
        Action submit, Action cancel, Action? save = null)
    {
        _store = store;
        _source = source;
        var from = source ?? defaults ?? new ReminderConfig();

        _originalTitle = source?.Title ?? "";
        _title = _originalTitle;
        _note = from.Note ?? "";
        _showNote = !string.IsNullOrWhiteSpace(_note);
        _dueDate = from.DueDate;
        _dueTime = from.DueTime;
        _rule = Clone(from.Recurrence);
        _flagged = from.Flagged;
        _priority = from.Priority;
        _listId = store.ListOf(from).Id;
        _timeText = _dueTime?.ToString("HH:mm", Inv) ?? "";

        SubmitCommand = new RelayCommand(submit);
        CancelCommand = new RelayCommand(cancel);
        SaveCommand = new RelayCommand(save ?? submit);
        ToggleFlagCommand = new RelayCommand(() => Flagged = !Flagged);
        ToggleNoteCommand = new RelayCommand(() => ShowNote = !ShowNote);
        QuickDateCommand = new RelayCommand(p => SetQuickDate(p as string));
        ChooseListCommand = new RelayCommand(p => { if (p is string id) ListId = id; });
        StepTimeCommand = new RelayCommand(p => StepTime(p is string s && int.TryParse(s, out int m) ? m : 15));
        OpenDetailsCommand = new RelayCommand(p => OpenDetails(p is DetailsPage page ? page : DetailsPage.Main));

        RepeatOptions = new[]
        {
            new RepeatOption(RepeatChoice.Never, Strings.RepeatNever),
            new RepeatOption(RepeatChoice.Daily, Strings.RepeatDaily),
            new RepeatOption(RepeatChoice.Weekly, Strings.RepeatWeekly),
            new RepeatOption(RepeatChoice.Weekdays, Strings.RepeatWeekdays),
            new RepeatOption(RepeatChoice.Monthly, Strings.RepeatMonthly),
            new RepeatOption(RepeatChoice.Custom, Strings.RepeatCustom),
        };
        UnitOptions = new[]
        {
            new RepeatUnitOption(RepeatKind.Daily, Strings.UnitDay),
            new RepeatUnitOption(RepeatKind.Weekly, Strings.UnitWeek),
            new RepeatUnitOption(RepeatKind.Monthly, Strings.UnitMonth),
            new RepeatUnitOption(RepeatKind.Yearly, Strings.UnitYear),
        };
        EndOptions = new[]
        {
            new RepeatEndOption(RepeatEnd.Never, Strings.RepeatEndNever),
            new RepeatEndOption(RepeatEnd.OnDate, Strings.RepeatEndOnDate),
            new RepeatEndOption(RepeatEnd.AfterCount, Strings.RepeatEndAfterCount),
        };
        PriorityOptions = new[]
        {
            new PriorityOption(ReminderPriority.None, Strings.PriorityNone),
            new PriorityOption(ReminderPriority.Low, Strings.PriorityLow),
            new PriorityOption(ReminderPriority.Medium, Strings.PriorityMedium),
            new PriorityOption(ReminderPriority.High, Strings.PriorityHigh),
        };
        ListOptions = store.Lists.Select(l => new ReminderListOption(l.Id, l.Name, ReminderDisplay.ListBrush(l.Color))).ToList();

        // Círculos de dia começando no domingo; letra e nome no idioma do app.
        var names = new CultureInfo(Strings.Current == AppLanguage.PtBr ? "pt-BR" : "en-US");
        WeekDayToggles = Enumerable.Range(0, 7).Select(i =>
        {
            var day = (DayOfWeek)i;
            var name = names.DateTimeFormat.GetDayName(day);
            int dash = name.IndexOf('-');
            name = char.ToUpper(name[0], names) + (dash > 0 ? name[1..dash] : name[1..]);
            return new WeekDayToggleViewModel(day, name[..1], name, ToggleDay);
        }).ToList();

        _customChosen = ChoiceOf(_rule, custom: false) == RepeatChoice.Custom;
        _parsed = Parse(_title);
        SyncDayToggles();
    }

    public bool IsNew => _source is null;

    public ICommand SubmitCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleFlagCommand { get; }
    public ICommand ToggleNoteCommand { get; }

    /// <summary>Parâmetro: "none", "today", "tomorrow", "nextweek".</summary>
    public ICommand QuickDateCommand { get; }

    /// <summary>Parâmetro: id da lista.</summary>
    public ICommand ChooseListCommand { get; }

    /// <summary>Stepper da hora; parâmetro em minutos ("15", "-15").</summary>
    public ICommand StepTimeCommand { get; }

    /// <summary>Abre o painel de detalhes; parâmetro opcional: a página (<see cref="DetailsPage"/>).</summary>
    public ICommand OpenDetailsCommand { get; }

    public IReadOnlyList<RepeatOption> RepeatOptions { get; }
    public IReadOnlyList<RepeatUnitOption> UnitOptions { get; }
    public IReadOnlyList<RepeatEndOption> EndOptions { get; }
    public IReadOnlyList<PriorityOption> PriorityOptions { get; }
    public IReadOnlyList<ReminderListOption> ListOptions { get; }
    public IReadOnlyList<WeekDayToggleViewModel> WeekDayToggles { get; }

    /// <summary>Painel de detalhes aberto (nulo = fechado).</summary>
    public ReminderDetailsViewModel? Details
    {
        get => _details;
        private set { if (SetProperty(ref _details, value)) OnPropertyChanged(nameof(IsDetailsOpen)); }
    }

    public bool IsDetailsOpen
    {
        get => _details is not null;
        set { if (!value) Details = null; }
    }

    // ---------- Título e nota ----------

    public string Title
    {
        get => _title;
        set
        {
            if (!SetProperty(ref _title, value)) return;
            _parsed = Parse(value);
            NotifySchedule();
        }
    }

    public string Note
    {
        get => _note;
        set
        {
            if (!SetProperty(ref _note, value)) return;
            OnPropertyChanged(nameof(HasNote));
            OnPropertyChanged(nameof(NotePreview));
        }
    }

    public bool HasNote => !string.IsNullOrWhiteSpace(_note);

    /// <summary>Primeira linha da nota (linha "Notas" do painel).</summary>
    public string NotePreview => HasNote ? _note.Trim().Split('\n')[0].Trim() : Strings.DetailsNotesAdd;

    public bool ShowNote
    {
        get => _showNote;
        set => SetProperty(ref _showNote, value);
    }

    // ---------- Bandeira, prioridade, lista ----------

    public bool Flagged
    {
        get => _flagged;
        set => SetProperty(ref _flagged, value);
    }

    public PriorityOption SelectedPriority
    {
        get => PriorityOptions.First(o => o.Priority == _priority);
        set
        {
            if (value is null || value.Priority == _priority) return;
            _priority = value.Priority;
            OnPropertyChanged();
        }
    }

    public string ListId
    {
        get => _listId;
        private set
        {
            if (!SetProperty(ref _listId, value)) return;
            OnPropertyChanged(nameof(ListName));
            OnPropertyChanged(nameof(ListBrush));
            OnPropertyChanged(nameof(SelectedList));
        }
    }

    public ReminderListOption SelectedList
    {
        get => ListOptions.FirstOrDefault(o => o.Id == _listId) ?? ListOptions.First();
        set { if (value is not null) ListId = value.Id; }
    }

    private ReminderListConfig CurrentList => _store.Lists.FirstOrDefault(l => l.Id == _listId) ?? _store.ListOf(new ReminderConfig());

    public string ListName => CurrentList.Name;

    public Brush ListBrush => ReminderDisplay.ListBrush(CurrentList.Color);

    // ---------- Data e hora ----------

    /// <summary>O que o chip de data mostra: o que o parser entendeu do título ou a data escolhida.</summary>
    public string DateChipText
    {
        get
        {
            var (date, time, rule) = EffectiveSchedule();
            if (date is not { } d) return Strings.ChipDate;
            var text = RelativeDateFormatter.Format(d, time, _store.Today, Strings.Current);
            return rule is null ? text : $"{text} · {SelectedRepeat.Label}";
        }
    }

    public bool HasDate
    {
        get => EffectiveSchedule().Date is not null;
        set
        {
            if (value == HasDate) return;
            TakeOverFromTitle();
            if (value) SetDate(_store.Today);
            else
            {
                _dueTime = null;
                _timeText = "";
                _rule = null;
                _customChosen = false;
                SetDate(null);
                OnPropertyChanged(nameof(TimeText));
            }
        }
    }

    /// <summary>Dia escolhido no calendário.</summary>
    public DateOnly? Date
    {
        get => EffectiveSchedule().Date;
        set
        {
            if (value == Date) return;
            TakeOverFromTitle();
            SetDate(value);
        }
    }

    public bool HasTime
    {
        get => EffectiveSchedule().Time is not null;
        set
        {
            if (value == HasTime) return;
            TakeOverFromTitle();
            // Liga na próxima hora cheia (ex.: 10:20 → 11:00).
            SetTime(value ? new TimeOnly(Math.Min(23, _store.Now.Hour + 1), 0) : null);
        }
    }

    /// <summary>"HH:mm" (campo mono do painel); vazio = sem hora.</summary>
    public string TimeText
    {
        get => _timeText;
        set
        {
            if (!SetProperty(ref _timeText, value)) return;
            if (string.IsNullOrWhiteSpace(value)) SetTime(null, keepText: true);
            else if (TimeOnly.TryParseExact(value.Trim(), new[] { "HH:mm", "H:mm", "HH", "H" }, Inv, DateTimeStyles.None, out var time))
                SetTime(time, keepText: true);
        }
    }

    // ---------- Repetição ----------

    public RepeatOption SelectedRepeat
    {
        get => RepeatOptions.First(o => o.Choice == ChoiceOf(EffectiveSchedule().Rule, UsesParsed ? false : _customChosen));
        set
        {
            if (value is null || value.Choice == SelectedRepeat.Choice) return;
            TakeOverFromTitle();
            var day = (_dueDate ?? _store.Today).DayOfWeek;
            _customChosen = value.Choice == RepeatChoice.Custom;
            _rule = value.Choice switch
            {
                RepeatChoice.Never => null,
                RepeatChoice.Daily => new ReminderRecurrence { Kind = RepeatKind.Daily },
                RepeatChoice.Weekly => new ReminderRecurrence { Kind = RepeatKind.Weekly, Days = new() { day } },
                RepeatChoice.Weekdays => new ReminderRecurrence { Kind = RepeatKind.Weekly, Days = Weekdays.ToList() },
                RepeatChoice.Monthly => new ReminderRecurrence { Kind = RepeatKind.Monthly },
                // Personalizado parte do que já estava (ou de "toda semana, no dia da data").
                _ => _rule ?? new ReminderRecurrence { Kind = RepeatKind.Weekly, Days = new() { day } }
            };
            _dateChosenByHand = true;
            if (_rule is not null && _dueDate is null) _dueDate = _store.Today;
            NotifySchedule();
        }
    }

    public bool IsCustomRepeat => SelectedRepeat.Choice == RepeatChoice.Custom;

    /// <summary>"A cada N" (campo mono de 46px).</summary>
    public string IntervalText
    {
        get => (_rule?.Interval ?? 1).ToString(Inv);
        set
        {
            if (!int.TryParse(value, NumberStyles.None, Inv, out int n) || n is < 1 or > 99) return;
            EditCustom(r => r.Interval = n);
        }
    }

    public RepeatUnitOption SelectedUnit
    {
        get => UnitOptions.First(u => u.Kind == (_rule?.Kind ?? RepeatKind.Weekly));
        set
        {
            if (value is null || value.Kind == _rule?.Kind) return;
            EditCustom(r =>
            {
                r.Kind = value.Kind;
                r.Days = value.Kind == RepeatKind.Weekly ? new() { (_dueDate ?? _store.Today).DayOfWeek } : new();
            });
        }
    }

    /// <summary>Os círculos "NOS DIAS" só aparecem com a unidade semana.</summary>
    public bool IsWeekUnit => _rule?.Kind == RepeatKind.Weekly;

    public RepeatEndOption SelectedEnd
    {
        get => EndOptions.First(o => o.End == EndOf(_rule));
        set
        {
            if (value is null || value.End == EndOf(_rule)) return;
            EditCustom(r =>
            {
                r.Until = value.End == RepeatEnd.OnDate ? (_dueDate ?? _store.Today).AddMonths(1) : null;
                r.Count = value.End == RepeatEnd.AfterCount ? 5 : null;
            });
        }
    }

    public bool IsEndOnDate => _rule?.Until is not null;
    public bool IsEndAfterCount => _rule?.Count is not null;

    public DateOnly? EndDate
    {
        get => _rule?.Until;
        set { if (value is { } d && _rule?.Until is not null) EditCustom(r => r.Until = d); }
    }

    public string EndCountText
    {
        get => (_rule?.Count ?? 5).ToString(Inv);
        set
        {
            if (_rule?.Count is null || !int.TryParse(value, NumberStyles.None, Inv, out int n) || n is < 1 or > 999) return;
            EditCustom(r => r.Count = n);
        }
    }

    /// <summary>Valor da linha "Terminar": "Nunca", "20 de dez", "Após 5 vezes".</summary>
    public string EndText => _rule switch
    {
        { Until: { } until } => RelativeDateFormatter.Format(until, null, _store.Today, Strings.Current),
        { Count: { } count } => Strings.RepeatEndAfterTimes(count),
        _ => Strings.RepeatEndNever
    };

    /// <summary>"Toda semana, seg · qua · sex, às 17:00" — recalculado a cada mudança.</summary>
    public string RepeatSummary
    {
        get
        {
            var (date, time, rule) = EffectiveSchedule();
            return RecurrenceSummary.Describe(rule, date ?? _store.Today, time, Strings.Current);
        }
    }

    /// <summary>A data do chip veio do título (vai sair do texto ao salvar).</summary>
    public bool IsDateFromTitle => UsesParsed;

    public bool IsBlank => string.IsNullOrWhiteSpace(_title);

    // ---------- Painel de detalhes ----------

    /// <summary>
    /// Retrato antes (pro "Cancelar"), depois os campos assumem o que o parser entendeu —
    /// o painel abre já com "amanhã às 14:00" preenchido.
    /// </summary>
    private void OpenDetails(DetailsPage page)
    {
        var snapshot = Snapshot();
        TakeOverFromTitle();
        NotifySchedule();
        Details = new ReminderDetailsViewModel(this, page, snapshot);
    }

    internal void CloseDetails() => Details = null;

    /// <summary>
    /// Começa a mexer na data à mão a partir do que o parser entendeu — senão o painel
    /// abriria vazio com "amanhã 14h" escrito no título. O trecho continua saindo do texto.
    /// </summary>
    private void TakeOverFromTitle()
    {
        if (!UsesParsed || _parsed is not { } p) return;
        _seededFromParsed = true;
        _dateChosenByHand = true;
        _dueDate = p.Date;
        _dueTime = p.Time;
        _rule = Clone(p.Recurrence);
        _customChosen = false;
        _timeText = _dueTime?.ToString("HH:mm", Inv) ?? "";
        OnPropertyChanged(nameof(TimeText));
    }

    internal sealed record EditorSnapshot(
        string Note, bool ShowNote, DateOnly? DueDate, TimeOnly? DueTime, ReminderRecurrence? Rule, bool CustomChosen,
        bool Flagged, ReminderPriority Priority, string ListId, bool DateChosenByHand, bool SeededFromParsed);

    private EditorSnapshot Snapshot() =>
        new(_note, _showNote, _dueDate, _dueTime, Clone(_rule), _customChosen, _flagged, _priority, _listId,
            _dateChosenByHand, _seededFromParsed);

    /// <summary>"Cancelar" do painel: volta tudo a como estava quando ele abriu.</summary>
    internal void Restore(EditorSnapshot s)
    {
        _dueDate = s.DueDate;
        _dueTime = s.DueTime;
        _rule = Clone(s.Rule);
        _customChosen = s.CustomChosen;
        _dateChosenByHand = s.DateChosenByHand;
        _seededFromParsed = s.SeededFromParsed;
        _timeText = _dueTime?.ToString("HH:mm", Inv) ?? "";
        Note = s.Note;
        ShowNote = s.ShowNote;
        Flagged = s.Flagged;
        _priority = s.Priority;
        ListId = s.ListId;
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(SelectedPriority));
        NotifySchedule();
    }

    // ---------- Gravar ----------

    /// <summary>Grava no lembrete. Data/hora/repetição vindas do título saem do texto.</summary>
    public void ApplyTo(ReminderConfig reminder)
    {
        var title = _title.Trim();
        var (date, time, _) = EffectiveSchedule();

        // Trecho de data do título sai do texto se veio dele — mesmo que depois tenha sido
        // ajustado à mão (os campos começam do que o parser entendeu).
        bool fromTitle = _parsed is { Found: true } && title != _originalTitle && (UsesParsed || _seededFromParsed);
        if (fromTitle && !string.IsNullOrWhiteSpace(_parsed!.Title)) title = _parsed.Title;

        var recurrence = UsesParsed ? _parsed!.Recurrence : BuildRecurrence(date);
        if (recurrence is not null && date is null) date = _store.Today;

        bool scheduleChanged = reminder.DueDate != date || reminder.DueTime != time
                               || !SameRule(reminder.Recurrence, recurrence);
        reminder.Title = title;
        reminder.Note = string.IsNullOrWhiteSpace(_note) ? null : _note.Trim();
        reminder.DueDate = date;
        reminder.DueTime = date is null ? null : time;
        reminder.Recurrence = recurrence;
        reminder.Flagged = _flagged;
        reminder.Priority = _priority;
        reminder.ListId = _listId;
        if (scheduleChanged)
        {
            reminder.NotifiedAt = null;
            reminder.SnoozedUntil = null;
        }
    }

    // ---------- Internos ----------

    /// <summary>O título mudou e tem data/hora/repetição nele, e a data não foi escolhida à mão.</summary>
    private bool UsesParsed => _parsed is { Found: true } && !_dateChosenByHand && _title.Trim() != _originalTitle;

    private (DateOnly? Date, TimeOnly? Time, ReminderRecurrence? Rule) EffectiveSchedule() =>
        UsesParsed && _parsed is { } p ? (p.Date, p.Time, p.Recurrence) : (_dueDate, _dueTime, _rule);

    private ParsedReminder Parse(string title) => NaturalDateParser.Parse(title, _store.Now.DateTime);

    /// <summary>Qual opção da lista a regra é — o que não for uma das prontas é "Personalizado".</summary>
    private RepeatChoice ChoiceOf(ReminderRecurrence? rule, bool custom)
    {
        if (rule is null || rule.Kind == RepeatKind.None) return RepeatChoice.Never;
        if (custom || rule.Interval != 1 || rule.Until is not null || rule.Count is not null) return RepeatChoice.Custom;
        var day = (_dueDate ?? _store.Today).DayOfWeek;
        return rule.Kind switch
        {
            RepeatKind.Daily => RepeatChoice.Daily,
            RepeatKind.Monthly => RepeatChoice.Monthly,
            RepeatKind.Weekly when rule.Days.Count == 0 || (rule.Days.Count == 1 && rule.Days[0] == day) => RepeatChoice.Weekly,
            RepeatKind.Weekly when rule.Days.Distinct().Count() == 5 && Weekdays.All(rule.Days.Contains) => RepeatChoice.Weekdays,
            _ => RepeatChoice.Custom
        };
    }

    private static RepeatEnd EndOf(ReminderRecurrence? rule) =>
        rule?.Until is not null ? RepeatEnd.OnDate : rule?.Count is not null ? RepeatEnd.AfterCount : RepeatEnd.Never;

    /// <summary>Mexe na regra do Personalizado (criando uma, se não houver) e recalcula tudo que depende dela.</summary>
    private void EditCustom(Action<ReminderRecurrence> change)
    {
        TakeOverFromTitle();
        _customChosen = true;
        _rule ??= new ReminderRecurrence { Kind = RepeatKind.Weekly, Days = new() { (_dueDate ?? _store.Today).DayOfWeek } };
        change(_rule);
        _dateChosenByHand = true;
        if (_dueDate is null) _dueDate = _store.Today;
        NotifySchedule();
    }

    private void ToggleDay(DayOfWeek day, bool selected)
    {
        EditCustom(r =>
        {
            r.Days.Remove(day);
            if (selected) r.Days.Add(day);
            r.Days.Sort();
        });
    }

    private void SyncDayToggles()
    {
        var (date, _, rule) = EffectiveSchedule();
        var days = rule is { Kind: RepeatKind.Weekly }
            ? (rule.Days.Count > 0 ? rule.Days : new List<DayOfWeek> { (date ?? _store.Today).DayOfWeek })
            : new List<DayOfWeek>();
        foreach (var toggle in WeekDayToggles) toggle.Sync(days.Contains(toggle.Day));
    }

    /// <summary>
    /// A regra em edição pronta pra gravar. Mantém a âncora antiga se a data não mudou (a
    /// fase do "a cada N" não pula); senão a série recomeça na data nova.
    /// </summary>
    private ReminderRecurrence? BuildRecurrence(DateOnly? date)
    {
        if (_rule is null || _rule.Kind == RepeatKind.None) return null;
        var rule = Clone(_rule)!;
        var anchor = date ?? _store.Today;
        bool sameDate = _source?.Recurrence is not null && date == _source.DueDate;
        rule.Anchor = sameDate ? _source!.Recurrence!.Anchor ?? anchor : anchor;
        if (rule.Kind == RepeatKind.Weekly && rule.Days.Count == 0) rule.Days = new() { anchor.DayOfWeek };
        return rule;
    }

    private static ReminderRecurrence? Clone(ReminderRecurrence? r) => r is null ? null : new ReminderRecurrence
    {
        Kind = r.Kind, Days = r.Days.ToList(), Anchor = r.Anchor, Interval = r.Interval, Until = r.Until, Count = r.Count
    };

    private static bool SameRule(ReminderRecurrence? a, ReminderRecurrence? b) =>
        (a, b) switch
        {
            (null, null) => true,
            (null, _) or (_, null) => false,
            _ => a!.Kind == b!.Kind && a.Interval == b.Interval && a.Until == b.Until && a.Count == b.Count
                 && a.Days.OrderBy(d => d).SequenceEqual(b.Days.OrderBy(d => d))
        };

    private void SetQuickDate(string? choice)
    {
        var today = _store.Today;
        TakeOverFromTitle();
        switch (choice)
        {
            case "none": HasDate = false; break;
            case "today": SetDate(today); break;
            case "tomorrow": SetDate(today.AddDays(1)); break;
            case "nextweek":
                int ahead = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
                SetDate(today.AddDays(ahead == 0 ? 7 : ahead));
                break;
        }
    }

    private void SetDate(DateOnly? date)
    {
        _dueDate = date;
        _dateChosenByHand = true;
        NotifySchedule();
    }

    private void SetTime(TimeOnly? time, bool keepText = false)
    {
        _dueTime = time;
        _dateChosenByHand = true;
        if (!keepText)
        {
            _timeText = time?.ToString("HH:mm", Inv) ?? "";
            OnPropertyChanged(nameof(TimeText));
        }
        if (time is not null && _dueDate is null) _dueDate = _store.Today;
        NotifySchedule();
    }

    /// <summary>▲▼ da hora: anda de 15 em 15 minutos, dando a volta no dia.</summary>
    private void StepTime(int minutes)
    {
        TakeOverFromTitle();
        var current = _dueTime ?? new TimeOnly(Math.Min(23, _store.Now.Hour + 1), 0);
        SetTime(current.AddMinutes(minutes));
    }

    private void NotifySchedule()
    {
        OnPropertyChanged(nameof(DateChipText));
        OnPropertyChanged(nameof(HasDate));
        OnPropertyChanged(nameof(Date));
        OnPropertyChanged(nameof(HasTime));
        OnPropertyChanged(nameof(SelectedRepeat));
        OnPropertyChanged(nameof(IsCustomRepeat));
        OnPropertyChanged(nameof(IntervalText));
        OnPropertyChanged(nameof(SelectedUnit));
        OnPropertyChanged(nameof(IsWeekUnit));
        OnPropertyChanged(nameof(SelectedEnd));
        OnPropertyChanged(nameof(IsEndOnDate));
        OnPropertyChanged(nameof(IsEndAfterCount));
        OnPropertyChanged(nameof(EndDate));
        OnPropertyChanged(nameof(EndCountText));
        OnPropertyChanged(nameof(EndText));
        OnPropertyChanged(nameof(RepeatSummary));
        OnPropertyChanged(nameof(IsDateFromTitle));
        SyncDayToggles();
    }
}
