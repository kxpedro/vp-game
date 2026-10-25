using Godot;
using System.Threading.Tasks;

// Invoca lacaios durante a luta de boss: a sala de boss so tem um inimigo, e a Furia Bestial so carrega por abate.
// Filho do BossController (criado em Configure). Invoca em intervalo fixo (mais curto a cada fase) e na troca de fase,
// respeitando o teto de lacaios vivos. Os lacaios nao contam para limpar a sala e somem quando o boss morre.
public partial class BossMinionSummoner : Node
{
    private static float G(string key, float defaultValue) => CompendiumTuning.GetFloat(key, defaultValue);

    // Rosa da rampa violeta da Guardia (AAP-64 bc4a9b, BossRuneFx.Violet[2]): marca do 3D.
    private static readonly Color WarningColor = BossRuneFx.Violet[2];

    private readonly RandomNumberGenerator _rng = new();
    private EnemyBasic _boss;
    private BossController _controller;
    private Health _bossHealth;
    private float _timer;
    private int _lastPhase = 1;
    private int _pendingSpawns;
    private bool _awake;

    public void Configure(EnemyBasic boss, BossController controller)
    {
        _boss = boss;
        _controller = controller;
        _bossHealth = boss.GetNode<Health>("Health");
        _bossHealth.Died += OnBossDied;
        _rng.Randomize();
    }

    public override void _ExitTree()
    {
        if (_bossHealth != null) _bossHealth.Died -= OnBossDied;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_boss) || _bossHealth == null || _bossHealth.IsDead) return;
        // Boss ainda surgindo/dormente: o relogio so comeca quando ele pode atacar.
        if (!_boss.CanAttackAfterSpawn) return;
        if (!_awake)
        {
            _awake = true;
            _timer = G("boss.minions.first_delay", 5.0f);
        }

        var phase = _controller?.CurrentPhase ?? 1;
        if (phase > _lastPhase)
        {
            _lastPhase = phase;
            if (G("boss.minions.on_phase", 1.0f) > 0.5f)
                Summon(phase);
        }

        _timer -= (float)delta;
        if (_timer > 0.0f) return;
        _timer = G("boss.minions.interval", 12.0f) / (1.0f + (phase - 1) * 0.15f);
        Summon(phase);
    }

    private void Summon(int phase)
    {
        var maxAlive = Mathf.RoundToInt(G("boss.minions.max_alive", 4.0f));
        var alive = GetTree().GetNodesInGroup("boss_minions").Count + _pendingSpawns;
        var count = Mathf.Min(Mathf.RoundToInt(G("boss.minions.count_base", 2.0f)) + phase - 1, maxAlive - alive);
        if (count <= 0) return;

        var runManager = GetTree().GetFirstNodeInGroup("run_manager") as RunManager;
        if (runManager == null) return;
        var positions = new Godot.Collections.Array<Vector3>();
        var center = _boss.GlobalPosition;
        var minDistance = G("boss.minions.distance_min", 6.0f);
        var maxDistance = Mathf.Max(G("boss.minions.distance_max", 8.5f), minDistance);
        var offset = _rng.Randf() * Mathf.Tau;
        for (var i = 0; i < count; i++)
        {
            // Longe do boss: perto da parede o ponto e puxado para dentro da sala, entao tenta outros angulos
            // e fica com o mais distante se nenhum alcancar a distancia minima.
            var best = center;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var angle = offset + Mathf.Tau * i / count + (attempt == 0 ? _rng.RandfRange(-0.3f, 0.3f) : _rng.Randf() * Mathf.Tau);
                var point = runManager.ClampToRoom(center + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _rng.RandfRange(minDistance, maxDistance));
                if (FlatDistance(point, center) > FlatDistance(best, center)) best = point;
                if (FlatDistance(best, center) >= minDistance) break;
            }
            positions.Add(best);
        }
        // Log de debug (literal).
        GetTree().CallGroup("hud", "AppendGameLog", $"{_controller?.Definition?.DisplayName} invoca {count} lacaio(s).");
        _ = SpawnAfterWarning(positions);
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // Marca no chao por boss.minions.warning segundos; depois os lacaios surgem nela.
    // No 2D e o circulo de ritual das waves na rampa violeta (estoura quando o lacaio surge); no 3D, um disco rosa.
    private async Task SpawnAfterWarning(Godot.Collections.Array<Vector3> positions)
    {
        var warning = G("boss.minions.warning", 0.9f);
        var marks = new Godot.Collections.Array<Node3D>();
        foreach (var position in positions)
            marks.Add(SpawnWarningMark(position, warning));
        _pendingSpawns += positions.Count;
        GetTree().CallGroup("audio_manager", "PlayEffect", "boss_windup");

        await ToSignal(GetTree().CreateTimer(Mathf.Max(warning, 0.05f), false), SceneTreeTimer.SignalName.Timeout);

        _pendingSpawns = Mathf.Max(_pendingSpawns - positions.Count, 0);
        foreach (var mark in marks)
        {
            if (!IsInstanceValid(mark)) continue;
            if (mark is SpawnRitualCircle circle) circle.Burst();
            else mark.QueueFree();
        }
        if (!IsInsideTree() || !IsInstanceValid(_boss) || _bossHealth.IsDead) return;
        (GetTree().GetFirstNodeInGroup("run_manager") as RunManager)?.SpawnBossMinions(positions);
    }

    private Node3D SpawnWarningMark(Vector3 position, float seconds)
    {
        if (GameMode.IsPixelArt)
        {
            var circle = SpawnRitualCircle.Spawn(GetTree().CurrentScene, position, seconds, SpawnRitualCircle.VioletSheetPath);
            if (circle != null) return circle;
        }
        var mark = new MeshInstance3D
        {
            Name = "BossMinionWarning",
            Mesh = new CylinderMesh { TopRadius = 0.85f, BottomRadius = 0.85f, Height = 0.02f, RadialSegments = 32 },
            Scale = new Vector3(0.2f, 1.0f, 0.2f)
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(WarningColor, 0.15f),
            EmissionEnabled = true,
            Emission = WarningColor,
            EmissionEnergyMultiplier = 3.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            RenderPriority = 9
        };
        mark.MaterialOverride = material;
        GetTree().CurrentScene.AddChild(mark);
        mark.GlobalPosition = new Vector3(position.X, 0.06f, position.Z);
        var tween = mark.CreateTween().SetParallel();
        tween.TweenProperty(mark, "scale", Vector3.One, seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(material, "albedo_color:a", 0.75f, seconds);
        return mark;
    }

    // O boss caiu: os lacaios restantes somem sem dar Furia nem recompensa.
    private void OnBossDied()
    {
        foreach (var node in GetTree().GetNodesInGroup("boss_minions"))
            if (IsInstanceValid(node)) node.QueueFree();
    }
}
