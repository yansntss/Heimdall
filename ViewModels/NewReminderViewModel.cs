using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;

namespace Heimdall.ViewModels;

/// <summary>Uma lista na página "Lista" do painel de detalhes.</summary>
internal sealed record ReminderListOption(string Id, string Name, Brush Brush);

/// <summary>Uma opção de repetição ("Nunca", "Diariamente"...).</summary>
internal sealed record RepeatOption(RepeatKind Kind, string Label);

/// <summary>Uma opção de prioridade ("Nenhuma"... "Alta").</summary>
internal sealed record PriorityOption(ReminderPriority Priority, string Label);

/// <summary>
/// Estado em edição de um lembrete (novo ou existente): título com o parser de linguagem
/// natural rodando ao vivo (o chip de data já mostra o que foi entendido), os chips da linha
/// e tudo que o painel de detalhes mexe. Nada vai pro store antes de <see cref="ApplyTo"/>
/// — que só acontece no Enter, no "Adicionar" ou num clique fora.
/// </summary>
internal sealed class NewReminderViewModel : ObservableObject
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly ReminderStore _store;
    private readonly ReminderConfig? _source;
    private readonly string _originalTitle;

    private string _title;
    private string _note;
    private bool _showNote;
    private DateOnly? _dueDate;
    private TimeOnly? _dueTime;
    private RepeatKind _repeat;
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
        _repeat = from.Recurrence?.Kind ?? RepeatKind.None;
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
            new RepeatOption(RepeatKind.None, Strings.RepeatNever),
            new RepeatOption(RepeatKind.Daily, Strings.RepeatDaily),
            new RepeatOption(RepeatKind.Weekly, Strings.RepeatWeekly),
            new RepeatOption(RepeatKind.Monthly, Strings.RepeatMonthly),
            new RepeatOption(RepeatKind.Yearly, Strings.RepeatYearly),
        };
        PriorityOptions = new[]
        {
            new PriorityOption(ReminderPriority.None, Strings.PriorityNone),
            new PriorityOption(ReminderPriority.Low, Strings.PriorityLow),
            new PriorityOption(ReminderPriority.Medium, Strings.PriorityMedium),
            new PriorityOption(ReminderPriority.High, Strings.PriorityHigh),
        };
        ListOptions = store.Lists.Select(l => new ReminderListOption(l.Id, l.Name, ReminderDisplay.ListBrush(l.Color))).ToList();
        _parsed = Parse(_title);
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
    public IReadOnlyList<PriorityOption> PriorityOptions { get; }
    public IReadOnlyList<ReminderListOption> ListOptions { get; }

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

    // ---------- Data, hora, repetição ----------

    /// <summary>O que o chip de data mostra: o que o parser entendeu do título ou a data escolhida.</summary>
    public string DateChipText
    {
        get
        {
            var (date, time, kind) = EffectiveSchedule();
            if (date is not { } d) return Strings.ChipDate;
            var text = RelativeDateFormatter.Format(d, time, _store.Today, Strings.Current);
            return kind is RepeatKind.None ? text : $"{text} · {RepeatOptions.First(o => o.Kind == kind).Label}";
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
                _repeat = RepeatKind.None;
                SetDate(null);
                OnPropertyChanged(nameof(TimeText));
                OnPropertyChanged(nameof(SelectedRepeat));
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

    public RepeatOption SelectedRepeat
    {
        get => RepeatOptions.First(o => o.Kind == EffectiveSchedule().Kind);
        set
        {
            if (value is null || value.Kind == EffectiveSchedule().Kind) return;
            TakeOverFromTitle();
            _repeat = value.Kind;
            _dateChosenByHand = true;
            if (_repeat != RepeatKind.None && _dueDate is null) SetDate(_store.Today);
            NotifySchedule();
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
        _repeat = p.Recurrence?.Kind ?? RepeatKind.None;
        _timeText = _dueTime?.ToString("HH:mm", Inv) ?? "";
        OnPropertyChanged(nameof(TimeText));
    }

    internal sealed record EditorSnapshot(
        string Note, bool ShowNote, DateOnly? DueDate, TimeOnly? DueTime, RepeatKind Repeat, bool Flagged,
        ReminderPriority Priority, string ListId, bool DateChosenByHand, bool SeededFromParsed);

    private EditorSnapshot Snapshot() =>
        new(_note, _showNote, _dueDate, _dueTime, _repeat, _flagged, _priority, _listId, _dateChosenByHand, _seededFromParsed);

    /// <summary>"Cancelar" do painel: volta tudo a como estava quando ele abriu.</summary>
    internal void Restore(EditorSnapshot s)
    {
        _dueDate = s.DueDate;
        _dueTime = s.DueTime;
        _repeat = s.Repeat;
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
        ReminderRecurrence? recurrence;

        // Trecho de data do título sai do texto se veio dele — mesmo que depois tenha sido
        // ajustado no painel (que começa do que o parser entendeu).
        bool fromTitle = _parsed is { Found: true } && title != _originalTitle && (UsesParsed || _seededFromParsed);
        if (fromTitle && !string.IsNullOrWhiteSpace(_parsed!.Title)) title = _parsed.Title;

        if (UsesParsed) recurrence = _parsed!.Recurrence;
        else if (fromTitle && _parsed!.Recurrence is { } fromText && fromText.Kind == _repeat)
        {
            recurrence = fromText;
            if (date is { } d && date != _parsed.Date) recurrence.Anchor = d;
        }
        else recurrence = BuildRecurrence(date);

        if (recurrence is not null && date is null) date = _store.Today;

        bool scheduleChanged = reminder.DueDate != date || reminder.DueTime != time
                               || reminder.Recurrence?.Kind != recurrence?.Kind;
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

    private (DateOnly? Date, TimeOnly? Time, RepeatKind Kind) EffectiveSchedule() =>
        UsesParsed && _parsed is { } p
            ? (p.Date, p.Time, p.Recurrence?.Kind ?? RepeatKind.None)
            : (_dueDate, _dueTime, _repeat);

    private ParsedReminder Parse(string title) => NaturalDateParser.Parse(title, _store.Now.DateTime);

    /// <summary>Mantém a regra original (dias, intervalo, fim) quando o tipo não mudou; senão uma nova a partir da data.</summary>
    private ReminderRecurrence? BuildRecurrence(DateOnly? date)
    {
        if (_repeat == RepeatKind.None) return null;
        var anchor = date ?? _store.Today;
        if (_source?.Recurrence is { } old && old.Kind == _repeat && date == _source.DueDate)
        {
            return new ReminderRecurrence
            {
                Kind = old.Kind, Days = old.Days.ToList(), Anchor = old.Anchor ?? anchor,
                Interval = old.Interval, Until = old.Until, Count = old.Count
            };
        }
        return new ReminderRecurrence
        {
            Kind = _repeat,
            Days = _repeat == RepeatKind.Weekly ? new List<DayOfWeek> { anchor.DayOfWeek } : new(),
            Anchor = anchor
        };
    }

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
        OnPropertyChanged(nameof(IsDateFromTitle));
    }
}
