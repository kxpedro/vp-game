using Godot;
using System.Collections.Generic;

// Modo 2D: visual de inimigo em pixel art (pacote Monsters Creatures Fantasy, assets/pixel_art/monsters/, e os
// chefes em assets/pixel_art/bosses/). Entra no lugar do modelo 3D importado (EnemyBasic.ApplyPlaceholderVisual):
// um Sprite3D em billboard com um AnimationPlayer de animações Idle/Walk/Attack/HitReact/Death, os mesmos nomes
// que o EnemyBasic já toca (os chefes têm também Cast). As folhas são faixas horizontais de quadros olhando para
// a direita; o lado vem da frente do inimigo.
public partial class PixelEnemySprite : Node3D
{
    public const string PathPrefix = "pixel:";
    private const string MonstersRoot = "res://assets/pixel_art/monsters/";
    private const string BossesRoot = "res://assets/pixel_art/bosses/";

    private sealed record Clip(string Name, string File, float Fps, bool Loop);

    // Folhas de um monstro: pasta, largura do quadro e distância dos pés ao centro do quadro (px).
    private sealed record SheetSet(string Folder, int FrameWidth, float FeetOffsetPixels, Clip[] Clips);

    private static readonly Dictionary<string, SheetSet> Monsters = new()
    {
        // Quadros de 150x150 com os pés na linha y≈101: 26 px abaixo do centro.
        ["skeleton"] = new(MonstersRoot + "skeleton/", 150, 26.0f, Clips("idle", "walk")),
        ["goblin"] = new(MonstersRoot + "goblin/", 150, 26.0f, Clips("idle", "walk")),
        ["mushroom"] = new(MonstersRoot + "mushroom/", 150, 26.0f, Clips("idle", "walk")),
        // O olho voador só tem o voo: serve de idle e de movimento.
        ["flying_eye"] = new(MonstersRoot + "flying_eye/", 150, 26.0f, Clips("flight", "flight")),
        // Guardiã Invisível (pacote Blue Witch, tools/pixel_art/bosses/build_invisible_woman.py): quadros de 176x52,
        // pés em y = 45.
        ["invisible_woman"] = new(BossesRoot + "invisible_woman/", 176, 19.0f, new[]
        {
            new Clip("Idle", "idle", 8.0f, true),
            new Clip("Walk", "walk", 10.0f, true),
            new Clip("Attack", "attack", 14.0f, false),
            new Clip("Cast", "cast", 10.0f, false),
            new Clip("HitReact", "hit", 12.0f, false),
            new Clip("Death", "death", 9.0f, false),
        }),
    };

    private static readonly Dictionary<string, Texture2D> TextureCache = new();
    // Largura com desenho de cada folha (px), para descontar os quadros vazios do fim.
    private static readonly Dictionary<Texture2D, int> UsedWidth = new();

    private static Clip[] Clips(string idle, string walk) => new[]
    {
        new Clip("Idle", idle, 8.0f, true),
        new Clip("Walk", walk, 10.0f, true),
        new Clip("Attack", "attack", 12.0f, false),
        new Clip("HitReact", "hit", 12.0f, false),
        new Clip("Death", "death", 8.0f, false),
    };

    public static bool IsPixelPath(string scenePath) => scenePath != null && scenePath.StartsWith(PathPrefix);

    private Sprite3D _sprite;
    private AnimationPlayer _player;
    private Tween _flashTween;

    // "pixel:goblin" -> nó pronto para entrar como ImportedVisual; null se as folhas não existirem.
    public static PixelEnemySprite Create(string scenePath)
    {
        var monster = scenePath.Substring(PathPrefix.Length);
        if (!Monsters.TryGetValue(monster, out var sheets))
        {
            GD.PushWarning($"PixelEnemySprite: monstro desconhecido: {monster}");
            return null;
        }

        var visual = new PixelEnemySprite { Name = "ImportedVisual" };
        visual._sprite = new Sprite3D
        {
            Name = "Sprite",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = true,
            DoubleSided = true,
            PixelSize = PlayerSpriteVisual.PixelSize,
            Offset = new Vector2(0.0f, sheets.FeetOffsetPixels),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        visual.AddChild(visual._sprite);

        var library = new AnimationLibrary();
        foreach (var clip in sheets.Clips)
        {
            var texture = LoadSheet(sheets.Folder + clip.File + ".png");
            if (texture == null)
                return null;
            library.AddAnimation(clip.Name, BuildAnimation(texture, clip, sheets.FrameWidth));
        }
        visual._player = new AnimationPlayer { Name = "AnimationPlayer" };
        visual._player.AddAnimationLibrary("", library);
        visual.AddChild(visual._player);
        return visual;
    }

    private static Texture2D LoadSheet(string path)
    {
        if (TextureCache.TryGetValue(path, out var cached))
            return cached;
        var image = PixelRoomComposer.LoadImage(path);
        if (image == null)
            return null;
        var texture = ImageTexture.CreateFromImage(image);
        TextureCache[path] = texture;
        var usedRect = image.GetUsedRect();
        UsedWidth[texture] = usedRect.Position.X + usedRect.Size.X;
        return texture;
    }

    // Trilhas da animação: textura e número de quadros no início, depois o quadro a cada passo.
    private static Animation BuildAnimation(Texture2D texture, Clip clip, int frameWidth)
    {
        // hframes = a faixa inteira; a animação percorre só os quadros com desenho (as folhas dos chefes vêm
        // completadas com quadros vazios para todas terem o mesmo hframes).
        var hframes = Mathf.Max(texture.GetWidth() / frameWidth, 1);
        var used = UsedWidth.TryGetValue(texture, out var width) ? width : texture.GetWidth();
        var frames = Mathf.Clamp(Mathf.CeilToInt(used / (float)frameWidth), 1, hframes);
        var step = 1.0f / clip.Fps;
        var animation = new Animation
        {
            Length = frames * step,
            LoopMode = clip.Loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None,
        };

        var textureTrack = animation.AddTrack(Animation.TrackType.Value);
        animation.TrackSetPath(textureTrack, "Sprite:texture");
        animation.ValueTrackSetUpdateMode(textureTrack, Animation.UpdateMode.Discrete);
        animation.TrackInsertKey(textureTrack, 0.0f, texture);

        var hframesTrack = animation.AddTrack(Animation.TrackType.Value);
        animation.TrackSetPath(hframesTrack, "Sprite:hframes");
        animation.ValueTrackSetUpdateMode(hframesTrack, Animation.UpdateMode.Discrete);
        animation.TrackInsertKey(hframesTrack, 0.0f, hframes);

        var frameTrack = animation.AddTrack(Animation.TrackType.Value);
        animation.TrackSetPath(frameTrack, "Sprite:frame");
        animation.ValueTrackSetUpdateMode(frameTrack, Animation.UpdateMode.Discrete);
        for (var i = 0; i < frames; i++)
            animation.TrackInsertKey(frameTrack, i * step, i);
        return animation;
    }

    public override void _Process(double delta)
    {
        // A arte olha para a direita: espelha quando a frente do inimigo aponta para a esquerda da tela.
        var camera = GetViewport()?.GetCamera3D();
        if (camera == null || _sprite == null || GetParent() is not Node3D owner)
            return;
        var forward = -owner.GlobalTransform.Basis.Z;
        var side = forward.Dot(camera.GlobalTransform.Basis.X);
        if (Mathf.Abs(side) > 0.05f)
            _sprite.FlipH = side < 0.0f;
    }

    // Pisca o sprite numa cor (dano, início do golpe) e volta ao normal (mantendo a transparência atual).
    public void Flash(Color color, float seconds)
    {
        if (_sprite == null)
            return;
        _flashTween?.Kill();
        _sprite.Modulate = new Color(color.R, color.G, color.B, _alpha);
        _flashTween = CreateTween();
        _flashTween.TweenProperty(_sprite, "modulate", new Color(1.0f, 1.0f, 1.0f, _alpha), seconds);
    }

    private float _alpha = 1.0f;
    private Tween _fadeTween;

    // Some/reaparece (Guardiã Invisível). O recorte de alfa não deixa esmaecer: troca para mistura enquanto o
    // sprite não estiver opaco e volta ao recorte (que ordena melhor com o cenário) quando fica opaco de novo.
    public void FadeTo(float alpha, float seconds)
    {
        if (_sprite == null)
            return;
        _fadeTween?.Kill();
        _flashTween?.Kill();
        _sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled;
        _alpha = alpha;
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_sprite, "modulate", new Color(1.0f, 1.0f, 1.0f, alpha), Mathf.Max(seconds, 0.01f));
        if (alpha >= 0.999f)
            _fadeTween.TweenCallback(Callable.From(() => _sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Discard));
    }

    // Morte: o sprite sai do inimigo (que é liberado na hora), toca a animação de morte no lugar e some.
    public void DetachAndDie()
    {
        var scene = GetTree()?.CurrentScene;
        if (scene == null || _player == null || !_player.HasAnimation("Death"))
            return;
        Reparent(scene);
        SetProcess(false);
        _flashTween?.Kill();
        _fadeTween?.Kill();
        _sprite.Modulate = Colors.White;
        _player.Play("Death");
        var length = (float)_player.GetAnimation("Death").Length;
        var tween = CreateTween();
        tween.TweenInterval(length + 0.35f);
        // Recorte de alfa não deixa esmaecer: troca para transparência só no fim.
        tween.TweenCallback(Callable.From(() => _sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled));
        tween.TweenProperty(_sprite, "modulate:a", 0.0f, 0.4f);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
