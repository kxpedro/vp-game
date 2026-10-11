using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BossController : Node
{
    // ---- Piso minimo de aviso (segundos): ponto unico de tuning da legibilidade dos golpes do boss. ----
    // Aplicado sobre o tempo final (ja com o multiplicador de cast do boss).
    private const float MinSingleCircleWarning = 0.50f;   // circulos isolados / de sequencia (psionic, singularity, kinetic 2o golpe)
    private const float MinSequenceFirstWarning = 0.60f;  // primeiro circulo de uma sequencia
    private const float MinLineWarning = 0.60f;           // cargas / linhas
    private const float MinClawFirstWarning = 0.45f;      // primeiro golpe do claw_combo
    private const float MinClawFollowWarning = 0.35f;     // golpes 2 e 3 do claw_combo
    private const float TeleportWarningSeconds = 0.40f;   // aviso antes do boss reaparecer
    private const float PhaseBreakSeconds = 0.50f;        // pausa na transicao de fase (invulnerabilidade so por esse intervalo)

    // Meshes/materiais compartilhados entre telegraphs (os que precisam de tween de alfa sao criados por instancia).
    private static CylinderMesh _unitDisk;
    private static PrismMesh _chevronMesh;
    private static StandardMaterial3D _backdropMaterial;
    private static StandardMaterial3D _outlineMaterial;
    private static readonly Dictionary<int, TorusMesh> RingMeshes = new();

    public BossDefinition Definition { get; private set; }
    public int CurrentPhase { get; private set; } = 1;

    private readonly RandomNumberGenerator _rng = new();
    private EnemyBasic _enemy;
    private Health _health;
    private float _moveTimer;
    private float _phaseBreakRemaining;
    private int _moveIndex;
    private int _circlesInMove;
    private bool _actionInProgress;
    // Cancelamento de golpe na troca de fase: o golpe em andamento so continua se as epocas coincidirem.
    private int _cancelEpoch;
    private int _moveEpoch;
    private readonly List<Node3D> _activeTelegraphs = new();
    private string _lastMoveId = "";
    private float CastTimeMultiplier => Definition?.Id == "silver_dragonkin" ? 1.3f : 1.0f;

    public void Configure(EnemyBasic enemy, BossDefinition definition)
    {
        _enemy = enemy;
        Definition = definition;
        _health = enemy.GetNode<Health>("Health");
        _rng.Randomize();
        AddToGroup("boss_controller");
        _health.SetMaxHealth(Mathf.Max(definition.EstimatedMaxHealth, 1000), true);
        _health.HealthChanged += OnHealthChanged;
        _enemy.EnableBossMoves();
        _moveTimer = 1.1f;
    }

    public override void _ExitTree()
    {
        if (_health != null) _health.HealthChanged -= OnHealthChanged;
    }

    public override void _Process(double delta)
    {
        if (_enemy == null || _health == null || _health.IsDead) return;
        if (_phaseBreakRemaining > 0.0f)
        {
            _phaseBreakRemaining -= (float)delta;
            return;
        }
        if (_actionInProgress || !_enemy.CanAttackAfterSpawn) return;
        _moveTimer -= (float)delta;
        if (_moveTimer <= 0.0f) StartNextMove();
    }

    private async void StartNextMove()
    {
        var move = SelectMove();
        if (move == null) return;

        _actionInProgress = true;
        _moveEpoch = _cancelEpoch;
        _lastMoveId = move.Id;
        _circlesInMove = 0;
        _enemy.ApplyBossMoveTuning(move, CurrentPhase);
        _enemy.PlayBossMoveAnimation(move.Id, CurrentPhase);
        _enemy.SetBossActionLocked(true);
        // Aviso de inicio de golpe: som + flash no corpo + anel no chao.
        PlayCue("boss_windup");
        _enemy.FlashWindup();
        SpawnRing(_enemy.GlobalPosition, 0.9f, 2.6f, 0.35f, new Color(1.0f, 0.7f, 0.15f));
        // Log de debug (literal): o texto exibido ao jogador nao passa por aqui.
        GetTree().CallGroup("hud", "AppendGameLog", $"{Definition.DisplayName} prepara {move.DisplayName}.");

        await ExecuteMove(move);

        if (IsInstanceValid(_enemy) && !_health.IsDead)
            _enemy.SetBossActionLocked(false);
        _actionInProgress = false;
        _moveTimer = Mathf.Max(move.Cooldown / (1.0f + (CurrentPhase - 1) * 0.12f), 0.75f);
    }

    private BossMoveDefinition SelectMove()
    {
        if (Definition?.Moves == null || Definition.Moves.Length == 0) return null;
        var available = new List<BossMoveDefinition>();
        foreach (var move in Definition.Moves)
            if (move.MinimumPhase <= CurrentPhase && (move.Id != _lastMoveId || Definition.Moves.Length == 1))
                available.Add(move);
        if (available.Count == 0)
            foreach (var move in Definition.Moves)
                if (move.MinimumPhase <= CurrentPhase) available.Add(move);
        if (available.Count == 0) return null;
        var selected = available[_moveIndex % available.Count];
        _moveIndex++;
        return selected;
    }

    private Task ExecuteMove(BossMoveDefinition move) => move.Id switch
    {
        "claw_combo" => ExecuteClawCombo(move),
        "tail_sweep" => ExecuteRadialPulse(move, 3.6f, 0.62f, 1),
        "dragon_volley" => ExecuteGroundVolley(move, 3 + CurrentPhase, 1.25f, 0.46f, MinSequenceFirstWarning, 0.0f),
        "blood_charge" => ExecuteCharge(move, 6.4f, 0.58f),
        "meteor_requiem" => ExecuteMeteorField(move, 6, 1.35f, 0.82f),
        "psionic_volley" => ExecuteGroundVolley(move, 4 + CurrentPhase, 1.15f, 0.38f, MinSequenceFirstWarning, MinSingleCircleWarning),
        "kinetic_shift" => ExecuteKineticShift(move),
        "force_outbreak" => ExecuteRadialPulse(move, 5.0f, 0.48f, 2 + CurrentPhase),
        "mirror_dash" => ExecuteMirrorDash(move),
        "singularity" => ExecuteSingularity(move),
        _ => ExecuteRadialPulse(move, 2.8f, 0.55f, 1)
    };

    private async Task ExecuteClawCombo(BossMoveDefinition move)
    {
        for (var hit = 0; hit < 3; hit++)
        {
            if (!CanContinue()) return;
            _enemy.FaceCombatTarget();
            var target = _enemy.GetCombatTarget();
            if (target == null) return;
            var direction = FlatDirection(_enemy.GlobalPosition, target.GlobalPosition);
            await StrikeAt(_enemy.GlobalPosition + direction * (1.35f + hit * 0.18f), 1.45f, _enemy.AttackDamage * (hit == 2 ? 1.15f : 0.72f), 0.28f, move.DisplayName,
                hit == 0 ? MinClawFirstWarning : MinClawFollowWarning);
            await WaitSeconds(0.1f);
        }
    }

    private async Task ExecuteRadialPulse(BossMoveDefinition move, float maximumRadius, float windup, int pulses)
    {
        for (var pulse = 0; pulse < pulses; pulse++)
        {
            if (!CanContinue()) return;
            var radius = maximumRadius * (pulse + 1.0f) / pulses;
            var floor = pulses == 1 ? MinSingleCircleWarning : pulse == 0 ? MinSequenceFirstWarning : 0.0f;
            await StrikeAt(_enemy.GlobalPosition, radius, _enemy.AttackDamage / Mathf.Max(pulses * 0.72f, 1.0f), windup, move.DisplayName, floor);
            await WaitSeconds(0.12f);
        }
    }

    private async Task ExecuteGroundVolley(BossMoveDefinition move, int count, float radius, float cadence, float firstFloor, float restFloor)
    {
        for (var shot = 0; shot < count; shot++)
        {
            if (!CanContinue()) return;
            var target = _enemy.GetCombatTarget();
            if (target == null) return;
            var lead = target is CharacterBody3D body ? body.Velocity * 0.22f : Vector3.Zero;
            var scatter = new Vector3(_rng.RandfRange(-0.65f, 0.65f), 0.0f, _rng.RandfRange(-0.65f, 0.65f));
            await StrikeAt(target.GlobalPosition + lead + scatter, radius, _enemy.AttackDamage * 0.58f, cadence, move.DisplayName, shot == 0 ? firstFloor : restFloor);
            await WaitSeconds(0.08f);
        }
    }

    private async Task ExecuteCharge(BossMoveDefinition move, float distance, float windup)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var origin = _enemy.GlobalPosition;
        var direction = FlatDirection(_enemy.GlobalPosition, target.GlobalPosition);
        const float chargeWidth = 1.7f;
        var castTime = Mathf.Max(windup * CastTimeMultiplier, MinLineWarning);
        var telegraph = CreateLineTelegraph(direction, distance, chargeWidth, castTime);
        var threat = IsInstanceValid(_enemy) ? _enemy.AddBossLineThreat(origin, origin + direction * distance, chargeWidth * 0.5f) : 0;
        await WaitSeconds(castTime);
        RemoveThreat(threat);
        if (!CanContinue()) { ReleaseTelegraph(telegraph, false); return; }
        ReleaseTelegraph(telegraph, true);
        PlayCue("boss_impact");
        _enemy.MoveBoss(direction * distance);
        DealDamageAlongSegment(origin, _enemy.GlobalPosition, chargeWidth * 0.5f, _enemy.AttackDamage, move.DisplayName);
        await WaitSeconds(0.18f);
    }

    private async Task ExecuteKineticShift(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        _health.ShieldHits += 2 + CurrentPhase;
        await StrikeAt(_enemy.GlobalPosition, 2.4f, _enemy.AttackDamage * 0.45f, 0.42f, move.DisplayName);
        if (!CanContinue()) return;
        var angle = _rng.RandfRange(0.0f, Mathf.Tau);
        var offset = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * 5.2f;
        var destination = ClampBossDestination(target.GlobalPosition + offset);
        await WarnTeleport(destination);
        if (!CanContinue()) return;
        _enemy.TeleportBoss(destination);
        _enemy.FaceCombatTarget();
        await StrikeAt(target.GlobalPosition, 1.55f, _enemy.AttackDamage * 0.65f, 0.35f, move.DisplayName, MinSingleCircleWarning);
    }

    private async Task ExecuteMirrorDash(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var origin = _enemy.GlobalPosition;
        var direction = FlatDirection(origin, target.GlobalPosition);
        var side = new Vector3(-direction.Z, 0.0f, direction.X) * (_rng.Randf() < 0.5f ? -1.0f : 1.0f);
        var destination = ClampBossDestination(target.GlobalPosition + side * 4.8f);
        await WarnTeleport(destination);
        if (!CanContinue()) return;
        _enemy.TeleportBoss(destination);
        await StrikeAt(origin, 1.8f, _enemy.AttackDamage * 0.55f, 0.38f, move.DisplayName, MinSingleCircleWarning);
        if (!CanContinue()) return;
        await ExecuteCharge(move, 7.0f, 0.38f);
    }

    private async Task ExecuteMeteorField(BossMoveDefinition move, int count, float radius, float windup)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var centers = new List<Vector3>();
        var telegraphs = new List<Node3D>();
        var threats = new List<int>();
        centers.Add(target.GlobalPosition);
        for (var i = 1; i < count; i++)
        {
            var angle = Mathf.Tau * i / (count - 1.0f);
            centers.Add(target.GlobalPosition + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _rng.RandfRange(2.4f, 4.8f));
        }
        var castTime = Mathf.Max(windup * CastTimeMultiplier, MinSequenceFirstWarning);
        foreach (var center in centers)
        {
            telegraphs.Add(CreateCircleTelegraph(center, radius, castTime));
            threats.Add(AddThreat(center, radius));
        }
        await WaitSeconds(castTime);
        foreach (var threat in threats) RemoveThreat(threat);
        var canContinue = CanContinue();
        if (canContinue) PlayCue("boss_impact");
        foreach (var telegraph in telegraphs) ReleaseTelegraph(telegraph, canContinue);
        if (canContinue)
            foreach (var center in centers) DealDamageIfWithin(center, radius, _enemy.AttackDamage * 0.72f, move.DisplayName);
    }

    private async Task ExecuteSingularity(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var center = target.GlobalPosition;
        for (var pulse = 0; pulse < 3 + CurrentPhase; pulse++)
        {
            if (!CanContinue()) return;
            var radius = 1.4f + pulse * 0.8f;
            await StrikeAt(center, radius, _enemy.AttackDamage * 0.42f, 0.34f, move.DisplayName, pulse == 0 ? MinSequenceFirstWarning : MinSingleCircleWarning);
        }
    }

    private async Task StrikeAt(Vector3 center, float radius, float damage, float windup, string source, float minCast = 0.0f)
    {
        var castTime = Mathf.Max(windup * CastTimeMultiplier, minCast);
        var telegraph = CreateCircleTelegraph(center, radius, castTime);
        // Tick por circulo de sequencia (o primeiro ja tem o som de inicio do golpe).
        if (_circlesInMove++ > 0) PlayCue("boss_tick");
        var threat = AddThreat(center, radius);
        await WaitSeconds(castTime);
        RemoveThreat(threat);
        var canContinue = CanContinue();
        ReleaseTelegraph(telegraph, canContinue);
        if (canContinue)
        {
            PlayCue("boss_impact");
            DealDamageIfWithin(center, radius, damage, source);
        }
    }

    // ---- Telegraphs ----

    private static CylinderMesh UnitDisk => _unitDisk ??= new CylinderMesh { TopRadius = 1.0f, BottomRadius = 1.0f, Height = 0.045f, RadialSegments = 48 };

    private static TorusMesh GetRingMesh(float radius)
    {
        var key = Mathf.RoundToInt(radius * 20.0f);
        if (!RingMeshes.TryGetValue(key, out var mesh))
        {
            mesh = new TorusMesh
            {
                InnerRadius = Mathf.Max(radius - 0.12f, 0.02f),
                OuterRadius = radius,
                Rings = 48,
                RingSegments = 12
            };
            RingMeshes[key] = mesh;
        }
        return mesh;
    }

    private Node3D CreateCircleTelegraph(Vector3 center, float radius, float windup)
    {
        var telegraph = new Node3D { Name = "BossCircleTelegraph" };
        GetTree().CurrentScene.AddChild(telegraph);
        _activeTelegraphs.Add(telegraph);
        // Procedural floors have their top at Y ~= 0.20. Keep the warning above that
        // surface instead of burying it at a fixed world height.
        var combatFloorY = Mathf.Max(center.Y, _enemy.GlobalPosition.Y);
        telegraph.GlobalPosition = new Vector3(center.X, combatFloorY + 0.065f, center.Z);

        // Contorno e fundo ja no tamanho final desde o primeiro frame; so o interior cresce.
        telegraph.AddChild(new MeshInstance3D
        {
            Name = "TelegraphBackdrop",
            Mesh = UnitDisk,
            MaterialOverride = BackdropMaterial,
            Scale = new Vector3(radius, 1.0f, radius)
        });
        var fill = new MeshInstance3D
        {
            Name = "TelegraphFill",
            Mesh = UnitDisk,
            MaterialOverride = CreateTelegraphMaterial(),
            Position = Vector3.Up * 0.01f,
            Scale = new Vector3(radius * 0.1f, 1.0f, radius * 0.1f)
        };
        telegraph.AddChild(fill);
        telegraph.AddChild(new MeshInstance3D
        {
            Name = "BossTelegraphOutline",
            Mesh = GetRingMesh(radius),
            MaterialOverride = OutlineMaterial,
            Position = Vector3.Up * 0.035f
        });
        fill.CreateTween().TweenProperty(fill, "scale", new Vector3(radius, 1.0f, radius), Mathf.Max(windup, 0.12f));
        return telegraph;
    }

    private Node3D CreateLineTelegraph(Vector3 direction, float length, float width, float windup)
    {
        var telegraph = new Node3D { Name = "BossChargeTelegraph" };
        GetTree().CurrentScene.AddChild(telegraph);
        _activeTelegraphs.Add(telegraph);
        telegraph.GlobalPosition = _enemy.GlobalPosition + Vector3.Up * 0.08f;
        telegraph.LookAt(telegraph.GlobalPosition + direction, Vector3.Up);

        // Fundo + bordas no tamanho final; interior preenche da origem do boss ate o fim da carga.
        telegraph.AddChild(new MeshInstance3D
        {
            Name = "TelegraphBackdrop",
            Mesh = new BoxMesh { Size = new Vector3(width, 0.045f, length) },
            MaterialOverride = BackdropMaterial,
            Position = new Vector3(0.0f, 0.0f, -length * 0.5f)
        });
        var pivot = new Node3D { Name = "TelegraphFillPivot", Scale = new Vector3(1.0f, 1.0f, 0.001f) };
        telegraph.AddChild(pivot);
        pivot.AddChild(new MeshInstance3D
        {
            Name = "TelegraphFill",
            Mesh = new BoxMesh { Size = new Vector3(width, 0.045f, 1.0f) },
            MaterialOverride = CreateTelegraphMaterial(),
            Position = new Vector3(0.0f, 0.01f, -0.5f)
        });
        var edgeMesh = new BoxMesh { Size = new Vector3(0.09f, 0.05f, length) };
        foreach (var sign in new[] { -1.0f, 1.0f })
            telegraph.AddChild(new MeshInstance3D
            {
                Name = "TelegraphEdge",
                Mesh = edgeMesh,
                MaterialOverride = OutlineMaterial,
                Position = new Vector3(sign * (width * 0.5f - 0.045f), 0.03f, -length * 0.5f)
            });
        // Chevrons de direcao (apontam para o fim da carga).
        _chevronMesh ??= new PrismMesh { Size = new Vector3(1.0f, 0.8f, 0.05f) };
        foreach (var fraction in new[] { 0.22f, 0.5f, 0.78f })
            telegraph.AddChild(new MeshInstance3D
            {
                Name = "TelegraphChevron",
                Mesh = _chevronMesh,
                MaterialOverride = OutlineMaterial,
                Position = new Vector3(0.0f, 0.05f, -length * fraction),
                RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
                Scale = new Vector3(width * 0.7f, 1.0f, 1.0f)
            });
        pivot.CreateTween().TweenProperty(pivot, "scale", new Vector3(1.0f, 1.0f, length), Mathf.Max(windup, 0.12f));
        return telegraph;
    }

    // Fim do telegraph: flash do preenchimento (impacto) e remocao.
    private void ReleaseTelegraph(Node3D telegraph, bool impact)
    {
        _activeTelegraphs.Remove(telegraph);
        if (!IsInstanceValid(telegraph)) return;
        var fill = telegraph.FindChild("TelegraphFill", true, false) as MeshInstance3D;
        if (!impact || fill?.MaterialOverride is not StandardMaterial3D material)
        {
            telegraph.QueueFree();
            return;
        }
        material.AlbedoColor = new Color(1.0f, 0.95f, 0.75f, 0.95f);
        material.Emission = new Color(1.0f, 0.85f, 0.5f);
        material.EmissionEnergyMultiplier = 7.0f;
        var tween = telegraph.CreateTween();
        tween.TweenProperty(material, "albedo_color:a", 0.0f, 0.2f);
        tween.Parallel().TweenProperty(telegraph, "scale", new Vector3(1.1f, 1.0f, 1.1f), 0.2f);
        tween.TweenCallback(Callable.From(telegraph.QueueFree));
    }

    private static StandardMaterial3D BackdropMaterial => _backdropMaterial ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.95f, 0.1f, 0.03f, 0.2f),
        EmissionEnabled = true,
        Emission = new Color(1.0f, 0.1f, 0.03f),
        EmissionEnergyMultiplier = 1.5f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = 7
    };

    // Preenchimento (um por instancia: o alfa e animado no impacto).
    private static StandardMaterial3D CreateTelegraphMaterial() => new()
    {
        AlbedoColor = new Color(0.95f, 0.015f, 0.04f, 0.55f),
        EmissionEnabled = true,
        Emission = new Color(1.0f, 0.0f, 0.08f),
        EmissionEnergyMultiplier = 3.2f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = 8
    };

    private static StandardMaterial3D OutlineMaterial => _outlineMaterial ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(1.0f, 0.72f, 0.12f, 0.98f),
        EmissionEnabled = true,
        Emission = new Color(1.0f, 0.18f, 0.02f),
        EmissionEnergyMultiplier = 4.5f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = 9
    };

    // ---- Avisos extras: anel de inicio de golpe / fase, teleporte ----

    private void SpawnRing(Vector3 position, float fromScale, float toScale, float seconds, Color color, bool fadeIn = false)
    {
        var ring = new MeshInstance3D
        {
            Name = "BossCueRing",
            Mesh = GetRingMesh(1.0f),
            Scale = new Vector3(fromScale, 1.0f, fromScale)
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, fadeIn ? 0.3f : 0.9f),
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 4.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = 10
        };
        ring.MaterialOverride = material;
        GetTree().CurrentScene.AddChild(ring);
        ring.GlobalPosition = new Vector3(position.X, _enemy.GlobalPosition.Y + 0.09f, position.Z);
        var tween = ring.CreateTween();
        tween.TweenProperty(ring, "scale", new Vector3(toScale, 1.0f, toScale), seconds);
        tween.Parallel().TweenProperty(material, "albedo_color:a", fadeIn ? 1.0f : 0.0f, seconds);
        tween.TweenCallback(Callable.From(ring.QueueFree));
    }

    // Aviso de teleporte: som + flash no corpo + anel no ponto de saida e coluna/anel convergente no destino.
    private async Task WarnTeleport(Vector3 destination)
    {
        if (!CanContinue()) return;
        PlayCue("boss_teleport");
        _enemy.FlashWindup();
        var violet = new Color(0.78f, 0.3f, 1.0f);
        SpawnRing(_enemy.GlobalPosition, 1.2f, 0.4f, TeleportWarningSeconds, violet);
        SpawnRing(destination, 2.4f, 1.0f, TeleportWarningSeconds, violet, true);

        var beam = new MeshInstance3D
        {
            Name = "BossTeleportBeam",
            Mesh = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.45f, Height = 3.5f, RadialSegments = 16 }
        };
        var beamMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(violet.R, violet.G, violet.B, 0.05f),
            EmissionEnabled = true,
            Emission = violet,
            EmissionEnergyMultiplier = 3.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        beam.MaterialOverride = beamMaterial;
        GetTree().CurrentScene.AddChild(beam);
        beam.GlobalPosition = new Vector3(destination.X, _enemy.GlobalPosition.Y + 1.75f, destination.Z);
        var tween = beam.CreateTween();
        tween.TweenProperty(beamMaterial, "albedo_color:a", 0.4f, TeleportWarningSeconds);
        tween.TweenCallback(Callable.From(beam.QueueFree));

        await WaitSeconds(TeleportWarningSeconds);
    }

    private void PlayCue(string effect) => GetTree().CallGroup("audio_manager", "PlayEffect", effect);

    private int AddThreat(Vector3 center, float radius) => IsInstanceValid(_enemy) ? _enemy.AddBossThreat(center, radius) : 0;

    private void RemoveThreat(int id)
    {
        if (id != 0 && IsInstanceValid(_enemy)) _enemy.RemoveBossThreat(id);
    }

    private void DealDamageIfWithin(Vector3 center, float radius, float damage, string source)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var delta = target.GlobalPosition - center;
        delta.Y = 0.0f;
        if (delta.Length() <= radius)
            target.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage, false, source);
    }

    private void DealDamageAlongSegment(Vector3 start, Vector3 end, float radius, float damage, string source)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;

        start.Y = 0.0f;
        end.Y = 0.0f;
        var targetPosition = target.GlobalPosition;
        targetPosition.Y = 0.0f;
        var segment = end - start;
        var segmentLengthSquared = segment.LengthSquared();
        var progress = segmentLengthSquared > 0.0001f
            ? Mathf.Clamp((targetPosition - start).Dot(segment) / segmentLengthSquared, 0.0f, 1.0f)
            : 0.0f;
        var closestPoint = start + segment * progress;
        if (targetPosition.DistanceTo(closestPoint) <= radius)
            target.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage, false, source);
    }

    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        var direction = to - from;
        direction.Y = 0.0f;
        return direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector3.Forward;
    }

    private Vector3 ClampBossDestination(Vector3 destination)
    {
        return GetTree().GetFirstNodeInGroup("room_generator") is RoomGenerator roomGenerator
            ? roomGenerator.ClampToWalkableArea(destination, 2)
            : destination;
    }

    private bool CanContinue() => IsInsideTree() && IsInstanceValid(_enemy) && _health != null && !_health.IsDead && _moveEpoch == _cancelEpoch;

    private async Task WaitSeconds(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(Mathf.Max(seconds, 0.01f), false), SceneTreeTimer.SignalName.Timeout);
    }

    private void OnHealthChanged(float current, float maximum)
    {
        var ratio = maximum > 0 ? current / maximum : 0;
        var phase = ratio <= 0.33f ? 3 : ratio <= 0.66f ? 2 : 1;
        if (phase == CurrentPhase) return;
        CurrentPhase = phase;
        _enemy.MoveSpeed *= 1.12f;
        _moveTimer = Mathf.Min(_moveTimer, 0.45f);
        // Log de debug (literal).
        GetTree().CallGroup("hud", "AppendGameLog", $"{Definition.DisplayName} entrou na fase {phase}!");
        if (_health.IsDead) return;
        CancelCurrentMove();
        PhaseTransitionCue();
    }

    // Cancela o golpe em andamento: telegraphs somem sem impacto e as areas de ameaca sao limpas.
    private void CancelCurrentMove()
    {
        _cancelEpoch++;
        foreach (var telegraph in _activeTelegraphs.ToArray())
            if (IsInstanceValid(telegraph)) telegraph.QueueFree();
        _activeTelegraphs.Clear();
        if (IsInstanceValid(_enemy)) _enemy.ClearBossThreats();
    }

    // Pausa curta com flash e anel na troca de fase. Invulnerabilidade apenas durante PhaseBreakSeconds.
    private async void PhaseTransitionCue()
    {
        _phaseBreakRemaining = PhaseBreakSeconds;
        var wasInvincible = _health.IsInvincible;
        _health.SetInvincible(true);
        PlayCue("boss_phase");
        SpawnRing(_enemy.GlobalPosition, 1.0f, 7.0f, PhaseBreakSeconds, new Color(1.0f, 0.85f, 0.4f));
        for (var flash = 0; flash < 3; flash++)
        {
            if (!IsInstanceValid(_enemy)) break;
            _enemy.FlashWindup();
            await WaitSeconds(PhaseBreakSeconds / 3.0f);
        }
        if (IsInstanceValid(_health) && !_health.IsDead)
            _health.SetInvincible(wasInvincible);
    }
}
