using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Rótulos 3D dos interativos (guia de UI §8.8) e leitura protegida de "interact".
/// Prompt = "[glifo] Verbo objeto" em TextPrimary, glifo pelo último dispositivo (UiInput);
/// título do objeto em Gold. ConsumeInteract ignora o input com UI aberta e no frame em que ela fechou.
/// </summary>
public static class InteractPrompt
{
    // Tamanho constante na tela (FixedSize): o rótulo não cresce perto da câmera nem encolhe longe dela.
    public const float PixelSize = 0.0011f;
    public const int PromptFontSize = 36;
    public const int PromptOutline = 10;
    public const int TitleFontSize = 48;
    public const int TitleOutline = 11;
    private const float TitleToPromptGapPx = 10.0f;
    // Altura mínima (global) do título: acima da cabeça do jogador (~1,8 m), para não cobrir o personagem.
    public const float TitleMinHeight = 2.2f;
    private const string RawTextMeta = "interact_prompt_raw";
    // Títulos só aparecem perto do jogador (distância no plano XZ, com histerese) e nunca sob a moldura do topo do HUD.
    public const float TitleHideDistance = 9.5f;
    public const float TitleShowDistance = 9.0f;
    // Moldura do topo do HUD (até 800 × 76 a partir de y 16) mais folga, em pixels lógicos.
    private const float HudTopFrameHalfWidth = 420.0f;
    private const float HudTopFrameBottom = 104.0f;
    private const int HudLookupIntervalFrames = 30;

    private static readonly StringName InteractAction = "interact";
    private static readonly StringName HudGroup = "hud";
    private static readonly StringName PlayerGroup = "player";
    private static readonly List<Label3D> Titles = new();

    // Prefixos legados ("E - ...", "Interagir — ...") que viram o glifo do dispositivo.
    private static readonly string[] LegacyPrefixes =
    {
        "E / X (A) -", "Pressione E", "Interagir —", "Interagir -", "Interact —", "Interact -",
        "Interactuar —", "Interactuar -", "E -", "E –", "E —"
    };

    private static SceneTree _trackedTree;
    private static ulong _lastBlockedFrame;
    private static bool _hasBlockedFrame;
    private static string _playStationCross;
    private static HUD _hud;
    private static Node3D _player;
    private static ulong _lastHudLookupFrame;
    private static bool _hasHudLookup;

    // ---------- Input ----------

    /// <summary>
    /// True se "interact" foi apertado neste frame e pode ser usado pelo mundo: nenhuma UI de jogo aberta
    /// (HUD.IsGameplayInputBlocked ou árvore pausada) neste frame nem no anterior (o aperto que fecha
    /// um overlay não reabre o interativo).
    /// </summary>
    public static bool ConsumeInteract(Node node)
    {
        if (node == null || !node.IsInsideTree())
            return false;
        EnsureTracking(node.GetTree());
        if (!Input.IsActionJustPressed(InteractAction))
            return false;

        var frame = Engine.GetProcessFrames();
        if (_hasBlockedFrame && frame - _lastBlockedFrame <= 1)
            return false;
        return true;
    }

    // Registra o estado de bloqueio uma vez por frame, inclusive com a árvore pausada
    // (process_frame é emitido antes do _Process dos nós e não depende de pausa).
    private static void EnsureTracking(SceneTree tree)
    {
        if (tree == null || tree == _trackedTree)
            return;
        if (_trackedTree != null && GodotObject.IsInstanceValid(_trackedTree))
            _trackedTree.ProcessFrame -= OnProcessFrame;
        _trackedTree = tree;
        _trackedTree.ProcessFrame += OnProcessFrame;
        OnProcessFrame();
    }

    private static void OnProcessFrame()
    {
        var tree = _trackedTree;
        if (tree == null || !GodotObject.IsInstanceValid(tree))
            return;
        var hud = FindHud(tree);
        var blocked = tree.Paused || (hud != null && hud.IsGameplayInputBlocked());
        if (blocked)
        {
            _lastBlockedFrame = Engine.GetProcessFrames();
            _hasBlockedFrame = true;
        }
        UpdateTitleVisibility(tree);
    }

    // HUD em cache (sem GetFirstNodeInGroup por frame); sem HUD na cena, procura de novo a cada 30 frames.
    private static HUD FindHud(SceneTree tree)
    {
        if (_hud != null && GodotObject.IsInstanceValid(_hud) && _hud.IsInsideTree())
            return _hud;
        _hud = null;
        var frame = Engine.GetProcessFrames();
        if (_hasHudLookup && frame - _lastHudLookupFrame < HudLookupIntervalFrames)
            return null;
        _hasHudLookup = true;
        _lastHudLookupFrame = frame;
        _hud = tree.GetFirstNodeInGroup(HudGroup) as HUD;
        return _hud;
    }

    private static Node3D FindPlayer(SceneTree tree)
    {
        if (_player != null && GodotObject.IsInstanceValid(_player) && _player.IsInsideTree())
            return _player;
        _player = tree.GetFirstNodeInGroup(PlayerGroup) as Node3D;
        return _player;
    }

    // Títulos (StyleTitle) ficam visíveis só a ≤ 9 m do jogador e fora da moldura do topo do HUD.
    private static void UpdateTitleVisibility(SceneTree tree)
    {
        if (Titles.Count == 0)
            return;
        Node3D player = null;
        Camera3D camera = null;
        var viewportWidth = 0.0f;
        var resolved = false;
        for (var i = Titles.Count - 1; i >= 0; i--)
        {
            var title = Titles[i];
            if (title == null || !GodotObject.IsInstanceValid(title))
            {
                Titles.RemoveAt(i);
                continue;
            }
            if (!title.IsInsideTree())
                continue;
            if (!resolved)
            {
                resolved = true;
                player = FindPlayer(tree);
                var viewport = title.GetViewport();
                camera = viewport?.GetCamera3D();
                viewportWidth = viewport != null ? viewport.GetVisibleRect().Size.X : 0.0f;
            }
            if (player == null)
                continue;
            var position = title.GlobalPosition;
            var playerPosition = player.GlobalPosition;
            var dx = position.X - playerPosition.X;
            var dz = position.Z - playerPosition.Z;
            var limit = title.Visible ? TitleHideDistance : TitleShowDistance;
            var visible = dx * dx + dz * dz <= limit * limit;
            if (visible && camera != null && !camera.IsPositionBehind(position))
            {
                var screen = camera.UnprojectPosition(position);
                if (screen.Y < HudTopFrameBottom && Mathf.Abs(screen.X - viewportWidth * 0.5f) < HudTopFrameHalfWidth)
                    visible = false;
            }
            if (title.Visible != visible)
                title.Visible = visible;
        }
    }

    // ---------- Prompt ----------

    /// <summary>Cria o prompt (oculto) como filho de <paramref name="owner"/>, a <paramref name="height"/> m.</summary>
    public static Label3D CreatePrompt(Node3D owner, string text, float height)
    {
        var label = new Label3D
        {
            Name = "PromptLabel",
            Position = Vector3.Up * height,
            Visible = false
        };
        StylePrompt(label);
        label.SetMeta(RawTextMeta, text ?? "");
        // Glifo pelo último dispositivo: assina já (o dispositivo pode mudar antes de o rótulo entrar na árvore),
        // reassina ao reentrar, reconfere ao entrar e ao ficar visível, e desassina ao sair da árvore.
        Action<InputDevice> handler = null;
        handler = _ =>
        {
            if (GodotObject.IsInstanceValid(label))
                Refresh(label);
            else
                UiInput.DeviceChanged -= handler;
        };
        UiInput.DeviceChanged += handler;
        label.TreeEntered += () =>
        {
            UiInput.DeviceChanged -= handler;
            UiInput.DeviceChanged += handler;
            Refresh(label);
        };
        label.TreeExiting += () => UiInput.DeviceChanged -= handler;
        label.VisibilityChanged += () =>
        {
            if (label.Visible)
                Refresh(label);
        };
        Refresh(label);
        owner.AddChild(label);
        if (owner.IsInsideTree())
            EnsureTracking(owner.GetTree());
        return label;
    }

    /// <summary>Troca o texto (sem glifo; prefixos legados "E - " e "Interagir — " são removidos).</summary>
    public static void SetText(Label3D label, string text)
    {
        if (label == null || !GodotObject.IsInstanceValid(label))
            return;
        label.SetMeta(RawTextMeta, text ?? "");
        Refresh(label);
    }

    /// <summary>
    /// Estiliza os Label3D-título do objeto como título (Gold, 48, outline 11) e ancora o prompt logo acima
    /// do primeiro título. Procura entre os filhos de <paramref name="container"/>, ignorando o prompt.
    /// </summary>
    public static Label3D StyleTitlesAndAnchor(Node container, Label3D prompt)
    {
        if (container == null)
            return null;
        Label3D anchor = null;
        foreach (var child in container.GetChildren())
        {
            if (child is not Label3D title || title == prompt)
                continue;
            StyleTitle(title);
            if (title.IsInsideTree() && title.GlobalPosition.Y < TitleMinHeight)
                title.GlobalPosition = new Vector3(title.GlobalPosition.X, TitleMinHeight, title.GlobalPosition.Z);
            anchor ??= title;
        }
        if (anchor != null && prompt != null && GodotObject.IsInstanceValid(prompt) && prompt.IsInsideTree() && anchor.IsInsideTree())
        {
            // Offset no plano do billboard: o prompt fica logo acima do título em qualquer inclinação de câmera.
            prompt.GlobalPosition = anchor.GlobalPosition;
            var font = ThemeDB.FallbackFont;
            var titleHeight = font.GetHeight(anchor.FontSize) * anchor.PixelSize;
            var promptHeight = font.GetHeight(prompt.FontSize) * prompt.PixelSize;
            var gap = TitleToPromptGapPx * prompt.PixelSize;
            var offsetMeters = (titleHeight + promptHeight) * 0.5f + gap;
            prompt.Offset = new Vector2(0.0f, offsetMeters / prompt.PixelSize);
        }
        return anchor;
    }

    /// <summary>
    /// Para interativos em que o objeto (Area3D) é filho de um nó visual próprio (hub): usa os títulos irmãos,
    /// desde que o pai não seja a raiz da cena (evita tocar nos rótulos da sala inteira).
    /// </summary>
    public static Label3D StyleSiblingTitlesAndAnchor(Node3D area, Label3D prompt)
    {
        var parent = area?.GetParent();
        if (parent == null || parent == area.Owner || parent == area.GetTree()?.CurrentScene)
            return null;
        return StyleTitlesAndAnchor(parent, prompt);
    }

    /// <summary>Visual do prompt: TextPrimary 36, contorno, billboard, tamanho constante, por cima da geometria.</summary>
    public static void StylePrompt(Label3D label)
    {
        if (label == null)
            return;
        label.PixelSize = PixelSize;
        label.FixedSize = true;
        label.FontSize = PromptFontSize;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.Modulate = UiPalette.TextPrimary;
        label.OutlineSize = PromptOutline;
        label.OutlineModulate = UiPalette.TextOutline;
        // Sempre legível por cima da geometria do objeto; desenhado depois do título.
        label.NoDepthTest = true;
        label.RenderPriority = 2;
        label.OutlineRenderPriority = 1;
    }

    /// <summary>
    /// Título de objeto/lugar (guia §2.2/§8.8): Gold 48, contorno, tamanho constante na tela, sem depth test.
    /// Visível só perto do jogador e fora da moldura do topo do HUD (UpdateTitleVisibility).
    /// </summary>
    public static void StyleTitle(Label3D title)
    {
        if (title == null)
            return;
        title.PixelSize = PixelSize;
        title.FixedSize = true;
        title.FontSize = TitleFontSize;
        title.Modulate = UiPalette.Gold;
        title.OutlineSize = TitleOutline;
        title.OutlineModulate = UiPalette.TextOutline;
        title.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        // Legível mesmo atrás de pilar/arco ou do próprio jogador; o prompt desenha por cima.
        title.NoDepthTest = true;
        title.RenderPriority = 1;
        title.OutlineRenderPriority = 0;
        if (!Titles.Contains(title))
            Titles.Add(title);
        if (title.IsInsideTree())
            EnsureTracking(title.GetTree());
        else
            title.Connect(Node.SignalName.TreeEntered,
                Callable.From(() => EnsureTracking(title.GetTree())), (uint)GodotObject.ConnectFlags.OneShot);
    }

    public static string FormatPrompt(string text, InputDevice device)
    {
        var body = StripLegacyPrefix(text);
        return string.IsNullOrEmpty(body) ? $"[{Glyph(device)}]" : $"[{Glyph(device)}] {body}";
    }

    // Texto do glifo de "interact" para rótulos 3D (tabela do guia §3.11). Label3D não desenha forma:
    // PlayStation usa ✕ se a fonte tiver o caractere, senão × (U+00D7, presente na fonte padrão).
    public static string Glyph(InputDevice device)
    {
        switch (device)
        {
            case InputDevice.Xbox:
                return "A";
            case InputDevice.PlayStation:
                _playStationCross ??= ThemeDB.FallbackFont != null && ThemeDB.FallbackFont.HasChar(0x2715) ? "✕" : "×";
                return _playStationCross;
            default:
                return "E";
        }
    }

    private static void Refresh(Label3D label)
    {
        var raw = label.HasMeta(RawTextMeta) ? label.GetMeta(RawTextMeta).AsString() : "";
        label.Text = FormatPrompt(raw, UiInput.LastDevice);
    }

    private static string StripLegacyPrefix(string text)
    {
        var body = (text ?? "").Trim();
        foreach (var prefix in LegacyPrefixes)
        {
            if (body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                body = body.Substring(prefix.Length).Trim();
                break;
            }
        }
        if (body.Length > 0 && char.IsLower(body[0]))
            body = char.ToUpper(body[0]) + body.Substring(1);
        return body;
    }
}
