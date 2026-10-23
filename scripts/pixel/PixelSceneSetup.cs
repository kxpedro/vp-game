using Godot;

// Modo 2D: primeiro nó das cenas de scenes/pixel. Liga o modo 2D (vale também ao abrir a cena direto no editor),
// troca o ambiente para cores exatas e, nas salas fixas, monta a sala do kit e põe os interativos no lugar.
// As salas da run (TestArena) são montadas pelo RoomGenerator; aqui só o ambiente.
public partial class PixelSceneSetup : Node3D
{
    public enum RoomKind
    {
        RunRoom,
        Hub,
        BlessingRoom,
        Intermission
    }

    private const string RoomsRoot = "res://assets/pixel_art/crypt/rooms/";

    [Export] public RoomKind Kind { get; set; } = RoomKind.RunRoom;

    private PixelRoom _room;

    public override void _EnterTree()
    {
        GameMode.Current = GameMode.Kind.PixelArt;
    }

    public override void _Ready()
    {
        var scene = GetParent();
        PixelRoom.ConfigureEnvironment(scene.GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment);
        if (Kind == RoomKind.RunRoom)
            return;

        var file = Kind switch
        {
            RoomKind.Hub => "hub_completo.txt",
            RoomKind.BlessingRoom => "sala_bencao.txt",
            _ => "camara_intervalo.txt"
        };
        var layout = PixelRoomComposer.LoadLayout(RoomsRoot + file);
        _room = new PixelRoom { Name = "PixelRoom" };
        AddChild(_room);
        // O centro de cada sala fixa é a origem da cena (onde fica a fonte no hub e na sala de bênção).
        if (layout == null || !_room.Build(layout, Kind == RoomKind.Hub ? (Vector2?)new Vector2(992.0f, 992.0f) : null))
        {
            GD.PushError($"PixelSceneSetup: sala 2D nao montada ({Kind}).");
            return;
        }

        // A IsoCamera vem depois deste nó na cena: a configuração entra antes do _Ready dela.
        if (scene.GetNodeOrNull<IsoCamera>("IsoCamera") is { } camera)
            _room.ConfigureCamera(camera);

        if (Kind == RoomKind.Hub)
            PlaceHub(scene);
        else
            AlignShellWalls(scene);
    }

    // Salas fixas (bênção e câmara): as paredes 3D da cena somem e vão para a borda da sala do kit, porque as portas
    // de recompensa se posicionam pela NorthWall. A colisão fica com o PixelRoom.
    private void AlignShellWalls(Node scene)
    {
        var bounds = _room.Picture.TileBounds;
        var north = _room.KitToWorld(bounds.GetCenter().X, bounds.Position.Y).Z;
        var south = _room.KitToWorld(bounds.GetCenter().X, bounds.End.Y).Z;
        var west = _room.KitToWorld(bounds.Position.X, bounds.GetCenter().Y).X;
        var east = _room.KitToWorld(bounds.End.X, bounds.GetCenter().Y).X;
        foreach (var (name, axis, edge, outward) in new[] { ("NorthWall", 2, north, -1.0f), ("SouthWall", 2, south, 1.0f), ("WestWall", 0, west, -1.0f), ("EastWall", 0, east, 1.0f) })
        {
            if (scene.GetNodeOrNull<Node3D>(name) is not { } wall)
                continue;
            var collision = wall.GetNodeOrNull<CollisionShape3D>("Collision") ?? wall.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            var thickness = (collision?.Shape as BoxShape3D)?.Size[axis] ?? 0.45f;
            var position = wall.Position;
            position[axis] = edge + outward * thickness * 0.5f;
            wall.Position = position;
            if (collision != null)
                collision.Disabled = true;
            HideVisual(wall);
        }

        if (scene.GetNodeOrNull<Node3D>("Floor") is { } floor)
        {
            if (floor.GetNodeOrNull<CollisionShape3D>("Collision") is { } c1) c1.Disabled = true;
            if (floor.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is { } c2) c2.Disabled = true;
            HideVisual(floor);
        }
    }

    private static void HideVisual(Node node)
    {
        if (node.GetNodeOrNull<Node3D>("Visual") is { } visual)
            visual.Visible = false;
    }

    // Hub do kit (layouts/hub_completo.txt, 31x31 tiles): fonte no centro (origem), portal na sala de cima, bancada e
    // Odalric na da esquerda, personagens futuros na da direita, boneco no salão central, caixão do Virgil embaixo.
    // Posições em unidades do kit (64 = 1 tile).
    private void PlaceHub(Node scene)
    {
        if (scene.GetNodeOrNull<Node3D>("Player") is { } player)
            player.Position = _room.KitToWorld(992.0f, 1610.0f, 0.2f);
        if (scene.GetNodeOrNull<Node3D>("WeaponBench") is { } bench)
            bench.Position = _room.KitToWorld(330.0f, 900.0f, 0.45f);
        if (scene.GetNodeOrNull<Node3D>("StartRunPortal") is { } portal)
            portal.Position = _room.KitToWorld(992.0f, 320.0f, 0.12f);

        var spots = new[] { _room.KitToWorld(1700.0f, 860.0f), _room.KitToWorld(1700.0f, 992.0f), _room.KitToWorld(1700.0f, 1124.0f) };
        for (var i = 0; i < spots.Length; i++)
            AddChild(new CharacterPlaceholder { Name = $"CharacterPlaceholder{i + 1}", Position = spots[i] });
        AddChild(new DialogueNpc { Name = "NpcOdalric", SpeakerId = "odalric", Position = _room.KitToWorld(330.0f, 1160.0f), RotationDegrees = new Vector3(0.0f, -60.0f, 0.0f) });
        CreateTrainingDummy(_room.KitToWorld(780.0f, 1200.0f));
    }

    // Mesmo boneco de treino do hub 3D (CrowsNestCrypt.CreateTrainingHall).
    private void CreateTrainingDummy(Vector3 center)
    {
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
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.48f, 0.32f, 0.16f), Roughness = 0.96f }
        });
        dummy.AddChild(new Health { Name = "Health", MaxHealth = 250000.0f });
        AddChild(dummy);
    }
}
