using System;
using System.Collections.Generic;

// Nome e regra exibidos vem de data/localization/<idioma>/world.json (map.<id>.name / map.<id>.rule),
// resolvidos a cada leitura para acompanhar a troca de idioma.
public sealed record MapDefinition(
    string Id,
    int CommonRoomCount,
    string BossId,
    float DifficultyMultiplier
)
{
    public string NameKey => $"map.{Id}.name";
    public string SpecialRuleKey => $"map.{Id}.rule";
    public string Name => Localization.Text(NameKey);
    public string SpecialRule => Localization.Text(SpecialRuleKey);
}

public static class CampaignCatalog
{
    // BossId deve coincidir com o Id do BossCatalog no mesmo indice de mapa.
    private static readonly MapDefinition[] Maps =
    {
        new("armands_dungeons", 6, "invisible_woman", 1.0f),
        new("blood_garden", 8, "labyrinth_keeper", 1.35f),
        // Novos andares entram aqui sem alterar o fluxo de RunState/RunManager.
        new("upper_castle", 10, "silver_dragonkin", 1.75f),
        new("armands_throne", 0, "armand", 2.25f)
    };

    public static IReadOnlyList<MapDefinition> All => Maps;
    public static int Count => Maps.Length;

    public static MapDefinition Get(int index)
    {
        if (Maps.Length == 0)
            throw new InvalidOperationException("A campanha precisa ter ao menos um mapa.");

        return Maps[Math.Clamp(index, 0, Maps.Length - 1)];
    }
}
