# Changelog

Todas as mudanças relevantes do Heimdall ficam registradas aqui. O formato segue o
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa
[Versionamento Semântico](https://semver.org/lang/pt-BR/).

A seção de cada versão vira automaticamente o "O que mudou" da release no GitHub —
mudanças ainda não lançadas entram em **[Não lançado]**.

## [Não lançado]

### Adicionado

- Visual novo da barra, inspirado na menu bar do macOS: altura padrão de 38px, botões
  de ícone 30x30, player de mídia em formato de pílula com capa, título e artista, e
  relógio com a data discreta e a hora em fonte monoespaçada.
- Quatro temas novos: **Obsidiana** (escuro translúcido com desfoque, o padrão),
  **Cristal** (claro translúcido), **Bifröst** (sólido, com um fio na cor de destaque
  no topo) e **Compacta** (26px, só com marca, mídia, lembretes e hora).
- Cor de destaque configurável no config (`AccentColor`), inclusive acompanhando a cor
  de destaque do Windows (`"system"`), e opacidade da barra (`BarOpacity`).
- Widgets novos: `brand` (a espada do Heimdall, abre as Configurações), `cpu` (uso de
  CPU com mini gráfico), `notifications` (sino com o número de lembretes pendentes hoje)
  e `network` (Wi-Fi, cabo ou sem conexão).
- Atalho global **Ctrl+Alt+H** para ocultar/mostrar a barra (o Ctrl+Shift+B continua
  funcionando).
- Fontes Manrope e JetBrains Mono embutidas no app.
- Dropdowns na barra, abertos ao clicar e fechados com Esc ou clicando fora:
  - **Mídia** (clique na capa ou no título): capa grande, barra de progresso arrastável
    para pular na faixa, controles e volume do app.
  - **Lembretes de hoje** (clique no sino): lista com checkbox para concluir, horário de
    cada um e botão "Novo lembrete".
    - Os atrasados de dias anteriores ficam numa seção própria, e a hora que já passou
      aparece em destaque.
    - Cada lembrete mostra a cor da lista, a repetição, a bandeira e a prioridade (`!`,
      `!!` ou `!!!` antes do título, de Baixa a Alta).
    - Clique direito num lembrete: Editar, Adiar (só nos vencidos), Sinalizar e Excluir.
    - Concluir um lembrete que se repete pula para a próxima ocorrência.
    - "Ver todos" abre a janela de lembretes.
  - **Sistema** (clique no chip de CPU): uso de CPU, memória e disco, tempo ligado,
    liga/desliga do modo overlay, ocultar barra e Configurações.
- Janela de lembretes (420px, redimensionável), aberta pelo "Ver todos" do sino, pelo
  menu do sino ou pela bandeja:
  - Listas inteligentes Hoje, Agendados, Sinalizados e Todos, com a contagem de cada.
  - A lista ativa aparece com o título na cor dela, e "Minhas listas" fica no rodapé.
  - Navegação pelo teclado: Tab entre as áreas, setas na lista e Espaço para concluir
    o lembrete selecionado.
  - Clique direito num lembrete: Editar, Adiar (vencidos), Sinalizar e Excluir.
  - Cabeçalhos de seção: Hoje separa os atrasados, Agendados agrupa por dia e Todos
    agrupa por lista.
  - Criar e editar acontecem na própria lista. O "+" do cabeçalho põe uma linha em
    edição no topo; clicar no título de um lembrete edita no lugar. Embaixo do título
    ficam chips de data, bandeira, nota e lista.
    - Enter salva e já abre outra linha; Esc cancela; clicar fora salva (ou descarta,
      se o título estiver vazio).
    - Apagar o título de um lembrete existente exclui o lembrete.
    - O "…" (e os chips de data e de lista) abre o painel de detalhes, com Data
      (calendário próprio com Hoje, Amanhã, Próxima segunda e +1 semana), Hora (com ▲▼
      de 15 em 15 min), Repetir, Lista, Prioridade (Nenhuma, Baixa, Média, Alta), Notas
      e Sinalizar. As opções abrem dentro do próprio painel; Esc volta uma etapa.
      "Cancelar" desfaz o que foi mexido no painel e "Adicionar" salva.
    - Repetir: Nunca, Diariamente, Semanalmente, Dias úteis, Mensalmente ou
      Personalizado. O Personalizado tem "a cada N" dias/semanas/meses/anos, os dias da
      semana em círculos e "Terminar" (nunca, numa data ou após N vezes), com um resumo
      em texto que se atualiza a cada mudança ("Toda semana, seg · qua · sex, às 17:00").
    - O lembrete novo herda a lista onde foi criado: em Hoje nasce com a data de hoje,
      em Sinalizados vem sinalizado e numa lista sua vai para ela.
  - Data no próprio texto, em português: "amanhã 14h", "sexta 19:00", "dia 20 às 10h",
    "todo dia 9h", "toda segunda e quarta 8h", "todo dia 10" (mensal). O chip de data
    mostra o que foi entendido enquanto você digita, e esse trecho sai do título ao salvar.
- Aviso próprio do Heimdall na hora do lembrete, no canto oposto à barra, com "Adiar
  10 min" e "Concluir". Clicar nele abre a janela de lembretes.
  - Nunca rouba o foco e aparece por cima de jogo em tela cheia sem bordas.
  - Some sozinho em 12 segundos, exceto lembretes de prioridade alta; para de contar com
    o mouse em cima.
  - Lembretes que venceram com o app fechado chegam juntos num aviso só ("3 lembretes
    atrasados").
  - Com o Assistente de Foco (Não incomodar) ligado, os avisos esperam e aparecem
    juntos quando ele desliga. "Pausar lembretes" da bandeja continua valendo.
  - O som continua opcional e desligado por padrão.
- Quando não sobra nada pendente, a janela de lembretes e o sino mostram "Tudo em ordem"
  com o próximo lembrete agendado.
- Opção "Mostrar concluídos": os concluídos aparecem riscados no fim de cada lista da
  janela de lembretes, e dá para desmarcar.
- Seção Lembretes das Configurações: hora padrão dos lembretes sem hora (09:00),
  minutos do "Adiar" (10), som, mostrar concluídos e as suas listas (criar, renomear,
  trocar a cor e excluir; os lembretes de uma lista excluída vão para a padrão).
- Tela de Configurações nova: janela redimensionável com barra de título própria,
  menu lateral (Geral, Aparência, Relógio, Widgets, Lembretes, Atalhos, Sobre) e rodapé
  com **Restaurar padrões**, **Cancelar** e **Aplicar**.
- Aba **Aparência** com prévia ao vivo na barra: posição (topo, baixo, esquerda,
  direita), tema, opacidade, altura, cor de acento (4 cores ou a cor de destaque do
  Windows) e as opções abaixo. Cancelar desfaz tudo; Aplicar salva.
- **Reservar espaço na área de trabalho** (`ReserveSpace`): desligado, janelas
  maximizadas podem ficar por baixo da barra.
- **Ocultar automaticamente** (`AutoHide`): a barra some e volta ao encostar o mouse na
  borda da tela.
- Aba **Atalhos** com a lista dos atalhos globais.
- Menus de clique direito novos, no visual do tema (ícones, atalhos à direita, submenus):
  - **Área vazia da barra**: Configurações, Posição da barra ▸ (borda e monitor, com o
    atual marcado), Widgets ▸ (adicionar widget, atalhos, histórico, config.json), Modo
    overlay, Ocultar barra, Recarregar widgets e Sair.
  - **Widget**: ações do próprio widget (abrir player, calendário, lembretes de hoje...),
    Mover ▸ entre as zonas, Fixar, Configurar widget e Ocultar widget.
- Ícone na bandeja do Windows: Mostrar barra, Modo overlay, **Pausar lembretes**,
  Configurações, Verificar atualizações (com a versão atual) e Sair.
- Esc fecha os menus de clique direito.

### Alterado

- Os 9 temas antigos foram substituídos pelos 4 novos. Configs existentes são migrados
  sozinhos para o tema mais próximo (Escuro → Bifröst, Claro → Cristal, translúcidos →
  Obsidiana, "Destaque do Windows" → Obsidiana com o destaque do Windows); o config
  original fica salvo em `config.json.bak-v1`. Temas próprios em JSON continuam valendo.
- Quem usava a espessura padrão antiga (28) passa para a nova (38); espessuras
  personalizadas são mantidas.
- Os widgets não têm mais linhas separadoras automáticas entre si — use o widget
  `separator` onde quiser uma divisão.
- O volume do app saiu do player da barra e foi para o dropdown de mídia (a roda do
  mouse sobre o player continua ajustando o volume).
- As opções de cor de fundo, cor do texto, fonte e tamanho da fonte saíram da tela de
  Configurações; continuam funcionando pelo `config.json` (`Style`).
- A troca de tema e a lista de monitores saíram do menu de clique direito: tema fica em
  Configurações → Aparência; monitores em Posição da barra ▸.
- Lembretes num formato novo, com data, hora opcional, repetição (diária, semanal,
  mensal ou anual) e lista. Os lembretes existentes são migrados sozinhos; o config
  original fica salvo em `config.json.bak-v2`.
  - Lembrete com hora que passou sem ser concluído continua na lista de hoje como
    atrasado, em vez de sumir como feito.
  - Concluir um lembrete que se repete pula para a próxima ocorrência. Se ninguém
    concluir, ele avisa de novo na ocorrência seguinte.
  - Lembrete com data e sem hora avisa às 09:00.
  - O som do aviso virou uma opção geral, não mais por lembrete.
  - Lembrete sem data concluído fica marcado como concluído em vez de ser apagado do
    config (continua indo para o histórico).
- "Novo lembrete" (no sino, no "+" do widget de lembretes e no Ctrl+Shift+R) e "Editar"
  abrem a janela de lembretes com a linha já em edição, em vez do popup de adição rápida.
- Na hora do lembrete aparece o aviso novo, no lugar do chip piscando no widget de
  lembretes. O widget continua mostrando os lembretes sem data.
- A tabela de lembretes das Configurações saiu: criar e editar lembretes agora é na
  janela de lembretes, e a seção Lembretes ficou só com as preferências e as listas.

### Corrigido

- Clicar em Aplicar nas Configurações desfazia o que tinha mudado nos lembretes com a
  janela aberta (um lembrete concluído ou criado nesse meio-tempo voltava atrás).
- Lembretes com hora só avisavam se o widget `reminder` estivesse na barra (e ele não
  vem no layout padrão), e o aviso podia atrasar até 1 minuto. Agora o aviso sai na
  hora marcada, com ou sem o widget.
- "Novo lembrete" (no sino ou no Ctrl+Shift+R) fechava o app quando o widget de
  lembretes estava na barra: o popup era ancorado na cópia escondida do overlay de tela
  cheia. Essa cópia também não "engole" mais os avisos que deviam aparecer na barra.
- Salvar a tela de Configurações apagava as datas dos lembretes (criação, dia
  específico, início/fim de recorrência).

## [1.3.0] - 2026-10-12

### Adicionado

- Clique triplo numa área vazia da barra + arrastar leva a barra para outro monitor (salva
  como monitor específico no config). Esc cancela.
- Widget `mic`: volume do microfone padrão do Windows na barra (só o ícone; porcentagem
  no tooltip) — roda do mouse ajusta em passos de 5%, clique abre um slider com botão de
  mudo.
- Widget `volume`: volume geral do PC (saída padrão do Windows) na barra, com o mesmo
  controle do `mic` — roda do mouse, slider e botão de mudo.
- Grupos do launcher como pasta: com a barra horizontal num monitor em pé (vertical),
  cada grupo vira um ícone só, com miniaturas dos atalhos, que abre a lista deles ao
  clicar. Clique direito no grupo → "Exibir como pasta" (Automático / Sempre / Nunca).
- Janelas abertas (widget `windows`) como pasta no monitor em pé: um ícone só, com
  miniaturas dos apps e o total de janelas, que abre a lista completa ao clicar.
- Player de música mínimo no monitor em pé: só capa, play/pause e próxima faixa — o
  título fica no tooltip e o volume continua na roda do mouse.

### Corrigido

- Menu de clique direito da barra agora fecha ao clicar em qualquer outro lugar da tela
  (antes ficava aberto até escolher um item ou clicar de novo na barra).

## [1.2.0] - 2026-09-30

### Adicionado

- Clique no widget de relógio abre um calendário do mês, com as cores do tema da barra.
- Grupos de atalhos no launcher (ex.: Apps, Jogos), separados visualmente por um
  separador: clique direito num ícone → "Mover para grupo". Um grupo inteiro pode ser
  arrastado pelo separador para trocar de lugar.
- Clique direito em qualquer widget da barra → "Remover widget".
- Aviso de versão nova: ao abrir, o Heimdall consulta a última release no GitHub e, se
  houver versão mais nova, mostra uma notificação e um ícone de download na barra que abre
  a página da versão (o que mudou + arquivos). Desligável com `"CheckForUpdates": false`
  no config.

### Corrigido

- Arrastar um atalho do launcher para reordenar não abre mais o app ao soltar.
- O Heimdall se relança sozinho depois de um reset do driver de vídeo, em vez de fechar.
- Crash ao entrar em tela cheia quando a troca de resolução acontecia durante o fade
  entre barra e overlay.

## [1.1.0] - 2026-09-28

### Adicionado

- Widget `windows`: uma taskbar leve com as janelas abertas no momento.
- Badge com o total de downloads no README.

## [1.0.0] - 2026-09-27

Primeira versão pública.

### Adicionado

- Barra fixa numa borda da tela (ou flutuante, com cantos arredondados), com três zonas
  de widgets arrastáveis e fixáveis.
- Widgets de relógio (modelos prontos ou formato personalizado), mídia (capa, progresso,
  volume por app e atalhos globais), lembretes (rápidos, agendados e recorrentes, com
  histórico), atalhos de apps, separador, RAM, temperatura de CPU/GPU e FPS (via RTSS).
- Modo overlay automático em jogos e apps de tela cheia, com toggle de modo gaming.
- 9 temas prontos e suporte a temas próprios em JSON.
- Tela de Configurações com navegação lateral e pré-visualização.
- Interface em português e inglês.

[Não lançado]: https://github.com/yansntss/Heimdall/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/yansntss/Heimdall/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/yansntss/Heimdall/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/yansntss/Heimdall/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/yansntss/Heimdall/releases/tag/v1.0.0
