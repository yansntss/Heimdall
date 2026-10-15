using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.ViewModels;

/// <summary>Uma lista na gestão de listas: nome editável, cor (troca pela paleta) e excluir em dois toques.</summary>
internal sealed class ReminderListEditViewModel : ObservableObject
{
    private readonly ReminderListConfig _list;
    private readonly Action<ReminderListEditViewModel> _delete;
    private bool _isConfirmingDelete;

    public ReminderListEditViewModel(ReminderListConfig list, int reminderCount, Action<ReminderListEditViewModel> delete)
    {
        _list = list;
        _delete = delete;
        CountText = Strings.ReminderCount(reminderCount);
        CycleColorCommand = new RelayCommand(() =>
        {
            int i = Array.IndexOf(RemindersSettingsViewModel.Palette, _list.Color.ToUpperInvariant());
            Color = RemindersSettingsViewModel.Palette[(i + 1) % RemindersSettingsViewModel.Palette.Length];
        });
        DeleteCommand = new RelayCommand(() =>
        {
            // Primeiro toque arma, o segundo exclui — sem diálogo do sistema.
            if (!IsConfirmingDelete) IsConfirmingDelete = true;
            else _delete(this);
        }, () => !IsDefault);
    }

    public ReminderListConfig List => _list;

    public bool IsDefault => _list.Id == ReminderListConfig.DefaultId;

    public string Name
    {
        get => _list.Name;
        set
        {
            if (_list.Name == value) return;
            _list.Name = value;
            OnPropertyChanged();
        }
    }

    public string Color
    {
        get => _list.Color;
        private set
        {
            if (_list.Color == value) return;
            _list.Color = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Brush));
        }
    }

    public Brush Brush => ReminderDisplay.ListBrush(_list.Color);

    /// <summary>"3 lembretes".</summary>
    public string CountText { get; }

    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        set
        {
            if (!SetProperty(ref _isConfirmingDelete, value)) return;
            OnPropertyChanged(nameof(DeleteLabel));
        }
    }

    public string DeleteLabel => IsConfirmingDelete ? Strings.ConfirmDeleteList : Strings.DeleteList;

    public ICommand CycleColorCommand { get; }
    public ICommand DeleteCommand { get; }
}

/// <summary>
/// Seção Lembretes das Configurações: hora padrão dos lembretes sem hora, minutos do
/// "Adiar", som, mostrar concluídos e gestão de listas. Mexe no rascunho; o Aplicar salva.
/// </summary>
internal sealed class RemindersSettingsViewModel : ObservableObject
{
    /// <summary>Cores das listas, na ordem em que "Trocar cor" passa por elas.</summary>
    public static readonly string[] Palette = { "#9DB8F2", "#8FD3C8", "#E0B562", "#F2A58C", "#C9A0F2", "#A8D98A", "#E08A62" };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly AppConfig _draft;
    private string _defaultTimeText;
    private string _snoozeText;

    public RemindersSettingsViewModel(AppConfig draft, IReadOnlyCollection<ReminderConfig> liveReminders)
    {
        _draft = draft;
        _defaultTimeText = draft.ReminderOptions.DefaultTime.ToString("HH:mm", Inv);
        _snoozeText = draft.ReminderOptions.SnoozeMinutes.ToString(Inv);

        foreach (var list in draft.ReminderLists)
            Lists.Add(new ReminderListEditViewModel(list, liveReminders.Count(r => r.ListId == list.Id && !r.IsCompleted), Delete));

        AddListCommand = new RelayCommand(() =>
        {
            var list = new ReminderListConfig { Name = Strings.NewListName, Color = Palette[_draft.ReminderLists.Count % Palette.Length] };
            _draft.ReminderLists.Add(list);
            Lists.Add(new ReminderListEditViewModel(list, 0, Delete));
        });
    }

    public ObservableCollection<ReminderListEditViewModel> Lists { get; } = new();

    public ICommand AddListCommand { get; }

    /// <summary>"HH:mm"; inválido fica no campo mas não muda o config.</summary>
    public string DefaultTimeText
    {
        get => _defaultTimeText;
        set
        {
            if (!SetProperty(ref _defaultTimeText, value)) return;
            if (TimeOnly.TryParseExact(value.Trim(), new[] { "HH:mm", "H:mm" }, Inv, DateTimeStyles.None, out var time))
                _draft.ReminderOptions.DefaultTime = time;
        }
    }

    /// <summary>Minutos do "Adiar" (1–240).</summary>
    public string SnoozeMinutesText
    {
        get => _snoozeText;
        set
        {
            if (!SetProperty(ref _snoozeText, value)) return;
            if (int.TryParse(value.Trim(), NumberStyles.None, Inv, out int minutes) && minutes is >= 1 and <= 240)
                _draft.ReminderOptions.SnoozeMinutes = minutes;
        }
    }

    public bool PlaySound
    {
        get => _draft.ReminderOptions.PlaySound;
        set { _draft.ReminderOptions.PlaySound = value; OnPropertyChanged(); }
    }

    public bool ShowCompleted
    {
        get => _draft.ReminderOptions.ShowCompleted;
        set { _draft.ReminderOptions.ShowCompleted = value; OnPropertyChanged(); }
    }

    private void Delete(ReminderListEditViewModel item)
    {
        if (item.IsDefault) return;
        _draft.ReminderLists.Remove(item.List);
        Lists.Remove(item);
    }

    /// <summary>
    /// Antes de salvar: lembretes vêm do que está valendo agora (a tabela antiga sobrescrevia
    /// o que mudou com a janela aberta), os de lista excluída vão pra padrão e lista sem nome
    /// ganha um.
    /// </summary>
    public void PrepareForSave(IEnumerable<ReminderConfig> liveReminders)
    {
        foreach (var list in _draft.ReminderLists)
            if (string.IsNullOrWhiteSpace(list.Name)) list.Name = list.Id == ReminderListConfig.DefaultId ? Strings.ReminderDefaultList : Strings.NewListName;
            else list.Name = list.Name.Trim();

        var ids = _draft.ReminderLists.Select(l => l.Id).ToHashSet();
        _draft.Reminders = liveReminders.ToList();
        foreach (var reminder in _draft.Reminders)
            if (!ids.Contains(reminder.ListId)) reminder.ListId = ReminderListConfig.DefaultId;
    }
}
