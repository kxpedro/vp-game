using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BossController : Node
{
    public BossDefinition Definition { get; private set; }
    public int CurrentPhase { get; private set; } = 1;

    private readonly RandomNumberGenerator _rng = new();
    private EnemyBasic _enemy;
    private Health _health;
    private float _moveTimer;
    private int _moveIndex;
    private bool _actionInProgress;
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
        if (_enemy == null || _health == null || _health.IsDead || _actionInProgress || !_enemy.CanAttackAfterSpawn) return;
        _moveTimer -= (float)delta;
        if (_moveTimer <= 0.0f) StartNextMove();
    }

    private async void StartNextMove()
    {
        var move = SelectMove();
        if (move == null) return;

        _actionInProgress = true;
        _lastMoveId = move.Id;
        _enemy.ApplyBossMoveTuning(move, CurrentPhase);
        _enemy.PlayBossMoveAnimation(move.Id, CurrentPhase);
        _enemy.SetBossActionLocked(true);
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
        "dragon_volley" => ExecuteGroundVolley(move, 3 + CurrentPhase, 1.25f, 0.46f),
        "blood_charge" => ExecuteCharge(move, 6.4f, 0.58f),
        "meteor_requiem" => ExecuteMeteorField(move, 6, 1.35f, 0.82f),
        "psionic_volley" => ExecuteGroundVolley(move, 4 + CurrentPhase, 1.15f, 0.38f),
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
            await StrikeAt(_enemy.GlobalPosition + direction * (1.35f + hit * 0.18f), 1.45f, _enemy.AttackDamage * (hit == 2 ? 1.15f : 0.72f), 0.28f, move.DisplayName);
            await WaitSeconds(0.1f);
        }
    }

    private async Task ExecuteRadialPulse(BossMoveDefinition move, float maximumRadius, float windup, int pulses)
    {
        for (var pulse = 0; pulse < pulses; pulse++)
        {
            if (!CanContinue()) return;
            var radius = maximumRadius * (pulse + 1.0f) / pulses;
            await StrikeAt(_enemy.GlobalPosition, radius, _enemy.AttackDamage / Mathf.Max(pulses * 0.72f, 1.0f), windup, move.DisplayName);
            await WaitSeconds(0.12f);
        }
    }

    private async Task ExecuteGroundVolley(BossMoveDefinition move, int count, float radius, float cadence)
    {
        for (var shot = 0; shot < count; shot++)
        {
            if (!CanContinue()) return;
            var target = _enemy.GetCombatTarget();
            if (target == null) return;
            var lead = target is CharacterBody3D body ? body.Velocity * 0.22f : Vector3.Zero;
            var scatter = new Vector3(_rng.RandfRange(-0.65f, 0.65f), 0.0f, _rng.RandfRange(-0.65f, 0.65f));
            await StrikeAt(target.GlobalPosition + lead + scatter, radius, _enemy.AttackDamage * 0.58f, cadence, move.DisplayName);
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
        var telegraph = CreateLineTelegraph(direction, distance, chargeWidth);
        await WaitSeconds(windup * CastTimeMultiplier);
        if (!CanContinue()) { telegraph?.QueueFree(); return; }
        telegraph?.QueueFree();
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
        _enemy.TeleportBoss(ClampBossDestination(target.GlobalPosition + offset));
        _enemy.FaceCombatTarget();
        await StrikeAt(target.GlobalPosition, 1.55f, _enemy.AttackDamage * 0.65f, 0.35f, move.DisplayName);
    }

    private async Task ExecuteMirrorDash(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var origin = _enemy.GlobalPosition;
        var direction = FlatDirection(origin, target.GlobalPosition);
        var side = new Vector3(-direction.Z, 0.0f, direction.X) * (_rng.Randf() < 0.5f ? -1.0f : 1.0f);
        _enemy.TeleportBoss(ClampBossDestination(target.GlobalPosition + side * 4.8f));
        await StrikeAt(origin, 1.8f, _enemy.AttackDamage * 0.55f, 0.38f, move.DisplayName);
        if (!CanContinue()) return;
        await ExecuteCharge(move, 7.0f, 0.38f);
    }

    private async Task ExecuteMeteorField(BossMoveDefinition move, int count, float radius, float windup)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var centers = new List<Vector3>();
        var telegraphs = new List<MeshInstance3D>();
        centers.Add(target.GlobalPosition);
        for (var i = 1; i < count; i++)
        {
            var angle = Mathf.Tau * i / (count - 1.0f);
            centers.Add(target.GlobalPosition + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * _rng.RandfRange(2.4f, 4.8f));
        }
        var castTime = windup * CastTimeMultiplier;
        foreach (var center in centers) telegraphs.Add(CreateCircleTelegraph(center, radius, castTime));
        await WaitSeconds(castTime);
        if (CanContinue())
            foreach (var center in centers) DealDamageIfWithin(center, radius, _enemy.AttackDamage * 0.72f, move.DisplayName);
        foreach (var telegraph in telegraphs) if (IsInstanceValid(telegraph)) telegraph.QueueFree();
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
            await StrikeAt(center, radius, _enemy.AttackDamage * 0.42f, 0.34f, move.DisplayName);
        }
    }

    private async Task StrikeAt(Vector3 center, float radius, float damage, float windup, string source)
    {
        var castTime = windup * CastTimeMultiplier;
        var telegraph = CreateCircleTelegraph(center, radius, castTime);
        await WaitSeconds(castTime);
        if (CanContinue()) DealDamageIfWithin(center, radius, damage, source);
        if (IsInstanceValid(telegraph)) telegraph.QueueFree();
    }

    private MeshInstance3D CreateCircleTelegraph(Vector3 center, float radius, float windup)
    {
        var telegraph = new MeshInstance3D
        {
            Name = "BossCircleTelegraph",
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.045f, RadialSegments = 48 },
            MaterialOverride = CreateTelegraphMaterial(),
            Scale = new Vector3(0.15f, 1.0f, 0.15f)
        };
        GetTree().CurrentScene.AddChild(telegraph);
        // Procedural floors have their top at Y ~= 0.20. Keep the warning above that
        // surface instead of burying it at a fixed world height.
        var combatFloorY = Mathf.Max(center.Y, _enemy.GlobalPosition.Y);
        telegraph.GlobalPosition = new Vector3(center.X, combatFloorY + 0.065f, center.Z);
        telegraph.AddChild(new MeshInstance3D
        {
            Name = "BossTelegraphOutline",
            Mesh = new TorusMesh
            {
                InnerRadius = Mathf.Max(radius - 0.12f, 0.02f),
                OuterRadius = radius,
                Rings = 48,
                RingSegments = 12
            },
            MaterialOverride = CreateTelegraphOutlineMaterial(),
            Position = Vector3.Up * 0.035f
        });
        telegraph.CreateTween().TweenProperty(telegraph, "scale", Vector3.One, Mathf.Max(windup, 0.12f));
        return telegraph;
    }

    private MeshInstance3D CreateLineTelegraph(Vector3 direction, float length, float width)
    {
        var telegraph = new MeshInstance3D
        {
            Name = "BossChargeTelegraph",
            Mesh = new BoxMesh { Size = new Vector3(width, 0.045f, length) },
            MaterialOverride = CreateTelegraphMaterial()
        };
        GetTree().CurrentScene.AddChild(telegraph);
        telegraph.GlobalPosition = _enemy.GlobalPosition + direction * length * 0.5f + Vector3.Up * 0.08f;
        telegraph.LookAt(telegraph.GlobalPosition + direction, Vector3.Up);
        return telegraph;
    }

    private static StandardMaterial3D CreateTelegraphMaterial() => new()
    {
        AlbedoColor = new Color(0.95f, 0.015f, 0.04f, 0.48f),
        EmissionEnabled = true,
        Emission = new Color(1.0f, 0.0f, 0.08f),
        EmissionEnergyMultiplier = 3.2f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = 8
    };

    private static StandardMaterial3D CreateTelegraphOutlineMaterial() => new()
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

    private bool CanContinue() => IsInsideTree() && IsInstanceValid(_enemy) && _health != null && !_health.IsDead;

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
        GetTree().CallGroup("hud", "AppendGameLog", $"{Definition.DisplayName} entrou na fase {phase}!");
    }
}
