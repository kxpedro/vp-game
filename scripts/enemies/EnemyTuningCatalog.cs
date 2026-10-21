using Godot;

public enum EnemyRarity { Normal, Magic, Rare, Legendary }
public enum EnemyArchetype { Melee, Ranged, Tank }

// Id estavel em minusculas; o nome exibido vem de world.json (enemy.rarity.<id>.name) a cada leitura.
public sealed record EnemyRarityDefinition(string Id, Color Color, float Health, float Damage, float Speed, int ModifierCount, bool FirstHitShield)
{
    public string DisplayName => Localization.Text($"enemy.rarity.{Id}.name");
}

public static class EnemyTuningCatalog
{
    // Multiplicadores de vida/dano/velocidade lidos do CompendiumTuning (enemy.rarity.<id>.health|damage|speed).
    private static float T(string id, string field, float defaultValue) => CompendiumTuning.GetFloat($"enemy.rarity.{id}.{field}", defaultValue);

    // Padroes de fabrica (vida, dano, velocidade) por raridade; tambem usados como referencia por RunManager.ApplyCommonEnemyTuning.
    public static (float Health, float Damage, float Speed) DefaultMultipliers(EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Magic => (1.45f, 1.3f, 1.08f),
        EnemyRarity.Rare => (2.35f, 1.75f, 1.14f),
        EnemyRarity.Legendary => (6.0f, 2.8f, 1.22f),
        _ => (1f, 1f, 1f)
    };

    public static EnemyRarityDefinition Get(EnemyRarity rarity)
    {
        var d = DefaultMultipliers(rarity);
        return rarity switch
        {
            EnemyRarity.Magic => new("magic", new Color(0.15f, 0.45f, 1), T("magic", "health", d.Health), T("magic", "damage", d.Damage), T("magic", "speed", d.Speed), 1, true),
            EnemyRarity.Rare => new("rare", new Color(1, 0.72f, 0.08f), T("rare", "health", d.Health), T("rare", "damage", d.Damage), T("rare", "speed", d.Speed), 3, true),
            EnemyRarity.Legendary => new("legendary", new Color(1, 0.35f, 0.04f), T("legendary", "health", d.Health), T("legendary", "damage", d.Damage), T("legendary", "speed", d.Speed), 5, true),
            _ => new("normal", Colors.White, 1, 1, 1, 0, false)
        };
    }

    // Nome exibido do arquetipo (world.json: enemy.archetype.<id>.name). A HUD/compendio pode consumir.
    public static string ArchetypeId(EnemyArchetype archetype) => archetype switch
    {
        EnemyArchetype.Ranged => "ranged",
        EnemyArchetype.Tank => "tank",
        _ => "melee"
    };

    public static string ArchetypeName(EnemyArchetype archetype) => Localization.Text($"enemy.archetype.{ArchetypeId(archetype)}.name");

    public static EnemyRarity Roll(RandomNumberGenerator rng, int mapIndex, float roomProgress)
    {
        roomProgress = Mathf.Clamp(roomProgress, 0, 1);
        var firstMap = mapIndex <= 0;
        // Faixas de rolagem (inicio -> fim da progressao): enemy.roll.<first|later>.<raridade>_<start|end>.
        var set = firstMap ? "first" : "later";
        float Range(string rarity, float start, float end) =>
            Mathf.Lerp(CompendiumTuning.GetFloat($"enemy.roll.{set}.{rarity}_start", start), CompendiumTuning.GetFloat($"enemy.roll.{set}.{rarity}_end", end), roomProgress);
        var normal = firstMap ? Range("normal", 0.90f, 0.72f) : Range("normal", 0.68f, 0.45f);
        var magic = firstMap ? Range("magic", 0.10f, 0.23f) : Range("magic", 0.25f, 0.32f);
        var rare = firstMap ? Range("rare", 0.0f, 0.05f) : Range("rare", 0.07f, 0.18f);
        var roll = rng.Randf();
        if (roll < normal) return EnemyRarity.Normal;
        if (roll < normal + magic) return EnemyRarity.Magic;
        if (roll < normal + magic + rare) return EnemyRarity.Rare;
        return firstMap ? EnemyRarity.Rare : EnemyRarity.Legendary;
    }
}
