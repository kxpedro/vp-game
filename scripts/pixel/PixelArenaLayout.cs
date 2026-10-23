using Godot;
using System.Collections.Generic;
using System.Linq;

// Modo 2D: converte a sala sorteada pelo RoomGenerator (células caminháveis, 2x2 células = 1 tile do kit) num layout
// do kit da cripta, com arco na saída, velas nos cantos, arandelas e nichos nas paredes do fundo e entulho espalhado.
public static class PixelArenaLayout
{
    // Lado da parede da saída (direção para fora, no grid de tiles) -> borda do tile no kit.
    public static string EdgeForOutward(Vector2I outward)
    {
        if (outward == Vector2I.Up) return "ne";
        if (outward == Vector2I.Left) return "nw";
        if (outward == Vector2I.Down) return "sw";
        if (outward == Vector2I.Right) return "se";
        return null;
    }

    public static PixelRoomLayout Build(HashSet<Vector2I> tiles, Vector2I? exitTile, Vector2I exitOutward, IEnumerable<PixelRoomProp> extraProps, ulong seed)
    {
        var layout = new PixelRoomLayout { Seed = (int)(seed % int.MaxValue) };
        foreach (var tile in tiles)
            layout.Tiles[tile] = 0.0f;

        var rng = new RandomNumberGenerator { Seed = seed };
        bool Has(int x, int y) => tiles.Contains(new Vector2I(x, y));
        bool NearExit(Vector2I tile) => exitTile is { } exit && Mathf.Abs(tile.X - exit.X) + Mathf.Abs(tile.Y - exit.Y) <= 1;

        // Saída: arco nas paredes do fundo (ne/nw) e vão nas paredes da frente (se/sw).
        if (exitTile is { } exitAt && EdgeForOutward(exitOutward) is { } exitSide)
            layout.Edges[(exitAt.X, exitAt.Y, exitSide)] = exitSide is "ne" or "nw" ? "arch" : "gap";

        foreach (var tile in tiles.OrderBy(t => t.X).ThenBy(t => t.Y))
        {
            int x = tile.X, y = tile.Y;
            bool ne = !Has(x, y - 1), nw = !Has(x - 1, y), se = !Has(x + 1, y), sw = !Has(x, y + 1);

            // Velas no canto de dentro da sala (bem perto das duas paredes).
            if ((ne || sw) && (nw || se) && !NearExit(tile))
            {
                var dx = nw ? -14.0f : 16.0f;
                var dy = ne ? -16.0f : 18.0f;
                layout.Props.Add(new PixelRoomProp(rng.Randf() < 0.5f ? "candles_large_a" : "candles_large_b", x, y, dx, dy));
                continue;
            }

            if (NearExit(tile))
                continue;

            // Paredes do fundo: arandela ou nicho de vez em quando.
            var roll = rng.Randf();
            if (ne && !nw && roll < 0.22f)
                layout.Props.Add(new PixelRoomProp("sconce_ne", x, y));
            else if (nw && !ne && roll < 0.22f)
                layout.Props.Add(new PixelRoomProp("sconce_nw", x, y));
            else if (ne && !nw && roll < 0.34f)
                layout.Edges[(x, y, "ne")] = "niche";
            else if (nw && !ne && roll < 0.34f)
                layout.Edges[(x, y, "nw")] = "niche";

            // Chão: entulho, ossos e velas soltas, sempre encostados numa parede para não atrapalhar a luta.
            if ((ne || nw || se || sw) && rng.Randf() < 0.12f)
            {
                var pick = rng.RandiRange(0, 3);
                var name = pick switch { 0 => "rubble_a", 1 => "rubble_b", 2 => "bones", _ => rng.Randf() < 0.5f ? "candles_small_a" : "candles_medium_b" };
                layout.Props.Add(new PixelRoomProp(name, x, y, nw ? -12.0f : se ? 12.0f : 0.0f, ne ? -12.0f : sw ? 12.0f : 0.0f));
            }
        }

        if (extraProps != null)
            layout.Props.AddRange(extraProps);
        return layout;
    }
}
