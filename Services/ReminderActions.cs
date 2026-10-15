using System.Windows;
using Heimdall.Config;

namespace Heimdall.Services;

/// <summary>Criar e editar lembrete a partir da barra (sino, chips, atalho global): tudo acontece na janela de lembretes.</summary>
internal static class ReminderActions
{
    /// <summary>Abre a janela de lembretes com uma linha nova em edição no topo.</summary>
    public static void OpenNew() => ((App)Application.Current).OpenReminders(newReminder: true);

    /// <summary>Abre a janela de lembretes com a linha desse lembrete em edição.</summary>
    public static void OpenEdit(ReminderConfig reminder) => ((App)Application.Current).OpenReminders(edit: reminder);
}
