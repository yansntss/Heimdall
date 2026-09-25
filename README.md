# InfoBar — Fase 1 (MVP)

Barra fixa numa borda da tela (AppBar), com relógio e escolha de monitor.

## Rodar
Requer Windows 10/11 + .NET 10 SDK.

```
dotnet run
```

Publicar (exe único, depende do .NET 10 Desktop Runtime instalado):
```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Uso
Clique direito na barra: abrir config, recarregar, ver monitores, sair.

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
- `Services/` — enumeração de monitores
- `Config/` — modelo e leitura/gravação do JSON
- `Widgets/` — `IWidget`, `WidgetFactory`, `ClockWidget`, `MediaWidget`, `ReminderWidget`
- `UI/` — `BarWindow` (3 zonas: início / centro / fim)
