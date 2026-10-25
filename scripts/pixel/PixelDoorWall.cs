using Godot;

// Modo 2D: a parede de saída de uma sala, onde ficam os arcos das portas. Uma porta usa o arco do tile do meio; duas
// usam os tiles vizinhos dele (um de cada lado, com o do meio liso entre os dois); três usam os três tiles. O arco é uma borda "arch" do layout,
// então trocar o número de portas redesenha a sala (PixelRoom.RecomposeAsync). Só paredes do fundo (ne e nw), que
// são as que têm arco e grade no kit.
public sealed class PixelDoorWall
{
    public PixelRoom Room { get; }
    public PixelRoomLayout Layout { get; }
    public Vector2I Center { get; }
    public string Side { get; }
    public int DoorCount { get; private set; }

    // Direção (em tiles) da esquerda para a direita de quem olha a parede de dentro da sala: +X local de
    // ScenePortal.WallBasis. A porta 0 de um conjunto fica do lado de -Along.
    private Vector2I Along => Side == "nw" ? new Vector2I(0, -1) : new Vector2I(1, 0);
    private Vector2I Outward => Side == "nw" ? Vector2I.Left : Vector2I.Up;

    public PixelDoorWall(PixelRoom room, PixelRoomLayout layout, Vector2I center, string side)
    {
        Room = room;
        Layout = layout;
        Center = center;
        Side = side == "nw" ? "nw" : "ne";
    }

    /// <summary>Lado da saída para uma direção para fora da sala no grid; null para as paredes da frente (sem arco).</summary>
    public static string SideForOutward(Vector2I outward)
    {
        if (outward == Vector2I.Up) return "ne";
        if (outward == Vector2I.Left) return "nw";
        return null;
    }

    /// <summary>True se a parede comporta duas portas: os dois vizinhos do meio são chão com parede atrás.</summary>
    public bool FitsTwoDoors => IsWallTile(Center - Along) && IsWallTile(Center + Along);

    /// <summary>True se a parede comporta três portas: o meio e os dois vizinhos são chão com parede atrás.</summary>
    public bool FitsThreeDoors => FitsTwoDoors && IsWallTile(Center);

    /// <summary>Tile do arco da porta <paramref name="index"/> num conjunto de <paramref name="count"/> portas.</summary>
    public Vector2I DoorTile(int index, int count)
    {
        if (count < 2 || !FitsTwoDoors)
            return Center;
        if (count >= 3 && FitsThreeDoors)
            return Center + Along * (Mathf.Clamp(index, 0, 2) - 1);
        return index == 0 ? Center - Along : Center + Along;
    }

    /// <summary>Base da porta <paramref name="index"/>: na face da parede, no meio do arco, virada para a sala.</summary>
    public Transform3D DoorTransform(int index, int count)
    {
        var tile = DoorTile(index, count);
        return PortcullisGate.PixelWallTransform(Room, tile.X, tile.Y, Side);
    }

    /// <summary>
    /// Deixa a parede com os arcos de <paramref name="count"/> portas. Antes do PixelRoom.Build só muda o layout;
    /// depois, redesenha a sala se algo mudou.
    /// </summary>
    public void SetDoorCount(int count)
    {
        count = count >= 3 && FitsThreeDoors ? 3 : count >= 2 && FitsTwoDoors ? 2 : 1;
        if (count == DoorCount)
            return;
        DoorCount = count;
        foreach (var offset in new[] { -1, 0, 1 })
        {
            var tile = Center + Along * offset;
            if (Layout.Edges.TryGetValue((tile.X, tile.Y, Side), out var kind) && kind == "arch")
                Layout.Edges.Remove((tile.X, tile.Y, Side));
        }
        for (var i = 0; i < count; i++)
        {
            var tile = DoorTile(i, count);
            Layout.Edges[(tile.X, tile.Y, Side)] = "arch";
        }
        if (Room?.Layout == Layout && Room.Picture != null)
            Room.RecomposeAsync();
    }

    private bool IsWallTile(Vector2I tile) => Layout.HasTile(tile.X, tile.Y) && !Layout.HasTile(tile.X + Outward.X, tile.Y + Outward.Y);
}
