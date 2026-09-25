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
- [ ] Item "Abrir histórico de lembretes" no menu de clique direito da barra
- [ ] Botão "+" no hover do widget de lembretes + atalho global `Ctrl+Shift+R`
- [ ] Popup de adição rápida ancorado à barra (posição conforme a borda), com foco (não `NOACTIVATE`)
- [ ] Campo de texto com foco automático, Enter salva, Esc fecha, fecha ao perder foco
- [ ] Chips de tempo rápido: Sem horário / +15 min / +1 h / Hoje 18h / Amanhã 9h / Personalizado
- [ ] "Personalizado" expande seletores de data/hora
- [ ] "Mais opções" recolhido por padrão: recorrência (Única/Diária/Dias da semana) + toggle de som
- [ ] Salvar direto no `config.json` e atualizar a barra sem recarregar tudo
- [ ] Ao disparar, lembrete pulsa com a cor de destaque do tema por alguns segundos
- [ ] Badge com contador de lembretes pendentes quando houver mais de um

## Etapa 4 — Detalhes visuais
- [ ] Modo flutuante opcional: margem das bordas da tela + cantos arredondados, espaço do AppBar incluindo a margem
- [ ] Separadores sutis entre widgets e entre zonas (início/centro/fim)
- [ ] Hover com fundo levemente destacado e transição de 150 ms
- [ ] Ícone animado de equalizador ao lado da faixa quando estiver tocando
- [ ] Tooltips em todos os botões
- [ ] Contorno leve no texto do overlay pra legibilidade sobre qualquer fundo (texto duplicado deslocado, não `DropShadowEffect`)
- [ ] Animação de fade (200 ms) na troca entre modo barra e overlay

## Outros
- [ ] Atualizar `README.md` — ainda descreve a Aparência antiga (3 presets de cor) em vez do sistema de 9 temas + dropdown + temas de usuário já implementado
