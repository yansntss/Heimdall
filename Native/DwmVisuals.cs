using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.Native;

/// <summary>Aplica backdrop (Acrílico/Mica/Translúcido) e cantos arredondados via DWM numa janela não-layered.</summary>
internal static class DwmVisuals
{
    // Acrylic/Mica via DWMWA_SYSTEMBACKDROP_TYPE só é confiável a partir do Win11 22H2.
    private static readonly bool SupportsSystemBackdrop = Environment.OSVersion.Version.Build >= 22621;

    public static void Apply(IntPtr hwnd, EffectiveStyle style, bool forceRoundedCorners = false)
    {
        ApplyCornerPreference(hwnd, forceRoundedCorners || style.CornerRadius > 0);

        // "Solid" fica 100% fora do DWM (renderização opaca comum) — extend-frame
        // sem um backdrop de verdade pode deixar a janela inteira invisível.
        if (style.Backdrop == BackdropType.Solid) return;

        var margins = new NativeMethods.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        int backdrop = (style.Backdrop, SupportsSystemBackdrop) switch
        {
            (BackdropType.Mica, true) => NativeMethods.DWMSBT_MAINWINDOW,
            (BackdropType.Acrylic, true) => NativeMethods.DWMSBT_TRANSIENTWINDOW,
            _ => NativeMethods.DWMSBT_NONE
        };
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }

    private static void ApplyCornerPreference(IntPtr hwnd, bool rounded)
    {
        int preference = rounded ? NativeMethods.DWMWCP_ROUND : NativeMethods.DWMWCP_DONOTROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }
}
