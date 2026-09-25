using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>
/// Popup de adição rápida de lembrete, ancorado à barra. Ao contrário da BarWindow
/// (NOACTIVATE), essa janela recebe foco de verdade — Enter salva, Esc fecha, perder
/// o foco fecha.
/// </summary>
internal sealed class QuickAddReminderWindow : Window
{
    private readonly TextBox _textBox;

    // Chips de tempo (só um selecionado por vez)
    private readonly ToggleButton _chipNone;
    private readonly ToggleButton _chipPlus15;
    private readonly ToggleButton _chipPlus1h;
    private readonly ToggleButton _chipToday18;
    private readonly ToggleButton _chipTomorrow9;
    private readonly ToggleButton _chipCustom;
    private List<ToggleButton> AllChips => new() { _chipNone, _chipPlus15, _chipPlus1h, _chipToday18, _chipTomorrow9, _chipCustom };

    // "Personalizado"
    private readonly StackPanel _customPanel;
    private readonly DatePicker _customDate;
    private readonly TextBox _customTime;

    // "Mais opções"
    private readonly RadioButton _recurOnce;
    private readonly RadioButton _recurDaily;
    private readonly RadioButton _recurWeekly;
    private readonly WrapPanel _daysPanel;
    private readonly Dictionary<DayOfWeek, ToggleButton> _dayToggles = new();
    private readonly CheckBox _playSoundCheck;

    private bool _closing;

    public event Action<ReminderConfig>? Saved;

    public QuickAddReminderWindow(EffectiveStyle style)
    {
        Title = "Heimdall — Novo lembrete";
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

        _textBox = new TextBox
        {
            FontSize = 13,
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CaretBrush = FrozenBrush(style.Foreground),
            Foreground = FrozenBrush(style.Foreground),
            FontFamily = new FontFamily(style.FontFamily)
        };

        _chipNone = CreateChip("Sem horário", isChecked: true);
        _chipPlus15 = CreateChip("+15 min");
        _chipPlus1h = CreateChip("+1 h");
        _chipToday18 = CreateChip("Hoje 18h");
        _chipTomorrow9 = CreateChip("Amanhã 9h");
        _chipCustom = CreateChip("Personalizado");

        var chipsPanel = new WrapPanel();
        foreach (var chip in AllChips)
        {
            chip.Checked += OnChipChecked;
            chipsPanel.Children.Add(chip);
        }

        _customDate = new DatePicker { SelectedDate = DateTime.Today, Width = 120, FontSize = 12 };
        _customTime = new TextBox
        {
            Text = DateTime.Now.AddHours(1).ToString("HH:mm"),
            Width = 50,
            FontSize = 12,
            Margin = new Thickness(4, 0, 0, 0),
            TextAlignment = TextAlignment.Center
        };
        _customPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _customPanel.Children.Add(_customDate);
        _customPanel.Children.Add(_customTime);

        _daysPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
        {
            var dayToggle = CreateChip(DayLabel(day));
            _dayToggles[day] = dayToggle;
            _daysPanel.Children.Add(dayToggle);
        }

        _recurOnce = new RadioButton { Content = "Única", GroupName = "Recur", IsChecked = true, Margin = new Thickness(0, 0, 10, 0) };
        _recurDaily = new RadioButton { Content = "Diária", GroupName = "Recur", Margin = new Thickness(0, 0, 10, 0) };
        _recurWeekly = new RadioButton { Content = "Dias da semana", GroupName = "Recur" };
        _recurWeekly.Checked += (_, _) => _daysPanel.Visibility = Visibility.Visible;
        _recurOnce.Checked += (_, _) => _daysPanel.Visibility = Visibility.Collapsed;
        _recurDaily.Checked += (_, _) => _daysPanel.Visibility = Visibility.Collapsed;
        var recurPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        recurPanel.Children.Add(_recurOnce);
        recurPanel.Children.Add(_recurDaily);
        recurPanel.Children.Add(_recurWeekly);

        _playSoundCheck = new CheckBox { Content = "Tocar som", Margin = new Thickness(0, 8, 0, 0) };

        var moreOptionsContent = new StackPanel();
        moreOptionsContent.Children.Add(recurPanel);
        moreOptionsContent.Children.Add(_daysPanel);
        moreOptionsContent.Children.Add(_playSoundCheck);

        var expander = new Expander
        {
            Header = "Mais opções",
            IsExpanded = false,
            Margin = new Thickness(0, 8, 0, 0),
            Content = moreOptionsContent,
            Foreground = FrozenBrush(style.Foreground)
        };

        var root = new StackPanel { Width = 260 };
        root.Children.Add(_textBox);
        root.Children.Add(chipsPanel);
        root.Children.Add(_customPanel);
        root.Children.Add(expander);

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

        // Fechar já dispara Deactivated de novo como parte do próprio fechamento — sem a
        // guarda, isso chama Close() reentrante e o WPF derruba o app (VerifyNotClosing).
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => { if (!_closing) Close(); };
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            Keyboard.Focus(_textBox);
        };
    }

    private static string DayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Seg",
        DayOfWeek.Tuesday => "Ter",
        DayOfWeek.Wednesday => "Qua",
        DayOfWeek.Thursday => "Qui",
        DayOfWeek.Friday => "Sex",
        DayOfWeek.Saturday => "Sáb",
        _ => "Dom"
    };

    private void OnChipChecked(object sender, RoutedEventArgs e)
    {
        foreach (var chip in AllChips)
        {
            if (!ReferenceEquals(chip, sender)) chip.IsChecked = false;
        }
        _customPanel.Visibility = _chipCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && e.OriginalSource is not DatePicker)
        {
            e.Handled = true;
            Save();
        }
    }

    private void Save()
    {
        var text = _textBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            Close();
            return;
        }

        var reminder = BuildReminder(text);
        if (reminder is null) return; // horário personalizado inválido — deixa a janela aberta pra corrigir

        Saved?.Invoke(reminder);
        Close();
    }

    private ReminderConfig? BuildReminder(string text)
    {
        var now = DateTime.Now;
        var reminder = new ReminderConfig
        {
            Text = text,
            CreatedAt = now,
            PlaySound = _playSoundCheck.IsChecked == true
        };

        if (_chipNone.IsChecked == true)
        {
            reminder.Kind = ReminderKind.Fixed;
            return reminder;
        }

        reminder.Kind = ReminderKind.Scheduled;

        DateTime target;
        if (_chipPlus15.IsChecked == true) target = now.AddMinutes(15);
        else if (_chipPlus1h.IsChecked == true) target = now.AddHours(1);
        else if (_chipToday18.IsChecked == true) target = now.Date.AddHours(18);
        else if (_chipTomorrow9.IsChecked == true) target = now.Date.AddDays(1).AddHours(9);
        else // _chipCustom
        {
            if (!TimeSpan.TryParse(_customTime.Text, out var time)) return null;
            target = (_customDate.SelectedDate ?? now.Date).Date + time;
        }

        reminder.Time = target.ToString("HH:mm");

        if (_recurDaily.IsChecked == true)
        {
            reminder.Recurrence = ReminderRecurrence.Daily;
        }
        else if (_recurWeekly.IsChecked == true)
        {
            reminder.Recurrence = ReminderRecurrence.Weekly;
            reminder.Days = _dayToggles.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
        }
        else
        {
            reminder.Recurrence = ReminderRecurrence.Once;
            reminder.Date = DateOnly.FromDateTime(target);
        }

        return reminder;
    }

    /// <summary>Posiciona a janela ancorada à barra, no lado oposto à borda configurada.</summary>
    public void AnchorTo(FrameworkElement anchor, BarEdge edge)
    {
        Loaded += (_, _) =>
        {
            // X/Y vêm do widget (abre perto de onde ele está na barra), mas a extensão
            // vertical/horizontal vem da JANELA da barra inteira — usar só o
            // ActualHeight/Width do widget deixaria o popup uns pixels dentro da barra
            // (o widget é menor que a espessura total, fica centralizado nela).
            var window = Window.GetWindow(anchor);
            var anchorTopLeft = anchor.PointToScreen(new Point(0, 0));
            var windowTopLeft = window?.PointToScreen(new Point(0, 0)) ?? anchorTopLeft;
            double windowWidth = window?.ActualWidth ?? anchor.ActualWidth;
            double windowHeight = window?.ActualHeight ?? anchor.ActualHeight;

            const double gap = 4;
            switch (edge)
            {
                case BarEdge.Bottom:
                    Left = anchorTopLeft.X;
                    Top = windowTopLeft.Y - ActualHeight - gap;
                    break;
                case BarEdge.Left:
                    Left = windowTopLeft.X + windowWidth + gap;
                    Top = anchorTopLeft.Y;
                    break;
                case BarEdge.Right:
                    Left = windowTopLeft.X - ActualWidth - gap;
                    Top = anchorTopLeft.Y;
                    break;
                default: // Top
                    Left = anchorTopLeft.X;
                    Top = windowTopLeft.Y + windowHeight + gap;
                    break;
            }

            // O widget de lembretes normalmente fica perto de uma ponta da barra — sem
            // isso, o popup passava da borda do monitor (renderiza, mas fica invisível,
            // fora de qualquer tela física).
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)anchorTopLeft.X, (int)anchorTopLeft.Y));
            var screenBounds = screen.Bounds;
            Left = Math.Max(screenBounds.Left, Math.Min(Left, screenBounds.Right - ActualWidth));
            Top = Math.Max(screenBounds.Top, Math.Min(Top, screenBounds.Bottom - ActualHeight));
        };
    }

    private static ToggleButton CreateChip(string label, bool isChecked = false) => new()
    {
        Content = label,
        FontSize = 11,
        Padding = new Thickness(8, 3, 8, 3),
        Margin = new Thickness(0, 0, 4, 4),
        Cursor = Cursors.Hand,
        IsChecked = isChecked
    };

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
