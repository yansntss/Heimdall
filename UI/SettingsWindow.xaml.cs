using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InfoBar.Config;
using InfoBar.Services;

namespace InfoBar.UI;

public partial class SettingsWindow : Window
{
    private static readonly string[] WidgetCatalog = { "clock", "media", "reminder" };

    private readonly AppConfig _cfg;
    private readonly ObservableCollection<ReminderRow> _reminders;

    public SettingsWindow()
    {
        InitializeComponent();
        _cfg = ConfigService.Load();

        // Geral
        EdgeCombo.ItemsSource = Enum.GetValues(typeof(BarEdge));
        EdgeCombo.SelectedItem = _cfg.Edge;
        ThicknessBox.Text = _cfg.Thickness.ToString(CultureInfo.InvariantCulture);

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
        BackgroundHexBox.Text = _cfg.Style.Background;
        ForegroundHexBox.Text = _cfg.Style.Foreground;
        FontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
        FontFamilyCombo.Text = _cfg.Style.FontFamily;
        FontSizeBox.Text = _cfg.Style.FontSize.ToString(CultureInfo.InvariantCulture);

        // Widgets
        StartCatalog.ItemsSource = WidgetCatalog;
        CenterCatalog.ItemsSource = WidgetCatalog;
        EndCatalog.ItemsSource = WidgetCatalog;
        foreach (var id in _cfg.Widgets.Start) StartList.Items.Add(id);
        foreach (var id in _cfg.Widgets.Center) CenterList.Items.Add(id);
        foreach (var id in _cfg.Widgets.End) EndList.Items.Add(id);

        // Lembretes
        KindColumn.ItemsSource = Enum.GetValues(typeof(ReminderKind));
        RecurrenceColumn.ItemsSource = Enum.GetValues(typeof(ReminderRecurrence));
        _reminders = new ObservableCollection<ReminderRow>(_cfg.Reminders.Select(ReminderRow.From));
        RemindersGrid.ItemsSource = _reminders;
    }

    // ---------- Geral ----------

    private void MonitorModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        MonitorCombo.IsEnabled = MonitorModeCombo.SelectedItem is MonitorMode.Specific;

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

    private void ThemeDark_Click(object sender, RoutedEventArgs e) => ApplyTheme("#E61E1E1E", "#FFFFFFFF");
    private void ThemeLight_Click(object sender, RoutedEventArgs e) => ApplyTheme("#F2F5F5F5", "#FF1E1E1E");
    private void ThemeAcrylic_Click(object sender, RoutedEventArgs e) => ApplyTheme("#661E1E1E", "#FFFFFFFF");

    private void ApplyTheme(string background, string foreground)
    {
        BackgroundHexBox.Text = background;
        ForegroundHexBox.Text = foreground;
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
        if (catalog.SelectedItem is string id && !list.Items.Contains(id))
            list.Items.Add(id);
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
            MessageBox.Show(this, "Espessura inválida — use um valor entre 16 e 400.", "InfoBar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(FontSizeBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double fontSize)
            || fontSize < 8 || fontSize > 72)
        {
            MessageBox.Show(this, "Tamanho de fonte inválido — use um valor entre 8 e 72.", "InfoBar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var row in _reminders)
        {
            if (row.Kind != ReminderKind.Scheduled) continue;
            if (!DateTime.TryParseExact(row.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                MessageBox.Show(this, $"Horário inválido em \"{row.Text}\" — use o formato HH:mm.", "InfoBar",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        _cfg.Edge = (BarEdge)EdgeCombo.SelectedItem;
        _cfg.Thickness = thickness;
        _cfg.MonitorMode = (MonitorMode)MonitorModeCombo.SelectedItem;
        _cfg.MonitorDevice = (MonitorCombo.SelectedItem as MonitorOption)?.Device;
        _cfg.Style.Background = BackgroundHexBox.Text;
        _cfg.Style.Foreground = ForegroundHexBox.Text;
        _cfg.Style.FontFamily = FontFamilyCombo.Text;
        _cfg.Style.FontSize = fontSize;
        _cfg.Widgets.Start = StartList.Items.Cast<string>().ToList();
        _cfg.Widgets.Center = CenterList.Items.Cast<string>().ToList();
        _cfg.Widgets.End = EndList.Items.Cast<string>().ToList();
        _cfg.Reminders = _reminders.Select(r => r.ToConfig()).ToList();

        bool startWithWindows = StartWithWindowsCheck.IsChecked == true;
        if (startWithWindows != _cfg.StartWithWindows) StartupService.SetEnabled(startWithWindows);
        _cfg.StartWithWindows = startWithWindows;

        ConfigService.Save(_cfg);
        ((App)Application.Current).Reload();

        Title = "InfoBar — Configurações (salvo ✓)";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record MonitorOption(string Device, string Label);

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
