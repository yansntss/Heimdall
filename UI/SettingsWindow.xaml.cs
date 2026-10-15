using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.ViewModels;
using Heimdall.Widgets;

namespace Heimdall.UI;

/// <summary>
/// Janela de Configurações. Aparência é MVVM puro (<see cref="AppearanceViewModel"/>, prévia
/// ao vivo); as seções antigas (Geral, Relógio, Widgets, Lembretes) mantêm a lógica que já
/// tinham aqui e só escrevem no rascunho (<see cref="SettingsViewModel.Draft"/>) no Aplicar.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly (string Id, string Icon)[] WidgetCatalog = WidgetFactory.Catalog;

    private const string GitHubUrl = "https://github.com/yansntss/Heimdall";

    private readonly SettingsViewModel _vm;
    private readonly AppConfig _cfg;
    private readonly ObservableCollection<ReminderRow> _reminders;
    private readonly DispatcherTimer _clockPreviewTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<ListBox> _widgetLists = new();
    private Point? _widgetDragStart;

    public SettingsWindow()
    {
        InitializeComponent();
        _vm = new SettingsViewModel();
        _cfg = _vm.Draft;
        DataContext = _vm;
        Title = Strings.SettingsTitleBar;

        // Geral
        FloatingModeCheck.IsChecked = _cfg.FloatingMode;
        FloatingMarginSlider.Value = _cfg.FloatingMargin;
        FloatingMarginSlider.IsEnabled = _cfg.FloatingMode;

        MonitorModeCombo.ItemsSource = Enum.GetValues(typeof(MonitorMode));
        var monitors = MonitorService.GetMonitors()
            .Select(m => new MonitorOption(m.ShortName,
                $"{m.ShortName} — {m.Bounds.Width}x{m.Bounds.Height}" + (m.IsPrimary ? Strings.MonitorPrimarySuffix : "")))
            .ToList();
        MonitorCombo.ItemsSource = monitors;
        MonitorCombo.DisplayMemberPath = nameof(MonitorOption.Label);
        MonitorCombo.SelectedItem = monitors.FirstOrDefault(o =>
            string.Equals(o.Device, MonitorInfo.Normalize(_cfg.MonitorDevice), StringComparison.OrdinalIgnoreCase))
            ?? monitors.FirstOrDefault();
        MonitorModeCombo.SelectedItem = _cfg.MonitorMode;
        MonitorCombo.IsEnabled = MonitorModeCombo.SelectedItem is MonitorMode.Specific;

        LanguageCombo.ItemsSource = new[]
        {
            new LanguageOption(AppLanguage.PtBr, Strings.LanguagePtBr),
            new LanguageOption(AppLanguage.EnUs, Strings.LanguageEnUs)
        };
        LanguageCombo.DisplayMemberPath = nameof(LanguageOption.Label);
        LanguageCombo.SelectedItem = ((IEnumerable<LanguageOption>)LanguageCombo.ItemsSource)
            .FirstOrDefault(o => o.Value == _cfg.Language);

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
        RecurrenceColumn.ItemsSource = Enum.GetValues(typeof(RepeatKind));
        _reminders = new ObservableCollection<ReminderRow>(_cfg.Reminders.Select(ReminderRow.From));
        RemindersGrid.ItemsSource = _reminders;

        // Atalhos (somente leitura: os globais registrados no App)
        ShortcutsList.ItemsSource = new[]
        {
            new ShortcutRow("Ctrl+Alt+H", Strings.ShortcutToggleBar),
            new ShortcutRow("Ctrl+Shift+B", Strings.ShortcutToggleBarAlt),
            new ShortcutRow("Ctrl+Shift+R", Strings.ShortcutNewReminder),
            new ShortcutRow("Ctrl+Alt+Space", Strings.ShortcutPlayPause),
            new ShortcutRow("Ctrl+Alt+← / →", Strings.ShortcutPrevNext),
            new ShortcutRow("Ctrl+Alt+↑ / ↓", Strings.ShortcutVolume),
        };

        // Sobre
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? Strings.VersionDev : Strings.VersionFormat(version.ToString(3));

        StateChanged += (_, _) => ApplyWindowState();
        Closing += OnClosing;
        Closed += (_, _) => _clockPreviewTimer.Stop();
    }

    /// <summary>Abre direto numa seção (ex.: "Configurar widget" do menu de um widget).</summary>
    internal void SelectSection(string key)
    {
        if (_vm.Sections.FirstOrDefault(s => s.Key == key) is { } section) _vm.SelectedSection = section;
    }

    // ---------- Chrome (só visual) ----------

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Maximizada, uma janela sem moldura passa uns pixels de cada borda da tela (a borda
    /// de redimensionar fica pra fora) — compensa com margem e tira o raio, que não faz
    /// sentido colado na borda.
    /// </summary>
    private void ApplyWindowState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        var border = SystemParameters.WindowResizeBorderThickness;
        Frame.Margin = maximized ? new Thickness(border.Left + 1, border.Top + 1, border.Right + 1, border.Bottom + 1) : new Thickness(0);
        if (maximized) Frame.CornerRadius = new CornerRadius(0);
        else Frame.SetResourceReference(Border.CornerRadiusProperty, "H.Radius.Window");
        MaximizeIcon.Geometry = (Geometry)FindResource(maximized ? "H.Icon.Restore" : "H.Icon.Maximize");
    }

    // ---------- Rodapé ----------

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!CollectLegacySections()) return;

        var previousLanguage = Strings.Current;
        _vm.Apply();

        // Essa janela já foi toda montada com os textos do idioma anterior — reabrir do
        // zero é mais simples e confiável do que trocar texto por texto.
        if (_cfg.Language != previousLanguage)
        {
            Close();
            ((App)Application.Current).OpenSettings();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e) => _vm.RestoreDefaults();

    /// <summary>Fechar sem aplicar desfaz a prévia ao vivo (a barra volta ao que está salvo).</summary>
    private void OnClosing(object? sender, CancelEventArgs e) => _vm.Cancel();

    /// <summary>Copia as seções antigas pro rascunho; false se algum lembrete tiver data ou horário inválido.</summary>
    private bool CollectLegacySections()
    {
        RemindersGrid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var row in _reminders)
        {
            if (!row.IsValid())
            {
                MessageBox.Show(this, Strings.InvalidTimeMessage(row.Text), "Heimdall", MessageBoxButton.OK, MessageBoxImage.Warning);
                _vm.SelectedSection = _vm.Sections.First(s => s.Key == "lembretes");
                return false;
            }
        }

        _cfg.FloatingMode = FloatingModeCheck.IsChecked == true;
        _cfg.FloatingMargin = (int)FloatingMarginSlider.Value;
        _cfg.MonitorMode = (MonitorMode)MonitorModeCombo.SelectedItem;
        _cfg.MonitorDevice = (MonitorCombo.SelectedItem as MonitorOption)?.Device;
        _cfg.Widgets.Start = StartList.Items.Cast<WidgetRow>().Select(r => r.ToEntry()).ToList();
        _cfg.Widgets.Center = CenterList.Items.Cast<WidgetRow>().Select(r => r.ToEntry()).ToList();
        _cfg.Widgets.End = EndList.Items.Cast<WidgetRow>().Select(r => r.ToEntry()).ToList();
        _cfg.Clock.Mode = (ClockMode)ClockModeCombo.SelectedItem;
        _cfg.Clock.Style = (ClockStyle)ClockStyleCombo.SelectedItem;
        _cfg.Clock.Culture = string.IsNullOrWhiteSpace(ClockCultureBox.Text) ? "pt-BR" : ClockCultureBox.Text;
        _cfg.Clock.CustomFormat = string.IsNullOrWhiteSpace(ClockCustomFormatBox.Text) ? null : ClockCustomFormatBox.Text;
        _cfg.Reminders = _reminders.Select(r => r.ToConfig()).ToList();
        _cfg.Language = (LanguageCombo.SelectedItem as LanguageOption)?.Value ?? _cfg.Language;
        return true;
    }

    // ---------- Geral ----------

    private void MonitorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        MonitorCombo.IsEnabled = MonitorModeCombo.SelectedItem is MonitorMode.Specific;

    private void FloatingModeCheck_Changed(object sender, RoutedEventArgs e) =>
        FloatingMarginSlider.IsEnabled = FloatingModeCheck.IsChecked == true;

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
                Style = (Style)FindResource("H.SecondaryButton"),
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

        var surface = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(8), Child = grid };
        surface.SetResourceReference(Border.BackgroundProperty, "H.Brush.Elevated");
        surface.SetResourceReference(Border.BorderBrushProperty, "H.Brush.Line");
        popup.Child = surface;
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

    private sealed record MonitorOption(string Device, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ShortcutRow(string Keys, string Description);

    private sealed record LanguageOption(AppLanguage Value, string Label)
    {
        // O ComboBox estilizado (Themes/Forms.xaml) mostra o item selecionado via SelectionBoxItem,
        // que às vezes cai no ToString() padrão em vez de respeitar DisplayMemberPath —
        // sobrescrever aqui garante o texto certo independente de qual caminho o WPF usa.
        public override string ToString() => Label;
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

    /// <summary>
    /// Linha editável da tabela (provisória — a seção Lembretes ganha tela própria). Data
    /// "dd/MM/yyyy" e hora "HH:mm", vazias = sem data/sem hora. Guarda o lembrete original
    /// pra não perder o que a tabela não mostra (id, lista, nota, bandeira, criação).
    /// </summary>
    private sealed class ReminderRow
    {
        private const string DateFormat = "dd/MM/yyyy";

        public ReminderConfig? Source { get; init; }
        public string Text { get; set; } = "";
        public string? Date { get; set; }
        public string? Time { get; set; }
        public RepeatKind Recurrence { get; set; }
        public string Days { get; set; } = "";
        public bool Completed { get; set; }

        public static ReminderRow From(ReminderConfig r) => new()
        {
            Text = r.Title,
            Date = r.DueDate?.ToString(DateFormat, CultureInfo.InvariantCulture),
            Time = r.DueTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
            Recurrence = r.Recurrence?.Kind ?? RepeatKind.None,
            Days = string.Join(",", r.Recurrence?.Days ?? new()),
            Completed = r.IsCompleted,
            Source = r
        };

        public bool IsValid() =>
            (string.IsNullOrWhiteSpace(Date) || TryDate(out _)) && (string.IsNullOrWhiteSpace(Time) || TryTime(out _));

        public ReminderConfig ToConfig()
        {
            var r = Source ?? new ReminderConfig { CreatedAt = DateTime.Now };
            r.Title = Text;
            r.DueDate = TryDate(out var date) ? date : null;
            // Hora sem data vira "hoje" — senão não teria quando disparar.
            if (r.DueDate is null && TryTime(out _)) r.DueDate = DateOnly.FromDateTime(DateTime.Now);
            r.DueTime = TryTime(out var time) ? time : null;

            if (Recurrence == RepeatKind.None || r.DueDate is null) r.Recurrence = null;
            else
            {
                var days = Days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(d => Enum.TryParse<DayOfWeek>(d, true, out var dow) ? (DayOfWeek?)dow : null)
                    .Where(d => d.HasValue).Select(d => d!.Value).ToList();
                r.Recurrence = new ReminderRecurrence
                {
                    Kind = Recurrence,
                    Days = days,
                    Anchor = r.Recurrence?.Anchor ?? r.DueDate,
                    Until = r.Recurrence?.Until
                };
            }

            if (!Completed) r.CompletedAt = null;
            else r.CompletedAt ??= DateTime.Now;
            return r;
        }

        private bool TryDate(out DateOnly date) =>
            DateOnly.TryParseExact(Date?.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private bool TryTime(out TimeOnly time) =>
            TimeOnly.TryParseExact(Time?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
