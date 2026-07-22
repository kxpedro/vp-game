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
        new("armands_dungeons", "Calaboucos de Armand", 6, "blood_mage", "Subsolo: celas, criptas e camaras de tortura", 1.0f),
        new("blood_garden", "Jardim Sangrento - Primeiro Andar", 8, "thorn_warden", "Neblina e vegetacao hostil alteram os comodos", 1.35f),
        // Novos andares entram aqui sem alterar o fluxo de RunState/RunManager.
        new("upper_castle", "Ala Superior do Castelo", 10, "labyrinth_keeper", "Andar reservado para a proxima etapa da subida", 1.75f),
        new("armands_throne", "Trono de Armand - Torre Superior", 0, "armand", "Confronto final em multiplas fases", 2.25f)
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
