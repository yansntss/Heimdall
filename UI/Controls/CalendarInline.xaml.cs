using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.ViewModels;

namespace Heimdall.UI.Controls;

/// <summary>
/// Calendário inline (no lugar do Calendar/DatePicker do WPF, que trazem o visual do
/// sistema). A lógica do mês fica no <see cref="CalendarMonthViewModel"/>; aqui só a ponte
/// com <see cref="SelectedDate"/>, que aceita binding de duas vias.
/// </summary>
public partial class CalendarInline : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateOnly?), typeof(CalendarInline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

    private readonly CalendarMonthViewModel _vm;

    public CalendarInline()
    {
        InitializeComponent();
        // Primeiro dia da semana do sistema; nomes no idioma do app.
        var names = new CultureInfo(Strings.Current == AppLanguage.PtBr ? "pt-BR" : "en-US");
        _vm = new CalendarMonthViewModel(DateOnly.FromDateTime(DateTime.Today), CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, names);
        _vm.SelectedChanged += date => SelectedDate = date;
        Root.DataContext = _vm;
    }

    public DateOnly? SelectedDate
    {
        get => (DateOnly?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CalendarInline)d)._vm.Selected = (DateOnly?)e.NewValue;
}
