using Godot;

// Tokens de UI (docs/design/ui-guia.md §2). Telas não usam new Color(...) nem tamanho de fonte inline: usam estes tokens.
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
    public static readonly Color TextDisabled  = new Color(0.52f, 0.48f, 0.45f, 1.00f); // revisão T4: era 0.42/0.38/0.36 (pouco legível em 720p)
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

    // Aceita o id (en) e, por tolerância, o nome em português.
    public static Color Rarity(string rarity) => (rarity ?? "").Trim().ToLowerInvariant() switch
    {
        "uncommon" or "incomum" => RarityUncommon,
        "rare" or "raro" or "rara" => RarityRare,
        "epic" or "épico" or "épica" => RarityEpic,
        "legendary" or "lendário" or "lendária" => RarityLegendary,
        "mythic" or "mítico" or "mítica" => RarityMythic,
        "stirps" => RarityStirps,
        _ => RarityCommon
    };
}

// Tipografia (§2.3). Fonte padrão do Godot; nada abaixo de 14.
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

    // Outline padrão
    public const int OutlineLabel = 3, OutlineHud = 5, OutlineDamage = 6;
}

// Espaçamento, raios, bordas e tamanhos (§2.4).
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

// Tempos de animação (§2.6). Tweens de UI: Kill() no anterior e IsInstanceValid após await.
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
    public const float CardChooseFlash = 0.15f;
}

// Nomes de theme_type_variation definidos em UIThemeFactory.Create().
public static class UiVariation
{
    // Label
    public const string DisplayLabel = "DisplayLabel";       // Display 54, GoldBright, sombra DecorCrimson
    public const string BannerLabel = "BannerLabel";         // Banner 34, Gold
    public const string TitleLabel = "TitleLabel";           // Title 28, Gold
    public const string SectionLabel = "SectionLabel";       // Section 22, TextPrimary
    public const string SectionGoldLabel = "SectionGoldLabel"; // Section 22, Gold (cabeçalho de coluna, título de confirmação, chefe)
    public const string BodyLabel = "BodyLabel";             // Body 17, TextPrimary
    public const string SecondaryLabel = "SecondaryLabel";   // Secondary 15, TextSecondary
    public const string BadgeLabel = "BadgeLabel";           // Badge 14, sem outline
    public const string HudValueLabel = "HudValueLabel";     // Body 17, outline 5
    public const string WarnLabel = "WarnLabel";             // Secondary 15, Warn
    public const string DamageNumberLabel = "DamageNumberLabel"; // 30, DamageTaken, outline 6
    // Button
    public const string PrimaryButton = "PrimaryButton";
    public const string SecondaryButton = "SecondaryButton";
    public const string DestructiveButton = "DestructiveButton";
    public const string FlatButton = "FlatButton";
    public const string TabButton = "TabButton";             // toggle: ativa = barra inferior Gold
    public const string SelectButton = "SelectButton";       // toggle: slot/linha selecionada
    // PanelContainer
    public const string WindowPanel = "WindowPanel";
    public const string HudPanel = "HudPanel";
    public const string InsetPanel = "InsetPanel";
    public const string RaisedPanel = "RaisedPanel";
}

public static class UIThemeFactory
{
    public const string SimplePanelPath = "res://assets/ui/kenney_hud/simple_panel.png";
    public const string SimpleDividerPath = "res://assets/ui/kenney_hud/simple_divider.png";

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = UiFont.Body };

        // Textos
        theme.SetColor("font_color", "Label", UiPalette.TextPrimary);
        theme.SetConstant("outline_size", "Label", UiFont.OutlineLabel);
        theme.SetColor("font_outline_color", "Label", UiPalette.TextOutline);
        theme.SetColor("font_color", "RichTextLabel", UiPalette.TextPrimary);
        theme.SetColor("default_color", "RichTextLabel", UiPalette.TextPrimary);
        LabelVariation(theme, UiVariation.DisplayLabel, UiFont.Display, UiPalette.GoldBright, UiFont.OutlineLabel);
        theme.SetColor("font_shadow_color", UiVariation.DisplayLabel, new Color(UiPalette.DecorCrimson, 0.85f));
        theme.SetConstant("shadow_offset_x", UiVariation.DisplayLabel, 0);
        theme.SetConstant("shadow_offset_y", UiVariation.DisplayLabel, 4);
        theme.SetConstant("shadow_outline_size", UiVariation.DisplayLabel, 10);
        LabelVariation(theme, UiVariation.BannerLabel, UiFont.Banner, UiPalette.Gold, UiFont.OutlineHud);
        LabelVariation(theme, UiVariation.TitleLabel, UiFont.Title, UiPalette.Gold, UiFont.OutlineLabel);
        LabelVariation(theme, UiVariation.SectionLabel, UiFont.Section, UiPalette.TextPrimary, UiFont.OutlineLabel);
        LabelVariation(theme, UiVariation.SectionGoldLabel, UiFont.Section, UiPalette.Gold, UiFont.OutlineLabel);
        LabelVariation(theme, UiVariation.BodyLabel, UiFont.Body, UiPalette.TextPrimary, UiFont.OutlineLabel);
        LabelVariation(theme, UiVariation.SecondaryLabel, UiFont.Secondary, UiPalette.TextSecondary, UiFont.OutlineLabel);
        LabelVariation(theme, UiVariation.BadgeLabel, UiFont.Badge, UiPalette.TextPrimary, 0);
        LabelVariation(theme, UiVariation.HudValueLabel, UiFont.Body, UiPalette.TextPrimary, UiFont.OutlineHud);
        LabelVariation(theme, UiVariation.WarnLabel, UiFont.Secondary, UiPalette.Warn, UiFont.OutlineLabel);
        LabelVariation(theme, UiVariation.DamageNumberLabel, UiFont.DamageNumber, UiPalette.DamageTaken, UiFont.OutlineDamage);

        // Botões: estados padrão (§3)
        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
            ApplyButtonStates(theme, type);
        theme.SetConstant("h_separation", "Button", UiMetrics.S2);

        theme.SetTypeVariation(UiVariation.SecondaryButton, "Button");

        theme.SetTypeVariation(UiVariation.PrimaryButton, "Button");
        theme.SetStylebox("normal", UiVariation.PrimaryButton, ControlBox(UiPalette.Panel, new Color(UiPalette.Gold, 0.70f), UiMetrics.BorderThin));

        theme.SetTypeVariation(UiVariation.DestructiveButton, "Button");
        var dangerHover = ControlBox(UiPalette.DangerBg, UiPalette.Danger, UiMetrics.BorderFocus);
        theme.SetStylebox("normal", UiVariation.DestructiveButton, ControlBox(UiPalette.Panel, new Color(UiPalette.Danger, 0.60f), UiMetrics.BorderThin));
        theme.SetStylebox("hover", UiVariation.DestructiveButton, dangerHover);
        theme.SetStylebox("pressed", UiVariation.DestructiveButton, ControlBox(UiPalette.DangerBg.Darkened(0.2f), UiPalette.Danger, UiMetrics.BorderFocus));
        theme.SetStylebox("hover_pressed", UiVariation.DestructiveButton, dangerHover);
        theme.SetColor("font_color", UiVariation.DestructiveButton, UiPalette.Danger);
        theme.SetColor("font_hover_color", UiVariation.DestructiveButton, UiPalette.Danger.Lightened(0.25f));
        theme.SetColor("font_focus_color", UiVariation.DestructiveButton, UiPalette.Danger.Lightened(0.25f));
        theme.SetColor("font_pressed_color", UiVariation.DestructiveButton, UiPalette.Danger.Lightened(0.25f));
        theme.SetColor("font_hover_pressed_color", UiVariation.DestructiveButton, UiPalette.Danger.Lightened(0.25f));

        theme.SetTypeVariation(UiVariation.FlatButton, "Button");
        var flatNormal = EmptyBox(UiMetrics.S2, UiMetrics.S1);
        var flatHover = ControlBox(Colors.Transparent, Colors.Transparent, 0, UiMetrics.S2, UiMetrics.S1);
        flatHover.BorderWidthBottom = 2;
        flatHover.BorderColor = UiPalette.GoldDim;
        theme.SetStylebox("normal", UiVariation.FlatButton, flatNormal);
        theme.SetStylebox("hover", UiVariation.FlatButton, flatHover);
        theme.SetStylebox("pressed", UiVariation.FlatButton, flatHover);
        theme.SetStylebox("hover_pressed", UiVariation.FlatButton, flatHover);
        theme.SetStylebox("disabled", UiVariation.FlatButton, flatNormal);
        theme.SetColor("font_color", UiVariation.FlatButton, UiPalette.TextSecondary);
        theme.SetColor("font_hover_color", UiVariation.FlatButton, UiPalette.TextPrimary);
        theme.SetColor("font_focus_color", UiVariation.FlatButton, UiPalette.TextPrimary);
        theme.SetColor("font_pressed_color", UiVariation.FlatButton, UiPalette.Gold);
        theme.SetFontSize("font_size", UiVariation.FlatButton, UiFont.Secondary);

        // Aba: inativa sem fundo; ativa (pressed) = PanelRaised + barra inferior 3 Gold.
        theme.SetTypeVariation(UiVariation.TabButton, "Button");
        var tabNormal = EmptyBox(UiMetrics.S4, UiMetrics.S2);
        var tabActive = SelectedBox(UiPalette.PanelRaised);
        theme.SetStylebox("normal", UiVariation.TabButton, tabNormal);
        theme.SetStylebox("hover", UiVariation.TabButton, tabNormal);
        theme.SetStylebox("pressed", UiVariation.TabButton, tabActive);
        theme.SetStylebox("hover_pressed", UiVariation.TabButton, tabActive);
        theme.SetStylebox("disabled", UiVariation.TabButton, tabNormal);
        theme.SetColor("font_color", UiVariation.TabButton, UiPalette.TextSecondary);
        theme.SetColor("font_hover_color", UiVariation.TabButton, UiPalette.TextPrimary);
        theme.SetColor("font_focus_color", UiVariation.TabButton, UiPalette.TextPrimary);
        theme.SetColor("font_pressed_color", UiVariation.TabButton, UiPalette.Gold);
        theme.SetColor("font_hover_pressed_color", UiVariation.TabButton, UiPalette.Gold);

        // Seleção (slot, linha): normal padrão; selecionado = PanelRaised + barra inferior 3 Gold.
        theme.SetTypeVariation(UiVariation.SelectButton, "Button");
        var selected = SelectedBox(UiPalette.PanelRaised);
        theme.SetStylebox("pressed", UiVariation.SelectButton, selected);
        theme.SetStylebox("hover_pressed", UiVariation.SelectButton, selected);
        theme.SetColor("font_pressed_color", UiVariation.SelectButton, UiPalette.Gold);
        theme.SetColor("font_hover_pressed_color", UiVariation.SelectButton, UiPalette.Gold);

        // CheckButton: linha transparente com estados; ícone de chave gerado por código.
        var checkNormal = EmptyBox(UiMetrics.S2, UiMetrics.S1);
        theme.SetStylebox("normal", "CheckButton", checkNormal);
        theme.SetStylebox("pressed", "CheckButton", checkNormal);
        theme.SetStylebox("hover", "CheckButton", ControlBox(UiPalette.PanelRaised, UiPalette.GoldDim, UiMetrics.BorderThin, UiMetrics.S2, UiMetrics.S1));
        theme.SetStylebox("hover_pressed", "CheckButton", ControlBox(UiPalette.PanelRaised, UiPalette.GoldDim, UiMetrics.BorderThin, UiMetrics.S2, UiMetrics.S1));
        theme.SetStylebox("disabled", "CheckButton", checkNormal);
        theme.SetStylebox("focus", "CheckButton", FocusRing());
        ButtonFontColors(theme, "CheckButton");
        theme.SetColor("font_pressed_color", "CheckButton", UiPalette.TextPrimary);
        // Ícones gerados por tema (sem cache estático: o tema libera as texturas junto com a cena).
        var toggleOn = ToggleTexture(true, UiPalette.GoldDim, UiPalette.GoldBright);
        // Desligado: trilho mais claro que o fundo e bolinha clara (revisão T4: contraste).
        var toggleOff = ToggleTexture(false, UiPalette.PanelRaised.Lightened(0.10f), UiPalette.TextPrimary.Darkened(0.12f));
        var toggleOnDisabled = ToggleTexture(true, UiPalette.PanelInset, UiPalette.TextDisabled);
        var toggleOffDisabled = ToggleTexture(false, UiPalette.PanelInset, UiPalette.TextDisabled);
        foreach (var suffix in new[] { "", "_mirrored" })
        {
            theme.SetIcon("checked" + suffix, "CheckButton", toggleOn);
            theme.SetIcon("unchecked" + suffix, "CheckButton", toggleOff);
            theme.SetIcon("checked_disabled" + suffix, "CheckButton", toggleOnDisabled);
            theme.SetIcon("unchecked_disabled" + suffix, "CheckButton", toggleOffDisabled);
        }
        ButtonFontColors(theme, "CheckBox");
        theme.SetStylebox("focus", "CheckBox", FocusRing());

        // PopupMenu (lista do OptionButton)
        theme.SetStylebox("panel", "PopupMenu", ControlBox(UiPalette.PanelRaised, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S1, UiMetrics.S1));
        var popupHover = ControlBox(UiPalette.PanelRaised.Lightened(0.06f), Colors.Transparent, 0, UiMetrics.S2, UiMetrics.S1);
        popupHover.BorderWidthLeft = 3;
        popupHover.BorderColor = UiPalette.Gold;
        theme.SetStylebox("hover", "PopupMenu", popupHover);
        theme.SetColor("font_color", "PopupMenu", UiPalette.TextPrimary);
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetColor("font_disabled_color", "PopupMenu", UiPalette.TextDisabled);
        theme.SetFontSize("font_size", "PopupMenu", UiFont.Body);
        theme.SetConstant("v_separation", "PopupMenu", UiMetrics.S2);
        theme.SetStylebox("panel", "PopupPanel", ControlBox(UiPalette.PanelRaised, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S1, UiMetrics.S1));

        // Painéis
        var panel = ControlBox(UiPalette.Panel, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.PadPanelX, UiMetrics.PadPanelY);
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "Panel", panel);
        theme.SetTypeVariation(UiVariation.WindowPanel, "PanelContainer");
        theme.SetStylebox("panel", UiVariation.WindowPanel, WindowBox());
        theme.SetTypeVariation(UiVariation.HudPanel, "PanelContainer");
        theme.SetStylebox("panel", UiVariation.HudPanel, ControlBox(UiPalette.PanelHud, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.PadPanelX, UiMetrics.PadPanelY));
        theme.SetTypeVariation(UiVariation.InsetPanel, "PanelContainer");
        theme.SetStylebox("panel", UiVariation.InsetPanel, ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S2, UiMetrics.S2));
        theme.SetTypeVariation(UiVariation.RaisedPanel, "PanelContainer");
        theme.SetStylebox("panel", UiVariation.RaisedPanel, ControlBox(UiPalette.PanelRaised, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.PadPanelX, UiMetrics.PadPanelY));

        // Campos
        var field = ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S2, UiMetrics.S1);
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("read_only", "LineEdit", ControlBox(UiPalette.PanelInset, new Color(UiPalette.Border, 0.36f), UiMetrics.BorderThin, UiMetrics.S2, UiMetrics.S1));
        theme.SetStylebox("focus", "LineEdit", FocusRing());
        theme.SetColor("font_color", "LineEdit", UiPalette.TextPrimary);
        theme.SetColor("font_uneditable_color", "LineEdit", UiPalette.TextDisabled);
        theme.SetColor("font_placeholder_color", "LineEdit", UiPalette.TextDisabled);
        theme.SetColor("caret_color", "LineEdit", UiPalette.GoldBright);
        theme.SetColor("selection_color", "LineEdit", new Color(UiPalette.Gold, 0.35f));

        // HSlider: trilho 6 px, área preenchida GoldDim/Gold, pegador redondo 16 px.
        // Trilho não preenchido: um pouco mais claro que PanelInset e com borda 1 Border (legível no fundo escuro).
        var sliderTrack = ControlBox(UiPalette.PanelRaised.Lightened(0.06f), new Color(UiPalette.Border, 1.0f), UiMetrics.BorderThin, 0, 3);
        sliderTrack.SetCornerRadiusAll(3);
        var sliderArea = ControlBox(UiPalette.GoldDim, Colors.Transparent, 0, 0, 3);
        sliderArea.SetCornerRadiusAll(3);
        var sliderAreaHighlight = ControlBox(UiPalette.Gold, Colors.Transparent, 0, 0, 3);
        sliderAreaHighlight.SetCornerRadiusAll(3);
        theme.SetStylebox("slider", "HSlider", sliderTrack);
        theme.SetStylebox("grabber_area", "HSlider", sliderArea);
        theme.SetStylebox("grabber_area_highlight", "HSlider", sliderAreaHighlight);
        theme.SetStylebox("focus", "HSlider", FocusRing());
        theme.SetIcon("grabber", "HSlider", CircleTexture(16, UiPalette.TextPrimary));
        theme.SetIcon("grabber_highlight", "HSlider", CircleTexture(16, UiPalette.GoldBright));
        theme.SetIcon("grabber_disabled", "HSlider", CircleTexture(16, UiPalette.TextDisabled));

        // Barras de rolagem: trilho 8 px.
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            var vertical = type == "VScrollBar";
            theme.SetStylebox("scroll", type, ScrollBox(UiPalette.PanelInset, vertical));
            theme.SetStylebox("scroll_focus", type, ScrollBox(UiPalette.PanelInset, vertical));
            theme.SetStylebox("grabber", type, ScrollBox(UiPalette.Border, vertical));
            theme.SetStylebox("grabber_highlight", type, ScrollBox(UiPalette.GoldDim, vertical));
            theme.SetStylebox("grabber_pressed", type, ScrollBox(UiPalette.Gold, vertical));
        }
        theme.SetStylebox("panel", "ScrollContainer", new StyleBoxEmpty());
        theme.SetStylebox("focus", "ScrollContainer", FocusRing());

        // TabBar / TabContainer
        foreach (var type in new[] { "TabBar", "TabContainer" })
        {
            theme.SetStylebox("tab_selected", type, SelectedBox(UiPalette.PanelRaised));
            theme.SetStylebox("tab_unselected", type, EmptyBox(UiMetrics.S4, UiMetrics.S2));
            theme.SetStylebox("tab_hovered", type, EmptyBox(UiMetrics.S4, UiMetrics.S2));
            theme.SetStylebox("tab_disabled", type, EmptyBox(UiMetrics.S4, UiMetrics.S2));
            theme.SetStylebox("tab_focus", type, FocusRing());
            theme.SetColor("font_selected_color", type, UiPalette.Gold);
            theme.SetColor("font_hovered_color", type, UiPalette.TextPrimary);
            theme.SetColor("font_unselected_color", type, UiPalette.TextSecondary);
            theme.SetColor("font_disabled_color", type, UiPalette.TextDisabled);
        }
        theme.SetStylebox("panel", "TabContainer", panel);

        // Tooltip
        theme.SetStylebox("panel", "TooltipPanel", ControlBox(UiPalette.PanelRaised, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S3, UiMetrics.S2));
        theme.SetColor("font_color", "TooltipLabel", UiPalette.TextPrimary);
        theme.SetFontSize("font_size", "TooltipLabel", UiFont.Secondary);
        theme.SetColor("font_outline_color", "TooltipLabel", UiPalette.TextOutline);
        theme.SetConstant("outline_size", "TooltipLabel", 0);

        // Barras e separadores
        theme.SetStylebox("background", "ProgressBar", ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, 0, 0));
        theme.SetStylebox("fill", "ProgressBar", ControlBox(UiPalette.Life, Colors.Transparent, 0, 0, 0));
        theme.SetColor("font_color", "ProgressBar", UiPalette.TextPrimary);
        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = new Color(UiPalette.GoldDim, 0.6f), Thickness = 1 });
        theme.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = new Color(UiPalette.GoldDim, 0.6f), Thickness = 1, Vertical = true });
        return theme;
    }

    // Legado: painel do HUD em jogo (agora segue UiPalette.PanelHud/Border).
    public static StyleBoxFlat HudPanel(float alpha = 0.84f) => Box(
        new Color(UiPalette.PanelHud, alpha),
        UiPalette.Border,
        UiMetrics.BorderThin,
        UiMetrics.RadiusControl);

    // Legado: estilo do card de escolha; telas novas usam UiComponents.ChoiceCard.
    public static StyleBoxFlat ChoiceCard(Color accent, float alpha = 0.98f) => Box(
        new Color(UiPalette.Panel, alpha),
        accent,
        UiMetrics.BorderCard,
        UiMetrics.RadiusCard);

    public static NinePatchRect AddSimpleFrame(
        Control parent,
        string name,
        Vector4 anchors,
        Vector4 offsets,
        Color tint,
        int patchMargin = 10)
    {
        var frame = new NinePatchRect
        {
            Name = name,
            Texture = ResourceLoader.Load<Texture2D>(SimplePanelPath),
            AnchorLeft = anchors.X,
            AnchorTop = anchors.Y,
            AnchorRight = anchors.Z,
            AnchorBottom = anchors.W,
            OffsetLeft = offsets.X,
            OffsetTop = offsets.Y,
            OffsetRight = offsets.Z,
            OffsetBottom = offsets.W,
            PatchMarginLeft = patchMargin,
            PatchMarginTop = patchMargin,
            PatchMarginRight = patchMargin,
            PatchMarginBottom = patchMargin,
            DrawCenter = true,
            Modulate = tint,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(frame);
        return frame;
    }

    // StyleBox de controle com tokens: raio RadiusControl e padding informado.
    public static StyleBoxFlat ControlBox(Color background, Color border, int borderWidth, int padX = UiMetrics.PadPanelX, int padY = UiMetrics.PadPanelY, int radius = UiMetrics.RadiusControl)
    {
        var box = new StyleBoxFlat { BgColor = background, BorderColor = border };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.ContentMarginLeft = padX;
        box.ContentMarginRight = padX;
        box.ContentMarginTop = padY;
        box.ContentMarginBottom = padY;
        return box;
    }

    // Anel de foco (§3): fundo transparente, borda 2 Focus, expand 3, sombra FocusGlow 6.
    public static StyleBoxFlat FocusRing()
    {
        var box = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            DrawCenter = false,
            BorderColor = UiPalette.Focus,
            ShadowColor = UiPalette.FocusGlow,
            ShadowSize = 6
        };
        box.SetBorderWidthAll(UiMetrics.BorderFocus);
        box.SetCornerRadiusAll(UiMetrics.RadiusControl);
        box.SetExpandMarginAll(UiMetrics.FocusExpand);
        return box;
    }

    // Moldura de janela modal: Panel 0.97, borda 2 WindowBorder, padding 24/20.
    public static StyleBoxFlat WindowBox() => ControlBox(UiPalette.Panel, UiPalette.WindowBorder, UiMetrics.BorderWindow, UiMetrics.PadWindowX, UiMetrics.PadWindowY);

    // Estado "selecionado": borda 1 Border + barra inferior 3 Gold.
    public static StyleBoxFlat SelectedBox(Color background)
    {
        var box = ControlBox(background, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S4, UiMetrics.S2);
        box.BorderWidthBottom = 3;
        box.BorderColor = UiPalette.Gold;
        box.BorderWidthLeft = 0;
        box.BorderWidthRight = 0;
        box.BorderWidthTop = 0;
        return box;
    }

    public static StyleBoxEmpty EmptyBox(int padX, int padY) => new StyleBoxEmpty
    {
        ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY
    };

    private static void LabelVariation(Theme theme, string name, int fontSize, Color color, int outline)
    {
        theme.SetTypeVariation(name, "Label");
        theme.SetFontSize("font_size", name, fontSize);
        theme.SetColor("font_color", name, color);
        theme.SetConstant("outline_size", name, outline);
        theme.SetColor("font_outline_color", name, UiPalette.TextOutline);
    }

    private static void ApplyButtonStates(Theme theme, string type)
    {
        var hover = ControlBox(UiPalette.PanelRaised, UiPalette.GoldDim, UiMetrics.BorderThin, UiMetrics.PadPanelX, UiMetrics.S2);
        theme.SetStylebox("normal", type, ControlBox(UiPalette.Panel, UiPalette.Border, UiMetrics.BorderThin, UiMetrics.PadPanelX, UiMetrics.S2));
        theme.SetStylebox("hover", type, hover);
        theme.SetStylebox("pressed", type, ControlBox(UiPalette.PanelRaised.Darkened(0.2f), UiPalette.Gold, UiMetrics.BorderFocus, UiMetrics.PadPanelX, UiMetrics.S2));
        theme.SetStylebox("hover_pressed", type, ControlBox(UiPalette.PanelRaised.Darkened(0.2f), UiPalette.Gold, UiMetrics.BorderFocus, UiMetrics.PadPanelX, UiMetrics.S2));
        theme.SetStylebox("disabled", type, ControlBox(UiPalette.PanelInset, new Color(UiPalette.Border, UiPalette.Border.A * 0.5f), UiMetrics.BorderThin, UiMetrics.PadPanelX, UiMetrics.S2));
        theme.SetStylebox("focus", type, FocusRing());
        ButtonFontColors(theme, type);
    }

    private static void ButtonFontColors(Theme theme, string type)
    {
        theme.SetColor("font_color", type, UiPalette.TextPrimary);
        theme.SetColor("font_hover_color", type, Colors.White);
        theme.SetColor("font_focus_color", type, Colors.White);
        theme.SetColor("font_pressed_color", type, UiPalette.Gold);
        theme.SetColor("font_hover_pressed_color", type, UiPalette.Gold);
        theme.SetColor("font_disabled_color", type, UiPalette.TextDisabled);
        theme.SetColor("font_outline_color", type, UiPalette.TextOutline);
    }

    private static StyleBoxFlat ScrollBox(Color color, bool vertical)
    {
        var box = new StyleBoxFlat { BgColor = color };
        box.SetCornerRadiusAll(UiMetrics.RadiusControl);
        if (vertical) { box.ContentMarginLeft = 4; box.ContentMarginRight = 4; }
        else { box.ContentMarginTop = 4; box.ContentMarginBottom = 4; }
        return box;
    }

    // Círculo antisserrilhado gerado por código (pegador do slider).
    public static ImageTexture CircleTexture(int diameter, Color color)
    {
        var image = Image.CreateEmpty(diameter, diameter, false, Image.Format.Rgba8);
        var radius = diameter * 0.5f;
        for (var y = 0; y < diameter; y++)
            for (var x = 0; x < diameter; x++)
            {
                var distance = new Vector2(x + 0.5f - radius, y + 0.5f - radius).Length();
                var coverage = Mathf.Clamp(radius - distance + 0.5f, 0.0f, 1.0f);
                image.SetPixel(x, y, new Color(color, color.A * coverage));
            }
        return ImageTexture.CreateFromImage(image);
    }

    // Chave 40x22: trilho arredondado + bolinha à esquerda (desligado) ou à direita (ligado).
    private static ImageTexture ToggleTexture(bool on, Color track, Color knob)
    {
        const int width = 40, height = 22;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var trackRadius = height * 0.5f;
        var knobRadius = 7.0f;
        var knobCenter = new Vector2(on ? width - trackRadius : trackRadius, trackRadius);
        var border = UiPalette.Border;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                var clampedX = Mathf.Clamp(point.X, trackRadius, width - trackRadius);
                var trackDistance = point.DistanceTo(new Vector2(clampedX, trackRadius));
                var trackCoverage = Mathf.Clamp(trackRadius - trackDistance + 0.5f, 0.0f, 1.0f);
                var borderMix = Mathf.Clamp(trackDistance - (trackRadius - 1.5f), 0.0f, 1.0f);
                var color = track.Lerp(new Color(border.Lightened(0.15f), 1.0f), borderMix);
                color.A = trackCoverage;
                var knobCoverage = Mathf.Clamp(knobRadius - point.DistanceTo(knobCenter) + 0.5f, 0.0f, 1.0f);
                if (knobCoverage > 0.0f)
                {
                    color = color.Lerp(knob, knobCoverage);
                    color.A = Mathf.Max(color.A, knobCoverage);
                }
                image.SetPixel(x, y, color);
            }
        return ImageTexture.CreateFromImage(image);
    }

    // Box legado (content margin 14/10), mantido para HudPanel/ChoiceCard antigos.
    private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius = 7)
    {
        return new StyleBoxFlat
        {
            BgColor = background, BorderColor = border,
            BorderWidthLeft = borderWidth, BorderWidthTop = borderWidth, BorderWidthRight = borderWidth, BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10
        };
    }
}
