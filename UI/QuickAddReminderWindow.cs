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
    private readonly EffectiveStyle _style;
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
    private readonly Expander _moreOptions;
    private readonly RadioButton _recurOnce;
    private readonly RadioButton _recurDaily;
    private readonly RadioButton _recurWeekly;
    private readonly WrapPanel _daysPanel;
    private readonly Dictionary<DayOfWeek, ToggleButton> _dayToggles = new();
    private readonly StackPanel _rangePanel;
    private readonly DatePicker _startDatePicker;
    private readonly DatePicker _endDatePicker;
    private readonly CheckBox _playSoundCheck;

    private readonly ReminderConfig? _editing;
    private bool _closing;
    private bool _everActivated;

    public event Action<ReminderConfig>? Saved;

    /// <summary>
    /// <paramref name="editing"/> nulo cria um lembrete novo; não-nulo pré-preenche os
    /// campos com os valores dele e o Save() atualiza esse mesmo objeto (mesma
    /// referência) em vez de criar outro.
    /// </summary>
    public QuickAddReminderWindow(EffectiveStyle style, ReminderConfig? editing = null)
    {
        _style = style;
        _editing = editing;

        Title = editing is null ? Strings.ReminderNewTitle : Strings.ReminderEditTitle;
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

        _textBox = new TextBox
        {
            FontSize = 13,
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CaretBrush = FrozenBrush(style.Foreground),
            Foreground = FrozenBrush(style.Foreground),
            FontFamily = new FontFamily(style.FontFamily),
            Text = editing?.Text ?? ""
        };

        _chipNone = CreateChip(Strings.ChipNone, isChecked: true);
        _chipPlus15 = CreateChip(Strings.ChipPlus15);
        _chipPlus1h = CreateChip(Strings.ChipPlus1h);
        _chipToday18 = CreateChip(Strings.ChipToday18);
        _chipTomorrow9 = CreateChip(Strings.ChipTomorrow9);
        _chipCustom = CreateChip(Strings.ChipCustom);

        var chipsPanel = new WrapPanel();
        foreach (var chip in AllChips)
        {
            chip.Checked += OnChipChecked;
            chipsPanel.Children.Add(chip);
        }

        _customDate = new DatePicker
        {
            SelectedDate = DateTime.Today,
            Width = 120,
            FontSize = 12,
            Foreground = Brushes.Black,
            Background = Brushes.White
        };
        _customTime = new TextBox
        {
            Text = DateTime.Now.AddHours(1).ToString("HH:mm"),
            Width = 50,
            FontSize = 12,
            Margin = new Thickness(4, 0, 0, 0),
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.Black,
            Background = Brushes.White
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

        _startDatePicker = new DatePicker { Width = 104, FontSize = 11, Foreground = Brushes.Black, Background = Brushes.White };
        _endDatePicker = new DatePicker { Width = 104, FontSize = 11, Foreground = Brushes.Black, Background = Brushes.White };
        var startLabel = CreateLabel(Strings.RangeFrom);
        startLabel.VerticalAlignment = VerticalAlignment.Center;
        startLabel.Margin = new Thickness(0, 0, 4, 0);
        var endLabel = CreateLabel(Strings.RangeTo);
        endLabel.VerticalAlignment = VerticalAlignment.Center;
        endLabel.Margin = new Thickness(8, 0, 4, 0);
        _rangePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _rangePanel.Children.Add(startLabel);
        _rangePanel.Children.Add(_startDatePicker);
        _rangePanel.Children.Add(endLabel);
        _rangePanel.Children.Add(_endDatePicker);

        // RadioButton/CheckBox/Expander: Content = string direto pega o Foreground
        // padrão do tema Fluent (preto), não o nosso — mesmo problema dos ícones dos
        // outros widgets. Um TextBlock à parte com Foreground seu resolve.
        _recurOnce = new RadioButton { Content = CreateLabel(Strings.RecurOnce), GroupName = "Recur", IsChecked = true, Margin = new Thickness(0, 0, 10, 0) };
        _recurDaily = new RadioButton { Content = CreateLabel(Strings.RecurDaily), GroupName = "Recur", Margin = new Thickness(0, 0, 10, 0) };
        _recurWeekly = new RadioButton { Content = CreateLabel(Strings.RecurWeekly), GroupName = "Recur" };
        _recurWeekly.Checked += (_, _) => { _daysPanel.Visibility = Visibility.Visible; _rangePanel.Visibility = Visibility.Visible; };
        _recurOnce.Checked += (_, _) => { _daysPanel.Visibility = Visibility.Collapsed; _rangePanel.Visibility = Visibility.Collapsed; };
        _recurDaily.Checked += (_, _) => { _daysPanel.Visibility = Visibility.Collapsed; _rangePanel.Visibility = Visibility.Visible; };
        var recurPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        recurPanel.Children.Add(_recurOnce);
        recurPanel.Children.Add(_recurDaily);
        recurPanel.Children.Add(_recurWeekly);

        _playSoundCheck = new CheckBox { Content = CreateLabel(Strings.PlaySound), Margin = new Thickness(0, 8, 0, 0) };

        var moreOptionsContent = new StackPanel();
        moreOptionsContent.Children.Add(recurPanel);
        moreOptionsContent.Children.Add(_daysPanel);
        moreOptionsContent.Children.Add(_rangePanel);
        moreOptionsContent.Children.Add(_playSoundCheck);

        _moreOptions = new Expander
        {
            Header = CreateLabel(Strings.MoreOptions),
            IsExpanded = false,
            Margin = new Thickness(0, 8, 0, 0),
            Content = moreOptionsContent,
            Foreground = FrozenBrush(style.Foreground)
        };

        var root = new StackPanel { Width = 260 };
        root.Children.Add(_textBox);
        root.Children.Add(chipsPanel);
        root.Children.Add(_customPanel);
        root.Children.Add(_moreOptions);

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
        // Aberta por clique num botão da barra (WS_EX_NOACTIVATE) ou pelo hotkey global,
        // sem o processo estar em primeiro plano — o Windows às vezes nega a ativação
        // (foreground lock) e dispara um Deactivated espúrio antes do usuário digitar
        // qualquer coisa. Sem essa guarda, isso caía direto em Save() com o texto vazio,
        // que fecha a janela — o popup "não abria" (fechava sozinho no mesmo instante).
        Activated += (_, _) => _everActivated = true;
        // Salva (não só fecha) ao perder o foco: o calendário do DatePicker é um popup à
        // parte, e clicar nele já dispara Deactivated na janela — sem isso, escolher uma
        // data e clicar nela perdia a edição inteira sem salvar nada. Esc continua
        // cancelando de verdade (Close() direto, sem passar por Save()).
        Deactivated += (_, _) => { if (!_closing && _everActivated) Save(); };
        // KeyDown (bubble) + handledEventsToo: Enter precisa chegar primeiro no
        // DatePicker em foco pra ele confirmar a data digitada/selecionada —
        // interceptar antes disso (Preview/tunneling) fazia o Save() ler o valor antigo
        // do campo. handledEventsToo garante que a gente ainda recebe o Enter mesmo se o
        // DatePicker já tiver marcado o evento como tratado ao confirmar a data.
        AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown), handledEventsToo: true);
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            Keyboard.Focus(_textBox);
            _textBox.SelectAll();
        };

        if (editing is not null) Prefill(editing);
    }

    /// <summary>Marca o chip/opções que correspondem ao lembrete existente, editando em vez de criar do zero.</summary>
    private void Prefill(ReminderConfig reminder)
    {
        if (reminder.Kind == ReminderKind.Fixed)
        {
            _chipNone.IsChecked = true;
            return;
        }

        _chipCustom.IsChecked = true;
        if (!string.IsNullOrWhiteSpace(reminder.Time)) _customTime.Text = reminder.Time;
        _customDate.SelectedDate = reminder.Date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;

        if (reminder.Recurrence == ReminderRecurrence.Daily)
        {
            _recurDaily.IsChecked = true;
        }
        else if (reminder.Recurrence == ReminderRecurrence.Weekly)
        {
            _recurWeekly.IsChecked = true;
            foreach (var day in reminder.Days)
                if (_dayToggles.TryGetValue(day, out var toggle)) toggle.IsChecked = true;
        }

        if (reminder.Recurrence != ReminderRecurrence.Once)
        {
            _startDatePicker.SelectedDate = reminder.StartDate?.ToDateTime(TimeOnly.MinValue);
            _endDatePicker.SelectedDate = reminder.EndDate?.ToDateTime(TimeOnly.MinValue);
        }

        _playSoundCheck.IsChecked = reminder.PlaySound;
        _moreOptions.IsExpanded = true;
    }

    private static string DayLabel(DayOfWeek day) => Strings.DayLabel(day);

    private void OnChipChecked(object sender, RoutedEventArgs e)
    {
        foreach (var chip in AllChips)
        {
            if (!ReferenceEquals(chip, sender)) chip.IsChecked = false;
        }
        _customPanel.Visibility = _chipCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            Save();
        }
    }

    private void Save()
    {
        // O DatePicker só converte o texto digitado em SelectedDate quando o próprio
        // controle perde o foco — desativar a janela inteira (Alt+Tab, clicar fora) não
        // dispara isso, então uma data digitada e nunca "confirmada" com Tab/clique em
        // outro campo se perdia mesmo com o Deactivated chamando Save(). Tirar o foco
        // força esse commit antes de ler _startDatePicker/_endDatePicker abaixo.
        Keyboard.ClearFocus();

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

        // Editando: atualiza o mesmo objeto (mesma referência, já está em _cfg.Reminders)
        // em vez de criar outro — e mantém o CreatedAt original.
        var reminder = _editing ?? new ReminderConfig { CreatedAt = now };
        reminder.Text = text;
        reminder.PlaySound = _playSoundCheck.IsChecked == true;
        // Reativa um "Once" que já tinha disparado/sido concluído — editar implica
        // "quero isso de novo", não deixar preso no estado antigo.
        reminder.Completed = false;

        if (_chipNone.IsChecked == true)
        {
            reminder.Kind = ReminderKind.Fixed;
            reminder.Time = null;
            reminder.Date = null;
            reminder.Recurrence = ReminderRecurrence.Once;
            reminder.Days = new();
            reminder.StartDate = null;
            reminder.EndDate = null;
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
        reminder.Date = null;
        reminder.Days = new();
        reminder.StartDate = null;
        reminder.EndDate = null;

        if (_recurDaily.IsChecked == true)
        {
            reminder.Recurrence = ReminderRecurrence.Daily;
            reminder.StartDate = _startDatePicker.SelectedDate is { } s ? DateOnly.FromDateTime(s) : null;
            reminder.EndDate = _endDatePicker.SelectedDate is { } e ? DateOnly.FromDateTime(e) : null;
        }
        else if (_recurWeekly.IsChecked == true)
        {
            reminder.Recurrence = ReminderRecurrence.Weekly;
            reminder.Days = _dayToggles.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            reminder.StartDate = _startDatePicker.SelectedDate is { } s ? DateOnly.FromDateTime(s) : null;
            reminder.EndDate = _endDatePicker.SelectedDate is { } e ? DateOnly.FromDateTime(e) : null;
        }
        else
        {
            reminder.Recurrence = ReminderRecurrence.Once;
            reminder.Date = DateOnly.FromDateTime(target);
        }

        // Fim antes do início nunca dispararia — deixa a janela aberta pra corrigir
        // em vez de salvar um lembrete que nunca vai funcionar.
        if (reminder.StartDate is { } rs && reminder.EndDate is { } re && re < rs) return null;

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

    /// <summary>
    /// Chip com fundo próprio (não só texto solto): sem isso, num tema escuro a
    /// combinação "sem bg + Foreground do tema Fluent ignorado" deixa o texto
    /// praticamente invisível. Fundo translúcido do Foreground quando solto, cor de
    /// destaque sólida com texto branco quando marcado — contraste garantido nos dois
    /// estados, em qualquer tema.
    /// </summary>
    private ToggleButton CreateChip(string label, bool isChecked = false)
    {
        var normalFg = FrozenBrush(_style.Foreground);
        var normalBg = FrozenBrush(Color.FromArgb(0x2A, _style.Foreground.R, _style.Foreground.G, _style.Foreground.B));
        var checkedBg = FrozenBrush(_style.Accent);
        var checkedFg = FrozenBrush(Colors.White);

        var text = new TextBlock { Text = label, Foreground = isChecked ? checkedFg : normalFg };
        TextOptions.SetTextRenderingMode(text, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);

        var chip = new ToggleButton
        {
            Content = text,
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 4),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
            IsChecked = isChecked,
            Background = isChecked ? checkedBg : normalBg
        };
        chip.Checked += (_, _) => { chip.Background = checkedBg; text.Foreground = checkedFg; };
        chip.Unchecked += (_, _) => { chip.Background = normalBg; text.Foreground = normalFg; };

        return chip;
    }

    private TextBlock CreateLabel(string text)
    {
        var label = new TextBlock { Text = text, Foreground = FrozenBrush(_style.Foreground) };
        TextOptions.SetTextRenderingMode(label, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);
        return label;
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
