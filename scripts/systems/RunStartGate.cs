using Godot;

// Portão do início da run, no hub (no lugar do antigo disco vermelho com "interagir"). Uma grade fecha a passagem;
// quando o jogador chega perto ela sobe, e atravessar o vão começa a run (RunState.StartRun) com a tela de
// carregamento (RunLoadingScreen). Frente local = +Z (lado do hub); a passagem segue para -Z.
// No 3D a grade são barras simples sob o arco Entrance.fbx (CrowsNestCrypt) e recolhem para o alto do vão. No 2D o
// PixelSceneSetup chama ConfigurePixel antes do _Ready: o portão vai para o arco do kit e a grade vira a peça
// props/portcullis_<lado>_<quadro> (tools/pixel_art/crypt_kit/export_kit.py), desenhada por cima da sala.
public partial class RunStartGate : Node3D
{
    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath { get; set; } = "res://scenes/game/RunStartBlessingRoom.tscn";
    // Distância (do centro do portão para o lado do hub) em que a grade começa a subir.
    [Export] public float OpenRadius { get; set; } = 3.4f;
    [Export] public float OpenSeconds { get; set; } = 0.8f;
    // Vão das barras (3D) e bloqueio do corpo enquanto a grade está fechada.
    [Export] public float OpeningWidth { get; set; } = 1.9f;
    [Export] public float OpeningHeight { get; set; } = 2.5f;
    [Export] public float BlockWidth { get; set; } = 2.6f;
    [Export] public float BlockDepth { get; set; } = 0.5f;
    // Centro da área (atrás da grade) que começa a run ao ser atravessada.
    [Export] public float PassDistance { get; set; } = 1.3f;
    [Export] public float TitleHeight { get; set; } = 3.3f;

    private const int PixelFrames = 8;
    private const float BarThickness = 0.07f;
    private const float BarPitch = 0.3f;
    private static readonly StringName PlayerGroup = "player";

    private PixelRoom _pixelRoom;
    private Vector2I _pixelTile;
    private string _pixelSide = "ne";
    private MeshInstance3D _pixelGrate;
    private int _pixelFrame;

    private Node3D _bars;
    private CollisionShape3D _blockShape;
    private bool _playerNear;
    private bool _playerPassing;
    private bool _opening;
    private bool _starting;
    private float _open;

    /// <summary>
    /// Modo 2D: põe o portão no arco do kit na borda <paramref name="side"/> ("ne" ou "nw") do tile (gx, gy), com a
    /// grade do kit no vão. Medidas do arco em walls.py (wall_arch: vão de 40 unidades, parede de 22).
    /// Chamar antes do _Ready do portão.
    /// </summary>
    public void ConfigurePixel(PixelRoom room, int gx, int gy, string side)
    {
        const float wallThickness = 22.0f;
        _pixelRoom = room;
        _pixelTile = new Vector2I(gx, gy);
        _pixelSide = side == "nw" ? "nw" : "ne";
        var unit = PixelRoom.UnitMeters;
        if (_pixelSide == "ne")
        {
            // Parede no plano y = gy * 64, hub do lado +y (= +Z no mundo): frente local já é +Z.
            Position = room.KitToWorld(gx * 64.0f + 32.0f, gy * 64.0f - wallThickness * 0.5f);
            Rotation = Vector3.Zero;
        }
        else
        {
            // Parede no plano x = gx * 64, hub do lado +x: gira +Z para +X.
            Position = room.KitToWorld(gx * 64.0f - wallThickness * 0.5f, gy * 64.0f + 32.0f);
            Rotation = new Vector3(0.0f, Mathf.Pi * 0.5f, 0.0f);
        }

        OpeningWidth = 40.0f * unit;
        BlockWidth = 64.0f * unit;
        BlockDepth = wallThickness * unit;
        PassDistance = (wallThickness * 0.5f + 26.0f) * unit;
        TitleHeight = 2.9f;
    }

    public override void _Ready()
    {
        CreateBlocker();
        var open = CreateArea("OpenArea", new CylinderShape3D { Radius = OpenRadius, Height = 3.0f }, new Vector3(0.0f, 1.5f, 0.6f));
        open.BodyEntered += body => { if (body.IsInGroup(PlayerGroup)) _playerNear = true; };
        open.BodyExited += body => { if (body.IsInGroup(PlayerGroup)) _playerNear = false; };
        var pass = CreateArea("PassArea", new BoxShape3D { Size = new Vector3(BlockWidth, 2.5f, 1.0f) }, new Vector3(0.0f, 1.25f, -PassDistance));
        pass.BodyEntered += body => { if (body.IsInGroup(PlayerGroup)) _playerPassing = true; };
        pass.BodyExited += body => { if (body.IsInGroup(PlayerGroup)) _playerPassing = false; };

        if (_pixelRoom != null)
            _pixelGrate = _pixelRoom.AddOverlay("RunGateGrate", PixelPiece(0), _pixelTile.X, _pixelTile.Y);
        else
            CreateBars();

        // Título do portão (PortalLabel, texto localizado pelo HUD): mesmo estilo dos títulos dos interativos, acima do arco.
        if (GetNodeOrNull<Label3D>("PortalLabel") is { } title)
        {
            title.Position = Vector3.Up * TitleHeight;
            InteractPrompt.StyleTitlesAndAnchor(this, null);
        }
    }

    public override void _Process(double delta)
    {
        if (!_opening && _playerNear)
            Open();

        if (_starting || !_playerPassing || _open < 0.95f || string.IsNullOrWhiteSpace(TargetScenePath))
            return;

        _starting = true;
        RunState.StartRun();
        RunLoadingScreen.Start(GetTree(), GameMode.Scene(TargetScenePath));
    }

    private void Open()
    {
        _opening = true;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(SetOpenAmount), 0.0f, 1.0f, OpenSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private void SetOpenAmount(float amount)
    {
        _open = amount;
        // O corpo passa quando a grade já subiu acima da cabeça.
        if (_blockShape != null && amount >= 0.7f && !_blockShape.Disabled)
            _blockShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        if (_pixelGrate != null)
        {
            var frame = Mathf.Clamp(Mathf.RoundToInt(amount * (PixelFrames - 1)), 0, PixelFrames - 1);
            if (frame != _pixelFrame)
            {
                _pixelFrame = frame;
                PixelRoom.SetOverlayPiece(_pixelGrate, PixelPiece(frame));
            }
        }

        if (_bars != null)
        {
            // As barras pendem do alto do vão e recolhem para cima.
            _bars.Scale = new Vector3(1.0f, Mathf.Max(1.0f - amount, 0.001f), 1.0f);
            _bars.Visible = amount < 0.99f;
        }
    }

    private string PixelPiece(int frame) => $"props/portcullis_{_pixelSide}_{frame}";

    private void CreateBlocker()
    {
        var body = new StaticBody3D { Name = "GateBlocker" };
        _blockShape = new CollisionShape3D
        {
            Name = "Collision",
            Position = new Vector3(0.0f, 1.5f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(BlockWidth, 3.0f, BlockDepth) }
        };
        body.AddChild(_blockShape);
        AddChild(body);
    }

    private Area3D CreateArea(string name, Shape3D shape, Vector3 position)
    {
        var area = new Area3D { Name = name, Monitorable = false };
        area.AddChild(new CollisionShape3D { Name = "Collision", Position = position, Shape = shape });
        AddChild(area);
        return area;
    }

    // Grade 3D: barras verticais e duas travessas, penduradas no alto do vão (a origem de Bars fica no topo).
    private void CreateBars()
    {
        _bars = new Node3D { Name = "Bars", Position = Vector3.Up * OpeningHeight };
        AddChild(_bars);
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.09f, 0.085f, 0.1f), Metallic = 0.6f, Roughness = 0.55f };
        var count = Mathf.Max(Mathf.FloorToInt(OpeningWidth / BarPitch), 2);
        var first = -(count - 1) * BarPitch * 0.5f;
        for (var i = 0; i < count; i++)
        {
            _bars.AddChild(new MeshInstance3D
            {
                Name = $"Bar{i}",
                Position = new Vector3(first + i * BarPitch, -OpeningHeight * 0.5f, 0.0f),
                Mesh = new BoxMesh { Size = new Vector3(BarThickness, OpeningHeight, BarThickness) },
                MaterialOverride = material
            });
        }
        foreach (var height in new[] { 0.35f, 0.7f })
        {
            _bars.AddChild(new MeshInstance3D
            {
                Name = $"Rail{height}",
                Position = new Vector3(0.0f, -OpeningHeight * height, 0.0f),
                Mesh = new BoxMesh { Size = new Vector3(OpeningWidth, BarThickness, BarThickness * 1.4f) },
                MaterialOverride = material
            });
        }
    }
}
