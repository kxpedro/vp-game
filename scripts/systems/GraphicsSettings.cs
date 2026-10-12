using Godot;
using System;
using System.Collections.Generic;

// Estado da janela que pode ser revertido (confirmação "Manter estas configurações?").
public readonly record struct DisplaySnapshot(int Mode, int Width, int Height, int Monitor, int Screen);

/// <summary>
/// Aplicador das opções gráficas (docs/design/ui-guia.md §3.12). Estático e sem estado de jogo:
///  - globais (viewport raiz, DisplayServer, RenderingServer, Engine): ApplyWindow / ApplyRendering / ApplyGameplay;
///  - por cena (WorldEnvironment e DirectionalLight3D): reaplicados a cada troca de cena (SceneTree.SceneChanged),
///    sem editar os .tscn. O Environment da cena é duplicado uma vez (o original fica no meta "vp_gfx_base"),
///    então o valor do .tscn é sempre a base e nada vaza entre cenas.
/// Padrões do <see cref="GlobalSettingsData"/> = aparência anterior ao menu de gráficos.
/// </summary>
public static class GraphicsSettings
{
    public const int ModeWindowed = 0, ModeBorderless = 1, ModeExclusive = 2;
    public const int PresetLow = 0, PresetMedium = 1, PresetHigh = 2, PresetCustom = 3;

    // Tamanhos antigos (índice 0..2 de ResolutionIndex): nunca reordenar, só serve à migração.
    public static readonly Vector2I[] LegacyResolutions = { new(1280, 720), new(1600, 900), new(1920, 1080) };
    private static readonly Vector2I[] StandardResolutions = { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160) };
    public static readonly int[] FpsLimits = { 30, 60, 120, 144, 0 };

    private const string BaseMeta = "vp_gfx_base";
    private const string LightMeta = "vp_gfx_light";

    private sealed record ShadowProfile(int Atlas, RenderingServer.ShadowQuality Filter, float MaxDistance);

    // 1 = Baixa, 2 = Média, 3 = Alta. Alta mantém a distância da própria cena (MaxDistance 0): é o visual de antes.
    private static readonly ShadowProfile[] ShadowProfiles =
    {
        null,
        new(1024, RenderingServer.ShadowQuality.SoftVeryLow, 20.0f),
        new(2048, RenderingServer.ShadowQuality.SoftLow, 28.0f),
        new(4096, RenderingServer.ShadowQuality.SoftLow, 0.0f)
    };

    private sealed record Preset(int Scale, int Upscaler, int AntiAliasing, int Shadows, int Ssao, bool Glow, int Fps);

    private static readonly Preset[] Presets =
    {
        new(65, 1, 1, 1, 0, false, 60),
        new(85, 1, 0, 2, 2, true, 144),
        new(100, 0, 2, 3, 3, true, 0)
    };

    private static int _lastShadowQuality = -1;
    private static int _lastSsaoQuality = -1;
    private static SceneTree _tree;
    private static bool _attached;

    // ---------- Ciclo de vida ----------

    public static void Attach(SceneTree tree)
    {
        _tree = tree;
        if (_attached || tree == null) return;
        _attached = true;
        // SceneChanged (troca por ChangeSceneToFile: ScenePortal, menu, recarga) em vez de NodeAdded, que marshalizava cada nó da run.
        // Nenhum script cria WorldEnvironment/DirectionalLight3D em runtime: estão nos .tscn.
        tree.SceneChanged += () => ApplyCurrentScene(GlobalSettings.Instance?.Data);
    }

    public static void ApplyAll(GlobalSettingsData data, bool applyWindow)
    {
        if (applyWindow) ApplyWindow(data);
        ApplyRendering(data);
        ApplyGameplay(data);
        ApplyCurrentScene(data);
    }

    // Reaplica nos nós que já estão na cena atual (mudança de opção com a cena aberta).
    public static void ApplyCurrentScene(GlobalSettingsData data)
    {
        var scene = _tree?.CurrentScene;
        if (scene == null || data == null) return;
        foreach (var node in scene.FindChildren("*", "WorldEnvironment", true, false))
            if (node is WorldEnvironment environment) ApplyEnvironment(environment, data);
        foreach (var node in scene.FindChildren("*", "DirectionalLight3D", true, false))
            if (node is DirectionalLight3D light) ApplyLight(light, data);
    }

    // ---------- Normalização e migração (só em memória) ----------

    public static void Normalize(GlobalSettingsData d)
    {
        if (d.DisplayMode < 0)
            d.DisplayMode = d.Fullscreen ? ModeBorderless : ModeWindowed;
        if (d.ResolutionWidth <= 0 || d.ResolutionHeight <= 0)
        {
            var legacy = LegacyResolutions[Math.Clamp(d.ResolutionIndex, 0, LegacyResolutions.Length - 1)];
            d.ResolutionWidth = legacy.X;
            d.ResolutionHeight = legacy.Y;
        }
        d.DisplayMode = Math.Clamp(d.DisplayMode, ModeWindowed, ModeExclusive);
        d.Monitor = Math.Max(-1, d.Monitor);
        d.VsyncMode = Math.Clamp(d.VsyncMode, 0, 2);
        if (d.MaxFps != 0) d.MaxFps = Math.Max(30, d.MaxFps); // 0 = ilimitado, senão piso de 30
        d.RenderScalePercent = Math.Clamp(d.RenderScalePercent, 50, 100);
        d.Upscaler = Math.Clamp(d.Upscaler, 0, 1);
        d.AntiAliasing = Math.Clamp(d.AntiAliasing, 0, 3);
        d.ShadowQuality = Math.Clamp(d.ShadowQuality, 0, 3);
        d.SsaoQuality = Math.Clamp(d.SsaoQuality, 0, 3);
        d.BrightnessPercent = Math.Clamp(d.BrightnessPercent, 50, 150);
        d.CameraShakeReduction = Math.Clamp(d.CameraShakeReduction, 0, 100);
        d.DamageFlashPercent = Math.Clamp(d.DamageFlashPercent, 0, 100);
        d.Version = Math.Max(d.Version, 3);
    }

    // Mantém os campos antigos coerentes (arquivo continua legível por versões anteriores).
    public static void SyncLegacyFields(GlobalSettingsData d)
    {
        d.Fullscreen = d.DisplayMode != ModeWindowed;
        var index = Array.IndexOf(LegacyResolutions, new Vector2I(d.ResolutionWidth, d.ResolutionHeight));
        if (index >= 0) d.ResolutionIndex = index;
    }

    // ---------- Janela ----------

    public static int ScreenCount => DisplayServer.GetScreenCount();

    public static int ResolveScreen(GlobalSettingsData d)
    {
        var count = ScreenCount;
        if (count <= 0) return 0;
        if (d.Monitor >= count) d.Monitor = -1;
        return d.Monitor >= 0 ? d.Monitor : Math.Clamp(DisplayServer.WindowGetCurrentScreen(), 0, count - 1);
    }

    // Tamanhos fixos que cabem no monitor + o tamanho do próprio monitor (maior só se couber).
    // Moldura estimada da janela (barra de título + bordas) para o modo Janela caber na área útil.
    private static readonly Vector2I WindowChrome = new(16, 40);

    public static List<Vector2I> ResolutionOptions(int screen, bool windowed = false)
    {
        var list = new List<Vector2I>();
        if (ScreenCount <= 0)
        {
            list.AddRange(LegacyResolutions);
            return list;
        }
        screen = Math.Clamp(screen, 0, ScreenCount - 1);
        var monitor = DisplayServer.ScreenGetSize(screen);
        // Modo Janela: só o que cabe na área útil (sem taskbar) já com a moldura; o tamanho do monitor fica de fora.
        var limit = windowed ? DisplayServer.ScreenGetUsableRect(screen).Size - WindowChrome : monitor;
        foreach (var size in StandardResolutions)
            if (size.X <= limit.X && size.Y <= limit.Y) list.Add(size);
        if (!windowed && !list.Contains(monitor)) list.Add(monitor);
        if (list.Count == 0) list.Add(StandardResolutions[0]);
        list.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return list;
    }

    // Se o tamanho salvo não existe nas opções do monitor (monitor menor/outro), escolhe o maior que cabe.
    public static void ClampResolution(GlobalSettingsData d, int screen)
    {
        var options = ResolutionOptions(screen, d.DisplayMode == ModeWindowed);
        var wanted = new Vector2I(d.ResolutionWidth, d.ResolutionHeight);
        if (options.Contains(wanted)) return;
        var best = options[0];
        foreach (var option in options)
            if (option.X * option.Y <= wanted.X * wanted.Y) best = option;
        d.ResolutionWidth = best.X;
        d.ResolutionHeight = best.Y;
    }

    public static void ApplyWindow(GlobalSettingsData d)
    {
        if (d == null || ScreenCount <= 0) return; // headless: sem tela
        var screen = ResolveScreen(d);
        ClampResolution(d, screen);
        var current = DisplayServer.WindowGetCurrentScreen();
        var mode = DisplayServer.WindowGetMode();
        var borderless = DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless);
        var screenPosition = DisplayServer.ScreenGetPosition(screen);
        var screenSize = DisplayServer.ScreenGetSize(screen);
        var usable = DisplayServer.ScreenGetUsableRect(screen);

        switch (d.DisplayMode)
        {
            case ModeBorderless:
                if (mode == DisplayServer.WindowMode.Windowed && borderless && screen == current
                    && DisplayServer.WindowGetPosition() == screenPosition && DisplayServer.WindowGetSize() == screenSize)
                    return;
                if (mode != DisplayServer.WindowMode.Windowed) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                DisplayServer.WindowSetPosition(screenPosition);
                DisplayServer.WindowSetSize(screenSize);
                break;

            case ModeExclusive:
                if (mode == DisplayServer.WindowMode.ExclusiveFullscreen && screen == current) return;
                if (mode != DisplayServer.WindowMode.Windowed) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                if (borderless) DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                if (screen != current) DisplayServer.WindowSetCurrentScreen(screen);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;

            default:
                var size = new Vector2I(d.ResolutionWidth, d.ResolutionHeight);
                var needsCenter = mode != DisplayServer.WindowMode.Windowed || borderless || screen != current;
                if (mode != DisplayServer.WindowMode.Windowed) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                if (borderless) DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                if (screen != current) DisplayServer.WindowSetCurrentScreen(screen);
                var decoration = DisplayServer.WindowGetSizeWithDecorations() - DisplayServer.WindowGetSize();
                if (decoration.X < 0 || decoration.Y < 0 || decoration == Vector2I.Zero) decoration = WindowChrome; // ainda não medida: usa a estimativa
                var sizeChanged = DisplayServer.WindowGetSize() != size;
                if (sizeChanged) DisplayServer.WindowSetSize(size);
                // Centraliza no monitor escolhido (e não no primário) só quando algo mudou: no boot sem mudança a janela não pula.
                if (needsCenter || sizeChanged)
                {
                    var position = usable.Position + (usable.Size - size - decoration) / 2;
                    var outer = size + decoration;
                    position.X = Math.Max(Math.Min(position.X, usable.End.X - outer.X), usable.Position.X);
                    position.Y = Math.Max(Math.Min(position.Y, usable.End.Y - outer.Y), usable.Position.Y);
                    DisplayServer.WindowSetPosition(position);
                }
                break;
        }
    }

    // ---------- Renderização global ----------

    public static void ApplyRendering(GlobalSettingsData d)
    {
        if (d == null) return;
        var root = _tree?.Root;
        if (root != null)
        {
            var scale = Mathf.Clamp(d.RenderScalePercent / 100.0f, 0.5f, 1.0f);
            // Em 100% o upscaler não faz nada: fica Bilinear para não passar pelo FSR à toa.
            root.Scaling3DMode = d.Upscaler == 1 && scale < 0.999f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
            root.Scaling3DScale = scale;
            root.Msaa3D = d.AntiAliasing == 2 ? Viewport.Msaa.Msaa2X : d.AntiAliasing == 3 ? Viewport.Msaa.Msaa4X : Viewport.Msaa.Disabled;
            root.ScreenSpaceAA = d.AntiAliasing == 1 ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        }
        if (ScreenCount > 0)
        {
            var vsync = (DisplayServer.VSyncMode)Math.Clamp(d.VsyncMode, 0, 2);
            if (DisplayServer.WindowGetVsyncMode() != vsync) DisplayServer.WindowSetVsyncMode(vsync);
        }
        Engine.MaxFps = Math.Max(0, d.MaxFps);

        var profile = ShadowProfiles[Math.Clamp(d.ShadowQuality, 1, 3)];
        // Só reaplica o que mudou: recriar o atlas de sombra/SSAO a cada passo de slider é caro.
        if (_lastShadowQuality != d.ShadowQuality)
        {
            _lastShadowQuality = d.ShadowQuality;
            RenderingServer.DirectionalShadowAtlasSetSize(profile.Atlas, true);
            RenderingServer.DirectionalSoftShadowFilterSetQuality(profile.Filter);
        }

        if (d.SsaoQuality > 0 && _lastSsaoQuality != d.SsaoQuality)
        {
            _lastSsaoQuality = d.SsaoQuality;
            var quality = d.SsaoQuality switch
            {
                1 => RenderingServer.EnvironmentSsaoQuality.Low,
                3 => RenderingServer.EnvironmentSsaoQuality.High,
                _ => RenderingServer.EnvironmentSsaoQuality.Medium
            };
            RenderingServer.EnvironmentSetSsaoQuality(quality, true, 0.5f, 2, 50.0f, 300.0f);
        }
    }

    public static void ApplyGameplay(GlobalSettingsData d)
    {
        if (d == null) return;
        IsoCamera.ShakeIntensity = 1.0f - d.CameraShakeReduction / 100.0f;
        CombatFeedback.FlashIntensity = d.DamageFlashPercent / 100.0f;
    }

    // ---------- Por cena ----------

    private static void ApplyEnvironment(WorldEnvironment node, GlobalSettingsData d)
    {
        if (d == null || node.Environment == null && !node.HasMeta(BaseMeta)) return;
        Godot.Environment baseEnvironment;
        if (node.HasMeta(BaseMeta))
        {
            baseEnvironment = node.GetMeta(BaseMeta).As<Godot.Environment>();
        }
        else
        {
            baseEnvironment = node.Environment;
            node.SetMeta(BaseMeta, baseEnvironment);
            node.Environment = (Godot.Environment)baseEnvironment.Duplicate();
        }
        var environment = node.Environment;
        if (baseEnvironment == null || environment == null) return;

        environment.GlowEnabled = baseEnvironment.GlowEnabled && d.Glow;
        environment.SsaoEnabled = baseEnvironment.SsaoEnabled && d.SsaoQuality > 0;
        if (d.BrightnessPercent == 100)
        {
            environment.AdjustmentEnabled = baseEnvironment.AdjustmentEnabled;
            environment.AdjustmentBrightness = baseEnvironment.AdjustmentBrightness;
        }
        else
        {
            environment.AdjustmentEnabled = true;
            environment.AdjustmentBrightness = baseEnvironment.AdjustmentBrightness * d.BrightnessPercent / 100.0f;
        }
    }

    private static void ApplyLight(DirectionalLight3D light, GlobalSettingsData d)
    {
        if (d == null) return;
        if (!light.HasMeta(LightMeta))
            light.SetMeta(LightMeta, new Godot.Collections.Array { light.ShadowEnabled, light.DirectionalShadowMaxDistance });
        var original = light.GetMeta(LightMeta).AsGodotArray();
        var originalEnabled = original[0].AsBool();
        var originalDistance = original[1].AsSingle();
        var quality = Math.Clamp(d.ShadowQuality, 0, 3);
        light.ShadowEnabled = originalEnabled && quality > 0;
        if (quality == 0) return;
        var cap = ShadowProfiles[quality].MaxDistance;
        light.DirectionalShadowMaxDistance = cap > 0.0f ? Mathf.Min(originalDistance, cap) : originalDistance;
    }

    // ---------- Predefinições ----------

    public static void ApplyPreset(GlobalSettingsData d, int preset)
    {
        if (d == null || preset < PresetLow || preset > PresetHigh) return;
        var p = Presets[preset];
        d.RenderScalePercent = p.Scale;
        d.Upscaler = p.Upscaler;
        d.AntiAliasing = p.AntiAliasing;
        d.ShadowQuality = p.Shadows;
        d.SsaoQuality = p.Ssao;
        d.Glow = p.Glow;
        d.MaxFps = p.Fps;
        d.VsyncMode = 1;
    }

    public static int DetectPreset(GlobalSettingsData d)
    {
        for (var i = 0; i < Presets.Length; i++)
        {
            var p = Presets[i];
            if (d.RenderScalePercent == p.Scale && (p.Scale >= 100 || d.Upscaler == p.Upscaler) && d.AntiAliasing == p.AntiAliasing
                && d.ShadowQuality == p.Shadows && d.SsaoQuality == p.Ssao && d.Glow == p.Glow && d.MaxFps == p.Fps && d.VsyncMode == 1)
                return i;
        }
        return PresetCustom;
    }
}
