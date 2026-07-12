public sealed record BossMoveDefinition(string Id, string DisplayName, float Cooldown, float DamageMultiplier, float RangeMultiplier);
public sealed record BossDefinition(string Id, string DisplayName, float TargetDurationSeconds, float ExpectedPlayerDps, BossMoveDefinition[] Moves)
{
    public float EstimatedMaxHealth => System.MathF.Round(TargetDurationSeconds * ExpectedPlayerDps);
}

public static class BossCatalog
{
    private static readonly BossDefinition[] Bosses =
    {
        new("silver_dragonkin", "Dragonkin Prateado", 300, 16.6667f, new[]
        {
            new BossMoveDefinition("claw", "Garras Prateadas", 1.0f, 1.0f, 1.0f),
            new BossMoveDefinition("tail_sweep", "Varredura de Cauda", 1.45f, 1.25f, 1.35f),
            new BossMoveDefinition("dragon_burst", "Explosao Draconica", 2.1f, 1.6f, 1.65f)
        }),
        new("invisible_woman", "Guardia Invisivel", 300, 60, new[]
        {
            new BossMoveDefinition("psionic", "Rajada Psionica", 1.4f, 1.0f, 1.4f),
            new BossMoveDefinition("shield", "Escudo Cinetico", 2.5f, 1.2f, 1.2f),
            new BossMoveDefinition("outbreak", "Surto de Forca", 3.2f, 1.7f, 1.7f)
        })
    };
    public static BossDefinition Get(int mapIndex) => Bosses[System.Math.Clamp(mapIndex, 0, Bosses.Length - 1)];
}
