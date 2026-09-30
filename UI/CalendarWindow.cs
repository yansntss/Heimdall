using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>
/// Calendário mensal aberto ao clicar no widget de relógio. Montado à mão em vez do
/// <see cref="Calendar"/> do WPF: o controle padrão (tema Aero) é branco/cinza fixo e
/// ignora as cores do tema da barra. Esc ou perder o foco fecha; roda do mouse troca de mês.
/// </summary>
internal sealed class CalendarWindow : Window
{
    private const string IconFontName = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const string GlyphPrev = "";
    private const string GlyphNext = "";
    private const double CellSize = 30;

    private readonly EffectiveStyle _style;
    private readonly CultureInfo _culture;
    private readonly TextBlock _monthTitle;
    private readonly UniformGrid _daysGrid;
    private DateTime _month;
    private bool _closing;
    private bool _everActivated;

    public CalendarWindow(EffectiveStyle style, CultureInfo culture)
    {
        _style = style;
        _culture = culture;
        _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        Title = Strings.CalendarTitle;
        Icon = AppIcon.Source;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        // Fora da tela até a posição final ser calculada (evita "piscar" no canto errado)
        Left = -32000;
        Top = -32000;

        _monthTitle = CreateText("", 13, FontWeights.SemiBold);
        _monthTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _monthTitle.VerticalAlignment = VerticalAlignment.Center;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var prev = CreateNavButton(GlyphPrev, -1);
        var next = CreateNavButton(GlyphNext, +1);
        DockPanel.SetDock(prev, Dock.Left);
        DockPanel.SetDock(next, Dock.Right);
        header.Children.Add(prev);
        header.Children.Add(next);
        header.Children.Add(_monthTitle);

        var weekHeader = new UniformGrid { Columns = 7 };
        var firstDay = (int)_culture.DateTimeFormat.FirstDayOfWeek;
        for (int i = 0; i < 7; i++)
        {
            var name = _culture.DateTimeFormat.GetShortestDayName((DayOfWeek)((firstDay + i) % 7));
            var label = CreateText(Capitalize(name), 11, FontWeights.Normal);
            label.Opacity = 0.6;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 0, 0, 2);
            weekHeader.Children.Add(label);
        }

        _daysGrid = new UniformGrid { Columns = 7, Rows = 6 };

        var todayLink = CreateText(Capitalize(DateTime.Today.ToString("D", _culture)), 11, FontWeights.Normal);
        todayLink.HorizontalAlignment = HorizontalAlignment.Center;
        todayLink.Margin = new Thickness(0, 6, 0, 0);
        todayLink.Cursor = Cursors.Hand;
        todayLink.ToolTip = Strings.CalendarGoToToday;
        todayLink.MouseLeftButtonUp += (_, _) => ShowMonth(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

        var root = new StackPanel { Width = CellSize * 7 };
        root.Children.Add(header);
        root.Children.Add(weekHeader);
        root.Children.Add(_daysGrid);
        root.Children.Add(todayLink);

        var border = new Border
        {
            Background = new SolidColorBrush(style.Background),
            BorderBrush = new SolidColorBrush(style.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(style.CornerRadius),
            Padding = new Thickness(10, 8, 10, 8),
            Child = root
        };
        TextElement.SetForeground(border, FrozenBrush(style.Foreground));
        Content = border;

        ShowMonth(_month);

        // Mesmas guardas do QuickAddReminderWindow: Close() reentrante via Deactivated
        // derruba o app, e aberta a partir da barra (NOACTIVATE) o Windows às vezes manda
        // um Deactivated espúrio antes da janela chegar a ser ativada.
        Closing += (_, _) => _closing = true;
        Activated += (_, _) => _everActivated = true;
        Deactivated += (_, _) => { if (!_closing && _everActivated) Close(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key is Key.Left or Key.PageUp) ShowMonth(_month.AddMonths(-1));
            else if (e.Key is Key.Right or Key.PageDown) ShowMonth(_month.AddMonths(1));
        };
        MouseWheel += (_, e) => ShowMonth(_month.AddMonths(e.Delta > 0 ? -1 : 1));
    }

    private void ShowMonth(DateTime month)
    {
        _month = month;
        _monthTitle.Text = Capitalize(month.ToString("Y", _culture));

        _daysGrid.Children.Clear();
        var firstDay = _culture.DateTimeFormat.FirstDayOfWeek;
        int offset = ((int)month.DayOfWeek - (int)firstDay + 7) % 7;
        var start = month.AddDays(-offset);
        var today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            bool isToday = date == today;
            bool inMonth = date.Month == month.Month;

            var text = CreateText(date.Day.ToString(_culture), 12, isToday ? FontWeights.SemiBold : FontWeights.Normal);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            if (isToday) text.Foreground = FrozenBrush(Colors.White);

            var cell = new Border
            {
                Width = CellSize - 2,
                Height = CellSize - 4,
                Margin = new Thickness(1, 2, 1, 2),
                CornerRadius = new CornerRadius(Math.Min(_style.CornerRadius, 6) + 2),
                Background = isToday ? FrozenBrush(_style.Accent) : Brushes.Transparent,
                Opacity = inMonth ? 1 : 0.35,
                Child = text
            };
            if (!isToday)
            {
                var hover = FrozenBrush(_style.Hover);
                cell.MouseEnter += (_, _) => cell.Background = hover;
                cell.MouseLeave += (_, _) => cell.Background = Brushes.Transparent;
            }
            _daysGrid.Children.Add(cell);
        }
    }

    private Button CreateNavButton(string glyph, int delta)
    {
        var icon = CreateText(glyph, 11, FontWeights.Normal);
        icon.FontFamily = new FontFamily(IconFontName);

        var button = new Button
        {
            Content = icon,
            Width = 26,
            Height = 22,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false
        };
        button.Click += (_, _) => ShowMonth(_month.AddMonths(delta));
        return button;
    }

    public void AnchorTo(FrameworkElement anchor, Config.BarEdge edge) => PopupPlacement.AnchorTo(this, anchor, edge);

    private TextBlock CreateText(string text, double size, FontWeight weight)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            FontFamily = new FontFamily(_style.FontFamily),
            Foreground = FrozenBrush(_style.Foreground)
        };
        TextOptions.SetTextRenderingMode(block, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
        return block;
    }

    private string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], _culture) + s[1..];

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
