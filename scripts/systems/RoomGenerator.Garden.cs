using Godot;
using System.Collections.Generic;

// Modo 2D, Jardim Sangrento (mapa 2): as salas comuns alternam entre a "clareira" (jardim formal com quatro canteiros e
// uma topiaria no meio) e os "corredores" (labirinto de sebes com uma clareira de combate no meio e a saída no fundo).
// A planta é em tiles do kit (1 tile = 2x2 células); a mesma regra gera as prévias em
// tools/pixel_art/garden_kit/preview_rooms.py. Os inimigos não desviam de obstáculos (vão reto até o jogador), então a
// luta fica na clareira: o jogador começa nela, os inimigos só surgem nela e o labirinto é o caminho até as portas.
public partial class RoomGenerator
{
    // Macrocélulas por lado do labirinto (cada uma = 1 tile de corredor + 1 tile de sebe).
    private const int GardenMazeSize = 8;
    private const float GardenMazeBraidChance = 0.1f;
    private const float GardenDeadEndPropChance = 0.6f;

    private GardenPlan _gardenPlan;

    // Planta da sala do jardim, já deslocada para o centro da clareira ficar perto da célula (0, 0).
    private sealed class GardenPlan
    {
        public readonly HashSet<Vector2I> Tiles = new();
        public readonly HashSet<Vector2I> Blocked = new();
        public readonly HashSet<Vector2I> Arena = new();
        public readonly List<PixelRoomProp> Props = new();
        public Vector2I Entrance;
        public Vector2I Focus;
        public Vector2I Reward;
        public Vector2I Exit;
    }

    private static bool UsesGardenKit => GameMode.IsPixelArt && RunState.CurrentMapIndex == EnemyVisualCatalog.GardenMapIndex;

    private void BuildGardenMap()
    {
        // Sala ímpar = clareira, par = corredores (a 1ª sala do mapa é a clareira).
        var plan = RunState.CurrentRoom % 2 == 0 ? BuildGardenMaze() : BuildGardenClearing();
        _gardenPlan = plan;

        foreach (var tile in plan.Tiles)
        {
            if (plan.Blocked.Contains(tile))
                continue;
            var arena = plan.Arena.Contains(tile);
            for (var dx = 0; dx < 2; dx++)
            for (var dy = 0; dy < 2; dy++)
            {
                var cell = new Vector2I(tile.X * 2 + dx, tile.Y * 2 + dy);
                _walkableCells.Add(cell);
                if (arena)
                    _roomCells.Add(cell);
            }
        }

        _entranceCell = TileCell(plan.Entrance);
        _focusCell = TileCell(plan.Focus);
        _rewardCell = TileCell(plan.Reward);
        // Saída na sebe do fundo (ne), na fileira de células encostada nela.
        _exitCell = new Vector2I(plan.Exit.X * 2, plan.Exit.Y * 2);
        _exitWallDirection = Vector2I.Up;
        _exitHasDoors = RoomRewardCatalog.HasRoomRewardSet(RunState.CurrentMapIndex, RunState.CurrentRoom);
    }

    private static Vector2I TileCell(Vector2I tile) => new(tile.X * 2 + 1, tile.Y * 2 + 1);

    // Clareira 11x11: canteiros 2x2 nos quatro quadrantes, topiaria no meio, lanternas nos cantos e saída no fundo.
    private GardenPlan BuildGardenClearing()
    {
        const int size = 11;
        const int mid = size / 2;
        var raw = new GardenPlan();
        for (var x = 0; x < size; x++)
        for (var y = 0; y < size; y++)
            raw.Tiles.Add(new Vector2I(x, y));

        foreach (var (px, py) in new[] { (2, 2), (7, 2), (2, 7), (7, 7) })
        {
            raw.Props.Add(new PixelRoomProp("parterre", px, py));
            for (var dx = 0; dx < 2; dx++)
            for (var dy = 0; dy < 2; dy++)
                raw.Blocked.Add(new Vector2I(px + dx, py + dy));
        }
        raw.Props.Add(new PixelRoomProp("topiary_center", mid, mid));
        raw.Blocked.Add(new Vector2I(mid, mid));
        foreach (var (lx, ly) in new[] { (1, 1), (size - 2, 1), (1, size - 2), (size - 2, size - 2) })
            raw.Props.Add(new PixelRoomProp("lantern", lx, ly));
        foreach (var (bx, by) in new[] { (mid - 1, mid - 1), (mid + 1, mid - 1), (mid - 1, mid + 1), (mid + 1, mid + 1) })
            raw.Props.Add(new PixelRoomProp("topiary_ball", bx, by));

        foreach (var tile in raw.Tiles)
            if (!raw.Blocked.Contains(tile))
                raw.Arena.Add(tile);
        raw.Entrance = new Vector2I(mid, size - 1);
        raw.Focus = new Vector2I(mid, mid + 2);
        raw.Reward = new Vector2I(mid, 2);
        raw.Exit = new Vector2I(mid, 0);
        return Shifted(raw, new Vector2I(mid, mid));
    }

    // Labirinto de macrocélulas (corredor de 1 tile, sebe de 1 tile): busca em profundidade com alguns laços, clareira
    // 5x5 no meio e saída aberta na sebe da primeira fileira.
    private GardenPlan BuildGardenMaze()
    {
        const int n = GardenMazeSize;
        const int s = 2 * n + 1;
        var wall = new bool[s, s]; // [gy, gx]
        for (var i = 0; i < s; i++)
        for (var j = 0; j < s; j++)
            wall[i, j] = i % 2 == 0 || j % 2 == 0;

        var seen = new bool[n, n];
        var stack = new Stack<Vector2I>();
        stack.Push(Vector2I.Zero);
        seen[0, 0] = true;
        var neighbors = new List<Vector2I>(4);
        while (stack.Count > 0)
        {
            var current = stack.Peek();
            neighbors.Clear();
            foreach (var step in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up })
            {
                var next = current + step;
                if (next.X >= 0 && next.X < n && next.Y >= 0 && next.Y < n && !seen[next.X, next.Y])
                    neighbors.Add(next);
            }
            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }
            var chosen = neighbors[_rng.RandiRange(0, neighbors.Count - 1)];
            // X = linha (i), Y = coluna (j) da macrocélula, como no preview_rooms.py.
            if (chosen.X != current.X)
                wall[2 * Mathf.Max(chosen.X, current.X), 2 * current.Y + 1] = false;
            else
                wall[2 * current.X + 1, 2 * Mathf.Max(chosen.Y, current.Y)] = false;
            seen[chosen.X, chosen.Y] = true;
            stack.Push(chosen);
        }
        // Alguns laços, para não ser um labirinto perfeito (e ter mais de um caminho até a saída).
        for (var i = 1; i < n; i++)
        for (var j = 0; j < n; j++)
        {
            if (_rng.Randf() < GardenMazeBraidChance) wall[2 * i, 2 * j + 1] = false;
            if (_rng.Randf() < GardenMazeBraidChance) wall[2 * j + 1, 2 * i] = false;
        }

        const int c0 = n - 2, c1 = n + 2;
        for (var i = c0; i <= c1; i++)
        for (var j = c0; j <= c1; j++)
            wall[i, j] = false;
        var exitColumn = 2 * (n / 2 + _rng.RandiRange(0, 1));
        wall[1, exitColumn] = false;

        var raw = new GardenPlan();
        for (var gy = 1; gy < s - 1; gy++)
        for (var gx = 1; gx < s - 1; gx++)
        {
            var tile = new Vector2I(gx, gy);
            raw.Tiles.Add(tile);
            if (wall[gy, gx])
            {
                raw.Blocked.Add(tile);
                raw.Props.Add(new PixelRoomProp("hedge_block_" + "abc"[(gx * 7 + gy * 3) % 3], gx, gy));
            }
            else if (gx >= c0 && gx <= c1 && gy >= c0 && gy <= c1)
                raw.Arena.Add(tile);
        }

        raw.Props.Add(new PixelRoomProp("topiary_center", n, n));
        raw.Blocked.Add(new Vector2I(n, n));
        raw.Arena.Remove(new Vector2I(n, n));
        foreach (var (lx, ly) in new[] { (c0, c0), (c1, c0), (c0, c1), (c1, c1) })
            raw.Props.Add(new PixelRoomProp("lantern", lx, ly));

        // Becos sem saída: árvore seca ou roseira (fora da clareira e longe da saída).
        foreach (var tile in raw.Tiles)
        {
            if (raw.Blocked.Contains(tile) || raw.Arena.Contains(tile) || (tile.Y <= 2 && Mathf.Abs(tile.X - exitColumn) <= 1))
                continue;
            var open = 0;
            foreach (var step in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up })
                if (raw.Tiles.Contains(tile + step) && !raw.Blocked.Contains(tile + step))
                    open++;
            if (open != 1 || _rng.Randf() >= GardenDeadEndPropChance)
                continue;
            var name = _rng.Randf() < 0.5f ? (_rng.Randf() < 0.5f ? "dead_tree_a" : "dead_tree_b") : "rose_bush";
            raw.Props.Add(new PixelRoomProp(name, tile.X, tile.Y));
        }

        raw.Entrance = new Vector2I(n, c1);
        raw.Focus = new Vector2I(n, c1 - 1);
        raw.Reward = new Vector2I(n, c0);
        raw.Exit = new Vector2I(exitColumn, 1);
        return Shifted(raw, new Vector2I(n, n));
    }

    private static GardenPlan Shifted(GardenPlan raw, Vector2I center)
    {
        var plan = new GardenPlan
        {
            Entrance = raw.Entrance - center,
            Focus = raw.Focus - center,
            Reward = raw.Reward - center,
            Exit = raw.Exit - center
        };
        foreach (var tile in raw.Tiles) plan.Tiles.Add(tile - center);
        foreach (var tile in raw.Blocked) plan.Blocked.Add(tile - center);
        foreach (var tile in raw.Arena) plan.Arena.Add(tile - center);
        foreach (var prop in raw.Props)
            plan.Props.Add(prop with { Gx = prop.Gx - center.X, Gy = prop.Gy - center.Y });
        return plan;
    }

    // Sala do jardim montada com o kit do jardim. Sala de boss do mapa 2: a arena de sempre, com blocos de sebe no lugar
    // dos pilares.
    private PixelRoomLayout BuildGardenLayout(Vector2I? exitTile, List<PixelRoomProp> bossPillars)
    {
        if (_gardenPlan != null)
            return PixelArenaLayout.BuildGarden(_gardenPlan.Tiles, _gardenPlan.Blocked, exitTile, _exitWallDirection, _gardenPlan.Props, _rng.Seed);

        var props = new List<PixelRoomProp>();
        for (var i = 0; i < bossPillars.Count; i++)
            props.Add(bossPillars[i] with { Name = "hedge_block_" + "abc"[i % 3] });
        return PixelArenaLayout.BuildGarden(CellsToPixelTiles(_walkableCells), null, exitTile, _exitWallDirection, props, _rng.Seed);
    }
}
