using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class NaturalDateParserTests
{
    // Quinta, 15/10/2026, 10:00.
    private static readonly DateTime Now = new(2026, 10, 15, 10, 0, 0);

    private static ParsedReminder P(string text) => NaturalDateParser.Parse(text, Now);

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    [Fact]
    public void Tomorrow_with_hour()
    {
        var r = P("Ligar pra mãe amanhã 14h");
        Assert.Equal("Ligar pra mãe", r.Title);
        Assert.Equal(D(10, 16), r.Date);
        Assert.Equal(new TimeOnly(14, 0), r.Time);
        Assert.Null(r.Recurrence);
    }

    [Fact]
    public void Weekday_with_colon_time()
    {
        var r = P("Jantar sexta 19:00");
        Assert.Equal("Jantar", r.Title);
        Assert.Equal(D(10, 16), r.Date);
        Assert.Equal(new TimeOnly(19, 0), r.Time);
    }

    [Fact]
    public void Same_weekday_is_today_until_the_time_passes()
    {
        Assert.Equal(D(10, 15), P("Reunião quinta 15h").Date);
        Assert.Equal(D(10, 22), P("Reunião quinta 9h").Date);
        Assert.Equal(D(10, 15), P("Reunião na quinta-feira").Date);
    }

    [Fact]
    public void Every_day_at_nine()
    {
        var r = P("Tomar remédio todo dia 9h");
        Assert.Equal("Tomar remédio", r.Title);
        Assert.Equal(RepeatKind.Daily, r.Recurrence!.Kind);
        Assert.Equal(new TimeOnly(9, 0), r.Time);
        // 9h de hoje já passou: começa amanhã.
        Assert.Equal(D(10, 16), r.Date);
    }

    [Fact]
    public void Every_monday_and_wednesday()
    {
        var r = P("Academia toda segunda e quarta 8h");
        Assert.Equal("Academia", r.Title);
        Assert.Equal(RepeatKind.Weekly, r.Recurrence!.Kind);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }, r.Recurrence.Days);
        Assert.Equal(D(10, 19), r.Date);
    }

    [Fact]
    public void Plural_weekly_form()
    {
        var r = P("Feira todos os sábados");
        Assert.Equal("Feira", r.Title);
        Assert.Equal(new[] { DayOfWeek.Saturday }, r.Recurrence!.Days);
        Assert.Equal(D(10, 17), r.Date);
        Assert.Null(r.Time);
    }

    [Fact]
    public void Every_day_ten_is_monthly()
    {
        var r = P("Pagar aluguel todo dia 10");
        Assert.Equal("Pagar aluguel", r.Title);
        Assert.Equal(RepeatKind.Monthly, r.Recurrence!.Kind);
        Assert.Equal(D(11, 10), r.Date);
        Assert.Equal(10, r.Recurrence.Anchor!.Value.Day);
    }

    [Fact]
    public void Day_of_month_with_hour()
    {
        var r = P("Dentista dia 20 às 10h");
        Assert.Equal("Dentista", r.Title);
        Assert.Equal(D(10, 20), r.Date);
        Assert.Equal(new TimeOnly(10, 0), r.Time);
    }

    [Fact]
    public void Past_day_of_month_goes_to_next_month()
    {
        Assert.Equal(D(11, 5), P("Revisão dia 5").Date);
    }

    [Fact]
    public void Slash_date_without_year_in_the_past_is_next_year()
    {
        Assert.Equal(D(3, 2, 2027), P("Renovar seguro 02/03").Date);
        Assert.Equal(D(12, 25), P("Natal 25/12").Date);
    }

    [Fact]
    public void Only_time_is_today_or_tomorrow()
    {
        Assert.Equal(D(10, 15), P("Café 15h30").Date);
        Assert.Equal(new TimeOnly(15, 30), P("Café 15h30").Time);
        Assert.Equal(D(10, 16), P("Café 8h").Date);
    }

    [Fact]
    public void Noon_and_after_tomorrow()
    {
        var r = P("Almoço depois de amanhã ao meio-dia");
        Assert.Equal("Almoço", r.Title);
        Assert.Equal(D(10, 17), r.Date);
        Assert.Equal(new TimeOnly(12, 0), r.Time);
    }

    [Fact]
    public void Next_week_is_next_monday()
    {
        Assert.Equal(D(10, 19), P("Planejar semana que vem").Date);
    }

    [Theory]
    [InlineData("Comprar 2 pães")]
    [InlineData("Ligar às 3")]
    [InlineData("Ver hojeiro")]
    [InlineData("Terminar o relatório")]
    public void Leaves_ordinary_text_alone(string text)
    {
        var r = P(text);
        Assert.False(r.Found);
        Assert.Equal(text, r.Title);
    }

    [Fact]
    public void Invalid_hour_is_ignored()
    {
        Assert.Null(P("Treino 25h").Time);
    }

    [Fact]
    public void Accents_and_case_do_not_matter()
    {
        var r = P("REUNIÃO AMANHA AS 9H");
        Assert.Equal("REUNIÃO", r.Title);
        Assert.Equal(D(10, 16), r.Date);
        Assert.Equal(new TimeOnly(9, 0), r.Time);
    }
}
