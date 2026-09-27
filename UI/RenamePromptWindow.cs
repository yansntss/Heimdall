using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>Popup mínimo pra renomear um item — Enter confirma, Esc cancela, perder o foco confirma (mesmo padrão do popup de lembrete).</summary>
internal sealed class RenamePromptWindow : Window
{
    private readonly TextBox _textBox;
    private bool _closing;
    private bool _cancelled;
    private bool _everActivated;

    public event Action<string>? Confirmed;

    public RenamePromptWindow(EffectiveStyle style, string currentName)
    {
        Title = Strings.RenameTitle;
        Icon = AppIcon.Source;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Left = -32000;
        Top = -32000;

        _textBox = new TextBox
        {
            Width = 220,
            FontSize = 13,
            Padding = new Thickness(4, 2, 4, 2),
            Background = Brushes.White,
            Foreground = Brushes.Black,
            FontFamily = new FontFamily(style.FontFamily),
            Text = currentName
        };

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

        Closing += (_, _) => _closing = true;
        // Mesmo problema do popup de lembrete: aberta a partir de um clique no menu de
        // contexto da barra (WS_EX_NOACTIVATE), a ativação real às vezes é negada pelo
        // Windows (foreground lock) e dispara um Deactivated espúrio antes do usuário
        // interagir — sem essa guarda, isso confirmava/fechava a janela sozinha.
        Activated += (_, _) => _everActivated = true;
        Deactivated += (_, _) => { if (!_closing && _everActivated) Confirm(); };
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => { _textBox.Focus(); _textBox.SelectAll(); };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _cancelled = true;
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Confirm();
        }
    }

    private void Confirm()
    {
        if (_cancelled) return;
        var text = _textBox.Text.Trim();
        if (!string.IsNullOrEmpty(text)) Confirmed?.Invoke(text);
        Close();
    }

    /// <summary>Posiciona logo abaixo/acima do elemento âncora, igual o popup de lembrete.</summary>
    public void AnchorTo(FrameworkElement anchor)
    {
        var topLeft = anchor.PointToScreen(new Point(0, anchor.ActualHeight));
        var source = PresentationSource.FromVisual(anchor);
        double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        Left = topLeft.X / scale;
        Top = topLeft.Y / scale;
    }
}
