using Godot;
using System.Threading.Tasks;

public partial class EnemyBasic : CharacterBody3D
{
    [Export] public float MoveSpeed { get; set; } = 3.2f;
    [Export] public float DetectionRange { get; set; } = 22.0f;
    [Export] public bool RequireLineOfSight { get; set; } = true;
    [Export] public float LineOfSightEyeHeight { get; set; } = 1.05f;
    [Export] public float PatrolSpeed { get; set; } = 1.65f;
    [Export] public float PatrolRadius { get; set; } = 6.0f;
    [Export] public float PatrolRetargetMinTime { get; set; } = 1.2f;
    [Export] public float PatrolRetargetMaxTime { get; set; } = 3.0f;
    [Export] public float AttackRange { get; set; } = 1.65f;
    [Export] public float AttackDamage { get; set; } = 18.0f;
    [Export] public float AttackWindup { get; set; } = 0.85f;
    [Export] public float AttackCooldown { get; set; } = 1.55f;
    [Export] public float AttackWidth { get; set; } = 1.25f;
    [Export] public float AttackDepthBonus { get; set; } = 0.75f;
    [Export] public float ContactDamage { get; set; } = 5.0f;
    [Export] public float ContactDamageRange { get; set; } = 0.95f;
    [Export] public float ContactDamageCooldown { get; set; } = 0.8f;
    [Export] public float KnockbackResistance { get; set; } = 0.45f;
    [Export] public int MinBloodCoinsReward { get; set; } = 1;
    [Export] public int MaxBloodCoinsReward { get; set; } = 10;
    [Export] public float FuryOnDeath { get; set; } = 12.0f;
    [Export] public float HealthPotionDropChance { get; set; } = 0.10f;
    [Export] public NodePath HealthPath { get; set; } = new("Health");
    [Export] public NodePath VisualPath { get; set; } = new("Visual");

    private Node3D _player;
    private Health _health;
    private MeshInstance3D _visual;
    private bool _isAttacking;
    private bool _hasDetectedPlayer;
    private Vector3 _lockedAttackDirection = Vector3.Forward;
    private Vector3 _knockbackVelocity;
    private Vector3 _spawnPosition;
    private Vector3 _patrolTarget;
    private float _patrolRetargetTimer;
    private StandardMaterial3D _baseMaterial;
    private StandardMaterial3D _hitMaterial;
    private StandardMaterial3D _alertMaterial;
    private StandardMaterial3D _warningMaterial;
    private Label3D _healthLabel;
    private Label3D _attackWarningLabel;
    private MeshInstance3D _attackTelegraph;
    private float _contactDamageTimer;

    public override void _Ready()
    {
        AddToGroup("enemies");
        _spawnPosition = GlobalPosition;
        _health = GetNode<Health>(HealthPath);
        _visual = GetNodeOrNull<MeshInstance3D>(VisualPath);
        _baseMaterial = _visual?.MaterialOverride as StandardMaterial3D;
        _hitMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.02f, 0.02f),
            EmissionEnabled = true,
            Emission = new Color(0.9f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 0.8f
        };
        _alertMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.48f, 0.02f, 0.03f),
            EmissionEnabled = true,
            Emission = new Color(0.7f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 0.55f
        };
        _warningMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.02f, 0.0f, 0.48f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 1.9f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _health.Damaged += OnDamaged;
        _health.HealthChanged += OnHealthChanged;
        _health.Died += OnDied;
        _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        CreateHealthLabel();
        CreateAttackTelegraph();
        PickNewPatrolTarget();
        UpdateHealthLabel(_health.CurrentHealth, _health.MaxHealth);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_health.IsDead || _player == null)
            return;

        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;
        var distance = toPlayer.Length();
        TickContactDamage((float)delta, distance);

        if (_isAttacking)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        if (_knockbackVelocity.LengthSquared() > 0.01f)
        {
            Velocity = _knockbackVelocity;
            _knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, 18.0f * (float)delta);
            MoveAndSlide();
            return;
        }

        if (!_hasDetectedPlayer && distance <= DetectionRange && CanSeePlayer())
            DetectPlayer();

        if (!_hasDetectedPlayer)
        {
            Patrol((float)delta);
            return;
        }

        LookAt(GlobalPosition + toPlayer.Normalized(), Vector3.Up);

        if (distance <= AttackRange)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            BeginAttack();
            return;
        }

        Velocity = toPlayer.Normalized() * MoveSpeed;
        MoveAndSlide();
    }

    private void Patrol(float delta)
    {
        _patrolRetargetTimer -= delta;
        var toTarget = _patrolTarget - GlobalPosition;
        toTarget.Y = 0.0f;

        if (_patrolRetargetTimer <= 0.0f || toTarget.LengthSquared() <= 0.35f)
            PickNewPatrolTarget();

        toTarget = _patrolTarget - GlobalPosition;
        toTarget.Y = 0.0f;

        if (toTarget.LengthSquared() <= 0.001f)
        {
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        var direction = toTarget.Normalized();
        LookAt(GlobalPosition + direction, Vector3.Up);
        Velocity = direction * PatrolSpeed;
        MoveAndSlide();
    }

    private void TickContactDamage(float delta, float distanceToPlayer)
    {
        _contactDamageTimer = Mathf.Max(_contactDamageTimer - delta, 0.0f);

        if (_contactDamageTimer > 0.0f || distanceToPlayer > ContactDamageRange)
            return;

        _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(ContactDamage);
        _contactDamageTimer = ContactDamageCooldown;
    }

    private void PickNewPatrolTarget()
    {
        var angle = GD.Randf() * Mathf.Tau;
        var distance = GD.Randf() * PatrolRadius;
        _patrolTarget = _spawnPosition + new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
        _patrolRetargetTimer = (float)GD.RandRange(PatrolRetargetMinTime, PatrolRetargetMaxTime);
    }

    private void DetectPlayer()
    {
        _hasDetectedPlayer = true;

        if (_visual != null)
            _visual.MaterialOverride = _alertMaterial;

        SpawnAlertMarker();
    }

    private bool CanSeePlayer()
    {
        if (!RequireLineOfSight || _player == null)
            return true;

        var from = GlobalPosition + Vector3.Up * LineOfSightEyeHeight;
        var to = _player.GlobalPosition + Vector3.Up * LineOfSightEyeHeight;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
            return true;

        return result.TryGetValue("collider", out var colliderVariant)
            && colliderVariant.AsGodotObject() == _player;
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.001f)
            return;

        _knockbackVelocity = direction.Normalized() * force * KnockbackResistance;
    }

    private async void BeginAttack()
    {
        if (_isAttacking)
            return;

        _isAttacking = true;
        LockAttackDirection();
        ShowAttackTelegraph();
        await Wait(AttackWindup);
        HideAttackTelegraph();

        if (!_health.IsDead && _player != null && IsPlayerInsideLockedAttack())
        {
            SpawnAttackImpact();
            _player.GetNodeOrNull<Health>("Health")?.ApplyDamage(AttackDamage);
        }

        await Wait(AttackCooldown);
        _isAttacking = false;
    }

    private async void OnDied()
    {
        GetTree().CallGroup("run_manager", "AddBloodCoins", RollBloodCoinsReward());
        GetTree().CallGroup("run_manager", "NotifyEnemyDefeated");
        GetTree().CallGroup("player_skills", "OnEnemyDefeated");
        TryDropHealthPotion();

        var combat = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        combat?.AddFury(FuryOnDeath);

        SetPhysicsProcess(false);
        await Wait(0.25f);
        QueueFree();
    }

    private void TryDropHealthPotion()
    {
        if (GD.Randf() > HealthPotionDropChance)
            return;

        var potion = new HealthPotion
        {
            Name = "HealthPotion",
            Position = GlobalPosition + Vector3.Up * 0.18f
        };
        GetParent()?.AddChild(potion);

        potion.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.24f, Height = 0.42f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.02f, 0.04f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.0f, 0.0f),
                EmissionEnergyMultiplier = 1.6f
            }
        });

        potion.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = 0.85f }
        });
    }

    private int RollBloodCoinsReward()
    {
        var minReward = Mathf.Min(MinBloodCoinsReward, MaxBloodCoinsReward);
        var maxReward = Mathf.Max(MinBloodCoinsReward, MaxBloodCoinsReward);
        return (int)GD.RandRange(minReward, maxReward + 1);
    }

    private async void OnDamaged(float amount)
    {
        SpawnDamageNumber(amount);

        if (_visual != null)
            _visual.MaterialOverride = _hitMaterial;

        await Wait(0.08f);

        if (_visual != null && !_health.IsDead)
        {
            if (_hasDetectedPlayer)
                _visual.MaterialOverride = _alertMaterial;
            else
                _visual.MaterialOverride = _baseMaterial;
        }
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateHealthLabel(currentHealth, maxHealth);
    }

    private void CreateHealthLabel()
    {
        _healthLabel = new Label3D
        {
            Name = "HealthLabel",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 34,
            Modulate = new Color(1.0f, 0.14f, 0.1f),
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.05f
        };
        AddChild(_healthLabel);
    }

    private void CreateAttackTelegraph()
    {
        var attackDepth = AttackRange + AttackDepthBonus;
        _attackTelegraph = new MeshInstance3D
        {
            Name = "AttackTelegraph",
            Mesh = new BoxMesh
            {
                Size = new Vector3(AttackWidth, 0.035f, attackDepth)
            },
            MaterialOverride = _warningMaterial,
            Position = new Vector3(0.0f, 0.035f, -0.35f - attackDepth * 0.5f),
            Visible = false
        };
        AddChild(_attackTelegraph);

        _attackWarningLabel = new Label3D
        {
            Name = "AttackWarningLabel",
            Text = "!",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 78,
            Modulate = new Color(1.0f, 0.02f, 0.0f),
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.75f,
            Visible = false
        };
        AddChild(_attackWarningLabel);
    }

    private void UpdateHealthLabel(float currentHealth, float maxHealth)
    {
        if (_healthLabel == null)
            return;

        _healthLabel.Text = $"HP {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}";
    }

    private void SpawnDamageNumber(float amount)
    {
        var label = new Label3D
        {
            Text = Mathf.RoundToInt(amount).ToString(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 76,
            Modulate = Colors.White,
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 1.8f
        };

        AddChild(label);
        AnimateDamageNumber(label);
    }

    private void SpawnAlertMarker()
    {
        var label = new Label3D
        {
            Text = "!",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 72,
            Modulate = new Color(1.0f, 0.02f, 0.0f),
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.65f
        };
        AddChild(label);
        AnimateAlertMarker(label);
    }

    private void LockAttackDirection()
    {
        if (_player == null)
            return;

        var direction = _player.GlobalPosition - GlobalPosition;
        direction.Y = 0.0f;

        if (direction.LengthSquared() <= 0.001f)
            return;

        _lockedAttackDirection = direction.Normalized();
        LookAt(GlobalPosition + _lockedAttackDirection, Vector3.Up);
    }

    private bool IsPlayerInsideLockedAttack()
    {
        var toPlayer = _player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0.0f;

        var forwardDistance = toPlayer.Dot(_lockedAttackDirection);
        if (forwardDistance < 0.0f || forwardDistance > AttackRange + AttackDepthBonus)
            return false;

        var lateralDirection = new Vector3(-_lockedAttackDirection.Z, 0.0f, _lockedAttackDirection.X);
        var lateralDistance = Mathf.Abs(toPlayer.Dot(lateralDirection));
        return lateralDistance <= AttackWidth * 0.5f;
    }

    private void ShowAttackTelegraph()
    {
        if (_attackTelegraph == null || _attackWarningLabel == null)
            return;

        _attackTelegraph.Visible = true;
        _attackTelegraph.Scale = new Vector3(0.45f, 1.0f, 0.35f);
        _attackWarningLabel.Visible = true;

        if (_visual != null)
            _visual.MaterialOverride = _warningMaterial;

        var tween = CreateTween();
        tween.TweenProperty(_attackTelegraph, "scale", Vector3.One, AttackWindup);
        tween.Parallel().TweenProperty(_attackWarningLabel, "scale", Vector3.One * 1.25f, AttackWindup * 0.5f);
        tween.TweenProperty(_attackWarningLabel, "scale", Vector3.One, AttackWindup * 0.5f);
    }

    private void HideAttackTelegraph()
    {
        if (_attackTelegraph != null)
            _attackTelegraph.Visible = false;

        if (_attackWarningLabel != null)
        {
            _attackWarningLabel.Visible = false;
            _attackWarningLabel.Scale = Vector3.One;
        }

        if (_visual != null && !_health.IsDead)
        {
            if (_hasDetectedPlayer)
                _visual.MaterialOverride = _alertMaterial;
            else
                _visual.MaterialOverride = _baseMaterial;
        }
    }

    private async void SpawnAttackImpact()
    {
        var attackDepth = AttackRange + AttackDepthBonus;
        var impact = new MeshInstance3D
        {
            Name = "AttackImpact",
            Mesh = new BoxMesh
            {
                Size = new Vector3(AttackWidth + 0.2f, 0.06f, attackDepth + 0.1f)
            },
            MaterialOverride = _warningMaterial,
            Position = new Vector3(0.0f, 0.08f, -0.35f - attackDepth * 0.5f)
        };
        AddChild(impact);

        var tween = impact.CreateTween();
        tween.TweenProperty(impact, "scale", new Vector3(1.18f, 1.0f, 1.12f), 0.1f);
        await ToSignal(tween, Tween.SignalName.Finished);
        impact.QueueFree();
    }

    private async void AnimateDamageNumber(Label3D label)
    {
        var start = label.Position;
        var tween = CreateTween();
        tween.TweenProperty(label, "position", start + Vector3.Up * 0.8f, 0.45f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.45f);
        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }

    private async void AnimateAlertMarker(Label3D label)
    {
        var tween = CreateTween();
        tween.TweenProperty(label, "position", label.Position + Vector3.Up * 0.55f, 0.45f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.45f);
        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }

    private async Task Wait(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
