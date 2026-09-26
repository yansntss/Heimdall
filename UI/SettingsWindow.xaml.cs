using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Widgets;

namespace Heimdall.UI;

public partial class SettingsWindow : Window
{
    private static readonly string[] WidgetCatalog = { "clock", "media", "reminder", "launcher", "separator" };

    private readonly AppConfig _cfg;
    private readonly ObservableCollection<ReminderRow> _reminders;
    private readonly DispatcherTimer _clockPreviewTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    public SettingsWindow()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();

        // Geral
        EdgeCombo.ItemsSource = Enum.GetValues(typeof(BarEdge));
        EdgeCombo.SelectedItem = _cfg.Edge;
        ThicknessBox.Text = _cfg.Thickness.ToString(CultureInfo.InvariantCulture);
        FloatingModeCheck.IsChecked = _cfg.FloatingMode;
        FloatingMarginBox.Text = _cfg.FloatingMargin.ToString(CultureInfo.InvariantCulture);
        FloatingMarginBox.IsEnabled = _cfg.FloatingMode;

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
        StartWithWindowsCheck.IsChecked = _cfg.StartWithWindows;

        // Aparência
        ThemeCombo.ItemsSource = ThemeService.GetAllThemeNames();
        ThemeCombo.SelectedItem = _cfg.Theme;
        BackgroundHexBox.Text = _cfg.Style.Background;
        ForegroundHexBox.Text = _cfg.Style.Foreground;
        FontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        FontFamilyCombo.Text = _cfg.Style.FontFamily;
        FontSizeBox.Text = _cfg.Style.FontSize?.ToString(CultureInfo.InvariantCulture) ?? "";

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
        Closed += (_, _) => _clockPreviewTimer.Stop();

        // Widgets
        StartCatalog.ItemsSource = WidgetCatalog;
        CenterCatalog.ItemsSource = WidgetCatalog;
        EndCatalog.ItemsSource = WidgetCatalog;
        foreach (var entry in _cfg.Widgets.Start) StartList.Items.Add(WidgetRow.From(entry));
        foreach (var entry in _cfg.Widgets.Center) CenterList.Items.Add(WidgetRow.From(entry));
        foreach (var entry in _cfg.Widgets.End) EndList.Items.Add(WidgetRow.From(entry));

        // Lembretes
        KindColumn.ItemsSource = Enum.GetValues(typeof(ReminderKind));
        RecurrenceColumn.ItemsSource = Enum.GetValues(typeof(ReminderRecurrence));
        _reminders = new ObservableCollection<ReminderRow>(_cfg.Reminders.Select(ReminderRow.From));
        RemindersGrid.ItemsSource = _reminders;
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

    // ---------- Geral ----------

    private void MonitorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        MonitorCombo.IsEnabled = MonitorModeCombo.SelectedItem is MonitorMode.Specific;

    private void FloatingModeCheck_Changed(object sender, RoutedEventArgs e) =>
        FloatingMarginBox.IsEnabled = FloatingModeCheck.IsChecked == true;

    // ---------- Aparência ----------

    private void BackgroundHexBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateSwatch(BackgroundSwatch, BackgroundHexBox.Text);

    private void ForegroundHexBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateSwatch(ForegroundSwatch, ForegroundHexBox.Text);

    private static void UpdateSwatch(Border swatch, string? hex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex) && ColorConverter.ConvertFromString(hex) is Color color)
                swatch.Background = new SolidColorBrush(color);
        }
        catch (FormatException) { }
    }

    private void PickBackground_Click(object sender, RoutedEventArgs e) => PickColor(BackgroundHexBox);

    private void PickForeground_Click(object sender, RoutedEventArgs e) => PickColor(ForegroundHexBox);

    private static void PickColor(TextBox target)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dialog.Color;
            target.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void ClearBackground_Click(object sender, RoutedEventArgs e)
    {
        BackgroundHexBox.Text = "";
        BackgroundSwatch.Background = Brushes.Transparent;
    }

    private void ClearForeground_Click(object sender, RoutedEventArgs e)
    {
        ForegroundHexBox.Text = "";
        ForegroundSwatch.Background = Brushes.Transparent;
    }

    // ---------- Widgets ----------

    private static void Move(ListBox? list, int delta)
    {
        if (list is null) return;
        int i = list.SelectedIndex;
        if (i < 0) return;
        int j = i + delta;
        if (j < 0 || j >= list.Items.Count) return;

        var item = list.Items[i];
        list.Items.RemoveAt(i);
        list.Items.Insert(j, item);
        list.SelectedIndex = j;
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move((sender as Button)?.Tag as ListBox, -1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move((sender as Button)?.Tag as ListBox, 1);

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ListBox list && list.SelectedIndex >= 0)
            list.Items.RemoveAt(list.SelectedIndex);
    }

    private void AddStart_Click(object sender, RoutedEventArgs e) => AddWidget(StartList, StartCatalog);
    private void AddCenter_Click(object sender, RoutedEventArgs e) => AddWidget(CenterList, CenterCatalog);
    private void AddEnd_Click(object sender, RoutedEventArgs e) => AddWidget(EndList, EndCatalog);

    private static void AddWidget(ListBox list, ComboBox catalog)
    {
        if (catalog.SelectedItem is string id && !list.Items.Cast<WidgetRow>().Any(r => r.Id == id))
            list.Items.Add(new WidgetRow { Id = id });
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ListBox list || list.SelectedItem is not WidgetRow row) return;
        row.Pinned = !row.Pinned;
        list.Items.Refresh();
    }

    // ---------- Lembretes ----------

    private void AddReminder_Click(object sender, RoutedEventArgs e) =>
        _reminders.Add(new ReminderRow { Text = "Novo lembrete" });

    private void RemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (RemindersGrid.SelectedItem is ReminderRow row) _reminders.Remove(row);
    }

    // ---------- Salvar ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RemindersGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (!int.TryParse(ThicknessBox.Text, out int thickness) || thickness < 16 || thickness > 400)
        {
            MessageBox.Show(this, "Espessura inválida — use um valor entre 16 e 400.", "Heimdall",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(FloatingMarginBox.Text, out int floatingMargin) || floatingMargin < 0 || floatingMargin > 100)
        {
            MessageBox.Show(this, "Margem flutuante inválida — use um valor entre 0 e 100.", "Heimdall",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double? fontSize = null;
        if (!string.IsNullOrWhiteSpace(FontSizeBox.Text))
        {
            if (!double.TryParse(FontSizeBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)
                || parsed < 8 || parsed > 72)
            {
                MessageBox.Show(this, "Tamanho de fonte inválido — use um valor entre 8 e 72 (ou deixe vazio pra usar o tema).", "Heimdall",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            fontSize = parsed;
        }

        foreach (var row in _reminders)
        {
            if (row.Kind != ReminderKind.Scheduled) continue;
            if (!DateTime.TryParseExact(row.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                MessageBox.Show(this, $"Horário inválido em \"{row.Text}\" — use o formato HH:mm.", "Heimdall",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        _cfg.Edge = (BarEdge)EdgeCombo.SelectedItem;
        _cfg.Thickness = thickness;
        _cfg.FloatingMode = FloatingModeCheck.IsChecked == true;
        _cfg.FloatingMargin = floatingMargin;
        _cfg.MonitorMode = (MonitorMode)MonitorModeCombo.SelectedItem;
        _cfg.MonitorDevice = (MonitorCombo.SelectedItem as MonitorOption)?.Device;
        _cfg.Theme = (string)ThemeCombo.SelectedItem;
        _cfg.Style.Background = string.IsNullOrWhiteSpace(BackgroundHexBox.Text) ? null : BackgroundHexBox.Text;
        _cfg.Style.Foreground = string.IsNullOrWhiteSpace(ForegroundHexBox.Text) ? null : ForegroundHexBox.Text;
        _cfg.Style.FontFamily = string.IsNullOrWhiteSpace(FontFamilyCombo.Text) ? null : FontFamilyCombo.Text;
        _cfg.Style.FontSize = fontSize;
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

        ConfigService.Save(_cfg);
        ((App)Application.Current).Reload();

        Title = "Heimdall — Configurações (salvo ✓)";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record MonitorOption(string Device, string Label);

    private sealed class WidgetRow
    {
        public string Id { get; set; } = "";
        public bool Pinned { get; set; }

        public static WidgetRow From(WidgetEntry entry) => new() { Id = entry.Id, Pinned = entry.Pinned };

        public WidgetEntry ToEntry() => new(Id, Pinned);

        public override string ToString() => Pinned ? $"📌 {Id}" : Id;
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
