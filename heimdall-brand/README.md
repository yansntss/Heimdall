# Heimdall — pacote de marca

## Conteúdo
- `svg/` — vetores (textos convertidos em curvas, não precisam das fontes)
  - `heimdall-logo-dark.svg` / `heimdall-logo-light.svg` — espada + nome + slogan
  - `heimdall-sword.svg` / `heimdall-sword-light.svg` — só a espada, fundo transparente
  - `heimdall-icon.svg` — ícone do app
  - `heimdall-icon-small.svg` — ícone simplificado para 16 e 24 px
- `png/` — ícone de 16 a 1024 px, logos @2x, espada @2x e banner para o README do GitHub
- `ico/heimdall.ico` — ícone do Windows com 16, 24, 32, 48, 64, 128 e 256 px

## Usar no projeto WPF
1. Copie `ico/heimdall.ico` para `Assets/heimdall.ico`.
2. No `.csproj`:
   ```xml
   <PropertyGroup>
     <ApplicationIcon>Assets\heimdall.ico</ApplicationIcon>
   </PropertyGroup>
   <ItemGroup>
     <Resource Include="Assets\heimdall.ico" />
   </ItemGroup>
   ```
3. Nas janelas: `Icon="pack://application:,,,/Assets/heimdall.ico"`.

## Banner no README do GitHub
```markdown
<p align="center"><img src="docs/heimdall-github-banner.png" alt="Heimdall" width="640"></p>
```

## Cores
| Nome | Hex |
|---|---|
| Fundo | `#0F1318` |
| Ícone | `#1A2028` |
| Lâmina | `#161B22` |
| Marfim | `#F2EEE6` |
| Âmbar | `#E9A23B` |
| Verde-azulado | `#2FA39A` |
| Azul | `#3F6FE0` |
| Cabo / separadores | `#3A4350` |
| Texto secundário | `#A7B0BC` |

## Fontes
- Nome e slogan: **Space Grotesk** (700 e 400)
- Relógio: **IBM Plex Mono**

Ambas gratuitas (OFL), no Google Fonts.
