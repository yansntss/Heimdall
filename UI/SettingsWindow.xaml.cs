using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services;
using Heimdall.Widgets;

namespace Heimdall.UI;

public partial class SettingsWindow : Window
{
    private static readonly (string Id, string Icon)[] WidgetCatalog = WidgetFactory.Catalog;

    private const string GitHubUrl = "https://github.com/yansntss/Heimdall";

    private readonly AppConfig _cfg;
    private readonly ObservableCollection<ReminderRow> _reminders;
    private readonly DispatcherTimer _clockPreviewTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<ListBox> _widgetLists = new();
    private string _selectedTheme;
    private Point? _widgetDragStart;

    public SettingsWindow()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();
        _selectedTheme = _cfg.Theme;
        Title = Strings.SettingsTitleBar;
        ApplyLabels();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int preference = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        };

        // Geral
        EdgeCombo.ItemsSource = Enum.GetValues(typeof(BarEdge));
        EdgeCombo.SelectedItem = _cfg.Edge;
        ThicknessSlider.Value = _cfg.Thickness;
        FloatingModeCheck.IsChecked = _cfg.FloatingMode;
        FloatingMarginSlider.Value = _cfg.FloatingMargin;
        FloatingMarginSlider.IsEnabled = _cfg.FloatingMode;

        MonitorModeCombo.ItemsSource = Enum.GetValues(typeof(MonitorMode));

        var monitors = MonitorService.GetMonitors()
            .Select(m => new MonitorOption(m.ShortName,
                $"{m.ShortName} — {m.Bounds.Width}x{m.Bounds.Height}" + (m.IsPrimary ? " [principal]" : "")))
            .ToList();
        MonitorCombo.ItemsSource = monitors;
        MonitorCombo.DisplayMemberPath = nameof(MonitorOption.Label);
        MonitorCombo.SelectedItem = monitors.FirstOrDefault(o =>
            string.Equals(o.Device, MonitorInfo.Normalize(_cfg.MonitorDevice), StringComparison.OrdinalIgnoreCase))
            ?? monitors.FirstOrDefault();

        MonitorModeCombo.SelectedItem = _cfg.MonitorMode;
        MonitorCombo.IsEnabled = MonitorModeCombo.SelectedItem is MonitorMode.Specific;
        StartWithWindowsCheck.IsChecked = _cfg.StartWithWindows;
        GamingModeCheck.IsChecked = _cfg.GamingMode;

        LanguageCombo.ItemsSource = new[]
        {
            new LanguageOption(AppLanguage.PtBr, Strings.LanguagePtBr),
            new LanguageOption(AppLanguage.EnUs, Strings.LanguageEnUs)
        };
        LanguageCombo.DisplayMemberPath = nameof(LanguageOption.Label);
        LanguageCombo.SelectedItem = ((IEnumerable<LanguageOption>)LanguageCombo.ItemsSource)
            .FirstOrDefault(o => o.Value == _cfg.Language);

        // Aparência
        BackgroundHexBox.Text = _cfg.Style.Background;
        ForegroundHexBox.Text = _cfg.Style.Foreground;
        UpdateSwatch(BackgroundSwatch, _cfg.Style.Background);
        UpdateSwatch(ForegroundSwatch, _cfg.Style.Foreground);
        FontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        FontFamilyCombo.Text = _cfg.Style.FontFamily;
        CustomFontSizeCheck.IsChecked = _cfg.Style.FontSize is not null;
        FontSizeSlider.Value = _cfg.Style.FontSize ?? 13;
        FontSizeSlider.IsEnabled = _cfg.Style.FontSize is not null;
        BuildThemeCards();
        UpdatePreview();

        // Relógio
        ClockModeCombo.ItemsSource = Enum.GetValues(typeof(ClockMode));
        ClockModeCombo.SelectedItem = _cfg.Clock.Mode;
        ClockStyleCombo.ItemsSource = Enum.GetValues(typeof(ClockStyle));
        ClockStyleCombo.SelectedItem = _cfg.Clock.Style;
        ClockCultureBox.Text = _cfg.Clock.Culture;
        ClockCustomFormatBox.Text = _cfg.Clock.CustomFormat;
        UpdateClockModeUi();
        UpdateClockPreview();
        _clockPreviewTimer.Tick += (_, _) => UpdateClockPreview();
        _clockPreviewTimer.Start();

        // Widgets
        foreach (var entry in _cfg.Widgets.Start) StartList.Items.Add(WidgetRow.From(entry));
        foreach (var entry in _cfg.Widgets.Center) CenterList.Items.Add(WidgetRow.From(entry));
        foreach (var entry in _cfg.Widgets.End) EndList.Items.Add(WidgetRow.From(entry));
        _widgetLists.AddRange(new[] { StartList, CenterList, EndList });
        foreach (var list in _widgetLists) EnableWidgetDragDrop(list);

        // Lembretes
        KindColumn.ItemsSource = Enum.GetValues(typeof(ReminderKind));
        RecurrenceColumn.ItemsSource = Enum.GetValues(typeof(ReminderRecurrence));
        _reminders = new ObservableCollection<ReminderRow>(_cfg.Reminders.Select(ReminderRow.From));
        RemindersGrid.ItemsSource = _reminders;

        // Sobre
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? Strings.VersionDev : Strings.VersionFormat(version.ToString(3));

        Closed += (_, _) => _clockPreviewTimer.Stop();
    }

    /// <summary>
    /// Aplica o idioma atual (<see cref="Strings.Current"/>, já resolvido por
    /// <see cref="ConfigService.Load"/> antes do construtor rodar) a todo texto estático
    /// da tela — o XAML mantém o pt-BR como valor de design-time/fallback.
    /// </summary>
    private void ApplyLabels()
    {
        TitleBarText.Text = Strings.SettingsTitleBar;
        CancelButton.Content = Strings.Cancel;
        SaveButton.Content = Strings.SaveAndReload;

        NavGeral.Content = Strings.NavGeral;
        NavAparencia.Content = Strings.NavAparencia;
        NavRelogio.Content = Strings.NavRelogio;
        NavWidgets.Content = Strings.NavWidgets;
        NavLembretes.Content = Strings.NavLembretes;
        NavSobre.Content = Strings.NavSobre;

        GeralTitle.Text = Strings.SectionGeral;
        EdgeLabel.Text = Strings.LabelEdge;
        ThicknessLabel.Text = Strings.LabelThickness;
        FloatingModeCheck.Content = Strings.LabelFloatingMode;
        FloatingMarginLabel.Text = Strings.LabelFloatingMargin;
        MonitorModeLabel.Text = Strings.LabelMonitorMode;
        MonitorSpecificLabel.Text = Strings.LabelMonitorSpecific;
        StartWithWindowsCheck.Content = Strings.LabelStartWithWindows;
        GamingModeCheck.Content = Strings.LabelGamingMode;
        GamingModeHint.Text = Strings.HintGamingMode;
        LanguageLabel.Text = Strings.LabelLanguage;

        AparenciaTitle.Text = Strings.SectionAparencia;
        PreviewLabel.Text = Strings.LabelPreview;
        ThemeLabel.Text = Strings.LabelTheme;
        OverridesLabel.Text = Strings.LabelOverridesOptional;
        BackgroundColorHint.Text = Strings.HintBackgroundColor;
        ClearBackgroundButton.Content = Strings.Clear;
        ForegroundColorHint.Text = Strings.HintForegroundColor;
        ClearForegroundButton.Content = Strings.Clear;
        FontLabel.Text = Strings.LabelFont;
        CustomFontSizeCheck.Content = Strings.LabelCustomFontSize;

        RelogioTitle.Text = Strings.SectionRelogio;
        ClockModeLabel.Text = Strings.LabelClockMode;
        ClockStyleLabel.Text = Strings.LabelClockStyle;
        ClockCultureLabel.Text = Strings.LabelClockCulture;
        ClockCustomFormatLabel.Text = Strings.LabelClockCustomFormat;
        ClockPreviewLabel.Text = Strings.LabelPreviewSection;
        ClockPreviewHorizontalLabel.Text = Strings.PreviewHorizontal;
        ClockPreviewVerticalLabel.Text = Strings.PreviewVertical;

        WidgetsTitle.Text = Strings.SectionWidgets;
        WidgetsHint.Text = Strings.HintWidgets;
        ZoneStartLabel.Text = Strings.ZoneStart;
        ZoneCenterLabel.Text = Strings.ZoneCenter;
        ZoneEndLabel.Text = Strings.ZoneEnd;
        StartPinButton.ToolTip = Strings.TogglePinTooltip;
        CenterPinButton.ToolTip = Strings.TogglePinTooltip;
        EndPinButton.ToolTip = Strings.TogglePinTooltip;
        StartRemoveButton.Content = Strings.Remove;
        CenterRemoveButton.Content = Strings.Remove;
        EndRemoveButton.Content = Strings.Remove;
        StartAddWidgetButton.Content = Strings.AddWidgetButton;
        CenterAddWidgetButton.Content = Strings.AddWidgetButton;
        EndAddWidgetButton.Content = Strings.AddWidgetButton;

        LembretesTitle.Text = Strings.SectionLembretes;
        AddReminderButton.Content = Strings.AddReminder;
        RemoveReminderButton.Content = Strings.RemoveSelected;
        TextColumn.Header = Strings.ColumnText;
        TimeColumn.Header = Strings.ColumnTime;
        DaysColumn.Header = Strings.ColumnDays;
        SoundColumn.Header = Strings.ColumnSound;
        CompletedColumn.Header = Strings.ColumnCompleted;
        KindColumn.Header = Strings.ColumnType;
        RecurrenceColumn.Header = Strings.ColumnRecurrence;

        AppTaglineText.Text = Strings.AppTagline;
        GitHubButton.Content = Strings.ViewOnGitHub;
    }

    // ---------- Relógio ----------

    private void ClockModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateClockModeUi();
        UpdateClockPreview();
    }

    private void ClockStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateClockPreview();

    private void ClockCultureBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateClockPreview();

    private void ClockCustomFormatBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateClockPreview();

    private void UpdateClockModeUi()
    {
        bool custom = ClockModeCombo.SelectedItem is ClockMode.Custom;
        ClockStyleCombo.IsEnabled = !custom;
        ClockCustomFormatBox.IsEnabled = custom;
    }

    private void UpdateClockPreview()
    {
        if (ClockPreviewHorizontal is null) return; // ainda no InitializeComponent

        var preview = new ClockConfig
        {
            Mode = ClockModeCombo.SelectedItem is ClockMode mode ? mode : ClockMode.Both,
            Style = ClockStyleCombo.SelectedItem is ClockStyle style ? style : ClockStyle.Classic,
            Culture = ClockCultureBox.Text,
            CustomFormat = ClockCustomFormatBox.Text
        };
        var now = DateTime.Now;
        ClockPreviewHorizontal.Text = ClockFormatter.Format(preview, now, vertical: false);
        ClockPreviewVertical.Text = ClockFormatter.Format(preview, now, vertical: true).Replace("\n", "   /   ");
    }

    // ---------- Navegação lateral ----------

    private void Nav_Changed(object sender, RoutedEventArgs e)
    {
        if (GeralSection is null) return; // dispara durante o InitializeComponent antes dos outros elementos existirem

        GeralSection.Visibility = NavGeral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AparenciaSection.Visibility = NavAparencia.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RelogioSection.Visibility = NavRelogio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        WidgetsSection.Visibility = NavWidgets.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        LembretesSection.Visibility = NavLembretes.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SobreSection.Visibility = NavSobre.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Barra de título customizada ----------

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) return; // sem maximizar — ResizeMode="NoResize"
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    // ---------- Geral ----------

    private void MonitorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        MonitorCombo.IsEnabled = MonitorModeCombo.SelectedItem is MonitorMode.Specific;

    private void FloatingModeCheck_Changed(object sender, RoutedEventArgs e) =>
        FloatingMarginSlider.IsEnabled = FloatingModeCheck.IsChecked == true;

    // ---------- Aparência ----------

    private void BuildThemeCards()
    {
        var amber = (Brush)FindResource("HeimdallAccent");
        var border = (Brush)FindResource("HeimdallBorder");

        ThemeCardsList.ItemsSource = ThemeService.GetAllThemeNames().Select(name =>
        {
            var def = ThemeService.GetThemeDefinition(name);
            bool selected = string.Equals(name, _selectedTheme, StringComparison.OrdinalIgnoreCase);
            return new ThemeCardViewModel
            {
                Name = name,
                Background = ToBrush(def.Background),
                Accent = ToBrush(def.Accent),
                Text = ToBrush(def.TextPrimary),
                BorderBrush = selected ? amber : border,
                BorderThickness = new Thickness(selected ? 2 : 1)
            };
        }).ToList();
    }

    private void ThemeCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string name) return;
        _selectedTheme = name;
        BuildThemeCards();
        UpdatePreview();
    }

    private void BackgroundHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSwatch(BackgroundSwatch, BackgroundHexBox.Text);
        UpdatePreview();
    }

    private void ForegroundHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSwatch(ForegroundSwatch, ForegroundHexBox.Text);
        UpdatePreview();
    }

    private static void UpdateSwatch(Border swatch, string? hex)
    {
        swatch.Background = TryParseColor(hex) is { } color ? new SolidColorBrush(color) : Brushes.Transparent;
    }

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return ColorConverter.ConvertFromString(hex) as Color?; }
        catch (FormatException) { return null; }
    }

    private static Brush ToBrush(string hex) =>
        TryParseColor(hex) is { } color ? new SolidColorBrush(color) : Brushes.Gray;

    // Grid de cores predefinidas do tema — clicar preenche o hex box (o "personalizado"
    // continua sendo o próprio TextBox de hex ao lado do swatch, sem precisar duplicar um
    // campo dentro do popup).
    private static readonly (string Name, string Hex)[] ColorPresets =
    {
        ("Âmbar", "#FFE9A23B"), ("Verde-azulado", "#FF2FA39A"), ("Azul", "#FF3F6FE0"),
        ("Fundo", "#FF0F1318"), ("Painel", "#FF161B22"), ("Campo", "#FF1A2028"),
        ("Marfim", "#FFF2EEE6"), ("Secundário", "#FFA7B0BC"), ("Cabo", "#FF3A4350"),
        ("Branco", "#FFFFFFFF"), ("Preto", "#FF000000"), ("Vermelho", "#FFE05252"),
        ("Verde", "#FF4CAF50"), ("Roxo", "#FF9370DB")
    };

    private void BackgroundSwatch_Click(object sender, RoutedEventArgs e) => ShowColorPickerPopup(BackgroundSwatchButton, BackgroundHexBox);

    private void ForegroundSwatch_Click(object sender, RoutedEventArgs e) => ShowColorPickerPopup(ForegroundSwatchButton, ForegroundHexBox);

    private void ShowColorPickerPopup(UIElement placementTarget, TextBox targetHexBox)
    {
        var grid = new WrapPanel { Width = 200 };
        foreach (var (name, hex) in ColorPresets)
        {
            var swatch = new Border
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                Background = ToBrush(hex),
                BorderBrush = (Brush)FindResource("HeimdallBorder"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = Strings.ColorPresetName(name)
            };
            var popup = new Popup { PlacementTarget = placementTarget, Placement = PlacementMode.Bottom, StaysOpen = false };
            swatch.MouseLeftButtonUp += (_, _) =>
            {
                targetHexBox.Text = hex;
                popup.IsOpen = false;
            };
            grid.Children.Add(swatch);

            if (grid.Tag is null) grid.Tag = popup; // guarda o popup só pra manter uma referência viva
        }

        var container = new Border
        {
            Background = (Brush)FindResource("HeimdallPanelBackground"),
            BorderBrush = (Brush)FindResource("HeimdallBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = grid
        };

        var mainPopup = new Popup
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = container,
            IsOpen = true
        };

        // Cada swatch fecha o popup certo — reatribui aqui em vez de dentro do loop, já
        // que o popup só existe depois de montar o conteúdo inteiro.
        foreach (var child in grid.Children.OfType<Border>())
        {
            child.MouseLeftButtonUp += (_, _) => mainPopup.IsOpen = false;
        }
    }

    private void FontFamilyCombo_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void CustomFontSizeCheck_Changed(object sender, RoutedEventArgs e)
    {
        FontSizeSlider.IsEnabled = CustomFontSizeCheck.IsChecked == true;
        UpdatePreview();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();

    private void ClearBackground_Click(object sender, RoutedEventArgs e)
    {
        BackgroundHexBox.Text = "";
    }

    private void ClearForeground_Click(object sender, RoutedEventArgs e)
    {
        ForegroundHexBox.Text = "";
    }

    private void UpdatePreview()
    {
        if (PreviewBar is null) return; // ainda no InitializeComponent

        var theme = ThemeService.GetThemeDefinition(_selectedTheme);
        var bg = TryParseColor(BackgroundHexBox.Text) ?? TryParseColor(theme.Background) ?? Colors.Black;
        var fg = TryParseColor(ForegroundHexBox.Text) ?? TryParseColor(theme.TextPrimary) ?? Colors.White;
        string fontFamily = string.IsNullOrWhiteSpace(FontFamilyCombo.Text) ? theme.FontFamily : FontFamilyCombo.Text;
        double fontSize = CustomFontSizeCheck.IsChecked == true ? FontSizeSlider.Value : 13;

        PreviewBar.Background = new SolidColorBrush(bg);
        var fgBrush = new SolidColorBrush(fg);
        PreviewTitle.Foreground = fgBrush;
        PreviewClock.Foreground = fgBrush;
        var family = new FontFamily(fontFamily);
        PreviewTitle.FontFamily = family;
        PreviewClock.FontFamily = family;
        PreviewTitle.FontSize = fontSize;
        PreviewClock.FontSize = fontSize;
    }

    // ---------- Widgets ----------

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ListBox list || list.SelectedItem is not WidgetRow row) return;
        row.Pinned = !row.Pinned;
        list.Items.Refresh();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ListBox list && list.SelectedIndex >= 0)
            list.Items.RemoveAt(list.SelectedIndex);
    }

    private void AddStart_Click(object sender, RoutedEventArgs e) => ShowWidgetCatalogPopup((Button)sender, StartList);
    private void AddCenter_Click(object sender, RoutedEventArgs e) => ShowWidgetCatalogPopup((Button)sender, CenterList);
    private void AddEnd_Click(object sender, RoutedEventArgs e) => ShowWidgetCatalogPopup((Button)sender, EndList);

    private void ShowWidgetCatalogPopup(Button anchor, ListBox target)
    {
        var grid = new WrapPanel { Width = 200 };
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };

        foreach (var (id, icon) in WidgetCatalog)
        {
            var button = new Button
            {
                Width = 92,
                Height = 56,
                Margin = new Thickness(3),
                Tag = id,
                Style = (Style)FindResource("HeimdallSecondaryButton"),
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = icon, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = Strings.WidgetName(id), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) }
                    }
                }
            };
            button.Click += (_, _) =>
            {
                if (!target.Items.Cast<WidgetRow>().Any(r => r.Id == id))
                    target.Items.Add(new WidgetRow { Id = id });
                popup.IsOpen = false;
            };
            grid.Children.Add(button);
        }

        popup.Child = new Border
        {
            Background = (Brush)FindResource("HeimdallPanelBackground"),
            BorderBrush = (Brush)FindResource("HeimdallBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = grid
        };
        popup.IsOpen = true;
    }

    /// <summary>Arrastar pra reordenar dentro da lista e entre as 3 colunas — drag-drop nativo do WPF, mais simples que a mecânica de fantasma da barra (que depende de janelas/telas reais), suficiente pra reordenar dentro da própria tela de Configurações.</summary>
    private void EnableWidgetDragDrop(ListBox listBox)
    {
        listBox.AllowDrop = true;

        listBox.PreviewMouseLeftButtonDown += (_, e) => _widgetDragStart = e.GetPosition(null);

        listBox.PreviewMouseMove += (_, e) =>
        {
            if (_widgetDragStart is null || e.LeftButton != MouseButtonState.Pressed) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _widgetDragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _widgetDragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            if (FindListBoxItem(e.OriginalSource as DependencyObject) is not { } item || item.Content is not WidgetRow row) return;
            _widgetDragStart = null;
            DragDrop.DoDragDrop(item, new DataObject(typeof(WidgetRow), row), DragDropEffects.Move);
        };

        listBox.Drop += (_, e) =>
        {
            if (!e.Data.GetDataPresent(typeof(WidgetRow)) || e.Data.GetData(typeof(WidgetRow)) is not WidgetRow dragged) return;

            foreach (var other in _widgetLists)
            {
                if (other.Items.Contains(dragged)) { other.Items.Remove(dragged); break; }
            }

            int insertAt = FindListBoxItem(e.OriginalSource as DependencyObject) is { } targetItem
                ? listBox.Items.IndexOf(targetItem.Content)
                : listBox.Items.Count;
            listBox.Items.Insert(Math.Clamp(insertAt, 0, listBox.Items.Count), dragged);
        };
    }

    private static ListBoxItem? FindListBoxItem(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem) source = VisualTreeHelper.GetParent(source);
        return source as ListBoxItem;
    }

    // ---------- Sobre ----------

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true }); }
        catch { /* sem navegador padrão configurado, etc. — não trava a tela por isso */ }
    }

    // ---------- Lembretes ----------

    private void AddReminder_Click(object sender, RoutedEventArgs e) =>
        _reminders.Add(new ReminderRow { Text = Strings.NewReminderDefaultText });

    private void RemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (RemindersGrid.SelectedItem is ReminderRow row) _reminders.Remove(row);
    }

    // ---------- Salvar ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RemindersGrid.CommitEdit(DataGridEditingUnit.Row, true);

        foreach (var row in _reminders)
        {
            if (row.Kind != ReminderKind.Scheduled) continue;
            if (!DateTime.TryParseExact(row.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                MessageBox.Show(this, Strings.InvalidTimeMessage(row.Text), "Heimdall",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        _cfg.Edge = (BarEdge)EdgeCombo.SelectedItem;
        _cfg.Thickness = (int)ThicknessSlider.Value;
        _cfg.FloatingMode = FloatingModeCheck.IsChecked == true;
        _cfg.FloatingMargin = (int)FloatingMarginSlider.Value;
        _cfg.MonitorMode = (MonitorMode)MonitorModeCombo.SelectedItem;
        _cfg.MonitorDevice = (MonitorCombo.SelectedItem as MonitorOption)?.Device;
        _cfg.Theme = _selectedTheme;
        _cfg.Style.Background = string.IsNullOrWhiteSpace(BackgroundHexBox.Text) ? null : BackgroundHexBox.Text;
        _cfg.Style.Foreground = string.IsNullOrWhiteSpace(ForegroundHexBox.Text) ? null : ForegroundHexBox.Text;
        _cfg.Style.FontFamily = string.IsNullOrWhiteSpace(FontFamilyCombo.Text) ? null : FontFamilyCombo.Text;
        _cfg.Style.FontSize = CustomFontSizeCheck.IsChecked == true ? FontSizeSlider.Value : null;
        _cfg.Widgets.Start = StartList.Items.Cast<WidgetRow>().Select(r => r.ToEntry()).ToList();
        _cfg.Widgets.Center = CenterList.Items.Cast<WidgetRow>().Select(r => r.ToEntry()).ToList();
        _cfg.Widgets.End = EndList.Items.Cast<WidgetRow>().Select(r => r.ToEntry()).ToList();
        _cfg.Clock.Mode = (ClockMode)ClockModeCombo.SelectedItem;
        _cfg.Clock.Style = (ClockStyle)ClockStyleCombo.SelectedItem;
        _cfg.Clock.Culture = string.IsNullOrWhiteSpace(ClockCultureBox.Text) ? "pt-BR" : ClockCultureBox.Text;
        _cfg.Clock.CustomFormat = string.IsNullOrWhiteSpace(ClockCustomFormatBox.Text) ? null : ClockCustomFormatBox.Text;
        _cfg.Reminders = _reminders.Select(r => r.ToConfig()).ToList();

        bool startWithWindows = StartWithWindowsCheck.IsChecked == true;
        if (startWithWindows != _cfg.StartWithWindows) StartupService.SetEnabled(startWithWindows);
        _cfg.StartWithWindows = startWithWindows;
        _cfg.GamingMode = GamingModeCheck.IsChecked == true;

        var previousLanguage = _cfg.Language;
        _cfg.Language = (LanguageCombo.SelectedItem as LanguageOption)?.Value ?? _cfg.Language;

        ConfigService.Save(_cfg);
        var app = (App)Application.Current;
        app.Reload();

        // Essa janela já foi toda montada com os textos do idioma anterior — reabrir do
        // zero é mais simples e confiável do que re-aplicar ApplyLabels() em cima de
        // controles que já têm valor/seleção do usuário. Close() dispara o Closed que
        // limpa App._settingsWindow, então OpenSettings() já cria uma instância nova.
        if (_cfg.Language != previousLanguage)
        {
            Close();
            app.OpenSettings();
            return;
        }

        StatusText.Text = Strings.Saved;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record MonitorOption(string Device, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record LanguageOption(AppLanguage Value, string Label)
    {
        // O ComboBox estilizado (Theme.xaml) mostra o item selecionado via SelectionBoxItem,
        // que às vezes cai no ToString() padrão em vez de respeitar DisplayMemberPath —
        // sobrescrever aqui garante o texto certo independente de qual caminho o WPF usa.
        public override string ToString() => Label;
    }

    private sealed class ThemeCardViewModel
    {
        public required string Name { get; init; }
        public required Brush Background { get; init; }
        public required Brush Accent { get; init; }
        public required Brush Text { get; init; }
        public required Brush BorderBrush { get; init; }
        public required Thickness BorderThickness { get; init; }
    }

    private sealed class WidgetRow
    {
        public string Id { get; set; } = "";
        public bool Pinned { get; set; }

        public string Icon => WidgetCatalog.FirstOrDefault(w => w.Id == Id).Icon is { } icon && !string.IsNullOrEmpty(icon) ? icon : "•";
        public string DisplayName => Strings.WidgetName(Id);

        public static WidgetRow From(WidgetEntry entry) => new() { Id = entry.Id, Pinned = entry.Pinned };

        public WidgetEntry ToEntry() => new(Id, Pinned);
    }

    private sealed class ReminderRow
    {
        public ReminderKind Kind { get; set; }
        public string Text { get; set; } = "";
        public string? Time { get; set; }
        public ReminderRecurrence Recurrence { get; set; }
        public string Days { get; set; } = "";
        public bool PlaySound { get; set; }
        public bool Completed { get; set; }

        public static ReminderRow From(ReminderConfig r) => new()
        {
            Kind = r.Kind,
            Text = r.Text,
            Time = r.Time,
            Recurrence = r.Recurrence,
            Days = string.Join(",", r.Days),
            PlaySound = r.PlaySound,
            Completed = r.Completed
        };

        public ReminderConfig ToConfig() => new()
        {
            Kind = Kind,
            Text = Text,
            Time = Time,
            Recurrence = Recurrence,
            Days = Days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => Enum.TryParse<DayOfWeek>(d, true, out var dow) ? (DayOfWeek?)dow : null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList(),
            PlaySound = PlaySound,
            Completed = Completed
        };
    }
}
