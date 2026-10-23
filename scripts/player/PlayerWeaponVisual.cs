using Godot;

public partial class PlayerWeaponVisual : Node
{
    [Export] public NodePath CharacterModelPath { get; set; } = new("../CharacterModel");
    [Export] public string RightHandBone { get; set; } = "Fist.R";
    [Export] public string LeftHandBone { get; set; } = "Fist.L";

    private BoneAttachment3D _rightSocket;
    private BoneAttachment3D _leftSocket;
    private Node3D _rightWeapon;
    private Node3D _leftWeapon;
    private PlayerCombat _combat;
    private bool _weaponVisible;
    private float _visibleLinger;

    [Export] public float WeaponLingerSeconds { get; set; } = 0.22f;
    private const float TrailLifetime = 0.16f;
    private const int MaxTrailSamples = 14;

    private readonly record struct TrailSample(Vector3 Base, Vector3 Tip, float Time);
    private readonly System.Collections.Generic.List<TrailSample> _trailSamples = new();
    private MeshInstance3D _trailMesh;
    private Vector3 _trailBaseLocal;
    private Vector3 _trailTipLocal;
    private Color _trailColor = new(0.70f, 0.92f, 1.0f, 0.6f);
    private float _trailClock;

    public override void _Ready()
    {
        var model = GetNodeOrNull<Node>(CharacterModelPath);
        var skeleton = FindSkeleton(model);
        if (skeleton == null)
        {
            GD.PushWarning("PlayerWeaponVisual: esqueleto do jogador nao encontrado.");
            return;
        }

        _rightSocket = CreateSocket(skeleton, "WeaponSocketRight", RightHandBone);
        _leftSocket = CreateSocket(skeleton, "WeaponSocketLeft", LeftHandBone);
        _combat = GetParent()?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        WeaponLoadout.EquippedWeaponChanged += RefreshVisual;
        RefreshVisual(WeaponLoadout.EquippedWeaponId);
    }

    public override void _Process(double delta)
    {
        // A arma fica visivel durante o golpe e um instante depois (embainhar sem "piscar").
        if (_combat?.IsAttacking == true)
            _visibleLinger = WeaponLingerSeconds;
        else
            _visibleLinger = Mathf.Max(_visibleLinger - (float)delta, 0.0f);
        SetWeaponVisibility(_visibleLinger > 0.0f);
        UpdateTrail((float)delta);
    }

    public override void _ExitTree()
    {
        WeaponLoadout.EquippedWeaponChanged -= RefreshVisual;
        if (_trailMesh != null && IsInstanceValid(_trailMesh))
            _trailMesh.QueueFree();
    }

    private void UpdateTrail(float delta)
    {
        _trailClock += delta;
        if (_trailMesh == null || _rightWeapon == null || !IsInstanceValid(_rightWeapon))
            return;

        var phase = _combat?.CurrentAttackPhase ?? PlayerAttackPhase.None;
        // Modo 2D: com o sprite ligado o modelo 3D some, e o rastro dele duplicaria a espada do sprite.
        var spriteOn = GameMode.IsPixelArt && PlayerSpriteVisual.Enabled;
        var swinging = _combat?.IsAttacking == true && phase != PlayerAttackPhase.None && !spriteOn;
        if (swinging && _weaponVisible)
        {
            var transform = _rightWeapon.GlobalTransform;
            var baseWorld = transform * _trailBaseLocal;
            var tipWorld = transform * _trailTipLocal;
            var count = _trailSamples.Count;
            if (count == 0 || _trailSamples[count - 1].Tip.DistanceTo(tipWorld) > 0.05f)
            {
                _trailSamples.Add(new TrailSample(baseWorld, tipWorld, _trailClock));
                if (_trailSamples.Count > MaxTrailSamples)
                    _trailSamples.RemoveAt(0);
            }
        }

        while (_trailSamples.Count > 0 && _trailClock - _trailSamples[0].Time > TrailLifetime)
            _trailSamples.RemoveAt(0);

        var mesh = (ImmediateMesh)_trailMesh.Mesh;
        mesh.ClearSurfaces();
        if (_trailSamples.Count < 2)
        {
            _trailMesh.Visible = false;
            return;
        }

        _trailMesh.Visible = true;
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        foreach (var sample in _trailSamples)
        {
            var age = Mathf.Clamp((_trailClock - sample.Time) / TrailLifetime, 0.0f, 1.0f);
            var color = _trailColor;
            color.A *= 1.0f - age;
            mesh.SurfaceSetColor(new Color(color.R, color.G, color.B, 0.0f));
            mesh.SurfaceAddVertex(sample.Base);
            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(sample.Tip);
        }
        mesh.SurfaceEnd();
    }

    private void CreateTrail(WeaponVisualDefinition definition)
    {
        _trailSamples.Clear();
        if (definition.UsesLeftHand || _rightWeapon == null)
            return;

        if (!TryGetLocalBounds(_rightWeapon, out var minimum, out var maximum))
            return;

        // Eixo mais longo da arma = lamina. A ponta e a extremidade mais distante da mao.
        var size = maximum - minimum;
        var axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
        var centre = (minimum + maximum) * 0.5f;
        var low = centre;
        var high = centre;
        low[axis] = minimum[axis];
        high[axis] = maximum[axis];
        var lowIsTip = low.LengthSquared() > high.LengthSquared();
        var tip = lowIsTip ? low : high;
        var handle = lowIsTip ? high : low;
        _trailTipLocal = tip;
        _trailBaseLocal = handle.Lerp(tip, 0.40f);
        _trailColor = definition.TrailColor.A > 0.0f ? definition.TrailColor : new Color(0.70f, 0.92f, 1.0f, 0.6f);

        if (_trailMesh == null)
        {
            _trailMesh = new MeshInstance3D
            {
                Name = "WeaponTrail",
                Mesh = new ImmediateMesh(),
                TopLevel = true,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add
                }
            };
            // O pai ainda está montando os filhos em _Ready: adiciona fora do frame (TopLevel já parte da identidade).
            GetParent()?.CallDeferred(Node.MethodName.AddChild, _trailMesh);
        }
    }

    private void RefreshVisual(string weaponId)
    {
        ClearWeapon(ref _rightWeapon);
        ClearWeapon(ref _leftWeapon);
        if (_rightSocket == null || _leftSocket == null)
            return;

        var definition = WeaponVisualCatalog.Get(weaponId);
        _rightWeapon = CreateWeapon(
            _rightSocket,
            definition,
            definition.RightPosition,
            definition.RightRotationDegrees,
            "EquippedWeaponRight");

        if (definition.UsesLeftHand)
            _leftWeapon = CreateWeapon(
                _leftSocket,
                definition,
                definition.LeftPosition,
                definition.LeftRotationDegrees,
                "EquippedWeaponLeft");
        CreateTrail(definition);
        SetWeaponVisibility(_visibleLinger > 0.0f, true);
    }

    private void SetWeaponVisibility(bool visible, bool force = false)
    {
        if (!force && _weaponVisible == visible)
            return;

        _weaponVisible = visible;
        if (_rightWeapon != null && IsInstanceValid(_rightWeapon))
            _rightWeapon.Visible = visible;
        if (_leftWeapon != null && IsInstanceValid(_leftWeapon))
            _leftWeapon.Visible = visible;
    }

    private static Node3D CreateWeapon(
        BoneAttachment3D socket,
        WeaponVisualDefinition definition,
        Vector3 position,
        Vector3 rotationDegrees,
        string nodeName)
    {
        var scene = ResourceLoader.Load<PackedScene>(definition.ScenePath);
        if (scene == null)
        {
            GD.PushWarning($"PlayerWeaponVisual: arma 3D nao importada: {definition.ScenePath}");
            return null;
        }

        var weapon = scene.Instantiate<Node3D>();
        weapon.Name = nodeName;
        socket.AddChild(weapon);
        NormalizeLargestDimension(weapon, definition.TargetSize);
        weapon.Position += position;
        weapon.RotationDegrees = rotationDegrees;
        return weapon;
    }

    private static BoneAttachment3D CreateSocket(Skeleton3D skeleton, string nodeName, string boneName)
    {
        var socket = new BoneAttachment3D
        {
            Name = nodeName,
            BoneName = boneName
        };
        skeleton.AddChild(socket);
        return socket;
    }

    private static void ClearWeapon(ref Node3D weapon)
    {
        if (weapon != null && IsInstanceValid(weapon))
            weapon.QueueFree();
        weapon = null;
    }

    private static void NormalizeLargestDimension(Node3D root, float targetSize)
    {
        if (!TryGetLocalBounds(root, out var minimum, out var maximum))
            return;

        var size = maximum - minimum;
        var largest = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        if (largest > 0.001f)
            root.Scale *= targetSize / largest;
    }

    private static bool TryGetLocalBounds(Node3D root, out Vector3 minimum, out Vector3 maximum)
    {
        minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var foundMesh = false;
        var toRoot = root.GlobalTransform.AffineInverse();

        foreach (var candidate in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (candidate is not MeshInstance3D mesh || mesh.Mesh == null)
                continue;

            var meshToRoot = toRoot * mesh.GlobalTransform;
            var bounds = mesh.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var point = bounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0.0f : bounds.Size.X,
                    (corner & 2) == 0 ? 0.0f : bounds.Size.Y,
                    (corner & 4) == 0 ? 0.0f : bounds.Size.Z);
                point = meshToRoot * point;
                minimum = new Vector3(
                    Mathf.Min(minimum.X, point.X),
                    Mathf.Min(minimum.Y, point.Y),
                    Mathf.Min(minimum.Z, point.Z));
                maximum = new Vector3(
                    Mathf.Max(maximum.X, point.X),
                    Mathf.Max(maximum.Y, point.Y),
                    Mathf.Max(maximum.Z, point.Z));
                foundMesh = true;
            }
        }

        return foundMesh;
    }

    private static Skeleton3D FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton)
            return skeleton;
        if (node == null)
            return null;

        foreach (var child in node.GetChildren())
            if (FindSkeleton(child) is { } found)
                return found;
        return null;
    }
}
