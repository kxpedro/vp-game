using Godot;
using System.Collections.Generic;

// Modo 2D: entrada da sala por um portão. Uma borda da sala perto da entrada vira um vão (arco com grade nas paredes do
// fundo, vão aberto nas da frente) e o jogador nasce dentro dele e sai andando para a sala; a grade desce atrás dele.
// Assim a troca de sala (RoomTransition) lê como atravessar de um portão para outro, em vez de aparecer no meio da sala.
public partial class RoomGenerator
{
    // Quanto o jogador nasce para dentro do nicho (o nicho tem PixelRoomComposer.DoorPocketDepth unidades).
    private const float EntryStartDepth = 0.8f;
    // Quanto ele anda para dentro da sala depois da face da parede.
    private const float EntryWalkOut = 1.3f;

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

    // Depois do PixelRoom.Build: grade aberta no vão (só no arco), jogador dentro do nicho e os passos para fora.
    private void PlacePlayerAtEntryDoor()
    {
        if (_entryEdge is not { } entry || _pixelRoom == null || GetParent().GetNodeOrNull<PlayerController>("Player") is not { } player)
            return;

        var door = EntryEdgeTransform(entry.Tile, entry.Side);
        var inward = door.Basis.Z;
        var root = new Node3D { Name = "EntryDoor", Transform = door };
        AddChild(root);

        PortcullisGate gate = null;
        CollisionShape3D blocker = null;
        if (entry.Side is "ne" or "nw")
        {
            gate = new PortcullisGate { Name = ScenePortal.GateName, OpeningWidth = 40.0f * PixelRoom.UnitMeters, BlocksPassage = true };
            gate.ConfigurePixel(_pixelRoom, entry.Tile.X, entry.Tile.Y, entry.Side);
            gate.SetOpenAmount(1.0f);
            root.AddChild(gate);
        }
        else
        {
            // Vão da frente não tem grade no kit: só um bloqueio invisível depois que o jogador sai.
            var body = new StaticBody3D { Name = "EntryBlocker" };
            blocker = new CollisionShape3D
            {
                Position = new Vector3(0.0f, 1.5f, -0.15f),
                Shape = new BoxShape3D { Size = new Vector3(40.0f * PixelRoom.UnitMeters + 0.3f, 3.0f, 0.3f) },
                Disabled = true
            };
            body.AddChild(blocker);
            root.AddChild(body);
        }

        var start = door.Origin - inward * EntryStartDepth;
        player.GlobalPosition = new Vector3(start.X, PlayerSpawnPosition.Y, start.Z);
        PlayerSpawnPosition = player.GlobalPosition;
        player.LookAt(player.GlobalPosition + inward, Vector3.Up);

        var seconds = (EntryStartDepth + EntryWalkOut) / Mathf.Max(player.WalkSpeed, 0.5f);
        Callable.From(() =>
        {
            if (IsInstanceValid(player))
                player.StartScriptedWalk(inward, seconds);
        }).CallDeferred();

        var timer = GetTree().CreateTimer(seconds + 0.1f, false);
        timer.Timeout += () =>
        {
            if (gate != null && IsInstanceValid(gate))
                gate.MoveTo(0.0f, 0.6f);
            if (blocker != null && IsInstanceValid(blocker))
                blocker.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        };
    }

    // Base do vão na face da parede, no meio da borda, com +Z para dentro da sala (como ScenePortal.WallBasis).
    private Transform3D EntryEdgeTransform(Vector2I tile, string side)
    {
        if (side is "ne" or "nw")
            return PortcullisGate.PixelWallTransform(_pixelRoom, tile.X, tile.Y, side);
        float x0 = tile.X * 64.0f, y0 = tile.Y * 64.0f;
        var (mid, inwardKit) = side == "se"
            ? (new Vector2(x0 + 64.0f, y0 + 32.0f), new Vector2(-1.0f, 0.0f))
            : (new Vector2(x0 + 32.0f, y0 + 64.0f), new Vector2(0.0f, -1.0f));
        var origin = _pixelRoom.KitToWorld(mid.X, mid.Y);
        var inward = _pixelRoom.KitToWorld(mid.X + inwardKit.X, mid.Y + inwardKit.Y) - origin;
        return new Transform3D(ScenePortal.WallBasis(inward), origin);
    }
}
