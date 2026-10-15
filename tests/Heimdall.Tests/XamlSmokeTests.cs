using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.UI;
using Heimdall.UI.Popups;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

/// <summary>
/// Carrega as telas de lembretes de verdade (XAML, recursos do tema, bindings) numa thread
/// STA — pega recurso inexistente e binding quebrado, que o build não vê.
/// </summary>
public class XamlSmokeTests
{
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (message is not null) Errors.Add(message); }
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new Exception("Falhou na thread STA", error);
    }

    private static AppConfig SampleConfig()
    {
        var cfg = new AppConfig();
        cfg.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        cfg.ReminderLists.Add(new ReminderListConfig { Id = "mercado", Name = "Mercado", Color = "#8FD3C8" });
        var today = DateOnly.FromDateTime(DateTime.Now);
        cfg.Reminders.Add(new ReminderConfig { Title = "Atrasado", DueDate = today.AddDays(-1), DueTime = new TimeOnly(8, 0), Flagged = true });
        cfg.Reminders.Add(new ReminderConfig { Title = "Hoje", DueDate = today, Priority = ReminderPriority.High, ListId = "mercado" });
        cfg.Reminders.Add(new ReminderConfig
        {
            Title = "Diário", DueDate = today, DueTime = new TimeOnly(23, 59),
            Recurrence = new ReminderRecurrence { Kind = RepeatKind.Daily }
        });
        cfg.Reminders.Add(new ReminderConfig { Title = "Fixo" });
        return cfg;
    }

    [Fact]
    public void Reminders_window_and_flyout_load_without_binding_errors()
    {
        RunSta(() =>
        {
            var listener = new BindingErrors();
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ThemeManager.Initialize();
            var cfg = SampleConfig();
            ReminderStore.Instance.Attach(cfg);

            var window = new RemindersWindow { Left = -10000, Top = -10000, ShowActivated = false };
            window.Show();
            window.UpdateLayout();
            var items = (ListBox)window.FindName("ItemsList");
            Assert.Equal(3, items.Items.Count);

            var flyoutHost = new Window { Left = -10000, Top = -10000, ShowActivated = false, Width = 300, Height = 400 };
            flyoutHost.Content = new RemindersFlyout(new RemindersFlyoutViewModel(cfg, new Border()));
            flyoutHost.Show();
            flyoutHost.UpdateLayout();

            window.Close();
            flyoutHost.Close();
            Assert.Empty(listener.Errors);
        });
    }

    [Fact]
    public void Quick_add_anchored_to_a_detached_element_does_not_crash()
    {
        RunSta(() =>
        {
            _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ThemeManager.Initialize();
            var cfg = SampleConfig();

            // Overlay de tela cheia: a cópia escondida não pode virar o alvo do "Novo lembrete".
            using var overlayWidget = new Heimdall.Widgets.ReminderWidget(cfg, isOverlay: true);
            Assert.Null(Heimdall.Widgets.ReminderWidget.Primary);

            var popup = new QuickAddReminderWindow(ThemeService.GetEffectiveStyle(cfg)) { ShowActivated = false };
            popup.AnchorTo(new Border(), BarEdge.Top);
            popup.Show();
            popup.UpdateLayout();
            popup.Close();
        });
    }
}
