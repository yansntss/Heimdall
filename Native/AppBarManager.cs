using System.Runtime.InteropServices;
using System.Windows.Threading;
using InfoBar.Config;
using InfoBar.Services;
using static InfoBar.Native.NativeMethods;

namespace InfoBar.Native;

/// <summary>
/// Registra a janela como AppBar: o Windows reserva a faixa da borda
/// e janelas maximizadas não ficam por baixo da barra.
/// </summary>
internal sealed class AppBarManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly MonitorInfo _monitor;
    private readonly BarEdge _edge;
    private readonly int _thicknessDip;
    private readonly int _callbackMsg;
    private readonly int _taskbarCreatedMsg;
    private bool _registered;
    private bool _positioning;

    /// <summary>true = um app em tela cheia abriu; false = fechou.</summary>
    public event Action<bool>? FullscreenChanged;

    /// <summary>Quando false, reposiciona sem forçar "sempre no topo".</summary>
    public bool KeepTopmost { get; set; } = true;

    public AppBarManager(IntPtr hwnd, MonitorInfo monitor, BarEdge edge, int thicknessDip)
    {
        _hwnd = hwnd;
        _monitor = monitor;
        _edge = edge;
        _thicknessDip = thicknessDip;
        _callbackMsg = (int)RegisterWindowMessage("InfoBar.AppBarCallback");
        // Enviada quando o Explorer reinicia: registros de AppBar são perdidos
        _taskbarCreatedMsg = (int)RegisterWindowMessage("TaskbarCreated");
    }

    public void Register()
    {
        var abd = NewData();
        abd.uCallbackMessage = _callbackMsg;
        SHAppBarMessage(ABM_NEW, ref abd);
        _registered = true;
        SetPosition();
    }

    public void SetPosition()
    {
        if (!_registered || _positioning) return;
        _positioning = true;
        try
        {
            int thickness = (int)Math.Round(_thicknessDip * GetScale());

            var abd = NewData();
            abd.uEdge = ToAbe(_edge);
            abd.rc = _monitor.Bounds;
            ApplyThickness(ref abd.rc, thickness);

            // Sistema ajusta o retângulo considerando taskbar/outras AppBars
            SHAppBarMessage(ABM_QUERYPOS, ref abd);
            ApplyThickness(ref abd.rc, thickness);
            SHAppBarMessage(ABM_SETPOS, ref abd);

            uint flags = SWP_NOACTIVATE | (KeepTopmost ? 0 : SWP_NOZORDER);
            SetWindowPos(_hwnd, KeepTopmost ? HWND_TOPMOST : IntPtr.Zero,
                abd.rc.Left, abd.rc.Top, abd.rc.Width, abd.rc.Height, flags);
        }
        finally
        {
            _positioning = false;
        }
    }

    public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _callbackMsg)
        {
            switch (wParam.ToInt32())
            {
                case ABN_POSCHANGED:
                    SetPosition();
                    break;
                case ABN_FULLSCREENAPP:
                    bool fullscreen = lParam != IntPtr.Zero;
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => FullscreenChanged?.Invoke(fullscreen)));
                    break;
            }
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == _taskbarCreatedMsg)
        {
            _registered = false;
            Register();
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case WM_ACTIVATE:
            {
                var abd = NewData();
                SHAppBarMessage(ABM_ACTIVATE, ref abd);
                break;
            }
            case WM_WINDOWPOSCHANGED:
            {
                var abd = NewData();
                SHAppBarMessage(ABM_WINDOWPOSCHANGED, ref abd);
                break;
            }
            case WM_DPICHANGED:
                // O WPF aplica o retângulo sugerido; depois recalculamos a espessura no novo DPI
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(SetPosition));
                break;
        }

        return IntPtr.Zero;
    }

    private double GetScale()
    {
        if (GetDpiForMonitor(_monitor.Handle, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
            return dpiX / 96.0;

        uint windowDpi = GetDpiForWindow(_hwnd);
        return windowDpi > 0 ? windowDpi / 96.0 : 1.0;
    }

    private void ApplyThickness(ref RECT rc, int t)
    {
        switch (_edge)
        {
            case BarEdge.Top: rc.Bottom = rc.Top + t; break;
            case BarEdge.Bottom: rc.Top = rc.Bottom - t; break;
            case BarEdge.Left: rc.Right = rc.Left + t; break;
            case BarEdge.Right: rc.Left = rc.Right - t; break;
        }
    }

    private static int ToAbe(BarEdge edge) => edge switch
    {
        BarEdge.Left => ABE_LEFT,
        BarEdge.Right => ABE_RIGHT,
        BarEdge.Bottom => ABE_BOTTOM,
        _ => ABE_TOP
    };

    private APPBARDATA NewData() => new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd
    };

    public void Dispose()
    {
        if (!_registered) return;
        var abd = NewData();
        SHAppBarMessage(ABM_REMOVE, ref abd);
        _registered = false;
    }
}
