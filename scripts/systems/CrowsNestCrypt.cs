using Godot;
using System.Collections.Generic;

public partial class CrowsNestCrypt : Node3D
{
    [Export] public float RoomSize { get; set; } = 10.0f;
    [Export] public float FloorThickness { get; set; } = 0.3f;
    [Export] public float WallHeight { get; set; } = 3.2f;
    [Export] public float WallThickness { get; set; } = 0.5f;

    private readonly Vector2I[] _crossRooms =
    {
        Vector2I.Zero,
        Vector2I.Up,
        Vector2I.Down,
        Vector2I.Left,
        Vector2I.Right,
        new Vector2I(0, 2)
    };

    public override void _Ready()
    {
        var floorMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.075f, 0.07f, 0.082f),
            Roughness = 0.96f
        };
        var wallMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.13f, 0.115f, 0.135f),
            Roughness = 0.93f
        };
        var roomSet = new HashSet<Vector2I>(_crossRooms);

        foreach (var room in _crossRooms)
        {
            CreateFloor(room, floorMaterial);
            CreateBoundaryWalls(room, roomSet, wallMaterial);
        }

        CreateTrainingHall();
    }

    private void CreateTrainingHall()
    {
        var center = new Vector3(0.0f, 0.0f, RoomSize * 2.0f);
        var dummy = new TrainingDummy { Position = center };
        dummy.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Position = new Vector3(0.0f, 0.8f, 0.0f),
            Shape = new CapsuleShape3D { Radius = 0.52f, Height = 1.65f }
        });
        dummy.AddChild(new MeshInstance3D
        {
            Name = "Visual",
            Position = new Vector3(0.0f, 0.82f, 0.0f),
            Mesh = new CylinderMesh { TopRadius = 0.48f, BottomRadius = 0.58f, Height = 1.65f, RadialSegments = 20 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.48f, 0.32f, 0.16f),
                Roughness = 0.96f
            }
        });
        dummy.AddChild(new Health { Name = "Health", MaxHealth = 250000.0f });
        AddChild(dummy);

        var title = new Label3D
        {
            Name = "TrainingHallLabel",
            Text = "SALA DE TREINAMENTO\nBoneco regenerativo",
            Position = center + new Vector3(0.0f, 2.7f, 0.0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30,
            OutlineSize = 8,
            Modulate = new Color(0.86f, 0.68f, 0.34f)
        };
        AddChild(title);

        var trainingMark = new MeshInstance3D
        {
            Name = "TrainingCircle",
            Position = center + Vector3.Up * 0.025f,
            Mesh = new TorusMesh { InnerRadius = 2.15f, OuterRadius = 2.28f, Rings = 48, RingSegments = 12 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.32f, 0.22f, 0.1f),
                EmissionEnabled = true,
                Emission = new Color(0.22f, 0.12f, 0.025f),
                EmissionEnergyMultiplier = 0.45f
            }
        };
        AddChild(trainingMark);
    }

    private void CreateFloor(Vector2I room, Material material)
    {
        var position = new Vector3(room.X * RoomSize, -FloorThickness * 0.5f, room.Y * RoomSize);
        var body = new StaticBody3D { Name = $"Room_{room.X}_{room.Y}_Floor", Position = position };
        AddChild(body);
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(RoomSize, FloorThickness, RoomSize) }
        });
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(RoomSize, FloorThickness, RoomSize) },
            MaterialOverride = material
        });
    }

    private void CreateBoundaryWalls(Vector2I room, HashSet<Vector2I> roomSet, Material material)
    {
        CreateWallWhenExposed(room, Vector2I.Up, true, roomSet, material);
        CreateWallWhenExposed(room, Vector2I.Down, true, roomSet, material);
        CreateWallWhenExposed(room, Vector2I.Left, false, roomSet, material);
        CreateWallWhenExposed(room, Vector2I.Right, false, roomSet, material);
    }

    private void CreateWallWhenExposed(Vector2I room, Vector2I side, bool horizontal, HashSet<Vector2I> roomSet, Material material)
    {
        if (roomSet.Contains(room + side))
            return;

        var center = new Vector3(room.X * RoomSize, WallHeight * 0.5f, room.Y * RoomSize);
        center.X += side.X * RoomSize * 0.5f;
        center.Z += side.Y * RoomSize * 0.5f;
        var size = horizontal
            ? new Vector3(RoomSize + WallThickness, WallHeight, WallThickness)
            : new Vector3(WallThickness, WallHeight, RoomSize + WallThickness);
        var body = new StaticBody3D { Name = $"CryptWall_{room.X}_{room.Y}_{side.X}_{side.Y}", Position = center };
        AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = material });
    }
}
