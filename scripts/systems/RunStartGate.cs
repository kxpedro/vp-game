using Godot;

// Portão do início da run, no hub (no lugar do antigo disco vermelho com "interagir"). Uma grade fecha a passagem;
// quando o jogador chega perto ela sobe, e atravessar o vão começa a run (RunState.StartRun) com a tela de
// carregamento (RunLoadingScreen). Frente local = +Z (lado do hub); a passagem segue para -Z.
// A grade é um PortcullisGate (o mesmo das portas das salas): no 3D, barras sob o arco Entrance.fbx (CrowsNestCrypt);
// no 2D o PixelSceneSetup chama ConfigurePixel antes do _Ready e a grade vira a peça do kit no arco da sala.
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

    private static readonly StringName PlayerGroup = "player";

    private PixelRoom _pixelRoom;
    private Vector2I _pixelTile;
    private string _pixelSide = "ne";

    private PortcullisGate _grate;
    private CollisionShape3D _blockShape;
    private bool _playerNear;
    private bool _playerPassing;
    private bool _opening;
    private bool _starting;

    /// <summary>
    /// Modo 2D: põe o portão no arco do kit na borda <paramref name="side"/> ("ne" ou "nw") do tile (gx, gy), com a
    /// grade do kit no vão. Medidas do arco em walls.py (wall_arch: vão de 40 unidades, parede de 22).
    /// Chamar antes do _Ready do portão.
    /// </summary>
    public void ConfigurePixel(PixelRoom room, int gx, int gy, string side)
    {
        const float wallThickness = PortcullisGate.PixelWallThickness;
        _pixelRoom = room;
        _pixelTile = new Vector2I(gx, gy);
        _pixelSide = side == "nw" ? "nw" : "ne";
        var unit = PixelRoom.UnitMeters;
        // Origem no meio da espessura da parede, frente local (+Z) para o hub.
        Transform = PortcullisGate.PixelWallTransform(room, gx, gy, _pixelSide, wallThickness * 0.5f);

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

        _grate = new PortcullisGate { Name = "Grate", OpeningWidth = OpeningWidth, OpeningHeight = OpeningHeight };
        if (_pixelRoom != null)
            _grate.ConfigurePixel(_pixelRoom, _pixelTile.X, _pixelTile.Y, _pixelSide);
        AddChild(_grate);

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

        if (_starting || !_playerPassing || _grate.OpenAmount < 0.95f || string.IsNullOrWhiteSpace(TargetScenePath))
            return;

        _starting = true;
        RunState.StartRun();
        RunLoadingScreen.Start(GetTree(), GameMode.Scene(TargetScenePath));
    }

    private void Open()
    {
        _opening = true;
        _grate.MoveTo(1.0f, OpenSeconds);
    }

    public override void _PhysicsProcess(double delta)
    {
        // O corpo passa quando a grade já subiu acima da cabeça.
        if (_blockShape != null && _grate != null && _grate.OpenAmount >= 0.7f && !_blockShape.Disabled)
            _blockShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
    }

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
}
