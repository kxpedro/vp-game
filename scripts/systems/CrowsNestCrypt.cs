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
        Vector2I.Right
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
