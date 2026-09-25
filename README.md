# InfoBar — Fase 1 (MVP)

Barra fixa numa borda da tela (AppBar), com relógio e escolha de monitor.

## Rodar
Requer Windows 10/11 + .NET 10 SDK.

```
dotnet run
```

### Publicar
Duas opções, dependendo de onde o `.exe` vai rodar:

- **Leve** (depende do .NET 10 Desktop Runtime instalado na máquina):
  ```
  dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
  ```
- **Self-contained** (maior, mas roda em qualquer PC Windows sem instalar nada):
  ```
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
  ```

## Uso
Clique direito na barra: **Configurações...** (tela de config completa — veja abaixo), editar `config.json` na mão, recarregar, ver monitores, sair.

Config: `%AppData%\InfoBar\config.json` (criado na 1ª execução, aceita comentários `//`).

```jsonc
{
  "Edge": "Top",              // Top | Bottom | Left | Right
  "Thickness": 28,            // DIPs. Laterais: ~72
  "MonitorMode": "Primary",   // Primary | Specific | All
  "MonitorDevice": "DISPLAY2",// usado em Specific (ver "Monitores detectados")
  "Style": {
    "Background": "#E61E1E1E",// #AARRGGBB
    "Foreground": "#FFFFFFFF",
    "FontFamily": "Segoe UI",
    "FontSize": 13
  },
  "Widgets": { "Start": [], "Center": ["clock", "media", "reminder"], "End": [] },
  "Clock": {
    "TimeFormat": "HH:mm:ss",
    "DateFormat": "ddd, dd/MM/yyyy",
    "VerticalDateFormat": "dd/MM",
    "Culture": "pt-BR"
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
    },
    {
      "Kind": "Scheduled",
      "Text": "Pagar boleto",
      "Time": "18:00",
      "Recurrence": "Once",
      "PlaySound": true
    }
  ]
}
```

## Configurações
Clique direito na barra → **Configurações...** abre uma janela com 4 abas — não precisa mais editar o `config.json` na mão pro dia a dia:

- **Geral**: borda, espessura, modo/escolha de monitor, "Iniciar com o Windows".
- **Aparência**: cor de fundo/texto (com seletor de cor nativo), fonte, tamanho, e 3 temas prontos (Escuro, Claro, Acrílico) que só preenchem os campos — dá pra ajustar manualmente depois.
- **Widgets**: reordena/adiciona/remove os widgets de cada zona (início/centro/fim).
- **Lembretes**: grid pra adicionar/editar/remover os itens de `Reminders`.

"Salvar e recarregar" grava o `config.json` e aplica na hora, sem reiniciar o app. Quem preferir editar o JSON direto ainda pode, pelo item "Editar config.json" do mesmo menu.

### Iniciar com o Windows
O checkbox na aba Geral grava/remove uma entrada em `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, apontando pro executável atual — sem instalador nem tarefa agendada. Importante: ele aponta pro `.exe` que está rodando *no momento em que você salva*. Em `dotnet run`/debug isso é o apphost de debug; assim que você rodar a partir do `.exe` publicado (veja "Publicar" abaixo), é só reativar o checkbox uma vez pra apontar pro caminho certo. Se mover ou republicar o app pra outra pasta, reative de novo.

## Widgets
- `clock` — relógio/data (ver `Clock` acima).
- `media` — controle de mídia via SMTC (Spotify, YouTube, qualquer player), sem login. Mostra "artista — título" da faixa atual. Clique esquerdo: play/pause. Clique do meio: próxima faixa. Fica oculto quando nada está tocando.
- `reminder` — lembretes fixos e agendados, definidos em `Reminders`:
  - `Kind`: `"Fixed"` (texto permanente, sempre visível) ou `"Scheduled"` (dispara em um horário).
  - `Time`: horário `"HH:mm"`, usado apenas em `Scheduled`.
  - `Recurrence`: `"Once"` (dispara uma vez e se autodesativa, marcando `Completed: true` de volta no `config.json`), `"Daily"` ou `"Weekly"` (usa `Days`, ex.: `["Monday", "Friday"]`).
  - `PlaySound`: toca um som do sistema ao disparar.
  - Ao disparar, o widget destaca o texto do lembrete por alguns segundos e depois volta a mostrar os lembretes fixos.

## Estrutura
- `Native/` — P/Invoke, `AppBarManager` (reserva de espaço, DPI, restart do Explorer, modo overlay em tela cheia) e `HotkeyManager` (hotkey global)
- `Services/` — enumeração de monitores e `StartupService` (iniciar com o Windows)
- `Config/` — modelo e leitura/gravação do JSON
- `Widgets/` — `IWidget`, `WidgetFactory`, `ClockWidget`, `MediaWidget`, `ReminderWidget`
- `UI/` — `BarWindow` (3 zonas: início / centro / fim) e `SettingsWindow` (tela de configurações)
