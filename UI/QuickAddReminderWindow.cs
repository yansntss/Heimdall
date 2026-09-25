using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>
/// Popup de adição rápida de lembrete, ancorado à barra. Ao contrário da BarWindow
/// (NOACTIVATE), essa janela recebe foco de verdade — Enter salva, Esc fecha, perder
/// o foco fecha.
/// </summary>
internal sealed class QuickAddReminderWindow : Window
{
    private readonly TextBox _textBox;
    private bool _closing;

    public event Action<ReminderConfig>? Saved;

    public QuickAddReminderWindow(EffectiveStyle style)
    {
        Title = "Heimdall — Novo lembrete";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        // Fora da tela até a posição final ser calculada (evita "piscar" no canto errado)
        Left = -32000;
        Top = -32000;

        _textBox = new TextBox
        {
            Width = 220,
            FontSize = 13,
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CaretBrush = new SolidColorBrush(style.Foreground),
            Foreground = new SolidColorBrush(style.Foreground),
            FontFamily = new FontFamily(style.FontFamily)
        };
        _textBox.PreviewKeyDown += OnTextBoxKeyDown;

        var border = new Border
        {
            Background = new SolidColorBrush(style.Background),
            BorderBrush = new SolidColorBrush(style.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(style.CornerRadius),
            Padding = new Thickness(10, 8, 10, 8),
            Child = _textBox
        };
        Content = border;

        // Fechar já dispara Deactivated de novo como parte do próprio fechamento — sem a
        // guarda, isso chama Close() reentrante e o WPF derruba o app (VerifyNotClosing).
        Closing += (_, _) => _closing = true;
        Deactivated += (_, _) => { if (!_closing) Close(); };
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            Keyboard.Focus(_textBox);
        };
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Save();
        }
    }

    private void Save()
    {
        var text = _textBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            Close();
            return;
        }

        Saved?.Invoke(new ReminderConfig
        {
            Kind = ReminderKind.Fixed,
            Text = text,
            CreatedAt = DateTime.Now
        });
        Close();
    }

    /// <summary>Posiciona a janela ancorada à barra, no lado oposto à borda configurada.</summary>
    public void AnchorTo(FrameworkElement anchor, BarEdge edge)
    {
        Loaded += (_, _) =>
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
                    Left = anchorTopLeft.X;
                    Top = windowTopLeft.Y - ActualHeight - gap;
                    break;
                case BarEdge.Left:
                    Left = windowTopLeft.X + windowWidth + gap;
                    Top = anchorTopLeft.Y;
                    break;
                case BarEdge.Right:
                    Left = windowTopLeft.X - ActualWidth - gap;
                    Top = anchorTopLeft.Y;
                    break;
                default: // Top
                    Left = anchorTopLeft.X;
                    Top = windowTopLeft.Y + windowHeight + gap;
                    break;
            }
        };
    }
}
