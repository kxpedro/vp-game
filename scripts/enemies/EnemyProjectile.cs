using Godot;

public partial class EnemyProjectile : Node3D
{
    public Vector3 Direction { get; set; } = Vector3.Forward;
    public Vector3 SpawnPosition { get; set; }
    public float Speed { get; set; } = 5.2f;
    public float Damage { get; set; } = 10.0f;
    // Nome da fonte de dano (causa da morte). Vazio = texto generico localizado no momento do impacto.
    public string SourceName { get; set; } = "";
    public Rid OwnerRid { get; set; }
    // Alcance maximo; o projetil some depois disso (0 = sem limite).
    public float MaxDistance { get; set; }
    private float _traveled;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        AddToGroup("enemy_projectiles");
        GlobalPosition = SpawnPosition;
        if (Direction.LengthSquared() > 0.001f)
            LookAt(GlobalPosition + Direction.Normalized(), Vector3.Up);

        var shaftMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.24f, 0.02f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.08f, 0.015f),
            EmissionEnergyMultiplier = 3.6f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var tipMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.92f, 0.28f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.52f, 0.04f),
            EmissionEnergyMultiplier = 5.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        AddChild(new MeshInstance3D
        {
            Name = "ArrowShaft",
            Mesh = new CylinderMesh { TopRadius = 0.065f, BottomRadius = 0.065f, Height = 0.92f, RadialSegments = 12 },
            MaterialOverride = shaftMaterial,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f)
        });
        AddChild(new MeshInstance3D
        {
            Name = "ArrowHead",
            Mesh = new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.19f, Height = 0.34f, RadialSegments = 6 },
            MaterialOverride = tipMaterial,
            Position = new Vector3(0.0f, 0.0f, -0.62f),
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f)
        });
        AddChild(new MeshInstance3D
        {
            Name = "ArrowFletchingHorizontal",
            Mesh = new BoxMesh { Size = new Vector3(0.32f, 0.035f, 0.24f) },
            MaterialOverride = shaftMaterial,
            Position = new Vector3(0.0f, 0.0f, 0.48f)
        });
        AddChild(new MeshInstance3D
        {
            Name = "ArrowFletchingVertical",
            Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.32f, 0.24f) },
            MaterialOverride = shaftMaterial,
            Position = new Vector3(0.0f, 0.0f, 0.48f)
        });
        AddChild(new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.13f, 0.02f),
            LightEnergy = 1.7f,
            OmniRange = 2.2f,
            ShadowEnabled = false
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = Direction.Normalized() * Speed * (float)delta;
        var from = GlobalPosition;
        var to = from + step;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        if (OwnerRid.IsValid)
            query.Exclude = new Godot.Collections.Array<Rid> { OwnerRid };
        var collision = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (collision.Count > 0)
        {
            var collider = collision["collider"].AsGodotObject() as Node;
            if (collider?.IsInGroup("player") == true)
                collider.GetNodeOrNull<Health>("Health")?.ApplyDamage(Damage, false, ResolveSourceName());
            QueueFree();
            return;
        }

        GlobalPosition = to;
        _traveled += step.Length();
        if (MaxDistance > 0.0f && _traveled >= MaxDistance)
        {
            QueueFree();
            return;
        }
        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        if (player != null && SegmentDistanceToPlayer(from, to, player.GlobalPosition) <= 0.82f)
        {
            player.GetNodeOrNull<Health>("Health")?.ApplyDamage(Damage, false, ResolveSourceName());
            QueueFree();
            return;
        }
    }

    private static float SegmentDistanceToPlayer(Vector3 from, Vector3 to, Vector3 playerPosition)
    {
        from.Y = 0.0f;
        to.Y = 0.0f;
        playerPosition.Y = 0.0f;
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        var progress = lengthSquared <= 0.0001f
            ? 0.0f
            : Mathf.Clamp((playerPosition - from).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        return playerPosition.DistanceTo(from + segment * progress);
    }

    private string ResolveSourceName() => string.IsNullOrWhiteSpace(SourceName) ? Localization.Text("enemy.damage.projectile") : SourceName;
}
