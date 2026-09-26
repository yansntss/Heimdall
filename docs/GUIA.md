# Guia completo — Heimdall

Este guia cobre em detalhe tudo que o [README](../README.md) só resume: configuração
completa via JSON, sistema de temas, cada widget, modo flutuante, modo overlay e a
estrutura interna do código.

## Configuração completa

Config: `%AppData%\Heimdall\config.json` (criado na 1ª execução).

```jsonc
{
  "Edge": "Top",               // Top | Bottom | Left | Right
  "Thickness": 28,              // DIPs. Laterais: ~72
  "FloatingMode": false,        // true = barra flutuante (margem + cantos arredondados)
  "FloatingMargin": 8,          // DIPs de respiro nos 4 lados, só com FloatingMode
  "MonitorMode": "Primary",     // Primary | Specific | All
  "MonitorDevice": "DISPLAY2",  // usado em Specific (ver "Monitores detectados")
  "Theme": "Escuro",            // nome de um tema embutido ou salvo em .../themes/*.json
  "GamingMode": true,           // true = overlay transparente em tela cheia; false = a barra só some
  "Style": {                    // overrides opcionais por cima do tema — null usa o tema
    "Background": null,
    "Foreground": null,
    "FontFamily": null,
    "FontSize": null
  },
  "Widgets": {                  // cada item é {Id, Pinned} — Pinned não se move nem é movido no arraste
    "Start": [],
    "Center": [{ "Id": "clock", "Pinned": false }],
    "End": [{ "Id": "media" }, { "Id": "reminder" }, { "Id": "launcher" }]
  },
  "Clock": {
    "Mode": "Both",              // TimeOnly | DateOnly | Both | Custom
    "Style": "Classic",          // Classic | Compact | Verbose | ISO — ignorado com Mode = Custom
    "Culture": "pt-BR",
    "CustomFormat": null         // formato .NET livre, só com Mode = Custom (não varia por orientação)
  },
  "Reminders": [
    { "Kind": "Fixed", "Text": "Beber água" },
    {
      "Kind": "Scheduled",
      "Text": "Reunião diária",
      "Time": "09:00",
      "Recurrence": "Weekly",
      "Days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
      "PlaySound": true
    }
  ],
  "Launchers": [
    { "Name": "Bloco de notas", "Path": "shell:AppsFolder\\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App" },
    { "Name": "Calculadora", "Path": "C:\\Windows\\System32\\calc.exe" }
  ]
}
```

## Configurações pela interface

Clique direito na barra → **Configurações...** abre uma janela com abas — não precisa
editar o `config.json` na mão pro dia a dia:

- **Geral**: borda, espessura, modo flutuante (+ margem), modo/escolha de monitor,
  "Iniciar com o Windows", modo gaming (ver "Modo overlay" abaixo).
- **Aparência**: dropdown com os temas (embutidos + os seus, salvos em
  `%AppData%\Heimdall\themes\*.json` — ver "Temas" abaixo), e overrides opcionais de
  cor de fundo/texto (com seletor nativo), fonte e tamanho por cima do tema escolhido.
- **Relógio**: dropdown de Modo e Estilo, cultura e formato personalizado, com
  pré-visualização ao vivo (horizontal e vertical) — ver "Relógio" abaixo.
- **Widgets**: reordena/adiciona/remove os widgets de cada zona (início/centro/fim), e o
  botão 📌 fixa/desafixa o selecionado. `launcher` (atalhos) entra aqui como qualquer
  outro — os itens em si (`Launchers`) são geridos direto pela barra, não por essa tela
  (ver "Widget: launcher"). Arrastar direto na barra faz a mesma coisa sem abrir essa tela
  (ver "Arrastar e fixar widgets" abaixo).
- **Lembretes**: grid pra adicionar/editar/remover os itens de `Reminders`.

"Salvar e recarregar" grava o `config.json` e aplica na hora, sem reiniciar o app. Quem
preferir editar o JSON direto ainda pode, pelo item "Editar config.json" do mesmo menu.

### Iniciar com o Windows

O checkbox na aba Geral grava/remove uma entrada em
`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, apontando pro
executável atual — sem instalador nem tarefa agendada. Importante: ele aponta pro `.exe`
que está rodando *no momento em que você salva*. Em `dotnet run`/debug isso é o apphost
de debug; assim que você rodar a partir do `.exe` publicado, é só reativar o checkbox uma
vez pra apontar pro caminho certo. Se mover ou republicar o app pra outra pasta, reative
de novo.

## Relógio

`Clock.Mode` decide o que mostra: `TimeOnly` (só hora), `DateOnly` (só data), `Both` (os
dois) ou `Custom` (formato .NET livre em `CustomFormat`, ignora `Style`). Com `Both` ou
`DateOnly`, `Clock.Style` escolhe o formato:

| Style | Exemplo (horizontal) | Exemplo (vertical) |
|---|---|---|
| `Classic` | `qui, 25/09/2026   14:30` | `14:30` / `25/09` |
| `Compact` | `25/09 14:30` | `14:30` / `25/09` |
| `Verbose` | `quinta-feira, 25 de setembro   14:30:45` | `14:30:45` / `25/09` |
| `ISO` | `2026-09-25 14:30:45` | `2026-09-25` / `14:30:45` |

Na barra vertical (laterais), a data sempre usa a variante curta de cada estilo — não é
um campo solto, faz parte da definição do `Style`. `Clock.Culture` (`pt-BR`, `en-US`, ...)
controla nomes de dia/mês e formatos regionais. A aba **Relógio** das Configurações tem
dropdown de Modo/Estilo e uma pré-visualização ao vivo dos dois formatos antes de salvar.

Configs antigos com `TimeFormat`/`DateFormat`/`VerticalDateFormat` são migrados
automaticamente pro `Mode: "Custom"` na primeira leitura, preservando o formato exato que
já estava configurado (só perde a variante vertical, que no `Custom` novo não varia por
orientação — dá pra trocar pra um `Style` pronto depois se quiser).

## Arrastar e fixar widgets

Qualquer widget (`clock`, `media`, `reminder`, `launcher`, `separator`) pode ser
arrastado — pressione e mova alguns pixels pra pegar. Um fantasma (miniatura do próprio
widget) segue o cursor, os vizinhos deslizam pra abrir espaço, e ele "voa" até o lugar
certo ao soltar — a mesma mecânica do arraste de ícones do `launcher`, só que pra widgets
inteiros, inclusive entre zonas (início/centro/fim). Esc cancela e volta tudo pro lugar.
A ordem e o alvo são salvos no `config.json` (e a barra recarrega) direto ao soltar, sem
precisar abrir as Configurações.

Clique direito num widget (numa área sem menu próprio, como o texto do relógio ou o fundo
do widget de mídia) → **Fixar posição** trava ele: não se move quando outros são
arrastados por perto, e ele mesmo não pode ser arrastado (o cursor mostra "bloqueado" ao
tentar). Um alfinete discreto aparece no canto ao passar o mouse por cima pra lembrar que
está fixado. **Desafixar** no mesmo menu libera de novo.

## Temas

9 temas embutidos — **Escuro**, **Claro**, **Translúcido Escuro**, **Translúcido Claro**,
**Acrílico**, **Mica**, **Vidro**, **Destaque do Windows** (usa a cor de destaque do
sistema) e **Auto** (acompanha o tema claro/escuro do Windows, trocando sozinho quando
ele muda). Escolha rápida pelo clique direito na barra → **Tema**, ou pela aba Aparência
das Configurações.

Temas de usuário são arquivos `.json` em `%AppData%\Heimdall\themes\`, com o mesmo
formato dos embutidos:

```jsonc
{
  "Name": "Meu tema",
  "Background": "#E61E1E1E",  // #AARRGGBB
  "TextPrimary": "#FFFFFFFF",
  "TextSecondary": "#FFB0B0B0",
  "Accent": "#FF4A9EFF",
  "Hover": "#22FFFFFF",
  "Border": "#22FFFFFF",
  "CornerRadius": 8,
  "Backdrop": "Translucent",   // Solid | Translucent | Acrylic | Mica | None
  "FontFamily": "Segoe UI"
}
```

Aparecem automaticamente no dropdown/menu assim que o arquivo existe — não precisa
reiniciar o app, só recarregar.

## Modo flutuante

Com `FloatingMode: true`, a barra ganha uma margem (`FloatingMargin`, em DIPs) das 4
bordas da tela e cantos arredondados, em vez de grudar na borda de ponta a ponta. O
espaço reservado do AppBar (que empurra janelas maximizadas) inclui essa margem, então o
respiro visual é respeitado mesmo sem a barra tocar a borda física da tela.

## Modo overlay (jogos em tela cheia)

Quando um app entra em tela cheia (detectado via notificação do shell e, como reforço,
comparando o retângulo da janela em primeiro plano com o monitor), o que acontece depende
de `GamingMode` (padrão `true`):

- **`true`** — a barra normal some e um overlay discreto assume: sem interceptar clique
  (`WS_EX_TRANSPARENT`), sem os botões interativos dos widgets — só texto/ícones
  informativos, com contorno pra ler sobre qualquer fundo.
- **`false`** — a barra simplesmente some (sem overlay, sem transparência, sem clique
  atravessando) e volta a aparecer normal ao sair da tela cheia.

Em qualquer um dos dois, a troca é um fade de 200 ms, não um corte seco. Alternar rápido
pelo clique direito na barra → **Modo gaming: Ativado/Desativado**, ou pela aba Geral das
Configurações.

## Widgets em detalhe

- **`clock`** — relógio/data (ver `Clock` acima).
- **`media`** — controle de mídia via SMTC (Spotify, YouTube, qualquer player), sem
  login. Botões ⏮ ⏯ ⏭ (habilitados/desabilitados conforme o player permite), capa do
  álbum, barra de progresso fina, texto com marquee no hover quando não cabe, e um ícone
  de equalizador animado enquanto toca. Roda do mouse ajusta o volume por app (via
  NAudio, com fallback pro volume master) em passos de 5%; clicar no ícone de volume
  abre um slider vertical. Atalhos globais opcionais: `Ctrl+Alt+Espaço` (play/pause),
  `Ctrl+Alt+←/→` (anterior/próximo), `Ctrl+Alt+↑/↓` (volume). Fica oculto quando nada
  está tocando.
- **`reminder`** — lembretes fixos e agendados. Botão "+" no hover (ou `Ctrl+Shift+R`)
  abre um popup rápido pra criar um lembrete: chips de horário (Sem horário/+15 min/+1h/
  Hoje 18h/Amanhã 9h/Personalizado), recorrência (Única/Diária/Dias da semana, com data
  de início/fim opcional) e som. Cada lembrete pode ser editado ou excluído pelo clique
  direito. `Kind: "Fixed"` fica sempre visível como chip; `Kind: "Scheduled"` só aparece
  na barra (pulsando com a cor de destaque) quando dispara — concluir marca
  `Completed: true` (uma vez, se `Recurrence: "Once"`) e grava no histórico, acessível
  pelo menu da barra → "Ver histórico de lembretes".
- **`launcher`** — ícones de atalhos: executáveis, `.lnk` (resolve pro destino real pra
  pegar o ícone certo), pastas, arquivos, URLs, e apps da Microsoft Store (via
  `shell:AppsFolder\{AppUserModelId}`). Ícones extraídos em alta resolução (cache em
  `%AppData%\Heimdall\cache\icons\`) e escalados pela espessura da barra. Clique abre;
  clique direito dá "Executar como administrador", "Abrir local do arquivo",
  "Renomear..." e "Remover"; um ícone é arrastável pra reordenar; um traço embaixo indica
  que o app já está aberto (varre as janelas de topo reais a cada poucos segundos via
  `EnumWindows`, comparando o executável dono com o `Path` do atalho — só funciona pra
  atalhos locais, não pra apps da Store/URLs). Clicar num ícone com o traço foca a janela
  em vez de abrir outra instância (restaura se estiver minimizada); se o app estiver
  elevado e o Heimdall não, `SetForegroundWindow` falha e o clique não faz nada — é uma
  limitação do Windows, sem contorno sem elevar o Heimdall também. Pra adicionar: arraste
  um arquivo/atalho/pasta pra cima da barra, ou clique direito na barra → **Adicionar
  atalho** (escolher um arquivo, ou escolher entre os apps instalados com busca). Some no
  modo overlay.

## Estrutura do código

- `Native/` — P/Invoke: `AppBarManager` (reserva de espaço, DPI, restart do Explorer,
  detecção de tela cheia), `DwmVisuals` (backdrop/cantos), `HotkeyManager` (hotkeys
  globais), `ShellInterop` (extração de ícone e resolução de `.lnk`).
- `Services/` — `ThemeService` (temas embutidos/usuário), `MonitorService`,
  `StartupService`, `AudioVolumeService` (volume por app), `IconCacheService`,
  `InstalledAppsService` (lista de apps instalados), `ReminderHistoryService`,
  `OpenWindowsService` (janelas de topo abertas, pro indicador do `launcher`).
- `Config/` — modelo e leitura/gravação do JSON (`AppConfig`, `WidgetEntry`, `ThemeConfig`,
  `ReminderConfig`, `LauncherConfig`) — `ConfigService` também migra formatos antigos
  (`Clock` solto → `Mode`/`Style`, `Widgets.*` de string pra `{Id, Pinned}`).
- `Widgets/` — `IWidget`, `WidgetFactory`, `ClockWidget` + `ClockFormatter`, `MediaWidget`,
  `ReminderWidget`, `LauncherWidget`.
- `UI/` — `BarPresenter` (troca barra↔overlay), `BarWindow`/`OverlayWindow` (3 zonas:
  início/centro/fim, via `WidgetZoneBuilder`), `WidgetDragController` (arrastar widgets
  entre zonas), `GhostIconWindow` (fantasma do arraste, reaproveitado pelo `launcher` e
  pelo `WidgetDragController`), `SettingsWindow`, `QuickAddReminderWindow`,
  `ReminderHistoryWindow`, `RenamePromptWindow`, `InstalledAppPickerWindow`.
