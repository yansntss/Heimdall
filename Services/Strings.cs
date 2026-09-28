using Heimdall.Config;

namespace Heimdall.Services;

/// <summary>
/// Todo texto voltado ao usuário passa por aqui — troca de idioma é só mudar
/// <see cref="Current"/> (feito por <see cref="ConfigService.Load"/> a cada carga do
/// config, antes de qualquer janela/widget ser construído) e reabrir a UI (igual tema:
/// não muda janela já aberta em tempo real, só o que for (re)criado depois).
/// Fora do escopo de propósito: nomes de tema (são o próprio valor salvo em
/// config.json — traduzir a exibição exigiria separar id de nome de exibição, o que
/// quebraria configs existentes) e o formato de data/hora do relógio (widget já tem
/// seu próprio campo "Culture", independente do idioma da interface).
/// </summary>
public static class Strings
{
    public static AppLanguage Current { get; set; } = AppLanguage.PtBr;

    private static string T(string ptBr, string enUs) => Current == AppLanguage.PtBr ? ptBr : enUs;

    // ---------- Nomes de widget (catálogo — menu da barra e tela de Configurações) ----------

    public static string WidgetName(string id) => id switch
    {
        "clock" => T("Relógio", "Clock"),
        "media" => T("Mídia", "Media"),
        "reminder" => T("Lembretes", "Reminders"),
        "launcher" => T("Atalhos", "Shortcuts"),
        "windows" => T("Janelas", "Windows"),
        "ram" => T("RAM", "RAM"),
        "temp" => T("Temperatura", "Temperature"),
        "fps" => T("FPS", "FPS"),
        "separator" => T("Separador", "Separator"),
        _ => id
    };

    // ---------- Menu de contexto da barra (BarWindow) ----------

    public static string MenuSettings => T("Configurações...", "Settings...");
    public static string MenuEditConfig => T("Editar config.json", "Edit config.json");
    public static string MenuReminderHistory => T("Ver histórico de lembretes", "View reminder history");
    public static string MenuAddShortcut => T("Adicionar atalho", "Add shortcut");
    public static string MenuAddShortcutFromFile => T("Escolher um arquivo...", "Choose a file...");
    public static string MenuAddShortcutFromInstalled => T("Escolher app instalado...", "Choose installed app...");
    public static string MenuAddSeparator => T("Adicionar separador", "Add separator");
    public static string MenuAddWidget => T("Adicionar widget", "Add widget");
    public static string MenuTheme => T("Tema", "Theme");
    public static string MenuGamingModeOn => T("Modo gaming: Ativado", "Gaming mode: On");
    public static string MenuGamingModeOff => T("Modo gaming: Desativado", "Gaming mode: Off");
    public static string MenuReload => T("Recarregar", "Reload");
    public static string MenuMonitors => T("Monitores detectados", "Detected monitors");
    public static string MenuExit => T("Sair", "Exit");

    public static string DialogChooseFileTitle => T("Escolher arquivo ou atalho", "Choose file or shortcut");
    public static string DialogAllFiles => T("Todos os arquivos", "All files");

    public static string MonitorsDialogBody(string list) => T(
        $"Use o nome em \"MonitorDevice\" com \"MonitorMode\": \"Specific\":\n\n{list}\n\n(Ctrl+C copia este texto)",
        $"Use the name in \"MonitorDevice\" with \"MonitorMode\": \"Specific\":\n\n{list}\n\n(Ctrl+C copies this text)");
    public static string MonitorPrimarySuffix => T("  [principal]", "  [primary]");

    // ---------- Separador do launcher (submenu de estilo) ----------

    public static string SeparatorStyleLine => T("Linha", "Line");
    public static string SeparatorStyleSpace => T("Espaço", "Space");
    public static string SeparatorStyleDot => T("Ponto", "Dot");
    public static string Remove => T("Remover", "Remove");
    public static string Pin => T("Fixar posição", "Pin position");
    public static string Unpin => T("Desafixar", "Unpin");

    // ---------- Widget de atalhos (LauncherWidget) ----------

    public static string LauncherRunAsAdmin => T("Executar como administrador", "Run as administrator");
    public static string LauncherOpenLocation => T("Abrir local do arquivo", "Open file location");
    public static string LauncherRename => T("Renomear...", "Rename...");
    public static string LauncherTooltipMissing(string name) => T($"{name} — Atalho não encontrado", $"{name} — Shortcut not found");

    // ---------- Widget de janelas abertas (WindowsWidget) ----------

    public static string WindowsCloseWindow => T("Fechar janela", "Close window");
    public static string WindowsMinimize => T("Minimizar", "Minimize");
    public static string WindowsPinAsShortcut => T("Fixar como atalho", "Pin as shortcut");
    public static string WindowsOverflowTooltip(int count) => T($"+{count} janela(s)", $"+{count} more window(s)");
    public static string WindowsUntitled => T("(sem título)", "(untitled)");

    // ---------- Widget de lembretes (ReminderWidget) ----------

    public static string ReminderNewTooltip => T("Novo lembrete (Ctrl+Shift+R)", "New reminder (Ctrl+Shift+R)");
    public static string ReminderComplete => T("Concluir", "Complete");
    public static string ReminderEdit => T("Editar...", "Edit...");
    public static string ReminderDelete => T("Excluir", "Delete");
    public static string ReminderQueueTooltip(int count) => T($"+{count} lembrete(s) na fila", $"+{count} reminder(s) queued");

    // ---------- Widget de temperatura (TempWidget) ----------

    public static string TempAdminRequiredTooltip => T(
        "Sensores de temperatura precisam do Heimdall rodando como administrador.",
        "Temperature sensors require Heimdall to be running as administrator.");

    // ---------- Widget de mídia (MediaWidget) ----------

    public static string MediaPrevious => T("Faixa anterior", "Previous track");
    public static string MediaNext => T("Próxima faixa", "Next track");
    public static string MediaPlayPauseTooltip => T("Tocar/Pausar", "Play/Pause");
    public static string MediaPlay => T("Tocar", "Play");
    public static string MediaPause => T("Pausar", "Pause");
    public static string MediaVolume => T("Volume", "Volume");
    public static string MediaMute => T("Mudo", "Mute");
    public static string MediaUnmute => T("Ativar som", "Unmute");
    public static string MediaMutedLabel => T("Mudo", "Muted");

    // ---------- Popup de adicionar/editar lembrete (QuickAddReminderWindow) ----------

    public static string ReminderNewTitle => T("Heimdall — Novo lembrete", "Heimdall — New reminder");
    public static string ReminderEditTitle => T("Heimdall — Editar lembrete", "Heimdall — Edit reminder");
    public static string ChipNone => T("Sem horário", "No time");
    public static string ChipPlus15 => T("+15 min", "+15 min");
    public static string ChipPlus1h => T("+1 h", "+1 h");
    public static string ChipToday18 => T("Hoje 18h", "Today 6 PM");
    public static string ChipTomorrow9 => T("Amanhã 9h", "Tomorrow 9 AM");
    public static string ChipCustom => T("Personalizado", "Custom");
    public static string MoreOptions => T("Mais opções", "More options");
    public static string RecurOnce => T("Única", "Once");
    public static string RecurDaily => T("Diária", "Daily");
    public static string RecurWeekly => T("Dias da semana", "Days of week");
    public static string PlaySound => T("Tocar som", "Play sound");
    public static string RangeFrom => T("De:", "From:");
    public static string RangeTo => T("até:", "to:");

    public static string DayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => T("Seg", "Mon"),
        DayOfWeek.Tuesday => T("Ter", "Tue"),
        DayOfWeek.Wednesday => T("Qua", "Wed"),
        DayOfWeek.Thursday => T("Qui", "Thu"),
        DayOfWeek.Friday => T("Sex", "Fri"),
        DayOfWeek.Saturday => T("Sáb", "Sat"),
        _ => T("Dom", "Sun")
    };

    // ---------- Renomear (RenamePromptWindow) ----------

    public static string RenameTitle => T("Heimdall — Renomear", "Heimdall — Rename");

    // ---------- Escolher app instalado (InstalledAppPickerWindow) ----------

    public static string PickAppTitle => T("Heimdall — Escolher app instalado", "Heimdall — Choose installed app");
    public static string Add => T("Adicionar", "Add");

    // ---------- Histórico de lembretes (ReminderHistoryWindow) ----------

    public static string HistoryTitle => T("Heimdall — Histórico de lembretes", "Heimdall — Reminder history");
    public static string HistoryEmpty => T("Nenhum lembrete concluído ainda.", "No completed reminders yet.");
    public static string HistoryCultureName => T("pt-BR", "en-US");
    public static string HistoryMonthFormat => T("MMMM 'de' yyyy", "MMMM yyyy");

    // ---------- Erro de config (ConfigService) ----------

    public static string ConfigLoadErrorTitle => "Heimdall";
    public static string ConfigLoadErrorBody(string message) => T(
        $"Erro ao ler a configuração:\n{message}\n\nUsando configuração padrão.",
        $"Error reading configuration:\n{message}\n\nUsing default configuration.");

    // ---------- Tela de Configurações (SettingsWindow) ----------

    public static string SettingsTitleBar => T("Configurações — Heimdall", "Settings — Heimdall");
    public static string NavGeral => T("Geral", "General");
    public static string NavAparencia => T("Aparência", "Appearance");
    public static string NavRelogio => T("Relógio", "Clock");
    public static string NavWidgets => T("Widgets", "Widgets");
    public static string NavLembretes => T("Lembretes", "Reminders");
    public static string NavSobre => T("Sobre", "About");
    public static string Cancel => T("Cancelar", "Cancel");
    public static string SaveAndReload => T("Salvar e recarregar", "Save and reload");
    public static string Saved => T("Salvo ✓", "Saved ✓");

    // Geral
    public static string SectionGeral => T("Geral", "General");
    public static string LabelEdge => T("Borda", "Edge");
    public static string LabelThickness => T("Espessura (DIPs)", "Thickness (DIPs)");
    public static string LabelFloatingMode => T("Modo flutuante (margem das bordas + cantos arredondados)", "Floating mode (screen margin + rounded corners)");
    public static string LabelFloatingMargin => T("Margem flutuante (DIPs)", "Floating margin (DIPs)");
    public static string LabelMonitorMode => T("Modo de monitor", "Monitor mode");
    public static string LabelMonitorSpecific => T("Monitor específico", "Specific monitor");
    public static string LabelStartWithWindows => T("Iniciar com o Windows", "Start with Windows");
    public static string LabelGamingMode => T("Ativar modo gaming (overlay em tela cheia)", "Enable gaming mode (fullscreen overlay)");
    public static string HintGamingMode => T(
        "Quando desativado, a barra some ao abrir um jogo em tela cheia, em vez de virar overlay transparente.",
        "When off, the bar just hides when a fullscreen game opens, instead of turning into a transparent overlay.");
    public static string LabelLanguage => T("Idioma", "Language");
    public static string LanguagePtBr => "Português (Brasil)";
    public static string LanguageEnUs => "English";

    // Aparência
    public static string SectionAparencia => T("Aparência", "Appearance");
    public static string LabelPreview => T("Prévia", "Preview");
    public static string LabelTheme => T("Tema", "Theme");
    public static string LabelOverridesOptional => T("Overrides opcionais (vazio = usa o tema)", "Optional overrides (empty = use theme)");
    public static string HintBackgroundColor => T("Cor de fundo", "Background color");
    public static string HintForegroundColor => T("Cor do texto", "Text color");
    public static string Clear => T("Limpar", "Clear");
    public static string LabelFont => T("Fonte", "Font");
    public static string LabelCustomFontSize => T("Personalizar tamanho da fonte", "Customize font size");

    public static string ColorPresetName(string ptBrName) => ptBrName switch
    {
        "Âmbar" => T("Âmbar", "Amber"),
        "Verde-azulado" => T("Verde-azulado", "Teal"),
        "Azul" => T("Azul", "Blue"),
        "Fundo" => T("Fundo", "Background"),
        "Painel" => T("Painel", "Panel"),
        "Campo" => T("Campo", "Field"),
        "Marfim" => T("Marfim", "Ivory"),
        "Secundário" => T("Secundário", "Secondary"),
        "Cabo" => T("Cabo", "Handle"),
        "Branco" => T("Branco", "White"),
        "Preto" => T("Preto", "Black"),
        "Vermelho" => T("Vermelho", "Red"),
        "Verde" => T("Verde", "Green"),
        "Roxo" => T("Roxo", "Purple"),
        _ => ptBrName
    };

    // Relógio
    public static string SectionRelogio => T("Relógio", "Clock");
    public static string LabelClockMode => T("Modo", "Mode");
    public static string LabelClockStyle => T("Estilo", "Style");
    public static string LabelClockCulture => T("Cultura (ex.: pt-BR, en-US)", "Culture (e.g.: pt-BR, en-US)");
    public static string LabelClockCustomFormat => T("Formato personalizado (.NET) — só com Modo = Custom", "Custom format (.NET) — only with Mode = Custom");
    public static string LabelPreviewSection => T("Pré-visualização", "Preview");
    public static string PreviewHorizontal => T("Barra horizontal:  ", "Horizontal bar:  ");
    public static string PreviewVertical => T("Barra vertical:  ", "Vertical bar:  ");

    // Widgets
    public static string SectionWidgets => T("Widgets", "Widgets");
    public static string HintWidgets => T(
        "Arraste pra reordenar, inclusive entre colunas. Clique num item e depois no alfinete pra fixar/desafixar.",
        "Drag to reorder, including between columns. Click an item then the pin to pin/unpin it.");
    public static string ZoneStart => T("Início", "Start");
    public static string ZoneCenter => T("Centro", "Center");
    public static string ZoneEnd => T("Fim", "End");
    public static string TogglePinTooltip => T("Fixar/desafixar selecionado", "Pin/unpin selected");
    public static string AddWidgetButton => T("+ Adicionar widget", "+ Add widget");

    // Lembretes
    public static string SectionLembretes => T("Lembretes", "Reminders");
    public static string AddReminder => T("Adicionar lembrete", "Add reminder");
    public static string RemoveSelected => T("Remover selecionado", "Remove selected");
    public static string ColumnType => T("Tipo", "Type");
    public static string ColumnText => T("Texto", "Text");
    public static string ColumnTime => T("Horário (HH:mm)", "Time (HH:mm)");
    public static string ColumnRecurrence => T("Recorrência", "Recurrence");
    public static string ColumnDays => T("Dias (Weekly)", "Days (Weekly)");
    public static string ColumnSound => T("Som", "Sound");
    public static string ColumnCompleted => T("Concluído", "Completed");
    public static string NewReminderDefaultText => T("Novo lembrete", "New reminder");
    public static string InvalidTimeMessage(string text) => T(
        $"Horário inválido em \"{text}\" — use o formato HH:mm.",
        $"Invalid time in \"{text}\" — use the HH:mm format.");

    // Sobre
    public static string SectionSobre => T("Sobre", "About");
    public static string VersionDev => T("Versão de desenvolvimento", "Development version");
    public static string VersionFormat(string version) => T($"Versão {version}", $"Version {version}");
    public static string AppTagline => T("Barra de sistema leve e personalizável pro Windows.", "A lightweight, customizable system bar for Windows.");
    public static string ViewOnGitHub => T("Ver no GitHub", "View on GitHub");
}
