# Heimdall

**Uma barra de sistema para Windows que faz o que a barra de tarefas deveria fazer.**

Fixa numa borda da tela (ou flutuando, com cantos arredondados) — relógio, controle de
mídia, lembretes e atalhos dos seus apps favoritos, tudo num só lugar, com o visual que
você escolher. E some sozinha, discretamente, assim que você abre um jogo em tela cheia.

![Plataforma](https://img.shields.io/badge/plataforma-Windows%2010%20%2F%2011-0078D6)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Licença](https://img.shields.io/badge/licença-MIT-green)

---

## ✨ Por que usar

- **Relógio sempre visível**, no formato e fuso que você quiser.
- **Controle de mídia** pra Spotify, YouTube ou qualquer player — play/pause, próxima
  faixa, capa do álbum, progresso e volume por app, sem precisar abrir a janela.
- **Lembretes** rápidos ou agendados (com recorrência), que avisam pulsando na barra e
  ficam registrados num histórico.
- **Atalhos dos seus apps** direto na barra — arraste um arquivo, uma pasta ou escolha
  entre os apps instalados.
- **9 temas prontos** (Escuro, Claro, Acrílico, Mica, Vidro, Destaque do Windows, Auto...)
  e suporte a temas próprios em JSON.
- **Modo overlay automático**: em jogos e apps de tela cheia, a barra normal some e um
  overlay discreto e informativo assume o lugar — sem atrapalhar, sem interceptar clique.
- **Leve**: não é Electron, não é serviço em segundo plano consumindo RAM — é WPF nativo.

---

## 📥 Baixar e instalar

Não precisa compilar nada. Baixe o executável já pronto na página de releases:

### [⬇️ Baixar a última versão](https://github.com/yansntss/Heimdall/releases/latest)

**Como instalar:**

1. Baixe o `.zip` (ou `.exe`) da versão mais recente no link acima.
2. Extraia (se for `.zip`) numa pasta de sua preferência.
3. Rode o `Heimdall.exe`.
4. (Opcional) Clique direito na barra → **Configurações...** → aba **Geral** → marque
   **"Iniciar com o Windows"** pra ela abrir sozinha sempre que você ligar o PC.

> **Requisito:** Windows 10 ou 11. Se baixar a versão "leve" (menor), é necessário ter o
> [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.
> A versão "self-contained" já vem com tudo embutido e roda sem instalar nada extra.
>
> Ainda não há releases publicados? Veja a seção [Rodar localmente](#-rodar-localmente)
> abaixo pra compilar você mesmo enquanto isso.

---

## 🚀 Rodar localmente

Pra quem quer testar em modo desenvolvimento ou contribuir com código.

**Pré-requisitos:**
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

**Passos:**

```bash
git clone https://github.com/yansntss/Heimdall.git
cd Heimdall
dotnet run
```

Pronto — a barra deve aparecer na borda da tela.

### Gerando seu próprio build

```bash
# Leve (precisa do .NET 10 Desktop Runtime instalado na máquina que for rodar)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Self-contained (maior, mas roda em qualquer PC Windows sem instalar nada)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

O executável final fica em `bin/Release/net10.0-windows.../win-x64/publish/`.

---

## ⚙️ Configuração

Tudo pode ser ajustado pela interface: clique direito na barra → **Configurações...**
abre uma janela com abas de borda/tamanho, aparência/temas, widgets e lembretes —
"Salvar e recarregar" aplica na hora, sem reiniciar o app.

Quem preferir, pode editar o JSON direto em `%AppData%\Heimdall\config.json`:

```jsonc
{
  "Edge": "Top",               // Top | Bottom | Left | Right
  "Thickness": 28,              // DIPs
  "FloatingMode": false,        // true = barra flutuante (margem + cantos arredondados)
  "MonitorMode": "Primary",     // Primary | Specific | All
  "Theme": "Escuro",            // tema embutido ou salvo em .../themes/*.json
  "Widgets": { "Start": [], "Center": ["clock"], "End": ["media", "reminder", "launcher"] }
}
```

Detalhes completos de cada opção, dos widgets, temas customizados e da estrutura interna
do projeto estão em [`docs/GUIA.md`](docs/GUIA.md).

---

## 🧩 Widgets disponíveis

| Widget | O que faz |
|---|---|
| `clock` | Relógio e data, formato configurável |
| `media` | Controle de mídia (SMTC), volume por app, capa do álbum |
| `reminder` | Lembretes fixos e agendados, com histórico |
| `launcher` | Atalhos de apps, pastas, arquivos e URLs |

---

## 🛠️ Stack

- **.NET 10** + **WPF** (interface nativa, sem Electron)
- **P/Invoke** direto no Windows Shell/DWM pra reserva de espaço na tela (AppBar),
  detecção de tela cheia, extração de ícones e backdrops (Acrílico/Mica)
- **SMTC** (System Media Transport Controls) pro controle de mídia
- **NAudio.Wasapi** pro volume por aplicativo

---

## 🤝 Contribuindo

Encontrou um bug, quer sugerir uma ideia ou mandar um PR? Abra uma
[issue](https://github.com/yansntss/Heimdall/issues) ou um pull request — toda
contribuição é bem-vinda.

## 💜 Apoie o projeto

O Heimdall é gratuito e feito nas horas livres. Se ele te ajudou e você quiser retribuir,
um Pix (de qualquer valor) é muito bem-vindo pra manter o projeto vivo:

**Chave Pix:** `SUA_CHAVE_PIX_AQUI`
<!-- Substitua pela sua chave Pix (e-mail, celular, CPF ou chave aleatória). -->

Nenhuma doação é obrigatória pra usar, sugerir ou contribuir com o projeto — é só uma
forma de dizer obrigado, se você quiser. 🙂

---

## 📄 Licença

MIT — veja [LICENSE](LICENSE) para mais detalhes.
