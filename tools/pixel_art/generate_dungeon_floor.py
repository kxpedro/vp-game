"""Gera o chão de masmorra isométrico em pixel art (128px, paleta AAP-64).

Uso: py tools/pixel_art/generate_dungeon_floor.py <prefixo_de_saida>
Ex.: py tools/pixel_art/generate_dungeon_floor.py assets/pixel_art/tiles/dungeon_floor/dungeon_floor_128

Requer numpy e Pillow. Gera 5 variantes (base, cracked, blood, missing_stone, wet) que
compartilham o mesmo desenho de borda, então encaixam entre si sem emenda; cada uma
em versão bloco (128x128, com lateral) e _flat (losango 128x64), mais prévias ampliadas.
"""
import sys
import numpy as np
from PIL import Image
import numpy as np
from PIL import Image

AAP64 = """060608 141013 3b1725 73172d b4202a df3e23 fa6a0a f9a31b ffd541 fffc40 d6f264 9cdb43 59c135 14a02e 1a7a3e 24523b
122020 143464 285cc4 249fde 20d6c7 a6fcdb ffffff fef3c0 fad6b8 f5a097 e86a73 bc4a9b 793a80 403353 242234 221c1a
322b28 71413b bb7547 dba463 f4d29c dae0ea b3b9d1 8b93af 6d758d 4a5462 333941 422433 5b3138 8e5252 ba756a e9b5a3
e3e6ff b9bffb 849be4 588dbe 477d85 23674e 328464 5daf8d 92dcba cdf7e2 e4d2aa c7b08b a08662 796755 5a4e44 423934""".split()
def C(h):
    assert h in AAP64, h
    return [int(h[i:i+2], 16) for i in (0, 2, 4)]
def ramp(*hs): return np.array([C(h) for h in hs], np.uint8)

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16 - .5

def pnoise(rng, f, u, v):
    """value noise periódico (f células por tile) em u,v contínuos [0,1)"""
    g = rng.random((f, f))
    x, y = u * f, v * f
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    tx, ty = x - x0, y - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a = g[y0 % f, x0 % f]; b = g[y0 % f, (x0 + 1) % f]
    c = g[(y0 + 1) % f, x0 % f]; d = g[(y0 + 1) % f, (x0 + 1) % f]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty
def fbm(rng, u, v, base=4, oct=4):
    s, amp, tot = 0, 1, 0
    for o in range(oct):
        s += amp * pnoise(rng, base * 2 ** o, u, v); tot += amp; amp *= .5
    return s / tot

def voronoi(rng, u, v, g, jitter=.8, scale=64):
    pts = (np.stack(np.meshgrid(np.arange(g), np.arange(g)), -1).reshape(-1, 2) + .5
           + (rng.random((g * g, 2)) - .5) * jitter) / g
    d = []
    for (pu, pv) in pts:
        du = np.abs(u - pu); du = np.minimum(du, 1 - du)
        dv = np.abs(v - pv); dv = np.minimum(dv, 1 - dv)
        d.append(np.hypot(du, dv) * scale)
    d = np.stack(d)
    order = np.argsort(d, 0)
    f1 = np.take_along_axis(d, order[:1], 0)[0]; f2 = np.take_along_axis(d, order[1:2], 0)[0]
    return order[0], f2 - f1


STONE = ramp("060608", "141013", "242234", "333941", "4a5462", "6d758d")   # carvão
MORT = ramp("141013", "221c1a", "322b28", "423934", "5a4e44", "71413b")    # junta terrosa
BLOOD = ramp("3b1725", "5b3138", "73172d")
WET_HI = ramp("588dbe", "8b93af")
W, HH, D, Y0, TEX = 128, 64, 24, 40, 64

ys, xs = np.mgrid[0:HH, 0:W]
px, py = xs + .5 - W / 2, ys + .5 - HH / 2
U = ((px / (W / 2) + py / (HH / 2)) / 2 + .5) % 1
V = ((py / (HH / 2) - px / (W / 2)) / 2 + .5) % 1
half = np.where(ys < HH // 2, 2 * (ys + 1), 2 * (HH - ys))
MASK = np.abs(xs + .5 - W / 2) <= half
DITH = BAYER[ys % 4, xs % 4]
BORDER = np.minimum(np.minimum(U, 1 - U), np.minimum(V, 1 - V))

def layout(seed, rows=5):
    rng = np.random.default_rng(seed)
    uw = (U + (fbm(rng, U, V, 3, 3) - .5) * .07) % 1
    vw = (V + (fbm(rng, U, V, 3, 3) - .5) * .06) % 1
    r = np.floor(vw * rows).astype(int) % rows
    fy = vw * rows - np.floor(vw * rows)
    dy = np.minimum(fy, 1 - fy) * TEX / rows
    sid = np.zeros_like(r); dx = np.zeros(U.shape)
    for row in range(rows):
        k = rng.integers(4, 7)
        w = rng.uniform(.75, 1.3, k); cuts = (np.cumsum(w) / w.sum() + rng.random()) % 1
        cuts = np.sort(cuts)
        sel = r == row
        uu = uw[sel]
        slant = rng.normal(0, .025, k)          # juntas verticais tortas
        cc = (cuts[None, :] + (fy[sel][:, None] - .5) * slant[None, :]) % 1
        d = np.abs(uu[:, None] - cc); d = np.minimum(d, 1 - d)
        dx[sel] = d.min(1) * TEX
        sid[sel] = row * 16 + (np.searchsorted(cuts, uu) % k)
    rad = 2.6
    edge = np.where((dx < rad) & (dy < rad), rad - np.hypot(rad - dx, rad - dy), np.minimum(dx, dy))
    tone = rng.normal(0, .45, rows * 16)
    tilt = rng.normal(0, .5, (rows * 16, 2))
    return sid, edge, tone, tilt

def make(variant, seed, L):
    sid, edge, tone, tilt = L
    rng = np.random.default_rng(seed)
    lrng = np.random.default_rng(999)                  # ruídos compartilhados: mantêm as bordas iguais entre variantes
    grain = fbm(lrng, U, V, 24, 2)
    worn = fbm(lrng, U, V, 3, 3)
    speck = lrng.random(U.shape)
    # pedras inteiramente internas podem mudar de tom por variante sem quebrar a emenda
    interior = np.zeros(len(tone), bool)
    for s in np.unique(sid):
        interior[s] = BORDER[sid == s].min() > .05
    tone = tone.copy()
    tone[interior] += rng.normal(0, .35, interior.sum())
    mw = .75 + .9 * np.clip(worn - .5, 0, 1) * 3
    mortar = edge < mw
    h = np.clip((edge - mw) / 3.2, 0, 1) ** .8 + (grain - .5) * .6
    h[mortar] = -.3
    gx = np.zeros_like(h); gy = np.zeros_like(h)
    gx[:, 1:-1] = (h[:, 2:] - h[:, :-2]) / 2; gy[1:-1, :] = (h[2:, :] - h[:-2, :]) / 2
    light = (gx + gy * 1.6) * 3.2
    lu = U * 6 % 1 - .5                                 # inclinação leve por pedra
    val = 3.15 + tone[sid] + light + tilt[sid, 0] * (U - .5) + (grain - .5) * 1.8 + DITH * .9
    val += (speck > .95) * 1 - (speck < .06) * 1        # granulado de granito
    dark = np.zeros(U.shape, bool)
    if variant == "rachado":
        csid, cedge = voronoi(np.random.default_rng(seed + 3), U, V, 8, .95)
        cracked = rng.random(len(tone)) < .75
        crack = (cedge < 1.0) & cracked[sid] & interior[sid] & (edge > mw + .8) & (fbm(rng, U, V, 5, 2) > .4)
        dark |= crack
    pit = np.zeros(U.shape, bool)
    if variant == "faltando":
        cands = [s for s in np.unique(sid) if interior[s] and BORDER[sid == s].min() > .15]
        s = max(cands, key=lambda q: (sid == q).sum())
        pit = (sid == s) & (edge > mw * .5)
    img = np.zeros((HH, W, 3), np.uint8)
    vi = np.clip(np.round(val), 0, 5).astype(int)
    below = np.zeros_like(mortar); below[:-1] = mortar[1:]
    right = np.zeros_like(mortar); right[:, :-1] = mortar[:, 1:]
    ol = (below | right) & ~mortar
    vi[ol] = np.minimum(vi[ol], 1)
    vi[dark] = 0
    img[:] = STONE[vi]
    mv = np.clip(np.round(1.4 + (worn - .5) * 2.5 + (grain - .5) * 2 + DITH - (edge < mw * .45) * 1.6), 0, 5).astype(int)
    mv[mortar & (speck > .92)] = 3
    img[mortar] = MORT[mv[mortar]]
    if variant == "faltando":
        # buraco de terra: borda de cima/esquerda em sombra, cascalho solto
        e = edge - mw * .5
        dv = np.clip(np.round(1.8 + (grain - .5) * 3 + DITH - (gx + gy * 1.6) * 0 + np.clip(e, 0, 4) * .4 - 1.5 * (e < 2) ), 0, 4).astype(int)
        img[pit] = MORT[dv[pit]]
        pebble = pit & (speck > .93)
        img[pebble] = STONE[3]
    if variant == "sangue":
        du = np.minimum(np.abs(U - .58), 1 - np.abs(U - .58)); dvv = np.minimum(np.abs(V - .4), 1 - np.abs(V - .4))
        r = np.hypot(du, dvv * 1.1)
        blob = 1 - r * 6.2 + (fbm(rng, U, V, 6, 3) - .5) * 1.8
        seep = mortar & (r < .38) & (fbm(rng, U, V, 8, 2) > .45)   # sangue escorrendo pelas juntas
        bl = (blob > 0) | seep
        bi = np.clip(np.round(blob * 1.4 + light * .5 + DITH * .8), 0, 2).astype(int)
        bi[mortar] = 0
        img[bl] = BLOOD[bi[bl]]
    if variant == "umido":
        du = np.minimum(np.abs(U - .45), 1 - np.abs(U - .45)); dvv = np.minimum(np.abs(V - .55), 1 - np.abs(V - .55))
        r = np.hypot(du, dvv)
        wet = (1 - r * 4.2 + (fbm(rng, U, V, 5, 3) - .5) * 1.2) > 0
        wv = np.clip(vi - 1, 0, 5)
        img[wet & ~mortar] = STONE[wv[wet & ~mortar]]
        img[wet & mortar] = STONE[np.clip(np.round(2 + DITH), 1, 3).astype(int)[wet & mortar]]  # água parada na junta
        glint = wet & ~mortar & (light > .55) & (speck > .55)
        img[glint] = WET_HI[(speck[glint] > .85).astype(int)]

    out = np.zeros((128, W, 4), np.uint8)
    blk = out[Y0:Y0 + HH]
    blk[MASK, :3] = img[MASK]; blk[MASK, 3] = 255
    bottom = np.array([Y0 + np.max(np.where(MASK[:, x])[0]) for x in range(W)])
    sl = ramp("060608", "141013", "242234", "333941", "4a5462")
    sr = ramp("141013", "242234", "333941", "4a5462", "6d758d")
    jm = C("221c1a")
    sn = np.random.default_rng(77)
    offs = [0, 9, 4]
    for x in range(W):
        left = x < W // 2
        s = x if left else W - 1 - x
        r = sl if left else sr
        for k in range(1, D + 1):
            y = bottom[x] + k
            course, kk = (k - 1) // 8, (k - 1) % 8
            if k == 1: c = r[4]
            elif kk == 7 or (s + offs[course]) % 18 == 0: c = jm if sn.random() < .6 else r[0]
            else:
                lvl = 2 + (kk == 0) - (kk >= 5) + ((s + offs[course]) % 18 == 1)
                lvl += int(sn.random() < .1) - int(sn.random() < .1)
                c = r[int(np.clip(lvl, 0, 4))]
            out[y, x, :3] = c; out[y, x, 3] = 255
    return Image.fromarray(out, "RGBA")

import os
base = sys.argv[1]
os.makedirs(os.path.dirname(os.path.abspath(base)), exist_ok=True)
NAMES = {"base": "base", "rachado": "cracked", "sangue": "blood", "faltando": "missing_stone", "umido": "wet"}
L = layout(int(sys.argv[2]) if len(sys.argv) > 2 else 8)
VARS = ["base", "rachado", "sangue", "faltando", "umido"]
tiles = []
for i, var in enumerate(VARS):
    t = make(var, 100 + i, L); tiles.append(t)
    name = f"{base}_{NAMES[var]}"
    t.save(name + ".png"); t.crop((0, Y0, W, Y0 + HH)).save(name + "_flat.png")
    a = np.array(t); used = {tuple(p[:3]) for p in a.reshape(-1, 4) if p[3]}
    print(var, len(used), all("%02x%02x%02x" % p in AAP64 for p in used))
bg = tuple(C("141013")) + (255,)
strip = Image.new("RGBA", (5 * 136, 128), bg)
for i, t in enumerate(tiles): strip.alpha_composite(t, (i * 136 + 4, 0))
strip.crop((0, 30, strip.width, 128)).resize((strip.width * 2, 98 * 2), Image.NEAREST).save(base + "_preview_variants_x2.png")
rng = np.random.default_rng(4); n = 5
mos = Image.new("RGBA", (n * 128 + 64, n * 64 + 110), tuple(C("060608")) + (255,))
for gy in range(n):
    for gx in range(n):
        t = tiles[rng.choice(5, p=[.44, .18, .1, .1, .18])]
        mos.alpha_composite(t, ((gx - gy) * 64 + (n - 1) * 64 + 32, (gx + gy) * 32 - 24))
mos.resize((mos.width * 2, mos.height * 2), Image.NEAREST).save(base + "_preview_mosaic_x2.png")
for i in (0, 3):
    tiles[i].resize((512, 512), Image.NEAREST).save(f"{base}_{NAMES[VARS[i]]}_preview_x4.png")
