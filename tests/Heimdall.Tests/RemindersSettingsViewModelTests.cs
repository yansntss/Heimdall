using Heimdall.Config;
using Heimdall.ViewModels;

namespace Heimdall.Tests;

public class RemindersSettingsViewModelTests
{
    private readonly AppConfig _draft = new();
    private readonly List<ReminderConfig> _live = new();

    public RemindersSettingsViewModelTests()
    {
        _draft.ReminderLists.Add(new ReminderListConfig { Id = ReminderListConfig.DefaultId, Name = "Lembretes" });
        _draft.ReminderLists.Add(new ReminderListConfig { Id = "mercado", Name = "Mercado", Color = "#8FD3C8" });
        _live.Add(new ReminderConfig { Title = "leite", ListId = "mercado" });
        _live.Add(new ReminderConfig { Title = "pão", ListId = "mercado" });
    }

    private RemindersSettingsViewModel Create() => new(_draft, _live);

    [Fact]
    public void Fields_write_valid_values_only()
    {
        var vm = Create();
        Assert.Equal("09:00", vm.DefaultTimeText);
        Assert.Equal("10", vm.SnoozeMinutesText);

        vm.DefaultTimeText = "8:30";
        vm.SnoozeMinutesText = "15";
        Assert.Equal(new TimeOnly(8, 30), _draft.ReminderOptions.DefaultTime);
        Assert.Equal(15, _draft.ReminderOptions.SnoozeMinutes);

        vm.DefaultTimeText = "25:00";
        vm.SnoozeMinutesText = "0";
        Assert.Equal(new TimeOnly(8, 30), _draft.ReminderOptions.DefaultTime);
        Assert.Equal(15, _draft.ReminderOptions.SnoozeMinutes);

        vm.PlaySound = true;
        vm.ShowCompleted = true;
        Assert.True(_draft.ReminderOptions.PlaySound);
        Assert.True(_draft.ReminderOptions.ShowCompleted);
    }

    [Fact]
    public void Lists_show_counts_and_rename_and_recolor()
    {
        var vm = Create();
        var mercado = vm.Lists[1];
        Assert.Equal("2 lembretes", mercado.CountText);
        Assert.True(vm.Lists[0].IsDefault);

        mercado.Name = "Feira";
        mercado.CycleColorCommand.Execute(null);
        Assert.Equal("Feira", _draft.ReminderLists[1].Name);
        Assert.Equal("#E0B562", _draft.ReminderLists[1].Color);
    }

    [Fact]
    public void Add_and_delete_with_confirmation()
    {
        var vm = Create();
        vm.AddListCommand.Execute(null);
        Assert.Equal(3, _draft.ReminderLists.Count);
        Assert.Equal("Nova lista", vm.Lists[2].Name);

        var mercado = vm.Lists[1];
        mercado.DeleteCommand.Execute(null);
        Assert.True(mercado.IsConfirmingDelete);
        Assert.Equal("Confirmar", mercado.DeleteLabel);
        Assert.Equal(3, _draft.ReminderLists.Count);

        mercado.DeleteCommand.Execute(null);
        Assert.DoesNotContain(_draft.ReminderLists, l => l.Id == "mercado");
        Assert.False(vm.Lists[0].DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void Save_takes_live_reminders_and_moves_orphans_to_default()
    {
        var vm = Create();
        var mercado = vm.Lists[1];
        mercado.DeleteCommand.Execute(null);
        mercado.DeleteCommand.Execute(null);
        vm.Lists[0].Name = "  ";

        // Um lembrete criado na janela enquanto as Configurações estavam abertas.
        _live.Add(new ReminderConfig { Title = "novo" });
        vm.PrepareForSave(_live);

        Assert.Equal(3, _draft.Reminders.Count);
        Assert.All(_draft.Reminders, r => Assert.Equal(ReminderListConfig.DefaultId, r.ListId));
        Assert.Equal("Lembretes", _draft.ReminderLists[0].Name);
    }
}
