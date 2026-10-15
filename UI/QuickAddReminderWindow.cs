using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;

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
            FontFamily = style.FontFamily,
            Text = editing?.Title ?? ""
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

        var moreOptionsContent = new StackPanel();
        moreOptionsContent.Children.Add(recurPanel);
        moreOptionsContent.Children.Add(_daysPanel);
        moreOptionsContent.Children.Add(_rangePanel);

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
        if (reminder.DueDate is not { } date)
        {
            _chipNone.IsChecked = true;
            return;
        }

        _chipCustom.IsChecked = true;
        if (reminder.DueTime is { } time) _customTime.Text = time.ToString("HH:mm");
        _customDate.SelectedDate = date.ToDateTime(TimeOnly.MinValue);

        switch (reminder.Recurrence?.Kind)
        {
            case RepeatKind.Daily:
                _recurDaily.IsChecked = true;
                break;
            case RepeatKind.Weekly:
                _recurWeekly.IsChecked = true;
                foreach (var day in reminder.Recurrence.Days)
                    if (_dayToggles.TryGetValue(day, out var toggle)) toggle.IsChecked = true;
                break;
        }

        if (reminder.Repeats)
        {
            _startDatePicker.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
            _endDatePicker.SelectedDate = reminder.Recurrence!.Until?.ToDateTime(TimeOnly.MinValue);
            _moreOptions.IsExpanded = true;
        }
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
        reminder.Title = text;
        // Reativa um lembrete que já tinha disparado/sido concluído — editar implica
        // "quero isso de novo", não deixar preso no estado antigo.
        reminder.CompletedAt = null;
        reminder.SnoozedUntil = null;
        reminder.NotifiedAt = null;

        if (_chipNone.IsChecked == true)
        {
            reminder.DueDate = null;
            reminder.DueTime = null;
            reminder.Recurrence = null;
            return reminder;
        }

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

        var dueTime = new TimeOnly(target.Hour, target.Minute);
        bool daily = _recurDaily.IsChecked == true;
        bool weekly = _recurWeekly.IsChecked == true;

        if (!daily && !weekly)
        {
            reminder.DueDate = DateOnly.FromDateTime(target);
            reminder.DueTime = dueTime;
            reminder.Recurrence = null;
            return reminder;
        }

        var today = DateOnly.FromDateTime(now);
        var start = _startDatePicker.SelectedDate is { } s ? DateOnly.FromDateTime(s) : today;
        if (start < today) start = today;
        var rule = new ReminderRecurrence
        {
            Kind = daily ? RepeatKind.Daily : RepeatKind.Weekly,
            Days = weekly ? _dayToggles.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList() : new(),
            Until = _endDatePicker.SelectedDate is { } e ? DateOnly.FromDateTime(e) : null
        };

        // Primeira ocorrência a partir do início — hoje só se o horário ainda não passou.
        var first = RecurrenceCalculator.FirstOnOrAfter(rule, start, start);
        if (first == today && dueTime <= TimeOnly.FromDateTime(now)) first = RecurrenceCalculator.NextDate(rule, start, today);

        // Fim antes da primeira ocorrência nunca dispararia — deixa a janela aberta pra
        // corrigir em vez de salvar um lembrete que nunca vai funcionar.
        if (first is not { } firstDate) return null;

        rule.Anchor = firstDate;
        reminder.DueDate = firstDate;
        reminder.DueTime = dueTime;
        reminder.Recurrence = rule;
        return reminder;
    }

    /// <summary>Posiciona a janela ancorada à barra, no lado oposto à borda configurada.</summary>
    public void AnchorTo(FrameworkElement anchor, BarEdge edge) => PopupPlacement.AnchorTo(this, anchor, edge);

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
