public sealed record EnemyVisualDefinition(string ScenePath, float Scale, string IdleAnimation, float CombatRadius, bool MatchPlayerHeight = false, float PlayerHeightMultiplier = 1.0f, float FacingYawDegrees = 0.0f);

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

    public static EnemyVisualDefinition GetBoss(int mapIndex) => mapIndex switch
    {
        0 => new("res://3d/silver_dragonkin_mir4.glb", 1.0f, "Mon_BlackDragon31_Btl_Std01", 1.2f, true, 2.0f, 180.0f),
        1 => new("res://3d/invisible_women__marvel_rivals.glb", 1.45f, "Idle_C", 0.9f, false, 1.0f, 180.0f),
        _ => GetBoss(0)
    };
}
