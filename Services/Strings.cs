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
        "volume" => T("Volume", "Volume"),
        "mic" => T("Microfone", "Microphone"),
        "ram" => T("RAM", "RAM"),
        "temp" => T("Temperatura", "Temperature"),
        "fps" => T("FPS", "FPS"),
        "brand" => T("Marca", "Brand"),
        "cpu" => T("CPU", "CPU"),
        "notifications" => T("Notificações", "Notifications"),
        "network" => T("Rede", "Network"),
        "separator" => T("Separador", "Separator"),
        _ => id
    };

    // ---------- Menu de contexto da barra (BarWindow) ----------

    public static string MenuEditConfig => T("Editar config.json", "Edit config.json");
    public static string MenuReminderHistory => T("Ver histórico de lembretes", "View reminder history");
    public static string MenuAddShortcut => T("Adicionar atalho", "Add shortcut");
    public static string MenuAddShortcutFromFile => T("Escolher um arquivo...", "Choose a file...");
    public static string MenuAddShortcutFromInstalled => T("Escolher app instalado...", "Choose installed app...");
    public static string MenuAddSeparator => T("Adicionar separador", "Add separator");
    public static string MenuAddWidget => T("Adicionar widget", "Add widget");
    public static string MenuSettingsShort => T("Configurações", "Settings");
    public static string MenuBarPosition => T("Posição da barra", "Bar position");
    public static string MenuAllMonitors => T("Todos os monitores", "All monitors");
    public static string MenuWidgets => T("Widgets", "Widgets");
    public static string MenuOverlayMode => T("Modo overlay", "Overlay mode");
    public static string MenuHideBar => T("Ocultar barra", "Hide bar");
    public static string MenuReloadWidgets => T("Recarregar widgets", "Reload widgets");
    public static string MenuExitApp => T("Sair do Heimdall", "Quit Heimdall");

    // Menu de um widget
    public static string WidgetMenuCaption(string name) => $"WIDGET · {name.ToUpperInvariant()}";
    public static string MenuMove => T("Mover", "Move");
    public static string MenuConfigureWidget => T("Configurar widget", "Configure widget");
    public static string MenuHideWidget => T("Ocultar widget", "Hide widget");
    public static string MenuOpenCalendar => T("Abrir calendário", "Open calendar");
    public static string MenuTodayReminders => T("Ver lembretes de hoje", "View today's reminders");
    public static string MenuSystemMonitor => T("Abrir monitor do sistema", "Open system monitor");
    public static string MenuNetworkSettings => T("Configurações de rede", "Network settings");

    // Ícone da bandeja
    public static string TrayShowBar => T("Mostrar barra", "Show bar");
    public static string TrayPauseReminders => T("Pausar lembretes", "Pause reminders");
    public static string TrayCheckUpdates => T("Verificar atualizações", "Check for updates");
    public static string TrayUpToDate(string version) => T($"Você já está na versão mais recente ({version}).", $"You are on the latest version ({version}).");

    public static string DialogChooseFileTitle => T("Escolher arquivo ou atalho", "Choose file or shortcut");
    public static string DialogAllFiles => T("Todos os arquivos", "All files");

    public static string MonitorPrimarySuffix => T("  [principal]", "  [primary]");

    // ---------- Separador do launcher (submenu de estilo) ----------

    public static string SeparatorStyleLine => T("Linha", "Line");
    public static string SeparatorStyleSpace => T("Espaço", "Space");
    public static string SeparatorStyleDot => T("Ponto", "Dot");
    public static string Remove => T("Remover", "Remove");
    public static string Pin => T("Fixar posição", "Pin position");
    public static string Unpin => T("Desafixar", "Unpin");
    public static string RemoveWidget => T("Remover widget", "Remove widget");

    // ---------- Aviso de versão nova ----------

    public static string UpdateAvailableTitle => T("Nova versão disponível", "New version available");
    public static string UpdateAvailableBody(string tag) => T($"O Heimdall {tag} saiu. Veja o que mudou e baixe na página da versão.", $"Heimdall {tag} is out. See what changed and download it on the release page.");
    public static string UpdateSeeWhatsNew => T("Ver novidades", "See what's new");
    public static string UpdateLater => T("Agora não", "Not now");
    public static string UpdateTooltip(string tag) => T($"Nova versão {tag} disponível — clique para ver o que mudou e baixar", $"New version {tag} available — click to see what changed and download");
    public static string UpdateButtonName => T("Baixar nova versão", "Download new version");

    // ---------- Widgets novos (marca, CPU, notificações, rede) ----------

    public static string BrandTooltip => T("Heimdall — abrir configurações", "Heimdall — open settings");
    public static string CpuTooltip(double percent) => T($"Uso de CPU: {percent:0}%", $"CPU usage: {percent:0}%");
    public static string NotificationsName => T("Lembretes de hoje", "Today's reminders");
    public static string NotificationsTooltip(int pending) => pending switch
    {
        0 => T("Nenhum lembrete pendente hoje", "No pending reminders today"),
        1 => T("1 lembrete pendente hoje", "1 pending reminder today"),
        _ => T($"{pending} lembretes pendentes hoje", $"{pending} pending reminders today")
    };
    public static string NetworkName => T("Rede", "Network");
    public static string NetworkConnected(string name, bool wifi) =>
        wifi ? T($"Wi-Fi: {name}", $"Wi-Fi: {name}") : T($"Cabo: {name}", $"Wired: {name}");
    public static string NetworkDisconnected => T("Sem conexão de rede", "No network connection");
    public static string MediaOpenPlayer => T("Abrir player", "Open player");

    // ---------- Dropdowns da barra (mídia, lembretes, sistema) ----------

    public static string FlyoutMediaTitle => T("Heimdall — Mídia", "Heimdall — Media");
    public static string MediaNothingPlaying => T("Nada tocando", "Nothing playing");
    public static string MediaSeek => T("Posição da faixa", "Track position");
    public static string MediaAppVolume => T("Volume do app", "App volume");

    public static string FlyoutRemindersTitle => T("Heimdall — Lembretes de hoje", "Heimdall — Today's reminders");
    public static string RemindersToday => T("HOJE", "TODAY");
    public static string RemindersPending(int pending) => pending == 1 ? T("1 pendente", "1 pending") : T($"{pending} pendentes", $"{pending} pending");
    public static string RemindersNew => T("Novo lembrete", "New reminder");
    public static string ReminderMetaFixed => T("Fixo", "Pinned");
    public static string ReminderMetaOnce => T("Uma vez", "Once");
    public static string ReminderMetaDaily => T("Todo dia", "Every day");
    public static string ReminderMetaWeekly => T("Semanal", "Weekly");
    public static string ReminderMetaMonthly => T("Mensal", "Monthly");
    public static string ReminderMetaYearly => T("Anual", "Yearly");
    public static string ReminderDefaultList => T("Lembretes", "Reminders");
    public static string ReminderMetaToday => T("Hoje", "Today");
    public static string ReminderMetaAllDay => T("Dia todo", "All day");
    public static string RemindersOverdue => T("ATRASADOS", "OVERDUE");
    public static string ReminderSnoozeMinutes(int minutes) => T($"Adiar {minutes} min", $"Snooze {minutes} min");
    public static string ReminderFlag => T("Sinalizar", "Flag");
    public static string ReminderUnflag => T("Remover sinalização", "Unflag");
    public static string ReminderFlagged => T("Sinalizado", "Flagged");
    public static string ReminderHighPriority => T("Prioridade alta", "High priority");

    // ---------- Janela de lembretes (RemindersWindow) ----------

    public static string RemindersWindowTitle => T("Heimdall — Lembretes", "Heimdall — Reminders");
    public static string RemindersWindowHeading => T("Lembretes", "Reminders");
    public static string RemindersOpenAll => T("Ver todos", "View all");
    public static string MenuOpenReminders => T("Abrir lembretes", "Open reminders");
    public static string SmartListToday => T("Hoje", "Today");
    public static string SmartListScheduled => T("Agendados", "Scheduled");
    public static string SmartListFlagged => T("Sinalizados", "Flagged");
    public static string SmartListAll => T("Todos", "All");
    public static string SmartListsName => T("Listas inteligentes", "Smart lists");
    public static string MyLists => T("MINHAS LISTAS", "MY LISTS");
    public static string MyListsName => T("Minhas listas", "My lists");
    public static string ReminderNoDate => T("Sem data", "No date");
    public static string SuggestNextMonday => T("Próxima segunda", "Next Monday");
    public static string ToastName => T("Lembrete", "Reminder");

    // ---------- Configurações → Lembretes ----------

    public static string LembretesSubtitle => T("Avisos, adiamento e as suas listas. Os lembretes em si ficam na janela de lembretes.",
        "Notices, snooze and your lists. The reminders themselves live in the reminders window.");
    public static string LabelReminderNotices => T("Avisos", "Notices");
    public static string LabelDefaultTime => T("Hora padrão", "Default time");
    public static string HintDefaultTime => T("Lembrete com data e sem hora avisa neste horário.", "Reminders with a date but no time notify at this time.");
    public static string LabelSnoozeMinutes => T("Adiar por (minutos)", "Snooze for (minutes)");
    public static string HintSnoozeMinutes => T("Usado no \"Adiar\" do aviso e do menu.", "Used by \"Snooze\" in notices and menus.");
    public static string LabelReminderSound => T("Tocar som no aviso", "Play a sound on notices");
    public static string HintReminderSound => T("Desligado por padrão.", "Off by default.");
    public static string LabelShowCompleted => T("Mostrar concluídos", "Show completed");
    public static string HintShowCompleted => T("Na janela de lembretes, os concluídos aparecem no fim de cada lista.", "In the reminders window, completed ones show at the end of each list.");
    public static string LabelMyListsSettings => T("Minhas listas", "My lists");
    public static string HintMyLists => T("Excluir uma lista manda os lembretes dela para a lista padrão.", "Deleting a list moves its reminders to the default list.");
    public static string AddList => T("Nova lista", "New list");
    public static string NewListName => T("Nova lista", "New list");
    public static string ListNameField => T("Nome da lista", "List name");
    public static string ChangeListColor => T("Trocar cor", "Change color");
    public static string DeleteList => T("Excluir", "Delete");
    public static string ConfirmDeleteList => T("Confirmar", "Confirm");
    public static string DefaultListBadge => T("Padrão", "Default");
    public static string ReminderCount(int count) => count == 1 ? T("1 lembrete", "1 reminder") : T($"{count} lembretes", $"{count} reminders");
    public static string AllClear => T("Tudo em ordem", "All clear");
    public static string NothingScheduled => T("Nada agendado por enquanto", "Nothing scheduled yet");
    public static string NextUp(string title, string when) => T($"Próximo: {title} · {when}", $"Next: {title} · {when}");
    public static string RemindersCompletedSection => T("CONCLUÍDOS", "COMPLETED");
    public static string ReminderUncompleteNamed(string text) => T($"Desmarcar lembrete: {text}", $"Mark as not done: {text}");
    public static string ToastOverdueGroup(int count) => T($"{count} lembretes atrasados", $"{count} overdue reminders");
    public static string ToastGroup(int count) => T($"{count} lembretes", $"{count} reminders");
    public static string ToastOpen => T("Ver lembretes", "View reminders");
    public static string ToastDismiss => T("Dispensar", "Dismiss");
    public static string ToastClose => T("Fechar aviso", "Close notice");
    public static string MoreDetails => T("Mais detalhes", "More details");
    public static string DetailsName => T("Detalhes do lembrete", "Reminder details");
    public static string DetailsDate => T("Data", "Date");
    public static string DetailsTime => T("Hora", "Time");
    public static string DetailsRepeat => T("Repetir", "Repeat");
    public static string DetailsList => T("Lista", "List");
    public static string DetailsPriority => T("Prioridade", "Priority");
    public static string DetailsNotes => T("Notas", "Notes");
    public static string DetailsNotesAdd => T("Adicionar", "Add");
    public static string DetailsFlag => T("Sinalizar", "Flag");
    public static string DetailsCancel => T("Cancelar", "Cancel");
    public static string DetailsAdd => T("Adicionar", "Add");
    public static string DetailsSave => T("Salvar", "Save");
    public static string DetailsBack => T("Voltar", "Back");
    public static string TimeLater => T("Mais 15 minutos", "15 minutes later");
    public static string TimeEarlier => T("Menos 15 minutos", "15 minutes earlier");
    public static string RepeatNever => T("Nunca", "Never");
    public static string RepeatDaily => T("Diariamente", "Daily");
    public static string RepeatWeekly => T("Semanalmente", "Weekly");
    public static string RepeatMonthly => T("Mensalmente", "Monthly");
    public static string RepeatYearly => T("Anualmente", "Yearly");
    public static string RepeatWeekdays => T("Dias úteis", "Weekdays");
    public static string RepeatCustom => T("Personalizado", "Custom");
    public static string RepeatEvery => T("A cada", "Every");
    public static string RepeatOnDays => T("NOS DIAS", "ON DAYS");
    public static string RepeatOnDaysName => T("Nos dias", "On days");
    public static string RepeatEndLabel => T("Terminar", "End");
    public static string RepeatEndNever => T("Nunca", "Never");
    public static string RepeatEndOnDate => T("Em uma data", "On a date");
    public static string RepeatEndAfterCount => T("Após N vezes", "After N times");
    public static string RepeatEndAfterTimes(int count) => T($"Após {count} vezes", $"After {count} times");
    public static string RepeatTimes => T("Vezes", "Times");
    public static string RepeatSummaryName => T("Resumo da repetição", "Repeat summary");
    public static string UnitDay => T("dia", "day");
    public static string UnitWeek => T("semana", "week");
    public static string UnitMonth => T("mês", "month");
    public static string UnitYear => T("ano", "year");
    public static string PriorityNone => T("Nenhuma", "None");
    public static string PriorityLow => T("Baixa", "Low");
    public static string PriorityMedium => T("Média", "Medium");
    public static string PriorityHigh => T("Alta", "High");
    public static string SuggestPlusWeek => T("+1 semana", "+1 week");
    public static string CalendarPreviousMonth => T("Mês anterior", "Previous month");
    public static string CalendarNextMonth => T("Próximo mês", "Next month");
    public static string CalendarName => T("Calendário", "Calendar");
    public static string ChipDate => T("Data", "Date");
    public static string ChipNote => T("Nota", "Note");
    public static string ChooseDate => T("Escolher data", "Choose date");
    public static string ChooseList => T("Escolher lista", "Choose list");
    public static string ReminderTitleField => T("Título do lembrete", "Reminder title");
    public static string ReminderNoteField => T("Nota do lembrete", "Reminder note");
    public static string QuickTomorrow => T("Amanhã", "Tomorrow");
    public static string ReminderListCount(string name, int count) => T($"{name}, {count} lembrete(s)", $"{name}, {count} reminder(s)");
    public static string ReminderCompleteNamed(string text) => T($"Concluir lembrete: {text}", $"Complete reminder: {text}");

    public static string FlyoutSystemTitle => T("Heimdall — Sistema", "Heimdall — System");
    public static string SystemMemory => T("Memória", "Memory");
    public static string SystemDisk => T("Disco", "Disk");
    public static string SystemUptime(TimeSpan up) => up.TotalDays >= 1
        ? T($"Ligado há {(int)up.TotalDays} d {up.Hours} h", $"Up for {(int)up.TotalDays} d {up.Hours} h")
        : T($"Ligado há {up.Hours} h {up.Minutes} min", $"Up for {up.Hours} h {up.Minutes} min");
    public static string ActionOverlayMode => T("Modo overlay", "Overlay mode");
    public static string ActionHideBar => T("Ocultar barra", "Hide bar");
    public static string ActionSettings => T("Configurações", "Settings");

    // ---------- Grupos do launcher ----------

    public static string LauncherMoveToGroup => T("Mover para grupo", "Move to group");
    public static string LauncherNoGroup => T("Sem grupo", "No group");
    public static string LauncherNewGroup => T("Novo grupo...", "New group...");
    public static string LauncherDefaultGroupName(int n) => T($"Grupo {n}", $"Group {n}");
    public static string LauncherRenameGroup => T("Renomear grupo...", "Rename group...");
    public static string LauncherRemoveGroup => T("Remover grupo (mantém os atalhos)", "Remove group (keeps shortcuts)");
    public static string LauncherGroupFolder => T("Exibir como pasta", "Show as folder");
    public static string LauncherGroupFolderAuto => T("Automático (só com o monitor em pé)", "Automatic (portrait monitor only)");
    public static string LauncherGroupFolderAlways => T("Sempre", "Always");
    public static string LauncherGroupFolderNever => T("Nunca", "Never");
    public static string LauncherGroupEmpty => T("Grupo vazio — clique direito num atalho → Mover para grupo", "Empty group — right-click a shortcut → Move to group");

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
    public static string WindowsFolderTooltip(int count) => T($"{count} janela(s) aberta(s)", $"{count} open window(s)");
    public static string WindowsUntitled => T("(sem título)", "(untitled)");

    // ---------- Calendário (clique no widget de relógio) ----------

    public static string CalendarTitle => T("Calendário", "Calendar");
    public static string CalendarGoToToday => T("Voltar pro mês atual", "Back to current month");

    // ---------- Widget de lembretes (ReminderWidget) ----------

    public static string ReminderNewTooltip => T("Novo lembrete (Ctrl+Shift+R)", "New reminder (Ctrl+Shift+R)");
    public static string ReminderComplete => T("Concluir", "Complete");
    public static string ReminderEdit => T("Editar...", "Edit...");
    public static string ReminderDelete => T("Excluir", "Delete");

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
    public static string MicNoDevice => T("Nenhum microfone padrão encontrado", "No default microphone found");
    public static string VolumeNoDevice => T("Nenhum dispositivo de saída padrão encontrado", "No default output device found");

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
    public static string NavAtalhos => T("Atalhos", "Shortcuts");
    public static string Apply => T("Aplicar", "Apply");
    public static string RestoreDefaults => T("Restaurar padrões", "Restore defaults");
    public static string Applied => T("Aplicado ✓", "Applied ✓");
    public static string DefaultsRestored => T("Padrões restaurados — clique em Aplicar para manter", "Defaults restored — click Apply to keep them");
    public static string WindowMinimize => T("Minimizar", "Minimize");
    public static string WindowMaximize => T("Maximizar", "Maximize");
    public static string WindowClose => T("Fechar", "Close");
    public static string SidebarRuntime(string version, string runtime) => $"v{version} · {runtime}";

    // Aparência (painel novo)
    public static string AppearanceSubtitle => T("Tudo aqui aparece na barra na hora; Aplicar salva, Cancelar desfaz.",
        "Changes show on the bar right away; Apply saves them, Cancel undoes them.");
    public static string LabelBarPosition => T("Posição da barra", "Bar position");
    public static string EdgeName(BarEdge edge) => edge switch
    {
        BarEdge.Bottom => T("Baixo", "Bottom"),
        BarEdge.Left => T("Esquerda", "Left"),
        BarEdge.Right => T("Direita", "Right"),
        _ => T("Topo", "Top")
    };
    public static string LabelOpacity => T("Opacidade", "Opacity");
    public static string HintOpacitySolid => T("Este tema é sólido — a opacidade vale só nos temas translúcidos.",
        "This theme is solid — opacity only applies to translucent themes.");
    public static string LabelBarHeight => T("Altura", "Height");
    public static string LabelBarWidth => T("Largura", "Width");
    public static string HintHeightCompact => T("A Compacta tem altura fixa de 26px.", "Compact has a fixed 26px height.");
    public static string Decrease => T("Diminuir", "Decrease");
    public static string Increase => T("Aumentar", "Increase");
    public static string LabelAccent => T("Cor de acento", "Accent color");
    public static string AccentWindows => T("Cor do Windows", "Windows color");
    public static string AccentName(string key) => key switch
    {
        "gold" => T("Ouro Hofuð", "Hofuð gold"),
        "teal" => T("Verde-água", "Teal"),
        "blue" => T("Azul", "Blue"),
        "violet" => T("Violeta", "Violet"),
        _ => key
    };
    public static string LabelBehavior => T("Comportamento", "Behavior");
    public static string HintStartWithWindows => T("Abre a barra junto com o Windows.", "Opens the bar when Windows starts.");
    public static string LabelReserveSpace => T("Reservar espaço na área de trabalho", "Reserve desktop space");
    public static string HintReserveSpace => T("Janelas maximizadas param na borda da barra, em vez de ficar por baixo dela.",
        "Maximized windows stop at the bar instead of going under it.");
    public static string LabelAutoHide => T("Ocultar automaticamente", "Auto-hide");
    public static string HintAutoHide => T("A barra some e volta ao encostar o mouse na borda da tela.",
        "The bar hides and comes back when the mouse touches the screen edge.");
    public static string LabelOverlayFullscreen => T("Sobrepor jogos em tela cheia", "Overlay fullscreen games");

    // Atalhos
    public static string SectionAtalhos => T("Atalhos", "Shortcuts");
    public static string AtalhosSubtitle => T("Atalhos globais — funcionam mesmo com outro app (ou um jogo) em foco.",
        "Global shortcuts — they work even with another app (or a game) focused.");
    public static string ShortcutToggleBar => T("Ocultar / mostrar a barra", "Hide / show the bar");
    public static string ShortcutToggleBarAlt => T("Ocultar / mostrar a barra (alternativo)", "Hide / show the bar (alternative)");
    public static string ShortcutNewReminder => T("Novo lembrete", "New reminder");
    public static string ShortcutPlayPause => T("Tocar / pausar", "Play / pause");
    public static string ShortcutPrevNext => T("Faixa anterior / próxima", "Previous / next track");
    public static string ShortcutVolume => T("Volume do player", "Player volume");

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

    // Sobre
    public static string SectionSobre => T("Sobre", "About");
    public static string VersionDev => T("Versão de desenvolvimento", "Development version");
    public static string VersionFormat(string version) => T($"Versão {version}", $"Version {version}");
    public static string AppTagline => T("Barra de sistema leve e personalizável pro Windows.", "A lightweight, customizable system bar for Windows.");
    public static string ViewOnGitHub => T("Ver no GitHub", "View on GitHub");
}
