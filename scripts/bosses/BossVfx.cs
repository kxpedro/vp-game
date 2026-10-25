using Godot;
using System.Collections.Generic;

// Efeitos animados dos golpes de boss em pixel art (folhas horizontais geradas por
// tools/pixel_art/bosses/build_invisible_woman.py). Cada efeito e um Sprite3D: em pe (billboard, ex.: orbe, escudo)
// ou deitado no chao (ex.: onda de choque, vortice), na mesma escala de pixel dos monstros do modo 2D.
public static class BossVfx
{
    public sealed record Sheet(string Path, int FrameSize);

    private const string Root = "res://assets/pixel_art/effects/boss_invisible_woman/";
    public static readonly Sheet Orb = new(Root + "orb.png", 24);
    public static readonly Sheet Burst = new(Root + "burst.png", 56);
    public static readonly Sheet Shield = new(Root + "shield.png", 56);
    public static readonly Sheet Ring = new(Root + "ring.png", 64);
    public static readonly Sheet Vortex = new(Root + "vortex.png", 64);

    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static int FrameCount(Sheet sheet)
    {
        var texture = Load(sheet);
        return texture == null ? 1 : Mathf.Max(texture.GetWidth() / sheet.FrameSize, 1);
    }

    // Tamanho de um quadro no mundo (m) com escala 1.
    public static float WorldSize(Sheet sheet) => sheet.FrameSize * PlayerSpriteVisual.PixelSize;

    public static Sprite3D Create(Node parent, Sheet sheet, Vector3 position, bool flat, float scale = 1.0f)
    {
        var texture = Load(sheet);
        if (texture == null || parent == null) return null;
        var sprite = new Sprite3D
        {
            Name = "BossVfx",
            Texture = texture,
            Hframes = FrameCount(sheet),
            Billboard = flat ? BaseMaterial3D.BillboardModeEnum.Disabled : BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            DoubleSided = true,
            // Deitado no chao: desenha por cima do piso como os telegraphs.
            NoDepthTest = flat,
            RenderPriority = flat ? 9 : 0,
            PixelSize = PlayerSpriteVisual.PixelSize,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = Vector3.One * scale
        };
        if (flat) sprite.RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f);
        parent.AddChild(sprite);
        sprite.GlobalPosition = position;
        return sprite;
    }

    // Toca os quadros [first, last] uma vez e libera o sprite no fim.
    public static Tween PlayOnce(Sprite3D sprite, float seconds, int first = 0, int last = -1, bool free = true)
    {
        if (sprite == null) return null;
        if (last < 0) last = sprite.Hframes - 1;
        var tween = sprite.CreateTween();
        tween.TweenMethod(Callable.From<float>(t => SetFrame(sprite, t)), (float)first, last + 0.999f, Mathf.Max(seconds, 0.01f));
        if (free) tween.TweenCallback(Callable.From(sprite.QueueFree));
        return tween;
    }

    // Repete os quadros [first, last] ate o sprite ser liberado (ou o tween morto).
    public static Tween Loop(Sprite3D sprite, float secondsPerCycle, int first = 0, int last = -1)
    {
        if (sprite == null) return null;
        if (last < 0) last = sprite.Hframes - 1;
        var tween = sprite.CreateTween().SetLoops();
        tween.TweenMethod(Callable.From<float>(t => SetFrame(sprite, t)), (float)first, last + 0.999f, Mathf.Max(secondsPerCycle, 0.05f));
        return tween;
    }

    // Uma vez, ja dimensionado para cobrir um raio no chao (aneis, explosoes).
    public static void BurstAt(Node parent, Sheet sheet, Vector3 position, float radius, float seconds, bool flat, Color tint)
    {
        // O desenho ocupa ~90% do quadro.
        var scale = radius * 2.0f / (WorldSize(sheet) * 0.9f);
        var sprite = Create(parent, sheet, position, flat, scale);
        if (sprite == null) return;
        sprite.Modulate = tint;
        PlayOnce(sprite, seconds);
    }

    private static void SetFrame(Sprite3D sprite, float t)
    {
        if (GodotObject.IsInstanceValid(sprite)) sprite.Frame = Mathf.Clamp((int)t, 0, sprite.Hframes - 1);
    }

    private static Texture2D Load(Sheet sheet)
    {
        if (Cache.TryGetValue(sheet.Path, out var cached)) return cached;
        var image = PixelRoomComposer.LoadImage(sheet.Path);
        if (image == null)
        {
            GD.PushWarning($"BossVfx: folha ausente: {sheet.Path}");
            return null;
        }
        var texture = ImageTexture.CreateFromImage(image);
        Cache[sheet.Path] = texture;
        return texture;
    }
}
