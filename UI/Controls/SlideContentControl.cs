using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Heimdall.UI.Controls;

/// <summary>
/// ContentControl que entra deslizando na horizontal (180ms, CubicEase) quando o conteúdo
/// muda — a navegação entre páginas do painel de detalhes. <see cref="SlideBack"/> = true
/// entra pela esquerda (voltando); senão pela direita.
/// </summary>
public sealed class SlideContentControl : ContentControl
{
    public static readonly DependencyProperty SlideBackProperty = DependencyProperty.Register(
        nameof(SlideBack), typeof(bool), typeof(SlideContentControl), new PropertyMetadata(false));

    private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(180);

    public SlideContentControl()
    {
        ClipToBounds = true;
        RenderTransform = new TranslateTransform();
    }

    public bool SlideBack
    {
        get => (bool)GetValue(SlideBackProperty);
        set => SetValue(SlideBackProperty, value);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (oldContent is null || !IsLoaded || ActualWidth <= 0) return;

        // Animações do sistema desligadas (acessibilidade): troca direto.
        if (!SystemParameters.ClientAreaAnimation) return;

        var transform = (TranslateTransform)RenderTransform;
        var slide = new DoubleAnimation(SlideBack ? -ActualWidth : ActualWidth, 0, SlideDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(TranslateTransform.XProperty, slide);
    }
}

/// <summary>Escolhe o template da página do painel de detalhes pelo valor do enum (Content = a página).</summary>
public sealed class DetailsPageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Main { get; set; }
    public DataTemplate? Repeat { get; set; }
    public DataTemplate? List { get; set; }
    public DataTemplate? Priority { get; set; }
    public DataTemplate? Notes { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        Heimdall.ViewModels.DetailsPage.Repeat => Repeat,
        Heimdall.ViewModels.DetailsPage.List => List,
        Heimdall.ViewModels.DetailsPage.Priority => Priority,
        Heimdall.ViewModels.DetailsPage.Notes => Notes,
        _ => Main
    };
}
