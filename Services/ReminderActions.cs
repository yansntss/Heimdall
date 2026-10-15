using System.Windows;
using Heimdall.Config;
using Heimdall.Services.Reminders;
using Heimdall.UI;
using Heimdall.Widgets;

namespace Heimdall.Services;

/// <summary>Ações sobre lembretes disparadas fora do widget de lembretes (lista de hoje do sino).</summary>
internal static class ReminderActions
{
    /// <summary>
    /// Conclui pela lista de hoje, com o mesmo efeito do "Concluir" do chip: grava no
    /// histórico; recorrente avança pra próxima ocorrência, o resto fica concluído.
    /// </summary>
    public static void Complete(AppConfig cfg, ReminderConfig reminder)
    {
        var now = DateTimeOffset.Now;
        ReminderHistoryService.AppendCompleted(reminder.Title, reminder.CreatedAt, now.LocalDateTime);
        ReminderSchedule.Complete(reminder, now);

        ConfigService.Save(cfg);
        ReminderWidget.Primary?.Refresh();
        ReminderAgenda.NotifyChanged();
    }

    /// <summary>Adição rápida: pelo widget de lembretes se ele estiver na barra (mantém os chips em dia), senão um popup ancorado em <paramref name="anchor"/>.</summary>
    public static void OpenQuickAdd(AppConfig cfg, FrameworkElement anchor)
    {
        if (ReminderWidget.Primary is { } widget)
        {
            widget.OpenQuickAdd();
            return;
        }

        var window = new QuickAddReminderWindow(ThemeService.GetEffectiveStyle(cfg));
        window.AnchorTo(anchor, cfg.Edge);
        window.Saved += reminder =>
        {
            cfg.Reminders.Add(reminder);
            ConfigService.Save(cfg);
            ReminderAgenda.NotifyChanged();
        };
        window.Show();
        window.Activate();
    }
}
