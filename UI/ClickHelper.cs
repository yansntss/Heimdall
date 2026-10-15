using System.Windows;
using System.Windows.Input;

namespace Heimdall.UI;

/// <summary>
/// Clique "de verdade" num elemento que também pode ser arrastado pela barra (o slot do
/// widget): só dispara se soltar no mesmo lugar, dentro do elemento. O WidgetDragController
/// captura o mouse no wrapper já no MouseDown (bolha), então o MouseUp nunca chega ao
/// elemento — mas o PreviewMouseUp da janela sempre passa (túnel da raiz até quem tem a
/// captura), e é nele que a soltura é lida.
/// </summary>
internal static class ClickHelper
{
    public static void Attach(FrameworkElement element, Action onClick)
    {
        element.MouseLeftButtonDown += (_, down) =>
        {
            if (Window.GetWindow(element) is not { } window) return;
            var press = down.GetPosition(element);

            void OnUp(object sender, MouseButtonEventArgs up)
            {
                window.PreviewMouseLeftButtonUp -= OnUp;

                var pos = up.GetPosition(element);
                if (Math.Abs(pos.X - press.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(pos.Y - press.Y) >= SystemParameters.MinimumVerticalDragDistance ||
                    pos.X < 0 || pos.Y < 0 || pos.X > element.ActualWidth || pos.Y > element.ActualHeight)
                    return;

                // Depois do MouseUp terminar de rotear: o wrapper solta a captura nele.
                element.Dispatcher.BeginInvoke(onClick);
            }

            window.PreviewMouseLeftButtonUp += OnUp;
        };
    }
}
