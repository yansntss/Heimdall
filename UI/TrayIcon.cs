using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;
using Heimdall.UI.Menus;
using Heimdall.Widgets;
using Forms = System.Windows.Forms;

namespace Heimdall.UI;

/// <summary>
/// Ícone na bandeja do Windows (NotifyIcon do WinForms, que o WPF não tem) com o mesmo
/// menu estilizado da barra — o menu nativo do NotifyIcon não aceita tema. Clique
/// esquerdo ou direito abre o menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly ContextMenu _menu = new() { Placement = PlacementMode.MousePoint };

    public TrayIcon()
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Heimdall;component/Assets/heimdall.ico"))!.Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "Heimdall",
            Visible = true
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right) ShowMenu();
        };
    }

    private static App CurrentApp => (App)Application.Current;

    /// <summary>
    /// Mostrar barra · Modo overlay · Pausar lembretes · — · Configurações · Verificar
    /// atualizações (versão à direita) · — · Sair. Montado a cada abertura pros checks
    /// refletirem o estado atual.
    /// </summary>
    private void ShowMenu()
    {
        var app = CurrentApp;
        var cfg = ConfigService.Load();
        string version = UpdateService.CurrentVersion is { } v ? $"v{v.ToString(3)}" : "dev";

        _menu.Items.Clear();
        _menu.Items.Add(MenuFactory.Item(Strings.TrayShowBar, "H.Icon.Eye", app.ToggleBarsVisibility, "Ctrl+Alt+H", isChecked: app.AreBarsVisible));
        _menu.Items.Add(MenuFactory.Item(Strings.MenuOverlayMode, "H.Icon.Gamepad", app.ToggleGamingMode, isChecked: cfg.GamingMode));
        _menu.Items.Add(MenuFactory.Item(Strings.MenuOpenReminders, "H.Icon.ListChecks", app.OpenReminders));
        _menu.Items.Add(MenuFactory.Item(Strings.TrayPauseReminders, "H.Icon.BellOff",
            () => ReminderStore.Instance.Paused = !ReminderStore.Instance.Paused, isChecked: ReminderStore.Instance.Paused));
        _menu.Items.Add(MenuFactory.Separator());
        _menu.Items.Add(MenuFactory.Item(Strings.MenuSettingsShort, "H.Icon.Settings", () => app.OpenSettings()));
        _menu.Items.Add(MenuFactory.Item(Strings.TrayCheckUpdates, "H.Icon.Download", () => _ = CheckUpdatesAsync(version), version));
        _menu.Items.Add(MenuFactory.Separator());
        _menu.Items.Add(MenuFactory.Item(Strings.MenuExitApp, "H.Icon.LogOut", app.ExitApp, danger: true));

        _menu.IsOpen = true;
    }

    /// <summary>Versão nova → abre a página da release; senão avisa num balão que já está atualizado.</summary>
    private async Task CheckUpdatesAsync(string version)
    {
        await UpdateService.CheckAsync();
        if (UpdateService.Available is not null) UpdateService.OpenReleasePage();
        else _icon.ShowBalloonTip(3000, "Heimdall", Strings.TrayUpToDate(version), Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _menu.IsOpen = false;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
