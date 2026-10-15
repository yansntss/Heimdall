using System.Windows.Input;
using Heimdall.Config;
using Heimdall.Services;
using Heimdall.Services.Reminders;

namespace Heimdall.ViewModels;

/// <summary>
/// Um aviso (toast) de lembrete: um só, com "Adiar N min" e "Concluir", ou vários agrupados
/// ("3 lembretes atrasados"), com "Ver lembretes" e "Dispensar". Some sozinho em 12s, exceto
/// se algum for de prioridade alta.
/// </summary>
internal sealed class ReminderToastViewModel : ObservableObject
{
    public static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(12);

    /// <summary>Um lembrete só.</summary>
    public ReminderToastViewModel(ReminderConfig reminder, ReminderStore store, Action openReminders)
    {
        Reminders = new[] { reminder };
        Title = reminder.Title;
        Meta = SingleMeta(reminder, store);
        PrimaryLabel = Strings.ReminderSnoozeMinutes(store.Options.SnoozeMinutes);
        SecondaryLabel = Strings.ReminderComplete;
        PrimaryCommand = new RelayCommand(() => { store.Snooze(reminder); Close(); });
        SecondaryCommand = new RelayCommand(() => { store.Complete(reminder); Close(); });
        OpenCommand = new RelayCommand(() => { openReminders(); Close(); });
        CloseCommand = new RelayCommand(Close);
    }

    /// <summary>Vários de uma vez.</summary>
    public ReminderToastViewModel(IReadOnlyList<ReminderConfig> reminders, bool overdue, ReminderStore store, Action openReminders)
    {
        Reminders = reminders;
        Title = overdue ? Strings.ToastOverdueGroup(reminders.Count) : Strings.ToastGroup(reminders.Count);
        var names = reminders.Take(3).Select(r => r.Title).ToList();
        if (reminders.Count > 3) names.Add($"+{reminders.Count - 3}");
        Meta = string.Join(" · ", names);
        PrimaryLabel = Strings.ToastOpen;
        SecondaryLabel = Strings.ToastDismiss;
        PrimaryCommand = new RelayCommand(() => { openReminders(); Close(); });
        SecondaryCommand = new RelayCommand(Close);
        OpenCommand = PrimaryCommand;
        CloseCommand = new RelayCommand(Close);
    }

    public IReadOnlyList<ReminderConfig> Reminders { get; }
    public string Title { get; }

    /// <summary>"Hoje, 17:00 · Trabalho" / títulos do grupo.</summary>
    public string Meta { get; }

    public string PrimaryLabel { get; }
    public string SecondaryLabel { get; }

    public ICommand PrimaryCommand { get; }
    public ICommand SecondaryCommand { get; }

    /// <summary>Clique no corpo do aviso: abre a janela de lembretes.</summary>
    public ICommand OpenCommand { get; }

    public ICommand CloseCommand { get; }

    /// <summary>Prioridade alta fica até alguém mexer.</summary>
    public bool AutoDismiss => Reminders.All(r => r.Priority != ReminderPriority.High);

    public string AccessibleName => $"{Strings.ToastName}: {Title}. {Meta}";

    /// <summary>A view fecha (com a animação de saída) quando isto dispara.</summary>
    public event Action? CloseRequested;

    public void Close() => CloseRequested?.Invoke();

    private static string SingleMeta(ReminderConfig r, ReminderStore store)
    {
        var parts = new List<string>();
        if (r.DueDate is { } date) parts.Add(RelativeDateFormatter.Format(date, r.DueTime, store.Today, Strings.Current));
        if (store.Lists.Count > 1) parts.Add(store.ListOf(r).Name);
        if (!string.IsNullOrWhiteSpace(r.Note)) parts.Add(r.Note.Trim().Split('\n')[0]);
        return string.Join(" · ", parts);
    }
}
