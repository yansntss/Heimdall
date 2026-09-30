using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.UI;

/// <summary>
/// Aviso de versão nova, colado no botão de download da barra. Não rouba o foco (abre
/// junto com o app, o usuário pode estar digitando em outro lugar) e some sozinho depois
/// de alguns segundos — o botão de download continua na barra depois disso.
/// </summary>
internal sealed class UpdateNotificationWindow : Window
{
    private const string IconFontName = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private static readonly TimeSpan AutoCloseAfter = TimeSpan.FromSeconds(20);

    private readonly EffectiveStyle _style;
    private readonly DispatcherTimer _autoClose = new() { Interval = AutoCloseAfter };

    public UpdateNotificationWindow(EffectiveStyle style, UpdateInfo update)
    {
        _style = style;

        Title = Strings.UpdateAvailableTitle;
        Icon = AppIcon.Source;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        // Fora da tela até a posição final ser calculada (evita "piscar" no canto errado)
        Left = -32000;
        Top = -32000;

        var icon = CreateText("", 18, FontWeights.Normal);
        icon.FontFamily = new FontFamily(IconFontName);
        icon.Foreground = FrozenBrush(style.Accent);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 10, 0);

        var title = CreateText(Strings.UpdateAvailableTitle, 13, FontWeights.SemiBold);
        var body = CreateText(Strings.UpdateAvailableBody(update.Tag), 12, FontWeights.Normal);
        body.Opacity = 0.8;
        body.TextWrapping = TextWrapping.Wrap;
        body.Margin = new Thickness(0, 2, 0, 8);

        var seeWhatsNew = CreateButton(Strings.UpdateSeeWhatsNew, primary: true);
        seeWhatsNew.Click += (_, _) =>
        {
            UpdateService.OpenReleasePage();
            Close();
        };
        var later = CreateButton(Strings.UpdateLater, primary: false);
        later.Margin = new Thickness(6, 0, 0, 0);
        later.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(seeWhatsNew);
        buttons.Children.Add(later);

        var text = new StackPanel { Width = 230 };
        text.Children.Add(title);
        text.Children.Add(body);
        text.Children.Add(buttons);

        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);

        var border = new Border
        {
            Background = new SolidColorBrush(style.Background),
            BorderBrush = new SolidColorBrush(style.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(style.CornerRadius),
            Padding = new Thickness(12, 10, 12, 10),
            Child = row
        };
        TextElement.SetForeground(border, FrozenBrush(style.Foreground));
        Content = border;

        // Com o mouse em cima, não fecha no meio da leitura.
        _autoClose.Tick += (_, _) => { if (!IsMouseOver) Close(); };
        Loaded += (_, _) => _autoClose.Start();
        Closed += (_, _) => _autoClose.Stop();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public void AnchorTo(FrameworkElement anchor, BarEdge edge) => PopupPlacement.AnchorTo(this, anchor, edge);

    private Button CreateButton(string label, bool primary)
    {
        var text = CreateText(label, 12, FontWeights.Normal);
        text.Foreground = FrozenBrush(primary ? Colors.White : _style.Foreground);

        return new Button
        {
            Content = text,
            Padding = new Thickness(10, 3, 10, 3),
            Cursor = Cursors.Hand,
            Focusable = false,
            BorderThickness = new Thickness(0),
            Background = primary
                ? FrozenBrush(_style.Accent)
                : FrozenBrush(Color.FromArgb(0x2A, _style.Foreground.R, _style.Foreground.G, _style.Foreground.B))
        };
    }

    private TextBlock CreateText(string text, double size, FontWeight weight)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            FontFamily = new FontFamily(_style.FontFamily),
            Foreground = FrozenBrush(_style.Foreground)
        };
        TextOptions.SetTextRenderingMode(block, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
        return block;
    }

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
