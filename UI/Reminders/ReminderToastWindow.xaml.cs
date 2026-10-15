using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Heimdall.Native;
using Heimdall.ViewModels;

namespace Heimdall.UI.Reminders;

/// <summary>
/// O aviso de lembrete do Heimdall (não o toast do Windows, pra manter o visual). Nunca
/// rouba o foco (WS_EX_NOACTIVATE) e fica por cima de tudo — inclusive de jogo em tela
/// cheia sem bordas, junto com o overlay. Aqui só o visual: entrada/saída (slide + fade de
/// 220ms) e o tempo até sumir sozinho, que pausa com o mouse em cima.
/// </summary>
public partial class ReminderToastWindow : Window
{
    private static readonly Duration AnimationDuration = TimeSpan.FromMilliseconds(220);
    private const double SlideDistance = 24;

    private readonly ReminderToastViewModel _vm;
    private readonly DispatcherTimer _dismissTimer = new() { Interval = ReminderToastViewModel.AutoDismissAfter };
    private bool _closing;

    /// <param name="slideFromBelow">Aviso no canto de baixo entra subindo; no de cima, descendo.</param>
    internal ReminderToastWindow(ReminderToastViewModel vm, bool slideFromBelow)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        SlideFrom = slideFromBelow ? SlideDistance : -SlideDistance;
        Opacity = 0;

        vm.CloseRequested += AnimateOut;
        _dismissTimer.Tick += (_, _) => AnimateOut();
        MouseEnter += (_, _) => _dismissTimer.Stop();
        MouseLeave += (_, _) => { if (vm.AutoDismiss && !_closing) _dismissTimer.Start(); };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.SetExStyle(hwnd,
                add: NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
                remove: NativeMethods.WS_EX_APPWINDOW);
        };
        Loaded += (_, _) => AnimateIn();
    }

    private double SlideFrom { get; }

    /// <summary>Altura do cartão visível (sem a margem da sombra) — pro empilhamento.</summary>
    internal double CardHeight => ActualHeight;

    private void AnimateIn()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(SlideFrom, 0, AnimationDuration) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, AnimationDuration) { EasingFunction = ease });
        if (_vm.AutoDismiss) _dismissTimer.Start();

        // Leitor de tela: anuncia o aviso sem precisar de foco.
        if (UIElementAutomationPeer.CreatePeerForElement(this) is { } peer)
            peer.RaiseNotificationEvent(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent,
                _vm.AccessibleName, "HeimdallReminder");
    }

    private void AnimateOut()
    {
        if (_closing) return;
        _closing = true;
        _dismissTimer.Stop();

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(1, 0, AnimationDuration) { EasingFunction = ease };
        fade.Completed += (_, _) => Close();
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(0, SlideFrom, AnimationDuration) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.CloseRequested -= AnimateOut;
        _dismissTimer.Stop();
        base.OnClosed(e);
    }
}
