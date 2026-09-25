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
  "Widgets": { "Start": [], "Center": ["clock"], "End": [] },
  "Clock": {
    "TimeFormat": "HH:mm:ss",
    "DateFormat": "ddd, dd/MM/yyyy",
    "VerticalDateFormat": "dd/MM",
    "Culture": "pt-BR"
  }
}
```

## Estrutura
- `Native/` — P/Invoke e `AppBarManager` (reserva de espaço, DPI, restart do Explorer)
- `Services/` — enumeração de monitores
- `Config/` — modelo e leitura/gravação do JSON
- `Widgets/` — `IWidget`, `WidgetFactory`, `ClockWidget`
- `UI/` — `BarWindow` (3 zonas: início / centro / fim)
