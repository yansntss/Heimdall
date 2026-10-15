using System.Windows;
using System.Windows.Controls;
using Heimdall.ViewModels;

namespace Heimdall.UI;

/// <summary>Cabeçalho de seção usa um contêiner que não foca nem seleciona; lembrete, o de linha.</summary>
public sealed class ReminderEntryStyleSelector : StyleSelector
{
    public Style? RowStyle { get; set; }
    public Style? SectionStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container) =>
        item is ReminderSectionViewModel ? SectionStyle : RowStyle;
}
