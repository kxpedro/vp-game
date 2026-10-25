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
    // Indice do Jardim Sangrento em CampaignCatalog (plantas no 2D).
    public const int GardenMapIndex = 1;

    private static readonly System.Collections.Generic.Dictionary<string, Godot.PackedScene> SceneCache = new();

    public static void BeginWarmup(bool isBoss, int mapIndex)
    {
        if (isBoss)
        {
            var bossPath = GetBoss(mapIndex).ScenePath;
            if (!PixelEnemySprite.IsPixelPath(bossPath))
                Godot.ResourceLoader.LoadThreadedRequest(bossPath);
            return;
        }

        foreach (EnemyArchetype archetype in System.Enum.GetValues(typeof(EnemyArchetype)))
        {
            var path = GetEnemy(archetype).ScenePath;
            if (!PixelEnemySprite.IsPixelPath(path))
                Godot.ResourceLoader.LoadThreadedRequest(path);
        }
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

    public static EnemyVisualDefinition GetEnemy(EnemyArchetype archetype) => GameMode.IsPixelArt ? GetPixelEnemy(archetype) : archetype switch
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

    // Modo 2D: monstros em pixel art (PixelEnemySprite). Corpo a corpo = goblin ou cogumelo, a distancia = olho voador,
    // tanque = esqueleto. No Jardim Sangrento (mapa 2), plantas predadoras e, no corpo a corpo, metade zumbis. Chefes: GetBoss.
    private static EnemyVisualDefinition GetPixelEnemy(EnemyArchetype archetype) => RunState.CurrentMapIndex == GardenMapIndex ? archetype switch
    {
        EnemyArchetype.Ranged => new(PixelEnemySprite.PathPrefix + "plant_bulb", 1.0f, "Idle", 0.48f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact"),
        EnemyArchetype.Tank => new(PixelEnemySprite.PathPrefix + "plant_purple", 1.0f, "Idle", 0.68f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact"),
        _ => new(PixelEnemySprite.PathPrefix + (Godot.GD.Randf() < 0.5f ? "plant_red" : "zombie"), 1.0f, "Idle", 0.48f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact")
    } : archetype switch
    {
        EnemyArchetype.Ranged => new(PixelEnemySprite.PathPrefix + "flying_eye", 1.0f, "Idle", 0.48f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact"),
        EnemyArchetype.Tank => new(PixelEnemySprite.PathPrefix + "skeleton", 1.0f, "Idle", 0.68f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact"),
        _ => new(PixelEnemySprite.PathPrefix + (Godot.GD.Randf() < 0.5f ? "goblin" : "mushroom"), 1.0f, "Idle", 0.48f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact")
    };

    // Modo 2D: a Guardiã Invisível usa o sprite do Mage Guardian; os outros chefes continuam com o modelo 3D.
    public static EnemyVisualDefinition GetBoss(int mapIndex) => GameMode.IsPixelArt && mapIndex == 0
        ? new(PixelEnemySprite.PathPrefix + "mage_guardian", 1.5f, "Idle", 0.95f, false, 1.0f, 0.0f, "Walk", "Attack", "HitReact")
        : GetModelBoss(mapIndex);

    private static EnemyVisualDefinition GetModelBoss(int mapIndex) => mapIndex switch
    {
        // Indice do mapa = boss: 0 invisible_woman, 1 labyrinth_keeper, 2 silver_dragonkin, 3 armand.
        0 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Ghost_Skull.gltf",
            1.0f, "Flying_Idle", 0.95f, true, 1.85f, 180.0f, "Fast_Flying", "Punch", "HitReact"),
        1 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Orc_Skull.gltf",
            1.0f, "Idle", 1.05f, true, 1.9f, 180.0f, "Run", "Weapon", "HitReact"),
        2 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/Dragon_Evolved.gltf",
            1.0f, "Flying_Idle", 1.2f, true, 2.15f, 180.0f, "Fast_Flying", "Headbutt", "HitReact"),
        3 => new(
            "res://assets/enemies/quaternius_ultimate_monsters/BlueDemon.gltf",
            1.0f, "Idle", 1.05f, true, 2.05f, 180.0f, "Run", "Punch", "HitReact"),
        _ => GetBoss(0)
    };
}
