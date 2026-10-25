using Godot;
using System;
using System.Collections.Generic;

// Componentes de UI reutilizáveis (docs/design/ui-guia.md §3, §6). Montados por código com os tokens de UIThemeFactory.cs.
// Nenhum componente mexe em pausa ou na UiModalStack: quem abre a tela faz Push/Pop.

public enum WindowSize { Small, Medium, Large, Full }
public enum ButtonVariant { Primary, Secondary, Destructive, Flat }
public enum ButtonSize { Large, Medium, Small }
public enum ConfirmKind { Destructive, Neutral }
public enum ResourceBarKind { Health, Fury, Boss }
public enum DetailPanelDock { Right, Bottom }

// Ações exibidas em glifos de botão (§3.11).
public enum UiAction { Accept, Cancel, Alt, TabPrev, TabNext, ZoomOut, ZoomIn, Scroll, CharacterSheet, Pause, Fury, Interact }

// Estados visuais de nó de skill (§2.1, Fonte de Sangue).
public enum SkillNodeState { Purchased, PurchasedMax, PurchasedNoCoins, Available, AvailableNoCoins, Locked, Conflict, Planned }

// Janela modal: Root (scrim) > Panel > Content [cabeçalho, status, divisor, Body, Actions, linha, Footer].
public sealed class UiWindow
{
    public ColorRect Root;
    public PanelContainer Panel;
    public VBoxContainer Content;
    public Label Title;
    public Label Status;
    public HBoxContainer HeaderRight;
    public VBoxContainer Body;
    public HBoxContainer Actions;
    public ColorRect FooterLine;
    public UiHintBar Footer;
    public WindowSize Size;
    // Oferta obrigatória: a tela não mostra "Voltar" e trata ui_cancel com aviso.
    public bool Mandatory;
    private Tween _fade;

    public void SetTitle(string text) => Title.Text = text ?? "";

    public void SetStatus(string text)
    {
        Status.Text = text ?? "";
        Status.Visible = !string.IsNullOrEmpty(text);
    }

    // Mostra e faz fade de OverlayIn.
    public void FadeIn()
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        Root.Visible = true;
        _fade?.Kill();
        if (!Root.IsInsideTree()) { Root.Modulate = Colors.White; return; }
        Root.Modulate = new Color(1, 1, 1, 0);
        _fade = Root.CreateTween();
        _fade.TweenProperty(Root, "modulate:a", 1.0f, UiMotion.OverlayIn);
    }

    // Fade de OverlayOut, esconde e chama onDone (ex.: QueueFree).
    public void FadeOut(Action onDone = null)
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        _fade?.Kill();
        if (!Root.IsInsideTree() || !Root.Visible)
        {
            Root.Visible = false;
            onDone?.Invoke();
            return;
        }
        _fade = Root.CreateTween();
        _fade.TweenProperty(Root, "modulate:a", 0.0f, UiMotion.OverlayOut);
        _fade.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(Root)) return;
            Root.Visible = false;
            Root.Modulate = Colors.White;
            onDone?.Invoke();
        }));
    }
}

public sealed class UiConfirm
{
    public UiWindow Window;
    public Button CancelButton;
    public Button ConfirmButton;
    public bool IsOpen { get; private set; } = true;
    internal Action OnConfirm;
    internal Action OnCancel;
    internal Control ReturnFocus;

    // Fecha sem confirmar (ui_cancel, botão Cancelar). Idempotente.
    public void Cancel()
    {
        if (!Close()) return;
        OnCancel?.Invoke();
    }

    public void Accept()
    {
        if (!Close()) return;
        OnConfirm?.Invoke();
    }

    private bool Close()
    {
        if (!IsOpen) return false;
        IsOpen = false;
        if (GodotObject.IsInstanceValid(Window.Root)) Window.Root.QueueFree();
        if (ReturnFocus != null && GodotObject.IsInstanceValid(ReturnFocus) && ReturnFocus.IsVisibleInTree())
            ReturnFocus.CallDeferred(Control.MethodName.GrabFocus);
        return true;
    }
}

// Dados do card de escolha (§3.4). Campos vazios/nulos não aparecem.
public sealed class ChoiceCardData
{
    public Texture2D Icon;
    public string IconText;          // monograma quando não há ícone (padrão: 2 letras do título)
    public string Title;
    public string RarityId;          // define a cor da borda; nulo = borda Border
    public string RarityLabel;       // selo (já localizado); nulo = UiComponents.RarityLabel(RarityId)
    public Color? Accent;            // substitui a cor da raridade (ex.: Gold na arma equipada)
    public string Tag;               // selo extra (ex.: "EQUIPADA", "EM BREVE")
    public Color? TagColor;
    public string Meta;
    public string BodyBbcode;
    public string PreviewBbcode;     // altar: "atual → próximo"
    public string Warning;           // ex.: "Substitui: X" (sempre inline)
    public string ActionText;        // vazio = card só leitura (o próprio card recebe foco)
    public bool WholeCardAction;     // sem botão ao lado: o clique no card (ou Enter com ele focado) escolhe
    public string CostText;
    public Color? CostColor;         // padrão TextSecondary; altar usa Warn
    public bool Disabled;
    public string DisabledReason;
    public float Width;              // 0 = preenche o container; >0 = largura fixa centrada
}

public sealed class UiChoiceCard
{
    public PanelContainer Root;
    public Button ActionButton;      // elemento focável (nulo no modo leitura)
    public Control FocusTarget => ActionButton != null ? ActionButton : Root;
    internal ChoiceCardData Data;
    internal Action OnChoose;
    internal ColorRect FocusBar;
    internal Color Accent;
    internal bool Focused, Hovered, Flashing;
    private Tween _flash;

    // Foco/hover mantêm a cor da raridade (clarear confundia Comum com Mítico): o foco é marcado pelo anel externo
    // Focus (só foco de teclado/controle, mais forte que o hover) e pela barra esquerda Gold.
    internal void Refresh()
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        var active = (Focused || Hovered) && !Data.Disabled;
        FocusBar.Color = active ? UiPalette.Gold : Colors.Transparent;
        // 2D: moldura em pixel art tingida pela raridade (clareada no foco/hover), barra Gold logo após o contorno.
        var tint = Flashing ? UiPalette.GoldBright : Data.Disabled ? UiPalette.Border : active ? Accent.Lightened(0.25f) : Accent;
        if (UiPixelKit.Frame(tint, active ? UiPalette.PanelRaised : UiPalette.Panel, UiMetrics.PadPanelX, UiMetrics.PadPanelY) is { } pixel)
        {
            pixel.ContentMarginLeft = UiPixelKit.FrameBorder;
            Root.AddThemeStyleboxOverride("panel", pixel);
            Root.QueueRedraw();
            return;
        }
        var style = new StyleBoxFlat
        {
            BgColor = active ? UiPalette.PanelRaised : UiPalette.Panel,
            BorderColor = Flashing ? UiPalette.Gold : Data.Disabled ? UiPalette.Border : Accent,
            ContentMarginLeft = 0, ContentMarginRight = UiMetrics.PadPanelX,
            ContentMarginTop = UiMetrics.PadPanelY, ContentMarginBottom = UiMetrics.PadPanelY
        };
        style.SetBorderWidthAll(Data.Disabled ? UiMetrics.BorderThin : active || Flashing ? UiMetrics.BorderCardFocus : UiMetrics.BorderCard);
        style.SetCornerRadiusAll(UiMetrics.RadiusCard);
        Root.AddThemeStyleboxOverride("panel", style);
        Root.QueueRedraw();
    }

    internal bool Stirps => (Data.RarityId ?? "").Equals("stirps", StringComparison.OrdinalIgnoreCase) && !Data.Disabled;

    // Desenhado depois do painel (sinal Draw), sem mexer no layout: linha interna do Stirps e anel de foco por fora.
    internal void DrawOverlay()
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        var rect = new Rect2(Vector2.Zero, Root.Size);
        if (Stirps)
        {
            var active = (Focused || Hovered) || Flashing;
            var inset = (active ? UiMetrics.BorderCardFocus : UiMetrics.BorderCard) + 2;
            if (_stirpsLine == null)
            {
                _stirpsLine = new StyleBoxFlat { DrawCenter = false, BorderColor = UiPalette.Gold };
                _stirpsLine.SetBorderWidthAll(1);
                _stirpsLine.SetCornerRadiusAll(UiMetrics.RadiusCard);
            }
            Root.DrawStyleBox(_stirpsLine, rect.Grow(-inset));
        }
        if (Focused && !Data.Disabled)
        {
            // O anel muda com o modo (3D/2D), que pode trocar na mesma sessão.
            if (_focusRing == null || _focusRingPixel != UiPixelKit.Enabled)
            {
                _focusRing = UIThemeFactory.FocusRing();
                _focusRingPixel = UiPixelKit.Enabled;
            }
            Root.DrawStyleBox(_focusRing, rect);
        }
    }

    private static StyleBoxFlat _stirpsLine;
    private static StyleBox _focusRing;
    private static bool _focusRingPixel;

    // Escolhe: flash Gold na borda e chama o handler (sem atrasar o fluxo da tela).
    public void Choose()
    {
        if (Data.Disabled || OnChoose == null) return;
        if (GodotObject.IsInstanceValid(Root) && Root.IsInsideTree())
        {
            Flashing = true;
            Refresh();
            _flash?.Kill();
            _flash = Root.CreateTween();
            _flash.TweenInterval(UiMotion.CardChooseFlash);
            _flash.TweenCallback(Callable.From(() => { Flashing = false; Refresh(); }));
        }
        OnChoose();
    }
}

// Barra de recurso desenhada por código (§3.5). Root vai para o layout; Bar é o trilho.
public sealed class UiResourceBar
{
    public ResourceBarKind Kind;
    public Control Root;
    public Control Bar;
    public Label ValueLabel;
    public Label BarrierLabel;   // Health: "+N" em Restore, fora da barra
    public Label NameLabel;      // Boss
    public Control FuryBadge;    // Fury
    public Control FuryGlyph;    // Fury: glifo do botão quando cheia
    private float _current, _max = 1, _barrier, _barrierMax, _chip, _borderFlash, _pulse = 1;
    private bool _furyReady, _furyActive;
    private Tween _chipTween, _flashTween, _pulseTween;
    private StyleBox _trackStyle;
    // Borda interna do trilho: 1 px no StyleBox desenhado; a moldura em pixel art do 2D (UiPixelKit) é mais grossa.
    private int _trackInset = 1;

    public float Current => _current;
    public float Maximum => _max;

    public void SetValue(float current, float maximum) => SetHealth(current, maximum, 0, 0);

    // Health: barreira atrás da vida sobre o máximo combinado (vida máx + barreira máx).
    public void SetHealth(float current, float maximum, float barrier, float barrierMaximum)
    {
        maximum = Mathf.Max(maximum, 0.001f);
        current = Mathf.Clamp(current, 0, maximum);
        var previous = _current;
        var firstSet = _max <= 1 && _current == 0 && _chip == 0;
        _current = current;
        _max = maximum;
        _barrier = Mathf.Max(barrier, 0);
        _barrierMax = Mathf.Max(barrierMaximum, _barrier);
        UpdateChip(previous, firstSet);
        if (ValueLabel != null)
            ValueLabel.Text = $"{Mathf.CeilToInt(_current)}/{Mathf.CeilToInt(_max)}";
        if (BarrierLabel != null)
            BarrierLabel.Text = _barrier > 0.01f ? $"+{Mathf.CeilToInt(_barrier)}" : "";
        Bar.QueueRedraw();
    }

    public void SetName(string name)
    {
        if (NameLabel != null) NameLabel.Text = name ?? "";
    }

    // Fury: texto do selo (troca de idioma).
    public void SetFuryBadgeText(string text)
    {
        if (FuryBadge == null) return;
        foreach (var child in FuryBadge.GetChildren())
            if (child is Label label) label.Text = text ?? "";
    }

    // Fury: cheia = borda FuryReady pulsando + glifo; ativa = borda fixa.
    public void SetFuryState(bool ready, bool active)
    {
        if (_furyReady == ready && _furyActive == active) return;
        _furyReady = ready;
        _furyActive = active;
        if (FuryGlyph != null) FuryGlyph.Visible = ready && !active;
        _pulseTween?.Kill();
        _pulse = 1;
        if (ready && !active && Bar.IsInsideTree())
        {
            var half = 0.5f / UiMotion.CriticalPulseHz;
            _pulseTween = Bar.CreateTween().SetLoops();
            _pulseTween.TweenMethod(Callable.From<float>(SetPulse), 1.0f, 0.35f, half);
            _pulseTween.TweenMethod(Callable.From<float>(SetPulse), 0.35f, 1.0f, half);
        }
        Bar.QueueRedraw();
    }

    // Dano: a borda do trilho pisca TextPrimary por FlashFall.
    public void FlashDamage()
    {
        _flashTween?.Kill();
        _borderFlash = 1;
        Bar.QueueRedraw();
        if (!Bar.IsInsideTree()) { _borderFlash = 0; return; }
        _flashTween = Bar.CreateTween();
        _flashTween.TweenMethod(Callable.From<float>(SetBorderFlash), 1.0f, 0.0f, UiMotion.FlashFall);
    }

    private void UpdateChip(float previous, bool firstSet)
    {
        _chipTween?.Kill();
        if (firstSet || _current >= _chip || !Bar.IsInsideTree())
        {
            _chip = _current;
            return;
        }
        if (_chip < previous) _chip = previous;
        _chipTween = Bar.CreateTween();
        _chipTween.TweenInterval(UiMotion.ChipDelay);
        _chipTween.TweenMethod(Callable.From<float>(SetChip), _chip, _current, UiMotion.ChipDrain);
    }

    private void SetChip(float value) { _chip = value; Bar.QueueRedraw(); }
    private void SetBorderFlash(float value) { _borderFlash = value; Bar.QueueRedraw(); }
    private void SetPulse(float value) { _pulse = value; Bar.QueueRedraw(); }

    internal void Draw()
    {
        var rect = new Rect2(Vector2.Zero, Bar.Size);
        _trackStyle ??= MakeTrackStyle();
        Bar.DrawStyleBox(_trackStyle, rect);
        var inner = rect.Grow(-_trackInset);
        if (inner.Size.X <= 0 || inner.Size.Y <= 0) return;
        var total = Kind == ResourceBarKind.Health ? Mathf.Max(_max + _barrierMax, 1.0f) : Mathf.Max(_max, 1.0f);
        if (Kind != ResourceBarKind.Fury && _chip > _current)
            Bar.DrawRect(Segment(inner, _chip / total), UiPalette.LifeChip);
        if (Kind == ResourceBarKind.Health && _barrier > 0.01f)
            Bar.DrawRect(Segment(inner, Mathf.Min(_current + _barrier, total) / total), UiPalette.BarrierFill);
        var fillColor = Kind switch
        {
            ResourceBarKind.Fury => _furyReady || _furyActive ? UiPalette.FuryReady : UiPalette.Fury,
            ResourceBarKind.Boss => UiPalette.BossFill,
            _ => UiPalette.Life
        };
        var fill = Segment(inner, _current / total);
        if (fill.Size.X > 0)
        {
            Bar.DrawRect(fill, fillColor);
            if (Kind == ResourceBarKind.Health)
                Bar.DrawRect(new Rect2(fill.Position, new Vector2(fill.Size.X, Mathf.Min(2, fill.Size.Y))), UiPalette.LifeHighlight);
        }
        // Marcas a cada 25% da vida máxima (Health: não da vida + barreira, para a marca continuar valendo "25% da vida").
        var tick = new Color(0, 0, 0, 0.5f);
        var tickSpan = Kind == ResourceBarKind.Health ? Mathf.Clamp(_max / total, 0, 1) : 1.0f;
        for (var i = 1; i < 4; i++)
        {
            var x = Mathf.Round(inner.Position.X + inner.Size.X * tickSpan * i * 0.25f);
            Bar.DrawRect(new Rect2(x, inner.Position.Y, 1, inner.Size.Y), tick);
        }
        if (Kind == ResourceBarKind.Fury && (_furyReady || _furyActive))
            Bar.DrawRect(rect, new Color(UiPalette.FuryReady, _furyActive ? 1.0f : _pulse), false, 2);
        if (_borderFlash > 0.001f)
            Bar.DrawRect(rect, new Color(UiPalette.TextPrimary, _borderFlash), false, 1);
    }

    private static Rect2 Segment(Rect2 inner, float ratio) =>
        new Rect2(inner.Position, new Vector2(inner.Size.X * Mathf.Clamp(ratio, 0, 1), inner.Size.Y));

    // 2D: moldura em pixel art (fúria no trilho fino, vida e chefe na moldura com aro); 3D ou peça ausente: StyleBox desenhado.
    private StyleBox MakeTrackStyle()
    {
        var thin = Kind == ResourceBarKind.Fury;
        var margin = thin ? UiPixelKit.ThinTrackMargin : UiPixelKit.BarFrameMargin;
        if (UiPixelKit.NineSlice(thin ? "bar_track_thin.png" : "bar_frame.png", margin, 0, 0) is { } pixel)
        {
            _trackInset = margin;
            return pixel;
        }
        _trackInset = 1;
        return Kind == ResourceBarKind.Boss ? MakeTrack(UiPalette.PanelHud, UiPalette.WindowBorder) : MakeTrack(UiPalette.PanelInset, UiPalette.Border);
    }

    private static StyleBoxFlat MakeTrack(Color background, Color border)
    {
        var box = new StyleBoxFlat { BgColor = background, BorderColor = border };
        box.SetBorderWidthAll(UiMetrics.BorderThin);
        return box;
    }
}

// Dados do painel de detalhes (§3.6).
public sealed class DetailData
{
    public Texture2D Icon;
    public string IconText;
    public string Title;
    public string RarityId;       // pinta o título; nulo = Gold
    public string BadgeText;
    public Color? BadgeColor;
    public string Meta;
    public string BodyBbcode;
    public string Flavor;
    public string ActionText;     // ex.: "Comprar · 3 moedas"
    public UiAction? ActionGlyph;
}

public sealed class UiDetailPanel
{
    public PanelContainer Root;
    public ScrollContainer Scroll;
    internal Control IconSlot;
    internal Label Title, Meta, Flavor, Empty, ActionLabel;
    internal HBoxContainer Badges, ActionRow, Header;
    internal RichTextLabel Body;
    internal Control ActionGlyph;

    public void Show(DetailData data)
    {
        if (data == null) { Clear(); return; }
        Empty.Visible = false;
        Header.Visible = true;
        Scroll.Visible = true;
        foreach (var child in IconSlot.GetChildren()) { IconSlot.RemoveChild(child); child.QueueFree(); }
        IconSlot.AddChild(UiComponents.IconOrMonogram(data.Icon, data.IconText ?? data.Title, UiMetrics.IconL));
        Title.Text = data.Title ?? "";
        Title.AddThemeColorOverride("font_color", string.IsNullOrEmpty(data.RarityId) ? UiPalette.Gold : UiPalette.Rarity(data.RarityId));
        foreach (var child in Badges.GetChildren()) { Badges.RemoveChild(child); child.QueueFree(); }
        if (!string.IsNullOrEmpty(data.BadgeText))
            Badges.AddChild(UiComponents.Badge(data.BadgeText, data.BadgeColor ?? (string.IsNullOrEmpty(data.RarityId) ? UiPalette.Gold : UiPalette.Rarity(data.RarityId))));
        Badges.Visible = Badges.GetChildCount() > 0;
        SetText(Meta, data.Meta);
        Body.Text = data.BodyBbcode ?? "";
        Body.Visible = !string.IsNullOrEmpty(data.BodyBbcode);
        SetText(Flavor, data.Flavor);
        ActionRow.Visible = !string.IsNullOrEmpty(data.ActionText);
        ActionLabel.Text = data.ActionText ?? "";
        if (ActionGlyph != null) { ActionRow.RemoveChild(ActionGlyph); ActionGlyph.QueueFree(); ActionGlyph = null; }
        if (data.ActionGlyph.HasValue)
        {
            ActionGlyph = UiComponents.ButtonGlyph(data.ActionGlyph.Value);
            ActionRow.AddChild(ActionGlyph);
            ActionRow.MoveChild(ActionGlyph, 0);
        }
        Scroll.ScrollVertical = 0;
    }

    public void Clear()
    {
        Empty.Visible = true;
        Header.Visible = false;
        Scroll.Visible = false;
        ActionRow.Visible = false;
    }

    // Rolagem do texto longo (analógico direito / ui_scroll_*), chamada pela tela.
    public void ScrollBy(float pixels) => Scroll.ScrollVertical += Mathf.RoundToInt(pixels);

    private static void SetText(Label label, string text)
    {
        label.Text = text ?? "";
        label.Visible = !string.IsNullOrEmpty(text);
    }
}

public sealed class UiTabs
{
    public HBoxContainer Root;
    public readonly List<Button> Buttons = new();
    public int Selected { get; private set; }
    internal Action<int> Changed;

    public void Select(int index, bool notify = true)
    {
        if (Buttons.Count == 0) return;
        index = (index % Buttons.Count + Buttons.Count) % Buttons.Count;
        var changed = index != Selected;
        Selected = index;
        Buttons[index].SetPressedNoSignal(true);
        for (var i = 0; i < Buttons.Count; i++)
            if (i != index) Buttons[i].SetPressedNoSignal(false);
        if (notify && changed) Changed?.Invoke(index);
    }

    public void Next() => Select(Selected + 1);
    public void Previous() => Select(Selected - 1);

    // L1/R1 (ui_tab_prev/ui_tab_next) com foco em qualquer lugar; chame no _Input/_UnhandledInput da tela.
    // Quadro em que as abas entraram na árvore/ficaram visíveis; eventos desse quadro são ignorados.
    internal ulong ArmedFrame;

    // Rearma manualmente (ex.: a tela reabre sem recriar o componente).
    public void Arm() => ArmedFrame = Engine.GetProcessFrames();

    // Troca de aba por EVENTO (sem polling de Input.IsActionPressed): chame no _Input/_UnhandledInput da tela, só com ela aberta.
    // ui_tab_prev/ui_tab_next usam Q/E e LB/RB, que colidem com interact/attack_fury/attack_special do gameplay.
    // Por isso ignora o quadro em que as abas entraram na árvore ou ficaram visíveis (a tecla que abriu a tela não troca a aba),
    // ignora repetição (echo) e eventos com as abas ocultas. Retorna true quando consumiu o evento.
    public bool HandleInput(InputEvent input)
    {
        if (input == null || input.IsEcho() || !GodotObject.IsInstanceValid(Root) || !Root.IsVisibleInTree()) return false;
        if (Engine.GetProcessFrames() <= ArmedFrame) return false;
        if (InputMap.HasAction("ui_tab_prev") && input.IsActionPressed("ui_tab_prev")) { Previous(); return true; }
        if (InputMap.HasAction("ui_tab_next") && input.IsActionPressed("ui_tab_next")) { Next(); return true; }
        return false;
    }
}

public sealed class UiHint
{
    public HBoxContainer Root;
    public Control Glyph;
    public Button Button;
    public void SetLabel(string text) => Button.Text = text ?? "";
    public bool Visible { get => Root.Visible; set => Root.Visible = value; }
}

// Rodapé de dicas: esquerda = voltar/fechar; direita = alt e confirmar (§3.11).
public sealed class UiHintBar
{
    public HBoxContainer Root;
    public HBoxContainer Left;
    public HBoxContainer Right;

    public UiHint Add(UiAction action, string label, Action onPressed = null, bool left = false)
    {
        var hint = new UiHint { Root = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore } };
        hint.Root.AddThemeConstantOverride("separation", UiMetrics.S1);
        hint.Glyph = UiComponents.ButtonGlyph(action);
        hint.Glyph.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        hint.Button = new Button
        {
            Text = label ?? "",
            ThemeTypeVariation = UiVariation.FlatButton,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        if (onPressed != null)
        {
            hint.Button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            hint.Button.Pressed += onPressed;
            hint.Glyph.MouseFilter = Control.MouseFilterEnum.Stop;
            hint.Glyph.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            hint.Glyph.GuiInput += input =>
            {
                if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) onPressed();
            };
        }
        hint.Root.AddChild(hint.Glyph);
        hint.Root.AddChild(hint.Button);
        (left ? Left : Right).AddChild(hint.Root);
        return hint;
    }

    public void Clear()
    {
        foreach (var box in new[] { Left, Right })
            foreach (var child in box.GetChildren()) { box.RemoveChild(child); child.QueueFree(); }
    }
}

public sealed class UiOptionRow
{
    public PanelContainer Root;
    public Label Label;
    public Control Control;
    public Label Value;
    public HSlider Slider => Control as HSlider;
    public OptionButton Options => Control as OptionButton;
    public CheckButton Toggle => Control as CheckButton;
}

// Fila de notificações em 3 canais (§3.9): alerta de combate, recompensa/info e "+N" de kill.
public sealed class UiToastStack
{
    public Control Root;
    internal PanelContainer Alert;
    internal Label AlertTitle, AlertBody, AlertCount;
    internal VBoxContainer Stack;
    // Topo da pilha de toasts: 132 (slot do alerta/banner livre), 204 (abaixo do alerta), 240 (abaixo do banner de sala limpa).
    internal const float StackTopFree = 132, StackTopBelowAlert = 204, StackTopBelowBanner = 240;
    private bool _bannerActive;
    private readonly Queue<(string Text, Texture2D Icon, bool Reward)> _pending = new();
    private int _visible;
    private Tween _alertTween, _countdownTween;
    private string _alertBody;
    private int _alertShownSecond = -1;

    public void ShowCombatAlert(string title, string body, float countdownSeconds = 0)
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        AlertTitle.Text = title ?? "";
        _alertBody = body ?? "";
        _alertShownSecond = -1;
        UpdateAlertCountdown(countdownSeconds);
        Alert.Visible = true;
        Stack.OffsetTop = _bannerActive ? StackTopBelowBanner : StackTopBelowAlert;
        _alertTween?.Kill();
        _countdownTween?.Kill();
        if (!Root.IsInsideTree()) return;
        if (countdownSeconds > 0)
        {
            // Contagem regressiva separada do fade (o alerta dura ~2 s independentemente).
            _countdownTween = Root.CreateTween();
            _countdownTween.TweenMethod(Callable.From<float>(UpdateAlertCountdown), countdownSeconds, 0.0f, countdownSeconds);
        }
        Alert.Modulate = new Color(1, 1, 1, 0);
        _alertTween = Root.CreateTween();
        _alertTween.TweenProperty(Alert, "modulate:a", 1.0f, UiMotion.WaveAlertIn);
        _alertTween.TweenInterval(UiMotion.WaveAlertHold);
        _alertTween.TweenProperty(Alert, "modulate:a", 0.0f, UiMotion.WaveAlertOut);
        _alertTween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(Root)) return;
            Alert.Visible = false;
            Stack.OffsetTop = _bannerActive ? StackTopBelowBanner : StackTopFree;
        }));
    }

    // Banner de sala limpa ocupa o slot do alerta (y 132): esconde o alerta e desce a pilha; ao terminar, restaura.
    public void SetBannerSlot(bool occupied)
    {
        _bannerActive = occupied;
        if (!GodotObject.IsInstanceValid(Root)) return;
        if (occupied)
        {
            _alertTween?.Kill();
            _countdownTween?.Kill();
            Alert.Visible = false;
            Stack.OffsetTop = StackTopBelowBanner;
        }
        else
        {
            Stack.OffsetTop = Alert.Visible ? StackTopBelowAlert : StackTopFree;
        }
    }

    // Canal (b). reward = borda dourada; senão borda neutra.
    public void ShowToast(string text, Texture2D icon = null, bool reward = true)
    {
        // Fora da árvore CreateTween falha: descarta (mesma guarda de ShowKill/ShowCombatAlert).
        if (!GodotObject.IsInstanceValid(Root) || !Root.IsInsideTree() || string.IsNullOrEmpty(text)) return;
        if (_visible >= UiMotion.ToastMaxVisible)
        {
            if (_pending.Count >= UiMotion.ToastQueueMax) _pending.Dequeue();
            _pending.Enqueue((text, icon, reward));
            return;
        }
        Spawn(text, icon, reward);
    }

    // Canal (c): "+N" ao lado do contador (host = moldura do painel), subindo 16 px. Sem frase.
    // A posição é lida no primeiro passo do tween (quadro seguinte), depois do relayout do host: se o número
    // de moedas ganhou um dígito, o "+N" já sai à direita da moldura nova, sem encostar nos dígitos.
    public void ShowKill(Control host, int amount)
    {
        if (!GodotObject.IsInstanceValid(Root) || host == null || !GodotObject.IsInstanceValid(host) || !Root.IsInsideTree()) return;
        var label = new Label
        {
            Text = $"+{amount}",
            ThemeTypeVariation = UiVariation.HudValueLabel,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        label.AddThemeColorOverride("font_color", UiPalette.Gold);
        Root.AddChild(label);
        var tween = label.CreateTween();
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(label) || !GodotObject.IsInstanceValid(host) || !GodotObject.IsInstanceValid(Root)) return;
            var hostRect = host.GetGlobalRect();
            var origin = Root.GetGlobalRect().Position;
            var height = label.GetCombinedMinimumSize().Y;
            label.Position = new Vector2(hostRect.End.X + UiMetrics.S2, hostRect.Position.Y + (hostRect.Size.Y - height) * 0.5f) - origin;
            label.Modulate = Colors.White;
        }));
        tween.TweenProperty(label, "position:y", -16.0f, UiMotion.KillPopup).AsRelative();
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, UiMotion.KillPopup).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }

    public void Clear()
    {
        _pending.Clear();
        _alertTween?.Kill();
        _countdownTween?.Kill();
        if (!GodotObject.IsInstanceValid(Root)) return;
        Alert.Visible = false;
        _bannerActive = false;
        Stack.OffsetTop = StackTopFree;
        foreach (var child in Stack.GetChildren()) { Stack.RemoveChild(child); child.QueueFree(); }
        _visible = 0;
    }

    private void UpdateAlertCountdown(float remaining)
    {
        var second = Mathf.CeilToInt(remaining);
        if (second == _alertShownSecond) return;
        _alertShownSecond = second;
        // Contagem em rótulo próprio (TextPrimary), separada do corpo secundário ("5 inimigos ·").
        AlertBody.Text = second > 0 && !string.IsNullOrEmpty(_alertBody) ? $"{_alertBody} ·" : _alertBody;
        AlertBody.Visible = !string.IsNullOrEmpty(AlertBody.Text);
        AlertCount.Text = second > 0 ? second.ToString() : "";
        AlertCount.Visible = second > 0;
    }

    private void Spawn(string text, Texture2D icon, bool reward)
    {
        if (!GodotObject.IsInstanceValid(Root) || !Root.IsInsideTree()) return;
        var style = UIThemeFactory.ControlBox(UiPalette.PanelHud, reward ? UiPalette.WindowBorder : UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S3, UiMetrics.S2);
        var toast = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (!UiPixelKit.ApplyPanel(toast, UiMetrics.S3, UiMetrics.S2))
            toast.AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiMetrics.S2);
        if (icon != null)
            row.AddChild(new TextureRect
            {
                Texture = icon, CustomMinimumSize = new Vector2(24, 24),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore
            });
        var width = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, UiFont.Body).X + 4;
        var maxText = 560 - UiMetrics.S3 * 2 - (icon != null ? 32 : 0);
        var minText = 280 - UiMetrics.S3 * 2 - (icon != null ? 32 : 0);
        row.AddChild(new Label
        {
            Text = text, ThemeTypeVariation = UiVariation.HudValueLabel,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(Mathf.Clamp(width, minText, maxText), 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        toast.AddChild(row);
        Stack.AddChild(toast);
        _visible++;
        toast.Modulate = new Color(1, 1, 1, 0);
        var tween = toast.CreateTween();
        tween.TweenProperty(toast, "modulate:a", 1.0f, UiMotion.ToastIn);
        tween.TweenInterval(UiMotion.ToastHold);
        tween.TweenProperty(toast, "modulate:a", 0.0f, UiMotion.ToastOut);
        tween.TweenCallback(Callable.From(() => Finish(toast)));
    }

    private void Finish(PanelContainer toast)
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        if (GodotObject.IsInstanceValid(toast)) { Stack.RemoveChild(toast); toast.QueueFree(); }
        _visible = Mathf.Max(0, _visible - 1);
        if (_pending.Count > 0)
        {
            var next = _pending.Dequeue();
            Spawn(next.Text, next.Icon, next.Reward);
        }
    }
}

// Log de eventos (§3.10): 3 linhas, sem horário; cada linha esmaece após LogFadeAfter. Uso só em debug (decisão da tela).
public sealed class UiEventLog
{
    public PanelContainer Root;
    internal VBoxContainer Lines;
    public int MaxLines = 3;

    public void Append(string text)
    {
        if (!GodotObject.IsInstanceValid(Root) || string.IsNullOrEmpty(text)) return;
        while (Lines.GetChildCount() >= MaxLines)
        {
            var oldest = Lines.GetChild(0);
            Lines.RemoveChild(oldest);
            oldest.QueueFree();
        }
        var line = new Label
        {
            Text = text, ThemeTypeVariation = UiVariation.SecondaryLabel,
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        Lines.AddChild(line);
        Root.Visible = true;
        if (!line.IsInsideTree()) return;
        var tween = line.CreateTween();
        tween.TweenInterval(UiMotion.LogFadeAfter);
        tween.TweenProperty(line, "modulate:a", 0.0f, 0.4f);
        tween.TweenCallback(Callable.From(() => RemoveLine(line)));
    }

    public void Clear()
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        foreach (var child in Lines.GetChildren()) { Lines.RemoveChild(child); child.QueueFree(); }
        Root.Visible = false;
    }

    private void RemoveLine(Label line)
    {
        if (!GodotObject.IsInstanceValid(Root)) return;
        if (GodotObject.IsInstanceValid(line) && line.GetParent() == Lines) { Lines.RemoveChild(line); line.QueueFree(); }
        Root.Visible = Lines.GetChildCount() > 0;
    }
}

public sealed class UiDevSection
{
    public PanelContainer Root;
    public VBoxContainer Body;
}

public static class UiComponents
{
    // ---------- Texto ----------

    // Texto localizado com fallback quando a chave não existe em data/localization/<idioma>/*.json.
    // Texto novo de UI usa Localization.Text direto; TextOr fica para chaves opcionais.
    public static string TextOr(string key, string fallback)
    {
        return Localization.Has(key) ? Localization.Text(key) : fallback;
    }

    public static Label MakeLabel(string text, string variation = UiVariation.BodyLabel, bool autowrap = false)
    {
        var label = new Label { Text = text ?? "", ThemeTypeVariation = variation };
        if (autowrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    // ---------- Raridade ----------

    // API comum (substitui BlessingRarityColor duplicado em HUD.BlessingOffer e IntermissionChamber).
    public static Color RarityColor(string rarity) => UiPalette.Rarity(rarity);

    public static string RarityLabel(string rarity)
    {
        var id = (rarity ?? "").Trim().ToLowerInvariant();
        return id switch
        {
            "uncommon" or "incomum" => Localization.Text("ui.rarity.uncommon"),
            "rare" or "raro" or "rara" => Localization.Text("ui.rarity.rare"),
            "epic" or "épico" or "épica" => Localization.Text("ui.rarity.epic"),
            "legendary" or "lendário" or "lendária" => Localization.Text("ui.rarity.legendary"),
            "mythic" or "mítico" or "mítica" => Localization.Text("ui.rarity.mythic"),
            "stirps" => Localization.Text("ui.rarity.stirps"),
            _ => Localization.Text("ui.rarity.common")
        };
    }

    // ---------- Janela e confirmação (§3.1, §3.3) ----------

    public static UiWindow Window(Control parent, string name, WindowSize size, string title, string subtitle = null)
    {
        var window = new UiWindow { Size = size };
        window.Root = new ColorRect
        {
            Name = name ?? "Window",
            Color = UiPalette.Scrim,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = Node.ProcessModeEnum.Always
        };
        window.Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        window.Panel = new PanelContainer { ThemeTypeVariation = UiVariation.WindowPanel };
        window.Panel.AddThemeStyleboxOverride("panel", UiPixelKit.WindowFrame() ?? UIThemeFactory.WindowBox());
        // 2D: molduras sem borrar (o filtro padrão do projeto é linear); os filhos herdam.
        if (UiPixelKit.Enabled)
            window.Panel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        PlaceWindowPanel(window.Panel, size);
        window.Root.AddChild(window.Panel);

        window.Content = new VBoxContainer();
        window.Content.AddThemeConstantOverride("separation", UiMetrics.S3);
        window.Panel.AddChild(window.Content);

        // Cabeçalho: título centrado, HeaderRight (moedas etc.) à direita.
        var titleRow = new Control { CustomMinimumSize = new Vector2(0, 40), MouseFilter = Control.MouseFilterEnum.Ignore };
        window.Title = new Label
        {
            Text = title ?? "", ThemeTypeVariation = UiVariation.TitleLabel,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        window.Title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        titleRow.AddChild(window.Title);
        window.HeaderRight = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        window.HeaderRight.AddThemeConstantOverride("separation", UiMetrics.S2);
        window.HeaderRight.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        window.HeaderRight.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(window.HeaderRight);
        window.Content.AddChild(titleRow);

        window.Status = new Label
        {
            ThemeTypeVariation = UiVariation.SecondaryLabel,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        window.Content.AddChild(window.Status);
        window.SetStatus(subtitle);
        window.Content.AddChild(Divider());

        window.Body = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        window.Body.AddThemeConstantOverride("separation", UiMetrics.S3);
        window.Content.AddChild(window.Body);

        window.Actions = new HBoxContainer { Visible = false };
        window.Actions.AddThemeConstantOverride("separation", UiMetrics.S3);
        window.Content.AddChild(window.Actions);

        window.FooterLine = Separator();
        window.Content.AddChild(window.FooterLine);
        window.Footer = HintBar();
        window.Content.AddChild(window.Footer.Root);

        parent?.AddChild(window.Root);
        return window;
    }

    // Diálogo de confirmação. Foco inicial em Cancelar; ui_cancel = Cancelar; o foco volta a quem estava focado.
    // Integração com a pilha modal fica com a tela (Push(confirm.Window.Root, ..., onCancel: confirm.Cancel)).
    public static UiConfirm Confirm(Control parent, string title, string body, string confirmText, Action onConfirm, ConfirmKind kind = ConfirmKind.Destructive, Action onCancel = null)
    {
        var confirm = new UiConfirm
        {
            OnConfirm = onConfirm,
            OnCancel = onCancel,
            ReturnFocus = parent?.GetViewport()?.GuiGetFocusOwner()
        };
        // Empilhado sobre outra tela modal (pilha aberta ou pai sob um scrim): scrim leve, a tela de baixo já escurece o jogo.
        var stacked = UiModalStack.AnyOpen || IsUnderModalScrim(parent);
        var window = Window(parent, "ConfirmDialog", WindowSize.Small, title);
        confirm.Window = window;
        // Painel 100% opaco: o texto da tela de baixo (menu de pausa etc.) não pode vazar pelo corpo do modal.
        // (a moldura em pixel art do 2D já tem o miolo opaco)
        if (!UiPixelKit.Enabled)
        {
            var opaqueBox = UIThemeFactory.WindowBox();
            opaqueBox.BgColor = new Color(UiPalette.Panel, 1.0f);
            window.Panel.AddThemeStyleboxOverride("panel", opaqueBox);
        }
        if (stacked) window.Root.Color = new Color(UiPalette.Scrim, StackedConfirmScrimAlpha);
        window.Title.ThemeTypeVariation = UiVariation.SectionGoldLabel;
        window.Body.AddChild(new Label
        {
            Text = body ?? "", ThemeTypeVariation = UiVariation.BodyLabel,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(512, 0)
        });
        if (kind == ConfirmKind.Destructive)
            window.Body.AddChild(new Label
            {
                Text = Localization.Text("ui.confirm.irreversible"),
                ThemeTypeVariation = UiVariation.SecondaryLabel,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        confirm.CancelButton = MenuButton(Localization.Text("ui.common.cancel"), confirm.Cancel, ButtonVariant.Secondary, ButtonSize.Medium);
        confirm.ConfirmButton = MenuButton(
            string.IsNullOrEmpty(confirmText) ? Localization.Text("ui.common.confirm") : confirmText,
            confirm.Accept,
            kind == ConfirmKind.Destructive ? ButtonVariant.Destructive : ButtonVariant.Primary,
            ButtonSize.Medium);
        window.Actions.Visible = true;
        window.Actions.AddChild(confirm.CancelButton);
        window.Actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        window.Actions.AddChild(confirm.ConfirmButton);
        // Rodapé (§3.3/§3.11): voltar à esquerda, aceitar à direita; glifos seguem o último dispositivo.
        // Aceitar ativa o botão focado (o foco inicial é Cancelar), por isso o rótulo é "Escolher" e não "Confirmar".
        window.Footer.Add(UiAction.Cancel, Localization.Text("ui.common.cancel"), confirm.Cancel, left: true);
        window.Footer.Add(UiAction.Accept, Localization.Text("ui.common.choose"));
        window.Root.GuiInput += input =>
        {
            if (!input.IsActionPressed("ui_cancel")) return;
            window.Root.AcceptEvent();
            confirm.Cancel();
        };
        if (window.Root.IsInsideTree())
        {
            window.FadeIn();
            confirm.CancelButton.CallDeferred(Control.MethodName.GrabFocus);
        }
        else
        {
            window.Root.Ready += () => confirm.CancelButton.CallDeferred(Control.MethodName.GrabFocus);
        }
        return confirm;
    }

    // Opacidade do scrim do Confirm quando já existe uma tela modal por baixo (evita dois scrims de 0,85).
    private const float StackedConfirmScrimAlpha = 0.6f;

    private static bool IsUnderModalScrim(Node node)
    {
        for (var current = node; current != null; current = current.GetParent())
            if (current is ColorRect rect && rect.Color == UiPalette.Scrim && rect.IsVisibleInTree())
                return true;
        return false;
    }

    // ---------- Botões (§3.2) ----------

    public static Button MenuButton(string text, Action onPressed, ButtonVariant variant = ButtonVariant.Secondary, ButtonSize size = ButtonSize.Medium)
    {
        var button = new Button
        {
            Text = text ?? "",
            ThemeTypeVariation = variant switch
            {
                ButtonVariant.Primary => UiVariation.PrimaryButton,
                ButtonVariant.Destructive => UiVariation.DestructiveButton,
                ButtonVariant.Flat => UiVariation.FlatButton,
                _ => UiVariation.SecondaryButton
            },
            CustomMinimumSize = size switch
            {
                ButtonSize.Large => new Vector2(420, UiMetrics.ButtonL),
                ButtonSize.Small => new Vector2(120, UiMetrics.ButtonS),
                _ => new Vector2(200, UiMetrics.ButtonM)
            },
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        if (variant == ButtonVariant.Flat) button.CustomMinimumSize = new Vector2(0, button.CustomMinimumSize.Y);
        button.AddThemeFontSizeOverride("font_size", size == ButtonSize.Small ? UiFont.Secondary : UiFont.Body);
        if (onPressed != null) button.Pressed += onPressed;
        return button;
    }

    // ---------- Card de escolha (§3.4) ----------

    public static UiChoiceCard ChoiceCard(ChoiceCardData data, Action onChoose)
    {
        data ??= new ChoiceCardData();
        var card = new UiChoiceCard { Data = data, OnChoose = onChoose };
        card.Accent = data.Accent ?? (string.IsNullOrEmpty(data.RarityId) ? UiPalette.Border : UiPalette.Rarity(data.RarityId));
        var readOnly = string.IsNullOrEmpty(data.ActionText) || onChoose == null;
        var wholeCard = data.WholeCardAction && !readOnly;
        card.Root = new PanelContainer
        {
            CustomMinimumSize = new Vector2(Mathf.Max(0, data.Width), 132),
            SizeFlagsHorizontal = data.Width > 0 ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = readOnly || data.Disabled ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand,
            FocusMode = readOnly ? Control.FocusModeEnum.All : Control.FocusModeEnum.None
        };
        if (readOnly) card.Root.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        Control container = card.Root;
        // Stirps: linha interna 1 px Gold (marca de forma além da cor), desenhada por cima sem alterar o layout
        // (antes um PanelContainer interno com margem deslocava o botão ~8 px em relação aos outros cards).
        card.Root.Draw += card.DrawOverlay;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiMetrics.S4);
        container.AddChild(row);
        card.FocusBar = new ColorRect { CustomMinimumSize = new Vector2(6, 0), Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(card.FocusBar);

        var iconFrame = new PanelContainer
        {
            ThemeTypeVariation = UiVariation.InsetPanel,
            CustomMinimumSize = new Vector2(104, 104),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconFrame.AddThemeStyleboxOverride("panel", (StyleBox)UiPixelKit.Frame(UiPalette.Border, UiPalette.PanelInset, 6, 6) ?? UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, 6, 6));
        iconFrame.AddChild(IconOrMonogram(data.Icon, data.IconText ?? data.Title, UiMetrics.IconXL));
        row.AddChild(iconFrame);

        var textColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        textColumn.AddThemeConstantOverride("separation", UiMetrics.S1);
        row.AddChild(textColumn);
        var titleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        titleRow.AddThemeConstantOverride("separation", UiMetrics.S2);
        var title = new Label
        {
            Text = data.Title ?? "", ThemeTypeVariation = UiVariation.SectionLabel,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        if (data.Disabled) title.AddThemeColorOverride("font_color", UiPalette.TextDisabled);
        titleRow.AddChild(title);
        var rarityText = data.RarityLabel ?? (string.IsNullOrEmpty(data.RarityId) ? null : RarityLabel(data.RarityId));
        if (!string.IsNullOrEmpty(rarityText))
        {
            var badge = Badge(rarityText, data.Disabled ? UiPalette.PanelInset : card.Accent);
            badge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            titleRow.AddChild(badge);
        }
        if (!string.IsNullOrEmpty(data.Tag))
        {
            var tag = Badge(data.Tag, data.TagColor ?? UiPalette.Gold);
            tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            titleRow.AddChild(tag);
        }
        textColumn.AddChild(titleRow);
        if (!string.IsNullOrEmpty(data.Meta))
            textColumn.AddChild(new Label { Text = data.Meta, ThemeTypeVariation = UiVariation.SecondaryLabel, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore });
        if (!string.IsNullOrEmpty(data.BodyBbcode))
            textColumn.AddChild(CardRichText(data.BodyBbcode, data.Disabled));
        if (!string.IsNullOrEmpty(data.PreviewBbcode))
            textColumn.AddChild(CardRichText(data.PreviewBbcode, data.Disabled));
        if (!string.IsNullOrEmpty(data.Warning))
            textColumn.AddChild(new Label { Text = data.Warning, ThemeTypeVariation = UiVariation.WarnLabel, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore });

        if (wholeCard)
        {
            // Sem botão visível: um Button invisível cobre o card só para foco de teclado/controle (Enter escolhe) e
            // ignora o mouse, que segue no próprio card (hover e clique abaixo).
            card.ActionButton = new Button
            {
                Text = "", Flat = true, Disabled = data.Disabled,
                FocusMode = Control.FocusModeEnum.All,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
                card.ActionButton.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            card.ActionButton.FocusNeighborLeft = new NodePath(".");
            card.ActionButton.FocusNeighborRight = new NodePath(".");
            card.ActionButton.Pressed += card.Choose;
            card.Root.AddChild(card.ActionButton);
            card.ActionButton.FocusEntered += () => { card.Focused = true; card.Refresh(); };
            card.ActionButton.FocusExited += () => { card.Focused = false; card.Refresh(); };
        }
        else if (!readOnly)
        {
            var actionColumn = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            actionColumn.AddThemeConstantOverride("separation", UiMetrics.S1);
            card.ActionButton = MenuButton(data.ActionText, card.Choose, ButtonVariant.Primary, ButtonSize.Medium);
            card.ActionButton.CustomMinimumSize = new Vector2(148, 82);
            card.ActionButton.Disabled = data.Disabled;
            // Evita saltos geométricos: esquerda/direita ficam no próprio botão.
            card.ActionButton.FocusNeighborLeft = new NodePath(".");
            card.ActionButton.FocusNeighborRight = new NodePath(".");
            actionColumn.AddChild(card.ActionButton);
            if (!string.IsNullOrEmpty(data.CostText))
            {
                var cost = new Label { Text = data.CostText, ThemeTypeVariation = UiVariation.SecondaryLabel, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
                if (data.CostColor.HasValue) cost.AddThemeColorOverride("font_color", data.CostColor.Value);
                actionColumn.AddChild(cost);
            }
            if (data.Disabled && !string.IsNullOrEmpty(data.DisabledReason))
                actionColumn.AddChild(new Label
                {
                    Text = data.DisabledReason, ThemeTypeVariation = UiVariation.WarnLabel,
                    HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(148, 0), MouseFilter = Control.MouseFilterEnum.Ignore
                });
            row.AddChild(actionColumn);
            card.ActionButton.FocusEntered += () => { card.Focused = true; card.Refresh(); };
            card.ActionButton.FocusExited += () => { card.Focused = false; card.Refresh(); };
        }
        else
        {
            card.Root.FocusEntered += () => { card.Focused = true; card.Refresh(); };
            card.Root.FocusExited += () => { card.Focused = false; card.Refresh(); };
        }
        card.Root.MouseEntered += () => { card.Hovered = true; card.Refresh(); };
        card.Root.MouseExited += () => { card.Hovered = false; card.Refresh(); };
        card.Root.GuiInput += input =>
        {
            if (readOnly || input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            card.Root.AcceptEvent();
            card.Choose();
        };
        card.Refresh();
        return card;
    }

    private static RichTextLabel CardRichText(string bbcode, bool disabled)
    {
        var text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            Text = bbcode,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        if (disabled) text.AddThemeColorOverride("default_color", UiPalette.TextDisabled);
        return text;
    }

    // Ícone ou monograma (2 letras em Section Gold sobre disco PanelInset).
    public static Control IconOrMonogram(Texture2D icon, string name, int size)
    {
        if (icon != null)
            return new TextureRect
            {
                Texture = icon, CustomMinimumSize = new Vector2(size, size),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
        return Monogram(name, size);
    }

    public static Control Monogram(string name, int size)
    {
        var disc = new Panel
        {
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var style = new StyleBoxFlat { BgColor = UiPalette.PanelInset, BorderColor = UiPalette.GoldDim };
        style.SetBorderWidthAll(UiMetrics.BorderThin);
        style.SetCornerRadiusAll(size / 2);
        disc.AddThemeStyleboxOverride("panel", style);
        var label = new Label
        {
            Text = MonogramText(name), ThemeTypeVariation = UiVariation.SectionGoldLabel,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        if (size < 40) label.AddThemeFontSizeOverride("font_size", UiFont.Badge);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        disc.AddChild(label);
        return disc;
    }

    public static string MonogramText(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var words = name.Trim().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var significant = new List<string>();
        foreach (var word in words)
            if (word.Length > 2 || words.Length == 1) significant.Add(word);
        if (significant.Count >= 2) return $"{char.ToUpperInvariant(significant[0][0])}{char.ToUpperInvariant(significant[1][0])}";
        var single = significant.Count == 1 ? significant[0] : words[0];
        return single.Length >= 2 ? $"{char.ToUpperInvariant(single[0])}{char.ToLowerInvariant(single[1])}" : single.ToUpperInvariant();
    }

    // ---------- Barras de recurso (§3.5) ----------

    // Health 360x22 · Fury 360x10 · Boss largura do container (âncoras 0.25..0.75 pela tela) x 18.
    public static UiResourceBar ResourceBar(ResourceBarKind kind, float width = 0)
    {
        var bar = new UiResourceBar { Kind = kind };
        var height = kind switch { ResourceBarKind.Fury => 10, ResourceBarKind.Boss => 18, _ => 22 };
        // A moldura em pixel art do 2D come 6 px de cada lado: vida e chefe ganham altura para o miolo continuar legível.
        if (UiPixelKit.Enabled && kind != ResourceBarKind.Fury)
            height = 26;
        if (width <= 0 && kind != ResourceBarKind.Boss) width = 360;
        bar.Bar = new Control
        {
            Name = kind + "Bar",
            CustomMinimumSize = new Vector2(Mathf.Max(width, 0), height),
            SizeFlagsHorizontal = width > 0 ? Control.SizeFlags.ShrinkBegin : Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        bar.Bar.Draw += bar.Draw;
        bar.Bar.Resized += bar.Bar.QueueRedraw;
        switch (kind)
        {
            case ResourceBarKind.Health:
            {
                var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                row.AddThemeConstantOverride("separation", UiMetrics.S2);
                row.AddChild(bar.Bar);
                // Valor FORA da barra (legível sobre chip e barreira): coluna fixa de 56, Body 17, outline 5 (HudValueLabel).
                bar.ValueLabel = new Label
                {
                    ThemeTypeVariation = UiVariation.HudValueLabel,
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    CustomMinimumSize = new Vector2(56, 0),
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore
                };
                row.AddChild(bar.ValueLabel);
                // "+N" da barreira: largura reservada (40) para o painel não mudar de largura; some só pelo texto vazio.
                bar.BarrierLabel = new Label
                {
                    ThemeTypeVariation = UiVariation.HudValueLabel,
                    CustomMinimumSize = new Vector2(40, 0),
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore
                };
                bar.BarrierLabel.AddThemeColorOverride("font_color", UiPalette.Restore);
                row.AddChild(bar.BarrierLabel);
                bar.Root = row;
                break;
            }
            case ResourceBarKind.Fury:
            {
                var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                column.AddThemeConstantOverride("separation", UiMetrics.S1);
                var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                header.AddThemeConstantOverride("separation", UiMetrics.S2);
                bar.FuryBadge = Badge(Localization.Text("hud.fury"), UiPalette.PanelInset, UiPalette.Fury);
                header.AddChild(bar.FuryBadge);
                bar.FuryGlyph = ButtonGlyph(UiAction.Fury);
                bar.FuryGlyph.Visible = false;
                header.AddChild(bar.FuryGlyph);
                column.AddChild(header);
                column.AddChild(bar.Bar);
                bar.Root = column;
                bar.Bar.TreeEntered += () =>
                {
                    // Reinicia o pulso se o estado "cheia" foi definido antes de entrar na árvore.
                    var ready = bar.FuryGlyph.Visible;
                    if (ready) { bar.SetFuryState(false, false); bar.SetFuryState(true, false); }
                };
                break;
            }
            default:
            {
                var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                column.AddThemeConstantOverride("separation", UiMetrics.S1);
                var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                bar.NameLabel = new Label { ThemeTypeVariation = UiVariation.SectionGoldLabel, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
                bar.ValueLabel = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
                // Valor do chefe é informação essencial: TextPrimary no tamanho Secondary.
                bar.ValueLabel.AddThemeColorOverride("font_color", UiPalette.TextPrimary);
                header.AddChild(bar.NameLabel);
                header.AddChild(bar.ValueLabel);
                column.AddChild(header);
                column.AddChild(bar.Bar);
                bar.Root = column;
                break;
            }
        }
        if (kind != ResourceBarKind.Fury) bar.SetValue(1, 1);
        else bar.SetValue(0, 1);
        return bar;
    }

    // ---------- Painel de detalhes, lista, abas (§3.6–3.8) ----------

    public static UiDetailPanel DetailPanel(DetailPanelDock dock)
    {
        var panel = new UiDetailPanel();
        panel.Root = new PanelContainer
        {
            ThemeTypeVariation = UiVariation.RaisedPanel,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = dock == DetailPanelDock.Right ? new Vector2(380, 0) : new Vector2(0, 150),
            SizeFlagsHorizontal = dock == DetailPanelDock.Right ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = dock == DetailPanelDock.Right ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill
        };
        var stack = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.Root.AddChild(stack);
        panel.Empty = new Label
        {
            Text = Localization.Text("ui.common.select_item"), ThemeTypeVariation = UiVariation.SecondaryLabel,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        panel.Empty.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        stack.AddChild(panel.Empty);

        BoxContainer content = dock == DetailPanelDock.Right ? new VBoxContainer() : new HBoxContainer();
        content.AddThemeConstantOverride("separation", UiMetrics.S3);
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        stack.AddChild(content);

        panel.Header = new HBoxContainer();
        panel.Header.AddThemeConstantOverride("separation", UiMetrics.S3);
        panel.IconSlot = new CenterContainer { CustomMinimumSize = new Vector2(UiMetrics.IconL, UiMetrics.IconL), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.Header.AddChild(panel.IconSlot);
        var headerText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        headerText.AddThemeConstantOverride("separation", UiMetrics.S1);
        panel.Title = new Label { ThemeTypeVariation = UiVariation.SectionGoldLabel, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        panel.Badges = new HBoxContainer();
        panel.Badges.AddThemeConstantOverride("separation", UiMetrics.S1);
        panel.Meta = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        headerText.AddChild(panel.Title);
        headerText.AddChild(panel.Badges);
        headerText.AddChild(panel.Meta);
        panel.Header.AddChild(headerText);
        if (dock == DetailPanelDock.Bottom) panel.Header.CustomMinimumSize = new Vector2(360, 0);
        content.AddChild(panel.Header);

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", UiMetrics.S2);
        content.AddChild(right);
        panel.Scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };
        right.AddChild(panel.Scroll);
        var scrollContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scrollContent.AddThemeConstantOverride("separation", UiMetrics.S2);
        panel.Scroll.AddChild(scrollContent);
        panel.Body = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };
        scrollContent.AddChild(panel.Body);
        panel.Flavor = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        scrollContent.AddChild(panel.Flavor);
        panel.ActionRow = new HBoxContainer();
        panel.ActionRow.AddThemeConstantOverride("separation", UiMetrics.S2);
        panel.ActionLabel = new Label
        {
            ThemeTypeVariation = UiVariation.BodyLabel,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        panel.ActionLabel.AddThemeColorOverride("font_color", UiPalette.Gold);
        panel.ActionRow.AddChild(panel.ActionLabel);
        right.AddChild(panel.ActionRow);
        panel.Clear();
        return panel;
    }

    // Lista rolável: segue o foco, sem rolagem horizontal, VBox com separação 12.
    public static ScrollContainer ScrollList(float minHeight, out VBoxContainer list)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, minHeight),
            FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", UiMetrics.S3);
        scroll.AddChild(list);
        return scroll;
    }

    // Lista sem itens focáveis (créditos, texto longo): o ScrollContainer recebe foco e rola 48 px por ui_up/ui_down.
    // No fim da rolagem o evento passa adiante (o foco sai da lista normalmente).
    public static void MakeScrollFocusable(ScrollContainer scroll, float step = 48)
    {
        scroll.FocusMode = Control.FocusModeEnum.All;
        scroll.GuiInput += input =>
        {
            var up = input.IsActionPressed("ui_up", true) || (InputMap.HasAction("ui_scroll_up") && input.IsActionPressed("ui_scroll_up", true));
            var down = input.IsActionPressed("ui_down", true) || (InputMap.HasAction("ui_scroll_down") && input.IsActionPressed("ui_scroll_down", true));
            if (!up && !down) return;
            var bar = scroll.GetVScrollBar();
            var limit = Mathf.Max(0, (float)(bar.MaxValue - bar.Page));
            var before = scroll.ScrollVertical;
            var target = Mathf.Clamp(before + (down ? step : -step), 0, limit);
            if (Mathf.RoundToInt(target) == before) return;
            scroll.ScrollVertical = Mathf.RoundToInt(target);
            scroll.AcceptEvent();
        };
    }

    // Abas com ButtonGroup; glifos L1/R1 (Q/E) nas pontas. A tela chama tabs.HandleInput(evento) no seu _Input (ver UiTabs.HandleInput).
    public static UiTabs Tabs(string[] labels, Action<int> onChanged, bool showGlyphs = true)
    {
        var tabs = new UiTabs { Changed = onChanged, Root = new HBoxContainer() };
        tabs.Root.TreeEntered += tabs.Arm;
        tabs.Root.VisibilityChanged += () => { if (tabs.Root.Visible) tabs.Arm(); };
        tabs.Root.AddThemeConstantOverride("separation", UiMetrics.S1);
        if (showGlyphs) tabs.Root.AddChild(ClickableGlyph(UiAction.TabPrev, tabs.Previous));
        var group = new ButtonGroup { AllowUnpress = false };
        for (var i = 0; i < (labels?.Length ?? 0); i++)
        {
            var index = i;
            var button = new Button
            {
                Text = labels[i] ?? "",
                ThemeTypeVariation = UiVariation.TabButton,
                ToggleMode = true,
                ButtonGroup = group,
                CustomMinimumSize = new Vector2(140, UiMetrics.TabHeight),
                FocusMode = Control.FocusModeEnum.All,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            button.Pressed += () => tabs.Select(index);
            tabs.Buttons.Add(button);
            tabs.Root.AddChild(button);
        }
        if (showGlyphs) tabs.Root.AddChild(ClickableGlyph(UiAction.TabNext, tabs.Next));
        if (tabs.Buttons.Count > 0) tabs.Select(0, false);
        return tabs;
    }

    private static Control ClickableGlyph(UiAction action, Action onPressed)
    {
        var glyph = ButtonGlyph(action);
        glyph.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        glyph.MouseFilter = Control.MouseFilterEnum.Stop;
        glyph.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        glyph.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) onPressed();
        };
        return glyph;
    }

    // ---------- Notificações e log (§3.9, §3.10) ----------

    public static UiToastStack ToastStack(Control parent)
    {
        var toasts = new UiToastStack
        {
            Root = new Control { Name = "ToastStack", MouseFilter = Control.MouseFilterEnum.Ignore }
        };
        toasts.Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        toasts.Alert = new PanelContainer
        {
            Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -260, OffsetRight = 260, OffsetTop = 132, OffsetBottom = 196,
            GrowHorizontal = Control.GrowDirection.Both
        };
        var alertStyle = UIThemeFactory.ControlBox(UiPalette.DangerBg, UiPalette.Danger, 0, UiMetrics.S4, UiMetrics.S2);
        alertStyle.BorderWidthLeft = 6;
        alertStyle.ContentMarginLeft = UiMetrics.S4 + 6;
        toasts.Alert.AddThemeStyleboxOverride("panel", alertStyle);
        var alertColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        alertColumn.AddThemeConstantOverride("separation", 0);
        toasts.AlertTitle = new Label { ThemeTypeVariation = UiVariation.SectionLabel, MouseFilter = Control.MouseFilterEnum.Ignore };
        toasts.AlertBody = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, MouseFilter = Control.MouseFilterEnum.Ignore };
        toasts.AlertCount = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        toasts.AlertCount.AddThemeColorOverride("font_color", UiPalette.TextPrimary);
        var alertBodyRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        alertBodyRow.AddThemeConstantOverride("separation", UiMetrics.S1);
        alertBodyRow.AddChild(toasts.AlertBody);
        alertBodyRow.AddChild(toasts.AlertCount);
        alertColumn.AddChild(toasts.AlertTitle);
        alertColumn.AddChild(alertBodyRow);
        toasts.Alert.AddChild(alertColumn);
        toasts.Root.AddChild(toasts.Alert);

        toasts.Stack = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -280, OffsetRight = 280, OffsetTop = 132,
            GrowHorizontal = Control.GrowDirection.Both
        };
        toasts.Stack.AddThemeConstantOverride("separation", UiMetrics.S2);
        toasts.Root.AddChild(toasts.Stack);
        parent?.AddChild(toasts.Root);
        return toasts;
    }

    // Canto inferior direito (−24/−24), 360 x 3 linhas, PanelHud a 0.90.
    public static UiEventLog EventLog(Control parent)
    {
        var log = new UiEventLog
        {
            Root = new PanelContainer
            {
                Name = "EventLog", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 1, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
                OffsetLeft = -UiMetrics.SafeMargin - 360, OffsetRight = -UiMetrics.SafeMargin,
                OffsetTop = -UiMetrics.SafeMargin, OffsetBottom = -UiMetrics.SafeMargin,
                GrowHorizontal = Control.GrowDirection.Begin, GrowVertical = Control.GrowDirection.Begin
            }
        };
        log.Root.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(new Color(UiPalette.PanelHud, 0.90f), UiPalette.Border, UiMetrics.BorderThin, UiMetrics.S3, UiMetrics.S2));
        log.Lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        log.Lines.AddThemeConstantOverride("separation", 2);
        log.Root.AddChild(log.Lines);
        parent?.AddChild(log.Root);
        return log;
    }

    // ---------- Rodapé de dicas e glifos (§3.11) ----------

    public static UiHintBar HintBar()
    {
        var bar = new UiHintBar
        {
            Root = new HBoxContainer { CustomMinimumSize = new Vector2(0, UiMetrics.FooterHeight), MouseFilter = Control.MouseFilterEnum.Ignore },
            Left = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore },
            Right = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End }
        };
        bar.Left.AddThemeConstantOverride("separation", UiMetrics.S5);
        bar.Right.AddThemeConstantOverride("separation", UiMetrics.S5);
        bar.Root.AddChild(bar.Left);
        bar.Root.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        bar.Root.AddChild(bar.Right);
        return bar;
    }

    // Glifo que segue o último dispositivo (UiInput.LastDevice) e troca sem reabrir a tela.
    public static Control ButtonGlyph(UiAction action) => CreateGlyph(action, UiInput.LastDevice, true);

    // Glifo fixo para um dispositivo (galeria, preview com --ui-device).
    public static Control ButtonGlyph(UiAction action, InputDevice device) => CreateGlyph(action, device, false);

    private enum PadShape { None, Cross, Circle, Square, Triangle }

    private sealed class GlyphState
    {
        public Control Node;
        public UiAction Action;
        public InputDevice Device;
        public string Text;
        public PadShape Shape;
        public bool Keycap;
        public float Width = UiMetrics.IconGlyph;
        public Action<InputDevice> Handler;
        public bool Subscribed;
        public StyleBoxFlat Style;
        public bool StyleIsKeycap;
    }

    private static Control CreateGlyph(UiAction action, InputDevice device, bool follow)
    {
        var state = new GlyphState
        {
            Action = action,
            Device = device,
            Node = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Name = "Glyph" + action }
        };
        ResolveGlyph(state);
        state.Node.Draw += () => DrawGlyph(state);
        if (follow)
        {
            state.Handler = newDevice =>
            {
                if (!GodotObject.IsInstanceValid(state.Node))
                {
                    if (state.Subscribed) { UiInput.DeviceChanged -= state.Handler; state.Subscribed = false; }
                    return;
                }
                state.Device = newDevice;
                ResolveGlyph(state);
            };
            state.Node.TreeEntered += () =>
            {
                if (!state.Subscribed) { UiInput.DeviceChanged += state.Handler; state.Subscribed = true; }
                if (state.Device != UiInput.LastDevice) { state.Device = UiInput.LastDevice; ResolveGlyph(state); }
            };
            state.Node.TreeExiting += () =>
            {
                if (state.Subscribed) { UiInput.DeviceChanged -= state.Handler; state.Subscribed = false; }
            };
        }
        return state.Node;
    }

    private static void ResolveGlyph(GlyphState state)
    {
        state.Shape = PadShape.None;
        state.Keycap = state.Device == InputDevice.KeyboardMouse;
        state.Text = state.Device switch
        {
            InputDevice.Xbox => state.Action switch
            {
                UiAction.Accept or UiAction.Interact => "A",
                UiAction.Cancel => "B",
                UiAction.Alt => "Y",
                UiAction.TabPrev => "LB",
                UiAction.TabNext or UiAction.Fury => "RB",
                UiAction.ZoomOut => "LT",
                UiAction.ZoomIn => "RT",
                UiAction.Scroll => "RS",
                UiAction.CharacterSheet => "View",
                UiAction.Pause => "Menu",
                _ => "?"
            },
            InputDevice.PlayStation => state.Action switch
            {
                UiAction.TabPrev => "L1",
                UiAction.TabNext or UiAction.Fury => "R1",
                UiAction.ZoomOut => "L2",
                UiAction.ZoomIn => "R2",
                UiAction.Scroll => "RS",
                UiAction.CharacterSheet => "Create",
                UiAction.Pause => "Options",
                _ => null
            },
            _ => state.Action switch
            {
                UiAction.Accept => "Enter",
                UiAction.Cancel or UiAction.Pause => "Esc",
                UiAction.Alt => "R",
                UiAction.TabPrev => "Q",
                UiAction.TabNext or UiAction.Interact => "E",
                UiAction.ZoomOut => "-",
                UiAction.ZoomIn => "=",
                UiAction.Scroll => "Mouse",
                UiAction.CharacterSheet => "C",
                UiAction.Fury => "F",
                _ => "?"
            }
        };
        if (state.Device == InputDevice.PlayStation && state.Text == null)
            state.Shape = state.Action switch
            {
                UiAction.Accept or UiAction.Interact => PadShape.Cross,
                UiAction.Cancel => PadShape.Circle,
                UiAction.Alt => PadShape.Triangle,
                _ => PadShape.Square
            };
        state.Width = UiMetrics.IconGlyph;
        if (state.Shape == PadShape.None && !string.IsNullOrEmpty(state.Text))
        {
            var textWidth = ThemeDB.FallbackFont.GetStringSize(state.Text, HorizontalAlignment.Left, -1, UiFont.Badge).X;
            state.Width = Mathf.Max(UiMetrics.IconGlyph, Mathf.Ceil(textWidth) + (state.Text.Length > 1 ? 12 : 8));
        }
        state.Node.CustomMinimumSize = new Vector2(state.Width, UiMetrics.IconGlyph);
        state.Node.QueueRedraw();
    }

    private static void DrawGlyph(GlyphState state)
    {
        var node = state.Node;
        var size = (float)UiMetrics.IconGlyph;
        var origin = new Vector2(Mathf.Floor((node.Size.X - state.Width) * 0.5f), Mathf.Floor((node.Size.Y - size) * 0.5f));
        var rect = new Rect2(origin, new Vector2(state.Width, size));
        if (state.Style == null || state.StyleIsKeycap != state.Keycap)
        {
            // Teclado = keycap r3 com base mais grossa; controle = disco/pílula.
            state.Style = new StyleBoxFlat { BgColor = UiPalette.PanelInset, BorderColor = UiPalette.TextSecondary, AntiAliasing = true };
            state.Style.SetBorderWidthAll(1);
            state.Style.SetCornerRadiusAll(state.Keycap ? UiMetrics.RadiusBadge : UiMetrics.IconGlyph / 2);
            if (state.Keycap) state.Style.BorderWidthBottom = 2;
            state.StyleIsKeycap = state.Keycap;
        }
        node.DrawStyleBox(state.Style, rect);
        var color = UiPalette.TextPrimary;
        var center = rect.GetCenter();
        switch (state.Shape)
        {
            case PadShape.Cross:
                node.DrawLine(center + new Vector2(-5, -5), center + new Vector2(5, 5), color, 2, true);
                node.DrawLine(center + new Vector2(-5, 5), center + new Vector2(5, -5), color, 2, true);
                return;
            case PadShape.Circle:
                node.DrawArc(center, 5.5f, 0, Mathf.Tau, 24, color, 2, true);
                return;
            case PadShape.Square:
                node.DrawRect(new Rect2(center - new Vector2(5, 5), new Vector2(10, 10)), color, false, 2);
                return;
            case PadShape.Triangle:
                node.DrawPolyline(new[] { center + new Vector2(0, -6), center + new Vector2(6, 4.5f), center + new Vector2(-6, 4.5f), center + new Vector2(0, -6) }, color, 2, true);
                return;
        }
        var font = ThemeDB.FallbackFont;
        var fontSize = UiFont.Badge;
        var ascent = font.GetAscent(fontSize);
        var descent = font.GetDescent(fontSize);
        var baseline = rect.Position.Y + (rect.Size.Y - (ascent + descent)) * 0.5f + ascent;
        node.DrawString(font, new Vector2(rect.Position.X, baseline), state.Text ?? "", HorizontalAlignment.Center, rect.Size.X, fontSize, color);
    }

    // ---------- Linhas de opção e painel de opções (§3.12) ----------

    public static UiOptionRow LabeledSlider(string label, double min, double max, double value, Action<double> onChanged, double step = 1, Func<double, string> format = null)
    {
        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step, Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(280, 24), FocusMode = Control.FocusModeEnum.All
        };
        var row = OptionRowBase(label, slider, true);
        format ??= v => Mathf.RoundToInt((float)v).ToString();
        row.Value.Text = format(slider.Value);
        slider.ValueChanged += v =>
        {
            row.Value.Text = format(v);
            onChanged?.Invoke(v);
        };
        return row;
    }

    public static UiOptionRow OptionRow(string label, IReadOnlyList<string> items, int selected, Action<int> onSelected)
    {
        var choice = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(280, UiMetrics.ButtonM), FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        if (items != null)
            foreach (var item in items) choice.AddItem(item ?? "");
        if (choice.ItemCount > 0) choice.Selected = Mathf.Clamp(selected, 0, choice.ItemCount - 1);
        if (onSelected != null) choice.ItemSelected += index => onSelected((int)index);
        return OptionRowBase(label, choice, false);
    }

    // Valor textual "Ligado/Desligado" na coluna de valor (não depende só da forma).
    public static UiOptionRow ToggleRow(string label, bool value, Action<bool> onToggled)
    {
        var toggle = new CheckButton
        {
            ButtonPressed = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(280, UiMetrics.ButtonS), FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        var row = OptionRowBase(label, toggle, true);
        row.Value.Text = ToggleText(value);
        toggle.Toggled += enabled =>
        {
            row.Value.Text = ToggleText(enabled);
            onToggled?.Invoke(enabled);
        };
        return row;
    }

    // Atualiza um ToggleRow sem disparar o handler.
    public static void SetToggleRow(UiOptionRow row, bool value)
    {
        if (row?.Toggle == null) return;
        row.Toggle.SetPressedNoSignal(value);
        row.Value.Text = ToggleText(value);
    }

    private static string ToggleText(bool on) => on ? Localization.Text("ui.common.on") : Localization.Text("ui.common.off");

    private static UiOptionRow OptionRowBase(string label, Control control, bool showValue)
    {
        var row = new UiOptionRow { Control = control };
        row.Root = new PanelContainer { CustomMinimumSize = new Vector2(0, UiMetrics.RowHeight) };
        var idle = UIThemeFactory.EmptyBox(UiMetrics.S2, 0);
        var focused = UIThemeFactory.ControlBox(UiPalette.PanelRaised, UiPalette.GoldDim, UiMetrics.BorderThin, UiMetrics.S2, 0);
        row.Root.AddThemeStyleboxOverride("panel", idle);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", UiMetrics.S4);
        row.Root.AddChild(line);
        row.Label = new Label
        {
            Text = label ?? "", ThemeTypeVariation = UiVariation.BodyLabel,
            CustomMinimumSize = new Vector2(240, 0), VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        line.AddChild(row.Label);
        line.AddChild(control);
        row.Value = new Label
        {
            ThemeTypeVariation = UiVariation.BodyLabel, Visible = showValue,
            CustomMinimumSize = new Vector2(64, 0), HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        row.Value.AddThemeColorOverride("font_color", UiPalette.TextSecondary);
        line.AddChild(row.Value);
        // A linha inteira acende quando o controle tem foco (o HSlider não tem anel próprio em todas as versões).
        control.FocusEntered += () => row.Root.AddThemeStyleboxOverride("panel", focused);
        control.FocusExited += () => row.Root.AddThemeStyleboxOverride("panel", idle);
        return row;
    }

    // Opções compartilhadas pelo menu e pela pausa, em abas Vídeo / Áudio / Jogo (ver OptionsPanelView.cs).
    // O idioma fica na aba Jogo (só com showLanguage). Depois de adicionar o botão "Voltar", chame view.SetFooterTarget(botão).
    public static OptionsPanelView OptionsPanel(bool showLanguage)
    {
        var view = new OptionsPanelView();
        view.Build(showLanguage);
        return view;
    }

    public static OptionsPanelView FindOptionsPanel(Node scope) => scope?.FindChild("OptionsPanel", true, false) as OptionsPanelView;

    // ---------- Ícones e selos (§3.13) ----------

    // Selo: Badge 14, caixa alta, fundo da cor do papel; texto escuro sobre cores claras.
    public static PanelContainer Badge(string text, Color color) => Badge(text, color, color.Luminance > 0.45f ? UiPalette.ScreenBg : UiPalette.TextPrimary);

    public static PanelContainer Badge(string text, Color background, Color textColor)
    {
        var badge = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = background,
            ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 2, ContentMarginBottom = 2
        };
        // 2D: selo de canto reto (pixel art).
        style.SetCornerRadiusAll(UiPixelKit.Enabled ? 0 : UiMetrics.RadiusBadge);
        badge.AddThemeStyleboxOverride("panel", style);
        var label = new Label { Text = text ?? "", ThemeTypeVariation = UiVariation.BadgeLabel, Uppercase = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeColorOverride("font_color", textColor);
        badge.AddChild(label);
        return badge;
    }

    // Ícone de bênção: slot PanelInset com borda 2 da raridade, ícone com inset 4, selo "N2" (nível > 1).
    public static Control BlessingIcon(BloodBlessingRecord record, int size = UiMetrics.IconHud, bool showLevel = true)
    {
        if (record == null) return BlessingIcon((Texture2D)null, "common", 1, size, showLevel);
        Texture2D icon = null;
        if (BloodBlessingCatalog.TryGet(record.Id, out var definition) && !string.IsNullOrEmpty(definition.IconPath))
            icon = ResourceLoader.Load<Texture2D>(definition.IconPath);
        var control = BlessingIcon(icon, record.Rarity, record.Level, size, showLevel);
        if (icon == null)
        {
            var monogram = Monogram(BloodBlessingCatalog.TryGet(record.Id, out var monogramDefinition) && !string.IsNullOrEmpty(monogramDefinition.DisplayName) ? monogramDefinition.DisplayName : record.Name, size - 8);
            monogram.Position = new Vector2(4, 4);
            control.AddChild(monogram);
        }
        return control;
    }

    public static Control BlessingIcon(Texture2D icon, string rarity, int level, int size = UiMetrics.IconHud, bool showLevel = true)
    {
        var root = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Pass };
        var slot = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = UiPalette.PanelInset, BorderColor = UiPalette.Rarity(rarity) };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(UiMetrics.RadiusBadge);
        slot.AddThemeStyleboxOverride("panel", style);
        slot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(slot);
        if (icon != null)
        {
            var texture = new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            texture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            texture.OffsetLeft = 4; texture.OffsetTop = 4; texture.OffsetRight = -4; texture.OffsetBottom = -4;
            root.AddChild(texture);
        }
        if (showLevel && level > 1)
        {
            var badge = Badge(Localization.TextFormat("ui.common.level_short", level), UiPalette.PanelInset, UiPalette.Gold);
            badge.AnchorLeft = 1; badge.AnchorTop = 1; badge.AnchorRight = 1; badge.AnchorBottom = 1;
            badge.OffsetLeft = 2; badge.OffsetTop = 2; badge.OffsetRight = 2; badge.OffsetBottom = 2;
            badge.GrowHorizontal = Control.GrowDirection.Begin;
            badge.GrowVertical = Control.GrowDirection.Begin;
            root.AddChild(badge);
        }
        return root;
    }

    // Moeda de Sangue: no 2D, a moeda em pixel art (UiPixelKit); senão, gota desenhada (Life + realce LifeHighlight).
    public static Control CoinGlyph(int size = 24)
    {
        if (UiPixelKit.Icon("coin_blood.png", size) is { } pixelCoin)
            return pixelCoin;
        var node = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var points = new Vector2[15];
        var outline = new Vector2[16];
        var radius = size * 0.32f;
        var center = new Vector2(size * 0.5f, size * 0.62f);
        points[0] = new Vector2(size * 0.5f, size * 0.06f);
        const int arcPoints = 14;
        var start = -Mathf.Pi / 6.0f;
        var end = Mathf.Pi + Mathf.Pi / 6.0f;
        for (var i = 0; i < arcPoints; i++)
        {
            var angle = start + (end - start) * i / (arcPoints - 1);
            points[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        Array.Copy(points, outline, points.Length);
        outline[15] = points[0];
        var highlight = center + new Vector2(-radius * 0.38f, -radius * 0.28f);
        node.Draw += () =>
        {
            node.DrawColoredPolygon(points, UiPalette.Life);
            node.DrawPolyline(outline, UiPalette.TextOutline, 1, true);
            node.DrawCircle(highlight, Mathf.Max(1.5f, radius * 0.24f), UiPalette.LifeHighlight);
        };
        return node;
    }

    // Sangue Amaldiçoado (recurso permanente do hub): losango violeta desenhado, para nunca ser confundido com a gota
    // das Moedas de Sangue (forma própria além da cor). Mesmo glifo do aviso de recompensa (HUD.Notifications).
    public static Control CursedBloodGlyph(int size = 24)
    {
        // No 2D, o frasco de Ícor em pixel art (UiPixelKit; desenho em tools/pixel_art/hud/draw_cursed_blood.py).
        if (UiPixelKit.Icon("cursed_blood.png", size) is { } pixelGem)
            return pixelGem;
        var node = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var scale = size / 24.0f;
        var diamond = new[] { new Vector2(12, 1) * scale, new Vector2(22, 12) * scale, new Vector2(12, 23) * scale, new Vector2(2, 12) * scale };
        var outline = new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] };
        node.Draw += () =>
        {
            node.DrawColoredPolygon(diamond, UiPalette.CursedBlood);
            node.DrawPolyline(outline, UiPalette.TextOutline, 1);
            node.DrawLine(new Vector2(12, 5) * scale, new Vector2(12, 19) * scale, UiPalette.TextOutline, 1);
        };
        return node;
    }

    // Cadeado desenhado (arco + corpo) em TextDisabled.
    public static Control LockGlyph(int size = UiMetrics.IconInline)
    {
        var node = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var color = UiPalette.TextDisabled;
        var body = new Rect2(size * 0.18f, size * 0.46f, size * 0.64f, size * 0.46f);
        var shackleCenter = new Vector2(size * 0.5f, size * 0.46f);
        var shackleRadius = size * 0.22f;
        var width = Mathf.Max(2.0f, size * 0.1f);
        var keyhole = new Rect2(size * 0.46f, size * 0.6f, size * 0.08f, size * 0.16f);
        node.Draw += () =>
        {
            node.DrawArc(shackleCenter, shackleRadius, Mathf.Pi, Mathf.Tau, 16, color, width, true);
            node.DrawRect(body, color);
            node.DrawRect(keyhole, UiPalette.PanelInset);
        };
        return node;
    }

    // ---------- Estados de nó de skill (§2.1) ----------

    // Fundo/borda por estado. Planejado: alpha 0.55 sem borda; somar DashedOutline. Conflito: somar StrikeOverlay.
    // hover: fundo PanelRaised (exceto Planned). No 2D, moldura em pixel art tingida pela cor da borda.
    public static StyleBox SkillNodeStyle(SkillNodeState state, bool hover = false)
    {
        var (background, border, width) = state switch
        {
            SkillNodeState.Purchased or SkillNodeState.PurchasedNoCoins => (UiPalette.PanelRaised, UiPalette.Gold, 2),
            SkillNodeState.PurchasedMax => (UiPalette.PanelRaised, UiPalette.GoldBright, 3),
            SkillNodeState.Available => (UiPalette.Panel, new Color(UiPalette.TextPrimary, 0.8f), 2),
            SkillNodeState.AvailableNoCoins => (UiPalette.Panel, UiPalette.Border, 1),
            SkillNodeState.Locked or SkillNodeState.Conflict => (UiPalette.PanelInset, UiPalette.Border, 1),
            _ => (new Color(UiPalette.Panel, 0.55f), Colors.Transparent, 0)
        };
        if (hover && state != SkillNodeState.Planned)
            background = UiPalette.PanelRaised;
        if (UiPixelKit.Frame(border, background, UiMetrics.S2, UiMetrics.S1) is { } pixel)
            return pixel;
        return UIThemeFactory.ControlBox(background, border, width, UiMetrics.S2, UiMetrics.S1);
    }

    public static Color SkillNodeTextColor(SkillNodeState state) => state switch
    {
        SkillNodeState.AvailableNoCoins => UiPalette.TextSecondary,
        SkillNodeState.Locked or SkillNodeState.Conflict or SkillNodeState.Planned => UiPalette.TextDisabled,
        _ => UiPalette.TextPrimary
    };

    public static Color SkillNodeCostColor(SkillNodeState state) => state switch
    {
        SkillNodeState.PurchasedNoCoins or SkillNodeState.AvailableNoCoins => UiPalette.Warn,
        SkillNodeState.Locked or SkillNodeState.Conflict or SkillNodeState.Planned => UiPalette.TextDisabled,
        _ => UiPalette.CursedBlood // custo em Sangue Amaldiçoado (violeta), distinto do dourado das Moedas/valores-chave
    };

    // Links da árvore: pré-requisito não comprado = 3 px Border; comprado = 4 px Gold.
    public static Color SkillLinkColor(bool purchased) => purchased ? UiPalette.Gold : UiPalette.Border;
    public static float SkillLinkWidth(bool purchased) => purchased ? 4 : 3;

    // Sobreposição de contorno tracejado 1 px (filho de controle não-container; tela cheia do pai).
    public static Control DashedOutline(Color color)
    {
        var node = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Name = "DashedOutline" };
        node.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        node.Draw += () => DrawDashedRect(node, new Rect2(Vector2.Zero, node.Size), color);
        node.Resized += node.QueueRedraw;
        return node;
    }

    // Diagonal 2 px TextSecondary sobre o nó (conflito exclusivo).
    public static Control StrikeOverlay()
    {
        var node = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Name = "StrikeOverlay" };
        node.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        node.Draw += () => node.DrawLine(new Vector2(4, node.Size.Y - 4), new Vector2(node.Size.X - 4, 4), UiPalette.TextSecondary, 2, true);
        node.Resized += node.QueueRedraw;
        return node;
    }

    private static void DrawDashedRect(Control node, Rect2 rect, Color color)
    {
        var a = rect.Position + new Vector2(0.5f, 0.5f);
        var b = new Vector2(rect.End.X - 0.5f, rect.Position.Y + 0.5f);
        var c = rect.End - new Vector2(0.5f, 0.5f);
        var d = new Vector2(rect.Position.X + 0.5f, rect.End.Y - 0.5f);
        node.DrawDashedLine(a, b, color, 1, 4);
        node.DrawDashedLine(b, c, color, 1, 4);
        node.DrawDashedLine(c, d, color, 1, 4);
        node.DrawDashedLine(d, a, color, 1, 4);
    }

    // Seção de ferramentas de desenvolvimento (§7): cabeçalho TextSecondary, selo DEV, borda tracejada. A tela decide se mostra (OS.IsDebugBuild()).
    public static UiDevSection DevSection(string title = null)
    {
        var section = new UiDevSection { Root = new PanelContainer { Name = "DevSection" } };
        section.Root.AddThemeStyleboxOverride("panel", UIThemeFactory.EmptyBox(UiMetrics.S3, UiMetrics.S3));
        var dash = new Color(UiPalette.TextSecondary, 0.5f);
        section.Root.Draw += () => DrawDashedRect(section.Root, new Rect2(Vector2.Zero, section.Root.Size), dash);
        section.Root.Resized += section.Root.QueueRedraw;
        section.Body = new VBoxContainer();
        section.Body.AddThemeConstantOverride("separation", UiMetrics.S2);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", UiMetrics.S2);
        var label = new Label { Text = title ?? Localization.Text("ui.common.dev_tools"), ThemeTypeVariation = UiVariation.SectionLabel };
        label.AddThemeColorOverride("font_color", UiPalette.TextSecondary);
        header.AddChild(label);
        var badge = Badge("DEV", UiPalette.PanelInset, UiPalette.TextSecondary);
        badge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(badge);
        section.Body.AddChild(header);
        section.Root.AddChild(section.Body);
        return section;
    }

    // ---------- Divisores ----------

    // Divisor decorativo de janela: simple_divider.png, 4 px, GoldDim a 60%.
    public static TextureRect Divider()
    {
        return new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(UIThemeFactory.SimpleDividerPath),
            CustomMinimumSize = new Vector2(0, 4),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            SelfModulate = new Color(UiPalette.GoldDim, 0.6f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    // Linha 1 px Border (antes de ações destrutivas, acima do rodapé).
    public static ColorRect Separator() => new ColorRect
    {
        Color = UiPalette.Border, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore
    };

    // ---------- Número de dano recebido (§3.14) ----------

    private static readonly List<Label> LiveDamageNumbers = new();

    // screenPos = jogador projetado + (0, −80) (calculado pela tela). No máximo 3 vivos; o quarto recicla o mais antigo.
    // randomJitter = false: sem o ±12 aleatório (golpes em sequência já saem em leque, deslocados por quem chama).
    public static Label DamageNumber(Control parent, float amount, Vector2 screenPos, bool randomJitter = true)
    {
        if (parent == null || !parent.IsInsideTree()) return null;
        LiveDamageNumbers.RemoveAll(label => !GodotObject.IsInstanceValid(label) || label.IsQueuedForDeletion());
        while (LiveDamageNumbers.Count >= 3)
        {
            var oldest = LiveDamageNumbers[0];
            LiveDamageNumbers.RemoveAt(0);
            oldest.QueueFree();
        }
        var label = new Label
        {
            Text = $"-{Mathf.RoundToInt(amount)}",
            ThemeTypeVariation = UiVariation.DamageNumberLabel,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(160, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(label);
        var jitter = randomJitter ? (float)GD.RandRange(-12.0, 12.0) : 0.0f;
        label.Position = screenPos + new Vector2(jitter - 80, -20);
        LiveDamageNumbers.Add(label);
        var tween = label.CreateTween();
        tween.TweenProperty(label, "position:y", label.Position.Y - UiMotion.DamageNumberRise, UiMotion.DamageNumber);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, UiMotion.DamageNumberFadeTail).SetDelay(UiMotion.DamageNumber - UiMotion.DamageNumberFadeTail);
        tween.TweenCallback(Callable.From(() =>
        {
            LiveDamageNumbers.Remove(label);
            if (GodotObject.IsInstanceValid(label)) label.QueueFree();
        }));
        return label;
    }

    // ---------- Vinheta de borda (§6) ----------

    // Textura radial: miolo de ~62% transparente, borda (0.55, 0, 0.03). Intensidade só por Modulate.a.
    public static GradientTexture2D VignetteTexture()
    {
        var edge = new Color(0.55f, 0.00f, 0.03f, 1.0f);
        var gradient = new Gradient
        {
            Offsets = new[] { 0.0f, 0.62f, 1.0f },
            Colors = new[] { new Color(edge, 0.0f), new Color(edge, 0.0f), edge }
        };
        return new GradientTexture2D
        {
            Width = 256, Height = 256,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1.0f, 0.5f),
            Gradient = gradient
        };
    }

    // Nó "EdgeVignette" tela cheia (Modulate.a = 0). A tela o coloca abaixo dos painéis do HUD e anima o alpha.
    public static TextureRect EdgeVignette(Control parent = null)
    {
        var vignette = new TextureRect
        {
            Name = "EdgeVignette",
            Texture = VignetteTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parent?.AddChild(vignette);
        return vignette;
    }

    // ---------- Internos ----------

    private static void PlaceWindowPanel(PanelContainer panel, WindowSize size)
    {
        if (size == WindowSize.Full)
        {
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            panel.OffsetLeft = UiMetrics.OverlayMarginX;
            panel.OffsetRight = -UiMetrics.OverlayMarginX;
            panel.OffsetTop = UiMetrics.OverlayMarginY;
            panel.OffsetBottom = -UiMetrics.OverlayMarginY;
            return;
        }
        var (width, height) = size switch
        {
            WindowSize.Small => (560f, 0f),
            WindowSize.Medium => (640f, 0f),
            _ => (1240f, 720f)
        };
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f; panel.AnchorTop = 0.5f; panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -width * 0.5f; panel.OffsetRight = width * 0.5f;
        panel.OffsetTop = -height * 0.5f; panel.OffsetBottom = height * 0.5f;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Both;
        panel.CustomMinimumSize = size == WindowSize.Large ? new Vector2(960, 560) : new Vector2(width, 0);
    }
}
