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
