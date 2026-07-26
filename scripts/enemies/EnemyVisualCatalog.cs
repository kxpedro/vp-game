public sealed record EnemyVisualDefinition(
    string ScenePath,
    float Scale,
    string IdleAnimation,
    float CombatRadius,
    bool MatchPlayerHeight = false,
    float PlayerHeightMultiplier = 1.0f,
    float FacingYawDegrees = 0.0f,
    string WalkAnimation = "",
    string AttackAnimation = "",
    string HitAnimation = "");

public static class EnemyVisualCatalog
{
    private static readonly System.Collections.Generic.Dictionary<string, Godot.PackedScene> SceneCache = new();

    public static void BeginWarmup(bool isBoss, int mapIndex)
    {
        if (isBoss)
        {
            Godot.ResourceLoader.LoadThreadedRequest(GetBoss(mapIndex).ScenePath);
            return;
        }

        foreach (EnemyArchetype archetype in System.Enum.GetValues(typeof(EnemyArchetype)))
            Godot.ResourceLoader.LoadThreadedRequest(GetEnemy(archetype).ScenePath);
    }

    public static Godot.PackedScene LoadScene(EnemyVisualDefinition definition)
    {
        if (SceneCache.TryGetValue(definition.ScenePath, out var cached)) return cached;
        var status = Godot.ResourceLoader.LoadThreadedGetStatus(definition.ScenePath);
        if (status == Godot.ResourceLoader.ThreadLoadStatus.Loaded)
        {
            var threaded = Godot.ResourceLoader.LoadThreadedGet(definition.ScenePath) as Godot.PackedScene;
            if (threaded != null) SceneCache[definition.ScenePath] = threaded;
            return threaded;
        }
        var scene = Godot.ResourceLoader.Load<Godot.PackedScene>(definition.ScenePath);
        if (scene != null) SceneCache[definition.ScenePath] = scene;
        return scene;
    }

    public static EnemyVisualDefinition GetEnemy(EnemyArchetype archetype) => archetype switch
    {
        EnemyArchetype.Ranged => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Demon.gltf",
            1.0f, "Idle", 0.48f, true, 1.0f, 180.0f, "Run", "Punch", "HitReact"),
        EnemyArchetype.Tank => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Orc.gltf",
            1.0f, "Idle", 0.68f, true, 1.28f, 180.0f, "Walk", "Weapon", "HitReact"),
        _ => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Ninja.gltf",
            1.0f, "Idle", 0.48f, true, 1.0f, 180.0f, "Run", "Weapon", "HitReact")
    };

    public static EnemyVisualDefinition GetBoss(int mapIndex) => mapIndex switch
    {
        0 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Dragon_Evolved.gltf",
            1.0f, "Flying_Idle", 1.2f, true, 2.15f, 180.0f, "Fast_Flying", "Headbutt", "HitReact"),
        1 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Ghost_Skull.gltf",
            1.0f, "Flying_Idle", 0.95f, true, 1.85f, 180.0f, "Fast_Flying", "Punch", "HitReact"),
        2 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Orc_Skull.gltf",
            1.0f, "Idle", 1.05f, true, 1.9f, 180.0f, "Run", "Weapon", "HitReact"),
        3 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/BlueDemon.gltf",
            1.0f, "Idle", 1.05f, true, 2.05f, 180.0f, "Run", "Punch", "HitReact"),
        _ => GetBoss(0)
    };
}
