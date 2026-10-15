using System.Windows;
using Heimdall.Config;
using Heimdall.Services.Reminders;
using Heimdall.UI;
using Heimdall.Widgets;

namespace Heimdall.Services;

/// <summary>Ações de interface sobre lembretes disparadas fora do widget de lembretes (sino, atalho global).</summary>
internal static class ReminderActions
{
    /// <summary>Adição rápida: pelo widget de lembretes se ele estiver na barra (popup ancorado nele), senão um popup ancorado em <paramref name="anchor"/>.</summary>
    public static void OpenQuickAdd(AppConfig cfg, FrameworkElement anchor)
    {
        if (ReminderWidget.Primary is { } widget)
        {
            widget.OpenQuickAdd();
            return;
        }

        var window = new QuickAddReminderWindow(ThemeService.GetEffectiveStyle(cfg));
        window.AnchorTo(anchor, cfg.Edge);
        window.Saved += ReminderStore.Instance.Add;
        window.Show();
        window.Activate();
    }
}
