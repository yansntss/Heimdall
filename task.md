# Heimdall — Tarefas pendentes (Fase 5)

> Etapa 1 (temas + separação BarWindow/OverlayWindow) já foi feita e commitada (`3c4e59a`).
> Ordem sugerida por você: mídia completa → lembretes interativos → detalhes visuais.

## Etapa 2 — Widget de mídia completo

- [x] Botões anterior/play-pause/próximo com ícones Segoe Fluent Icons (fallback Segoe MDL2 Assets)
- [x] Usar `TrySkipPreviousAsync` / `TryTogglePlayPauseAsync` / `TrySkipNextAsync` do SMTC
- [x] Habilitar/desabilitar botões conforme `PlaybackInfo.Controls` (ex: `IsNextEnabled`)
- [x] Ícone de play/pause refletindo estado real via `PlaybackInfoChanged`
- [x] Volume por app via NAudio (`AudioSessionManager` + `SimpleAudioVolume`), casando processo por `SourceAppUserModelId`
- [x] Fallback pro volume master do sistema quando não achar a sessão do app
- [x] Roda do mouse sobre o widget ajusta volume em passos de 5%, com tooltip da porcentagem
- [x] Popup com slider vertical de volume + botão de mudo ao clicar no ícone
- [x] Capa do álbum em miniatura via `MediaProperties.Thumbnail` → `BitmapImage`
- [x] Marquee (rolagem) de título/artista quando não cabe, só com o mouse em cima
- [x] Barra de progresso fina via `GetTimelineProperties()` (ocultar quando o app não informa)
- [x] Layout horizontal: `[capa] Título — Artista [⏮ ⏯ ⏭] [🔊]`
- [x] Layout vertical: capa e botões empilhados, texto só no tooltip
- [x] Modo overlay: só texto da faixa, sem botões (clique atravessa)
- [x] Atalhos globais opcionais: `Ctrl+Alt+Espaço` (play/pause), `Ctrl+Alt+←/→` (anterior/próximo), `Ctrl+Alt+↑/↓` (volume)

## Etapa 3 — Lembretes interativos

- [x] Hover no lembrete mostra botão "✓ Concluir" com fade (via `Opacity`, sem `Visibility.Collapsed`)
- [x] Concluir remove com animação curta e grava no histórico
- [x] Recorrentes: concluir encerra só a ocorrência atual (volta no próximo horário)
- [x] Opção "Excluir" no menu de clique direito do lembrete
- [x] Histórico em `%AppData%\Heimdall\docs\lembretes\*.md` (um arquivo por mês, formato `- [x] Texto — criado dd/MM HH:mm — concluído dd/MM HH:mm`)
- [x] Item "Abrir histórico de lembretes" no menu de clique direito da barra
- [x] Botão "+" no hover do widget de lembretes + atalho global `Ctrl+Shift+R`
- [x] Popup de adição rápida ancorado à barra (posição conforme a borda), com foco (não `NOACTIVATE`)
- [x] Campo de texto com foco automático, Enter salva, Esc fecha, fecha ao perder foco
- [x] Chips de tempo rápido: Sem horário / +15 min / +1 h / Hoje 18h / Amanhã 9h / Personalizado
- [x] "Personalizado" expande seletores de data/hora
- [x] "Mais opções" recolhido por padrão: recorrência (Única/Diária/Dias da semana) + toggle de som
- [x] Salvar direto no `config.json` e atualizar a barra sem recarregar tudo
- [x] Ao disparar, lembrete pulsa com a cor de destaque do tema por alguns segundos
- [x] Badge com contador de lembretes pendentes quando houver mais de um

## Etapa 4 — Detalhes visuais

- [x] Modo flutuante opcional: margem das bordas da tela + cantos arredondados, espaço do AppBar incluindo a margem
- [x] Separadores sutis entre widgets e entre zonas (início/centro/fim)
- [x] Hover com fundo levemente destacado e transição de 150 ms
- [x] Ícone animado de equalizador ao lado da faixa quando estiver tocando
- [x] Tooltips em todos os botões
- [x] Contorno leve no texto do overlay pra legibilidade sobre qualquer fundo (texto duplicado deslocado, não `DropShadowEffect`)
- [x] Animação de fade (200 ms) na troca entre modo barra e overlay

## Outros

- [x] Atualizar `README.md` — ainda descreve a Aparência antiga (3 presets de cor) em vez do sistema de 9 temas + dropdown + temas de usuário já implementado

# Fase 6 — Widget de atalhos (launcher)

## O que pode ser um atalho

- [x] Executáveis (.exe) e atalhos (.lnk) — resolver o .lnk pro destino real pra pegar o ícone certo
- [x] Apps da Microsoft Store (UWP) via `shell:AppsFolder\<AppUserModelId>`
- [x] Pastas, arquivos e URLs, abertos com o programa padrão do Windows

## Configuração

- [x] `AppConfig.Launchers`: lista de itens com `Name`, `Path`, `Arguments`, `WorkingDirectory`, `IconPath`, `RunAsAdmin`

## Ícones

- [x] Extração em alta resolução via `IShellItemImageFactory` (Shell API) — funciona pra .exe, .lnk, pastas e apps da Store (nada de `Icon.ExtractAssociatedIcon`, que só dá 32px e depende de WinForms)
- [x] Cache em PNG em `%AppData%\Heimdall\cache\icons\` pra não extrair de novo a cada inicialização
- [x] Tamanho do ícone acompanha a espessura da barra, com margem

## Widget base

- [x] `LauncherWidget` mostrando os ícones da lista, horizontal (lado a lado) ou vertical (empilhado) conforme a borda
- [x] Funciona em qualquer zona (início/centro/fim), como os outros widgets
- [x] Oculto no modo overlay

## Interação

- [x] Clique abre via `Process.Start(UseShellExecute = true)`
- [x] Hover: destaque leve + tooltip com o nome
- [x] Clique direito: Executar como administrador (`Verb = "runas"`), Abrir local do arquivo, Renomear, Remover
- [x] Arrastar um ícone pra reordenar
- [x] Opcional: ponto abaixo do ícone quando o app está aberto (compara caminho dos processos a cada poucos segundos) — só funciona pra atalhos apontando direto pro .exe real: `calc.exe`/`notepad.exe` no Windows 11 são stubs que redirecionam pro app moderno em outro caminho (`WindowsApps\...`), então não batem na comparação; testado e confirmado com um .exe que não redireciona

## Adicionar atalho

- [x] Arrastar e soltar um arquivo/atalho/pasta na barra (`AllowDrop` na zona do widget) — implementado; não validado ponta a ponta (drag do Explorer entre processos é frágil demais pra automatizar com segurança, mas o handler é o mesmo `AddLaunchers` já testado pelos outros dois fluxos)
- [x] Clique direito na barra → "Adicionar atalho" → escolher arquivo (`OpenFileDialog`)
- [x] Clique direito na barra → "Adicionar atalho" → escolher entre apps instalados (lista de `shell:AppsFolder`, com busca)

## Erros

- [x] Caminho que não existe mais: ícone esmaecido, tooltip "Atalho não encontrado", opção de remover

# Fase 7 — Arrastar com animação e separadores

## Separadores (primeiro, mais simples)

- [x] `LauncherConfig.Type` (`App` | `Separator`) + `Style` (`Line` | `Space` | `Dot`) pros itens de `Launchers`
- [x] `LauncherWidget` renderiza separador conforme o estilo (linha fina/espaço/ponto discreto), cor de borda ou texto secundário do tema, baixa opacidade, ~60% da espessura da barra
- [x] Widget `"separator"` novo registrado no `WidgetFactory`, pra separar widgets inteiros dentro de uma zona (ex: entre `clock` e `media`)
- [x] Clique direito na barra → "Adicionar separador", inserido na posição do clique
- [x] Separador reordenável (mesmo mecanismo de arrastar dos ícones)
- [x] Clique direito no separador → trocar estilo ou remover

## Arrastar com animação (depois, mais trabalhoso)

- [x] Arraste manual com `CaptureMouse` pra reordenar dentro da barra — não usar `DragDrop.DoDragDrop` (bloqueia a thread, cursor padrão do Windows, sem dar pra animar); OLE drag-drop continua só pra receber arquivos de fora (Explorer)
- [x] Só inicia o arraste depois de mover alguns pixels (`SystemParameters.MinimumHorizontalDragDistance`), pra não confundir com clique
- [x] Ao pegar: ícone original vira espaço vazio (opacidade 0); um "fantasma" (janela pequena, transparente, topmost, click-through) segue o cursor, crescendo a ~115% com sombra suave em ~120ms (inclinação por direção não implementada — opcional, ficou de fora)
- [x] Durante o arraste: ícones vizinhos deslizam (~180ms, `CubicEase EaseOut`) só via `RenderTransform`/`TranslateTransform`, sem mexer no layout; posição de destino pelo centro de cada ícone
- [x] Ao soltar: fantasma "voa" até a posição final e volta ao tamanho normal (~200ms, `BackEase`); só então aplica a nova ordem na lista e salva
- [x] Esc cancela o arraste — ícone volta animado pra posição original (checagem por `GetAsyncKeyState`, já que a barra é `WS_EX_NOACTIVATE` e não recebe foco de teclado — corrigido um bug onde só era lida dentro do `PreviewMouseMove`, então segurar parado e apertar Esc nunca cancelava; agora também tem um timer de 40ms como watchdog)
- [x] Arrastar pra longe da barra mostra indicador de remoção (ícone esmaecido com "×"); soltar fora remove o atalho com animação de sumir (escala pra 0 + fade)
- [x] Respeita `SystemParameters.ClientAreaAnimation` (animações do Windows desligadas = trocas sem animação)
- [x] Durações de animação centralizadas em constantes num só lugar (`LauncherDragAnimations`)

> **Investigação (vizinhos "sumindo" durante o arraste):** revisão completa de
> `BeginDrag`/`UpdateDrag`/`EndDrag`/`Rebuild()` do `LauncherWidget` confirma que nenhum
> vizinho tem `Visibility`/`Opacity` alterado nem é removido da `_root.Children` durante o
> arraste — só o item arrastado vira `Opacity = 0` (mantém o lugar), e os outros só
> recebem `TranslateTransform`. Hipótese mais provável (não confirmada ao vivo — teste
> manual automatizado nessa sessão causou efeitos colaterais indesejados, então não deu
> pra reproduzir com segurança): o `GhostIconWindow`, opaco e ~1.15x maior que o ícone
> (`PickupScale`), cobre por completo o vizinho por baixo enquanto o cursor passa perto
> dele — parece "sumir" mas é só oclusão visual do fantasma, não um bug de visibilidade.
> Mitigação aplicada (não era a causa raiz, mas continua válida como ajuste visual):
> `GhostIconWindow` ganhou `Opacity = 0.85`.
>
> **Causa raiz encontrada** (confirmada pelo usuário: acontecia com qualquer ícone,
> mesmo na build com a mitigação acima): o `WidgetDragController` (Fase 8, arrasta
> widgets inteiros entre zonas) escuta `PreviewMouseLeftButtonDown/Move/Up` (túnel) no
> wrapper que envolve o `LauncherWidget` inteiro. Túnel dispara de fora pra dentro, ou
> seja, ANTES da própria interação de arrastar ícone do `LauncherWidget` (numa Border
> aninhada bem mais fundo). Como nada marcava `e.Handled`, um clique num ícone também
> era visto pelo `WidgetDragController`, que começava a arrastar o LAUNCHER INTEIRO como
> widget — e isso faz `wrapper.Opacity = 0` no wrapper que envolve TODOS os ícones,
> escondendo todo mundo junto (não só o ícone que o usuário queria mover).
> Corrigido em duas pontas: `LauncherWidget.SetupItemInteraction` marca
> `e.Handled = true` no `PreviewMouseLeftButtonDown` do ícone, e `WidgetDragController`
> passou a escutar a fase de bolha (`MouseLeftButtonDown/Move/Up`) em vez de túnel — bolha
> dispara de dentro pra fora, então o `Handled` do ícone (que roda primeiro) corretamente
> impede o wrapper do widget de agir.

# Fase 8 — Logo, mais modelos de relógio, widgets móveis/fixáveis

## 1) Logo do heimdall-brand
- [x] Copiar `ico/heimdall.ico` pra `Assets/heimdall.ico` no projeto (`Resource` no `.csproj`)
- [x] `<ApplicationIcon>Assets\heimdall.ico</ApplicationIcon>` no `.csproj`
- [x] `Icon="pack://application:,,,/Assets/heimdall.ico"` em todas as janelas (`BarWindow`, `OverlayWindow`, `SettingsWindow`, popup de lembrete, e as outras janelas de código só — via `AppIcon.Source` compartilhado)
- [x] Mesmo ícone no atalho do registro do Windows Startup — automático: `StartupService` aponta direto pro `.exe`, sem `.lnk` próprio, então usa o ícone embutido nele (o `ApplicationIcon` que acabou de mudar)
- [x] Trocar qualquer referência a "InfoBar" por "Heimdall" (títulos de janela, README, etc.) — checado: não achei nenhuma sobrando

## 2) Mais modelos de relógio
- [x] `ClockConfig` troca campos fixos por `Mode` (`TimeOnly`/`DateOnly`/`Both`/`Custom`) + `Style` (`Classic`/`Compact`/`Verbose`/`ISO`) + `Culture` + `CustomFormat`
- [x] 4 estilos com formatos próprios, aplicáveis a qualquer modo
- [x] Diferença de formato pra barra vertical vira parte de cada estilo, não um campo solto
- [x] Migração automática do config antigo (`TimeFormat`/`DateFormat`) pra `Mode: "Custom"`, preservando o formato exato já configurado
- [x] Dropdown de Modo e Estilo com preview ao vivo na tela de Configurações

## 3) Widgets móveis, fixáveis e com indicador de app aberto

### Arrastar para reordenar (generalizado pra todo widget)
- [x] Todo widget (`clock`, `media`, `reminder`, `launcher`, `separator`) arrastável e solto em qualquer posição, entre zonas inclusive
- [x] Reaproveita a mecânica de arraste com animação já feita pro launcher (fantasma seguindo o cursor, vizinhos deslizando, soltar com quique) — generalizada, não só pra ícones de atalho. Diferença necessária: como as 3 zonas ficam sobrepostas na mesma célula (não em colunas próprias), o fantasma segue o cursor livremente e a barra é dividida em 3 terços pra decidir a zona-alvo (senão uma zona vazia não teria área própria pra receber o cursor); dentro da zona, os vizinhos deslizam igual ao launcher. O fantasma é um snapshot renderizado do widget (`RenderTargetBitmap`), não uma recriação do visual, já que cada widget tem uma aparência diferente

### Fixar (pin) via botão direito
- [x] Clique direito em qualquer widget → "Fixar posição" / "Desafixar"
- [x] Widget fixado não se move ao arrastar os outros ao redor, e ele mesmo não pode ser arrastado (cursor bloqueado ao tentar)
- [x] Indicador visual: ícone de alfinete discreto no canto, visível no hover

### Persistência
- [x] Cada entrada em `Widgets.Start/Center/End` passa de `string` pra objeto `{ "Id": "clock", "Pinned": false }`
- [x] Migração automática do formato antigo (lista de strings) pro novo
- [x] Salva ordem e estado de fixado no `config.json` assim que o item for solto, sem precisar abrir a tela de Configurações

## 4) Indicador de app aberto no launcher (por cima da mecânica de arraste)
- [x] A cada poucos segundos, `EnumWindows` filtrando pelas visíveis na taskbar real (`IsWindowVisible`, sem `WS_EX_TOOLWINDOW`, sem dono via `GetWindow(GW_OWNER)`)
- [x] Pra cada uma, pega o executável dono (`GetWindowThreadProcessId` + `QueryFullProcessImageName`) e compara com o `Path` de cada `Launcher`
- [x] Guarda por launcher: se está aberto, e o HWND da primeira janela encontrada (não trata múltiplas janelas do mesmo app)
- [x] Indicador visual: traço/ponto discreto sob o ícone quando aberto — sem contador, sem distinção de foco
- [x] Clique no ícone: não aberto → `Process.Start` (atual); aberto → `ShowWindow(SW_RESTORE)` + `SetForegroundWindow`
- [x] Se `SetForegroundWindow` falhar (app elevado, Heimdall não), ignora silenciosamente — limitação conhecida, documentada no `docs/GUIA.md`
- [x] Fora de escopo: `SetWinEventHook`, `DwmRegisterThumbnail`, peek de miniaturas, jump list, fechar janela pelo menu, ícones temporários pra apps não fixados

> Isso substitui o indicador de "app aberto" simples da Fase 7 (que só comparava caminho de processo e não conseguia focar a janela) por uma versão que também enumera janelas de topo e permite focar/restaurar ao clicar.

# Fase 9 — Corrigir reload ao arrastar widgets

## Problema
Ao arrastar um widget na barra pra reordenar e soltar, o app dá um reload completo
(pisca/reconstrói a janela), porque o fluxo de soltar chama o mesmo caminho do botão
"Recarregar" (`App.Reload()`), que destrói e recria todas as `BarWindow`
(`CloseBars()` + `BuildBars()`).

## Correção
Separar persistência de reconstrução — ao soltar um widget:

- [x] Atualizar a lista em memória (`WidgetLayout.Start/Center/End`) na ordem/zona nova
- [x] Persistir no `config.json` via `ConfigService.Save()`
- [x] **Não** chamar `App.Reload()` — só reordenar os `UIElement` já existentes dentro do
      `StackPanel` da zona (mover/reinserir na posição certa), sem recriar os widgets
      (`IWidget`) nem suas `View`
- [x] Trocar de zona (ex: tirar do centro e jogar pro início) segue a mesma lógica:
      remover o `FrameworkElement` do `StackPanel` de origem e adicionar ao de destino,
      sem recriar o `IWidget` por trás — preservando seu estado interno (ex: o
      `DispatcherTimer` do relógio continua rodando, sessão do SMTC do widget de mídia
      não reconecta à toa, etc.)
- [x] `Reload()` continua existindo só pros casos que realmente exigem reconstrução:
      mudar borda/monitor/espessura pela tela de Configurações, ou o botão manual
      "Recarregar" do menu — esses não foram tocados por essa correção

> Implementado em `WidgetDragController` (`RebuildZoneChildren`): ao soltar, atualiza as
> listas, salva e reordena/move os `FrameworkElement` já existentes nas zonas afetadas,
> recriando só os separadores (linha fina entre widgets e a de fronteira entre zonas).

- [x] **Bug encontrado no teste manual:** depois de mover um widget pra outra zona, ele
      não conseguia mais ser arrastado de novo — `SlotInfo.ZoneIndex` (a zona onde o
      widget foi registrado) nunca era atualizado após uma troca de zona, então o próximo
      `BeginDrag` procurava o `Entry` na lista da zona antiga, não achava (`IndexOf < 0`)
      e desistia em silêncio. Corrigido atualizando `slot.ZoneIndex = drag.CurrentZone`
      ao concluir uma troca de zona.

# Fase 10 — Toggle do modo overlay (gaming)

## Objetivo
Tornar opcional o comportamento de overlay em tela cheia (fase 2): quando desativado, a
barra simplesmente some ao entrar em tela cheia, em vez de virar overlay transparente.

- [x] `AppConfig.GamingMode` (`bool`, padrão `true`)
- [x] `GamingMode: true` — comportamento original, sem mudança: overlay transparente,
      `WS_EX_TRANSPARENT`, AppBar removido
- [x] `GamingMode: false` — ao detectar tela cheia, só `Hide()` a `BarWindow` (sem
      overlay, sem transparência, sem click-through); ao sair, `Show()` + `AppBar.Register()`
- [x] Detecção de tela cheia em si (`FullscreenChanged`, fallback de retângulo) não foi
      tocada — só o que acontece quando o evento dispara, baseado no novo flag
- [x] Toggle na aba Geral das Configurações, com a descrição curta pedida
- [x] Atalho rápido no menu de clique direito da barra ("Modo gaming: Ativado/Desativado"),
      salva no config e recarrega na hora, sem precisar abrir Configurações

> Implementado em `BarPresenter`: `OnFullscreenChanged` agora bifurca em
> `OnFullscreenChangedGaming` (código original, intocado) e `OnFullscreenChangedPlain`
> (novo — só Hide/Show da BarWindow). `_inOverlay` virou `_fullscreenActive` pra cobrir os
> dois casos (overlay ou só escondida) no guard de "já está nesse estado, ignora".

# Fase 11 — Widgets de RAM, temperatura e FPS

## 1) Widget de RAM
- [x] `GlobalMemoryStatusEx` (Win32, mais leve que `PerformanceCounter`) via `RamStatsService`
- [x] Usado/total (ex: 11,9 / 16 GB) ou só porcentagem, configurável (`Ram.ShowUsedTotal`)
- [x] Atualiza a cada 2s
- [x] Barra vertical: só a porcentagem (sem espaço pra "usado / total")

## 2) Widget de temperatura (CPU/GPU)
- [x] `LibreHardwareMonitorLib` (NuGet) em vez de WMI `MSAcpi_ThermalZoneTemperature`
      (pouco confiável em hardware moderno) — `TemperatureService`
- [x] Checa elevação (`WindowsPrincipal.IsInRole(Administrator)`) **antes** de tentar abrir
      os sensores — sem admin, nem tenta; mostra "—" com tooltip explicando, nunca lança
      exceção nem trava
- [x] CPU e GPU separados (`Temp.ShowCpu`/`Temp.ShowGpu`); barra vertical com os dois
      ativos alterna entre eles a cada tick em vez de espremer os dois juntos
- [x] Atualiza a cada 3s

## 3) Widget de FPS (via RTSS)
- [x] `MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2")` — `RtssService`
- [x] **Validado empiricamente nesta sessão** (RTSS/MSI Afterburner estava rodando na
      máquina de dev): assinatura, versão e offsets de
      `dwProcessID`/`szName`/`dwFlags`/`dwTime0`/`dwTime1`/`dwFrames`/`dwFrameTime`
      conferidos contra processos reais sendo monitorados pelo RTSS — frametime em
      microssegundos, FPS = 1.000.000 / dwFrameTime, resultado batendo com o cálculo de
      frames/tempo decorrido do próprio RTSS
- [x] Acha a entrada do processo em primeiro plano comparando PID
      (`GetForegroundWindow` + `GetWindowThreadProcessId`)
- [x] RTSS não rodando (mapeamento não existe) → widget não aparece, sem erro visível
      (`FileNotFoundException` capturada); qualquer outra falha também só esconde, nunca
      derruba o app
- [x] Atualização throttled a 4x/s (250ms) — RTSS atualiza a cada frame, repintar nessa
      frequência só pisca à toa
- [x] `Fps.OnlyInGame: true` (padrão) — só no modo overlay; `false` — sempre

## 4) Config e registro
- [x] `"ram"`, `"temp"` e `"fps"` registrados no `WidgetFactory` (e no catálogo da aba
      Widgets das Configurações)
- [x] `AppConfig.Ram/Temp/Fps` (`RamConfig`, `TempConfig`, `FpsConfig`)
- [x] Documentado no README (tabela de widgets) e no `docs/GUIA.md` (detalhe de cada um,
      requisito de administrador da temperatura, requisito do RTSS do FPS)

> Testado ao vivo nesta sessão (não elevado): RAM mostrou dado real ("11,9 / 16 GB"),
> Temp mostrou "—" corretamente sem travar (sem admin), FPS corretamente não apareceu
> quando não havia dado do processo em primeiro plano no instante do teste — os três
> comportamentos esperados confirmados. Não testado: leitura de sensores de temperatura
> de verdade rodando como administrador (precisa do usuário elevar o Heimdall).

# Fase 12 — Diagnóstico e redução de consumo de RAM

## Diagnóstico (antes de otimizar às cegas)
Medido com `dotnet-counters`/`Get-Process -Module` (sem Visual Studio no ambiente),
isolando config (1 monitor, sem overlay) e comparando antes/depois:

- **Achado #1 (confirmado, maior impacto):** `App.OnStartup` chamava
  `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`
  incondicionalmente, mesmo sem o widget `media` configurado e sem nunca apertar um
  atalho de mídia. Isso carrega `Microsoft.Windows.SDK.NET.dll` (23,74 MB sozinha) +
  `windows.storage.dll` + `OneCoreUAPCommonProxyStub.dll` + `Windows.Media.MediaControl.dll`
  — **~30 MB confirmados via `Get-Process -Module`** antes/depois da correção, só de
  projeção WinRT que ninguém pediu.
- **Achado #2 (confirmado, leak real):** `MediaWidget` inscrevia
  `_manager.CurrentSessionChanged` mas nunca desinscrevia no `Dispose()` — como o
  `GlobalSystemMediaTransportControlsSessionManager` é gerenciado pelo SO e sobrevive
  além do widget, isso mantinha o `MediaWidget` inteiro (árvore visual, timers já
  parados mas ainda referenciados) vivo pra sempre a cada `Reload()` com o widget de
  mídia configurado — um "zumbi" que se acumula a cada troca de tema, cada
  `AddLauncher`, cada save nas Configurações.
- **Descartado (não é o problema):** cache de ícones (`IconCacheService`) já lê do PNG
  em disco corretamente antes de reextrair, e libera o `HBITMAP` nativo via
  `DeleteObject` num `finally`. `LibreHardwareMonitorLib` só é aberta se o processo
  estiver elevado (`TemperatureService.IsElevated()` checado **antes** de `new
  Computer()`) — sem admin, o custo é só a DLL em si (0,7 MB), não o driver de
  sensores. O arrastar/mover widget (Fase 9) não cria `IWidget` novo — confirmado via
  grep que `WidgetFactory.Create` só é chamado em `WidgetZoneBuilder`, nunca em
  `WidgetDragController`.
- **Fora do controle do Heimdall:** `AMDXN64.DLL` (38,89 MB, driver de GPU AMD, puxado
  pelo D3D do WPF — aconteceria com qualquer app WPF nesta máquina) e `RTSSHooks64.dll`
  (injetado pelo próprio RTSS em processos que usam D3D, não é o Heimdall carregando).
- **Nota de metodologia:** conectar `dotnet-counters`/profiler no processo por si só
  infla o working set em ~30 MB (sessão de diagnóstico/EventPipe fica retida) — medir
  primeiro com Task Manager/`tasklist` antes de anexar qualquer profiler, senão o
  número reportado já vem inflado pela própria ferramenta de medição.

## Correções aplicadas
- [x] `App.xaml.cs`: `RequestAsync()` do SMTC virou lazy (`GetMediaManagerAsync`),
      inicializada só no primeiro atalho de mídia de fato usado
- [x] `MediaWidget.Dispose()`: desinscreve `_manager.CurrentSessionChanged` (handler
      nomeado `OnCurrentSessionChanged`, antes era uma lambda anônima impossível de
      desinscrever)

## Resultado medido (tasklist limpo, sem profiler anexado)
- Baseline (`clock` só, 1 monitor): **~164 MB → ~151 MB**
- Config completa (media+launcher+reminder+ram+temp+fps, sem elevação): **~198–207 MB**
  — o `media` widget legitimamente carrega o SMTC de novo (~30 MB), então o alvo
  original de 40–70 MB citado no plano era calibrado sem contar o custo inerente de
  WPF com aceleração de hardware nesta máquina (GPU AMD: ~39 MB só de driver, mais
  ~70–90 MB de infraestrutura básica .NET/WPF) — não é algo que dê pra cortar sem
  desligar aceleração de hardware (troca ruim pra uma barra que precisa ser fluida).

## Não verificado
- Consumo de `LibreHardwareMonitorLib` **rodando elevado de verdade** (com o driver de
  sensores aberto) — precisa do usuário elevar o Heimdall pra medir; sem elevação
  (caso mais comum) o custo já está confirmado como desprezível.
