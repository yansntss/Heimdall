<!--
  Template pro corpo da release no GitHub. Colar acima das notas automáticas
  geradas pelo `generate_release_notes: true` do workflow, preenchendo:
  - vX.Y.Z pela tag da versão
  - o resumo do que mudou
  - os hashes do SHA256SUMS.txt gerado no build
-->

## O que mudou nesta versão

- ...

## Qual arquivo baixar

| Arquivo | Quando usar |
|---|---|
| `Heimdall-vX.Y.Z-win-x64.zip` | Leve. Requer o [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) instalado. |
| `Heimdall-vX.Y.Z-win-x64-standalone.zip` | Maior (~150 MB), roda em qualquer PC Windows sem instalar nada. |

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
<colar o conteúdo de SHA256SUMS.txt aqui>
```

## Limitações conhecidas

- Widget de temperatura precisa do Heimdall rodando como administrador.
- Widget de FPS precisa do RTSS/MSI Afterburner rodando.
- O overlay funciona apenas em tela cheia em janela (borderless), não em tela cheia
  exclusiva.
