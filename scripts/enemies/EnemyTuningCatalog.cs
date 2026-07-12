using Godot;

public enum EnemyRarity { Normal, Magic, Rare, Legendary }
public enum EnemyArchetype { Melee, Ranged, Tank }

public sealed record EnemyRarityDefinition(string DisplayName, Color Color, float Health, float Damage, float Speed, int ModifierCount, bool FirstHitShield);

public static class EnemyTuningCatalog
{
    public static EnemyRarityDefinition Get(EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Magic => new("Magico", new Color(0.15f, 0.45f, 1), 1.35f, 1.25f, 1.08f, 1, true),
        EnemyRarity.Rare => new("Raro", new Color(1, 0.72f, 0.08f), 1.9f, 1.55f, 1.12f, 3, true),
        EnemyRarity.Legendary => new("Lendario", new Color(1, 0.35f, 0.04f), 3.5f, 2.1f, 1.18f, 5, true),
        _ => new("Normal", Colors.White, 1, 1, 1, 0, false)
    };

    public static EnemyRarity Roll(RandomNumberGenerator rng, float roomProgress)
    {
        roomProgress = Mathf.Clamp(roomProgress, 0, 1);
        var normal = Mathf.Lerp(0.80f, 0.40f, roomProgress);
        var magic = Mathf.Lerp(0.15f, 0.30f, roomProgress);
        var rare = Mathf.Lerp(0.05f, 0.22f, roomProgress);
        var roll = rng.Randf();
        if (roll < normal) return EnemyRarity.Normal;
        if (roll < normal + magic) return EnemyRarity.Magic;
        if (roll < normal + magic + rare) return EnemyRarity.Rare;
        return EnemyRarity.Legendary;
    }
}
