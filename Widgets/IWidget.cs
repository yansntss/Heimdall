using System.Windows;
using System.Windows.Controls;

namespace InfoBar.Widgets;

public interface IWidget : IDisposable
{
    FrameworkElement View { get; }

    /// <summary>Horizontal para barras em cima/baixo; vertical nas laterais.</summary>
    void ApplyOrientation(Orientation orientation);

    void Start();
}
