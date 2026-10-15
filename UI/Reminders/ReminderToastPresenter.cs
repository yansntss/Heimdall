using System.Media;
using System.Windows;
using Heimdall.Config;
using Heimdall.Native;
using Heimdall.Services.Reminders;
using Heimdall.ViewModels;

namespace Heimdall.UI.Reminders;

/// <summary>
/// Mostra os avisos do agendador: no canto oposto à borda da barra (barra em cima → canto
/// de baixo à direita; embaixo → em cima à direita; à direita → embaixo à esquerda), o mais
/// novo encostado no canto e os anteriores empilhados. No máximo 3 na tela.
/// </summary>
internal sealed class ReminderToastPresenter : IReminderNotifier
{
    private const int MaxVisible = 3;

    private readonly ReminderStore _store;
    private readonly Func<BarEdge> _edge;
    private readonly Action _openReminders;
    private readonly List<ReminderToastWindow> _open = new();

    public ReminderToastPresenter(ReminderStore store, Func<BarEdge> edge, Action openReminders)
    {
        _store = store;
        _edge = edge;
        _openReminders = openReminders;
    }

    public bool IsQuiet => FocusAssist.IsQuiet;

    /// <summary>Avisos na tela, do mais antigo ao mais novo.</summary>
    internal IReadOnlyList<ReminderToastWindow> OpenToasts => _open;

    public void Show(ReminderConfig reminder) =>
        Present(new ReminderToastViewModel(reminder, _store, _openReminders));

    public void ShowGroup(IReadOnlyList<ReminderConfig> reminders, bool overdue) =>
        Present(new ReminderToastViewModel(reminders, overdue, _store, _openReminders));

    private void Present(ReminderToastViewModel vm)
    {
        if (_open.Count >= MaxVisible) _open[0].Close();

        var edge = _edge();
        var window = new ReminderToastWindow(vm, slideFromBelow: edge != BarEdge.Bottom)
        {
            // Fora da tela até saber a altura (SizeToContent) e entrar no lugar certo.
            Left = -32000,
            Top = -32000
        };
        window.SizeChanged += (_, _) => Relayout();
        window.Closed += (_, _) =>
        {
            _open.Remove(window);
            Relayout();
        };
        _open.Add(window);
        window.Show();

        if (_store.Options.PlaySound) SystemSounds.Asterisk.Play();
    }

    private void Relayout()
    {
        var area = SystemParameters.WorkArea;
        var edge = _edge();
        bool top = edge == BarEdge.Bottom;
        bool left = edge == BarEdge.Right;

        double offset = 0;
        // Mais novo no canto; os anteriores vão se afastando dele.
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var window = _open[i];
            if (window.ActualHeight <= 0) continue;

            window.Left = left ? area.Left : area.Right - window.ActualWidth;
            window.Top = top ? area.Top + offset : area.Bottom - offset - window.ActualHeight;
            // A margem da sombra já separa um aviso do outro; tira um pouco pra não ficarem soltos demais.
            offset += window.ActualHeight - 22;
        }
    }
}
