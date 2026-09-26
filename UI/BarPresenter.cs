using System.Windows;
using System.Windows.Media.Animation;
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

    private const int FadeMs = 200;

    private void OnFullscreenChanged(bool fullscreen)
    {
        if (fullscreen == _inOverlay) return;
        _inOverlay = fullscreen;

        if (fullscreen)
        {
            FadeOut(_bar, () =>
            {
                _bar.AppBar?.Unregister();
                _bar.Hide();
                _overlay.ShowOnMonitor(_monitor);
                FadeIn(_overlay);
            });
        }
        else
        {
            FadeOut(_overlay, () =>
            {
                _overlay.HideAway();
                _bar.Show();
                _bar.AppBar?.Register();
                FadeIn(_bar);
            });
        }
    }

    /// <summary>Fade de 200 ms na troca barra↔overlay — sem isso a troca era um corte seco (Hide/Show instantâneo).</summary>
    private static void FadeOut(Window window, Action onCompleted)
    {
        var animation = new DoubleAnimation(window.Opacity, 0, TimeSpan.FromMilliseconds(FadeMs));
        animation.Completed += (_, _) => onCompleted();
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void FadeIn(Window window)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeMs));
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
