<p align="center">
  <img src="docs/assets/banner.webp" alt="Heimdall" width="720">
</p>

<h1 align="center">Heimdall</h1>

<p align="center">Uma barra de sistema para Windows que faz o que a barra de tarefas deveria fazer.</p>

<p align="center">
  <a href="https://github.com/yansntss/Heimdall/releases/latest">
    <img alt="Última versão" src="https://img.shields.io/github/v/release/yansntss/Heimdall">
  </a>
  <a href="https://github.com/yansntss/Heimdall/releases">
    <img alt="Downloads" src="https://img.shields.io/github/downloads/yansntss/Heimdall/total">
  </a>
  <img alt="Plataforma" src="https://img.shields.io/badge/plataforma-Windows%2010%20%2F%2011-0078D6">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="Licença" src="https://img.shields.io/badge/licença-MIT-green">
</p>

---

Fixa numa borda da tela (ou flutuando, com cantos arredondados), reunindo relógio,
controle de mídia, lembretes e atalhos dos seus apps num só lugar, com o visual que você
escolher. Ela se recolhe automaticamente para um overlay discreto quando um jogo em tela
cheia é aberto, e volta ao normal quando ele fecha.

## Por que usar

- **Relógio sempre visível**, no formato e fuso que você quiser.
- **Controle de mídia** para Spotify, YouTube ou qualquer player — play/pause, próxima
  faixa, capa do álbum, progresso e volume por app, sem precisar abrir a janela.
- **Lembretes** rápidos ou agendados, com recorrência, aviso visual na barra e histórico.
- **Atalhos de apps** direto na barra — arraste um arquivo, uma pasta ou escolha entre os
  apps instalados.
- **9 temas prontos** (Escuro, Claro, Acrílico, Mica, Vidro, Destaque do Windows, Auto...)
  com suporte a temas próprios em JSON.
- **Modo overlay automático**: em jogos e apps de tela cheia, a barra normal some e um
  overlay discreto e somente informativo assume o lugar, sem interceptar clique.
- **Leve e nativo**: WPF puro, sem Electron e sem serviço em segundo plano consumindo RAM.

## Screenshots

<!-- TODO: substituir pelos screenshots reais em docs/assets/ antes de publicar -->
<p align="center">
  <img src="docs/assets/screenshot-normal.png" alt="Heimdall em modo normal" width="720"><br>
  <sub>Modo normal</sub>
</p>
<p align="center">
  <img src="docs/assets/screenshot-overlay.png" alt="Heimdall em modo overlay durante um jogo" width="720"><br>
  <sub>Modo overlay em jogo</sub>
</p>

## Baixar e instalar

Não é necessário compilar nada — baixe o executável já pronto:

**[Baixar a última versão](https://github.com/yansntss/Heimdall/releases/latest)**

O que mudou em cada versão: [CHANGELOG.md](CHANGELOG.md).

Cada release traz dois arquivos — escolha um:

| Arquivo | Quando usar |
|---|---|
| `Heimdall-vX.Y.Z-win-x64.zip` | Leve (poucos MB). Requer o [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado. |
| `Heimdall-vX.Y.Z-win-x64-standalone.zip` | Maior (~150 MB), roda em qualquer PC Windows sem instalar nada. |

1. Baixe um dos `.zip` acima na [página de releases](https://github.com/yansntss/Heimdall/releases/latest).
2. Extraia numa pasta de sua preferência.
3. Execute o `Heimdall.exe`.
4. Opcional: clique direito na barra → **Configurações...** → aba **Geral** → marque
   **"Iniciar com o Windows"** para que ela abra automaticamente ao ligar o PC.

> Requer Windows 10 ou 11.
>
> Ainda não há releases publicados? Veja [Rodar localmente](#rodar-localmente) para
> compilar a partir do código-fonte.

### Verificação de integridade

O executável **não é assinado digitalmente** — o Windows pode exibir o alerta "O Windows
protegeu o computador" na primeira execução. Clique em **Mais informações** → **Executar
assim mesmo**.

Para confirmar que o arquivo baixado não foi adulterado, compare o hash SHA256 com os
publicados em `SHA256SUMS.txt`, anexado a cada release:

```powershell
Get-FileHash Heimdall-vX.Y.Z-win-x64.zip -Algorithm SHA256
```

## Rodar localmente

Para quem quer testar em modo desenvolvimento ou contribuir com código.

**Pré-requisitos:**
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

**Passos:**

```bash
git clone https://github.com/yansntss/Heimdall.git
cd Heimdall
dotnet run
```

A barra deve aparecer na borda da tela.

**Gerando seu próprio build:**

```bash
# Leve (requer o .NET 10 Desktop Runtime instalado na máquina que for rodar)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Self-contained (maior, mas roda em qualquer PC Windows sem instalar nada)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

> Não use `-p:PublishTrimmed=true`: WPF e as bibliotecas de sensores usam reflexão, e o
> trimming quebra o app.

O executável final fica em `bin/Release/net10.0-windows.../win-x64/publish/`.

## Configuração

Tudo pode ser ajustado pela interface: clique direito na barra → **Configurações...**
abre uma janela com abas de borda/tamanho, aparência/temas, widgets e lembretes.
"Salvar e recarregar" aplica as mudanças na hora, sem reiniciar o app.

Quem preferir também pode editar o JSON diretamente em
`%AppData%\Heimdall\config.json`:

```jsonc
{
  "Edge": "Top",               // Top | Bottom | Left | Right
  "Thickness": 28,              // DIPs
  "FloatingMode": false,        // true = barra flutuante (margem + cantos arredondados)
  "MonitorMode": "Primary",     // Primary | Specific | All
  "Theme": "Escuro",            // tema embutido ou salvo em .../themes/*.json
  "Widgets": { "Start": [], "Center": [{ "Id": "clock" }], "End": [{ "Id": "media" }, { "Id": "reminder" }, { "Id": "launcher" }] }
}
```

Detalhes completos de cada opção, dos widgets, temas customizados e da estrutura interna
do projeto estão em [`docs/GUIA.md`](docs/GUIA.md).

## Widgets disponíveis

| Widget | O que faz |
|---|---|
| `clock` | Relógio e data, formato configurável; clique abre um calendário do mês |
| `media` | Controle de mídia (SMTC), volume por app, capa do álbum |
| `reminder` | Lembretes fixos e agendados, com histórico |
| `launcher` | Atalhos de apps, pastas, arquivos e URLs, organizáveis em grupos |
| `windows` | Janelas abertas no momento, agrupadas por app — uma taskbar leve |
| `ram` | Uso de memória — funciona sempre, sem dependências |
| `temp` | Temperatura de CPU/GPU — **precisa do Heimdall rodando como administrador** |
| `fps` | FPS via RTSS/MSI Afterburner — **precisa do RTSS rodando**; sem ele, o widget simplesmente não aparece |

## Limitações conhecidas

- Widget de temperatura precisa do Heimdall rodando como administrador.
- Widget de FPS precisa do RTSS/MSI Afterburner rodando.
- O overlay funciona apenas em tela cheia em janela (borderless), não em tela cheia
  exclusiva.

## Stack

- **.NET 10** + **WPF** — interface nativa, sem Electron
- **P/Invoke** direto no Windows Shell/DWM para reserva de espaço na tela (AppBar),
  detecção de tela cheia, extração de ícones e backdrops (Acrílico/Mica)
- **SMTC** (System Media Transport Controls) para controle de mídia
- **NAudio.Wasapi** para volume por aplicativo
- **LibreHardwareMonitorLib** para sensores de temperatura de CPU/GPU (widget `temp`,
  precisa de administrador)

## Contribuindo

Encontrou um bug ou quer sugerir uma ideia? Abra uma
[issue](https://github.com/yansntss/Heimdall/issues) ou um pull request — toda
contribuição é bem-vinda.

## Apoie o projeto

O Heimdall é gratuito e desenvolvido nas horas livres. Se ele foi útil para você e
quiser retribuir, um Pix é muito bem-vindo para manter o projeto vivo:

**Chave Pix:** `c85cc18b-19b3-4d94-82ad-c68fa3dc8721`


Nenhuma doação é necessária para usar, sugerir ou contribuir com o projeto — é apenas uma
forma de agradecer, caso queira.

## Licença

Distribuído sob a licença MIT — veja [LICENSE](LICENSE) para mais detalhes.
