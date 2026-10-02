# Changelog

Todas as mudanças relevantes do Heimdall ficam registradas aqui. O formato segue o
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto usa
[Versionamento Semântico](https://semver.org/lang/pt-BR/).

A seção de cada versão vira automaticamente o "O que mudou" da release no GitHub —
mudanças ainda não lançadas entram em **[Não lançado]**.

## [Não lançado]

### Adicionado

- Clique triplo numa área vazia da barra + arrastar leva a barra para outro monitor (salva
  como monitor específico no config). Esc cancela.

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

[Não lançado]: https://github.com/yansntss/Heimdall/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/yansntss/Heimdall/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/yansntss/Heimdall/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/yansntss/Heimdall/releases/tag/v1.0.0
