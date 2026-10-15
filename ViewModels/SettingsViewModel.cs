using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Heimdall.Config;
using Heimdall.Services;

namespace Heimdall.ViewModels;

/// <summary>Item da sidebar.</summary>
internal sealed record SettingsSection(string Key, string Label, string IconKey);

/// <summary>
/// Janela de Configurações: um rascunho do config (cópia; nada vai pro disco até o
/// Aplicar), a navegação entre seções e o ciclo Aplicar / Cancelar / Restaurar padrões.
/// A prévia ao vivo recria a barra a partir do rascunho; cancelar recarrega do disco.
/// </summary>
internal sealed class SettingsViewModel : ObservableObject
{
    private SettingsSection _selected;
    private string _status = "";
    private bool _previewing;
    private bool _savedStartWithWindows;

    public SettingsViewModel()
    {
        Draft = ConfigService.Load();
        _savedStartWithWindows = Draft.StartWithWindows;
        Appearance = new AppearanceViewModel(Draft, Preview);

        Sections = new List<SettingsSection>
        {
            new("geral", Strings.NavGeral, "H.Icon.Home"),
            new("aparencia", Strings.NavAparencia, "H.Icon.Palette"),
            new("relogio", Strings.NavRelogio, "H.Icon.Clock"),
            new("widgets", Strings.NavWidgets, "H.Icon.Grid"),
            new("lembretes", Strings.NavLembretes, "H.Icon.ListChecks"),
            new("atalhos", Strings.NavAtalhos, "H.Icon.Keyboard"),
            new("sobre", Strings.NavSobre, "H.Icon.Info"),
        };
        _selected = Sections[1];

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionInfo = Strings.SidebarRuntime(version?.ToString(3) ?? "dev", RuntimeInformation.FrameworkDescription);
    }

    /// <summary>Rascunho editado pela janela inteira (painel novo e seções antigas).</summary>
    public AppConfig Draft { get; }

    public AppearanceViewModel Appearance { get; }

    public IReadOnlyList<SettingsSection> Sections { get; }

    public SettingsSection SelectedSection
    {
        get => _selected;
        set { if (value is not null) SetProperty(ref _selected, value); }
    }

    public string VersionInfo { get; }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private static App CurrentApp => (App)Application.Current;

    private void Preview(bool rebuild)
    {
        _previewing = true;
        Status = "";
        CurrentApp.PreviewConfig(Draft, rebuild);
    }

    /// <summary>Salva o rascunho e recria a barra a partir do disco.</summary>
    public void Apply()
    {
        if (Draft.StartWithWindows != _savedStartWithWindows)
        {
            StartupService.SetEnabled(Draft.StartWithWindows);
            _savedStartWithWindows = Draft.StartWithWindows;
        }

        ConfigService.Save(Draft);
        _previewing = false;
        CurrentApp.Reload();
        Status = Strings.Applied;
    }

    /// <summary>Desfaz a prévia: a barra volta ao que está salvo.</summary>
    public void Cancel()
    {
        if (!_previewing) return;
        _previewing = false;
        CurrentApp.Reload();
    }

    public void RestoreDefaults()
    {
        Appearance.RestoreDefaults();
        Status = Strings.DefaultsRestored;
    }
}
