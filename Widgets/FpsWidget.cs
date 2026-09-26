using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Services;

namespace Heimdall.Widgets;

/// <summary>FPS do processo em primeiro plano, via RTSS. Some completamente (sem texto, sem "—") quando o RTSS não está rodando ou não tem dado pro processo atual.</summary>
public sealed class FpsWidget : IWidget
{
    // Throttled a 4x/s: o RTSS atualiza a cada frame, mas repintar o texto nessa
    // frequência (podendo passar de 100Hz) só pisca sem ajudar a leitura.
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _text = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Visibility = Visibility.Collapsed
    };

    public FrameworkElement View => _text;

    public FpsWidget() => _timer.Tick += (_, _) => Update();

    public void ApplyOrientation(Orientation orientation) { }

    public void Start()
    {
        Update();
        _timer.Start();
    }

    private void Update()
    {
        var fps = RtssService.GetForegroundFps();
        if (fps is null)
        {
            _text.Visibility = Visibility.Collapsed;
            return;
        }

        _text.Visibility = Visibility.Visible;
        _text.Text = $"{fps.Value:0} FPS";
    }

    public void Dispose() => _timer.Stop();
}
