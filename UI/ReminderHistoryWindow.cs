using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>
/// Lista o histórico de lembretes concluídos (<see cref="ReminderHistoryService"/>).
/// Janela normal (não NOACTIVATE, não borderless) — mesmo padrão da SettingsWindow,
/// usa a aparência padrão do sistema em vez de tentar casar com o tema da barra.
/// </summary>
internal sealed class ReminderHistoryWindow : Window
{
    public ReminderHistoryWindow()
    {
        Title = "Heimdall — Histórico de lembretes";
        Icon = AppIcon.Source;
        Width = 420;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        var stack = new StackPanel { Margin = new Thickness(14) };
        var groups = ReminderHistoryService.ReadAll();

        if (groups.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "Nenhum lembrete concluído ainda.",
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            foreach (var (monthKey, lines) in groups)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = FormatMonth(monthKey),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, 12, 0, 6)
                });

                foreach (var line in lines)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = CleanLine(line),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 2)
                    });
                }
            }
        }

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = stack
        };
    }

    private static string FormatMonth(string monthKey)
    {
        if (DateTime.TryParseExact(monthKey, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            var label = date.ToString("MMMM 'de' yyyy", new CultureInfo("pt-BR"));
            return char.ToUpper(label[0], new CultureInfo("pt-BR")) + label[1..];
        }
        return monthKey;
    }

    private static string CleanLine(string line) =>
        line.StartsWith("- [x] ") ? "✓ " + line["- [x] ".Length..] : line;
}
