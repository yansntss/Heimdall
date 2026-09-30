<!--
  Corpo da release no GitHub, montado automaticamente pelo workflow de release:
  - {{CHANGELOG}} vira a seção da versão no CHANGELOG.md (o job falha se ela não existir)
  - vX.Y.Z vira a tag da versão
  - {{SHA256SUMS}} vira o conteúdo do SHA256SUMS.txt gerado no build
  As notas automáticas do GitHub (link "Full Changelog") entram logo abaixo.
-->

## O que mudou nesta versão

{{CHANGELOG}}

## Qual arquivo baixar

| Arquivo                                  | Quando usar                                                                                            |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `Heimdall-vX.Y.Z-win-x64.zip`            | Leve. Requer o [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado. |
| `Heimdall-vX.Y.Z-win-x64-standalone.zip` | Maior (~150 MB), roda em qualquer PC Windows sem instalar nada.                                        |

## Requisitos

- Windows 10 ou 11
- Na variante leve: [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Aviso do SmartScreen

> O executável não é assinado digitalmente, então o Windows pode exibir o alerta "O
> Windows protegeu o computador" na primeira execução. Clique em **Mais informações** →
> **Executar assim mesmo**. Para confirmar que o arquivo não foi adulterado, compare o
> hash SHA256 com os publicados abaixo.

## Como verificar o checksum

```powershell
Get-FileHash Heimdall-vX.Y.Z-win-x64.zip -Algorithm SHA256
```

O arquivo `SHA256SUMS.txt` já vem anexado a esta release, mas os hashes também estão
colados aqui para facilitar:

```
{{SHA256SUMS}}
```

## Limitações conhecidas

- Widget de temperatura precisa do Heimdall rodando como administrador.
- Widget de FPS precisa do RTSS/MSI Afterburner rodando.
- O overlay funciona apenas em tela cheia em janela (borderless), não em tela cheia
  exclusiva.
