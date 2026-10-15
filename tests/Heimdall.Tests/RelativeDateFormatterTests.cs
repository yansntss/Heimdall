using Heimdall.Config;
using Heimdall.Services.Reminders;

namespace Heimdall.Tests;

public class RelativeDateFormatterTests
{
    // Quinta-feira.
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Theory]
    [InlineData(0, 17, 0, "Hoje, 17:00")]
    [InlineData(1, 9, 0, "Amanhã, 09:00")]
    [InlineData(-1, 8, 30, "Ontem, 08:30")]
    [InlineData(1, 19, 0, "Amanhã, 19:00")]
    [InlineData(2, 19, 0, "Sábado, 19:00")]
    [InlineData(6, 7, 5, "Quarta, 07:05")]
    public void Portuguese_with_time(int days, int hour, int minute, string expected)
    {
        Assert.Equal(expected, RelativeDateFormatter.Format(Today.AddDays(days), new TimeOnly(hour, minute), Today, AppLanguage.PtBr));
    }

    [Theory]
    [InlineData(0, "Hoje")]
    [InlineData(1, "Amanhã")]
    [InlineData(7, "22 out")]
    [InlineData(-2, "13 out")]
    [InlineData(80, "3 jan 2027")]
    public void Portuguese_without_time(int days, string expected)
    {
        Assert.Equal(expected, RelativeDateFormatter.Format(Today.AddDays(days), null, Today, AppLanguage.PtBr));
    }

    [Theory]
    [InlineData(0, "Today, 17:00")]
    [InlineData(1, "Tomorrow, 17:00")]
    [InlineData(-1, "Yesterday, 17:00")]
    [InlineData(1 + 1, "Saturday, 17:00")]
    [InlineData(10, "Oct 25, 17:00")]
    public void English(int days, string expected)
    {
        Assert.Equal(expected, RelativeDateFormatter.Format(Today.AddDays(days), new TimeOnly(17, 0), Today, AppLanguage.EnUs));
    }

    [Fact]
    public void Other_year_in_the_past_shows_the_year()
    {
        Assert.Equal("20 dez 2025", RelativeDateFormatter.Format(new DateOnly(2025, 12, 20), null, Today, AppLanguage.PtBr));
    }
}
