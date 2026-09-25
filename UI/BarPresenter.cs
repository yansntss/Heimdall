using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>
/// Dono das duas janelas de um monitor — a <see cref="BarWindow"/> (modo normal, AppBar)
/// e a <see cref="OverlayWindow"/> (modo tela cheia) — e da troca entre elas.
/// </summary>
internal sealed class BarPresenter
{
    private readonly BarWindow _bar;
    private readonly OverlayWindow _overlay;
    private readonly MonitorInfo _monitor;
    private bool _inOverlay;

    public BarPresenter(AppConfig cfg, MonitorInfo monitor)
    {
        _monitor = monitor;
        _bar = new BarWindow(cfg, monitor);
        _overlay = new OverlayWindow(cfg, monitor);
        _bar.FullscreenChanged += OnFullscreenChanged;
    }

    public void Show() => _bar.Show();

    public void Close()
    {
        _overlay.Close();
        _bar.Close();
    }

    /// <summary>Esconde/mostra a janela ativa no momento (bar ou overlay) — usado pelo hotkey global.</summary>
    public void SetHidden(bool hidden)
    {
        var active = _inOverlay ? (System.Windows.Window)_overlay : _bar;
        active.Visibility = hidden ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
    }

    public bool IsVisible => (_inOverlay ? (System.Windows.Window)_overlay : _bar).Visibility == System.Windows.Visibility.Visible;

    private void OnFullscreenChanged(bool fullscreen)
    {
        if (fullscreen == _inOverlay) return;
        _inOverlay = fullscreen;

        if (fullscreen)
        {
            _bar.AppBar?.Unregister();
            _bar.Hide();
            _overlay.ShowOnMonitor(_monitor);
        }
        else
        {
            _overlay.HideAway();
            _bar.Show();
            _bar.AppBar?.Register();
        }
    }
}
