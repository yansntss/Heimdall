using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Heimdall.Native;

namespace Heimdall.UI;

/// <summary>
/// Janela pequena, transparente, topmost e click-through que segue o cursor durante o
/// arraste de um ícone/separador do launcher — sai dos limites da barra livremente, o
/// que uma animação via RenderTransform dentro da própria barra não conseguiria fazer.
/// </summary>
internal sealed class GhostIconWindow : Window
{
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Border _host;
    private readonly Border _removalBadge;

    public GhostIconWindow(FrameworkElement visual, double size) : this(visual, size, size) { }

    /// <summary>Largura/altura independentes — widgets (relógio, mídia...) não são quadrados como os ícones do launcher.</summary>
    public GhostIconWindow(FrameworkElement visual, double width, double height)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Width = width;
        Height = height;

        // Levemente translúcida de propósito: ícones/widgets ficam pequenos e próximos
        // uns dos outros, e o fantasma (que segue o cursor colado neles, ainda mais
        // "grande" por causa do PickupScale) senão cobre por completo o vizinho que está
        // por baixo — dava a impressão de que ele tinha sumido, mesmo sem nada no código
        // mexendo em Visibility/Opacity dos vizinhos (isso nunca acontece, é só o fantasma
        // opaco por cima). Opacity baixo o bastante pra dar pra notar o que está atrás,
        // alto o bastante pra continuar parecendo "sólido"/pego na mão.
        Opacity = 0.85;

        _host = new Border
        {
            Width = width,
            Height = height,
            Child = visual,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _scale
        };

        _removalBadge = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Background = Brushes.Firebrick,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -4, -4, 0),
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "✕",
                FontSize = 9,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            }
        };

        var overlay = new Grid();
        overlay.Children.Add(_host);
        overlay.Children.Add(_removalBadge);
        Content = overlay;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetExStyle(hwnd,
                add: NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE
                     | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);
        };
    }

    /// <summary>Centraliza a janela num ponto de tela (o cursor, tipicamente) sem animação — chamado a cada movimento do mouse.</summary>
    public void CenterOn(Point screenPoint)
    {
        Left = screenPoint.X - Width / 2;
        Top = screenPoint.Y - Height / 2;
    }

    public void AnimatePickup(bool animate, double scaleUpTo)
    {
        _host.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0, Color = Colors.Black };

        if (!animate)
        {
            _scale.ScaleX = _scale.ScaleY = scaleUpTo;
            ((DropShadowEffect)_host.Effect).Opacity = 0.45;
            return;
        }

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var scaleAnim = new DoubleAnimation(1, scaleUpTo, TimeSpan.FromMilliseconds(LauncherDragAnimations.PickupMs)) { EasingFunction = ease };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        _host.Effect.BeginAnimation(DropShadowEffect.OpacityProperty,
            new DoubleAnimation(0, 0.45, TimeSpan.FromMilliseconds(LauncherDragAnimations.PickupMs)));
    }

    public void SetRemovalHint(bool active)
    {
        _host.Opacity = active ? 0.4 : 1.0;
        _removalBadge.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// "Voa" até um ponto de tela e volta à escala normal — usado tanto pra soltar no
    /// lugar certo quanto pra cancelar (voltando pra posição original). Window.Left/Top
    /// não anima bem via BeginAnimation normal (é uma janela de verdade, não um
    /// RenderTransform) — CompositionTarget.Rendering interpola manualmente, quadro a
    /// quadro, reaproveitando a curva de EasingFunctionBase.Ease() em vez de reinventar.
    /// </summary>
    public void FlyTo(Point targetScreenCenter, bool animate, Action onCompleted)
    {
        if (!animate)
        {
            CenterOn(targetScreenCenter);
            _scale.ScaleX = _scale.ScaleY = 1;
            onCompleted();
            return;
        }

        double startLeft = Left, startTop = Top, startScale = _scale.ScaleX;
        double targetLeft = targetScreenCenter.X - Width / 2, targetTop = targetScreenCenter.Y - Height / 2;
        var easing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
        var clock = System.Diagnostics.Stopwatch.StartNew();

        EventHandler? tick = null;
        tick = (_, _) =>
        {
            double t = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / LauncherDragAnimations.DropMs);
            double e = easing.Ease(t);
            Left = startLeft + (targetLeft - startLeft) * e;
            Top = startTop + (targetTop - startTop) * e;
            _scale.ScaleX = _scale.ScaleY = startScale + (1 - startScale) * e;

            if (t >= 1.0)
            {
                CompositionTarget.Rendering -= tick;
                onCompleted();
            }
        };
        CompositionTarget.Rendering += tick;
    }

    /// <summary>Some (escala pra 0 + fade) — usado ao soltar fora da barra pra remover o atalho.</summary>
    public void Vanish(bool animate, Action onCompleted)
    {
        if (!animate)
        {
            onCompleted();
            return;
        }

        var duration = TimeSpan.FromMilliseconds(LauncherDragAnimations.RemoveMs);
        var scaleAnim = new DoubleAnimation(_scale.ScaleX, 0, duration);
        var fadeAnim = new DoubleAnimation(Opacity, 0, duration);
        fadeAnim.Completed += (_, _) => onCompleted();
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        BeginAnimation(OpacityProperty, fadeAnim);
    }
}

/// <summary>Durações num só lugar, como pedido — pra ajustar depois sem caçar números espalhados.</summary>
internal static class LauncherDragAnimations
{
    public const int PickupMs = 120;
    public const int SlideMs = 180;
    public const int DropMs = 200;
    public const int RemoveMs = 150;
    public const double PickupScale = 1.15;
}
