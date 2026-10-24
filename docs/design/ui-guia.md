<!-- plano: 20261004-1829-hud-auditoria · tarefa: T3 · executor: designer-ux-arte · gerado-em: 2026-10-04 · commit-base: 0c387de -->
# Guia de estilo e especificação de telas da UI — Vampire's Prey

> Fase 1, tarefa T3. Fontes: `docs/design/ui-auditoria.md` (T1, citada como **Aud §n / Tn**) e `docs/design/ui-inventario.md` (T2, citado como **Inv §n / Cn**).
> Este guia é a referência para a fundação (`hud-fundacao`) e para as fases 3a/3b/3c (`hud-ingame`, `hud-menus-sistema`, `hud-overlays-progressao`). **Quem implementa não toma decisão de design:** se algo não estiver aqui, registre como pendência no resultado da tarefa.
> Resolução base: 1600×900 lógicos (stretch `canvas_items`/`expand`). Todos os valores em px são lógicos.

## Sumário

1. Princípios · 2. Tokens · 3. Componentes · 4. Layout · 5. Interação, pausa e foco · 6. Feedback de tela · 7. Ferramentas de dev · 8. Especificação por tela · 9. Não mexer · 10. Decisões em aberto · Anexo A (previews) · Anexo B (bugs → tarefa) · Anexo C (chaves de localização sugeridas) · Anexo D (localização na prática)

---

## 1. Princípios

1. **Leitura de combate primeiro.** Nada persistente no centro da tela. Nenhum efeito de tela cobre o piso inteiro. Feedback de hit é curto e não esconde telegraphs.
2. **Vermelho vivo = perigo; carmesim = vida.** Cada vermelho tem um papel só (§2.2). Raridade e decoração usam tons próprios e sempre vêm com rótulo ou forma.
3. **Controle é cidadão de primeira classe.** Toda informação acessível por mouse também é acessível por foco. Foco sempre visível. `ui_cancel` fecha a camada do topo. Dicas de botão no rodapé de toda tela.
4. **Consistência via componentes.** Toda tela é montada com os componentes da §3 (`UiComponents`) e os tokens da §2 (`UiPalette`/`UiFont`/`UiMetrics`/`UiMotion`). Proibido `new Color(...)`, `Modulate` de estado ou tamanho de fonte inline nas telas; se faltar um token, é pendência da fundação.

---

## 2. Tokens

Implementação: fundação T1, em `scripts/ui/UIThemeFactory.cs` como classes estáticas públicas `UiPalette`, `UiFont`, `UiMetrics`, `UiMotion` (no mesmo arquivo, indentação de 4 espaços do arquivo). Valores prontos para C#.

### 2.1 Paleta

```csharp
public static class UiPalette
{
    // Superfícies
    public static readonly Color ScreenBg     = new Color(0.006f, 0.002f, 0.005f, 1.00f); // fundo de tela (menu)
    public static readonly Color Scrim        = new Color(0.010f, 0.000f, 0.004f, 0.85f); // fundo ÚNICO de overlay modal
    public static readonly Color Panel        = new Color(0.016f, 0.011f, 0.014f, 0.97f); // janela, card
    public static readonly Color PanelHud     = new Color(0.012f, 0.009f, 0.011f, 0.84f); // painéis do HUD em jogo
    public static readonly Color PanelRaised  = new Color(0.060f, 0.028f, 0.036f, 0.98f); // hover, foco, aba ativa, linha selecionada
    public static readonly Color PanelInset   = new Color(0.030f, 0.012f, 0.016f, 1.00f); // trilho de barra, campo, scrollbar

    // Bordas
    public static readonly Color Border       = new Color(0.28f, 0.16f, 0.17f, 0.72f);    // borda padrão (= HudPanel atual)
    public static readonly Color WindowBorder = new Color(0.82f, 0.64f, 0.30f, 0.45f);    // moldura de janela (dourado a 45%)
    public static readonly Color Focus        = new Color(0.90f, 0.72f, 0.38f, 1.00f);    // anel de foco
    public static readonly Color FocusGlow    = new Color(0.90f, 0.72f, 0.38f, 0.25f);    // sombra do anel de foco

    // Texto
    public static readonly Color TextPrimary   = new Color(0.92f, 0.88f, 0.80f, 1.00f);
    public static readonly Color TextSecondary = new Color(0.68f, 0.62f, 0.57f, 1.00f);
    public static readonly Color TextDisabled  = new Color(0.52f, 0.48f, 0.45f, 1.00f); // ajustado na revisão da fundação (contraste em 720p)
    public static readonly Color TextOutline   = new Color(0.02f, 0.005f, 0.008f, 0.90f);

    // Identidade / destaque
    public static readonly Color Gold        = new Color(0.82f, 0.64f, 0.30f, 1.00f);     // títulos de tela, valores-chave, seleção
    public static readonly Color GoldBright  = new Color(0.90f, 0.72f, 0.38f, 1.00f);     // display (título do menu, game over)
    public static readonly Color GoldDim     = new Color(0.55f, 0.42f, 0.20f, 1.00f);     // divisores, hover de borda, slider
    public static readonly Color DecorCrimson = new Color(0.45f, 0.06f, 0.09f, 1.00f);    // decoração: glow, sombra de título, cabeçalho Bestialis

    // Jogo (papéis fixos)
    public static readonly Color Danger      = new Color(1.00f, 0.30f, 0.12f, 1.00f);     // perigo: alerta de wave, ação destrutiva, vinheta
    public static readonly Color DangerBg    = new Color(0.30f, 0.05f, 0.02f, 0.92f);     // fundo de alerta / hover destrutivo
    public static readonly Color Life        = new Color(0.62f, 0.06f, 0.11f, 1.00f);     // preenchimento da vida
    public static readonly Color LifeHighlight = new Color(0.80f, 0.18f, 0.22f, 1.00f);   // realce superior (2 px) da vida
    public static readonly Color LifeChip    = new Color(0.95f, 0.85f, 0.75f, 0.55f);     // faixa de dano atrasada
    public static readonly Color BossFill    = new Color(0.72f, 0.10f, 0.14f, 1.00f);
    public static readonly Color Restore     = new Color(0.39f, 0.78f, 1.00f, 1.00f);     // cura e barreira (texto, "+N")
    public static readonly Color BarrierFill = new Color(0.12f, 0.52f, 0.95f, 1.00f);     // preenchimento da barreira (atual)
    public static readonly Color Fury        = new Color(0.94f, 0.37f, 0.04f, 1.00f);
    public static readonly Color FuryReady   = new Color(1.00f, 0.72f, 0.20f, 1.00f);
    public static readonly Color Warn        = new Color(1.00f, 0.70f, 0.28f, 1.00f);     // aviso não letal (#ffb347): substitui, moedas insuficientes
    public static readonly Color DamageTaken = new Color(1.00f, 0.36f, 0.32f, 1.00f);     // número de dano recebido

    // Raridades (decorativas; SEMPRE com selo de texto)
    public static readonly Color RarityCommon    = new Color(0.66f, 0.63f, 0.58f, 1.00f);
    public static readonly Color RarityUncommon  = new Color(0.34f, 0.74f, 0.58f, 1.00f); // jade (≠ Pestilência #78d957)
    public static readonly Color RarityRare      = new Color(0.40f, 0.50f, 0.98f, 1.00f); // índigo (≠ Barreira #63c7ff)
    public static readonly Color RarityEpic      = new Color(0.66f, 0.36f, 0.92f, 1.00f);
    public static readonly Color RarityLegendary = new Color(0.96f, 0.60f, 0.20f, 1.00f);
    public static readonly Color RarityMythic    = new Color(0.94f, 0.90f, 0.78f, 1.00f); // só artefatos (1 no catálogo)
    public static readonly Color RarityStirps    = new Color(0.70f, 0.16f, 0.34f, 1.00f); // vinho (≠ Vida, ≠ Sangramento)

    public static Color Rarity(string rarity) => (rarity ?? "").ToLowerInvariant() switch
    {
        "uncommon" => RarityUncommon, "rare" => RarityRare, "epic" => RarityEpic,
        "legendary" => RarityLegendary, "mythic" => RarityMythic, "stirps" => RarityStirps,
        _ => RarityCommon
    };
}
```

**Estados de skill (Fonte de Sangue)** — sempre StyleBox + marca de forma, nunca `Modulate` (Aud T4):

| Estado (código atual, `HUD.BloodFountain.cs:323-336`) | Família | Fundo | Borda | Texto | Marca de forma |
|---|---|---|---|---|---|
| comprada, rank < máx, comprável | **Comprado** | `PanelRaised` | 2 px `Gold` | `TextPrimary` | selo rank "1/3" em `Gold` + custo |
| comprada, rank máximo | **Comprado (máx)** | `PanelRaised` | 3 px `GoldBright` | `TextPrimary` | selo "MÁX" |
| comprada, sem moedas p/ próximo rank | **Comprado** | `PanelRaised` | 2 px `Gold` | `TextPrimary` | custo em `Warn` |
| disponível, comprável | **Disponível** | `Panel` | 2 px `TextPrimary` a 80% | `TextPrimary` | custo em `Gold` sob o nó |
| disponível, sem moedas | **Disponível** | `Panel` | 1 px `Border` | `TextSecondary` | custo em `Warn` |
| pré-requisito faltando | **Bloqueado** | `PanelInset` | 1 px `Border` | `TextDisabled` | cadeado desenhado (§3.13) |
| conflito exclusivo | **Bloqueado** | `PanelInset` | 1 px `Border` | `TextDisabled` | diagonal 2 px `TextSecondary` sobre o nó + "Exclusiva" no detalhe |
| `!IsImplemented` | **Planejado** | `Panel` a alpha 0.55 | 1 px tracejado `TextSecondary` a 50% | `TextDisabled` | selo "EM BREVE" |

Links da árvore: pré-requisito não comprado = 3 px `Border`; comprado = 4 px `Gold`.

**Cores de palavra-chave nas descrições** (`HUD.BlessingOffer.cs:484-517`): mantidas como estão (ver §9). Sempre aparecem junto da palavra, então não dependem só de cor.

### 2.2 Regras dos vermelhos

| Token | Pode | Não pode |
|---|---|---|
| `Danger` | alerta de wave, borda/texto de botão destrutivo, vinheta de dano e de vida crítica, telegraphs (3D, já existentes) | título, prompt de interação, raridade, seleção, decoração |
| `Life` / `LifeHighlight` / `BossFill` | barras de vida (jogador e chefe) | texto, bordas de card |
| `DamageTaken` | número de dano recebido | qualquer outro uso |
| `DecorCrimson` | glow do menu, sombra de título display, cabeçalho Bestialis, divisores decorativos | estados, alertas |
| `RarityStirps` | borda e selo de raridade Stirps | qualquer outro uso |

Seleção, foco e títulos são **dourados**. `Modulate (1,0.35,0.32)` de seleção (Inv C12) deixa de existir.

### 2.3 Tipografia

Fonte: a padrão do Godot (o tema não define `DefaultFont`; não há fonte no projeto). **Não adicionar fonte** nesta reforma (exigiria asset novo, `SOURCE.md` e `CREDITS.md`).

```csharp
public static class UiFont
{
    public const int Display   = 54; // título do menu, GAME OVER
    public const int Banner    = 34; // SALA LIMPA, SANGUIS PURGATUS
    public const int Title     = 28; // título de tela/janela
    public const int Section   = 22; // título de card, cabeçalho de coluna, nome do chefe, título de confirmação
    public const int Body      = 17; // corpo (= DefaultFontSize atual)
    public const int Secondary = 15; // metadados, status, tooltip, dicas de rodapé
    public const int Badge     = 14; // selos (N2, MÁX, EQUIPADA, raridade) — só com fundo opaco
    public const int DamageNumber = 30;
}
```

- A escala da auditoria (54/28/22/17/15/13) foi mantida, com duas mudanças: **selo sobe de 13 para 14**, para respeitar o mínimo da Aud §21 (em 720p, 14 vira ~11 px físicos); e **Banner 34** foi incluído para os anúncios que hoje usam 27–32.
- Mínimos: nada abaixo de 14. Informação essencial no HUD em jogo ≥ 15. Texto essencial nunca com alpha < 0,8.
- Outline: `Label` 3 (tema atual). Texto do HUD sobre o mundo (valores, toasts, número de dano): 4–6. Display: sombra `DecorCrimson` alpha 0.85, offset (0,4), `shadow_outline_size` 10 (como o menu hoje).
- Caixa alta: só Display, Banner e selos (`Label.Uppercase = true`, nunca no texto da chave). Títulos de tela em caixa normal ("Pausa", "Fonte de Sangue").
- Cor: Display `GoldBright`; Title `Gold`; Section `TextPrimary` (cabeçalho de coluna em `Gold`); Body `TextPrimary`; Secondary `TextSecondary`.
- Variações de tema (`theme_type_variation`, substituem os overrides inline de Inv C18): `TitleLabel`, `SectionLabel`, `SecondaryLabel`, `BadgeLabel`, `HudValueLabel` (Body, outline 5), `DisplayLabel`.

### 2.4 Espaçamento, raios, bordas

```csharp
public static class UiMetrics
{
    // Escala de espaçamento
    public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32, S7 = 48;
    public const int SafeMargin = 24;      // margem segura do HUD em jogo
    public const int OverlayMarginX = 48, OverlayMarginY = 40; // janelas "cheias"
    // Raios
    public const int RadiusCard = 2, RadiusControl = 4, RadiusBadge = 3;
    // Bordas
    public const int BorderThin = 1, BorderFocus = 2, BorderWindow = 2, BorderCard = 3, BorderCardFocus = 4;
    public const int FocusExpand = 3;      // expand_margin do anel de foco
    // Padding de conteúdo
    public const int PadPanelX = 16, PadPanelY = 12, PadWindowX = 24, PadWindowY = 20;
    // Ícones
    public const int IconXL = 92, IconL = 64, IconM = 48, IconHud = 44, IconS = 32, IconGlyph = 22, IconInline = 20;
    // Alturas de controle
    public const int ButtonL = 52, ButtonM = 44, ButtonS = 36, RowHeight = 52, FooterHeight = 52, TabHeight = 44;
}
```

### 2.5 Opacidades

| Uso | Valor |
|---|---|
| Scrim de overlay modal (único) | 0.85 (`UiPalette.Scrim`) |
| Janela/card | 0.97–0.98 |
| Painéis do HUD em jogo | 0.84 |
| Elemento desabilitado | cores `TextDisabled`/`PanelInset` (não usar `Modulate` alpha) |
| Skill "planejada" | 0.55 (única exceção de alpha de estado) |
| Log de eventos (debug) | 0.90, esmaecendo a 0 após 6 s |
| Vinheta (borda) | ver §6 |

### 2.6 Animações e tempos

```csharp
public static class UiMotion
{
    public const float Hover = 0.08f;          // troca de StyleBox/cor em hover/foco (pode ser instantânea)
    public const float OverlayIn = 0.12f;      // fade do scrim + janela (modulate:a 0→1)
    public const float OverlayOut = 0.08f;
    public const float ToastIn = 0.15f, ToastHold = 1.6f, ToastOut = 0.30f;
    public const int   ToastMaxVisible = 3, ToastQueueMax = 6;
    public const float WaveAlertIn = 0.15f, WaveAlertHold = 1.6f, WaveAlertOut = 0.25f; // total ~2 s
    public const float BannerIn = 0.25f, BannerHold = 1.6f, BannerOut = 0.40f;
    public const float KillPopup = 0.8f;       // "+N" ao lado das moedas
    public const float DamageNumber = 0.6f, DamageNumberFadeTail = 0.25f, DamageNumberRise = 40f;
    public const float ChipDelay = 0.35f, ChipDrain = 0.40f;
    public const float FlashRise = 0.03f, FlashFall = 0.12f;
    public const float CriticalPulseHz = 1.2f;
    public const float LogFadeAfter = 6.0f;
}
```

Regras: todo tween de UI roda com o HUD em `ProcessMode.Always`; **antes de criar um tween no mesmo alvo, `Kill()` o anterior** (padrão de `ShowEnemyDefeatedToast`, Inv §7.1); após `await`, conferir `IsInstanceValid(this)` e se o nó alvo ainda existe.

---

## 3. Componentes

Implementação: fundação T1 em `scripts/ui/UiComponents.cs` (classe estática `UiComponents`, indentação de 4 espaços) + estilos de tema em `UIThemeFactory.Create()`. Os métodos antigos (`HudPanel`, `ChoiceCard`, `AddSimpleFrame`, `BlessingRarityColor`) continuam existindo e passam a delegar aos novos até as fases 3 migrarem.

**Estados padrão de qualquer controle focável** (aplicados no tema a `Button`, `OptionButton`, `CheckButton`, `HSlider`, abas, linhas de lista):

| Estado | Fundo | Borda | Texto | Extra |
|---|---|---|---|---|
| normal | `Panel` | 1 px `Border` | `TextPrimary` | — |
| hover (mouse) | `PanelRaised` | 1 px `GoldDim` | branco | cursor `PointingHand` |
| foco (teclado/controle) | `PanelRaised` | 2 px `Focus` + `expand_margin` 3 + sombra `FocusGlow` 6 px | branco | **sempre** mais forte que o hover |
| pressionado | `PanelRaised` escurecido 20% | 2 px `Gold` | `Gold` | — |
| desabilitado | `PanelInset` | 1 px `Border` a 50% | `TextDisabled` | motivo em texto secundário perto (ex. "Moedas insuficientes") |
| selecionado (toggle/aba/slot) | `PanelRaised` | 1 px `Border` + **barra inferior 3 px `Gold`** | `Gold` | coexistindo com foco: anel de foco por fora |

Implementação do foco: StyleBox `focus` do tema (desenhado por cima) com `BgColor` transparente, `BorderColor = Focus`, `BorderWidth = 2`, `ExpandMargin = 3`, `ShadowColor = FocusGlow`, `ShadowSize = 6`.

### 3.1 Janela / overlay modal — `UiComponents.Window(...)` (Inv C3, C4, C5, C6, C8, C17)

Assinatura sugerida: `Window(Control parent, string name, WindowSize size, string title, string subtitle = null) → UiWindow` com `UiWindow.Root` (scrim), `.Header`, `.Status` (Label), `.HeaderRight` (HBox p/ moedas), `.Body` (VBox), `.Footer` (`HintBar`).

```
┌──────────────────────── Scrim (tela cheia, Scrim 0.85, MouseFilter Stop) ────────────────────────┐
│        ┌──────────────── Window (PanelContainer: Panel 0.97, borda 2 WindowBorder, r4) ───────┐  │
│        │ [HeaderRight: moedas]                 Título (Title 28, Gold, centro)                │  │
│        │                           status / subtítulo (Secondary 15, centro, autowrap)        │  │
│        │ ───────────── divisor simple_divider.png, 4 px, GoldDim a 60% ─────────────          │  │
│        │ Body (VBox, separação 12; lista rolável quando passar da altura)                     │  │
│        │                                                                                      │  │
│        │ ──────────── borda 1 px Border ────────────────────────────────────────────────      │  │
│        │ Footer 52: [○/B/Esc] Voltar                    [△/Y/R] Trocar   [✕/A/Enter] Escolher │  │
│        └──────────────────────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

| Tamanho (`WindowSize`) | Largura × altura | Uso |
|---|---|---|
| `Small` | 560 × auto (máx. 420) | confirmação, game over |
| `Medium` | 640 × auto (máx. 680) | pausa e subpáginas, opções (cabe a linha de opção 240+280+64 com padding) |
| `Large` | 1240 × 720 (mín. 960 × 560) | ofertas, altar, baú, bancada (880×640 — usar `Large` com corpo de 880 centrado) |
| `Full` | âncoras 0..1 com margens 48/40 | ficha, Fonte de Sangue, compêndio |

- Padding interno 24/20. Centralizada por âncoras 0.5 + offsets ±w/2, ±h/2 (nunca offsets absolutos a partir do canto).
- Abrir: `UiModalStack.Push` (§5.2) → fade `OverlayIn`. Fechar: `Pop`.
- Estados: única variação é `Mandatory` (oferta obrigatória): o rodapé não mostra "Voltar" e `ui_cancel` chama o handler de aviso.
- Vazio: corpo mostra uma linha `Secondary` centrada (ex. "Nenhum artefato na coleção").

### 3.2 Botões — `UiComponents.MenuButton(text, onPressed, ButtonVariant variant, ButtonSize size)` (Inv C7)

| Variante | normal | hover/foco | Uso |
|---|---|---|---|
| `Primary` | fundo `Panel`, borda 1 `Gold` a 70%, texto `TextPrimary` | estados padrão | ação principal da tela (Escolher, Comprar, Continuar) |
| `Secondary` | estados padrão (borda `Border`) | estados padrão | Voltar, Opções, Cancelar |
| `Destructive` | fundo `Panel`, borda 1 `Danger` a 60%, texto `Danger` | fundo `DangerBg`, borda 2 `Danger`, anel de foco `Focus` por fora | Excluir, Desistir, Voltar ao menu (sempre abre `Confirm`) |
| `Flat` | sem fundo/borda, texto `TextSecondary` | texto `TextPrimary`, barra inferior 2 `GoldDim` | dica de rodapé clicável, linha de lista |

| Tamanho | Altura | Largura mínima | Fonte |
|---|---|---|---|
| `Large` | 52 | 420 (listas de menu: largura da coluna, 420–460) | Body 17 |
| `Medium` | 44 | 200 | Body 17 |
| `Small` | 36 | 120 | Secondary 15 |

Texto nunca corta: largura mínima, crescendo com o texto (`en`/`es` ~30% maiores). Ações destrutivas ficam **no fim da lista, após um divisor**.

### 3.3 Diálogo de confirmação — `UiComponents.Confirm(parent, title, body, confirmText, onConfirm, ConfirmKind kind = Destructive)`

- `Window(Small)`: título `Section 22` `Gold`; corpo `Body` autowrap (largura 512); segunda linha `Secondary` "Esta ação não pode ser desfeita." quando irreversível.
- Botões no rodapé: **[Cancelar] `Secondary` à esquerda com foco inicial**, [Confirmar] `Destructive` à direita. `ui_cancel` = Cancelar.
- Painel 100% opaco (o texto da tela de baixo não vaza); se já há modal por baixo, o scrim é leve (0,6).
- Empilha sobre a tela atual (Push); ao fechar, o foco volta ao botão que abriu.
- Usos obrigatórios: excluir slot (menu), desistir da run, voltar ao menu principal com run ativa, "Redefinir valores originais" (debug).

### 3.4 Card de escolha — `UiComponents.ChoiceCard(ChoiceCardData data, Action onChoose)` (Inv C1, C2; unifica bênção/artefato/altar)

`ChoiceCardData`: `Texture2D Icon`, `string IconText` (monograma se não houver ícone), `string Title`, `string RarityId`, `string RarityLabel` (localizado), `string Meta`, `string BodyBbcode`, `string PreviewBbcode` (altar: "atual → próximo"), `string Warning` (ex. "Substitui: X"), `string ActionText`, `string CostText`, `bool Disabled`, `string DisabledReason`.

```
┌▌───────────────────────────────────────────────────────────────────────────────────────┐ borda 3 px Rarity(id), r2
│▌ ┌────────┐  Título (Section 22, TextPrimary)   [RARA] ← selo Badge 14, fundo Rarity  ┌──────────┐ │
│▌ │ ícone  │  Meta (Secondary 15): Linhagem · Slot · N1 → N2                           │ Escolher │ │
│▌ │ 92×92  │  Corpo (Body 17, RichText, autowrap, números já calculados)               │  148×82  │ │
│▌ └────────┘  Prévia (altar) / ⚠ Aviso em Warn ("SUBSTITUI: Nome")                     │ custo    │ │
└▌───────────────────────────────────────────────────────────────────────────────────────┘
 ▲ barra de foco 6 px Gold (visível só em foco/hover)
```

- Largura: preenche a lista (máx. 1080). Altura mínima 132; cresce com o texto (`FitContent`), lista rola.
- Ícone 92 em moldura `PanelInset` 104×104. Sem ícone (artefatos usam `generic.png`): **monograma** = 2 letras do nome em `Section 22` `Gold` sobre disco `PanelInset` (Aud §12), não o ankh branco.
- Stirps: além da borda vinho, linha interna 1 px `Gold` (marca de forma).
- Estados: **normal** borda 3 `Rarity`; **foco/hover** (do botão OU do card) borda 4 `Rarity` clareada 15% + fundo `PanelRaised` + barra esquerda 6 px `Gold`; **pressionado/escolhido** flash `Gold` 0,15 s na borda antes de fechar; **desabilitado** borda 1 `Border`, texto `TextDisabled`, botão desabilitado e `DisabledReason` em `Warn` sob o botão.
- Foco: o elemento focável é o **botão de ação**; o card espelha o estado via `FocusEntered/Exited` e `MouseEntered/Exited`. Clique em qualquer parte do card = ação (como a bênção hoje). D-pad cima/baixo entre cards com wrap (§9). `FocusNeighborLeft/Right` do botão apontam para ele mesmo (evita saltos geométricos).
- O aviso de substituição aparece **inline sempre** (não depende de hover).

### 3.5 Barra de recurso — `UiComponents.ResourceBar(ResourceBarKind kind, float width)`

Anatomia: trilho `PanelInset` com borda 1 `Border` → faixa *chip* (`LifeChip`, segue o valor com `ChipDelay`+`ChipDrain`) → preenchimento → sobreposição opcional (barreira) → marcas a cada 25% (1 px, preto a 50%; no `Health`, 25% da **vida máxima**, não da vida + barreira) → rótulo de valor.

| Tipo | Tamanho | Preenchimento | Rótulo | Extras |
|---|---|---|---|---|
| `Health` | largura do chamador (HUD: 260) × 22 | `Life` + realce superior 2 px `LifeHighlight` | "83/100" **fora da barra**, na mesma linha: rótulo de largura mínima 56, alinhado à direita, `HudValueLabel` (`Body 17` `TextPrimary`, outline 5) — nunca sobre o chip nem sobre a barreira | barreira: `BarrierFill` atrás da vida sobre máximo combinado (comportamento atual); "+20" em `Restore` num rótulo **separado** à direita do valor, com largura reservada 40 (o painel não muda de largura; sem barreira o texto fica vazio). Linha: barra · 8 · valor 56 · 8 · barreira 40 = 372 |
| `Fury` | largura do chamador (HUD: 260, alinhada à vida) × 10 | `Fury` | selo "FÚRIA" `Badge` à esquerda, acima | cheia: borda 2 `FuryReady` pulsando (1,2 Hz) + glifo do botão (`F`/RB/R1); ativa: borda `FuryReady` fixa, drenagem visível |
| `Boss` | âncoras 0.25..0.75 × 18 | `BossFill` | nome à esquerda (`Section 22` `Gold`), "1234/5678" à direita (`Secondary 15` em `TextPrimary` — valor essencial) em rótulos separados | trilho `PanelHud` com borda 1 `WindowBorder`; chip |

Dano: borda do trilho pisca `TextPrimary` 0,12 s (substitui parte do flash de tela, §6).

### 3.6 Tooltip e painel de detalhes (Inv C14, C15; Aud T5)

**Regra:** tooltip só **repete** informação acessível de outra forma. Toda tela com itens tem um **painel de detalhes que segue o foco e o hover**.

- Tooltip (tema): `TooltipPanel` = `PanelRaised`, borda 1 `Border`, r4, padding 12/8; `TooltipLabel` `Secondary 15` `TextPrimary`, largura máx. 360.
- `UiComponents.DetailPanel(DetailPanelDock dock)` → `UiDetailPanel.Show(DetailData)` / `.Clear()`:
  - `Right`: 380 de largura, altura do corpo; `Bottom`: altura 150, largura do corpo.
  - Conteúdo: ícone 64 + nome `Section 22` (`Rarity` ou `Gold`) + selo de raridade/estado; meta `Secondary`; corpo `Body`; flavor `Secondary` `TextSecondary`; linha de custo/ação em `Gold` com glifo ("[✕] Comprar · 3 moedas").
  - Atualiza em `FocusEntered` **e** `MouseEntered` dos itens; vazio: "Selecione um item" `Secondary` centrado.
  - Não recebe foco; o texto longo rola sozinho com analógico direito (`ui_scroll_*`, §5.4).

### 3.7 Lista rolável — `UiComponents.ScrollList(minHeight)` (Inv C9)

`ScrollContainer` com `FollowFocus = true`, `HorizontalScrollMode = Disabled`, VBox separação 12. Tema: `VScrollBar` trilho `PanelInset` 8 px r4; `grabber` `Border`; `grabber_highlight` `GoldDim`; `grabber_pressed` `Gold`. Lista sem itens focáveis (créditos, texto longo): o próprio `ScrollContainer` recebe `FocusMode.All`, mostra o anel de foco e rola 48 px por `ui_up/ui_down` e contínuo pelo analógico direito.

### 3.8 Abas — `UiComponents.Tabs(string[] labels, Action<int> onChanged)`

- HBox de `Button` `ToggleMode` + `ButtonGroup`; altura 44; largura mínima 140; texto `Body`.
- Ativa: fundo `PanelRaised`, texto `Gold`, barra inferior 3 px `Gold`. Inativa: sem fundo, texto `TextSecondary`. Hover: texto `TextPrimary`. Foco: anel padrão.
- Glifos de `ui_tab_prev`/`ui_tab_next` (L1/R1, LB/RB, Q/E) nas pontas. Trocar de aba com L1/R1 funciona com foco em qualquer lugar da tela; o foco vai ao primeiro item da nova aba.
- Abas só em telas que pausam (ficha, compêndio) — `E` é também `interact`.

### 3.9 Toast e fila de notificações — `UiComponents.ToastStack(parent)` (Aud §5)

Três canais (HUD em jogo):

| Canal | Quando | Aparência | Posição | Tempo |
|---|---|---|---|---|
| **(a) Alerta de combate** | `NotifyWaveIncoming` | faixa 520×64, fundo `DangerBg`, borda esquerda 6 `Danger`; "Próxima onda" `Section 22` `TextPrimary` + "N inimigos ·" em `Secondary` seguido da contagem regressiva (a partir de `seconds`) em rótulo próprio `TextPrimary` | topo-centro, y 132 | `WaveAlert*` (~2 s); não é substituído por kills |
| **(b) Recompensa/info** | artefato, bênção, skill (`NotifySkillMessage`), mensagens da Câmara | `PanelHud`, borda 1 `WindowBorder` (recompensa) ou `Border` (info), ícone opcional 24, texto `Body`; largura 280–560 | topo-centro, abaixo do alerta (y 132 ou 204), empilhando para baixo, separação 8 | `Toast*`; máx. 3 visíveis, fila de 6 (descarta a mais antiga) |
| **(c) Kill** | `NotifyEnemyDefeated` / `NotifyBloodCoinsRewarded` | só "+N" `Body` `Gold`, subindo 16 px; host = moldura do painel de recursos (`_resourceFrame`), à direita da borda (posição lida no quadro seguinte, depois do relayout) | painel de recursos | `KillPopup` 0,8 s; **sem frase** |

### 3.10 Log de eventos — `UiComponents.EventLog()`

Só em `OS.IsDebugBuild()`. Canto inferior direito (âncora 1,1; −24/−24), 360 × 3 linhas, `PanelHud`, `Secondary 15`, **sem horário**, sem quebra (elipse), cada linha esmaece após `LogFadeAfter`. No release: `AppendGameLog` continua existindo (assinatura intacta) e só guarda as últimas 20 entradas em memória (sem UI).

### 3.11 Rodapé de dicas de botão — `UiComponents.HintBar()` + `ButtonGlyph(UiAction, InputDevice)`

- HBox altura 52; esquerda: `ui_cancel` ("Voltar"/"Fechar"); direita, nesta ordem: `ui_alt`, `ui_accept`; abas nas pontas do cabeçalho de abas.
- Cada dica = glifo 22 + rótulo `Secondary`, como `Button` `Flat` (clique dispara a ação; mouse também usa).
- Glifo segue o último dispositivo (`GameCursor.LastDevice`, §5.5); troca sem reabrir a tela (sinal `DeviceChanged`).
- Glifos **desenhados por código** (sem arte nova; Unicode ✕○□△ pode não existir na fonte padrão): disco 22 px `PanelInset` com borda 1 `TextSecondary`; Xbox = letra (A/B/X/Y, LB/RB, LT/RT, View/Menu) em `Badge`; PlayStation = forma desenhada (`DrawLine`/`DrawArc`/`DrawRect`/`DrawPolyline`) em `TextPrimary`, ombros em texto (L1/R1/L2/R2, Create/Options); teclado = keycap retangular r3 com o nome da tecla. Cores neutras (não usar as cores de marca).

| Ação de UI | Teclado | Xbox | PlayStation |
|---|---|---|---|
| `ui_accept` | Enter | A | ✕ |
| `ui_cancel` | Esc | B | ○ |
| `ui_alt` (novo: trocar oferta, ação secundária) | R | Y | △ |
| `ui_tab_prev` / `ui_tab_next` (novo) | Q / E | LB / RB | L1 / R1 |
| `ui_zoom_out` / `ui_zoom_in` (novo) | − / = (e roda) | LT / RT | L2 / R2 |
| `ui_scroll_*` / pan (novo) | arrastar com mouse | analógico direito | analógico direito |
| `character_sheet` | C | View | Create |
| `pause` | Esc | Menu | Options |
| `attack_fury` (dica na barra de fúria) | F | RB | R1 |
| `interact` (prompts) | E | A | ✕ |

### 3.12 Linhas de opção — `LabeledSlider`, `OptionRow`, `ToggleRow` (Inv C10, C11)

- Linha 52 de altura: rótulo 240 (`Body`, à esquerda) · controle (mín. 280, expande) · valor 64 (`Body`, à direita, `TextSecondary`).
- `HSlider` (tema): `slider` trilho 6 px `PanelInset` r3; `grabber_area` `GoldDim`; `grabber_area_highlight` `Gold`; ícones `grabber`/`grabber_highlight` = círculo 16 px gerado por código (`Image` → `ImageTexture`) em `TextPrimary`/`GoldBright`; `focus` = anel padrão. Valor sempre visível ("70").
- `OptionButton`: estados de botão padrão + `PopupMenu` (`panel` `PanelRaised` borda 1 `Border`; `hover` `PanelRaised` com barra esquerda 3 `Gold`; fonte `Body`).
- `CheckButton`: ícones gerados por código 40×22: desligado = trilho `PanelInset` + bolinha `TextSecondary` à esquerda; ligado = trilho `GoldDim` + bolinha `GoldBright` à direita; valor textual "Ligado/Desligado" na coluna de valor (não só a forma).
- `SpinBox` (só ferramentas de dev): `LineEdit` com estilo de campo (`PanelInset`, borda 1 `Border`, foco = anel).
- `UiComponents.OptionsPanel(bool showLanguage)` (devolve `OptionsPanelView`, `scripts/ui/OptionsPanelView.cs`): três abas **Vídeo / Áudio / Jogo** (`UiTabs`; L1/R1 = Q/E trocam, também clicando nas abas), cada uma numa lista rolável (`ScrollList`, `FollowFocus`) de `OptionRow`/`ToggleRow`/`LabeledSlider`. **Usado pelo menu e pela pausa** (mesmo componente, mesmas chaves `options.*`). Detalhes em §3.12.1.

#### 3.12.1 Tela de opções (abas, gráficos e confirmação)

- **Abas:** Vídeo (qualidade gráfica/preset, modo de janela, resolução, monitor [só com 2+ telas], VSync, limite de FPS, escala de renderização, reescalonamento, antisserrilhado, sombras, oclusão de ambiente, glow, brilho), Áudio (música, efeitos) e Jogo (idioma, redução de tremor da câmera, flash de dano). A aba atual é lembrada (`OptionsPanelView.RememberedTab`): a troca de idioma recria a pausa/menu e volta na mesma aba.
- **Foco/controle:** foco inicial no botão da aba atual; cima na primeira linha volta à aba, baixo na última vai ao "Voltar" (e cima/baixo da aba/"Voltar" fecham o ciclo). Esquerda/direita só mexem em sliders (nas demais linhas ficam presas). L1/R1 só valem sem outra camada modal por cima. A roda do mouse rola a lista (os sliders não reagem à roda). A pausa alarga a janela para 704 enquanto a página de opções está aberta.
- **Aplicação:** `GlobalSettings` guarda os dados e grava com atraso (1 s, arquivo `.tmp` + rename, `FlushPendingSave` ao sair); `GraphicsSettings` aplica no viewport raiz/`DisplayServer`/`RenderingServer`/`Engine` e, por cena, em cada `WorldEnvironment` (glow, SSAO, brilho) e `DirectionalLight3D` (sombra e distância) de cada cena carregada (`SceneTree.SceneChanged` + uma aplicação na cena inicial; nenhum script cria esses nós em runtime) sem editar os `.tscn`: o Environment da cena é duplicado e o original fica no meta `vp_gfx_base`. Presets (Baixa = escala 65, passo do slider 5; Média/Alta) mexem em escala, reescalonamento, antisserrilhado, sombras, SSAO, glow, FPS e VSync; a linha "Qualidade gráfica" mostra "Personalizada" quando os valores não batem com nenhum. O padrão (primeiro boot e arquivos antigos) é a aparência de antes: sem MSAA, escala 100%, sombras/SSAO/glow ligados, VSync ligado, FPS ilimitado, brilho 100% (por isso o padrão aparece como "Personalizada").
- **Confirmação:** mudar modo de janela, resolução ou monitor aplica na hora e abre "Manter estas configurações?" (modal na `UiModalStack`, foco inicial em **Reverter**, contagem de 10 s em tempo real que reverte sozinha; Esc/Círculo/B = Reverter). Só "Manter" grava; enquanto a confirmação está pendente o arquivo recebe o estado confirmado (snapshot), e Reverter restaura também o monitor (tela resolvida). No modo Janela a lista só oferece tamanhos que cabem na área útil com a moldura. Arquivo ilegível é copiado para `global_settings.bad.json` antes de cair nos padrões; FPS salvo tem piso de 30 (ou 0).
- **Compatibilidade:** `global_settings.json` antigo (`Language`/`ResolutionIndex`/`Fullscreen`/volumes) é migrado só em memória (`ResolutionIndex` 0..2 = 1280x720/1600x900/1920x1080; `Fullscreen` = modo "Sem bordas"); os campos antigos continuam sendo gravados.
- **Debug:** `--settings-file=<caminho>` (build de debug) usa outro arquivo de opções; previews (`--ui-preview`, `--ui-batch`) não alteram a janela.
- **Previews:** `menu_options`, `menu_options_audio`, `menu_options_game`, `options`, `options_audio`, `options_game`, `options_video_end` (Vídeo rolado até o fim), `options_confirm`. As capturas aplicam o preset gráfico do `global_settings.json` do usuário (MSAA, escala, brilho…), então variam com ele; use `--settings-file=` com um arquivo conhecido para reproduzir.

### 3.13 Ícones e selos — `BlessingIcon(record, size, showLevel)`, `Badge(text, color)`, `LockGlyph`, `CoinGlyph` (Inv C13)

- `Badge`: `BadgeLabel` 14, caixa alta, padding 6/2, fundo da cor do papel (raridade, `Gold`, `PanelInset`), texto `ScreenBg` sobre cores claras e `TextPrimary` sobre escuras.
- `BlessingIcon`: slot `PanelInset` com borda 2 `Rarity` (StyleBox, não `simple_panel` esticado); ícone com inset 4; selo de nível "N2" no canto inferior direito (`Badge`, fundo `PanelInset`, texto `Gold`). Tamanhos: 44 (HUD), 48 (listas), 92 (card).
- `CoinGlyph(size)`: gota desenhada por código (`DrawColoredPolygon`) em `Life` com realce `LifeHighlight` — representa Moedas de Sangue sem asset novo.
- `LockGlyph(size)`: cadeado desenhado (arco + retângulo) em `TextDisabled`.

### 3.14 Número de dano recebido — `UiComponents.DamageNumber(parent, amount, screenPos)`

- Filho do `Root` com tema (corrige Inv §0). `DamageNumber 30`, cor `DamageTaken`, outline 6 `TextOutline`.
- Origem: posição do jogador projetada (`Camera3D.UnprojectPosition(player.GlobalPosition)`) + (0, −80); jitter horizontal ±12; sobe `DamageNumberRise` 40 px em 0,6 s; esmaece nos últimos 0,25 s.
- Golpes em sequência (intervalo < 0,35 s) abrem em **leque**: centro, depois +64 px à direita (e 14 px acima), depois −64 px à esquerda (e 28 px acima); só o primeiro mantém o jitter aleatório de ±12, os deslocados saem sem ele; o ciclo reinicia após uma pausa maior (`HUD.StatusBars.cs`, `ShowReceivedDamageNumber`).
- No máximo 3 vivos; o quarto recicla o mais antigo. Sem câmera, cai para junto da barra de vida (acima do painel do jogador).
- Dano causado aos inimigos (`EnemyBasic.SpawnDamageNumber`, 3D branco/crítico dourado) não muda.

---

## 4. Layout

### 4.1 Regras gerais

- Tudo por **âncoras + offsets relativos**; nada de posição absoluta calculada a partir do tamanho do viewport no build (Fonte hoje, Inv §2.6) — se precisar, recalcular em `GetViewport().SizeChanged`.
- Base 1600×900. Com `canvas_items` + `expand`, **1280×720 e 1920×1080 mostram o mesmo layout lógico** (escala 0,8× e 1,2×). Em proporções diferentes de 16:9, o viewport lógico cresce em uma dimensão: elementos ancorados nas bordas acompanham; janelas centradas ficam centradas e não esticam.
- Margem segura do HUD: 24 de cada borda. Janelas `Full`: 48/40.
- Grade: múltiplos de 4 (escala `UiMetrics.S*`). Colunas de conteúdo em janelas `Full`: 12 colunas de 108 com calhas de 24 (largura útil 1504).
- **Zona de combate protegida:** retângulo de âncoras 0.18..0.82 × 0.16..0.80 sem elemento persistente. Só entram: número de dano, alerta de wave (topo da zona) e prompts de interação.
- Mínimo de texto em 720p: ver §2.3. Verificação visual das 3 resoluções: só em janela real (o Movie Maker grava sempre 1600×900, Aud Método).

### 4.2 Zonas do HUD em jogo (1600×900)

```
y0  ┌────────────────────────────────────────────────────────────────────────────────────────┐
    │ [Recursos 24,24]          [Título da sala ±280 × 16..64]                   [FPS debug] │
    │  ◆ 1234 (+5)              Calabouços · Sala 1/6 · 5/20                                  │
    │                           [Barra do chefe 0.25..0.75 × 16..84 — substitui o título]     │
    │                           [Alerta de wave 520×64, y132]                                 │
    │                           [Toasts y132/204+, até 3]                                     │
    │                                                                                         │
    │                    ( zona de combate protegida 0.18..0.82 × 0.16..0.80 )                │
    │                                                                                         │
    │                                                                                         │
    │ [Painel do jogador 24 / −24, ~404 × auto]  [Barra de bênçãos ±246, −24]   [Log debug]  │
y900└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Interação, pausa e foco

### 5.1 Regras

1. **Toda tela modal entra pela mesma API** (`UiModalStack`, §5.2): pausa, ofertas, compêndio, game over, ficha, Fonte, baú, bancada, popup de skill, confirmação e o **altar da Câmara** (hoje fora). Ninguém mais escreve `GetTree().Paused` direto.
2. **Comportamento de pausa preservado** (só a API muda): pausam = Pausa, Ficha, Compêndio, Oferta de artefato, Oferta de bênção, Game over, Altar, Confirmação (herda da tela abaixo). Não pausam (só bloqueiam input) = Fonte, Baú, Bancada, popup de skill. A pausa passa a incluir sempre o áudio (`CallGroup("audio_manager","SetPaused", bool)`), que hoje falta em ofertas, compêndio, game over e altar.
3. **Foco inicial explícito** em toda tela (a ação primária); `FocusFirst` só como fallback.
4. **Retorno de foco**: ao fechar, o foco volta ao controle que estava focado quando a tela abriu (se ainda válido e visível); senão, `FocusFirst` da nova camada do topo.
5. **`ui_cancel` fecha a camada do topo**; Esc/B. Exceções: oferta obrigatória (mostra aviso no status e refoca) e game over (ignora).
6. **Ordem de desenho = ordem de fechamento**: `Push` faz `MoveToFront()` na camada; o topo da pilha é sempre o que está desenhado por cima. Game over limpa a pilha (fecha todas) e entra no topo.
7. **Ações destrutivas sempre com `Confirm`** (§3.3).
8. **Nenhuma informação só por mouse**: tooltips repetem o painel de detalhes; zoom/pan têm equivalente no controle; clique direito vira atalho opcional.
9. **Cursor some com controle** e volta ao mover o mouse (§5.5).
10. **Refresh sem perder foco:** ao recriar itens, use `RemoveChild` + `QueueFree` (nunca `FocusFirst` após só `QueueFree` no mesmo frame, Inv §7.3) e refoque o item equivalente (mesmo ID) ou o mesmo índice.
11. **Navegação em grade**: árvore de skills e cards definem `FocusNeighbor*` explícitos (fundação T2 oferece helper).

### 5.2 API `UiModalStack` (fundação T2, em `scripts/ui/ControllerUiSupport.cs`)

```csharp
public static class UiModalStack
{
    // Abre uma camada: MoveToFront, guarda o foco atual para retorno, aplica foco inicial, recalcula pausa.
    public static void Push(Control layer, bool pauses, Control initialFocus = null, Action onCancel = null, bool mandatory = false);
    // Fecha a camada (não precisa ser o topo), restaura foco, recalcula pausa.
    public static void Pop(Control layer);
    // ui_cancel: executa onCancel do topo (padrão: Pop). Retorna false se a pilha estiver vazia.
    public static bool CancelTop();
    public static bool IsOpen(Control layer);
    public static bool AnyOpen { get; }      // usado por HUD.IsGameplayInputBlocked
    public static bool AnyPausing { get; }
    public static void Clear();              // game over / troca de cena
}
```

- Pausa: `tree.Paused = AnyPausing` + `CallGroup("audio_manager","SetPaused", AnyPausing)` em todo Push/Pop. Limpar a pilha quando a camada sair da árvore (`TreeExiting`) para não vazar entre cenas.
- Compatibilidade (não renomear): `HUD.SetModalPause`, `HUD.RefreshModalPause` e `HUD.CloseTopmostOverlay` continuam existindo (hud-menus T2) e passam a delegar: `CloseTopmostOverlay` → `UiModalStack.CancelTop()` primeiro e depois a cadeia legada (enquanto houver telas não migradas); `RefreshModalPause` → recalcula a partir da pilha **e** dos flags legados.
- `HUD.IsGameplayInputBlocked()` (assinatura intacta) passa a retornar `UiModalStack.AnyOpen || <checagens atuais>`, cobrindo altar e overlay de absorção. Edição em `HUD.cs` feita junto de fundação T3 (dona de `HUD.cs` na fase 2).
- O altar deixa de tratar `ui_cancel` no `IntermissionChamber._Input`: registra `onCancel = CloseUpgradeOverlay` no Push e o `HUD._Input` resolve.

### 5.3 Foco (fundação T2)

- `ControllerUiSupport.FocusFirst` mantido (fallback). Novos: `FocusInitial(Control preferred, Control fallbackRoot)`, `SetGridNeighbors(IList<Control> items, int columns, bool wrap)`, `SetVerticalCycle(IList<Control> items)` (generaliza o ciclo de bênçãos de `HUD.cs:299-306`).
- Indicador de foco: anel do tema (§3); cards espelham (§3.4).

### 5.4 Novas ações de input (fundação T2, `EnsureMappings`)

| Ação | Joypad | Teclado |
|---|---|---|
| `ui_alt` | botão 3 (Y/△) | R |
| `ui_tab_prev` / `ui_tab_next` | botões 9 / 10 (LB/RB, L1/R1) | Q / E |
| `ui_zoom_out` / `ui_zoom_in` | eixos 4 / 5 (LT/RT, L2/R2) | − / = |
| `ui_scroll_up/down/left/right` | eixos 3 / 2 (analógico direito) | — |

Essas ações só são lidas pela UI (com a tela aberta); não conflitam com combate porque o input do jogador está bloqueado. Abas só existem em telas que pausam (§3.8).

### 5.5 Cursor e dispositivo (fundação T2, `GameCursor.cs`)

- `GameCursor` ganha `public static InputDevice LastDevice` (`KeyboardMouse`, `Xbox`, `PlayStation`) e sinal `DeviceChanged`.
- Em `_Input`: botão de joypad ou eixo > 0,3 → `LastDevice` = PlayStation se `Input.GetJoyName(device)` contiver "PS", "DualSense", "DualShock", "PlayStation" ou "Sony", senão Xbox; e `Input.MouseMode = Hidden`. Movimento do mouse > 2 px ou clique → `KeyboardMouse` e `MouseMode = Visible`. Teclado → `KeyboardMouse` sem mexer no cursor.
- Cursor 64×64 atual mantido (sem asset novo); redução para 32–48 fica para quando houver variante registrada.

---

## 6. Feedback de tela

Substitui `_damageFlash` e `_criticalHealthVignette` (`ColorRect` cheios, `HUD.StatusBars.cs:102-117,399,454-470`) por **uma** vinheta de borda. Implementação: hud-ingame T1.

- Nó: `TextureRect` "EdgeVignette", tela cheia, `MouseFilter.Ignore`, `StretchMode.Scale`, `ExpandMode.IgnoreSize`, abaixo dos painéis do HUD e acima do mundo.
- Textura (sem shader, sem asset): `GradientTexture2D` 256×256, `Fill = Radial`, `FillFrom = (0.5, 0.5)`, `FillTo = (1.0, 0.5)`; `Gradient`: offset 0.00 → `(0.55, 0.00, 0.03, 0)`, offset **0.62** → `(0.55, 0.00, 0.03, 0)`, offset 1.00 → `(0.55, 0.00, 0.03, 1)`. Esticada em 16:9 vira elipse: **o miolo de ~62% da largura e da altura fica 100% transparente** (cobre a zona de combate protegida).
- Intensidade só por `Modulate.a` (cor fixa):
  - **Flash de dano:** sobe a 0,45 em `FlashRise` 0,03 s, cai a 0 (ou ao nível crítico) em `FlashFall` 0,12 s. `Kill()` o tween anterior. Junto: borda da barra de vida pisca (§3.5).
  - **Vida crítica (< 30%):** `a = 0,20 + 0,25 × severidade + 0,10 × pulso`, `severidade = 1 − vida/0,3·máx` (0..1), `pulso = 0,5 + 0,5·sin(2π·1,2·t)`; teto 0,55. Calcular em `_Process` só quando o valor muda de faixa ou com vida crítica (sem alocação por frame).
  - Flash durante vida crítica: soma, com teto 0,70.
- Proibido qualquer `ColorRect` de tela cheia com cor durante o combate.

---

## 7. Ferramentas de desenvolvimento

- Tudo atrás de `OS.IsDebugBuild()`: parâmetros de gameplay (tuning), editores `SpinBox` e "Redefinir valores originais" do Dicionário, caminhos de arquivo/IDs internos, cheat de moedas, "Reiniciar teste", FPS, log de eventos, linha "Dano tuning" da ficha.
- Separação visual: seção própria com cabeçalho **"Ferramentas de desenvolvimento"** (`Section 22` em `TextSecondary`), borda tracejada 1 px `TextSecondary` a 50% e selo `Badge` "DEV" (`PanelInset`). Nunca dourado nem destaque de ação primária.
- Na pausa: botão "Ferramentas de desenvolvimento" após divisor, abaixo das ações do jogador e acima das destrutivas. Dentro: "Painel de dev (F1)" e "Reiniciar teste" (os ajustes de gameplay da pausa foram para o grupo "Ajustes rápidos" do F1).
- Nos overlays: botão de cheat no rodapé, à direita, `Small`, com selo DEV.

---

## 8. Especificação por tela

Formato: **Prioridade** (P0 = bloqueia leitura de combate, controle ou perde progresso; P1 = usabilidade/consistência; P2 = polimento) · **Tarefa** · **Componentes** · **Muda** · **Bugs junto** · **Previews**. Os textos exibidos vivem em `data/localization/<idioma>/{ui,content,world}.json` (fase 4 concluída; ver Anexo D). Os nomes literais entre aspas nas especificações abaixo são o texto em pt-BR. O Anexo C é a proposta original de nomes de chave; vale o que está nos arquivos de localização.

### 8.0 Fundação (componentes, foco, preview)

- **Tarefa:** fundação T1 (§2, §3 em `UIThemeFactory.cs`/`UiComponents.cs`), T2 (§5 em `ControllerUiSupport.cs`/`GameCursor.cs`), T3 (`UiPreview.cs`, `HUD.cs`, `MainMenu.cs`).
- **Bugs junto:** `BlessingRarityColor` duplicado → `UiPalette.Rarity` (as cópias em `HUD.BlessingOffer.cs:384` e `IntermissionChamber.cs:257` passam a delegar; removidas em 3c T3/T5); cursor visível no controle (§5.5); foco não restaurado (API §5.2); `IsGameplayInputBlocked` sem altar/absorção (§5.2).
- **Previews:** `gallery` (todos os componentes e estados) e `confirm` foram previstos mas **não existem** em `UiPreview.cs`; os componentes são conferidos pelas telas reais (Anexo A).

> **Limpeza da HUD (2026-10), vale sobre o texto abaixo:** saíram o nome do mapa no título da sala (fica só "Sala x/y · n/m", "Sala limpa"...), o banner "Sala limpa / Escolha sua recompensa", o alerta "Próxima onda" (`NotifyWaveIncoming` removido), o rótulo "Recompensa: …" ao lado dos medalhões das portas e dos itens no chão e os nomes flutuantes da Fonte, da Bancada, do Altar e da Loja (o prompt de interação continua). O contador de Moedas/Sangue Amaldiçoado fica no canto inferior esquerdo, ao lado do painel de vida (`BottomLeftHud`), e o aviso de Sangue Amaldiçoado sai logo acima dele. A barra de bênçãos virou uma coluna na borda esquerda, centrada na altura. Previews `room_clear` e `wave_warning` removidos.

### 8.1 HUD — painel do jogador (Aud §1) — **P1**

- **Tarefa:** hud-ingame T1 (`HUD.StatusBars.cs`).
- **Componentes:** `PanelContainer` com `PanelHud` (altura automática), `ResourceBar(Health)`, `ResourceBar(Fury)`.
- **Muda:** remove "VIRGIL"; âncora inferior esquerda (24, −24), largura ~404 (conteúdo 372 = barra 260 + valor 56 + barreira 40 + 2 × 8 de separação, mais padding de 16 de cada lado; é automática, não fixada); altura 64 sem fúria, ~92 com fúria; vida em `Life` (não mais `(0.86,0.02,0.04)`); chip de dano; "+N" de barreira em rótulo separado `Restore`; fúria fina com selo, estado "pronta" e glifo. Barreira integrada preservada.
- **Previews:** `hud_combat` (vida 83/100 + barreira 20 + fúria cheia), `critical_health`.

### 8.2 HUD — recursos (Aud §2) — **P1**

- **Tarefa:** hud-ingame T1; o texto de moedas hardcoded em `HUD.BloodFountain.cs:268` → hud-overlays T2 (usar `OnBloodCoinsChanged`).
- **Componentes:** `PanelHud`, `CoinGlyph(24)`, `HudValueLabel`.
- **Muda:** só moedas: glifo + número `Section 22` `Gold`; "+N" do canal (c) ao lado. Arma sai do HUD (fica na ficha e na bancada). Progresso de inimigos vai para o título da sala e some fora de salas de combate.
- **Previews:** `hud_combat`, `toast` (inclui "+N").

### 8.3 HUD — título da sala (Aud §3) — **P2**

- **Tarefa:** hud-ingame T1 (layout, divisor, texto pós-limpeza em `StatusBars.cs:523`) + T2 (`SyncRoomInformation`, `Notifications.cs:69`).
- **Muda:** `PanelHud` ±280 × 16..64 sem divisor sobreposto; nome do mapa `Section 22` `Gold` + "Sala 1/6 · 5/20" `Secondary`; ao limpar, a parte secundária vira "Sala limpa" e, quando o portal abre de fato (depois da recompensa, via `NotifyPortalOpened`), vira "Portal aberto" (o nome continua). Sala de chefe: o título some e a barra do chefe ocupa o lugar. Na Câmara de Hemomancia: "Câmara de Hemomancia" via `_roomLabel` (sem vida) — substitui o `_statusLabel` da Câmara (ver §8.19).
- **Previews:** `hud_combat`.

### 8.4 HUD — barra do chefe (Aud §4) — **P1**

- **Tarefa:** hud-ingame T1 (construção em `BuildLayout`) + T2 (`BindBoss`/`UpdateBossHealth`).
- **Componentes:** `ResourceBar(Boss)`.
- **Bugs junto:** `Split('—')` em `Notifications.cs:66` → nome e valor em rótulos separados (T2).
- **Previews:** `boss_bar`.

### 8.5 HUD — log, toasts, banner, aviso de wave (Aud §5) — **P0**

- **Tarefa:** hud-ingame T2 (`HUD.Notifications.cs`). **Contrato com T1:** a construção do log e do toast sai de `BuildLayout` (`StatusBars.cs:119-127, 227-243`) para `BuildNotificationsLayout()` em `HUD.Notifications.cs`; T1 chama esse método no mesmo ponto da ordem de criação. As duas tarefas mudam juntas na onda 1 (o revisor T5 confere).
- **Componentes:** `ToastStack` (3 canais), `EventLog` (debug), banner = `PanelHud` borda 2 `WindowBorder`, 560 × auto, no **slot do alerta** (âncora topo 0, y 132), título `Banner 34` `Gold` caixa alta + linha `Secondary` "Escolha sua recompensa" (o banner **não** afirma que o portal abriu).
- **Banner × alerta × toasts:** ao mostrar o banner, `UiToastStack.SetBannerSlot(true)` esconde o alerta de wave (mata o fade e a contagem) e desce a pilha de toasts para y 240; ao fim do banner, `SetBannerSlot(false)` devolve a pilha a y 132 (ou 204 se houver alerta). Nada cruza o banner.
- **Alerta de wave:** "Próxima onda" (`Section 22` `TextPrimary`) + linha "5 inimigos ·" (`Secondary`) com a contagem em rótulo próprio `TextPrimary` ("3").
- **"+N" de kill:** host = moldura do painel de recursos (`_resourceFrame`), fora da borda à direita; a posição é lida no quadro seguinte (depois do relayout), então o "+N" não encosta nos dígitos quando o contador ganha uma casa.
- **Portal:** limpar a sala → secundário do título "Sala limpa" + banner; a recompensa é escolhida → `RunManager.UnlockExitAfterReward()` abre o portal e chama `CallGroup("hud", "NotifyPortalOpened")` → secundário "Portal aberto" + toast info "O portal foi aberto" (idempotente). No chefe, isso só acontece depois de pegar o artefato.
- **Muda:** §3.9 e §3.10; o banner anima com `PivotOffset` no centro (hoje escala a partir do canto). Mantém as assinaturas `AppendGameLog`, `NotifyEnemyDefeated`, `NotifyWaveIncoming`, `NotifyRoomCleared`, `NotifySkillMessage`, `NotifyArtifactCollected`, `NotifyBloodCoinsRewarded`, `SyncBloodCoins`, `SyncEnemyProgress`; novo via `CallGroup`: `NotifyPortalOpened` (hud-ingame T8).
- **Bugs junto:** banner sem pivô; `await` do banner sem `IsInstanceValid`.
- **Previews:** `toast` (3 recompensas em fila + "+N"), `event_log`.

### 8.6 HUD — barra de bênçãos (Aud §6) — **P0** (controle) / P1 (visual)

- **Tarefa:** hud-ingame T3 (`HUD.BlessingBar.cs`).
- **Componentes:** `BlessingIcon(44, showLevel)`; tooltip do tema.
- **Muda:** slot em StyleBox com borda 2 na cor da raridade (fim do `simple_panel` esticado); selo de nível; excedente "+3" no mesmo formato de slot. A barra **não é focável**: o acesso por controle é a aba/coluna "Bênçãos" da ficha (§8.13) — a dica "[View/Create] Ficha" aparece ao lado da barra quando houver ≥ 1 bênção. Tooltip só repete o que a ficha mostra.
- **Previews:** `blessing_bar` (8 bênçãos fictícias, raridades variadas, níveis 1–3).

### 8.7 HUD — vida crítica, flash e número de dano (Aud §7) — **P0**

- **Tarefa:** hud-ingame T1.
- **Componentes:** vinheta §6, `DamageNumber` §3.14.
- **Bugs junto:** número de dano fora do tema (`StatusBars.cs:414`); tweens de flash concorrentes (`StatusBars.cs:400`, `Kill()` antes).
- **Previews:** `damage_flash`, `critical_health`, `damage_number` (3 hits seguidos).

### 8.8 Rótulos 3D e prompts de interação (Aud §8) — **P1** — *sem tarefa nas fases 3a–3c*

- **Arquivos:** `ArtifactChest.cs`, `BloodFountain.cs`, `WeaponBench.cs`, `ScenePortal.cs`, `RoomArtifact.cs`, `RoomGenerator.cs`, `RunManager.cs`, `.tscn` das salas. **Pendência:** criar tarefa (`designer-ux-arte`) após a fase 3a; a fase 4 só localiza `scripts/ui` e `IntermissionChamber.cs`.
- **Especificação:** formato único "[glifo] Verbo objeto" (ex. "[E] Abrir baú"), glifo pelo último dispositivo; prompts em `TextPrimary`, títulos de objeto/lugar em `Gold`, vermelho só para alerta; títulos com fonte 48–56 (ou `pixel_size` 0,012) e outline 10–12; "Entrada" removido ou elevado. Idealmente prompt 2D do HUD no centro-baixo (y −150), `PanelHud`, `Body`.
- **Previews:** `prompt` (quando houver a tarefa).

### 8.9 Menu principal (Aud §9) — **P0** (confirmação) / P1

- **Tarefa:** hud-menus T1 (`MainMenu.cs`).
- **Componentes:** `MenuButton(Large)`, `Confirm`, `OptionsPanel(showLanguage: true)`, `ScrollList` focável (créditos), `HintBar`, `TitleLabel`.
- **Muda:** identidade preservada (§9). Coluna de páginas 440 de largura, começando em y≈330; título de página `Title 28` `Gold` com divisor; slots como linha `Large` com resumo `Secondary` ("1234 moedas · data"); "Excluir" `Destructive` `Small` → `Confirm` ("Excluir Slot 2?", Cancelar em foco). Ao voltar de uma página, foco no botão que a abriu. Rodapé: dicas à esquerda, versão do jogo à direita (`Secondary`). Emblema 96 px a alpha 0.8.
- **Bugs junto:** excluir sem confirmação (`MainMenu.cs:134`); créditos não rolam por controle; retorno de foco.
- **Previews:** `menu_main`, `menu_slots`, `menu_confirm_delete`, `menu_options`, `menu_credits`.

### 8.10 Pausa, opções e parâmetros (Aud §10) — **P0** (confirmação, dev) / P1

- **Tarefa:** hud-menus T2 (`HUD.Pause.cs`).
- **Componentes:** `Window(Medium)` "Pausa", `MenuButton`, `Confirm`, `OptionsPanel(showLanguage: true)` (a pausa tem o seletor de idioma; o HUD reage a `LanguageChanged`), seção DEV (§7), `LabeledSlider`.
- **Muda:** ordem: Continuar (foco) · Opções · Dicionário · *[divisor]* · Ferramentas de desenvolvimento (debug) · Reiniciar teste (debug) · *[divisor]* · Desistir da run (`Destructive`, só com run ativa) · Voltar ao menu principal (`Destructive` se run ativa, senão `Secondary`). Título e log do HUD escondidos enquanto a pausa estiver aberta. Tuning: rodapé só "Salvar" e "Voltar". Subpáginas voltam com foco no botão que as abriu.
- **Bugs junto:** sliders presos em 100 (`HUD.Pause.cs:351`: alinhar o clamp ao `MaxValue` de cada slider); pausa inconsistente (wrappers §5.2); game over abaixo de outros overlays (§5.1 regra 6).
- **Previews:** `pause`, `options`, `options_audio`, `options_game`, `options_video_end`, `options_confirm`, `pause_confirm_abandon`, `pause_confirm_menu`.

### 8.11 Game over (Aud §11) — **P1**

- **Tarefa:** hud-menus T2.
- **Componentes:** `Window(Small)` larga 640, `DisplayLabel` (`Display 54`, `GoldBright`, sombra `DecorCrimson`), `MenuButton`.
- **Muda:** causa e "Último golpe: N" em duas linhas `Body` (autowrap 560); resumo de 3–4 linhas `Secondary` com dados já disponíveis em `RunState` (mapa/sala alcançados, bênçãos, moedas) — se algum dado não existir, omitir a linha (sem criar estado novo); botões [Voltar ao Ninho] (foco, `Primary`) e [Menu principal] (`Secondary`; a run já terminou, sem confirmação). HUD de combate escondido. `UiModalStack.Clear()` e push no topo.
- **Previews:** `game_over`.

### 8.12 Dicionário / compêndio (Aud §12) — **P1**

- **Tarefa:** hud-menus T3 (`HUD.Compendium.cs`).
- **Componentes:** `Window(Full)`, `Tabs`, `ScrollList`, `ChoiceCard` em modo leitura (sem botão de ação) ou linha de lista + `DetailPanel(Right)`, seção DEV (§7).
- **Muda:** release mostra leitura (descrição, flavor, efeitos via `FriendlyName`, raridade colorida com selo) sem IDs, caminhos e `SpinBox`; editores e "Redefinir" só em debug, dentro da seção DEV de cada entrada. Aba ativa pelo componente `Tabs` (fim do `Modulate`). Placeholder de ícone = monograma (§3.4). Fechar devolve o foco ao botão "Dicionário" da pausa. Conteúdo do Codex no release: ver decisão em aberto D2.
- **Bugs junto:** foco não restaurado; pausa direta ao abrir (`Compendium.cs:133`) → `UiModalStack`; refresh a cada passo do `SpinBox` (debounce de 0,2 s, só debug).
- **Previews:** `compendium` com `--ui-tab=artifacts|blessings|skills|attacks|enemies`.

### 8.13 Ficha do personagem (Aud §13) — **P1** (P0 para o acesso às bênçãos por controle)

- **Tarefa:** hud-overlays T1 (`HUD.CharacterSheet.cs`).
- **Atributos só em debug:** em release a aba Atributos não é criada (os atributos continuam computados, só ocultos); o preview `character_sheet_Attributes` do lote exige build de debug.
- **Componentes:** `Window(Full)`, `Tabs` (Atributos · Bênçãos · Artefatos · Habilidades), `ScrollList`, linhas `Flat` focáveis, `BlessingIcon(48)`, `DetailPanel(Right)`, `HintBar` ("[○] Fechar").
- **Muda:** abas em vez de 4 colunas fixas (resolve estouro e scroll); cada linha focável alimenta o painel de detalhes (descrição completa da bênção/artefato/skill); raridade localizada com selo; efeitos via `ArtifactEffectRegistry.FriendlyName` + `FormatArtifactOperation`; "Artefatos da run" e "Impacto" fundidos; "Dano tuning" só em debug. Abre na aba Bênçãos se houver bênçãos, senão Atributos.
- **Bugs junto:** `RefreshCharacterSheet` com a ficha fechada (só marcar "sujo" e reconstruir ao abrir); ficha aberta atrás de outros overlays (`Push` faz `MoveToFront`).
- **Previews:** `character_sheet` (+ `--ui-tab=`).

### 8.14 Fonte de Sangue e detalhes de skill (Aud §14) — **P0** (controle) / P1

- **Tarefa:** hud-overlays T2 (`HUD.BloodFountain.cs`).
- **Componentes:** `Window(Full)` com `HeaderRight` = moedas (`CoinGlyph` + número), dois painéis de árvore (cabeçalho Bestialis em `DecorCrimson`/texto `TextPrimary`; Humanitas em `Gold`), nós em StyleBox por estado (§2.1), `DetailPanel(Bottom)`, `HintBar` ("[○] Fechar · [L2/R2] Zoom · [✕] Comprar").
- **Muda:** detalhes seguem o foco (substituem o popup por clique direito; o clique direito continua como atalho e abre o mesmo painel); nó com sigla `Section 22` `Gold`, selo rank e custo; coluna de 196 (rótulo de nome com 2 linhas, largura 184); links acesos; foco move o pan até o nó; zoom em `ui_zoom_*`, pan no analógico direito; layout recalculado em `SizeChanged`. Foco inicial: primeiro nó disponível/comprável. Cheat de moedas no rodapé (debug, §7). Não pausa (comportamento atual).
- **Bugs junto:** Fonte só por mouse; foco de vizinhança geométrica (`SetGridNeighbors`); moedas hardcoded `:268`; status de 2 linhas invadindo o cheat; popup sem foco/retorno.
- **Previews:** `blood_fountain`, `skill_detail` (foco num nó bloqueado e num comprável).

### 8.15 Baú de artefatos (Aud §15) — **P0** (bug) / P1

> Histórico: o Baú de artefatos (`HUD.ArtifactChest.cs`) não existe mais no código. Artefatos agora só são comprados na Loja da Câmara (`HUD.Shop.cs`, preview `shop`).

- **Tarefa:** hud-overlays T4 (`HUD.ArtifactChest.cs`).
- **Componentes:** `Window(Large)`; coluna esquerda (260) com slots como cards pequenos (`IconM`, nome ou "Vazio", `Badge` do slot) usando estado **selecionado** (§3); coluna direita `ScrollList` de linhas de coleção (ícone 48/monograma, nome em `Rarity`, selo de origem "Run"/"Coleção", efeitos `Secondary`); `DetailPanel(Bottom)` opcional; `HintBar` ("[○] Fechar · [✕] Equipar").
- **Muda:** foco começa no slot 1; ✕ no slot seleciona e leva o foco à coleção; ✕ num item equipa no slot selecionado e devolve o foco ao slot.
- **Bugs junto:** slots invisíveis/equipar sempre no slot 1 (`HUD.ArtifactChest.cs:56,66`); foco perdido após equipar (refocar por ID, §5.1 regra 10); seleção por `Modulate` (`:170`).
- **Previews:** `artifact_chest`.

### 8.16 Oferta de artefatos (Aud §16) — **P1**

> Histórico: a oferta de artefatos do boss (`HUD.ArtifactOffer.cs`) não existe mais. A Loja usa o mesmo `ChoiceCard` em `Window(Large)`.

- **Tarefa:** hud-overlays T4 (`HUD.ArtifactOffer.cs`).
- **Componentes:** `Window(Large)` "Tesouro de Artefatos", `ChoiceCard` ×3–4 (meta = slot localizado; corpo = efeitos via `FriendlyName`), `ScrollList`, `HintBar`.
- **Muda:** igual à oferta de bênção; "Trocar oferta" sai do topo e vira dica `ui_alt` "[△] Trocar (N)" no rodapé, **oculta** sem a skill "Espelho do Destino"; com 4 opções, a lista rola. `ui_cancel` mantém o comportamento atual (fecha sem escolher, ver D1) e o rodapé mostra "[○] Decidir depois".
- **Bugs junto:** pausa direta (`:17,:96,:159`) → `UiModalStack`; "Trocar" sobreposto ao status; sem refoco após reroll; "Trocar" inalcançável por d-pad.
- **Previews:** `artifact_offer`, `artifact_offer_4`.

### 8.17 Oferta de bênçãos e absorção (Aud §17) — **P1**

- **Tarefa:** hud-overlays T3 (`HUD.BlessingOffer.cs`).
- **Componentes:** `Window(Large, Mandatory)` "Hereditas Sanguinis", `ChoiceCard` (meta = linhagem · ataque afetado; selo de raridade localizado; aviso "Substitui: X" inline), `HintBar` ("[△] Trocar (N) · [✕] Escolher", sem "Voltar").
- **Muda:** card inteiro reage ao foco; "Trocar oferta" via `ui_alt` (oculto sem a skill); `ui_cancel` mantém o aviso no status; glossário dos termos presentes nos cards numa linha `Secondary` acima do rodapé (o `[hint]` passa a ser redundante). Título "Sanguis Purgatus" em `Banner 34` `Gold` (hoje vermelho de perigo); animação de absorção preservada. Usa `UiPalette.Rarity` e remove `BlessingRarityColor` local (ou o deixa delegando).
- **Bugs junto:** `await` da absorção sem checagem (`:87-91`: após cada `await`, `if (!IsInstanceValid(this) || !IsInsideTree()) return;`; guarda de reentrada `_absorptionRunning`); pausa direta (`:28,:300,:306,:410`) → `UiModalStack`; "Trocar oferta" inalcançável.
- **Previews:** `blessing_offer`, `blessing_offer_replace` (um card com substituição).

### 8.18 Bancada de armas (Aud §18) — **P1**

- **Tarefa:** hud-overlays T4 (`HUD.WeaponBench.cs`).
- **Componentes:** `Window(Large)` com corpo de 880, `ChoiceCard` sem raridade (borda `Border`; ícone = monograma) com ação "Equipar"; card da arma atual com selo "EQUIPADA" e borda `Gold`, botão desabilitado ("Equipada"); armas placeholder com selo "EM BREVE" no estado desabilitado; `HintBar`.
- **Muda:** nome em `Section`, descrição em autowrap (fim da linha de ~1 000 px). Foco inicial na arma equipada. Não pausa.
- **Previews:** `weapon_bench`.

### 8.19 Câmara de Hemomancia — altar e status (Aud §19) — **P1**

- **Tarefa:** hud-overlays T5 (`IntermissionChamber.cs`); título via HUD → hud-ingame T1 (§8.3).
- **Componentes:** `Window(Large)` "Altar do Sacrifício" (dentro do próprio CanvasLayer da Câmara, com o tema), `ChoiceCard` com `PreviewBbcode` "atual → próximo" e `CostText` "−5% vida máx." em `Warn`; `HintBar` ("[○] Voltar · [✕] Aprimorar").
- **Muda:** remove o `_statusLabel` sobre a moldura do HUD; mensagens de fonte/altar viram toast (b) via `CallGroup("hud","NotifySkillMessage", texto)` (método existente, sem novo contrato); cura da fonte com o valor real no prompt (não o "35%" fixo de `:398`). Remove a cópia de `BlessingRarityColor` e do layout de card.
- **Bugs junto:** pausa direta e altar fora de `RefreshModalPause`/`IsGameplayInputBlocked` → `UiModalStack.Push(_upgradeOverlay, pauses: true, onCancel: CloseUpgradeOverlay)`; `ui_cancel` paralelo em `_Input` removido; foco não devolvido. Tornar `OpenUpgradeOverlay` público para o preview.
- **Previews:** `altar` (cena `IntermissionChamber`; `UiPreview` chama `OpenUpgradeOverlay` se existir).

### 8.20 Cursor e navegação por controle (Aud §20) — **P0**

- **Tarefa:** fundação T2 (API, ações, cursor); cada tarefa de tela declara foco inicial e usa `HintBar`.
- **Muda:** §5. O `_controlsHintLabel` morto (`StatusBars.cs:199-211`) é removido em hud-ingame T1.
- **Previews:** qualquer tela com `--ui-device=kbm|xbox|ps` (não há `gallery`; ver Anexo A).

### 8.21 Resoluções (Aud §21) — **P1**

- **Tarefa:** verificação em hud-ingame T4, hud-menus T4, hud-overlays T6 e localização T3 (janela real em 1280×720 e 1920×1080, além das capturas em 1600×900).
- **Critério:** nenhum texto < 14 lógicos; nenhuma janela com offset absoluto de canto; janelas `Large` cabem em 1600×900 com ≥ 180 de margem lateral.

---

## 9. Não mexer

- **Métodos chamados por string via `CallGroup("hud", ...)`** — nomes e assinaturas intactos (Inv §4): `AppendGameLog(string)`, `SyncEnemyProgress(int,int)`, `NotifyEnemyDefeated(int,int,int)`, `NotifyBloodCoinsRewarded(int,int)`, `SyncBloodCoins(int)`, `NotifyRoomCleared()`, `NotifyWaveIncoming(int,float)`, `BindBoss(Node,string)`, `OpenBloodFountain()`, `NotifySkillMessage(string)`, `OpenArtifactChest()`, `OpenArtifactOffer(RoomArtifact)`, `OpenBloodBlessingOffer()`, `RefreshBlessingDisplays()`, `OpenWeaponBench()`.
- **Chamadas diretas:** `IsGameplayInputBlocked()` e `OpenMandatoryBloodBlessingOffer()`. Manter também público `NotifyArtifactCollected(string)`.
- **Contratos consumidos pelo HUD:** `CallGroup("run_manager","UnlockExitAfterReward"|"AddBloodCoins")`, `CallGroup("player","ApplyRuntimeTuning")`, `CallGroup("audio_manager","SetPaused"|"PlayEffect"|"PlayGameOverMusic")`; nomes de nó `Health`, `PlayerCombat`, `BloodBlessingRuntime`, `PlayerSkillController`; chaves de `CompendiumTuning` e IDs de aba do compêndio (Inv §2.12).
- **Métodos de pausa** `SetModalPause`, `RefreshModalPause`, `CloseTopmostOverlay` (passam a delegar, §5.2).
- **Regras de jogo:** dano, custos, ranks, exclusividade, reroll, chance de 4ª opção, cura, sacrifício de vida, recompensas. Só apresentação.
- **Fluxo de pausa:** quem pausa e quem não pausa (§5.1 regra 2); oferta obrigatória de bênção não fecha com Esc e mostra aviso; ciclo do d-pad entre cards com wrap.
- **Identidade:** fundo preto com glow radial carmesim, título dourado 54 com sombra carmesim, subtítulo, divisor (`MainMenu.cs:35-58,71-88`); card com borda de raridade, ícone 92, título 22 e ação à direita; prévia "atual → próximo" do altar; descrição de bênção com números calculados e cores de palavra-chave; barreira azul integrada à vida; animação "Sanguis Purgatus"; indicadores de status nos inimigos, "!" de alerta e telegraphs no chão.
- **Debug fora do release:** FPS, cheat de moedas, "Reiniciar teste" (já corretos).
- **Assets:** nenhuma arte nova gerada. Usar `assets/ui/kenney_hud/*` (registrado), ícones já usados em `images/bloodBoons/*`, `images/effects/*`, `images/generic.png` e desenho por código/StyleBox. **`images/inventory/*` não pode ser usado** (origem não registrada).

---

## 10. Decisões do usuário (2026-10-04)

| # | Pergunta | Decisão |
|---|---|---|
| D1 | A oferta de artefatos é obrigatória? | **Sim.** O jogador precisa escolher um artefato: Esc/Círculo/B **não** fecham a oferta, não há "Decidir depois" e o fechamento/reabertura não pode re-sortear opções (acaba o reroll grátis). Na `UiModalStack` a oferta entra como modal **não cancelável**; o reroll pago (skill) continua. Substitui o §8.16 onde ele falar em "Decidir depois". |
| D2 | O que o jogador vê no Codex/Dicionário no release? | **Diário de descobertas.** Cada coisa nova que o jogador encontra (artefatos, bênçãos, skills, inimigos, bosses, ataques) é registrada no save do slot e passa a aparecer no Codex; o que não foi descoberto aparece como entrada bloqueada ("???"). Em **build de debug**, tudo aparece desbloqueado, com IDs e editores, para conferir o conteúdo. |
| D3 | Cor de "Mythic". | `RarityMythic` marfim `(0.94,0.90,0.78)` com selo "Mítico". |
| D4 | Log de eventos no release. | **Não aparece no release**; só em build de debug. |
| D5 | Textos, termos e acentos. | **Todo texto exibido vem de um recurso de localização por idioma** (um conjunto de arquivos por idioma: pt-BR, en, es), inclusive nomes/descrições de bênçãos, artefatos, skills, bosses, mapas e armas. No código ficam só chaves. Erros de tradução e acentuação são corrigidos na migração (fase 4). |

Fonte, Baú e Bancada continuam sem pausar (comportamento atual). Fazê-los pausar não traria risco no hub, mas mudaria o fluxo e por isso fica fora desta reforma.

---

## Anexo A — Previews `--ui-preview=<nome>` (fundação T3)

Implementação: `scripts/ui/UiPreview.cs` (o cabeçalho do arquivo é a fonte da lista de telas). Só em `OS.IsDebugBuild()`; em release, nada acontece. Os argumentos de usuário vêm depois de `--`.

**Uma tela:**

```
F:/GODOT/App/Godot.exe --path . res://scenes/game/CrowsNest.tscn -- --ui-preview=blood_fountain
F:/GODOT/App/Godot.exe --path . --resolution 1600x900 --fixed-fps 30 --write-movie <dir>/f.png --quit-after 60 <cena> -- --ui-preview=<nome> [--ui-device=kbm|xbox|ps] [--ui-lang=pt-BR|en|es] [--ui-tab=<aba>] [--ui-codex-locked[=sample]]
```

- `--ui-lang` define o idioma da sessão antes de montar a tela, sem gravar nas configurações.
- `--ui-device` força os glifos e o cursor e fica travado na sessão (`kbm`, `xbox`, `ps`; `keyboard` e `playstation` também valem).
- `--ui-tab` escolhe a aba do compêndio (`Artifacts`, `Blessings`, `Skills`, `Attacks`, `Enemies`, `Weapons`) ou da ficha (`Attributes`, `Blessings`, `Artifacts`, `Skills`).
- `--ui-codex-locked` mostra o Dicionário na visão de release (entradas bloqueadas); `=sample` finge algumas descobertas.

**Lote (um único processo do Godot):** percorre todas as telas × idiomas, recarregando a cena entre uma captura e outra, e grava um PNG por combinação. Evita abrir e fechar o jogo uma vez por tela e não usa `--write-movie`.

```
F:/GODOT/App/Godot.exe --path . --resolution 1600x900 -- --ui-batch --ui-shots=C:/tmp/shots [--ui-langs=pt-BR,en,es] [--ui-screens=pause,compendium]
```

- `--ui-batch` ativa o lote (a cena inicial pode ser qualquer uma; o lote escolhe a cena de cada tela).
- `--ui-shots=<pasta>` é onde gravar (`<tela>[_<aba>]_<idioma>.png`); sem ela, `user://ui_shots`.
- `--ui-langs=a,b,c` limita os idiomas (padrão `pt-BR,en,es`); `--ui-screens=a,b` limita as telas (padrão: todas).
- Cada captura espera cerca de 0,8 s depois de a tela abrir; uma tela que não abre em 6 s é ignorada com aviso. Ao fim, o processo encerra sozinho.

Apelidos: `skill_detail` = `skill_popup`. Toast, flash de dano e número de dano se repetem para aparecerem em qualquer quadro da captura. `event_log` só aparece em build de debug.

| Nome | Cena | Seção |
|---|---|---|
| `menu_main`, `menu_slots`, `menu_confirm_delete`, `menu_options`, `menu_options_audio`, `menu_options_game`, `menu_credits` | `MainMenu` | 8.9 |
| `hud_combat`, `critical_health`, `damage_flash`, `damage_number`, `boss_bar`, `toast`, `blessing_bar`, `event_log` | `TestArena` | 8.1–8.7 |
| `pause`, `options`, `options_audio`, `options_game`, `options_video_end`, `options_confirm`, `pause_confirm_abandon`, `pause_confirm_menu`, `game_over`, `compendium` | `TestArena` | 8.10–8.12 |
| `dev_panel`, `dev_panel_actions` | `TestArena` | Painel de desenvolvedor F1 (`HUD.DevPanel.cs`, só debug): lista de parâmetros do `DevParameterRegistry` e aba "Ações de teste" |
| `character_sheet`, `blessing_offer`, `blessing_offer_replace` | `TestArena` | 8.13, 8.17 |
| `anvil_offer`, `anvil_offer_single` | `TestArena` | Oferta da Bigorna (`HUD.AnvilOffer.cs`; `docs/design/bigorna.md`): 3 cartas de modificação da arma equipada / 1 carta; obrigatória, sem "Voltar" |
| `blood_fountain`, `skill_popup`, `weapon_bench`, `hub` | `CrowsNest` | 8.14, 8.18 (`hub` = só o cenário) |
| `altar`, `shop` | `IntermissionChamber` | 8.19 (`shop` = Loja de Artefatos, `HUD.Shop.cs`) |
| `dialogue`, `dialogue_choice`, `hub_npc` | `CrowsNest` | Diálogo (`HUD.Dialogue.cs`; `dialogos-arquitetura.md` §7). `dialogue` = 1ª linha de Odalric já revelada; `dialogue_choice` = quadro de escolha com 2 opções; `hub_npc` = só o cenário, jogador diante de Odalric |
| `dialogue_bark` | `TestArena` | Legenda de bark de Virgil (não modal), repetida a cada 3 s |
| `dialogue_long`, `dialogue_choice_long` | `CrowsNest` | Estresse do diálogo: texto sintético de 180 caracteres (limite do validador) com palavras longas no idioma corrente, nome de falante longo; no quadro de escolha, 2 opções de 120 e 100 caracteres |
| `dialogue_bark_long` | `TestArena` | Estresse do bark: 90 caracteres + nome longo |

**Encaixe verificado (pt-BR, en, es; 9 capturas).** Linha de 180 caracteres: 3 linhas na caixa de 1100 px, sem crescer além do mínimo (220 px); opções de escolha: 2 linhas cada, botões inteiros e a caixa cresce para cima levando o retrato junto; bark de 90 caracteres: 2 linhas, painel acima da barra de vida sem cobri-la (barra termina em x≈523/1920, bark começa em x≈577). Nomes de falante usam `ClipText` + reticências. O layout é em unidades de `canvas_items`/`expand`, então 1280×720 reproduz o mesmo enquadramento (as capturas em lote saem em 1920×1080 nas duas resoluções). Pior caso real hoje: 105 caracteres (es), opções ≤ 21. Não foi preciso reduzir fonte nem rolar.

As telas do HUD abrem em qualquer cena com HUD (`CrowsNest`, `RunStartBlessingRoom`, `TestArena`, `IntermissionChamber`). `pause` e `pause_confirm_*` iniciam uma run volátil se não houver, para mostrar "Desistir da run". Os previews `gallery`, `confirm` e `prompt` previstos na fundação não foram implementados. Os previews `artifact_chest`, `artifact_offer` e `artifact_offer_4` deixaram de existir junto com o Baú e a Oferta de artefatos (as seções 8.15 e 8.16 acima são histórico).

**Lote padrão (`UiPreview.BatchScreens`):** 54 entradas (telas/abas) × 3 idiomas = 162 PNGs, com `--ui-batch` e sem `--ui-screens` (contagem conferida em `BatchScreens` e no lote real: 162 capturas). A fonte da lista é `UiPreview.BatchScreens`; a tabela acima descreve cada tela. Entram no lote, entre outras, as 7 de `MainMenu`, as 6 abas do compêndio, as 4 (5 em debug) abas da ficha, `anvil_offer`/`anvil_offer_single`, `hub_cast` (enquadra os 3 sarcófagos de personagens), as de diálogo, `dev_panel`/`dev_panel_actions` e `reward_doors` (jogador diante das portas de recompensa, em `IntermissionChamber`; não mostra a porta de Bigorna). Ficam fora do lote (só via `--ui-preview=`): `critical_health`, `damage_flash`, `damage_number` e `event_log`. A captura `character_sheet_Attributes` abre a aba Atributos, que só existe em build de debug.

**Decisões do diálogo (T4):** a conversa modal usa scrim de 0,35 em vez do scrim único de 0,85 (desvio consciente: o cenário 3D continua legível atrás da caixa), a caixa fica no terço inferior (1100 px, mín. 220) com retrato de 160 px que sobe 40 px acima dela, e a velocidade do texto é fixa em 45 caracteres/s (+0,15 s após `. ! ? …`), sem opção de ajuste. Confirmar completa a linha e, completa, avança; Esc/Círculo/B pula até a próxima escolha ou o fim; na escolha o cancelar não faz nada. O bark é uma legenda não modal no rodapé (dentro do `_combatHudLayer`), uma por vez. As chaves de preview vivem em `ui.json` (`ui.dialogue.preview.*`), não em `dialogue.json`.

**Sem preview:** os itens de recompensa no chão (`RewardPickup`, `DevilsFruit`). As portas com recompensa têm `reward_doors` e os `CharacterPlaceholder` do hub têm `hub_cast`.

Dados fictícios: 8 bênçãos (todas as raridades de bênção, níveis 1–3), 3 artefatos (Common/Epic/Mythic), 500 moedas, vida 83/100 + barreira 20, fúria cheia, chefe com 60% de vida.

## Anexo B — Bugs e riscos → tarefa

| Bug / risco (origem) | Tarefa |
|---|---|
| Sliders de tuning presos em 100 (`HUD.Pause.cs:351`) | hud-menus T2 |
| Slots do baú invisíveis, equipar sempre no slot 1 (`HUD.ArtifactChest.cs:56,66`) | hud-overlays T4 |
| Foco não restaurado (pausa→compêndio, subpáginas, baú após equipar, popup de skill, altar, reroll de artefato) | fundação T2 (API) + tarefa de cada tela |
| "Trocar oferta" inalcançável por d-pad | hud-overlays T3 (bênção), T4 (artefato) via `ui_alt` |
| Fonte só por mouse (detalhes, zoom, pan) | hud-overlays T2 |
| `await` da absorção sem checagem e sem guarda de reentrada (`HUD.BlessingOffer.cs:87-91`) | hud-overlays T3 |
| Pausa inconsistente (ofertas, compêndio, game over, altar; áudio não pausa) | fundação T2 (API) + T3 (`HUD.cs`); hud-menus T2/T3; hud-overlays T3/T4/T5 |
| Número de dano fora do tema (`HUD.StatusBars.cs:414`) | hud-ingame T1 |
| Cursor visível no controle | fundação T2 |
| `BlessingRarityColor` duplicado (`HUD.BlessingOffer.cs:384`, `IntermissionChamber.cs:257`) | fundação T1 (token) → hud-overlays T3/T5 (remover cópias) |
| Moedas hardcoded na Fonte (`HUD.BloodFountain.cs:268`) | hud-overlays T2 |
| `Split('—')` no nome do chefe (`HUD.Notifications.cs:66`) | hud-ingame T2 |
| Banner sem pivô / `await` sem checagem | hud-ingame T2 |
| Tweens de flash concorrentes | hud-ingame T1 |
| Game over abaixo de outros overlays; ficha atrás de ofertas/baú | `UiModalStack` (fundação T2) + hud-menus T2, hud-overlays T1 |
| `RefreshCharacterSheet` com a ficha fechada | hud-overlays T1 |
| Destaque de palavras-chave depende do texto em PT (`HUD.BlessingOffer.cs:486,498`) | localização T1; resolvido com a marcação `[kw=<id>]termo[/kw]` nas descrições (Anexo D) |
| **Risco novo:** `interact` (E / botão 0) é lido por polling em `ArtifactChest.cs:19`, `BloodFountain.cs:19`, `WeaponBench.cs:30` sem checar `IsGameplayInputBlocked`; como Fonte/Baú/Bancada não pausam, ✕/A dentro do overlay reaciona `Open*`. | Verificar com controle em hud-overlays T6; correção em `scripts/systems/*` não tem dono nas fases 3 (pendência) |

## Anexo C — Chaves de localização sugeridas (fase 4)

Prefixos: `ui.common.*` (confirm, cancel, back, close, choose, equip, upgrade, reroll, decide_later, dev_tools, coming_soon, equipped, max), `ui.confirm.*` (delete_slot.title/body, abandon_run.*, main_menu.*, irreversible), `ui.rarity.*` (common, uncommon, rare, epic, legendary, mythic, stirps), `hud.room.*` (title, room_progress, boss, portal_open, chamber), `hud.toast.*` (wave_title, wave_body, artifact, blessing), `hud.fury`, `hud.banner.portal_open`, `pause.*`, `gameover.*`, `sheet.*`, `fountain.*`, `chest.*`, `offer.artifact.*`, `offer.blessing.*`, `bench.*`, `altar.*`, `codex.*`, `hint.*`. Reaproveitar as existentes (`hud.*`, `menu.*`, `options.*`). Rótulos de rodapé devem caber em 160 px a 15 px nos três idiomas.

## Anexo D — Localização na prática (fase 4 concluída)

- **Onde:** `data/localization/<idioma>/{ui,content,world}.json` (idiomas `pt-BR`, `en`, `es`), JSON plano `"chave": "texto"`. `ui.json` = interface; `content.json` = bênçãos, artefatos, skills, armas e efeitos; `world.json` = bosses, mapas, inimigos e mensagens de combate. O `game_texts.json` antigo não existe mais. Estado: 905 chaves por idioma (375 + 467 + 63).
- **Carga e fallback:** `scripts/systems/Localization.cs`. Idioma atual → `pt-BR` → a própria chave (aviso em debug, uma vez por chave). `Localization.Text`, `TextFormat`, `Has` e o sinal `LanguageChanged`; tradução com formato malformado cai para o `pt-BR` e depois para o texto cru. A cultura da sessão segue o idioma (`en` com ponto decimal; `pt-BR` e `es` com vírgula).
- **Reação à troca de idioma:** o HUD, o menu, as salas e os rótulos 3D (fonte, baú, bancada, portais, artefatos no chão) assinam `LanguageChanged`. A pausa tem o seletor de idioma (`OptionsPanel(true)`).
- **Regra para tela nova:** todo texto visível usa chave (`Localization.Text`/`TextFormat`); texto só de debug (log de eventos, seção DEV, linhas "Prévia:" dos previews) pode ficar literal. Chave nova entra nos três idiomas; o rótulo precisa caber em `en` e `es` (~30% maiores).
- **Palavras-chave de bênção:** descrição com `{n}` (valor do efeito `n`) e `[kw=<id>]termo[/kw]`; termo e explicação em `blessing.keyword.<id>.term/.hint` (`BloodBlessingText`).
- **Validação:** `py tools/validate_localization.py` (paridade, placeholders, JSON válido, chaves literais do C# existem); os validadores de bênçãos e artefatos conferem as chaves de `content.json`. Termos de referência em `docs/design/glossario-localizacao.json`.
- **Idioma novo:** `Localization.Languages` e `NormalizeLanguage`, o seletor de `UiComponents.OptionsPanel` (três opções fixas) e a lista `LANGUAGES` dos três validadores.
