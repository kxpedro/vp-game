using Godot;

// Grade de ferro de um portão (a do portão do hub, RunStartGate), que sobe para abrir. Só o visual: quem decide quando
// abre é o dono (RunStartGate no hub, ScenePortal nas portas das salas). A origem é o centro da base do vão, com a
// frente local (+Z) para o lado de onde o jogador chega.
// No 3D são barras simples penduradas no alto do vão, que recolhem para cima. No 2D (ConfigurePixel antes do _Ready) a
// grade é a peça props/portcullis_<lado>_<quadro> do kit (tools/pixel_art/crypt_kit/export_kit.py), desenhada por
// cima da sala no arco do tile; o arco em si vem do layout da sala (borda "arch").
public partial class PortcullisGate : Node3D
{
    // Vão das barras no 3D.
    [Export] public float OpeningWidth { get; set; } = 1.9f;
    [Export] public float OpeningHeight { get; set; } = 2.5f;

    public const int PixelFrames = 8;
    // Espessura das paredes do kit (walls.py, T = 22 unidades): a grade fica no vão do arco, no meio dela.
    public const float PixelWallThickness = 22.0f;
    private const float BarThickness = 0.07f;
    private const float BarPitch = 0.3f;

    /// <summary>Quanto a grade já subiu: 0 = fechada, 1 = aberta.</summary>
    public float OpenAmount { get; private set; }

    private PixelRoom _pixelRoom;
    private Vector2I _pixelTile;
    private string _pixelSide = "ne";
    private MeshInstance3D _pixelGrate;
    private int _pixelFrame;
    private Node3D _bars;
    private Tween _tween;
    private float _target = -1.0f;

    /// <summary>
    /// Ponto do chão no meio do vão do arco da borda <paramref name="side"/> ("ne" ou "nw") do tile (gx, gy), na face
    /// da parede que olha para a sala, com +Z local para dentro da sala, +Y para cima e +X ao longo da parede (mesma
    /// base de ScenePortal.WallBasis). <paramref name="depth"/> recua a origem para dentro da parede (unidades do kit).
    /// </summary>
    public static Transform3D PixelWallTransform(PixelRoom room, int gx, int gy, string side, float depth = 0.0f)
    {
        if (side == "nw")
        {
            // Parede no plano x = gx * 64, sala do lado +x: +Z local vira +X do mundo.
            var origin = room.KitToWorld(gx * 64.0f - depth, gy * 64.0f + 32.0f);
            return new Transform3D(new Basis(Vector3.Up, Mathf.Pi * 0.5f), origin);
        }
        // Parede no plano y = gy * 64, sala do lado +y (= +Z no mundo).
        return new Transform3D(Basis.Identity, room.KitToWorld(gx * 64.0f + 32.0f, gy * 64.0f - depth));
    }

    /// <summary>Modo 2D: a grade vai para o arco da borda <paramref name="side"/> do tile (gx, gy). Chamar antes do _Ready.</summary>
    public void ConfigurePixel(PixelRoom room, int gx, int gy, string side)
    {
        _pixelRoom = room;
        _pixelTile = new Vector2I(gx, gy);
        _pixelSide = side == "nw" ? "nw" : "ne";
    }

    public override void _Ready()
    {
        if (_pixelRoom != null)
            _pixelGrate = _pixelRoom.AddOverlay($"Portcullis_{_pixelTile.X}_{_pixelTile.Y}_{_pixelSide}", PixelPiece(0), _pixelTile.X, _pixelTile.Y);
        else
            CreateBars();
        SetOpenAmount(OpenAmount);
    }

    // A peça 2D mora no PixelRoom, fora deste nó: sai junto com o portão (portas refeitas, cena trocada).
    public override void _ExitTree()
    {
        if (_pixelGrate != null && IsInstanceValid(_pixelGrate))
            _pixelGrate.QueueFree();
        _pixelGrate = null;
    }

    /// <summary>Move a grade até <paramref name="amount"/> (0 = fechada, 1 = aberta) em <paramref name="seconds"/>.</summary>
    public void MoveTo(float amount, float seconds)
    {
        amount = Mathf.Clamp(amount, 0.0f, 1.0f);
        if (Mathf.IsEqualApprox(amount, _target))
            return;
        _target = amount;
        _tween?.Kill();
        if (!IsInsideTree() || seconds <= 0.0f)
        {
            SetOpenAmount(amount);
            return;
        }
        // Mesmo ritmo do portão do hub, proporcional ao trecho que falta.
        var duration = seconds * Mathf.Max(Mathf.Abs(amount - OpenAmount), 0.15f);
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(SetOpenAmount), OpenAmount, amount, duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    public void SetOpenAmount(float amount)
    {
        OpenAmount = amount;
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
                Name = $"GateBar{i}",
                Position = new Vector3(first + i * BarPitch, -OpeningHeight * 0.5f, 0.0f),
                Mesh = new BoxMesh { Size = new Vector3(BarThickness, OpeningHeight, BarThickness) },
                MaterialOverride = material
            });
        }
        foreach (var height in new[] { 0.35f, 0.7f })
        {
            _bars.AddChild(new MeshInstance3D
            {
                Name = $"GateRail{height}",
                Position = new Vector3(0.0f, -OpeningHeight * height, 0.0f),
                Mesh = new BoxMesh { Size = new Vector3(OpeningWidth, BarThickness, BarThickness * 1.4f) },
                MaterialOverride = material
            });
        }
    }
}
