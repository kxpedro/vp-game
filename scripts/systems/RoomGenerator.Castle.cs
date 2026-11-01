using Godot;
using System.Collections.Generic;

// Modo 2D, Ala Superior do Castelo (mapa 3): cada sala comum é um andar inteiro do castelo, com várias salas ligadas
// por corredores. Três plantas se revezam (Torres, Aposentos, Salão). A luta acontece no salão principal (a arena):
// o jogador começa nele, os inimigos só surgem nele e o resto do andar (biblioteca, quartos, galerias) é o caminho
// até a saída. As mesmas plantas estão em tools/pixel_art/castle_kit/floors.py (conceito aprovado).
public partial class RoomGenerator
{
    private static bool UsesCastleKit => GameMode.IsPixelArt && RunState.CurrentMapIndex == EnemyVisualCatalog.CastleMapIndex;

    // Mobília que bloqueia o passo: o tile sai da arena e da área caminhável.
    private static readonly string[] CastleBlockingProps = { "column", "brazier", "armor", "candelabrum", "bookcase", "table", "bed" };

    private void BuildCastleMap()
    {
        var floor = (((RunState.CurrentRoom - 1) % 3) + 3) % 3;
        ApplyGridPlan(floor switch
        {
            1 => BuildCastleApartments(),
            2 => BuildCastleNave(),
            _ => BuildCastleTowers()
        });
    }

    private static void AddRect(HashSet<Vector2I> set, int x0, int y0, int w, int h)
    {
        for (var x = x0; x < x0 + w; x++)
        for (var y = y0; y < y0 + h; y++)
            set.Add(new Vector2I(x, y));
    }

    private static GardenPlan FinishCastlePlan(GardenPlan raw, List<(string Name, int X, int Y)> props, Vector2I entrance, Vector2I focus,
        Vector2I reward, Vector2I exit)
    {
        foreach (var (name, x, y) in props)
        {
            var tile = new Vector2I(x, y);
            if (!raw.Tiles.Contains(tile))
                continue;
            raw.Props.Add(new PixelRoomProp(name, x, y));
            foreach (var prefix in CastleBlockingProps)
            {
                if (!name.StartsWith(prefix))
                    continue;
                raw.Blocked.Add(tile);
                raw.Arena.Remove(tile);
                break;
            }
        }

        raw.Entrance = entrance;
        raw.Focus = focus;
        raw.Reward = reward;
        raw.Exit = exit;
        return Shifted(raw, focus);
    }

    // Andar das Torres: quatro torres nos cantos ligadas por galerias em anel; a arena é o pátio coberto do meio.
    private GardenPlan BuildCastleTowers()
    {
        var raw = new GardenPlan();
        foreach (var (x, y, w, h) in new[]
        {
            (0, 0, 6, 6), (16, 0, 6, 6), (0, 14, 6, 6), (16, 14, 6, 6), (6, 2, 10, 2), (6, 16, 10, 2), (2, 6, 2, 8), (18, 6, 2, 8),
            (8, 7, 6, 6), (10, 4, 2, 3), (10, 13, 2, 3)
        })
            AddRect(raw.Tiles, x, y, w, h);
        AddRect(raw.Arena, 8, 7, 6, 6);

        var props = new List<(string, int, int)>
        {
            ("brazier", 1, 1), ("brazier", 20, 1), ("brazier", 1, 18), ("brazier", 20, 18),
            ("column", 8, 2), ("column", 13, 2), ("column", 8, 17), ("column", 13, 17), ("column", 2, 8), ("column", 2, 11),
            ("column", 19, 8), ("column", 19, 11), ("column", 9, 8), ("column", 12, 8), ("column", 9, 11), ("column", 12, 11),
            ("bookcase_y", 0, 2), ("bookcase_y", 0, 3), ("bed", 17, 1), ("table", 17, 4), ("armor", 3, 15), ("armor", 5, 15),
            ("candelabrum", 18, 16), ("silver_pool", 10, 9), ("rubble_a", 5, 5), ("candelabrum", 11, 6)
        };
        return FinishCastlePlan(raw, props, new Vector2I(10, 12), new Vector2I(10, 9), new Vector2I(10, 7), new Vector2I(3, 0));
    }

    // Andar dos Aposentos: salão central de colunas (a arena), biblioteca a oeste, quarto e armaria a leste, saguão ao sul
    // e antecâmara ao norte, onde fica a saída.
    private GardenPlan BuildCastleApartments()
    {
        var raw = new GardenPlan();
        foreach (var (x, y, w, h) in new[]
        {
            (8, 4, 9, 9), (1, 3, 6, 7), (19, 2, 6, 5), (19, 9, 6, 6), (6, 15, 14, 4), (9, 0, 7, 3), (7, 6, 1, 2), (17, 7, 2, 2),
            (21, 7, 2, 2), (12, 13, 2, 2), (12, 3, 1, 1), (3, 10, 1, 5), (3, 15, 3, 1)
        })
            AddRect(raw.Tiles, x, y, w, h);
        AddRect(raw.Arena, 8, 4, 9, 9);

        var props = new List<(string, int, int)>();
        for (var y = 0; y < 19; y++)
            if (raw.Tiles.Contains(new Vector2I(12, y)))
                props.Add(("carpet_sw", 12, y));
        foreach (var x in new[] { 10, 14 })
            foreach (var y in new[] { 6, 8, 10 })
                props.Add(("column", x, y));
        props.AddRange(new (string, int, int)[]
        {
            ("brazier", 9, 5), ("brazier", 15, 5), ("armor", 9, 11), ("armor", 15, 11),
            ("bookcase_x", 1, 3), ("bookcase_x", 2, 3), ("bookcase_x", 4, 3), ("bookcase_x", 5, 3), ("bookcase_y", 1, 5),
            ("bookcase_y", 1, 6), ("bookcase_y", 1, 8), ("table", 3, 6), ("candelabrum", 5, 8),
            ("bed", 20, 2), ("bed", 23, 2), ("candelabrum", 22, 5), ("armor", 20, 10), ("armor", 23, 10), ("brazier", 21, 13),
            ("rubble_a", 24, 12), ("brazier", 7, 16), ("brazier", 18, 16), ("silver_pool", 12, 17), ("candelabrum", 3, 13),
            ("brazier", 10, 1), ("brazier", 14, 1)
        });
        return FinishCastlePlan(raw, props, new Vector2I(12, 12), new Vector2I(12, 8), new Vector2I(12, 6), new Vector2I(12, 0));
    }

    // Andar do Salão: nave longa em cruz com braços laterais e antecâmara; a arena é o trecho central da nave e a saída
    // fica no fundo norte.
    private GardenPlan BuildCastleNave()
    {
        var raw = new GardenPlan();
        foreach (var (x, y, w, h) in new[]
        {
            (9, 2, 7, 15), (2, 7, 7, 5), (16, 7, 7, 5), (11, 17, 3, 3), (0, 8, 2, 3), (23, 8, 2, 3), (10, 0, 5, 2)
        })
            AddRect(raw.Tiles, x, y, w, h);
        AddRect(raw.Arena, 9, 5, 7, 9);

        var props = new List<(string, int, int)>();
        for (var y = 0; y < 20; y++)
            if (raw.Tiles.Contains(new Vector2I(12, y)))
                props.Add(("carpet_sw", 12, y));
        foreach (var x in new[] { 10, 14 })
            foreach (var y in new[] { 6, 9, 12 })
                props.Add(("column", x, y));
        props.AddRange(new (string, int, int)[]
        {
            ("brazier", 10, 2), ("brazier", 14, 2), ("armor", 3, 7), ("armor", 7, 7), ("armor", 17, 7), ("armor", 21, 7),
            ("table", 4, 9), ("table", 19, 9), ("candelabrum", 3, 11), ("candelabrum", 21, 11), ("silver_pool", 12, 9),
            ("rubble_b", 24, 9), ("brazier", 11, 18), ("brazier", 13, 18)
        });
        return FinishCastlePlan(raw, props, new Vector2I(12, 13), new Vector2I(12, 9), new Vector2I(12, 6), new Vector2I(12, 0));
    }

    // Sala montada com o kit do castelo. Sala de boss do mapa 3: a arena de sempre, com colunas no lugar dos pilares.
    private PixelRoomLayout BuildCastleLayout(Vector2I? exitTile, List<PixelRoomProp> bossPillars)
    {
        if (_gardenPlan != null)
            return PixelArenaLayout.BuildCastle(_gardenPlan.Tiles, _gardenPlan.Blocked, exitTile, _exitWallDirection, _gardenPlan.Props, _rng.Seed);

        var props = new List<PixelRoomProp>();
        foreach (var pillar in bossPillars)
            props.Add(pillar with { Name = "column" });
        return PixelArenaLayout.BuildCastle(CellsToPixelTiles(_walkableCells), null, exitTile, _exitWallDirection, props, _rng.Seed);
    }
}
