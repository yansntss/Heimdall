using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Heimdall.ViewModels;

namespace Heimdall.UI;

/// <summary>Seção selecionada na sidebar == ConverterParameter (chave) → Visible.</summary>
public sealed class SectionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SettingsSection section && section.Key == parameter as string ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Texto vazio/nulo → Collapsed (dicas opcionais).</summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>true → Collapsed, false → Visible.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Nega um bool (ex.: "reservar espaço" desabilitado com auto-ocultar ligado).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>Chave de recurso (ex.: "H.Icon.Home") → o recurso do app — ícones da sidebar vindos do ViewModel.</summary>
public sealed class ResourceKeyConverter : IValueConverter
{
    public static readonly ResourceKeyConverter Instance = new();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string key ? Application.Current.TryFindResource(key) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Espaçamento entre letras (o WPF não tem): intercala hair spaces (U+200A, ~1px em 11px —
/// o 0.08em dos cabeçalhos de seção). Só pra exibição: o nome acessível usa o texto original.
/// </summary>
public sealed class LetterSpacingConverter : IValueConverter
{
    public static readonly LetterSpacingConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string text ? string.Join(' ', text.ToCharArray()) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
