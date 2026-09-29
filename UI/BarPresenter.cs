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
    private readonly AppConfig _cfg;
    private readonly BarWindow _bar;
    private readonly OverlayWindow _overlay;
    private readonly MonitorInfo _monitor;

    /// <summary>true enquanto a barra normal está fora do ar por causa de tela cheia — seja em overlay (GamingMode) ou só escondida.</summary>
    private bool _fullscreenActive;

    /// <summary>true depois do <see cref="Close"/> — callbacks atrasados (fim de fade, FullscreenChanged) viram no-op.</summary>
    private bool _closed;

    public BarPresenter(AppConfig cfg, MonitorInfo monitor)
    {
        _cfg = cfg;
        _monitor = monitor;
        _bar = new BarWindow(cfg, monitor);
        _overlay = new OverlayWindow(cfg, monitor);
        _bar.FullscreenChanged += OnFullscreenChanged;
    }

    public void Show() => _bar.Show();

    public void Close()
    {
        _closed = true;
        _bar.FullscreenChanged -= OnFullscreenChanged;
        _overlay.Close();
        _bar.Close();
    }

    /// <summary>Esconde/mostra a janela ativa no momento (bar ou overlay) — usado pelo hotkey global.</summary>
    public void SetHidden(bool hidden)
    {
        var active = ActiveWindow();
        active.Visibility = hidden ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
    }

    public bool IsVisible => ActiveWindow().Visibility == System.Windows.Visibility.Visible;

    /// <summary>Janela "de verdade" no momento: overlay se o GamingMode estiver ativo e a tela cheia tiver disparado; a própria barra caso contrário (inclusive quando ela está só escondida, sem GamingMode).</summary>
    private System.Windows.Window ActiveWindow() =>
        _fullscreenActive && _cfg.GamingMode ? _overlay : _bar;

    private const int FadeMs = 200;

    private void OnFullscreenChanged(bool fullscreen)
    {
        if (_closed || fullscreen == _fullscreenActive) return;
        _fullscreenActive = fullscreen;

        if (_cfg.GamingMode) OnFullscreenChangedGaming(fullscreen);
        else OnFullscreenChangedPlain(fullscreen);
    }

    /// <summary>GamingMode = true (comportamento original): overlay transparente com clique atravessando.</summary>
    private void OnFullscreenChangedGaming(bool fullscreen)
    {
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

    /// <summary>GamingMode = false: a barra simplesmente some — sem overlay, sem transparência, sem clique atravessando.</summary>
    private void OnFullscreenChangedPlain(bool fullscreen)
    {
        if (fullscreen)
        {
            FadeOut(_bar, () =>
            {
                _bar.AppBar?.Unregister();
                _bar.Hide();
            });
        }
        else
        {
            _bar.Show();
            _bar.AppBar?.Register();
            FadeIn(_bar);
        }
    }

    /// <summary>Fade de 200 ms na troca barra↔overlay — sem isso a troca era um corte seco (Hide/Show instantâneo).</summary>
    /// <remarks>
    /// O fade pode terminar depois que as barras foram reconstruídas (jogo em tela cheia muda
    /// a resolução → DisplaySettingsChanged → BuildBars fecha estas janelas no meio da
    /// animação) — Show() numa janela já fechada derrubava o app. Daí o guard de _closed.
    /// </remarks>
    private void FadeOut(Window window, Action onCompleted)
    {
        var animation = new DoubleAnimation(window.Opacity, 0, TimeSpan.FromMilliseconds(FadeMs));
        animation.Completed += (_, _) =>
        {
            if (!_closed) onCompleted();
        };
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void FadeIn(Window window)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FadeMs));
        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
