using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Heimdall.Services;

namespace Heimdall.ViewModels;

/// <summary>Uma célula da grade do calendário.</summary>
internal sealed record CalendarDayViewModel(
    DateOnly Date, string Number, bool IsCurrentMonth, bool IsToday, bool IsSelected, string AccessibleName);

/// <summary>Cabeçalho de coluna: letra ("S") e nome completo pro leitor de tela ("Segunda").</summary>
internal sealed record WeekDayHeader(string Letter, string Name);

/// <summary>Sugestão rápida acima da grade (Hoje, Amanhã, Próxima segunda, +1 semana).</summary>
internal sealed record DateSuggestion(string Label, DateOnly Date);

/// <summary>
/// Mês do calendário inline: 6 semanas fixas (altura estável ao trocar de mês), começando
/// no primeiro dia da semana da cultura do sistema. Nomes de mês/dia em pt-BR ou en-US
/// conforme o idioma do app.
/// </summary>
internal sealed class CalendarMonthViewModel : ObservableObject
{
    private readonly DateOnly _today;
    private readonly DayOfWeek _firstDay;
    private readonly CultureInfo _names;
    private DateOnly _month;
    private DateOnly? _selected;

    public CalendarMonthViewModel(DateOnly today, DayOfWeek firstDayOfWeek, CultureInfo names)
    {
        _today = today;
        _firstDay = firstDayOfWeek;
        _names = names;
        _month = new DateOnly(today.Year, today.Month, 1);

        WeekDays = Enumerable.Range(0, 7)
            .Select(i => (DayOfWeek)(((int)firstDayOfWeek + i) % 7))
            .Select(d => new WeekDayHeader(Capitalize(names.DateTimeFormat.GetDayName(d))[..1], DayName(d)))
            .ToList();

        Suggestions = new[]
        {
            new DateSuggestion(Strings.SmartListToday, today),
            new DateSuggestion(Strings.QuickTomorrow, today.AddDays(1)),
            new DateSuggestion(Strings.SuggestNextMonday, NextMonday(today)),
            new DateSuggestion(Strings.SuggestPlusWeek, today.AddDays(7)),
        };

        PreviousMonthCommand = new RelayCommand(() => ShowMonth(_month.AddMonths(-1)));
        NextMonthCommand = new RelayCommand(() => ShowMonth(_month.AddMonths(1)));
        SelectCommand = new RelayCommand(p =>
        {
            switch (p)
            {
                case CalendarDayViewModel day: Selected = day.Date; break;
                case DateSuggestion suggestion: Selected = suggestion.Date; break;
            }
        });
        Rebuild();
    }

    /// <summary>Dia escolhido (clique num dia ou numa sugestão).</summary>
    public event Action<DateOnly?>? SelectedChanged;

    public IReadOnlyList<WeekDayHeader> WeekDays { get; }

    public IReadOnlyList<DateSuggestion> Suggestions { get; }

    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();

    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }

    /// <summary>Parâmetro: <see cref="CalendarDayViewModel"/> ou <see cref="DateSuggestion"/>.</summary>
    public ICommand SelectCommand { get; }

    public DateOnly Month => _month;

    /// <summary>"Outubro 2026".</summary>
    public string Title => $"{Capitalize(_names.DateTimeFormat.GetMonthName(_month.Month))} {_month.Year}";

    public DateOnly? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            OnPropertyChanged();
            // Escolher um dia de outro mês (ou uma sugestão) leva o calendário até ele.
            if (value is { } date && (date.Year != _month.Year || date.Month != _month.Month)) _month = new DateOnly(date.Year, date.Month, 1);
            Rebuild();
            SelectedChanged?.Invoke(value);
        }
    }

    public void ShowMonth(DateOnly anyDayOfMonth)
    {
        _month = new DateOnly(anyDayOfMonth.Year, anyDayOfMonth.Month, 1);
        Rebuild();
    }

    private void Rebuild()
    {
        int offset = ((int)_month.DayOfWeek - (int)_firstDay + 7) % 7;
        var start = _month.AddDays(-offset);

        Days.Clear();
        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            Days.Add(new CalendarDayViewModel(
                date,
                date.Day.ToString(CultureInfo.InvariantCulture),
                IsCurrentMonth: date.Month == _month.Month,
                IsToday: date == _today,
                IsSelected: date == _selected,
                AccessibleName: $"{DayName(date.DayOfWeek)}, {date.ToString(LongDate, _names)}"));
        }

        OnPropertyChanged(nameof(Month));
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>"15 de outubro de 2026" / "October 15, 2026" — o dia da semana vai à parte, sem o "-feira".</summary>
    private string LongDate => _names.TwoLetterISOLanguageName == "pt" ? "d 'de' MMMM 'de' yyyy" : "MMMM d, yyyy";

    /// <summary>"Segunda" (pt: sem o "-feira").</summary>
    private string DayName(DayOfWeek day)
    {
        var name = Capitalize(_names.DateTimeFormat.GetDayName(day));
        int dash = name.IndexOf('-');
        return dash > 0 ? name[..dash] : name;
    }

    private string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], _names) + text[1..];

    private static DateOnly NextMonday(DateOnly today)
    {
        int ahead = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(ahead == 0 ? 7 : ahead);
    }
}
