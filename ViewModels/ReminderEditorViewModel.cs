using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;

namespace Heimdall.ViewModels;

/// <summary>Uma lista no popup do chip de lista.</summary>
internal sealed record ReminderListOption(string Id, string Name, Brush Brush, bool IsCurrent);

/// <summary>Uma opção de repetição no popup do chip de data.</summary>
internal sealed record RepeatOption(RepeatKind Kind, string Label);

/// <summary>
/// Edição inline de um lembrete (novo ou existente): título com o parser de linguagem
/// natural rodando ao vivo (o chip de data já mostra o que foi entendido) e a barra de
/// chips — data, bandeira, nota e lista. Só grava no lembrete em <see cref="ApplyTo"/>.
/// </summary>
internal sealed class ReminderEditorViewModel : ObservableObject
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
    private string _listId;
    private string _dateText = "";
    private string _timeText = "";
    private bool _isDatePopupOpen;
    private bool _isListPopupOpen;
    private bool _dateChosenByHand;
    private bool _seededFromParsed;
    private ParsedReminder? _parsed;

    /// <param name="source">Nulo = lembrete novo, com os padrões de <paramref name="defaults"/>.</param>
    public ReminderEditorViewModel(ReminderStore store, ReminderConfig? source, ReminderConfig? defaults,
        Action submit, Action cancel)
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
        _listId = store.ListOf(from).Id;
        SyncDateFields();

        SubmitCommand = new RelayCommand(submit);
        CancelCommand = new RelayCommand(cancel);
        ToggleFlagCommand = new RelayCommand(() => Flagged = !Flagged);
        ToggleNoteCommand = new RelayCommand(() => ShowNote = !ShowNote);
        OpenDatePopupCommand = new RelayCommand(() => IsDatePopupOpen = true);
        OpenListPopupCommand = new RelayCommand(() => IsListPopupOpen = true);
        ClosePopupsCommand = new RelayCommand(() => IsDatePopupOpen = IsListPopupOpen = false);
        QuickDateCommand = new RelayCommand(p => SetQuickDate(p as string));
        ChooseListCommand = new RelayCommand(p =>
        {
            if (p is string id) ListId = id;
            IsListPopupOpen = false;
        });

        RepeatOptions = new[]
        {
            new RepeatOption(RepeatKind.None, Strings.RepeatNone),
            new RepeatOption(RepeatKind.Daily, Strings.ReminderMetaDaily),
            new RepeatOption(RepeatKind.Weekly, Strings.ReminderMetaWeekly),
            new RepeatOption(RepeatKind.Monthly, Strings.ReminderMetaMonthly),
            new RepeatOption(RepeatKind.Yearly, Strings.ReminderMetaYearly),
        };
        _parsed = Parse(_title);
    }

    public bool IsNew => _source is null;

    /// <summary>Enter: salva (e abre uma linha nova).</summary>
    public ICommand SubmitCommand { get; }

    /// <summary>Esc: descarta a edição.</summary>
    public ICommand CancelCommand { get; }

    public ICommand ToggleFlagCommand { get; }
    public ICommand ToggleNoteCommand { get; }
    public ICommand OpenDatePopupCommand { get; }
    public ICommand OpenListPopupCommand { get; }
    public ICommand ClosePopupsCommand { get; }

    /// <summary>Parâmetro: "none", "today", "tomorrow", "nextweek".</summary>
    public ICommand QuickDateCommand { get; }

    /// <summary>Parâmetro: id da lista.</summary>
    public ICommand ChooseListCommand { get; }

    public IReadOnlyList<RepeatOption> RepeatOptions { get; }

    public string Title
    {
        get => _title;
        set
        {
            if (!SetProperty(ref _title, value)) return;
            _parsed = Parse(value);
            NotifyDateChip();
        }
    }

    public string Note
    {
        get => _note;
        set { if (SetProperty(ref _note, value)) OnPropertyChanged(nameof(HasNote)); }
    }

    public bool HasNote => !string.IsNullOrWhiteSpace(_note);

    public bool ShowNote
    {
        get => _showNote;
        set => SetProperty(ref _showNote, value);
    }

    public bool Flagged
    {
        get => _flagged;
        set => SetProperty(ref _flagged, value);
    }

    public string ListId
    {
        get => _listId;
        private set
        {
            if (!SetProperty(ref _listId, value)) return;
            OnPropertyChanged(nameof(ListName));
            OnPropertyChanged(nameof(ListBrush));
            OnPropertyChanged(nameof(ListOptions));
        }
    }

    private ReminderListConfig CurrentList => _store.Lists.FirstOrDefault(l => l.Id == _listId) ?? _store.ListOf(new ReminderConfig());

    public string ListName => CurrentList.Name;

    public Brush ListBrush => ReminderDisplay.ListBrush(CurrentList.Color);

    public IEnumerable<ReminderListOption> ListOptions =>
        _store.Lists.Select(l => new ReminderListOption(l.Id, l.Name, ReminderDisplay.ListBrush(l.Color), l.Id == _listId)).ToList();

    // ---------- Data ----------

    /// <summary>O que o chip de data mostra: o que o parser entendeu do título ou a data escolhida.</summary>
    public string DateChipText
    {
        get
        {
            var (date, time, kind) = EffectiveSchedule();
            if (date is not { } d) return Strings.ChipDate;
            var text = RelativeDateFormatter.Format(d, time, _store.Today, Strings.Current);
            var repeat = kind is RepeatKind.None ? null : RepeatOptions.First(o => o.Kind == kind).Label;
            return repeat is null ? text : $"{text} · {repeat}";
        }
    }

    public bool HasDate => EffectiveSchedule().Date is not null;

    /// <summary>A data do chip veio do título (vai sair do texto ao salvar).</summary>
    public bool IsDateFromTitle => UsesParsed;

    /// <summary>"dd/MM/aaaa" (ou "dd/MM"); vazio = sem data.</summary>
    public string DateText
    {
        get => _dateText;
        set
        {
            if (!SetProperty(ref _dateText, value)) return;
            if (string.IsNullOrWhiteSpace(value)) SetDate(null, keepText: true);
            else if (TryParseDate(value, out var date)) SetDate(date, keepText: true);
        }
    }

    /// <summary>"HH:mm"; vazio = dia todo.</summary>
    public string TimeText
    {
        get => _timeText;
        set
        {
            if (!SetProperty(ref _timeText, value)) return;
            if (string.IsNullOrWhiteSpace(value)) SetTime(null);
            else if (TimeOnly.TryParseExact(value.Trim(), new[] { "HH:mm", "H:mm", "HH", "H" }, Inv, DateTimeStyles.None, out var time)) SetTime(time);
        }
    }

    public RepeatOption SelectedRepeat
    {
        get => RepeatOptions.First(o => o.Kind == _repeat);
        set
        {
            if (value is null || value.Kind == _repeat) return;
            _repeat = value.Kind;
            _dateChosenByHand = true;
            if (_repeat != RepeatKind.None && _dueDate is null) SetDate(_store.Today, keepText: false);
            OnPropertyChanged();
            NotifyDateChip();
        }
    }

    public bool IsDatePopupOpen
    {
        get => _isDatePopupOpen;
        set
        {
            if (!SetProperty(ref _isDatePopupOpen, value) || !value) return;
            // Abriu o popup com algo entendido do título: começa dele, pra ajustar em cima.
            if (UsesParsed && _parsed is { } p)
            {
                _seededFromParsed = true;
                _dueDate = p.Date;
                _dueTime = p.Time;
                _repeat = p.Recurrence?.Kind ?? RepeatKind.None;
                SyncDateFields();
                OnPropertyChanged(nameof(SelectedRepeat));
            }
        }
    }

    public bool IsListPopupOpen
    {
        get => _isListPopupOpen;
        set => SetProperty(ref _isListPopupOpen, value);
    }

    /// <summary>Grava no lembrete. Data/hora/repetição vindas do título saem do texto.</summary>
    public void ApplyTo(ReminderConfig reminder)
    {
        var title = _title.Trim();
        var (date, time, _) = EffectiveSchedule();
        ReminderRecurrence? recurrence;

        // Trecho de data do título sai do texto se veio dele — mesmo que depois tenha sido
        // ajustado no popup (que abriu já com o que o parser entendeu).
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
        reminder.ListId = _listId;
        if (scheduleChanged)
        {
            reminder.NotifiedAt = null;
            reminder.SnoozedUntil = null;
        }
    }

    public bool IsBlank => string.IsNullOrWhiteSpace(_title);

    // ---------- Internos ----------

    /// <summary>O título mudou e tem data/hora/repetição nele, e a data não foi escolhida à mão.</summary>
    private bool UsesParsed => _parsed is { Found: true } && !_dateChosenByHand && _title.Trim() != _originalTitle;

    private (DateOnly? Date, TimeOnly? Time, RepeatKind Kind) EffectiveSchedule() =>
        UsesParsed && _parsed is { } p
            ? (p.Date, p.Time, p.Recurrence?.Kind ?? RepeatKind.None)
            : (_dueDate, _dueTime, _repeat);

    private ParsedReminder Parse(string title) => NaturalDateParser.Parse(title, _store.Now.DateTime);

    /// <summary>Mantém a regra original (dias, fim) quando o tipo não mudou; senão uma nova a partir da data.</summary>
    private ReminderRecurrence? BuildRecurrence(DateOnly? date)
    {
        if (_repeat == RepeatKind.None) return null;
        var anchor = date ?? _store.Today;
        if (_source?.Recurrence is { } old && old.Kind == _repeat && date == _source.DueDate)
        {
            return new ReminderRecurrence { Kind = old.Kind, Days = old.Days.ToList(), Anchor = old.Anchor ?? anchor, Until = old.Until };
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
        switch (choice)
        {
            case "none":
                _repeat = RepeatKind.None;
                SetDate(null, keepText: false);
                SetTime(null);
                OnPropertyChanged(nameof(SelectedRepeat));
                break;
            case "today": SetDate(today, keepText: false); break;
            case "tomorrow": SetDate(today.AddDays(1), keepText: false); break;
            case "nextweek":
                int ahead = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
                SetDate(today.AddDays(ahead == 0 ? 7 : ahead), keepText: false);
                break;
        }
        SyncDateFields();
    }

    private void SetDate(DateOnly? date, bool keepText)
    {
        _dueDate = date;
        _dateChosenByHand = true;
        if (!keepText) SyncDateFields();
        NotifyDateChip();
    }

    private void SetTime(TimeOnly? time)
    {
        _dueTime = time;
        _dateChosenByHand = true;
        if (time is not null && _dueDate is null) SetDate(_store.Today, keepText: false);
        NotifyDateChip();
    }

    private void SyncDateFields()
    {
        _dateText = _dueDate?.ToString("dd/MM/yyyy", Inv) ?? "";
        _timeText = _dueTime?.ToString("HH:mm", Inv) ?? "";
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(TimeText));
    }

    private bool TryParseDate(string text, out DateOnly date)
    {
        var value = text.Trim();
        if (DateOnly.TryParseExact(value, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" }, Inv, DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParseExact($"{value}/{_store.Today.Year}", new[] { "dd/MM/yyyy", "d/M/yyyy" }, Inv, DateTimeStyles.None, out date))
            return true;
        return false;
    }

    private void NotifyDateChip()
    {
        OnPropertyChanged(nameof(DateChipText));
        OnPropertyChanged(nameof(HasDate));
        OnPropertyChanged(nameof(IsDateFromTitle));
    }
}
