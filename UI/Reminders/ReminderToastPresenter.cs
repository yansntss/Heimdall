using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.UI.Reminders;

/// <summary>
/// Mostra os avisos do agendador: no canto oposto à borda da barra (barra em cima → canto
/// de baixo à direita; embaixo → em cima à direita; à direita → embaixo à esquerda), o mais
/// novo encostado no canto e os anteriores empilhados. No máximo 3 na tela.
/// </summary>
internal sealed class ReminderToastPresenter : IReminderNotifier
{
    private const int MaxVisible = 3;

    private readonly ReminderStore _store;
    private readonly Func<BarEdge> _edge;
    private readonly Func<IntPtr> _monitor;
    private readonly Action _openReminders;
    private readonly List<ReminderToastWindow> _open = new();

    /// <param name="monitor">HMONITOR da barra; <see cref="IntPtr.Zero"/> usa o monitor principal.</param>
    public ReminderToastPresenter(ReminderStore store, Func<BarEdge> edge, Func<IntPtr> monitor, Action openReminders)
    {
        _store = store;
        _edge = edge;
        _monitor = monitor;
        _openReminders = openReminders;
    }

    public bool IsQuiet => FocusAssist.IsQuiet;

    /// <summary>Avisos na tela, do mais antigo ao mais novo.</summary>
    internal IReadOnlyList<ReminderToastWindow> OpenToasts => _open;

    public void Show(ReminderConfig reminder) =>
        Present(new ReminderToastViewModel(reminder, _store, _openReminders));

    public void ShowGroup(IReadOnlyList<ReminderConfig> reminders, bool overdue) =>
        Present(new ReminderToastViewModel(reminders, overdue, _store, _openReminders));

    private void Present(ReminderToastViewModel vm)
    {
        if (_open.Count >= MaxVisible) _open[0].Close();

        var edge = _edge();
        var window = new ReminderToastWindow(vm, slideFromBelow: edge != BarEdge.Bottom)
        {
            // Fora da tela até saber a altura (SizeToContent) e entrar no lugar certo.
            Left = -32000,
            Top = -32000
        };
        window.SizeChanged += (_, _) => Relayout();
        // Depois do WPF aplicar a escala nova.
        window.DpiChanged += (_, _) => window.Dispatcher.BeginInvoke(Relayout, DispatcherPriority.Loaded);
        window.Closed += (_, _) =>
        {
            _open.Remove(window);
            Relayout();
        };
        _open.Add(window);
        window.Show();

        if (_store.Options.PlaySound) SystemSounds.Asterisk.Play();
    }

    /// <summary>A margem da sombra já separa um aviso do outro; tira um pouco pra não ficarem soltos demais.</summary>
    private const double StackOverlapDip = 22;

    private void Relayout()
    {
        var edge = _edge();
        bool top = edge == BarEdge.Bottom;
        bool left = edge == BarEdge.Right;

        if (TryGetWorkArea(_monitor(), out var work, out double scale))
        {
            LayoutInPixels(work, scale, top, left);
            return;
        }

        var area = SystemParameters.WorkArea;
        double offset = 0;
        // Mais novo no canto; os anteriores vão se afastando dele.
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var window = _open[i];
            if (window.ActualHeight <= 0) continue;

            window.Left = left ? area.Left : area.Right - window.ActualWidth;
            window.Top = top ? area.Top + offset : area.Bottom - offset - window.ActualHeight;
            offset += window.ActualHeight - StackOverlapDip;
        }
    }

    /// <summary>
    /// Em pixels físicos, como a AppBar: com DPI por monitor, Left/Top em DIP não acertam um
    /// monitor que não é o principal. Ao cair num monitor de outro DPI o tamanho em pixels
    /// muda (em DIP não) — o DpiChanged chama de novo e ele se ajusta.
    /// </summary>
    private void LayoutInPixels(NativeMethods.RECT work, double scale, bool top, bool left)
    {
        int offset = 0;
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var window = _open[i];
            if (window.ActualHeight <= 0) continue;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) continue;

            // Tamanho pelo layout do WPF: no SizeChanged o HWND ainda está com o tamanho antigo.
            var dpi = VisualTreeHelper.GetDpi(window);
            int width = (int)Math.Round(window.ActualWidth * dpi.DpiScaleX);
            int height = (int)Math.Round(window.ActualHeight * dpi.DpiScaleY);
            int x = left ? work.Left : work.Right - width;
            int y = top ? work.Top + offset : work.Bottom - offset - height;
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            offset += height - (int)Math.Round(StackOverlapDip * scale);
        }
    }

    /// <summary>Área de trabalho do monitor (já sem a barra) e a escala dele (1,0 = 96 dpi).</summary>
    private static bool TryGetWorkArea(IntPtr monitor, out NativeMethods.RECT work, out double scale)
    {
        work = default;
        scale = 1;
        if (monitor == IntPtr.Zero) return false;

        var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false; // monitor desconectado
        work = info.rcWork;
        if (NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 && dpi > 0)
            scale = dpi / 96.0;
        return true;
    }
}
