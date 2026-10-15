using System.Windows.Input;
using Heimdall.Services;

namespace Heimdall.ViewModels;

/// <summary>Páginas do painel de detalhes — a navegação é por este enum, não por Visibility.</summary>
internal enum DetailsPage { Main, Repeat, List, Priority, Notes }

/// <summary>
/// Painel de detalhes do lembrete em edição (o "…" da linha). Mexe direto no
/// <see cref="NewReminderViewModel"/> — que só grava no lembrete no "Adicionar"/Enter —
/// e guarda um retrato dele pra "Cancelar" desfazer o que foi feito no painel.
/// Esc volta uma página; na principal, cancela.
/// </summary>
internal sealed class ReminderDetailsViewModel : ObservableObject
{
    private readonly NewReminderViewModel.EditorSnapshot _snapshot;
    private DetailsPage _page;
    private bool _isNavigatingBack;

    public ReminderDetailsViewModel(NewReminderViewModel editor, DetailsPage page, NewReminderViewModel.EditorSnapshot snapshot)
    {
        Editor = editor;
        _page = page;
        _snapshot = snapshot;

        NavigateCommand = new RelayCommand(p => { if (p is DetailsPage target) Navigate(target); });
        BackCommand = new RelayCommand(() => Navigate(DetailsPage.Main, back: true), () => _page != DetailsPage.Main);
        EscapeCommand = new RelayCommand(() =>
        {
            if (_page != DetailsPage.Main) Navigate(DetailsPage.Main, back: true);
            else Cancel();
        });
        CancelCommand = new RelayCommand(Cancel);
        ConfirmCommand = new RelayCommand(() =>
        {
            editor.CloseDetails();
            editor.SaveCommand.Execute(null);
        });
    }

    public NewReminderViewModel Editor { get; }

    public DetailsPage Page
    {
        get => _page;
        private set
        {
            if (!SetProperty(ref _page, value)) return;
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IsMainPage));
        }
    }

    public bool IsMainPage => _page == DetailsPage.Main;

    /// <summary>A view anima pra direita (voltando) ou pra esquerda (entrando numa página).</summary>
    public bool IsNavigatingBack
    {
        get => _isNavigatingBack;
        private set => SetProperty(ref _isNavigatingBack, value);
    }

    /// <summary>Cabeçalho "‹ Repetir" das subpáginas.</summary>
    public string PageTitle => _page switch
    {
        DetailsPage.Repeat => Strings.DetailsRepeat,
        DetailsPage.List => Strings.DetailsList,
        DetailsPage.Priority => Strings.DetailsPriority,
        DetailsPage.Notes => Strings.DetailsNotes,
        _ => ""
    };

    public string ConfirmLabel => Editor.IsNew ? Strings.DetailsAdd : Strings.DetailsSave;

    /// <summary>Parâmetro: <see cref="DetailsPage"/>.</summary>
    public ICommand NavigateCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand EscapeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ConfirmCommand { get; }

    private void Navigate(DetailsPage page, bool back = false)
    {
        IsNavigatingBack = back;
        Page = page;
    }

    private void Cancel()
    {
        Editor.Restore(_snapshot);
        Editor.CloseDetails();
    }
}
