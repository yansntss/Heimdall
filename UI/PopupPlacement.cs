using System.Windows;
using Heimdall.Config;

namespace Heimdall.UI;

/// <summary>Posiciona popups (lembrete rápido, calendário) ancorados à barra, no lado oposto à borda configurada.</summary>
internal static class PopupPlacement
{
    public static void AnchorTo(Window popup, FrameworkElement anchor, BarEdge edge)
    {
        popup.Loaded += (_, _) =>
        {
            // X/Y vêm do widget (abre perto de onde ele está na barra), mas a extensão
            // vertical/horizontal vem da JANELA da barra inteira — usar só o
            // ActualHeight/Width do widget deixaria o popup uns pixels dentro da barra
            // (o widget é menor que a espessura total, fica centralizado nela).
            var window = Window.GetWindow(anchor);
            var anchorTopLeft = anchor.PointToScreen(new Point(0, 0));
            var windowTopLeft = window?.PointToScreen(new Point(0, 0)) ?? anchorTopLeft;
            double windowWidth = window?.ActualWidth ?? anchor.ActualWidth;
            double windowHeight = window?.ActualHeight ?? anchor.ActualHeight;

            const double gap = 4;
            switch (edge)
            {
                case BarEdge.Bottom:
                    popup.Left = anchorTopLeft.X;
                    popup.Top = windowTopLeft.Y - popup.ActualHeight - gap;
                    break;
                case BarEdge.Left:
                    popup.Left = windowTopLeft.X + windowWidth + gap;
                    popup.Top = anchorTopLeft.Y;
                    break;
                case BarEdge.Right:
                    popup.Left = windowTopLeft.X - popup.ActualWidth - gap;
                    popup.Top = anchorTopLeft.Y;
                    break;
                default: // Top
                    popup.Left = anchorTopLeft.X;
                    popup.Top = windowTopLeft.Y + windowHeight + gap;
                    break;
            }

            // Widgets perto de uma ponta da barra — sem isso, o popup passava da borda do
            // monitor (renderiza, mas fica invisível, fora de qualquer tela física).
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)anchorTopLeft.X, (int)anchorTopLeft.Y));
            var screenBounds = screen.Bounds;
            popup.Left = Math.Max(screenBounds.Left, Math.Min(popup.Left, screenBounds.Right - popup.ActualWidth));
            popup.Top = Math.Max(screenBounds.Top, Math.Min(popup.Top, screenBounds.Bottom - popup.ActualHeight));
        };
    }
}
