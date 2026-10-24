"""Monta uma sala isométrica a partir de um layout em texto, usando só as peças exportadas do kit.

Uso: py tools/pixel_art/crypt_kit/build_room.py <layout.txt> <saida.png> [--sem-luz] [--godot]

Com --godot grava também, ao lado da saída, <saida>_altura.png (altura de cada pixel, 16 bits em R e G, x100)
e <saida>.json (origem do mundo na imagem, tiles, paredes e obstáculos em unidades do mundo) para o jogo.

Formato do layout (linhas que começam com # são comentários):
  [grid]            linhas = gy, colunas = gx
     .  chão         ,  chão que some no escuro (corredor)       ;  chão na meia-luz
     (espaço ou #) vazio
  [edges]           sobrescreve uma borda:   <peça> <lado> <gx> <gy>
                     peça: plain | niche | arch | low | gap | none     lado: ne | nw | se | sw
  [props]           <peça_do_kit> <gx> <gy> [dx dy]   (dx, dy em unidades do mundo; 64 = 1 tile)
  [settings]        ambient -0.6   seed 3
As paredes saem sozinhas: borda NE/NW sem chão vizinho = parede alta (ou baixa se tapar chão atrás);
borda SE/SW = parede cortada da frente. Cantos são colocados automaticamente.
"""
import os, sys, json, hashlib
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso import FAM, FNAMES, C, BAYER

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
KIT = os.path.join(ROOT, "assets", "pixel_art", "crypt")
FLOOR = os.path.join(ROOT, "assets", "pixel_art", "tiles", "dungeon_floor", "dungeon_floor_128_")
FLOOR_VARIANTS = (["base", "cracked", "blood", "missing_stone", "wet"], [.5, .22, .05, .05, .18])

def parse(path):
    sec, grid, edges, props, st = None, [], {}, [], {"ambient": -.6, "seed": 3}
    for raw in open(path, encoding="utf-8"):
        line = raw.rstrip("\n")
        if line.strip().startswith("#") and sec != "grid": continue
        if line.strip().startswith("[") and line.strip().endswith("]"): sec = line.strip()[1:-1]; continue
        if sec == "grid":
            if line.strip().startswith("#") and set(line.strip()) - set("#"): continue
            grid.append(line)
        elif sec == "edges" and line.strip():
            p, side, gx, gy = line.split()[:4]; edges[(int(gx), int(gy), side)] = p
        elif sec == "props" and line.strip():
            t = line.split(); props.append((t[0], int(t[1]), int(t[2]), float(t[3]) if len(t) > 3 else 0., float(t[4]) if len(t) > 4 else 0.))
        elif sec == "settings" and line.strip():
            k, v = line.split()[:2]; st[k] = float(v)
    while grid and not grid[-1].strip(): grid.pop()
    tiles = {}
    for gy, row in enumerate(grid):
        for gx, ch in enumerate(row):
            if ch in ".,;": tiles[(gx, gy)] = {".": 0., ";": -1.5, ",": -4.}[ch]
    return tiles, edges, props, st

_HEIGHTS = {}
def piece_height(name):
    """altura (z do mundo) de cada pixel da peça, refazendo o desenho dela; NaN onde a peça é transparente"""
    if name not in _HEIGHTS:
        from iso import Canvas
        import export_kit as EK
        if not hasattr(piece_height, "defs"): piece_height.defs = EK.pieces()
        _, fn, *canvas = piece_height.defs[name]
        cw, ch, tx, ty = canvas[0] if canvas else (EK.CW, EK.CH, EK.TX, EK.TY)
        cv = Canvas(cw, ch, tx, ty)
        fn(cv, [])
        _HEIGHTS[name] = np.where(cv.fam >= 0, cv.wz, np.nan)
    return _HEIGHTS[name]

# meia-largura x meia-profundidade (unidades do mundo, eixo "sw" = comprido em y) dos objetos que bloqueiam o passo
BLOCKERS = {"coffin_virgil": (19, 36), "coffin_stone": (13, 31), "coffin_wood": (12, 29),
            "candles_large": (13, 13), "candles_medium": (9, 9), "candles_small": (6, 6), "candle_tall": (4, 4),
            "blood_fountain": (46, 46), "blood_basin": (14, 14), "weapon_bench": (30, 64)}
# centro do obstáculo fora do centro do tile (unidades do mundo)
BLOCKER_OFFSETS = {"weapon_bench": (-2, -23)}
WALL_EDGES = ("plain", "niche", "low")

def collision(tiles, edges, props):
    """paredes = bordas de tile sem vizinho ou fechadas no layout; segmentos (x0, y0, x1, y1) em unidades do mundo"""
    walls = []
    for (gx, gy) in sorted(tiles):
        x0, y0, x1, y1 = gx * 64, gy * 64, gx * 64 + 64, gy * 64 + 64
        for side, nb, seg in (("ne", (gx, gy - 1), (x0, y0, x1, y0)), ("nw", (gx - 1, gy), (x0, y0, x0, y1)),
                              ("se", (gx + 1, gy), (x1, y0, x1, y1)), ("sw", (gx, gy + 1), (x0, y1, x1, y1))):
            if nb not in tiles or edges.get((gx, gy, side)) in WALL_EDGES: walls.append(seg)
    blockers = []
    for (name, gx, gy, dx, dy) in props:
        key = next((k for k in BLOCKERS if name.startswith(k)), None)
        if key is None: continue
        hx, hy = BLOCKERS[key]
        if name.endswith("_se"): hx, hy = hy, hx
        bx, by = BLOCKER_OFFSETS.get(key, (0, 0))
        blockers.append((gx * 64 + dx + 32 + bx, gy * 64 + dy + 32 + by, hx, hy, name))
    return walls, blockers

def build(layout, out, lit=True, godot=False):
    tiles, edges, props, st = parse(layout)
    man = json.load(open(os.path.join(KIT, "manifest.json"), encoding="utf-8"))
    CW, CH = man["canvas"]; TX, TY = man["tile_top_vertex"]
    cache = {}
    def piece(name):
        if name not in cache: cache[name] = Image.open(os.path.join(KIT, name + ".png")).convert("RGBA")
        return cache[name]
    def h(*a): return int(hashlib.md5(repr(a).encode()).hexdigest(), 16)
    xs = [(x - y) * 64 for x, y in tiles]; ys = [(x + y) * 32 for x, y in tiles]
    ox, oy = -min(xs) + 140, -min(ys) + 140
    Wd, Hd = max(xs) - min(xs) + 280 + 64, max(ys) - min(ys) + 280 + 64
    img = Image.new("RGBA", (Wd, Hd), (0, 0, 0, 0))
    darkmap = np.zeros((Hd, Wd))
    height = np.zeros((Hd, Wd), np.float32)
    def scr(gx, gy, dx=0., dy=0.):
        wx, wy = gx * 64 + dx, gy * 64 + dy
        return int(round(wx - wy + ox)), int(round((wx + wy) / 2 + oy))
    items, lights = [], []
    def put(name, gx, gy, order, dx=0., dy=0.):
        items.append((order, name, gx, gy, dx, dy))
    rng = np.random.default_rng(int(st["seed"]))
    names, probs = FLOOR_VARIANTS
    flat = {n: Image.open(FLOOR + n + "_flat.png").convert("RGBA") for n in names}
    for (gx, gy) in sorted(tiles, key=lambda t: (t[0] + t[1], t[0])):
        sx, sy = scr(gx, gy)
        img.alpha_composite(flat[names[rng.choice(len(names), p=probs)]], (sx - 64, sy))
        if tiles[(gx, gy)]:
            m = np.zeros((Hd, Wd), bool)
            yy, xx = np.mgrid[0:64, 0:128]
            dia = np.abs(xx + .5 - 64) / 64 + np.abs(yy + .5 - 32) / 32 <= 1.05
            darkmap[sy:sy + 64, sx - 64:sx + 64][dia] = np.minimum(darkmap[sy:sy + 64, sx - 64:sx + 64][dia], tiles[(gx, gy)])
    F = lambda x, y: (x, y) in tiles
    def hides(x, y):
        return any(F(x2, y2) and tiles[(x2, y2)] > -3 for x2 in range(x - 4, x + 1) for y2 in range(y - 4, y + 1)
                   if abs((x2 - y2) - (x - y)) <= 1 and 1 <= (x + y) - (x2 + y2) <= 3)
    for (gx, gy) in tiles:
        d = gx + gy
        tall = {}
        for side, nb, blocked in (("ne", (gx, gy - 1), hides(gx, gy - 1)), ("nw", (gx - 1, gy), hides(gx - 1, gy))):
            kind = edges.get((gx, gy, side))
            if kind is None:
                if F(*nb): continue
                kind = "low" if blocked else "plain"
            if kind == "none": continue
            nm = f"walls/wall_{side}_plain_{'abc'[h(gx, gy, side) % 3]}" if kind == "plain" else f"walls/wall_{side}_{kind}"
            put(nm, gx, gy, (d - .6, 0, gx)); tall[side] = kind != "low"
        if tall.get("ne") and tall.get("nw"): put("walls/corner_n", gx, gy, (d - .7, 0, gx))
        for side, nb in (("se", (gx + 1, gy)), ("sw", (gx, gy + 1))):
            kind = edges.get((gx, gy, side))
            if kind is None:
                if F(*nb): continue
                kind = "low"
            if kind == "none": continue
            put(f"walls/wall_{side}_{kind}", gx, gy, (d + .9, 2, gx))
        se, sw = not F(gx + 1, gy), not F(gx, gy + 1)
        if se and sw: put("walls/corner_s", gx, gy, (d + .95, 2, gx))
        if se and not F(gx, gy - 1) and not F(gx + 1, gy - 1): put("walls/corner_e" if not hides(gx, gy - 1) else "walls/corner_e_low", gx, gy, (d - .65, 0, gx))
        if sw and not F(gx - 1, gy) and not F(gx - 1, gy + 1): put("walls/corner_w" if hides(gx - 1, gy) else "walls/corner_w_tall", gx, gy, (d - .65, 0, gx))
    for (name, gx, gy, dx, dy) in props:
        kind = man["pieces"]["props/" + name]["kind"]
        order = {"decal": -.5, "wall_prop": -.55}.get(kind, (dx + dy) / 64)
        put("props/" + name, gx, gy, (gx + gy + order, 1, gx), dx, dy)
    for (order, name, gx, gy, dx, dy) in sorted(items, key=lambda t: t[0]):
        sx, sy = scr(gx, gy, dx, dy)
        pc = piece(name)
        info = man["pieces"][name]
        TX, TY = info.get("tile_top_vertex", man["tile_top_vertex"]); CW, CH = info.get("canvas", man["canvas"])
        img.alpha_composite(pc, (sx - TX, sy - TY))
        dk = tiles.get((gx, gy), 0.)
        pa = np.array(pc)[..., 3] > 0
        x0, y0 = sx - TX, sy - TY
        sub = darkmap[max(y0, 0):y0 + CH, max(x0, 0):x0 + CW]
        pa = pa[max(-y0, 0):max(-y0, 0) + sub.shape[0], max(-x0, 0):max(-x0, 0) + sub.shape[1]]
        sub[pa] = dk                                   # a peça herda a penumbra do tile dela
        if godot:
            hp = piece_height(name)[max(-y0, 0):max(-y0, 0) + sub.shape[0], max(-x0, 0):max(-x0, 0) + sub.shape[1]]
            hs = height[max(y0, 0):y0 + CH, max(x0, 0):x0 + CW]
            hs[pa] = np.nan_to_num(hp[pa])
        for L in man["pieces"][name]["lights"]:
            lights.append((sx - TX + L["x"], sy - TY + L["y"], L["power"], L["radius"]))
    if lit: img = relight(img, lights, st["ambient"] - man["baked_ambient"], darkmap)
    a = np.array(img); ys_, xs_ = np.where(a[..., 3] > 0)
    box = (int(xs_.min()) - 12, int(ys_.min()) - 12, int(xs_.max()) + 12, int(ys_.max()) + 12)
    img = img.crop(box)
    bg = Image.new("RGBA", img.size, C("060608") + (255,)); bg.alpha_composite(img)
    bg.convert("RGB").save(out)
    if godot:
        hc = np.zeros((box[3] - box[1], box[2] - box[0]), np.float32)
        sy0, sx0 = max(box[1], 0), max(box[0], 0)
        part = height[sy0:box[3], sx0:box[2]]
        hc[sy0 - box[1]:sy0 - box[1] + part.shape[0], sx0 - box[0]:sx0 - box[0] + part.shape[1]] = part
        v = np.clip(np.round(hc * 100), 0, 65535).astype(np.uint32)
        hm = np.dstack([(v >> 8).astype(np.uint8), (v & 255).astype(np.uint8), np.zeros_like(v, np.uint8)])
        base = os.path.splitext(out)[0]
        Image.fromarray(hm, "RGB").save(base + "_altura.png")
        walls, blockers = collision(tiles, edges, props)
        meta = {"image": os.path.basename(out), "height_image": os.path.basename(base) + "_altura.png",
                "size": list(bg.size), "height_scale": 100,
                "origin": [ox - box[0], oy - box[1]],
                "note": "pixel (px, py) = (wx - wy + origin.x, (wx + wy) / 2 - wz + origin.y); 1 tile = 64 unidades",
                "tiles": sorted([gx, gy] for (gx, gy) in tiles),
                "walls": [list(w) for w in walls],
                "blockers": [{"x": b[0], "y": b[1], "hx": b[2], "hy": b[3], "piece": b[4]} for b in blockers]}
        with open(base + ".json", "w", encoding="utf-8") as f: json.dump(meta, f, ensure_ascii=False)
    return bg

LUT = {}
for n in FNAMES:
    for i, hx in enumerate(FAM[n]): LUT.setdefault(C(hx), (FNAMES.index(n), i))

def relight(img, lights, amb, darkmap):
    """luz em espaço de tela: desloca cada cor dentro da sua rampa da AAP-64; velas esquentam a pedra fria"""
    a = np.array(img); H, W = a.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W]
    glow = np.zeros((H, W))
    for (lx, ly, p, r) in lights:
        R = int(r * 1.25 * 7)                      # janela: além disso a luz já é desprezível
        x0, x1, y0, y1 = max(lx - R, 0), min(lx + R, W), max(ly - R // 2, 0), min(ly + R // 2, H)
        if x1 <= x0 or y1 <= y0: continue
        d = np.hypot(xs[y0:y1, x0:x1] - lx, (ys[y0:y1, x0:x1] - ly) * 1.7)
        glow[y0:y1, x0:x1] += p * np.exp(-d / (r * 1.25))
    glow = 2.3 * (1 - np.exp(-glow / 1.7))
    dith = BAYER[ys % 4, xs % 4]
    L = amb + glow + darkmap * (1 - np.clip(glow / 2, 0, 1))
    warm = glow + dith * 1.1 > .95
    rgb = a[..., :3]; out = rgb.copy()
    keys = rgb[..., 0].astype(np.int64) * 65536 + rgb[..., 1].astype(np.int64) * 256 + rgb[..., 2]
    keys = np.where(a[..., 3] > 0, keys, -1)
    uk, inv = np.unique(keys, return_inverse=True); inv = inv.reshape(keys.shape)
    for col, (f, i) in LUT.items():
        k = col[0] * 65536 + col[1] * 256 + col[2]
        j = np.searchsorted(uk, k)
        if j >= len(uk) or uk[j] != k: continue
        m = inv == j
        if not m.any(): continue
        n = FNAMES[f]
        if n == "flame": continue
        idx = np.round(i + L[m] + dith[m] * .85).astype(int)
        pal = np.array([C(hx) for hx in FAM[n]], np.uint8)
        if n in ("cool", "iron"):
            wp = np.array([C(hx) for hx in FAM["warm"]], np.uint8)
            res = np.where(warm[m][:, None], wp[np.clip(idx, 0, len(wp) - 1)], pal[np.clip(idx, 0, len(pal) - 1)])
        else:
            res = pal[np.clip(idx, 0, len(pal) - 1)]
        out[m] = res
    a[..., :3] = out
    return Image.fromarray(a, "RGBA")

if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2], lit="--sem-luz" not in sys.argv, godot="--godot" in sys.argv)
