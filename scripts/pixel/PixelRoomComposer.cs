using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// Modo 2D: monta a imagem de uma sala a partir das peças do kit da cripta (assets/pixel_art/crypt), igual ao
// tools/pixel_art/crypt_kit/build_room.py: piso, paredes e cantos automáticos, objetos, penumbra dos corredores
// e luz das velas deslocando cada cor dentro da sua rampa da paleta AAP-64. Gera também a altura de cada pixel
// (para a profundidade no 3D) e as colisões (paredes e objetos que bloqueiam o passo).
// Mundo do kit: 1 tile = 64 unidades; tela: sx = wx - wy, sy = (wx + wy) / 2 - z.
public sealed record PixelRoomProp(string Name, int Gx, int Gy, float Dx = 0.0f, float Dy = 0.0f);

public sealed class PixelRoomLayout
{
    // Tile -> escurecimento (0 = chão normal, -1,5 meia-luz, -4 some no escuro).
    public Dictionary<Vector2I, float> Tiles { get; } = new();
    // (gx, gy, lado) -> peça: plain | niche | arch | low | gap | none. Lados: ne, nw, se, sw.
    public Dictionary<(int X, int Y, string Side), string> Edges { get; } = new();
    public List<PixelRoomProp> Props { get; } = new();
    public float Ambient { get; set; } = -0.6f;
    public int Seed { get; set; } = 3;
    // Pasta do kit em assets/pixel_art/ (crypt = cripta; garden = Jardim Sangrento, mesmas peças de parede).
    public string Kit { get; set; } = PixelRoomComposer.DefaultKit;

    public bool HasTile(int gx, int gy) => Tiles.ContainsKey(new Vector2I(gx, gy));

    // Mesmo formato dos layouts .txt do kit ([grid], [edges], [props], [settings]).
    public static PixelRoomLayout Parse(string text)
    {
        var layout = new PixelRoomLayout();
        var section = "";
        var grid = new List<string>();
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw;
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                section = trimmed.Substring(1, trimmed.Length - 2);
                continue;
            }

            if (section == "grid")
            {
                if (trimmed.StartsWith("#") && trimmed.Trim('#').Length > 0)
                    continue;
                grid.Add(line);
                continue;
            }

            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                continue;
            var parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            switch (section)
            {
                case "edges" when parts.Length >= 4:
                    layout.Edges[(int.Parse(parts[2]), int.Parse(parts[3]), parts[1])] = parts[0];
                    break;
                case "props" when parts.Length >= 3:
                    layout.Props.Add(new PixelRoomProp(parts[0], int.Parse(parts[1]), int.Parse(parts[2]),
                        parts.Length > 3 ? ParseFloat(parts[3]) : 0.0f, parts.Length > 4 ? ParseFloat(parts[4]) : 0.0f));
                    break;
                case "settings" when parts.Length >= 2:
                    if (parts[0] == "ambient") layout.Ambient = ParseFloat(parts[1]);
                    else if (parts[0] == "seed") layout.Seed = (int)ParseFloat(parts[1]);
                    else if (parts[0] == "kit") layout.Kit = parts[1];
                    break;
            }
        }

        for (var gy = 0; gy < grid.Count; gy++)
        {
            for (var gx = 0; gx < grid[gy].Length; gx++)
            {
                var dark = grid[gy][gx] switch { '.' => 0.0f, ';' => -1.5f, ',' => -4.0f, _ => float.NaN };
                if (!float.IsNaN(dark))
                    layout.Tiles[new Vector2I(gx, gy)] = dark;
            }
        }

        return layout;
    }

    private static float ParseFloat(string text) => float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class PixelRoomPicture
{
    public Image Color { get; init; }
    public Image Height { get; init; }
    // Pixel da imagem onde cai a origem do mundo do kit (wx = wy = z = 0).
    public Vector2 Origin { get; init; }
    public float HeightScale => PixelRoomComposer.HeightScale;
    // Segmentos de parede (pontas e lado de fora; a parede cresce para fora da borda) e obstáculos (centro e
    // meia-extensão), em unidades do kit.
    public List<(Vector2 A, Vector2 B, Vector2 Outward)> Walls { get; init; }
    public List<(Vector2 Center, Vector2 Half)> Blockers { get; init; }
    public Rect2 TileBounds { get; init; }
    // Peças animadas do layout (manifest "frames"): todos os quadros já com a luz da sala, para o PixelRoom trocar.
    public List<PixelRoomAnimation> Animations { get; init; } = new();
}

// Quadros (cor RGBA e altura) de uma peça animada, posicionada como no layout: tile, deslocamento e vértice de cima
// do tile dentro da peça.
public sealed class PixelRoomAnimation
{
    public string Name { get; init; }
    public int Gx { get; init; }
    public int Gy { get; init; }
    public float Dx { get; init; }
    public float Dy { get; init; }
    public Vector2I TileTop { get; init; }
    // Quadros trocados à mão por quem usa a peça (PixelRoom.SetAnimationFrame), sem o laço da sala.
    public bool Manual { get; init; }
    public List<Image> Colors { get; } = new();
    public List<Image> Heights { get; } = new();
}

public static class PixelRoomComposer
{
    public const string DefaultKit = "crypt";
    // Kit da cripta: a grade do portão e as peças avulsas (PixelRoom.AddOverlay) vêm sempre dele.
    public const string KitRoot = "res://assets/pixel_art/crypt/";
    public const string FloorRoot = "res://assets/pixel_art/tiles/dungeon_floor/dungeon_floor_128_";

    public static string KitRootOf(string kit) => $"res://assets/pixel_art/{(string.IsNullOrEmpty(kit) ? DefaultKit : kit)}/";
    public const float HeightScale = 100.0f;

    // Chão padrão (cripta); outro kit pode trazer o seu no manifest ("floor": root, variants, weights).
    private static readonly string[] FloorNames = { "base", "cracked", "blood", "missing_stone", "wet" };
    private static readonly float[] FloorWeights = { 0.5f, 0.22f, 0.05f, 0.05f, 0.18f };
    private static readonly string[] WallEdges = { "plain", "niche", "low" };

    // Meia-largura x meia-profundidade (unidades do kit; eixo "sw" = comprido em y) dos objetos que bloqueiam o passo,
    // e o deslocamento do centro do obstáculo em relação ao centro do tile.
    // Velas, arandelas, entulho e ossos são decoração: ficam de fora e o jogador atravessa.
    private static readonly (string Prefix, Vector2 Half, Vector2 Offset)[] BlockerSizes =
    {
        ("coffin_virgil", new Vector2(19, 36), Vector2.Zero), ("coffin_stone", new Vector2(13, 31), Vector2.Zero),
        ("coffin_wood", new Vector2(12, 29), Vector2.Zero), ("blood_fountain", new Vector2(46, 46), Vector2.Zero),
        ("blood_basin", new Vector2(14, 14), Vector2.Zero),
        ("weapon_bench", new Vector2(30, 64), new Vector2(-2, -23)),
        // Altar do sacrifício (estela + altar; o estrado é pisável) e bancada de artefatos (armário + balcão, de x -36
        // a 100 e y 0 a 46 do tile).
        ("sacrifice_altar", new Vector2(22, 24), new Vector2(-5, 0)),
        ("artifact_counter", new Vector2(68, 23), new Vector2(0, -9)),
        // Jardim Sangrento: bloco de sebe do labirinto (tile inteiro), canteiro 2x2 (a peça começa no tile da âncora),
        // canteiro redondo com topiaria, topiaria pequena, lanterna e árvore seca. Roseiras são decoração.
        ("hedge_block", new Vector2(32, 32), Vector2.Zero), ("parterre", new Vector2(62, 62), new Vector2(32, 32)),
        ("topiary_center", new Vector2(36, 36), Vector2.Zero), ("topiary_ball", new Vector2(11, 11), Vector2.Zero),
        ("lantern", new Vector2(5, 5), Vector2.Zero), ("dead_tree", new Vector2(7, 7), Vector2.Zero)
    };

    // Paleta AAP-64 em famílias (mesma ordem de tools/pixel_art/crypt_kit/iso.py).
    private static readonly (string Name, string[] Colors)[] Families =
    {
        ("cool", new[] { "060608", "141013", "242234", "333941", "4a5462", "6d758d", "8b93af", "b3b9d1" }),
        ("warm", new[] { "060608", "141013", "221c1a", "322b28", "423934", "5a4e44", "796755", "a08662", "c7b08b", "e4d2aa" }),
        ("earth", new[] { "060608", "141013", "221c1a", "322b28", "71413b", "8e5252" }),
        ("moss", new[] { "060608", "122020", "24523b", "23674e", "328464" }),
        ("blood", new[] { "060608", "3b1725", "5b3138", "73172d", "b4202a" }),
        ("wet", new[] { "242234", "588dbe", "849be4" }),
        ("wood", new[] { "060608", "141013", "221c1a", "422433", "5b3138", "71413b", "8e5252" }),
        ("iron", new[] { "060608", "141013", "242234", "333941", "4a5462", "6d758d" }),
        ("flame", new[] { "fa6a0a", "f9a31b", "ffd541", "fffc40", "ffffff" }),
        // Sebe do Jardim: as cores comuns já caem na "moss"; aqui entra só o verde claro do topo (5daf8d).
        ("hedge", new[] { "060608", "141013", "122020", "24523b", "23674e", "328464", "5daf8d" }),
    };

    /// <summary>Deslocamento do centro e meia-extensão (unidades do kit) do obstáculo de uma peça; zero se ela não bloqueia.</summary>
    public static (Vector2 Offset, Vector2 Half) BlockerOf(string piece)
    {
        var size = BlockerSizes.FirstOrDefault(b => piece.StartsWith(b.Prefix));
        return size.Prefix == null ? (Vector2.Zero, Vector2.Zero) : (size.Offset, size.Half);
    }

    private static readonly float[,] Bayer =
    {
        { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 }
    };

    private sealed class Piece
    {
        public int Width;
        public int Height;
        public byte[] Rgba;
        public float[] Z;
        public string Kind;
        // Vértice de cima do tile dentro da peça (o padrão do manifest, ou o da peça com tela própria).
        public int TopX;
        public int TopY;
        public int Frames = 1;
        public bool Manual;
        public List<(float X, float Y, float Power, float Radius)> Lights = new();
    }

    // Manifest de um kit (manifest.json): vértice de cima do tile, luz base assada e o chão do kit.
    private sealed class KitInfo
    {
        public string Root;
        public JsonDocument Manifest;
        public int TopX = 112;
        public int TopY = 116;
        public float BakedAmbient = -0.6f;
        public string FloorRoot = PixelRoomComposer.FloorRoot;
        public string[] FloorNames = PixelRoomComposer.FloorNames;
        public float[] FloorWeights = PixelRoomComposer.FloorWeights;
    }

    // Vértice de cima do tile dentro de cada peça do kit da cripta (manifest.json, tile_top_vertex).
    public static int TileTopX => EnsureManifest(DefaultKit) is { } kit ? kit.TopX : 112;
    public static int TileTopY => EnsureManifest(DefaultKit) is { } kit ? kit.TopY : 116;

    private static readonly Dictionary<string, KitInfo> Kits = new();
    private static readonly Dictionary<string, Piece> PieceCache = new();
    private static readonly Dictionary<string, Piece> FloorCache = new();
    private static Dictionary<int, (int Family, int Index)> _lut;
    private static byte[][][] _palettes;

    public static PixelRoomLayout LoadLayout(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PushError($"PixelRoomComposer: layout nao encontrado: {path}");
            return null;
        }

        return PixelRoomLayout.Parse(FileAccess.GetFileAsString(path));
    }

    public static PixelRoomPicture Compose(PixelRoomLayout layout)
    {
        if (layout == null || layout.Tiles.Count == 0 || EnsureManifest(layout.Kit) is not { } kit)
            return null;

        var tiles = layout.Tiles;
        bool F(int x, int y) => tiles.ContainsKey(new Vector2I(x, y));
        float Dark(int x, int y) => tiles.TryGetValue(new Vector2I(x, y), out var d) ? d : 0.0f;

        var xs = tiles.Keys.Select(t => (t.X - t.Y) * 64).ToArray();
        var ys = tiles.Keys.Select(t => (t.X + t.Y) * 32).ToArray();
        var ox = -xs.Min() + 140;
        var oy = -ys.Min() + 140;
        var width = xs.Max() - xs.Min() + 280 + 64;
        var height = ys.Max() - ys.Min() + 280 + 64;
        var rgba = new byte[width * height * 4];
        var zmap = new float[width * height];
        var darkmap = new float[width * height];

        (int X, int Y) Scr(int gx, int gy, float dx = 0, float dy = 0)
        {
            var wx = gx * 64 + dx;
            var wy = gy * 64 + dy;
            return ((int)Math.Round(wx - wy + ox), (int)Math.Round((wx + wy) / 2 + oy));
        }

        // Piso: uma variante sorteada por tile, desenhado de trás para a frente.
        var rng = new RandomNumberGenerator { Seed = (ulong)layout.Seed };
        foreach (var tile in tiles.Keys.OrderBy(t => t.X + t.Y).ThenBy(t => t.X))
        {
            var (sx, sy) = Scr(tile.X, tile.Y);
            var floor = LoadFloor(kit, kit.FloorNames[PickWeighted(rng, kit.FloorWeights)]);
            if (floor != null)
                Blit(floor, rgba, null, null, width, height, sx - 64, sy, 0.0f, false);
            var dark = tiles[tile];
            if (dark != 0.0f)
            {
                for (var yy = 0; yy < 64; yy++)
                for (var xx = 0; xx < 128; xx++)
                {
                    if (Math.Abs(xx + 0.5f - 64) / 64 + Math.Abs(yy + 0.5f - 32) / 32 > 1.05f)
                        continue;
                    var px = sx - 64 + xx;
                    var py = sy + yy;
                    if (px < 0 || py < 0 || px >= width || py >= height)
                        continue;
                    var i = py * width + px;
                    darkmap[i] = Math.Min(darkmap[i], dark);
                }
            }
        }

        // Paredes, cantos e objetos, com a mesma ordem de desenho do build_room.py.
        var items = new List<((float D, int Layer, int Gx) Order, string Name, int Gx, int Gy, float Dx, float Dy)>();
        void Put(string name, int gx, int gy, (float, int, int) order, float dx = 0, float dy = 0) => items.Add((order, name, gx, gy, dx, dy));
        bool Hides(int x, int y)
        {
            for (var x2 = x - 4; x2 <= x; x2++)
            for (var y2 = y - 4; y2 <= y; y2++)
            {
                if (!F(x2, y2) || Dark(x2, y2) <= -3) continue;
                if (Math.Abs((x2 - y2) - (x - y)) <= 1 && (x + y) - (x2 + y2) is >= 1 and <= 3)
                    return true;
            }
            return false;
        }

        foreach (var tile in tiles.Keys)
        {
            int gx = tile.X, gy = tile.Y, d = gx + gy;
            var tall = new Dictionary<string, bool>();
            foreach (var (side, nbx, nby) in new[] { ("ne", gx, gy - 1), ("nw", gx - 1, gy) })
            {
                if (!layout.Edges.TryGetValue((gx, gy, side), out var kind))
                {
                    if (F(nbx, nby)) continue;
                    kind = Hides(nbx, nby) ? "low" : "plain";
                }
                if (kind == "none") continue;
                var name = kind == "plain" ? $"walls/wall_{side}_plain_{"abc"[(int)(Hash(gx, gy, side) % 3)]}" : $"walls/wall_{side}_{kind}";
                Put(name, gx, gy, (d - 0.6f, 0, gx));
                tall[side] = kind != "low";
            }
            if (tall.GetValueOrDefault("ne") && tall.GetValueOrDefault("nw"))
                Put("walls/corner_n", gx, gy, (d - 0.7f, 0, gx));
            foreach (var (side, nbx, nby) in new[] { ("se", gx + 1, gy), ("sw", gx, gy + 1) })
            {
                if (!layout.Edges.TryGetValue((gx, gy, side), out var kind))
                {
                    if (F(nbx, nby)) continue;
                    kind = "low";
                }
                if (kind == "none") continue;
                Put($"walls/wall_{side}_{kind}", gx, gy, (d + 0.9f, 2, gx));
            }
            bool se = !F(gx + 1, gy), sw = !F(gx, gy + 1);
            if (se && sw) Put("walls/corner_s", gx, gy, (d + 0.95f, 2, gx));
            if (se && !F(gx, gy - 1) && !F(gx + 1, gy - 1)) Put(Hides(gx, gy - 1) ? "walls/corner_e_low" : "walls/corner_e", gx, gy, (d - 0.65f, 0, gx));
            if (sw && !F(gx - 1, gy) && !F(gx - 1, gy + 1)) Put(Hides(gx - 1, gy) ? "walls/corner_w" : "walls/corner_w_tall", gx, gy, (d - 0.65f, 0, gx));
        }

        foreach (var prop in layout.Props)
        {
            var piece = LoadPiece(kit, "props/" + prop.Name);
            if (piece == null) continue;
            var order = piece.Kind switch { "decal" => -0.5f, "wall_prop" => -0.55f, _ => (prop.Dx + prop.Dy) / 64.0f };
            Put("props/" + prop.Name, prop.Gx, prop.Gy, (prop.Gx + prop.Gy + order, 1, prop.Gx), prop.Dx, prop.Dy);
        }

        var lights = new List<(float X, float Y, float Power, float Radius)>();
        var animated = new List<(string Name, int Gx, int Gy, float Dx, float Dy, int X0, int Y0, float Dark)>();
        foreach (var item in items.OrderBy(i => i.Order.D).ThenBy(i => i.Order.Layer).ThenBy(i => i.Order.Gx))
        {
            var piece = LoadPiece(kit, item.Name);
            if (piece == null) continue;
            var (sx, sy) = Scr(item.Gx, item.Gy, item.Dx, item.Dy);
            Blit(piece, rgba, zmap, darkmap, width, height, sx - piece.TopX, sy - piece.TopY, Dark(item.Gx, item.Gy), true);
            foreach (var light in piece.Lights)
                lights.Add((sx - piece.TopX + light.X, sy - piece.TopY + light.Y, light.Power, light.Radius));
            if (piece.Frames > 1)
                animated.Add((item.Name, item.Gx, item.Gy, item.Dx, item.Dy, sx - piece.TopX, sy - piece.TopY, Dark(item.Gx, item.Gy)));
        }

        Relight(rgba, darkmap, width, height, lights, layout.Ambient - kit.BakedAmbient);
        var animations = animated.Select(a => ComposeFrames(kit, a.Name, a.Gx, a.Gy, a.Dx, a.Dy, a.X0, a.Y0, a.Dark, lights, layout.Ambient - kit.BakedAmbient))
            .Where(a => a != null).ToList();

        // Recorte com margem de 12 px e fundo opaco, como no build_room.py.
        int minX = width, minY = height, maxX = 0, maxY = 0;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (rgba[(y * width + x) * 4 + 3] == 0) continue;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        int bx = minX - 12, by = minY - 12, bw = maxX + 12 - bx, bh = maxY + 12 - by;
        var colorOut = new byte[bw * bh * 3];
        var heightOut = new byte[bw * bh * 3];
        for (var y = 0; y < bh; y++)
        for (var x = 0; x < bw; x++)
        {
            int sx = bx + x, sy = by + y, o = (y * bw + x) * 3;
            colorOut[o] = 6; colorOut[o + 1] = 6; colorOut[o + 2] = 8;
            if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;
            var i = sy * width + sx;
            if (rgba[i * 4 + 3] > 0)
            {
                colorOut[o] = rgba[i * 4];
                colorOut[o + 1] = rgba[i * 4 + 1];
                colorOut[o + 2] = rgba[i * 4 + 2];
            }
            var v = (int)Math.Clamp(Math.Round(zmap[i] * HeightScale), 0, 65535);
            heightOut[o] = (byte)(v >> 8);
            heightOut[o + 1] = (byte)(v & 255);
        }

        var (walls, blockers) = Collision(layout);
        var minGx = tiles.Keys.Min(t => t.X) * 64.0f;
        var minGy = tiles.Keys.Min(t => t.Y) * 64.0f;
        var maxGx = tiles.Keys.Max(t => t.X) * 64.0f + 64.0f;
        var maxGy = tiles.Keys.Max(t => t.Y) * 64.0f + 64.0f;
        return new PixelRoomPicture
        {
            Color = Image.CreateFromData(bw, bh, false, Image.Format.Rgb8, colorOut),
            Height = Image.CreateFromData(bw, bh, false, Image.Format.Rgb8, heightOut),
            Origin = new Vector2(ox - bx, oy - by),
            Walls = walls,
            Blockers = blockers,
            TileBounds = new Rect2(minGx, minGy, maxGx - minGx, maxGy - minGy),
            Animations = animations
        };
    }

    // Cada quadro da peça animada (<nome>, <nome>_1 .. _N-1) recebe a mesma luz da sala; a peça ocupa o retângulo
    // (x0, y0) da imagem da sala, então as luzes são transladadas para a peça.
    private static PixelRoomAnimation ComposeFrames(KitInfo kit, string name, int gx, int gy, float dx, float dy, int x0, int y0, float dark,
        List<(float X, float Y, float Power, float Radius)> roomLights, float ambient)
    {
        var first = LoadPiece(kit, name);
        var lights = roomLights.Select(l => (l.X - x0, l.Y - y0, l.Power, l.Radius)).ToList();
        var animation = new PixelRoomAnimation { Name = name, Gx = gx, Gy = gy, Dx = dx, Dy = dy, TileTop = new Vector2I(first.TopX, first.TopY), Manual = first.Manual };
        for (var k = 0; k < first.Frames; k++)
        {
            var piece = k == 0 ? first : LoadPiece(kit, $"{name}_{k}");
            if (piece == null || piece.Width != first.Width || piece.Height != first.Height)
            {
                GD.PushWarning($"PixelRoomComposer: quadro {k} da peca animada {name} ausente ou com outro tamanho.");
                return null;
            }

            var count = piece.Width * piece.Height;
            var rgba = new byte[count * 4];
            var zmap = new float[count];
            var darkmap = new float[count];
            Blit(piece, rgba, zmap, darkmap, piece.Width, piece.Height, 0, 0, dark, true);
            Relight(rgba, darkmap, piece.Width, piece.Height, lights, ambient);
            var heightBytes = new byte[count * 3];
            for (var i = 0; i < count; i++)
            {
                var v = (int)Math.Clamp(Math.Round(zmap[i] * HeightScale), 0, 65535);
                heightBytes[i * 3] = (byte)(v >> 8);
                heightBytes[i * 3 + 1] = (byte)(v & 255);
            }
            animation.Colors.Add(Image.CreateFromData(piece.Width, piece.Height, false, Image.Format.Rgba8, rgba));
            animation.Heights.Add(Image.CreateFromData(piece.Width, piece.Height, false, Image.Format.Rgb8, heightBytes));
        }
        return animation;
    }

    // Paredes = bordas de tile sem vizinho ou fechadas no layout.
    private static (List<(Vector2, Vector2, Vector2)>, List<(Vector2, Vector2)>) Collision(PixelRoomLayout layout)
    {
        var walls = new List<(Vector2, Vector2, Vector2)>();
        foreach (var tile in layout.Tiles.Keys.OrderBy(t => t.X).ThenBy(t => t.Y))
        {
            int gx = tile.X, gy = tile.Y;
            float x0 = gx * 64, y0 = gy * 64, x1 = x0 + 64, y1 = y0 + 64;
            foreach (var (side, nbx, nby, seg) in new[]
            {
                ("ne", gx, gy - 1, (new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(0, -1))),
                ("nw", gx - 1, gy, (new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(-1, 0))),
                ("se", gx + 1, gy, (new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(1, 0))),
                ("sw", gx, gy + 1, (new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(0, 1)))
            })
            {
                var hasKind = layout.Edges.TryGetValue((gx, gy, side), out var kind);
                var closed = hasKind && WallEdges.Contains(kind);
                if (layout.HasTile(nbx, nby) && !closed)
                    continue;
                if (hasKind && kind is "arch" or "gap")
                    AddDoorPocket(walls, seg.Item1, seg.Item2, seg.Item3);
                else
                    walls.Add(seg);
            }
        }

        var blockers = new List<(Vector2, Vector2)>();
        foreach (var prop in layout.Props)
        {
            var size = BlockerSizes.FirstOrDefault(b => prop.Name.StartsWith(b.Prefix));
            if (size.Prefix == null) continue;
            var half = prop.Name.EndsWith("_se") ? new Vector2(size.Half.Y, size.Half.X) : size.Half;
            blockers.Add((new Vector2(prop.Gx * 64 + prop.Dx + 32, prop.Gy * 64 + prop.Dy + 32) + size.Offset, half));
        }

        return (walls, blockers);
    }

    // Vão de porta (arco ou vão sem parede vizinha atrás): a parede fica só nos batentes e o vão vira um nicho de
    // DoorPocketDepth unidades para fora, para o corpo entrar no portão antes de atravessar (ScenePortal.PlayerInDoorway).
    public const float DoorJamb = 12.0f;
    public const float DoorPocketDepth = 30.0f;

    private static void AddDoorPocket(List<(Vector2, Vector2, Vector2)> walls, Vector2 a, Vector2 b, Vector2 outward)
    {
        var along = (b - a) / 64.0f;
        var left = a + along * DoorJamb;
        var right = b - along * DoorJamb;
        var depth = outward * DoorPocketDepth;
        walls.Add((a, left, outward));
        walls.Add((right, b, outward));
        walls.Add((left, left + depth, -along));
        walls.Add((right, right + depth, along));
        walls.Add((left + depth, right + depth, outward));
    }

    private static void Relight(byte[] rgba, float[] darkmap, int width, int height, List<(float X, float Y, float Power, float Radius)> lights, float ambient)
    {
        EnsurePalette();
        var glow = new float[width * height];
        foreach (var (lx, ly, power, radius) in lights)
        {
            var r = radius * 1.25f;
            var reach = (int)(r * 7);
            int x0 = Math.Max((int)lx - reach, 0), x1 = Math.Min((int)lx + reach, width);
            int y0 = Math.Max((int)ly - reach / 2, 0), y1 = Math.Min((int)ly + reach / 2, height);
            for (var y = y0; y < y1; y++)
            {
                var dy = (y - ly) * 1.7f;
                for (var x = x0; x < x1; x++)
                {
                    var dx = x - lx;
                    glow[y * width + x] += power * MathF.Exp(-MathF.Sqrt(dx * dx + dy * dy) / r);
                }
            }
        }

        var warmFamily = 1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var i = y * width + x;
            if (rgba[i * 4 + 3] == 0) continue;
            var key = (rgba[i * 4] << 16) | (rgba[i * 4 + 1] << 8) | rgba[i * 4 + 2];
            if (!_lut.TryGetValue(key, out var entry)) continue;
            var family = Families[entry.Family].Name;
            if (family == "flame") continue;
            var g = 2.3f * (1 - MathF.Exp(-glow[i] / 1.7f));
            var dith = Bayer[y % 4, x % 4] / 16.0f - 0.5f;
            var light = ambient + g + darkmap[i] * (1 - Math.Clamp(g / 2, 0, 1));
            var warm = g + dith * 1.1f > 0.95f;
            var index = (int)Math.Round(entry.Index + light + dith * 0.85f);
            var palette = _palettes[entry.Family];
            if ((family == "cool" || family == "iron") && warm)
                palette = _palettes[warmFamily];
            var c = palette[Math.Clamp(index, 0, palette.Length - 1)];
            rgba[i * 4] = c[0];
            rgba[i * 4 + 1] = c[1];
            rgba[i * 4 + 2] = c[2];
        }
    }

    // Copia os pixels opacos da peça; com zmap/darkmap a peça também grava a altura e herda a penumbra do tile.
    private static void Blit(Piece piece, byte[] rgba, float[] zmap, float[] darkmap, int width, int height, int x0, int y0, float dark, bool depth)
    {
        for (var y = 0; y < piece.Height; y++)
        {
            var ty = y0 + y;
            if (ty < 0 || ty >= height) continue;
            for (var x = 0; x < piece.Width; x++)
            {
                var tx = x0 + x;
                if (tx < 0 || tx >= width) continue;
                var s = (y * piece.Width + x) * 4;
                if (piece.Rgba[s + 3] == 0) continue;
                var t = ty * width + tx;
                rgba[t * 4] = piece.Rgba[s];
                rgba[t * 4 + 1] = piece.Rgba[s + 1];
                rgba[t * 4 + 2] = piece.Rgba[s + 2];
                rgba[t * 4 + 3] = 255;
                if (!depth) continue;
                darkmap[t] = dark;
                zmap[t] = piece.Z != null ? piece.Z[y * piece.Width + x] : 0.0f;
            }
        }
    }

    private static int PickWeighted(RandomNumberGenerator rng, float[] weights)
    {
        var roll = rng.Randf();
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0) return i;
        }
        return 0;
    }

    private static ulong Hash(int gx, int gy, string side)
    {
        // Variação estável das paredes lisas (a, b, c) por posição.
        var h = 1469598103934665603UL;
        foreach (var c in $"{gx},{gy},{side}")
            h = (h ^ c) * 1099511628211UL;
        return h;
    }

    private static KitInfo EnsureManifest(string kitName)
    {
        kitName = string.IsNullOrEmpty(kitName) ? DefaultKit : kitName;
        lock (Kits)
        {
            if (Kits.TryGetValue(kitName, out var cached)) return cached;
            var root = KitRootOf(kitName);
            var path = root + "manifest.json";
            if (!FileAccess.FileExists(path))
            {
                GD.PushError($"PixelRoomComposer: manifest do kit nao encontrado: {path}");
                return null;
            }
            try
            {
                var manifest = JsonDocument.Parse(FileAccess.GetFileAsString(path));
                var json = manifest.RootElement;
                var kit = new KitInfo
                {
                    Root = root,
                    Manifest = manifest,
                    TopX = json.GetProperty("tile_top_vertex")[0].GetInt32(),
                    TopY = json.GetProperty("tile_top_vertex")[1].GetInt32(),
                    BakedAmbient = json.GetProperty("baked_ambient").GetSingle()
                };
                if (json.TryGetProperty("floor", out var floor))
                {
                    kit.FloorRoot = floor.GetProperty("root").GetString();
                    kit.FloorNames = floor.GetProperty("variants").EnumerateArray().Select(v => v.GetString()).ToArray();
                    kit.FloorWeights = floor.GetProperty("weights").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                }
                Kits[kitName] = kit;
                return kit;
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                GD.PushError($"PixelRoomComposer: manifest invalido ({kitName}): {exception.Message}");
                return null;
            }
        }
    }

    private static Piece LoadPiece(KitInfo kit, string name)
    {
        var key = kit.Root + name;
        lock (PieceCache)
        {
            if (PieceCache.TryGetValue(key, out var cached)) return cached;
        }
        Piece piece = null;
        if (kit.Manifest.RootElement.GetProperty("pieces").TryGetProperty(name, out var info))
        {
            var image = LoadImage(kit.Root + name + ".png");
            var heights = LoadImage(kit.Root + "heights/" + name + ".png");
            if (image != null)
            {
                piece = new Piece { Width = image.GetWidth(), Height = image.GetHeight(), Rgba = image.GetData(), Kind = info.GetProperty("kind").GetString(), TopX = kit.TopX, TopY = kit.TopY };
                if (info.TryGetProperty("tile_top_vertex", out var top))
                {
                    piece.TopX = top[0].GetInt32();
                    piece.TopY = top[1].GetInt32();
                }
                if (info.TryGetProperty("frames", out var frames))
                    piece.Frames = Math.Max(frames.GetInt32(), 1);
                if (info.TryGetProperty("manual", out var manual))
                    piece.Manual = manual.GetBoolean();
                if (heights != null && heights.GetWidth() == piece.Width && heights.GetHeight() == piece.Height)
                {
                    heights.Convert(Image.Format.Rgb8);
                    var data = heights.GetData();
                    piece.Z = new float[piece.Width * piece.Height];
                    for (var i = 0; i < piece.Z.Length; i++)
                        piece.Z[i] = ((data[i * 3] << 8) | data[i * 3 + 1]) / HeightScale;
                }
                foreach (var light in info.GetProperty("lights").EnumerateArray())
                    piece.Lights.Add((light.GetProperty("x").GetSingle(), light.GetProperty("y").GetSingle(), light.GetProperty("power").GetSingle(), light.GetProperty("radius").GetSingle()));
            }
        }
        else
        {
            GD.PushWarning($"PixelRoomComposer: peca fora do kit: {kit.Root}{name}");
        }
        lock (PieceCache)
            PieceCache[key] = piece;
        return piece;
    }

    private static Piece LoadFloor(KitInfo kit, string variant)
    {
        var key = kit.FloorRoot + variant;
        lock (FloorCache)
        {
            if (FloorCache.TryGetValue(key, out var cached)) return cached;
        }
        var image = LoadImage(key + "_flat.png");
        var piece = image == null ? null : new Piece { Width = image.GetWidth(), Height = image.GetHeight(), Rgba = image.GetData() };
        lock (FloorCache)
            FloorCache[key] = piece;
        return piece;
    }

    // PNG lido direto do disco (sem compressão de importação); no jogo exportado cai para o recurso importado.
    public static Image LoadImage(string path)
    {
        Image image = null;
        var absolute = ProjectSettings.GlobalizePath(path);
        if (System.IO.File.Exists(absolute))
            image = Image.LoadFromFile(absolute);
        else if (ResourceLoader.Exists(path))
            image = ResourceLoader.Load<Texture2D>(path)?.GetImage();
        if (image == null || image.IsEmpty())
        {
            GD.PushWarning($"PixelRoomComposer: imagem nao carregada: {path}");
            return null;
        }
        if (image.IsCompressed())
            image.Decompress();
        image.Convert(Image.Format.Rgba8);
        return image;
    }

    private static void EnsurePalette()
    {
        if (_lut != null) return;
        _lut = new Dictionary<int, (int, int)>();
        _palettes = new byte[Families.Length][][];
        for (var f = 0; f < Families.Length; f++)
        {
            var colors = Families[f].Colors;
            _palettes[f] = new byte[colors.Length][];
            for (var i = 0; i < colors.Length; i++)
            {
                var hex = colors[i];
                var c = new[] { Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16) };
                _palettes[f][i] = c;
                _lut.TryAdd((c[0] << 16) | (c[1] << 8) | c[2], (f, i));
            }
        }
    }
}
