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
            // Atrasado + 2 de hoje, com os cabeçalhos ATRASADOS e HOJE.
            Assert.Equal(5, items.Items.Count);

            // Edição inline aberta com o painel de detalhes passando por todas as páginas.
            var vm = (RemindersWindowViewModel)window.DataContext;
            var row = vm.Items.OfType<ReminderRowViewModel>().First();
            row.BeginEditCommand.Execute(null);
            row.Editor!.OpenDetailsCommand.Execute(DetailsPage.Main);
            row.Editor.HasDate = true;
            row.Editor.HasTime = true;
            window.UpdateLayout();
            row.Editor.SelectedRepeat = row.Editor.RepeatOptions.Single(o => o.Choice == RepeatChoice.Custom);
            row.Editor.SelectedEnd = row.Editor.EndOptions.Single(o => o.End == RepeatEnd.OnDate);
            foreach (var page in new[] { DetailsPage.Repeat, DetailsPage.RepeatEnd, DetailsPage.List, DetailsPage.Priority, DetailsPage.Notes })
            {
                row.Editor.Details!.NavigateCommand.Execute(page);
                window.UpdateLayout();
                row.Editor.Details.BackCommand.Execute(null);
                window.UpdateLayout();
            }
            row.Editor.Details!.CancelCommand.Execute(null);
            row.Editor.CancelCommand.Execute(null);
            vm.NewReminderCommand.Execute(null);
            vm.NewRow!.Editor!.Title = "Teste amanhã 9h";
            vm.NewRow!.Editor!.ShowNote = true;
            window.UpdateLayout();
            vm.NewRow!.Editor!.CancelCommand.Execute(null);

            foreach (var smart in vm.SmartLists)
            {
                vm.SelectedSmart = smart;
                window.UpdateLayout();
            }

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
    public void Inline_calendar_and_shared_styles_render()
    {
        RunSta(() =>
        {
            var listener = new BindingErrors();
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ThemeManager.Initialize();

            Style S(string key) => (Style)app.FindResource(key);
            var calendar = new Heimdall.UI.Controls.CalendarInline { SelectedDate = new DateOnly(2026, 10, 20) };
            var options = new ListBox { ItemContainerStyle = S("OptionRowStyle"), ItemsSource = new[] { "Nunca", "Diariamente" }, SelectedIndex = 1 };
            var panel = new StackPanel
            {
                Children =
                {
                    calendar,
                    new System.Windows.Controls.Primitives.ToggleButton { Style = S("SwitchToggleStyle"), IsChecked = true },
                    new Button { Style = S("ChipButtonStyle"), Content = "Hoje, 17:00", Tag = true },
                    new Button { Style = S("ChipIconButtonStyle"), Content = "…" },
                    new Button { Style = S("DetailRowStyle"), Content = "Repetir", Tag = "Nunca" },
                    new System.Windows.Controls.Primitives.ToggleButton { Style = S("DetailSwitchRowStyle"), Content = "Data", IsChecked = true },
                    new System.Windows.Controls.Primitives.ToggleButton { Style = S("DayCircleStyle"), Content = "S", IsChecked = true },
                    options,
                }
            };
            var host = new Window { Left = -10000, Top = -10000, ShowActivated = false, Width = 360, Height = 900, Content = panel };
            host.Show();
            host.UpdateLayout();

            calendar.SelectedDate = new DateOnly(2027, 1, 5);
            host.UpdateLayout();
            host.Close();

            Assert.Empty(listener.Errors);
        });
    }

    [Fact]
    public void Toasts_stack_in_the_corner_opposite_to_the_bar()
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
            var store = new ReminderStore(_ => { }, (_, _, _) => { }, TimeProvider.System);
            store.Attach(cfg);

            // Barra em cima → avisos no canto de baixo à direita, o mais novo encostado nele.
            var presenter = new Heimdall.UI.Reminders.ReminderToastPresenter(store, () => BarEdge.Top, () => { });
            presenter.Show(cfg.Reminders[0]);
            presenter.ShowGroup(cfg.Reminders.Take(3).ToList(), overdue: true);
            var toasts = presenter.OpenToasts.ToList();
            foreach (var t in toasts) t.UpdateLayout();

            var area = SystemParameters.WorkArea;
            Assert.Equal(2, toasts.Count);
            var newest = toasts[1];
            Assert.Equal(area.Bottom, newest.Top + newest.ActualHeight, 1);
            Assert.Equal(area.Right, newest.Left + newest.ActualWidth, 1);
            Assert.True(toasts[0].Top < newest.Top);
            Assert.False(newest.IsActive);

            foreach (var t in toasts) t.Close();
            Assert.Empty(listener.Errors);
        });
    }

    [Fact]
    public void Popup_anchored_to_a_detached_element_does_not_crash()
    {
        RunSta(() =>
        {
            _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ThemeManager.Initialize();

            // Âncora fora de qualquer janela visível (era o overlay escondido): abre perto do mouse.
            var popup = new Window { ShowActivated = false, Width = 200, Height = 100 };
            PopupPlacement.AnchorTo(popup, new Border(), BarEdge.Top);
            popup.Show();
            popup.UpdateLayout();
            popup.Close();
        });
    }
}
