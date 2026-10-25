using Godot;
using System.Collections.Generic;

/// <summary>
/// Modo 2D: efeitos em pixel art dos ataques dos monstros (assets/pixel_art/effects/enemy_attacks/, recortados dos
/// pacotes de efeitos por tools/pixel_art/enemy_fx/import_enemy_fx.py). Cada monstro tem o seu: o olho voador cospe
/// um orbe de sangue, a planta-bulbo um esporo, o goblin corta, o cogumelo solta esporos, a planta vermelha morde, o
/// zumbi arranha, o esqueleto levanta poeira e a planta roxa faz brotar espinhos. Só visual: o dano continua no
/// EnemyBasic e no EnemyProjectile. Folhas = faixas horizontais de quadros de 64x64.
/// </summary>
public partial class EnemyAttackFx : Node3D
{
    private const string Root = "res://assets/pixel_art/effects/enemy_attacks/";
    private const int Cell = 64;
    private const float Fps = 18.0f;

    /// <summary>
    /// Attack: golpe corpo a corpo (ou pancada no chão, se Grounded). Projectile/Burst: orbe em laço e o estouro dele
    /// no impacto, para quem ataca a distância.
    /// </summary>
    public sealed record Style(string Attack, float AttackScale, bool Grounded, string Projectile = null, string Burst = null);

    private static readonly Dictionary<string, Style> Styles = new()
    {
        ["goblin"] = new("slash_white", 1.0f, false),
        ["mushroom"] = new("spore_cloud", 1.0f, true),
        ["zombie"] = new("claw_red", 1.0f, false),
        ["plant_red"] = new("bite_red", 1.1f, false),
        ["skeleton"] = new("dust_slam", 1.2f, true),
        ["skeleton_guardian"] = new("dust_slam", 1.0f, true),
        ["plant_purple"] = new("thorn_burst", 1.1f, true),
        ["flying_eye"] = new(null, 1.0f, false, "eye_orb", "eye_orb_burst"),
        ["plant_bulb"] = new(null, 1.0f, false, "spore_orb", "spore_burst"),
    };

    private static readonly Dictionary<string, (Texture2D Texture, int Frames, int Bottom)> Sheets = new();

    // null = monstro sem efeito próprio (usa o efeito 3D antigo).
    public static Style For(string monster) => monster != null && Styles.TryGetValue(monster, out var style) ? style : null;

    private Sprite3D _sprite;
    private int _frames;
    private bool _loop;
    private float _time;

    /// <summary>
    /// Toca um efeito uma vez e se libera no fim. direction (no plano do chão) decide o espelhamento: a arte olha
    /// para a direita. grounded: em pé no chão, com a base da arte na posição; senão, centrado nela.
    /// </summary>
    public static EnemyAttackFx Play(Node parent, string sheet, Vector3 position, Vector3 direction, float scale, bool grounded)
    {
        var fx = Create(sheet, scale, grounded, false);
        if (fx == null || parent == null)
            return null;
        parent.AddChild(fx);
        fx.GlobalPosition = position;
        var camera = fx.GetViewport()?.GetCamera3D();
        if (camera != null)
            fx._sprite.FlipH = direction.Dot(camera.GlobalTransform.Basis.X) < 0.0f;
        return fx;
    }

    /// <summary>Efeito em laço (corpo do projétil), para pôr como filho; quem o criou o libera.</summary>
    public static EnemyAttackFx CreateLoop(string sheet, float scale) => Create(sheet, scale, false, true);

    private static EnemyAttackFx Create(string sheet, float scale, bool grounded, bool loop)
    {
        if (string.IsNullOrEmpty(sheet) || !TryLoad(sheet, out var data))
            return null;
        var fx = new EnemyAttackFx { Name = "EnemyAttackFx", _frames = data.Frames, _loop = loop };
        fx._sprite = new Sprite3D
        {
            Name = "Sprite",
            Texture = data.Texture,
            Hframes = data.Frames,
            Frame = 0,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            DoubleSided = true,
            PixelSize = PlayerSpriteVisual.PixelSize * scale,
            // Base da arte (última linha com pixel) na origem, para a pancada nascer do chão.
            Offset = grounded ? new Vector2(0.0f, Cell * 0.5f - (Cell - 1 - data.Bottom)) : Vector2.Zero,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        if (grounded)
            PixelRoom.StandUpright(fx._sprite);
        fx.AddChild(fx._sprite);
        return fx;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        var frame = (int)(_time * Fps);
        if (_loop)
        {
            _sprite.Frame = frame % _frames;
            return;
        }
        if (frame >= _frames)
        {
            QueueFree();
            return;
        }
        _sprite.Frame = frame;
    }

    private static bool TryLoad(string sheet, out (Texture2D Texture, int Frames, int Bottom) data)
    {
        if (Sheets.TryGetValue(sheet, out data))
            return true;
        var image = PixelRoomComposer.LoadImage(Root + sheet + ".png");
        if (image == null)
            return false;
        var used = image.GetUsedRect();
        data = (ImageTexture.CreateFromImage(image), Mathf.Max(image.GetWidth() / Cell, 1), used.End.Y - 1);
        Sheets[sheet] = data;
        return true;
    }
}
