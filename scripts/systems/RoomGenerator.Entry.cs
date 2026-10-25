using Godot;
using System.Collections.Generic;

// Modo 2D: entrada da sala por um portão. Uma borda da sala perto da entrada vira um vão (arco nas paredes do fundo,
// vão das paredes cortadas nas da frente, os dois com grade) e o jogador nasce dentro dele e sai andando para a sala; a
// grade desce atrás dele (PixelEntryDoor).
// Assim a troca de sala (RoomTransition) lê como atravessar de um portão para outro, em vez de aparecer no meio da sala.
public partial class RoomGenerator
{
    private (Vector2I Tile, string Side)? _entryEdge;

    // Escolhe a borda (antes do PixelRoom.Build) e abre o vão no layout. Fora o Jardim (kit sem arco/vão próprios).
    private void PrepareEntryDoor(PixelRoomLayout layout, Vector2I? exitTile)
    {
        _entryEdge = null;
        if (UsesGardenKit)
            return;

        var entranceTile = CellToPixelTile(_entranceCell);
        var bestScore = float.PositiveInfinity;
        foreach (var tile in layout.Tiles.Keys)
        {
            if (exitTile is { } exit && Mathf.Abs(tile.X - exit.X) + Mathf.Abs(tile.Y - exit.Y) <= 1)
                continue;
            foreach (var (side, outward) in new[] { ("ne", Vector2I.Up), ("nw", Vector2I.Left), ("se", Vector2I.Right), ("sw", Vector2I.Down) })
            {
                if (layout.HasTile(tile.X + outward.X, tile.Y + outward.Y) || layout.Edges.ContainsKey((tile.X, tile.Y, side)))
                    continue;
                // Parede reta: os dois vizinhos ao longo dela também são borda do mesmo lado (sem canto colado no vão).
                var along = new Vector2I(Mathf.Abs(outward.Y), Mathf.Abs(outward.X));
                if (!IsStraightWall(layout, tile + along, outward) || !IsStraightWall(layout, tile - along, outward))
                    continue;
                var delta = tile - entranceTile;
                // Paredes do fundo (arco com grade) levam uma pequena preferência.
                var score = delta.X * delta.X + delta.Y * delta.Y + (side is "ne" or "nw" ? 0.0f : 0.5f);
                if (score >= bestScore)
                    continue;
                bestScore = score;
                _entryEdge = (tile, side);
            }
        }

        if (_entryEdge is not { } entry)
            return;
        layout.Edges[(entry.Tile.X, entry.Tile.Y, entry.Side)] = entry.Side is "ne" or "nw" ? "arch" : "gap";
        // Peças de parede ou de canto no tile do vão atrapalhariam o arco e o caminho de saída.
        layout.Props.RemoveAll(prop => prop.Gx == entry.Tile.X && prop.Gy == entry.Tile.Y);
    }

    private static bool IsStraightWall(PixelRoomLayout layout, Vector2I tile, Vector2I outward)
    {
        return layout.HasTile(tile.X, tile.Y) && !layout.HasTile(tile.X + outward.X, tile.Y + outward.Y);
    }

    // Depois do PixelRoom.Build: grade aberta no vão, jogador dentro do nicho e os passos para fora (PixelEntryDoor).
    private void PlacePlayerAtEntryDoor()
    {
        if (_entryEdge is not { } entry || _pixelRoom == null || GetParent().GetNodeOrNull<PlayerController>("Player") is not { } player)
            return;
        PlayerSpawnPosition = PixelEntryDoor.Place(this, _pixelRoom, entry.Tile, entry.Side, player, PlayerSpawnPosition.Y);
    }
}
