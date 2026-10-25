using Godot;

// Modo 2D: entrada da sala por um portão. O vão (arco nas paredes do fundo, vão das paredes cortadas da frente) já está
// no layout; aqui entra a grade aberta, o jogador nasce dentro do nicho do vão (PixelRoomComposer.AddDoorPocket) e sai
// andando para a sala, e a grade desce atrás dele. Usado pelas salas da run (RoomGenerator.Entry) e pelas salas fixas
// com vão de entrada (PixelSceneSetup).
public static class PixelEntryDoor
{
    // Quanto o jogador nasce para dentro do nicho (o nicho tem PixelRoomComposer.DoorPocketDepth unidades).
    private const float StartDepth = 0.8f;
    // Quanto ele anda para dentro da sala depois da face da parede.
    private const float WalkOut = 1.3f;
    private const float CloseSeconds = 0.6f;

    /// <summary>
    /// Monta o portão de entrada na borda <paramref name="side"/> do tile <paramref name="tile"/> (filho de
    /// <paramref name="parent"/>), põe o jogador dentro dele na altura <paramref name="spawnY"/> e, depois de
    /// <paramref name="delay"/> s, faz ele sair andando. Devolve a posição onde o jogador nasceu.
    /// </summary>
    public static Vector3 Place(Node parent, PixelRoom room, Vector2I tile, string side, PlayerController player, float spawnY, float delay = 0.0f)
    {
        var door = PortcullisGate.PixelWallTransform(room, tile.X, tile.Y, side);
        var inward = door.Basis.Z;
        var root = new Node3D { Name = "EntryDoor", Transform = door };
        parent.AddChild(root);

        var front = side is "se" or "sw";
        var gate = new PortcullisGate
        {
            Name = ScenePortal.GateName,
            // Vão do kit (walls.py): arco 40 unidades, vão da frente 44.
            OpeningWidth = (front ? 44.0f : 40.0f) * PixelRoom.UnitMeters,
            BlocksPassage = true
        };
        gate.ConfigurePixel(room, tile.X, tile.Y, side);
        gate.SetOpenAmount(1.0f);
        root.AddChild(gate);

        var start = door.Origin - inward * StartDepth;
        player.GlobalPosition = new Vector3(start.X, spawnY, start.Z);
        player.LookAt(player.GlobalPosition + inward, Vector3.Up);

        var seconds = (StartDepth + WalkOut) / Mathf.Max(player.WalkSpeed, 0.5f);
        var tree = parent.GetTree();
        void Walk()
        {
            if (!GodotObject.IsInstanceValid(player))
                return;
            player.StartScriptedWalk(inward, seconds);
            tree.CreateTimer(seconds + 0.1f, false).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(gate))
                    gate.MoveTo(0.0f, CloseSeconds);
            };
        }

        if (delay > 0.0f)
            tree.CreateTimer(delay, false).Timeout += Walk;
        else
            Callable.From(Walk).CallDeferred();
        return player.GlobalPosition;
    }
}
