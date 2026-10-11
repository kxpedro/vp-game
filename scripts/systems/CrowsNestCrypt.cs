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

    private Label3D _trainingHallLabel;

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string language)
    {
        if (IsInstanceValid(_trainingHallLabel))
            _trainingHallLabel.Text = Localization.Text("scene.crows_nest.training_hall");
    }

    public override void _Ready()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
        // Mesma pedra das salas fixas; as paredes recebem um tom um pouco mais claro para separar do piso.
        var floorMaterial = ResourceLoader.Load<StandardMaterial3D>(StoneMaterialPath);
        var wallMaterial = (StandardMaterial3D)floorMaterial.Duplicate();
        wallMaterial.AlbedoColor = new Color(1.45f, 1.35f, 1.45f);
        var roomSet = new HashSet<Vector2I>(_crossRooms);

        foreach (var room in _crossRooms)
        {
            CreateFloor(room, floorMaterial);
            CreateBoundaryWalls(room, roomSet, wallMaterial);
        }

        CreateTrainingHall();
        CreateImportedDecorations();

        // Rótulo do caixão no padrão dos títulos 3D (§8.8): acima da cabeça do jogador no spawn.
        var coffinLabel = GetNodeOrNull<Label3D>("../VirgilCoffin/CoffinLabel");
        if (coffinLabel != null)
        {
            coffinLabel.Position = new Vector3(coffinLabel.Position.X, Mathf.Max(coffinLabel.Position.Y, 2.2f), coffinLabel.Position.Z);
            InteractPrompt.StyleTitle(coffinLabel);
        }
    }

    private const string StoneMaterialPath = "res://assets/materials/dungeon_stone.tres";

    private void CreateImportedDecorations()
    {
        const string root = "res://assets/environment/quaternius_medieval_dungeon/";

        PlaceDecoration(root + "Carpet.fbx", "CentralCarpet", new Vector3(0.0f, 0.02f, 0.0f), 6.4f);
        PlaceDecoration(root + "Entrance.fbx", "RunPortalArch", new Vector3(0.0f, 0.0f, -12.3f), 4.8f);
        PlaceDecoration(root + "Chest.fbx", "ArtifactChestVisual", new Vector3(10.0f, 0.68f, 0.0f), 1.7f, -90.0f);
        PlaceDecoration(root + "Book_Open.fbx", "WeaponLoreBook", new Vector3(-10.0f, 0.69f, 0.0f), 0.62f, 90.0f);
        PlaceDecoration(root + "Bones.fbx", "CryptBonesWest", new Vector3(-3.15f, 0.02f, 10.15f), 1.7f, 18.0f);
        PlaceDecoration(root + "Bones.fbx", "CryptBonesEast", new Vector3(3.2f, 0.02f, 10.1f), 1.45f, -24.0f);
        PlaceDecoration(root + "Barrel.fbx", "SupplyBarrelA", new Vector3(-7.7f, 0.0f, -2.8f), 1.05f, 8.0f);
        PlaceDecoration(root + "Barrel.fbx", "SupplyBarrelB", new Vector3(-8.65f, 0.0f, -2.45f), 0.92f, -11.0f);

        PlaceDecoration(root + "Candelabrum_Tall.fbx", "CandelabrumNW", new Vector3(-3.3f, 0.0f, -3.25f), 2.25f);
        PlaceDecoration(root + "Candelabrum_Tall.fbx", "CandelabrumNE", new Vector3(3.3f, 0.0f, -3.25f), 2.25f);
        PlaceDecoration(root + "Candelabrum_Tall.fbx", "CandelabrumSW", new Vector3(-3.3f, 0.0f, 3.25f), 2.25f);
        PlaceDecoration(root + "Candelabrum_Tall.fbx", "CandelabrumSE", new Vector3(3.3f, 0.0f, 3.25f), 2.25f);

        PlaceDecoration(root + "Torch_Wall.fbx", "WestWallTorch", new Vector3(-4.72f, 1.15f, 0.0f), 1.3f, 90.0f);
        PlaceDecoration(root + "Torch_Wall.fbx", "EastWallTorch", new Vector3(4.72f, 1.15f, 0.0f), 1.3f, -90.0f);

        CreateCandleLight("CandleLightNW", new Vector3(-3.3f, 2.0f, -3.25f));
        CreateCandleLight("CandleLightNE", new Vector3(3.3f, 2.0f, -3.25f));
        CreateCandleLight("CandleLightSW", new Vector3(-3.3f, 2.0f, 3.25f));
        CreateCandleLight("CandleLightSE", new Vector3(3.3f, 2.0f, 3.25f));
    }

    private void PlaceDecoration(
        string scenePath,
        string nodeName,
        Vector3 position,
        float targetLargestDimension,
        float yawDegrees = 0.0f)
    {
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PushWarning($"Decoracao 3D nao importada: {scenePath}");
            return;
        }

        var instance = scene.Instantiate<Node3D>();
        instance.Name = nodeName;
        instance.Position = position;
        instance.RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f);
        AddChild(instance);

        if (!TryGetVisualBounds(instance, out var minimum, out var maximum))
            return;

        var size = maximum - minimum;
        var largestDimension = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        if (largestDimension > 0.001f)
            instance.Scale *= targetLargestDimension / largestDimension;

        if (TryGetVisualBounds(instance, out var normalizedMinimum, out _))
            instance.GlobalPosition += Vector3.Up * (position.Y - normalizedMinimum.Y);
    }

    private void CreateCandleLight(string nodeName, Vector3 position)
    {
        AddChild(new OmniLight3D
        {
            Name = nodeName,
            Position = position,
            LightColor = new Color(1.0f, 0.32f, 0.08f),
            LightEnergy = 1.2f,
            OmniRange = 4.8f,
            ShadowEnabled = false
        });
    }

    internal static bool TryGetVisualBounds(Node root, out Vector3 minimum, out Vector3 maximum)
    {
        minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var foundMesh = false;

        foreach (var candidate in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (candidate is not MeshInstance3D mesh || mesh.Mesh == null)
                continue;

            var bounds = mesh.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var localPoint = bounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0.0f : bounds.Size.X,
                    (corner & 2) == 0 ? 0.0f : bounds.Size.Y,
                    (corner & 4) == 0 ? 0.0f : bounds.Size.Z);
                var worldPoint = mesh.GlobalTransform * localPoint;
                minimum = new Vector3(
                    Mathf.Min(minimum.X, worldPoint.X),
                    Mathf.Min(minimum.Y, worldPoint.Y),
                    Mathf.Min(minimum.Z, worldPoint.Z));
                maximum = new Vector3(
                    Mathf.Max(maximum.X, worldPoint.X),
                    Mathf.Max(maximum.Y, worldPoint.Y),
                    Mathf.Max(maximum.Z, worldPoint.Z));
                foundMesh = true;
            }
        }

        return foundMesh;
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
            PixelSize = 0.0085f,
            Name = "TrainingHallLabel",
            Text = Localization.Text("scene.crows_nest.training_hall"),
            Position = center + new Vector3(-1.8f, 2.9f, -1.8f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30,
            OutlineSize = 8,
            Modulate = new Color(0.86f, 0.68f, 0.34f)
        };
        AddChild(title);
        _trainingHallLabel = title;

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
