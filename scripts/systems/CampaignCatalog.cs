using System;
using System.Collections.Generic;

public sealed record MapDefinition(
    string Id,
    string Name,
    int CommonRoomCount,
    string BossId,
    string SpecialRule,
    float DifficultyMultiplier
);

public static class CampaignCatalog
{
    private static readonly MapDefinition[] Maps =
    {
        new("ruined_castle", "Castelo em Ruinas", 3, "blood_mage", "Altares de sangue: vida por recompensa", 1.0f),
        new("crimson_gardens", "Jardins Carmesins", 4, "thorn_warden", "Neblina e vegetacao hostil alteram as rotas", 1.35f),
        new("shifting_dungeons", "Masmorras Mutaveis", 5, "labyrinth_keeper", "Armadilhas e modificadores aleatorios de sala", 1.75f),
        new("armands_throne", "Trono de Armand", 0, "armand", "Confronto final em multiplas fases", 2.25f)
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
