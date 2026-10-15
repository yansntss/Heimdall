using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.UI;
using Heimdall.UI.Popups;
using Heimdall.ViewModels;

namespace Heimdall.Widgets;

/// <summary>Chip de CPU: rótulo, sparkline das últimas 5 amostras (a cada 2s) e o valor atual.</summary>
public sealed class CpuWidget : IWidget, IWidgetMenuProvider
{
    private const int Samples = 5;
    private const double SparkMaxHeight = 12;
    private const double SparkMinHeight = 2;

    private readonly CpuUsageSampler _sampler = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Queue<double> _history = new();
    private readonly Rectangle[] _bars = new Rectangle[Samples];

    private readonly TextBlock _label = WidgetUi.Text(10, "H.Brush.TextTertiary", weight: FontWeights.SemiBold);
    private readonly TextBlock _value = WidgetUi.Text(11, mono: true);
    private readonly StackPanel _spark = new() { Orientation = Orientation.Horizontal, Height = SparkMaxHeight, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _content = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _chip = new() { Padding = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

    public FrameworkElement View => _chip;

    private readonly Action? _openFlyout;

    public IEnumerable<WidgetMenuEntry> GetMenuEntries()
    {
        if (_openFlyout is not null) yield return new WidgetMenuEntry(Strings.MenuSystemMonitor, "H.Icon.Cpu", _openFlyout);
    }

    public CpuWidget(AppConfig cfg, bool isOverlay = false)
    {
        _label.Text = "CPU";
        _value.Text = "—";
        // Largura fixa pro chip não "pular" entre 9% e 100%.
        _value.MinWidth = 28;
        _value.TextAlignment = TextAlignment.Right;

        for (int i = 0; i < Samples; i++)
        {
            _bars[i] = new Rectangle
            {
                Width = 3,
                Height = SparkMinHeight,
                RadiusX = 1,
                RadiusY = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0)
            };
            // Mais recente em acento, histórico em terciário.
            _bars[i].SetResourceReference(Shape.FillProperty, i == Samples - 1 ? "H.Brush.Accent" : "H.Brush.TextTertiary");
            _spark.Children.Add(_bars[i]);
        }

        _content.Children.Add(_label);
        _content.Children.Add(_spark);
        _content.Children.Add(_value);

        _chip.Child = _content;
        _chip.SetResourceReference(Border.BackgroundProperty, "H.Brush.Selected");
        _chip.SetResourceReference(Border.CornerRadiusProperty, "H.Radius.Chip");
        _chip.SetResourceReference(FrameworkElement.HeightProperty, "H.Size.Chip");

        // Clique abre o dropdown de sistema (CPU/RAM/disco, tempo ligado, ações rápidas).
        if (!isOverlay)
        {
            _chip.Cursor = Cursors.Hand;
            AutomationProperties.SetName(_chip, Strings.FlyoutSystemTitle);
            _chip.MouseEnter += (_, _) => _chip.SetResourceReference(Border.BackgroundProperty, "H.Brush.Pressed");
            _chip.MouseLeave += (_, _) => _chip.SetResourceReference(Border.BackgroundProperty, "H.Brush.Selected");
            _openFlyout = () => FlyoutWindow.Toggle(_chip, cfg.Edge,
                () => new SystemFlyout(new SystemFlyoutViewModel(cfg)), Strings.FlyoutSystemTitle);
            ClickHelper.Attach(_chip, _openFlyout);
        }

        _timer.Tick += (_, _) => Update();
    }

    public void ApplyOrientation(Orientation orientation)
    {
        // Barra vertical: só o valor, sem largura pra rótulo + sparkline.
        bool vertical = orientation == Orientation.Vertical;
        _label.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
        _spark.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
        _chip.Padding = vertical ? new Thickness(4, 0, 4, 0) : new Thickness(8, 0, 8, 0);
        _value.MinWidth = vertical ? 0 : 28;
    }

    public void Start()
    {
        _sampler.Sample(); // primeira amostra só arma a diferença
        _timer.Start();
    }

    private void Update()
    {
        if (_sampler.Sample() is not { } percent) return;

        _history.Enqueue(percent);
        while (_history.Count > Samples) _history.Dequeue();

        // Alinha à direita: com menos de 5 amostras, as barras da esquerda ficam no mínimo.
        var values = _history.ToArray();
        int offset = Samples - values.Length;
        for (int i = 0; i < Samples; i++)
        {
            double v = i >= offset ? values[i - offset] : 0;
            _bars[i].Height = SparkMinHeight + (SparkMaxHeight - SparkMinHeight) * v / 100;
        }

        _value.Text = $"{percent:0}%";
        _chip.ToolTip = Strings.CpuTooltip(percent);
    }

    public void Dispose() => _timer.Stop();
}
