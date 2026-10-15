using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Heimdall.Native;

namespace Heimdall.Interop;

/// <summary>
/// Acrílico (desfoque do que está atrás) numa janela WPF comum, via
/// SetWindowCompositionAttribute — funciona no Windows 10 1803+ e no 11, e não depende da
/// janela estar ativa (a barra é WS_EX_NOACTIVATE, nunca fica ativa, e o backdrop de
/// sistema do Win11 cai pro visual "inativo" sem desfoque nesse caso). A tinta fica toda
/// no WPF (fundo H.Brush.BarBg com a opacidade do tema); aqui só liga o desfoque.
/// </summary>
internal static class AcrylicInterop
{
    private enum AccentState
    {
        Disabled = 0,
        EnableBlurBehind = 3,
        EnableAcrylicBlurBehind = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor; // AABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int WCA_ACCENT_POLICY = 19;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>Liga o desfoque. A janela precisa ter Background transparente no WPF.</summary>
    public static void Enable(IntPtr hwnd)
    {
        if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
            target.BackgroundColor = Colors.Transparent;

        // Sem estender o frame o DWM ignora o alfa do conteúdo WPF e as partes
        // translúcidas viram preto.
        var margins = new NativeMethods.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Tinta quase transparente: a cor de verdade vem do fundo WPF por cima.
        SetAccent(hwnd, AccentState.EnableAcrylicBlurBehind, gradientColor: 0x01000000);
    }

    public static void Disable(IntPtr hwnd) => SetAccent(hwnd, AccentState.Disabled, 0);

    private static void SetAccent(IntPtr hwnd, AccentState state, uint gradientColor)
    {
        var policy = new AccentPolicy { AccentState = state, AccentFlags = 2, GradientColor = gradientColor };
        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData { Attribute = WCA_ACCENT_POLICY, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
