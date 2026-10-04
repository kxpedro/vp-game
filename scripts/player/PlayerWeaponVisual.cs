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
        SetWeaponVisibility(_combat?.IsAttacking == true);
    }

    public override void _ExitTree()
    {
        WeaponLoadout.EquippedWeaponChanged -= RefreshVisual;
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
        SetWeaponVisibility(_combat?.IsAttacking == true, true);
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
