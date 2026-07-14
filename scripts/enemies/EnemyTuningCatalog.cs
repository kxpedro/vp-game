using Godot;

public enum EnemyRarity { Normal, Magic, Rare, Legendary }
public enum EnemyArchetype { Melee, Ranged, Tank }

public sealed record EnemyRarityDefinition(string DisplayName, Color Color, float Health, float Damage, float Speed, int ModifierCount, bool FirstHitShield);

public static class EnemyTuningCatalog
{
    public static EnemyRarityDefinition Get(EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Magic => new("Mágico", new Color(0.15f, 0.45f, 1), 1.45f, 1.3f, 1.08f, 1, true),
        EnemyRarity.Rare => new("Raro", new Color(1, 0.72f, 0.08f), 2.35f, 1.75f, 1.14f, 3, true),
        EnemyRarity.Legendary => new("Lendário", new Color(1, 0.35f, 0.04f), 6.0f, 2.8f, 1.22f, 5, true),
        _ => new("Normal", Colors.White, 1, 1, 1, 0, false)
    };

    public static EnemyRarity Roll(RandomNumberGenerator rng, int mapIndex, float roomProgress)
    {
        roomProgress = Mathf.Clamp(roomProgress, 0, 1);
        var firstMap = mapIndex <= 0;
        var normal = firstMap ? Mathf.Lerp(0.90f, 0.72f, roomProgress) : Mathf.Lerp(0.68f, 0.45f, roomProgress);
        var magic = firstMap ? Mathf.Lerp(0.10f, 0.23f, roomProgress) : Mathf.Lerp(0.25f, 0.32f, roomProgress);
        var rare = firstMap ? Mathf.Lerp(0.0f, 0.05f, roomProgress) : Mathf.Lerp(0.07f, 0.18f, roomProgress);
        var roll = rng.Randf();
        if (roll < normal) return EnemyRarity.Normal;
        if (roll < normal + magic) return EnemyRarity.Magic;
        if (roll < normal + magic + rare) return EnemyRarity.Rare;
        return firstMap ? EnemyRarity.Rare : EnemyRarity.Legendary;
    }
}
