using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.ViewModels;

/// <summary>Textos e cores de lembrete comuns ao dropdown do sino e à janela de lembretes.</summary>
internal static class ReminderDisplay
{
    /// <summary>"Todo dia", "Semanal"...; nulo se não repete.</summary>
    public static string? RecurrenceLabel(ReminderConfig reminder) => reminder.Recurrence?.Kind switch
    {
        RepeatKind.Daily => Strings.ReminderMetaDaily,
        RepeatKind.Weekly => Strings.ReminderMetaWeekly,
        RepeatKind.Monthly => Strings.ReminderMetaMonthly,
        RepeatKind.Yearly => Strings.ReminderMetaYearly,
        _ => null
    };

    /// <summary>Segunda linha do estado vazio: "Próximo: Dentista · Amanhã, 14:00" ou "Nada agendado".</summary>
    public static string NextUpText(Services.Reminders.ReminderStore store)
    {
        if (store.NextUpcoming() is not { DueDate: { } date } next) return Strings.NothingScheduled;
        var when = Services.Reminders.RelativeDateFormatter.Format(date, next.DueTime, store.Today, Strings.Current);
        return Strings.NextUp(next.Title, when);
    }

    /// <summary>Cor da lista ("#RRGGBB"); inválida vira cinza em vez de derrubar a lista.</summary>
    public static Brush ListBrush(string hex)
    {
        Color color;
        try { color = (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { color = Colors.Gray; }
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
