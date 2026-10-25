using Godot;
using System.Collections.Generic;

// Estados de herança de sangue no inimigo, legíveis de longe: cada estado ativo solta partículas na cor da família
// que o declara em blood_blessings.json ("statuses"; BloodlineVisuals.StatusColor) e o sprite 2D pulsa nessa cor.
// Com mais de um estado a tinta do corpo alterna entre eles. Burst = estouro curto quando o estado pega ou o golpe
// com herança acerta.
public partial class EnemyStatusVisuals : Node3D
{
    public const string Bleeding = "bleeding";
    public const string Pestilence = "pestilence";
    public const string Charm = "charm";
    public const string Fascination = "fascination";

    private const float TintStrength = 0.55f;
    private const float TintCycleSeconds = 0.8f;

    // Cor reserva se nenhuma família declarar o estado (a mesma dos rótulos de pilha de antes).
    private static readonly Dictionary<string, Color> Fallbacks = new()
    {
        [Bleeding] = new Color(1.0f, 0.12f, 0.24f),
        [Pestilence] = new Color(0.36f, 1.0f, 0.23f),
        [Charm] = new Color(1.0f, 0.31f, 0.75f),
        [Fascination] = new Color(1.0f, 0.31f, 0.75f),
    };

    private readonly Dictionary<string, CpuParticles3D> _emitters = new();
    private readonly List<Color> _activeColors = new();
    private PixelEnemySprite _sprite;
    private float _bodyHeight = 1.0f;
    private float _cycleTimer;
    private int _cycleIndex;

    public static Color ColorOf(string status) => BloodlineVisuals.StatusColor(status, Fallbacks.TryGetValue(status, out var fallback) ? fallback : Colors.White);

    public void Configure(PixelEnemySprite sprite, float overheadHeight)
    {
        _sprite = sprite;
        _bodyHeight = Mathf.Max(overheadHeight * 0.45f, 0.6f);
        foreach (var emitter in _emitters.Values)
            emitter.Position = Vector3.Up * _bodyHeight;
    }

    public void Refresh(bool bleeding, bool pestilent, bool charmed, bool fascinated)
    {
        _activeColors.Clear();
        SetEmitting(Bleeding, bleeding);
        SetEmitting(Pestilence, pestilent);
        // Encanto e fascínio são da mesma família: uma nuvem só.
        SetEmitting(Charm, charmed || fascinated);
        if (bleeding) _activeColors.Add(ColorOf(Bleeding));
        if (pestilent) _activeColors.Add(ColorOf(Pestilence));
        if (charmed || fascinated) _activeColors.Add(ColorOf(charmed ? Charm : Fascination));
    }

    public override void _Process(double delta)
    {
        if (_sprite == null || !IsInstanceValid(_sprite))
            return;
        if (_activeColors.Count == 0)
        {
            _sprite.SetStatusTint(Colors.White, 0.0f);
            return;
        }
        _cycleTimer -= (float)delta;
        if (_cycleTimer <= 0.0f)
        {
            _cycleTimer = TintCycleSeconds;
            _cycleIndex++;
        }
        _sprite.SetStatusTint(_activeColors[_cycleIndex % _activeColors.Count], TintStrength);
    }

    // Estouro de partículas no corpo (o estado acabou de pegar, ou o golpe com herança acertou).
    public void Burst(Color color, int amount = 14)
    {
        var scene = GetTree()?.CurrentScene;
        if (scene == null)
            return;
        var burst = new CpuParticles3D
        {
            Name = "BloodlineBurst",
            Mesh = BuildMesh(0.11f),
            Amount = Mathf.Max(amount, 1),
            Lifetime = 0.45f,
            OneShot = true,
            Explosiveness = 1.0f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.2f,
            Direction = Vector3.Up,
            Spread = 180.0f,
            Gravity = new Vector3(0.0f, -3.0f, 0.0f),
            InitialVelocityMin = 1.6f,
            InitialVelocityMax = 3.2f,
            ColorRamp = Fade(color),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Emitting = false,
        };
        scene.AddChild(burst);
        burst.GlobalPosition = GlobalPosition + Vector3.Up * _bodyHeight;
        burst.Emitting = true;
        var timer = burst.GetTree().CreateTimer(1.0f);
        timer.Timeout += () => { if (IsInstanceValid(burst)) burst.QueueFree(); };
    }

    private void SetEmitting(string status, bool on)
    {
        if (!_emitters.TryGetValue(status, out var emitter))
        {
            if (!on)
                return;
            emitter = BuildEmitter(status);
            _emitters[status] = emitter;
            AddChild(emitter);
        }
        if (emitter.Emitting != on)
            emitter.Emitting = on;
    }

    // Sangue pinga, pestilência borbulha para cima, encanto sobe devagar e espalhado.
    private CpuParticles3D BuildEmitter(string status)
    {
        var emitter = new CpuParticles3D
        {
            Name = "Status_" + status,
            Mesh = BuildMesh(status == Bleeding ? 0.08f : 0.1f),
            Amount = status == Charm ? 6 : 8,
            Lifetime = status == Bleeding ? 0.7f : 1.1f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.38f,
            Direction = Vector3.Up,
            Spread = status == Charm ? 70.0f : 25.0f,
            Gravity = status == Bleeding ? new Vector3(0.0f, -5.5f, 0.0f) : new Vector3(0.0f, status == Charm ? 0.5f : 1.1f, 0.0f),
            InitialVelocityMin = status == Bleeding ? 0.2f : 0.3f,
            InitialVelocityMax = status == Bleeding ? 0.8f : 0.7f,
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.2f,
            ColorRamp = Fade(ColorOf(status)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = Vector3.Up * _bodyHeight,
            Emitting = false,
        };
        return emitter;
    }

    // Quadradinho sem luz, de frente para a câmera: lê como pixel ao lado do sprite.
    private static QuadMesh BuildMesh(float size) => new()
    {
        Size = new Vector2(size, size),
        Material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        },
    };

    private static Gradient Fade(Color color)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(BloodlineVisuals.Lighten(color, 0.25f), 1.0f));
        gradient.SetColor(1, new Color(color, 0.0f));
        return gradient;
    }
}
