using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Heimdall.UI;

/// <summary>
/// <c>ui:FocusOnLoad.Enabled="True"</c>: a caixa de texto ganha o foco (cursor no fim) assim
/// que aparece — é como a edição inline abre já pronta pra digitar.
/// </summary>
public static class FocusOnLoad
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(FocusOnLoad), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;
        element.Loaded += (_, _) => element.Dispatcher.BeginInvoke(() =>
        {
            element.Focus();
            Keyboard.Focus(element);
            if (element is TextBox box) box.CaretIndex = box.Text.Length;
        });
    }
}
