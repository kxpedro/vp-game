using Godot;

public sealed record WeaponVisualDefinition(
    string ScenePath,
    float TargetSize,
    Vector3 RightPosition,
    Vector3 RightRotationDegrees,
    bool UsesLeftHand = false,
    Vector3 LeftPosition = default,
    Vector3 LeftRotationDegrees = default,
    Color TrailColor = default);

public static class WeaponVisualCatalog
{
    public static WeaponVisualDefinition Get(string weaponId) => weaponId switch
    {
        "dual_revolvers" => new(
            "res://assets/weapons/quaternius_ultimate_guns/Revolver.glb",
            0.52f,
            new Vector3(0.0f, 0.0f, 0.02f),
            new Vector3(0.0f, 90.0f, -90.0f),
            true,
            new Vector3(0.0f, 0.0f, 0.02f),
            new Vector3(0.0f, -90.0f, 90.0f)),
        "whip" => new(
            "res://assets/weapons/google_poly_whip/Whip.glb",
            1.65f,
            new Vector3(0.02f, 0.0f, 0.0f),
            new Vector3(0.0f, 90.0f, -90.0f),
            TrailColor: new Color(0.95f, 0.88f, 0.65f, 0.6f)),
        _ => new(
            "res://assets/weapons/quaternius_modular_weapons/Sword_Big.fbx",
            1.85f,
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 0.0f, -90.0f),
            TrailColor: new Color(0.70f, 0.92f, 1.0f, 0.6f))
    };
}
