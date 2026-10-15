using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.ViewModels;

namespace Heimdall.UI.Popups;

public partial class MediaFlyout : UserControl
{
    internal MediaFlyout(MediaFlyoutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Só visual: o player recebe a busca quando o usuário solta a seekbar, não a cada
        // pixel arrastado (e o timer de posição não briga com o arraste).
        Seek.PreviewMouseLeftButtonDown += (_, _) => viewModel.BeginSeek();
        Seek.PreviewMouseLeftButtonUp += (_, _) => viewModel.CommitSeek();
        Seek.PreviewKeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) viewModel.BeginSeek(); };
        Seek.KeyUp += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) viewModel.CommitSeek(); };
    }
}
