using Godot;

public partial class SummonedRat : CharacterBody3D
{
    public PlayerController OwnerPlayer { get; set; }
    public float LifetimeSeconds { get; set; } = 8.0f;
    public float MovementSpeed { get; set; } = 7.4f;
    public float AttackInterval { get; set; } = 0.24f;
    public float AttackDamage { get; set; } = 16.0f;

    private const float AttackRange = 0.72f;
    private const float AttackAnimSeconds = 0.28f;
    private const float DeathAnimSeconds = 0.5f;
    private EnemyBasic _target;
    private float _attackTimer;
    private float _lifetimeRemaining;

    public override void _Ready()
    {
        AddToGroup("summoned_rats");
        CollisionLayer = 1u << 4;
        CollisionMask = (1u << 4) | 1u;
        _lifetimeRemaining = Mathf.Max(LifetimeSeconds, 0.1f);
        _attackTimer = 0.0f;
        AddChild(new CollisionShape3D
        {
            Name = "RatCollision",
            Position = Vector3.Up * 0.08f,
            Shape = new CylinderShape3D { Radius = 0.22f, Height = 0.16f }
        });
        CreateVisual();
    }

    public override void _PhysicsProcess(double delta)
    {
        var elapsed = (float)delta;
        _lifetimeRemaining -= elapsed;
        _attackTimer = Mathf.Max(_attackTimer - elapsed, 0.0f);
        if (_dying)
            return;
        if (_lifetimeRemaining <= 0.0f || !IsInstanceValid(OwnerPlayer))
        {
            if (_sprite != null && IsInstanceValid(OwnerPlayer))
                StartDeath();
            else
                QueueFree();
            return;
        }

        if (!IsValidTarget(_target))
            _target = FindNearestTarget();

        if (_target == null)
        {
            FollowOwner();
            return;
        }

        var offset = _target.GlobalPosition - GlobalPosition;
        offset.Y = 0.0f;
        if (offset.Length() > AttackRange)
        {
            MoveRat(offset.Normalized());
            if (offset.LengthSquared() > 0.001f)
                LookAt(GlobalPosition + offset.Normalized(), Vector3.Up);
            return;
        }

        if (_attackTimer > 0.0f)
            return;

        _attackTimer = Mathf.Max(AttackInterval, 0.08f);
        _attackAnim = AttackAnimSeconds;
        FaceTowards(offset);
        RunStats.Attribute("RatSummonOnPestilentKill");
        _target.GetNodeOrNull<Health>("Health")?.ApplyDamage(AttackDamage, false, "Rato invocado");
        SpawnBitePulse();
    }

    private EnemyBasic FindNearestTarget()
    {
        EnemyBasic nearest = null;
        var nearestDistanceSquared = float.PositiveInfinity;
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBasic enemy || !IsValidTarget(enemy)) continue;
            var distanceSquared = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
            if (distanceSquared >= nearestDistanceSquared) continue;
            nearest = enemy;
            nearestDistanceSquared = distanceSquared;
        }
        return nearest;
    }

    private static bool IsValidTarget(EnemyBasic enemy)
    {
        return IsInstanceValid(enemy)
            && enemy.IsInsideTree()
            && enemy.GetNodeOrNull<Health>("Health") is { IsDead: false }
            && !enemy.IsCharmed;
    }

    private void FollowOwner()
    {
        var offset = OwnerPlayer.GlobalPosition - GlobalPosition;
        offset.Y = 0.0f;
        if (offset.Length() <= 1.6f)
        {
            Velocity = Vector3.Zero;
            return;
        }
        MoveRat(offset.Normalized(), 0.75f);
    }

    private void MoveRat(Vector3 desiredDirection, float speedMultiplier = 1.0f)
    {
        var separation = Vector3.Zero;
        foreach (var node in GetTree().GetNodesInGroup("summoned_rats"))
        {
            if (node is not SummonedRat other || other == this) continue;
            var away = GlobalPosition - other.GlobalPosition;
            away.Y = 0.0f;
            var distance = away.Length();
            if (distance <= 0.001f || distance >= 0.65f) continue;
            separation += away.Normalized() * (0.65f - distance) / 0.65f;
        }
        var direction = (desiredDirection + separation * 1.35f).Normalized();
        Velocity = new Vector3(direction.X * MovementSpeed * speedMultiplier, 0.0f, direction.Z * MovementSpeed * speedMultiplier);
        MoveAndSlide();
    }

    private Sprite3D _sprite;
    private float _animTime;
    private bool _facingRight = true;

    // No 2D o rato é um sprite de 4 quadros (tools/pixel_art/summoned_rat) virado para a câmera; no 3D, o disco de antes.
    private void CreateSpriteVisual()
    {
        _sprite = new Sprite3D
        {
            Name = "RatSprite",
            Texture = GD.Load<Texture2D>("res://assets/pixel_art/effects/summoned_rat/summoned_rat_sheet.png"),
            Hframes = 10,
            Vframes = 3,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = Vector3.Up * 0.5f
        };
        AddChild(_sprite);
    }

    private float _attackAnim;
    private bool _dying;
    private float _deathTime;

    private void FaceTowards(Vector3 direction)
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera != null && direction.LengthSquared() > 0.0001f)
            _facingRight = direction.Dot(camera.GlobalTransform.Basis.X) >= 0.0f;
    }

    // Fim do tempo de vida: o rato tomba (5 quadros) e some, em vez de sumir de uma vez.
    private void StartDeath()
    {
        _dying = true;
        _deathTime = 0.0f;
        Velocity = Vector3.Zero;
        RemoveFromGroup("summoned_rats");
        CollisionLayer = 0;
        CollisionMask = 0;
    }

    // Folha 10x3 de 30x30 (tools/pixel_art/summoned_rat): linha 0 corrida (0-2 esquerda, 3-5 direita), linha 1 ataque
    // (0-3 esquerda, 4-7 direita), linha 2 morte (0-4 esquerda, 5-9 direita).
    public override void _Process(double delta)
    {
        if (_sprite == null) return;
        var dt = (float)delta;
        if (_dying)
        {
            _deathTime += dt;
            var frame = Mathf.Min((int)(_deathTime / DeathAnimSeconds * 5.0f), 4);
            _sprite.Frame = 20 + (_facingRight ? 5 : 0) + frame;
            if (_deathTime >= DeathAnimSeconds)
                QueueFree();
            return;
        }
        if (_attackAnim > 0.0f)
        {
            _attackAnim -= dt;
            var frame = Mathf.Clamp((int)((1.0f - _attackAnim / AttackAnimSeconds) * 4.0f), 0, 3);
            _sprite.Frame = 10 + (_facingRight ? 4 : 0) + frame;
            return;
        }
        var moving = Velocity.LengthSquared() > 0.1f;
        if (moving) _animTime += dt * 10.0f;
        if (moving) FaceTowards(Velocity);
        _sprite.Frame = (_facingRight ? 3 : 0) + (int)_animTime % 3;
    }

    private void CreateVisual()
    {
        if (GameMode.IsPixelArt)
        {
            CreateSpriteVisual();
            return;
        }
        var lime = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.52f, 1.0f, 0.02f),
            EmissionEnabled = true,
            Emission = new Color(0.38f, 1.0f, 0.0f),
            EmissionEnergyMultiplier = 2.8f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        AddChild(new MeshInstance3D
        {
            Name = "LimeRatBody",
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.10f, RadialSegments = 24 },
            MaterialOverride = lime,
            Position = Vector3.Up * 0.08f
        });
        AddChild(new MeshInstance3D
        {
            Name = "LimeRatGlow",
            Mesh = new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.31f, Rings = 24, RingSegments = 8 },
            MaterialOverride = lime,
            Position = Vector3.Up * 0.035f
        });
    }

    private async void SpawnBitePulse()
    {
        if (GameMode.IsPixelArt) return;
        var pulse = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.62f, 1.0f, 0.02f, 0.78f),
                EmissionEnabled = true,
                Emission = new Color(0.4f, 1.0f, 0.0f),
                EmissionEnergyMultiplier = 3.4f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            Position = Vector3.Up * 0.2f
        };
        AddChild(pulse);
        var tween = pulse.CreateTween();
        tween.TweenProperty(pulse, "scale", Vector3.One * 1.8f, 0.12f);
        tween.Parallel().TweenProperty(pulse, "transparency", 1.0f, 0.14f);
        await ToSignal(tween, Tween.SignalName.Finished);
        if (IsInstanceValid(pulse)) pulse.QueueFree();
    }
}
