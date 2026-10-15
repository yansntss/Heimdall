using Heimdall.Interop;

namespace Heimdall.Native;

/// <summary>Backdrop (acrílico) e cantos arredondados via DWM numa janela não-layered.</summary>
internal static class DwmVisuals
{
    public static void Apply(IntPtr hwnd, bool acrylic, bool roundedCorners)
    {
        int preference = roundedCorners ? NativeMethods.DWMWCP_ROUND : NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));

        // Sólido fica 100% fora do DWM (renderização opaca comum).
        if (acrylic) AcrylicInterop.Enable(hwnd);
        else AcrylicInterop.Disable(hwnd);
    }
}
