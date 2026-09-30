using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI;

namespace Heimdall.Widgets;

public sealed class ClockWidget : IWidget
{
    private readonly AppConfig _appCfg;
    private readonly ClockConfig _cfg;
    private readonly DispatcherTimer _timer = new();
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };
    // Fundo transparente (não nulo) pra área inteira ser clicável, não só os glifos do texto.
    private readonly Border _root = new() { Background = Brushes.Transparent };
    private bool _vertical;

    private CalendarWindow? _calendar;
    private Point? _pressPoint;
    private bool _calendarOpenAtPress;

    public FrameworkElement View => _root;

    public ClockWidget(AppConfig cfg, bool isOverlay = false)
    {
        _appCfg = cfg;
        _cfg = cfg.Clock;
        _root.Child = _text;
        _timer.Tick += (_, _) => Update();

        // No overlay o clique atravessa a janela — calendário só na barra normal.
        if (!isOverlay)
        {
            _root.Cursor = Cursors.Hand;
            _root.ToolTip = Strings.CalendarTitle;
            _root.MouseLeftButtonDown += OnMouseDown;
            _root.MouseLeftButtonUp += (_, e) => OnMouseUp(e);
        }
    }

    public void ApplyOrientation(Orientation orientation)
    {
        _vertical = orientation == Orientation.Vertical;
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
        _text.Text = ClockFormatter.Format(_cfg, now, _vertical);

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
