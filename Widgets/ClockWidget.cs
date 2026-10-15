using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI;

namespace Heimdall.Widgets;

/// <summary>Data (11.5, texto secundário) + hora (mono 13). Clique abre o calendário.</summary>
public sealed class ClockWidget : IWidget
{
    private readonly AppConfig _appCfg;
    private readonly ClockConfig _cfg;
    private readonly DispatcherTimer _timer = new();
    private readonly TextBlock _date = WidgetUi.Text(11.5, "H.Brush.TextSecondary");
    private readonly TextBlock _time = WidgetUi.Text(13, mono: true, weight: FontWeights.Medium);
    private readonly StackPanel _content = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    // Fundo transparente (não nulo) pra área inteira ser clicável, não só os glifos do texto.
    private readonly Border _root = new() { Background = Brushes.Transparent, Padding = new Thickness(8, 0, 8, 0) };
    private bool _vertical;

    private CalendarWindow? _calendar;
    private Point? _pressPoint;
    private bool _calendarOpenAtPress;

    public FrameworkElement View => _root;

    public ClockWidget(AppConfig cfg, bool isOverlay = false)
    {
        _appCfg = cfg;
        _cfg = cfg.Clock;

        _date.Margin = new Thickness(0, 0, 8, 0);
        _date.TextAlignment = TextAlignment.Center;
        _time.TextAlignment = TextAlignment.Center;
        _content.Children.Add(_date);
        _content.Children.Add(_time);
        _root.Child = _content;
        _root.SetResourceReference(Border.CornerRadiusProperty, "H.Radius.Item");
        _root.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.IconButton");
        _timer.Tick += (_, _) => Update();

        // No overlay o clique atravessa a janela — calendário só na barra normal.
        if (!isOverlay)
        {
            _root.Cursor = Cursors.Hand;
            _root.ToolTip = Strings.CalendarTitle;
            AutomationProperties.SetName(_root, Strings.CalendarTitle);
            _root.MouseEnter += (_, _) => _root.SetResourceReference(Border.BackgroundProperty, "H.Brush.Hover");
            _root.MouseLeave += (_, _) => _root.Background = Brushes.Transparent;
            _root.MouseLeftButtonDown += OnMouseDown;
            _root.MouseLeftButtonUp += (_, e) => OnMouseUp(e);
        }
    }

    public void ApplyOrientation(Orientation orientation)
    {
        _vertical = orientation == Orientation.Vertical;

        // Vertical: hora em cima, data embaixo, sem altura fixa.
        _content.Orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal;
        _content.Children.Clear();
        if (_vertical)
        {
            _content.Children.Add(_time);
            _content.Children.Add(_date);
            _date.Margin = new Thickness(0);
            _root.Height = double.NaN;
            _root.Padding = new Thickness(2, 6, 2, 6);
        }
        else
        {
            _content.Children.Add(_date);
            _content.Children.Add(_time);
        }

        if (_timer.IsEnabled) Update();
    }

    public void Start()
    {
        Update();
        _timer.Start();
    }

    private void Update()
    {
        var now = DateTime.Now;
        var (date, time, custom) = ClockFormatter.FormatParts(_cfg, now, _vertical);

        _date.Text = date;
        _date.Visibility = date.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _time.Text = time;
        _time.Visibility = time.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Formato livre pode ter texto: fonte da interface em vez de mono.
        if (custom) _time.SetResourceReference(TextBlock.FontFamilyProperty, "H.Font.UI");

        // Alinha o próximo tick à virada do segundo
        _timer.Interval = TimeSpan.FromMilliseconds(1000 - now.Millisecond + 10);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(_root);
        _calendarOpenAtPress = _calendar is not null;

        // O WidgetDragController captura o mouse no wrapper logo depois (bolha), então o
        // MouseLeftButtonUp vai direto pra ele e nunca chega aqui. Escuta o Up de quem
        // ficou com a captura — sem captura (widget fixado), o Up do _root já resolve.
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (Mouse.Captured is not UIElement captor || ReferenceEquals(captor, _root)) return;
            void OnCaptorUp(object s, MouseButtonEventArgs up)
            {
                captor.MouseLeftButtonUp -= OnCaptorUp;
                OnMouseUp(up);
            }
            captor.MouseLeftButtonUp += OnCaptorUp;
        });
    }

    private void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_pressPoint is not { } press) return;
        _pressPoint = null;

        // Arrastou (reordenando o widget) ou soltou fora — não é clique.
        var pos = e.GetPosition(_root);
        if (Math.Abs(pos.X - press.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(pos.Y - press.Y) >= SystemParameters.MinimumVerticalDragDistance ||
            pos.X < 0 || pos.Y < 0 || pos.X > _root.ActualWidth || pos.Y > _root.ActualHeight)
            return;

        // Segundo clique fecha (toggle). Se o clique na barra já desativou/fechou o popup,
        // _calendarOpenAtPress impede que ele reabra logo em seguida.
        if (_calendarOpenAtPress)
        {
            _calendar?.Close();
            return;
        }
        OpenCalendar();
    }

    private void OpenCalendar()
    {
        var window = new CalendarWindow(ThemeService.GetEffectiveStyle(_appCfg), ClockFormatter.ResolveCulture(_cfg.Culture));
        window.AnchorTo(_root, _appCfg.Edge);
        window.Closed += (_, _) => _calendar = null;
        _calendar = window;
        window.Show();
        window.Activate();
    }

    public void Dispose()
    {
        _timer.Stop();
        _calendar?.Close();
    }
}
