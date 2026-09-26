using System.Windows.Media.Imaging;

namespace Heimdall.UI;

/// <summary>Ícone do app, pra janelas montadas só em código (sem XAML pra referenciar via pack URI direto).</summary>
internal static class AppIcon
{
    public static readonly BitmapImage Source = new(new Uri("pack://application:,,,/Assets/heimdall.ico"));
}
