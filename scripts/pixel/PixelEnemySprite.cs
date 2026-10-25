using Godot;
using System.Collections.Generic;

// Modo 2D: visual de inimigo em pixel art (pacote Monsters Creatures Fantasy, assets/pixel_art/monsters/).
// Entra no lugar do modelo 3D importado (EnemyBasic.ApplyPlaceholderVisual): um Sprite3D em billboard com um
// AnimationPlayer de animações Idle/Walk/Attack/HitReact/Death, os mesmos nomes que o EnemyBasic já toca.
// As folhas são faixas horizontais de quadros de 150x150 olhando para a direita; o lado vem da frente do inimigo.
public partial class PixelEnemySprite : Node3D
{
    public const string PathPrefix = "pixel:";
    private const string MonstersRoot = "res://assets/pixel_art/monsters/";
    private const int FrameSize = 150;
    // Pés na linha y≈101 do quadro de 150 px: 26 px abaixo do centro.
    private const float FeetOffsetPixels = 26.0f;

    private sealed record Clip(string Name, string File, float Fps, bool Loop);

    private static readonly Dictionary<string, Clip[]> Monsters = new()
    {
        ["skeleton"] = Clips("idle", "walk"),
        // Lacaios da Guardiã Invisível: o esqueleto tingido na rampa violeta dela (recolor_monsters.py).
        ["skeleton_guardian"] = Clips("idle", "walk"),
        ["goblin"] = Clips("idle", "walk"),
        ["mushroom"] = Clips("idle", "walk"),
        // O olho voador só tem o voo: serve de idle e de movimento.
        ["flying_eye"] = Clips("flight", "flight"),
        // Guardiã Invisível (boss do mapa 1): o pacote só tem a flutuação parada, que serve para tudo; golpe, dano e
        // morte vêm dos efeitos (BossController.InvisibleWoman.cs, Flash e o esmaecer de DetachAndDie).
        ["mage_guardian"] = new[]
        {
            new Clip("Idle", "idle", 10.0f, true),
            new Clip("Walk", "idle", 14.0f, true),
            new Clip("Attack", "idle", 18.0f, false),
            new Clip("HitReact", "idle", 18.0f, false),
            new Clip("Death", "idle", 10.0f, false),
        },
        // Plantas predadoras do Jardim Sangrento (mapa 2; pacote da Craftpix): vermelha = corpo a corpo,
        // bulbo = a distância, roxa = tanque.
        ["plant_red"] = Clips("idle", "walk"),
        ["plant_bulb"] = Clips("idle", "walk"),
        ["plant_purple"] = Clips("idle", "walk"),
        // Zumbi do Jardim Sangrento: desenhado por código no padrão das plantas (tools/pixel_art/monsters/zombie/).
        ["zombie"] = Clips("idle", "walk"),
    };

    // Quadro quadrado de cada folha (padrão: 150 px do Monsters Creatures Fantasy). Os pés ficam em y≈FrameSize-49
    // nas folhas de 150 e em y≈58 nas de 64: nos dois casos 26 px abaixo do centro.
    private static readonly Dictionary<string, int> FrameSizes = new()
    {
        ["mage_guardian"] = 64,
        ["plant_red"] = 64,
        ["plant_bulb"] = 64,
        ["plant_purple"] = 64,
        ["zombie"] = 64,
    };

    // Pés abaixo do centro do quadro, quando não são os 26 px padrão (plantas e zumbi: pés em y≈51 do quadro de
    // 64).
    private static readonly Dictionary<string, float> FeetOffsets = new()
    {
        ["plant_red"] = 19.0f,
        ["plant_bulb"] = 19.0f,
        ["plant_purple"] = 19.0f,
        ["zombie"] = 19.0f,
    };

    private static readonly Dictionary<string, Texture2D> TextureCache = new();

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
        if (!Monsters.TryGetValue(monster, out var clips))
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
            // Sem luz da cena, como o Virgil e a sala (ver PlayerSpriteVisual).
            Shaded = false,
            DoubleSided = true,
            PixelSize = PlayerSpriteVisual.PixelSize,
            Offset = new Vector2(0.0f, FeetOffsets.TryGetValue(monster, out var feet) ? feet : FeetOffsetPixels),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        PixelRoom.StandUpright(visual._sprite);
        visual.AddChild(visual._sprite);

        var library = new AnimationLibrary();
        foreach (var clip in clips)
        {
            var texture = LoadSheet(monster, clip.File);
            if (texture == null)
                return null;
            library.AddAnimation(clip.Name, BuildAnimation(texture, clip, FrameSizes.TryGetValue(monster, out var size) ? size : FrameSize));
        }
        visual._player = new AnimationPlayer { Name = "AnimationPlayer" };
        visual._player.AddAnimationLibrary("", library);
        visual.AddChild(visual._player);
        return visual;
    }

    private static Texture2D LoadSheet(string monster, string file)
    {
        var path = MonstersRoot + monster + "/" + file + ".png";
        if (TextureCache.TryGetValue(path, out var cached))
            return cached;
        var image = PixelRoomComposer.LoadImage(path);
        if (image == null)
            return null;
        var texture = ImageTexture.CreateFromImage(image);
        TextureCache[path] = texture;
        return texture;
    }

    // Trilhas da animação: textura e número de quadros no início, depois o quadro a cada passo.
    private static Animation BuildAnimation(Texture2D texture, Clip clip, int frameSize)
    {
        var frames = Mathf.Max(texture.GetWidth() / frameSize, 1);
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
        animation.TrackInsertKey(hframesTrack, 0.0f, frames);

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

    // Pisca o sprite numa cor (dano, início do golpe) e volta ao normal.
    public void Flash(Color color, float seconds)
    {
        if (_sprite == null)
            return;
        _flashTween?.Kill();
        // Mantém a transparência atual (Guardiã meio invisível).
        var alpha = _sprite.Modulate.A;
        _sprite.Modulate = new Color(color, alpha);
        _flashTween = CreateTween();
        _flashTween.TweenProperty(_sprite, "modulate", new Color(1.0f, 1.0f, 1.0f, alpha), seconds);
    }

    // Transparência do corpo (a Guardiã Invisível some e reaparece). O recorte de alfa só volta com o corpo opaco.
    public void SetAlpha(float alpha)
    {
        if (_sprite == null)
            return;
        _sprite.AlphaCut = alpha < 0.99f ? SpriteBase3D.AlphaCutMode.Disabled : SpriteBase3D.AlphaCutMode.Discard;
        _sprite.Modulate = new Color(_sprite.Modulate, alpha);
    }

    public float Alpha => _sprite?.Modulate.A ?? 1.0f;

    // Cópia parada do quadro atual, tingida, que esmaece no lugar (imagem espelhada, rastro da investida).
    // offset desloca a cópia em relação ao sprite (rastro ao longo de um caminho).
    public void SpawnAfterimage(Color tint, float seconds, Vector3 offset = default)
    {
        var scene = GetTree()?.CurrentScene;
        if (scene == null || _sprite == null)
            return;
        var ghost = new Sprite3D
        {
            Name = "Afterimage",
            Texture = _sprite.Texture,
            Hframes = _sprite.Hframes,
            Frame = _sprite.Frame,
            FlipH = _sprite.FlipH,
            Billboard = _sprite.Billboard,
            TextureFilter = _sprite.TextureFilter,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
            Shaded = false,
            DoubleSided = true,
            PixelSize = _sprite.PixelSize,
            Offset = _sprite.Offset,
            Modulate = tint,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        scene.AddChild(ghost);
        ghost.GlobalTransform = _sprite.GlobalTransform.Translated(offset);
        var tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate:a", 0.0f, Mathf.Max(seconds, 0.05f));
        tween.TweenCallback(Callable.From(ghost.QueueFree));
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
