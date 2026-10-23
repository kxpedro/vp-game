"""Materiais, texturas e objetos da cripta (paleta AAP-64). Gerado a partir dos concepts da sala."""
import sys, os, numpy as np
from PIL import Image
from iso import Canvas, FID, vnoise, hash1

# ===== origem: crypt.py =====





ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
TILES = os.path.join(ROOT, "assets", "pixel_art", "tiles", "dungeon_floor", "dungeon_floor_128_")
COOL, WARM, WOOD, IRON, MOSS = FID["cool"], FID["warm"], FID["wood"], FID["iron"], FID["moss"]
FACE_BASE = {"top": 4.0, "py": 3.4, "px": 2.5}

def grain(wx, wy, wz, k=1.0):
    return (vnoise(wx * .35 + wz * .2, wy * .35 - wz * .3, 3) - .5) * 1.4 * k + (vnoise(wx * 1.3, wy * 1.3 + wz * 1.1, 9) - .5) * .9 * k

def ashlar(course_h=16, block_w=34, seed=1, base_shift=0.0, moss=0.0):
    """cantaria: fiadas desencontradas, bisel no topo de cada bloco, tom por bloco"""
    def tex(face, wx, wy, wz, a, b, B):
        base = FACE_BASE[face] + base_shift
        g = grain(wx, wy, wz)
        if face == "top":
            return COOL, base - .8 + g
        along = wx if face == "py" else wy
        course = np.floor(wz / course_h).astype(int)
        off = (course * 37 + seed * 11) % block_w
        u = (along + off) % block_w
        blk = np.floor((along + off) / block_w).astype(int)
        cz = wz % course_h
        tone = ((blk * 73 + course * 151 + seed * 7) % 17) / 17 - .5
        v = base + g + tone * .9
        v = v + np.where(cz > course_h - 3, .9, 0) - np.where(cz < 3, .7, 0) + np.where(u < 3, .5, 0) - np.where(u > block_w - 3, .5, 0)
        mortar = (cz < 1.2) | (u > block_w - 1.4)
        v = np.where(mortar, 1.0 + g * .4, v)
        fam = np.full(v.shape, COOL)
        if moss:
            mm = mortar & (vnoise(along * .08, wz * .08, 5) * (1.4 - wz / 120) > 1 - moss)
            fam = np.where(mm, MOSS, fam); v = np.where(mm, 1.4 + g * .6, v)
        return fam, v
    return tex

def plain_stone(shift=0.0, frame=0, cross=False):
    def tex(face, wx, wy, wz, a, b, B):
        x0, x1, y0, y1, z0, z1 = B
        v = FACE_BASE[face] + shift + grain(wx, wy, wz, .7)
        if frame:
            if face == "top":
                ea = np.minimum(a, (x1 - x0) - a); eb = np.minimum(b, (y1 - y0) - b)
            elif face == "py":
                ea = np.minimum(a, (x1 - x0) - a); eb = np.minimum(b, (z1 - z0) - b)
            else:
                ea = np.minimum(a, (y1 - y0) - a); eb = np.minimum(b, (z1 - z0) - b)
            e = np.minimum(ea, eb)
            v = v + np.where(e < frame, .6, 0) - np.where((e >= frame) & (e < frame + 1.5), .9, 0)
            if cross and face == "top":
                L, W = x1 - x0, y1 - y0
                cx = (np.abs(b - W / 2) < 3.5) & (a > L * .18) & (a < L * .88)
                cy = (np.abs(a - L * .34) < 3.5) & (np.abs(b - W / 2) < W * .32)
                cr = cx | cy
                v = v + np.where(cr, 1.0, 0)
                rim = (np.abs(b - W / 2) < 5) & (a > L * .18) & (a < L * .88) | (np.abs(a - L * .34) < 5) & (np.abs(b - W / 2) < W * .32 + 1.5)
                v = v - np.where(rim & ~cr, .9, 0)
        return np.full(v.shape, COOL), v
    return tex

def sarc_body():
    inner = plain_stone(0, frame=5)
    def tex(face, wx, wy, wz, a, b, B):
        f, v = inner(face, wx, wy, wz, a, b, B)
        if face == "top":
            x0, x1, y0, y1, *_ = B
            e = np.minimum(np.minimum(a, x1 - x0 - a), np.minimum(b, y1 - y0 - b))
            v = np.where(e > 6, .2 + (vnoise(wx * .3, wy * .3, 4) - .5) * .8, v)   # cavidade escura
        return f, v
    return tex

def rubble_tex(seed):
    def tex(face, wx, wy, wz, a, b, B):
        return np.full(wx.shape, COOL), FACE_BASE[face] - .3 + (hash1(seed) - .5) * 1.4 + grain(wx, wy, wz)
    return tex

def wax(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, WARM), {"top": 8.6, "py": 8.0, "px": 7.0}[face] + (b > (B[5] - B[4]) - 2) * .5

def iron(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, IRON), {"top": 3.5, "py": 3.0, "px": 2.0}[face] + (vnoise(wx * .5, wz * .5 + wy * .5, 2) - .5)

FLAME = [" a ", " b ", "bcb", "bdb", " c "]
FLAME_C = {"a": ("flame", 0, True), "b": ("flame", 1, True), "c": ("flame", 2, True), "d": ("flame", 4, True)}
SKULL = [
    "  .....  ",
    " .oOOOo. ",
    ".oOOOOOo.",
    ".oOOOOOo.",
    ".o##O##o.",
    ".o##O##o.",
    " .oO.Oo. ",
    "  .ttt.  ",
    "  .t.t.  ",
]
SKULL_C = {"o": ("warm", 7), "O": ("warm", 8), ".": ("warm", 2), "#": ("warm", 0), "t": ("warm", 6)}

def candle(cv, x, y, h, lights, z=0):
    cv.box(x - 1.5, x + 1.5, y - 1.5, y + 1.5, z, z + h, wax, step=.4)
    cv.sprite(FLAME, FLAME_C, x + 1, y + 1, z + h + 1, depth_bias=6)
    lights.append((x, y, z + h + 4, 1.0, 60))

def candle_cluster(cv, x, y, lights, seed, z=0):
    rng = np.random.default_rng(seed)
    for k in range(rng.integers(3, 6)):
        candle(cv, x + rng.uniform(-9, 9), y + rng.uniform(-9, 9), rng.uniform(5, 16), lights, z)

def pillar(cv, x, y, h=190, broken=False, seed=0):
    w = 22
    cv.box(x - w - 6, x + w + 6, y - w - 6, y + w + 6, 0, 12, plain_stone(.2, frame=3))
    if not broken:
        cv.box(x - w, x + w, y - w, y + w, 12, h - 14, ashlar(14, 44, seed + 3))
        cv.box(x - w - 5, x + w + 5, y - w - 5, y + w + 5, h - 14, h, plain_stone(.4, frame=3))
    else:
        rng = np.random.default_rng(seed)
        for i in range(0, 2 * w, 4):            # topo quebrado em degraus
            for j in range(0, 2 * w, 4):
                top = 60 + 30 * vnoise(np.array(i * .08), np.array(j * .08), seed) + rng.uniform(-6, 6) - (i + j) * .5
                cv.box(x - w + i, x - w + i + 4, y - w + j, y - w + j + 4, 12, float(top), ashlar(14, 44, seed + 3))
        for k in range(9):
            rx, ry, s = x + rng.uniform(-60, 70), y + rng.uniform(25, 80), rng.uniform(5, 13)
            cv.box(rx, rx + s, ry, ry + s * rng.uniform(.6, 1.2), 0, s * rng.uniform(.4, .9), rubble_tex(seed * 31 + k))



# ===== origem: hub.py =====





COOL, WARM, WOOD, IRON, MOSS, EARTH, BLOOD = (FID[k] for k in ["cool", "warm", "wood", "iron", "moss", "earth", "blood"])
T = 30            # espessura das paredes
WH = 210          # altura das paredes do fundo (somem no escuro)
FH = 26           # altura das paredes da frente (corte)

def fbm2(a, b, seed, oct=4, f=.04):
    s, amp, tot = 0, 1, 0
    for o in range(oct):
        s += amp * vnoise(a * f * 2 ** o, b * f * 2 ** o, seed + o); tot += amp; amp *= .5
    return s / tot

def side_light(th): return 2.95 + .45 * (np.sin(th) - np.cos(th))

def rockbrick(seed=0, brick=True):
    """rocha escavada (relevo irregular, fendas) com remendos de tijolo"""
    def tex(face, wx, wy, wz, a, b, B):
        base = FACE_BASE[face]
        if face == "top":
            return COOL, base - 1.0 + grain(wx, wy, wz) + (fbm2(wx, wy, seed + 4) - .5) * 2
        along = wx if face == "py" else wy
        sgn = 1 if face == "py" else -1           # direção "esquerda da tela" ao longo da parede
        h = lambda p, q: fbm2(p, q, seed, 5, .035)
        h0 = h(along, wz)
        shade = np.clip((h(along - 3 * sgn, wz + 3) - h0) * -16, -1.6, 1.6)
        v = base - .3 + shade + (vnoise(along * .9, wz * .9, seed + 7) - .5) * .9
        crack = (np.abs(fbm2(along, wz, seed + 20, 2, .05) - .5) < .012) & (vnoise(along * .03, wz * .03, seed + 21) > .55)
        fam = np.full(v.shape, COOL)
        if brick:
            mask = fbm2(along, wz, seed + 40, 3, .012) + (wz < 110) * .06
            bm = mask > .6
            ch = 10; bw = 22
            course = np.floor(wz / ch).astype(int)
            off = (course % 2) * (bw / 2)
            u = (along + off) % bw; cz = wz % ch
            blk = np.floor((along + off) / bw).astype(int)
            tone = ((blk * 73 + course * 151) % 13) / 13 - .5
            missing = (((blk * 31 + course * 17) % 11) == 0) | (mask < .62) & ((((blk * 7 + course * 5) % 3) == 0))
            bv = 3.1 + tone * 1.2 + (cz > ch - 2.5) * .9 - (cz < 2) * .6 + (vnoise(along * .7, wz * .7, 3) - .5) * .8
            mort = (cz < 1.3) | (u < 1.3)
            bv = np.where(mort, 1.2, bv)
            bv = np.where(missing & ~mort, 1.0, bv)        # tijolo faltando: buraco escuro
            fam = np.where(bm, EARTH, fam)
            v = np.where(bm, np.where(mort | missing, bv, bv - 1.1), v)
            fam = np.where(bm & (mort | missing), COOL, fam)
        mm = (wz < 40) & (fbm2(along, wz, seed + 60, 3, .05) * (1.3 - wz / 60) > .62)
        fam = np.where(mm, MOSS, fam); v = np.where(mm, 1.6 + shade * .5, v)
        return fam, v
    return tex

def dressed(shift=0.0, ch=14, bw=30, seed=0):
    """pedra aparelhada (arcos, soleiras)"""
    def tex(face, wx, wy, wz, a, b, B):
        base = FACE_BASE[face] + shift
        if face == "top": return COOL, base + grain(wx, wy, wz, .6)
        along = wx if face == "py" else wy
        course = np.floor(wz / ch).astype(int); off = (course % 2) * bw / 2
        u = (along + off) % bw; cz = wz % ch
        v = base + grain(wx, wy, wz, .6) + (cz > ch - 2.5) * .8 - (cz < 2) * .6
        v = np.where((cz < 1.2) | (u < 1.2), 1.0, v)
        return np.full(v.shape, COOL), v
    return tex

ROCK = rockbrick(1)

def room_tiles():
    R = set()
    def rect(x0, x1, y0, y1):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1): R.add((x, y))
    rect(4, 7, 5, 8)      # centro (fonte)
    rect(5, 7, 0, 4)      # braço longo até o arco do fundo
    rect(1, 3, 5, 7)      # braço da bancada de armas
    rect(4, 6, 9, 11)     # braço do caixão do Virgil
    rect(8, 9, 6, 8)      # braço do arco da direita
    return R

def walls(cv, R, openings):
    """paredes do fundo (altas) e da frente (cortadas) a partir dos tiles; openings = trechos sem parede"""
    def opened(kind, fixed, a0):
        for (k, f, o0, o1) in openings:
            if k == kind and abs(f - fixed) < 1 and a0 + 64 > o0 and a0 < o1: return (o0, o1)
        return None
    def hides_room(x, y):
        return any((x2, y2) in R for x2 in range(x - 8, x + 1) for y2 in range(y - 8, y + 1)
                   if abs((x2 - y2) - (x - y)) <= 1 and 1 <= (x + y) - (x2 + y2) <= 7)
    for (x, y) in sorted(R):
        X, Y = x * 64, y * 64
        if (x - 1, y) not in R and not opened("x", X, Y):
            cv.box(X - T, X, Y - T, Y + 64 + T, 0, FH if hides_room(x - 1, y) else WH, ROCK)
        if (x, y - 1) not in R and not opened("y", Y, X):
            cv.box(X - T, X + 64 + T, Y - T, Y, 0, FH if hides_room(x, y - 1) else WH, ROCK)
        if (x + 1, y) not in R:
            o = opened("x", X + 64, Y)
            for (a0, a1) in ([(Y, Y + 64)] if not o else [(Y, o[0]), (o[1], Y + 64)]):
                if a1 > a0: cv.box(X + 64, X + 64 + T, a0 - (T if (x, y - 1) not in R else 0), a1, 0, FH, ROCK)
        if (x, y + 1) not in R:
            o = opened("y", Y + 64, X)
            for (a0, a1) in ([(X, X + 64)] if not o else [(X, o[0]), (o[1], X + 64)]):
                if a1 > a0: cv.box(a0 - (T if (x - 1, y) not in R else 0), a1 + T * ((x + 1, y) not in R), Y + 64, Y + 64 + T, 0, FH, ROCK)

def back_arch(cv, x0, x1, ywall, lights):
    """arco de passagem numa parede do fundo (plano y = ywall), vão de x0 a x1"""
    w = x1 - x0; r = w / 2; post = 78; cx = (x0 + x1) / 2
    jamb = 14
    for xx in np.arange(x0 - jamb, x1 + jamb, 2.0):
        a = xx + 1 - cx
        if abs(a) < r:
            top_open = post + np.sqrt(max(r * r - a * a, 0))
            cv.box(xx, xx + 2, ywall - T, ywall, top_open, top_open + 14, dressed(.6, 7, 9, 2), step=.5)   # aduelas
            cv.box(xx, xx + 2, ywall - T, ywall, top_open + 14, WH, ROCK)
        else:
            cv.box(xx, xx + 2, ywall - T, ywall + 4, 0, post + 8 + (r + jamb - abs(a)) * 1.2, dressed(.5, 13, 40, 3))
            cv.box(xx, xx + 2, ywall - T, ywall, post + 8 + (r + jamb - abs(a)) * 1.2, WH, ROCK)
    # corredor: paredes laterais escuras
    cv.box(x0 - T, x0, ywall - 200, ywall - T, 0, WH, ROCK)
    cv.box(x1, x1 + T, ywall - 200, ywall - T, 0, WH, ROCK)
    for sx in (x0 - jamb - 8, x1 + jamb + 8):     # arandelas dos dois lados
        sconce(cv, sx, ywall + 3, 96, lights)

def front_arch(cv, xwall, y0, y1):
    """passagem numa parede da frente (corte baixo): ombreiras de pedra aparelhada"""
    for (a0, a1) in [(y0 - 16, y0), (y1, y1 + 16)]:
        cv.box(xwall, xwall + T + 4, a0, a1, 0, FH + 22, dressed(.7, 12, 16, 4))
    cv.box(xwall, xwall + T, y0, y1, 0, 3, dressed(.1, 12, 64, 5))      # soleira

SCONCE = [
    "  i  ",
    " iii ",
    "iiiii",
    "  i  ",
    " ii  ",
    "ii   ",
]
SCONCE_C = {"i": ("iron", 2)}

def sconce(cv, x, y, z, lights):
    cv.sprite(SCONCE, SCONCE_C, x + 1, y + 1, z, depth_bias=20)
    cv.box(x - 1.5, x + 1.5, y - 1.5, y + 1.5, z + 6, z + 16, wax, step=.4, depth_bias=20)
    cv.sprite(FLAME, FLAME_C, x + 1, y + 1, z + 17, depth_bias=24)
    lights.append((x, y + 6, z + 20, 1.5, 70))

# ---------- fonte de sangue ----------
def stone_cyl(shift=0.0, frame=0):
    def tex(face, wx, wy, wz, th, rr):
        if face == "top":
            v = 4.0 + shift + grain(wx, wy, wz, .6)
            if frame: v = v + np.where(rr > rr.max() - frame, .5, 0)
        else:
            v = side_light(th) + shift + grain(wx, wy, wz, .6)
            seg = (np.floor(th / (2 * np.pi) * 24) % 2 == 0)
            v = v + np.where(np.abs(((th / (2 * np.pi) * 24) % 1) - .5) > .45, -1.2, 0)   # juntas dos blocos
        return np.full(np.shape(v), COOL), v
    return tex

def blood_pool(cx, cy):
    def tex(face, wx, wy, wz, th, rr):
        ring = np.sin(rr * .45 - 0) * .5
        v = .9 + ring * .6 + (vnoise(wx * .15, wy * .15, 77) - .5) * 1.2
        hi = (vnoise(wx * .3, wy * .3, 78) > .8) & (rr > 18)
        v = np.where(hi, 2.6, v)
        return np.full(np.shape(v), BLOOD), v
    return tex

def blood_drip(shift=0.0, density=.5, seed=0):
    base = stone_cyl(shift)
    def tex(face, wx, wy, wz, th, rr):
        f, v = base(face, wx, wy, wz, th, rr)
        if face == "side":
            streak = vnoise(th * 9, wz * .02, seed) + (vnoise(th * 30, np.zeros_like(th), seed + 1) - .5) * .6
            m = streak > 1 - density
            f = np.where(m, BLOOD, f); v = np.where(m, 1.8 + (side_light(th) - 2.95) * 2 + (wz % 7 < 1) * .4, v)
        return f, v
    return tex

def fountain(cv, cx, cy, lights):
    cv.cyl(cx, cy, 84, 0, 7, stone_cyl(.0, frame=3))
    cv.cyl(cx, cy, 76, 7, 32, stone_cyl(.3, frame=4), r_in=66)
    cv.cyl(cx, cy, 66, 7, 32, stone_cyl(-1.2), top=False)           # parede interna do tanque
    cv.cyl(cx, cy, 66, 7, 25, blood_pool(cx, cy), side=False)
    cv.cyl(cx, cy, 16, 25, 70, blood_drip(.2, .3, 3))
    cv.cyl(cx, cy, 22, 70, 74, stone_cyl(.4))
    cv.cyl(cx, cy, 34, 74, 88, blood_drip(.3, .55, 9), r_in=28)
    cv.cyl(cx, cy, 28, 74, 85, blood_pool(cx, cy), side=False)
    # coração de pedra sobre a taça, de onde o sangue nasce
    cv.cyl(cx, cy, 7, 85, 104, blood_drip(.1, .7, 5))
    cv.sprite(HEART, HEART_C, cx + 1, cy + 1, 100, depth_bias=10)
    for k in range(12):                                                  # velas em volta do tanque
        t = k / 12 * 2 * np.pi + .2
        candle(cv, cx + np.cos(t) * 96, cy + np.sin(t) * 96, 6 + (k * 7) % 10, lights)
    lights.append((cx, cy, 90, 1.2, 120))

HEART = [
    "  .oo...oo.  ",
    " .oOOo.oOOo. ",
    ".oOOOOoOOOOo.",
    ".oOOOOOOOOOo.",
    ".oOOOOOOOOOo.",
    " .oOOOOOOOo. ",
    "  .oOOOOOo.  ",
    "   .oOOOo.   ",
    "    .oOo.    ",
    "     .o.     ",
]
HEART_C = {".": ("blood", 0), "o": ("blood", 2), "O": ("blood", 3)}

# ---------- madeira, bancada e armas ----------
def wood_tex(shift=0.0):
    def tex(face, wx, wy, wz, a, b, B):
        base = {"top": 3.6, "py": 3.0, "px": 2.2}[face] + shift
        along = (wx if face != "px" else wy)
        grain_ = (vnoise(along * .08, (wz if face != "top" else (wy if face == "top" else wz)) * 1.2, 31) - .5) * 1.6
        v = base + grain_
        plank = ((wy if face == "top" else wz) % 9) < 1
        v = np.where(plank, 1.2, v)
        return np.full(v.shape, WOOD), v
    return tex

SWORD = [
    "  o  ", "  O  ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ",
    " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ", " .O. ",
    "ggggg", "  w  ", "  w  ", "  w  ", "  w  ", "  g  ",
]
SWORD_C = {"o": ("cool", 7), "O": ("cool", 7), ".": ("cool", 5), "g": ("warm", 6), "w": ("wood", 4)}
AXE = [
    " ..  ", ".OO. ", ".OOo.", ".OOow", ".OO.w", " .. w", "    w", "    w", "    w", "    w", "    w",
    "    w", "    w", "    w", "    w", "    w", "    w", "    w", "    g",
]
AXE_C = {"O": ("cool", 7), "o": ("cool", 6), ".": ("cool", 4), "w": ("wood", 4), "g": ("iron", 3)}
WHIP = [
    "  wwww  ", " w    w ", "w  ww  w", "w w  w w", "w  ww  w", " w    w ", "  wwww  ", "     ww ", "      g ", "      g ",
]
WHIP_C = {"w": ("wood", 5), "g": ("iron", 3)}
REVOLVER = [
    "........ ", ".OOOOOOO.", ".OOoo... ", ".Oo.     ", ".ww.     ", ".ww.     ", " ..      ",
]
REVOLVER_C = {".": ("iron", 1), "O": ("iron", 4), "o": ("iron", 3), "w": ("wood", 4)}

def x2(rows): return [r2 for r in rows for r2 in ("".join(c * 2 for c in r),) * 2]

def weapon_bench(cv, X, y0, y1, lights):
    """armário/estante de armas encostado na parede x = X, de y0 a y1"""
    d = 16
    cv.box(X, X + d, y0, y1, 0, 8, wood_tex(-.4))                    # rodapé
    cv.box(X, X + 3, y0, y1, 8, 120, wood_tex(-1.0))                  # fundo do armário
    for yy in (y0, (y0 + y1) / 2, y1 - 6):
        cv.box(X, X + d, yy, yy + 6, 8, 120, wood_tex(0))             # montantes
    cv.box(X, X + d + 4, y0 - 2, y1 + 2, 120, 128, wood_tex(.2))      # topo
    cv.box(X, X + 6, y0, y1, 30, 34, wood_tex(-.2)); cv.box(X, X + 6, y0, y1, 84, 88, wood_tex(-.2))  # travessas
    for k, (spr, col, off) in enumerate([(SWORD, SWORD_C, 16), (AXE, AXE_C, 44), (SWORD, SWORD_C, 74), (SWORD, SWORD_C, 100)]):
        cv.sprite(x2(spr), col, X + 12, y0 + off + 4, 34, depth_bias=60)
    for k in range(4):
        candle(cv, X + 8, y0 + 8 + k * (y1 - y0 - 16) / 3, 6 + (k * 5) % 9, lights, z=128)
    # mesa de trabalho na frente
    tx0, tx1, ty0, ty1, th = X + 46, X + 86, y0 + 6, y1 - 6, 38
    for (lx, ly) in [(tx0 + 2, ty0 + 2), (tx1 - 6, ty0 + 2), (tx0 + 2, ty1 - 6), (tx1 - 6, ty1 - 6)]:
        cv.box(lx, lx + 4, ly, ly + 4, 0, th - 4, wood_tex(-.5))
    cv.box(tx0, tx1, ty0, ty1, th - 4, th, wood_tex(.3))
    cv.sprite(x2(REVOLVER), REVOLVER_C, (tx0 + tx1) / 2, ty0 + 24, th, depth_bias=6)
    cv.sprite(x2(REVOLVER), REVOLVER_C, (tx0 + tx1) / 2 + 4, ty0 + 40, th, depth_bias=6)
    cv.sprite(x2(WHIP), WHIP_C, (tx0 + tx1) / 2, ty1 - 18, th, depth_bias=6)
    candle(cv, tx0 + 8, ty0 + 8, 10, lights, z=th); candle(cv, tx0 + 12, ty0 + 13, 6, lights, z=th)
    candle(cv, tx1 - 10, ty1 - 10, 8, lights, z=th)

# ---------- caixão do Virgil ----------
def sarc_tex(frame=5, cavity=False, cross=False, along_y=False):
    def tex(face, wx, wy, wz, a, b, B):
        x0, x1, y0, y1, z0, z1 = B
        v = FACE_BASE[face] + grain(wx, wy, wz, .7)
        dims = {"top": (x1 - x0, y1 - y0), "py": (x1 - x0, z1 - z0), "px": (y1 - y0, z1 - z0)}[face]
        e = np.minimum(np.minimum(a, dims[0] - a), np.minimum(b, dims[1] - b))
        v = v + np.where(e < frame, .6, 0) - np.where((e >= frame) & (e < frame + 1.5), .9, 0)
        f = np.full(v.shape, COOL)
        if face == "top" and cross:
            A, Bc, LA, LB = (b, a, y1 - y0, x1 - x0) if along_y else (a, b, x1 - x0, y1 - y0)
            cr = ((np.abs(Bc - LB / 2) < 3.5) & (A > LA * .14) & (A < LA * .86)) | ((np.abs(A - LA * .32) < 3.5) & (np.abs(Bc - LB / 2) < LB * .32))
            v = v + np.where(cr, 1.1, 0)
        if face == "top" and cavity:
            inside = e > 6
            f = np.where(inside, BLOOD, f)                        # forro de veludo vermelho-escuro
            v = np.where(inside, .9 + (vnoise(wx * .2, wy * .2, 4) - .5) * 1.2, v)
        return f, v
    return tex

def virgil_coffin(cv, cx, cy, lights):
    w, l = 34, 70
    cv.box(cx - w - 8, cx + w + 8, cy - l - 8, cy + l + 8, 0, 10, sarc_tex(3))
    cv.box(cx - w, cx + w, cy - l, cy + l, 10, 56, sarc_tex(5, cavity=True))
    cv.box(cx - w - 4, cx + w + 4, cy - l + 34, cy + l + 26, 56, 68, sarc_tex(4, cross=True, along_y=True))
    for (dx, dy, s) in [(-58, -60, 1), (56, -76, 2), (-60, 40, 3), (58, 66, 4), (0, l + 52, 5)]:
        candle_cluster(cv, cx + dx, cy + dy, lights, s)



# ===== origem: wing.py =====






COOL, WARM, MOSS, BLOOD, IRON = (FID[k] for k in ["cool", "warm", "moss", "blood", "iron"])
T, WH, FH = 22, 76, 12

def crypt_stone(seed=0):
    """blocos de pedra bruta em fiadas irregulares + rocha viva, tons frios e escuros"""
    rows = np.cumsum([0] + [12 + int(hash1(seed, k) * 9) for k in range(12)])
    def tex(face, wx, wy, wz, a, b, B):
        if face == "top":
            return np.full(wx.shape, COOL), .5 + (fbm2(wx, wy, seed + 4) - .5) * 1.5
        along = wx if face == "py" else wy
        base = 2.6 if face == "py" else 1.9
        wa = along + (vnoise(along * .05, wz * .08, seed + 1) - .5) * 4
        wz2 = wz + (vnoise(along * .07, wz * .05, seed + 2) - .5) * 3
        course = np.searchsorted(rows, wz2, side="right") - 1
        z0 = rows[np.clip(course, 0, len(rows) - 2)]; z1 = rows[np.clip(course + 1, 1, len(rows) - 1)]
        bw = 22 + (course * 7 % 5) * 4
        off = (course * 13) % 17
        u = (wa + off) % bw; blk = np.floor((wa + off) / bw).astype(int)
        cz = wz2 - z0; chh = z1 - z0
        tone = ((blk * 73 + course * 151 + seed) % 17) / 17 - .5
        g = grain(wx, wy, wz, .9)
        v = base + tone * 1.1 + g
        v = v + (cz > chh - 2.5) * .9 - (cz < 2.5) * .9 + (u < 2.5) * .5 - (u > bw - 2.5) * .6
        chip = vnoise(wa * .25, wz2 * .25, seed + 9) > .8
        v = np.where(chip, v - 1.0, v)
        joint = (cz < 1.4) | (u > bw - 1.6)
        v = np.where(joint, .4 + g * .3, v)
        # rocha viva onde a parede foi escavada
        rock = fbm2(along, wz, seed + 30, 3, .02) + wz / 400 > .62
        h = lambda p, q: fbm2(p, q, seed + 31, 4, .06)
        sgn = 1 if face == "py" else -1
        rv = base - .4 + np.clip((h(along, wz) - h(along - 2 * sgn, wz + 2)) * 18, -1.5, 1.5) + g * .6
        v = np.where(rock, rv, v)
        fam = np.full(v.shape, COOL)
        mm = (wz < 14) & (vnoise(along * .06, wz * .1, seed + 50) > .72)
        fam = np.where(mm, MOSS, fam); v = np.where(mm, 1.2 + g * .4, v)
        return fam, v
    return tex

def shadow_tex(v0):
    def tex(face, wx, wy, wz, a, b, B):
        return np.full(wx.shape, COOL), v0 + FACE_BASE[face] - 3.4 + grain(wx, wy, wz, .5)
    return tex

def wall(cv, axis, fixed, a0, a1, height, holes=(), seed=0):
    """axis 'y': parede no plano y=fixed (face +y), corre em x; axis 'x': plano x=fixed (face +x), corre em y.
    holes: (a0, a1, z0, z1, prof) — nichos escavados"""
    tex = crypt_stone(seed)
    def box(p0, p1, z0, z1, t0=-T, t1=0, tx=tex):
        if p1 <= p0 or z1 <= z0: return
        if axis == "y": cv.box(p0, p1, fixed + t0, fixed + t1, z0, z1, tx)
        else: cv.box(fixed + t0, fixed + t1, p0, p1, z0, z1, tx)
    cuts = sorted({a0, a1} | {h[0] for h in holes} | {h[1] for h in holes})
    for p0, p1 in zip(cuts, cuts[1:]):
        hs = [h for h in holes if h[0] <= p0 and h[1] >= p1]
        if not hs:
            box(p0, p1, 0, height); continue
        _, _, z0, z1, d = hs[0]
        box(p0, p1, 0, z0); box(p0, p1, z1, height)
        box(p0, p1, z0, z1, -T, -d, shadow_tex(.2))                 # fundo do nicho
        box(p0, p1, z0 - 2, z0, -d, 2, shadow_tex(1.2))              # soleira

BONES = [
    " ..    ",
    ".oO.   ",
    ".#o.ooo",
    " .. ...",
]
BONES_C = {"o": ("warm", 7), "O": ("warm", 8), ".": ("warm", 2), "#": ("warm", 0)}
CANDLE_SMALL = 1.0

def candle(cv, x, y, h, lights, z=0, power=.9, rad=34):
    cv.box(x - 1, x + 1, y - 1, y + 1, z, z + h, wax, step=.34)
    cv.sprite(FLAME, FLAME_C, x + 1, y + 1, z + h + 1, depth_bias=6)
    lights.append((x, y, z + h + 3, power, rad))

def cluster(cv, x, y, lights, seed, n=(3, 6), z=0, spread=6):
    rng = np.random.default_rng(seed)
    for k in range(rng.integers(*n)):
        candle(cv, x + rng.uniform(-spread, spread), y + rng.uniform(-spread, spread), rng.uniform(3, 10), lights, z)

def sconce(cv, x, y, z, lights):
    cv.sprite(SCONCE, SCONCE_C, x + 1, y + 1, z, depth_bias=14)
    cv.box(x - 1, x + 1, y - 1, y + 1, z + 6, z + 12, wax, step=.34, depth_bias=14)
    cv.sprite(FLAME, FLAME_C, x + 1, y + 1, z + 13, depth_bias=18)
    lights.append((x, y + 5, z + 15, 1.6, 70))

def sarc(frame, cavity=False, cross=False):
    def tex(face, wx, wy, wz, a, b, B):
        x0, x1, y0, y1, z0, z1 = B
        v = FACE_BASE[face] - .6 + grain(wx, wy, wz, .6)
        dims = {"top": (x1 - x0, y1 - y0), "py": (x1 - x0, z1 - z0), "px": (y1 - y0, z1 - z0)}[face]
        e = np.minimum(np.minimum(a, dims[0] - a), np.minimum(b, dims[1] - b))
        v = v + np.where(e < frame, .6, 0) - np.where((e >= frame) & (e < frame + 1), .9, 0)
        f = np.full(v.shape, COOL)
        if face == "top" and cross:
            LA, LB = y1 - y0, x1 - x0
            cr = ((np.abs(a - LB / 2) < 1.6) & (b > LA * .15) & (b < LA * .85)) | ((np.abs(b - LA * .33) < 1.6) & (np.abs(a - LB / 2) < LB * .3))
            v = v + np.where(cr, 1.2, 0)
        if face == "top" and cavity:
            inside = e > 3
            f = np.where(inside, BLOOD, f); v = np.where(inside, .8 + (vnoise(wx * .3, wy * .3, 4) - .5), v)
        return f, v
    return tex

def coffin(cv, cx, cy, lights):
    w, l = 12, 29                                     # 24 x 58 unidades (~1,2x a altura do Virgil)
    cv.box(cx - w - 4, cx + w + 4, cy - l - 4, cy + l + 4, 0, 4, sarc(2))
    cv.box(cx - w, cx + w, cy - l, cy + l, 4, 24, sarc(2.5, cavity=True))
    cv.box(cx - w - 2, cx + w + 2, cy - l + 12, cy + l + 8, 24, 29, sarc(2, cross=True))
    for k, (dx, dy) in enumerate([(-26, -30), (26, -36), (-28, 20), (27, 30), (0, 48), (-6, -50)]):
        cluster(cv, cx + dx, cy + dy, lights, 40 + k)

VIRGIL_REF = os.environ.get("VIRGIL_REF", "")

def load_virgil():
    im = np.array(Image.open(VIRGIL_REF).convert("RGBA")).astype(int)
    cell = im[768:1152, 0:384][::2, ::2]
    bg = im[5, 5]
    a = cell.copy(); a[np.abs(cell - bg).sum(2) < 30, 3] = 0
    ys, xs = np.where(a[..., 3] > 0)
    return Image.fromarray(a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].astype(np.uint8))



# ===== origem: wing2.py =====







COOL, WARM, MOSS, BLOOD, WOOD = (FID[k] for k in ["cool", "warm", "moss", "blood", "wood"])
T, WH, FH = 22, 76, 12
RH = 12.8

_rows = {}
def row_cuts(r, seed):
    key = (r, seed)
    if key not in _rows:
        rng = np.random.default_rng(abs(hash((r, seed))) % 2**32)
        w = rng.uniform(9, 17, 200)
        _rows[key] = np.cumsum(w) - 800 + rng.uniform(0, 14)
    return _rows[key]

def sett_wall(seed=0, shift=0.0):
    """paredes com o mesmo paralelepípedo do chão: pedra carvão, bisel arredondado, junta terrosa"""
    def edge_at(p, q):
        pw = p + (vnoise(p * .06, q * .06, seed + 1) - .5) * 4
        qw = q + (vnoise(p * .05, q * .07, seed + 2) - .5) * 3
        rf = qw / RH; r = np.floor(rf).astype(int); fy = rf - r
        dy = np.minimum(fy, 1 - fy) * RH
        dx = np.zeros_like(p); sid = np.zeros(p.shape, int)
        for rr in np.unique(r):
            m = r == rr
            c = row_cuts(int(rr), seed)
            i = np.clip(np.searchsorted(c, pw[m]), 1, len(c) - 1)
            dx[m] = np.minimum(pw[m] - c[i - 1], c[i] - pw[m]); sid[m] = rr * 1000 + i
        rad = 2.6
        e = np.where((dx < rad) & (dy < rad), rad - np.hypot(rad - dx, rad - dy), np.minimum(dx, dy))
        return e, sid, fy
    def tex(face, wx, wy, wz, a, b, B):
        if face == "top":
            return np.full(wx.shape, COOL), .7 + shift + (fbm2(wx, wy, seed + 4) - .5) * 1.2
        p = wx if face == "py" else wy
        s = 1 if face == "py" else -1
        worn = fbm2(p, wz, seed + 9, 3, .02)
        mw = .75 + .9 * np.clip(worn - .5, 0, 1) * 3
        e, sid, fy = edge_at(p, wz)
        eR, _, _ = edge_at(p + s, wz); eU, _, _ = edge_at(p, wz + 1)
        H = lambda ee: np.clip((ee - mw) / 3.2, 0, 1) ** .8
        h0 = H(e)
        light = (s * (H(eR) - h0) * -1 + (H(eU) - h0) * -1) * -3.0
        light = (h0 - H(eR)) * 0 + ((H(eR) - h0) * 1 + (h0 - H(eU)) * 1) * 3.0
        tone = ((sid * 2654435761) % 1000) / 1000 - .5
        g = fbm2(p, wz, seed + 12, 2, .375)
        speck = ((np.floor(p).astype(int) * 73856093 ^ np.floor(wz).astype(int) * 19349663 ^ seed) % 1000) / 1000
        base = {"py": 2.95, "px": 2.35}[face] + shift
        v = base + tone * .9 + light + (g - .5) * 1.8 + (speck > .95) - (speck < .06)
        mortar = e < mw
        outl = ~mortar & (e < mw + 1.2) & (fy > .5)
        v = np.where(outl, np.minimum(v, 1.0), v)
        fam = np.where(mortar, WARM, COOL)
        mv = 2.2 + shift * .5 + (worn - .5) * 2.5 + (g - .5) * 2 - (e < mw * .45) * 1.6 + (speck > .92) * 1.2
        v = np.where(mortar, mv, v)
        mm = (wz < 12) & mortar & (vnoise(p * .08, wz * .1, seed + 50) > .6)
        fam = np.where(mm, MOSS, fam); v = np.where(mm, 1.6, v)
        return fam, v
    return tex

def wall(cv, axis, fixed, a0, a1, height, holes=(), seed=0):
    tex = sett_wall(seed)
    def box(p0, p1, z0, z1, t0=-T, t1=0, tx=tex):
        if p1 <= p0 or z1 <= z0: return
        if axis == "y": cv.box(p0, p1, fixed + t0, fixed + t1, z0, z1, tx)
        else: cv.box(fixed + t0, fixed + t1, p0, p1, z0, z1, tx)
    cuts = sorted({a0, a1} | {h[0] for h in holes} | {h[1] for h in holes})
    for p0, p1 in zip(cuts, cuts[1:]):
        hs = [h for h in holes if h[0] <= p0 and h[1] >= p1]
        if not hs: box(p0, p1, 0, height); continue
        _, _, z0, z1, d = hs[0]
        box(p0, p1, 0, z0); box(p0, p1, z1, height)
        box(p0, p1, z0, z1, -T, -d, shadow_tex(.2)); box(p0, p1, z0 - 2, z0, -d, 2, shadow_tex(1.2))

def arch_y(cv, cx, ywall, lights, half=20, post=32, ring=9, jamb=9):
    """arco arredondado (estilo da 1ª versão) numa parede do fundo; vão estreito"""
    x0, x1 = cx - half, cx + half
    vous = dressed(1.0, 5, 6, 2)
    for xx in np.arange(x0 - jamb, x1 + jamb, 1.0):
        a = xx + .5 - cx
        if abs(a) < half:
            top = post + np.sqrt(max(half * half - a * a, 0))
            cv.box(xx, xx + 1, ywall - T, ywall + 2, top, top + ring, vous, step=.5)
            cv.box(xx, xx + 1, ywall - T, ywall, top + ring, WH, sett_wall(5))
        else:
            cv.box(xx, xx + 1, ywall - T, ywall + 2, 0, post + 4, dressed(.5, 9, 40, 3))
            cv.box(xx, xx + 1, ywall - T, ywall, post + 4, WH, sett_wall(5))
    cv.box(x0, x1, ywall - T, ywall + 3, 0, 1.5, dressed(-.2, 10, 44, 4))     # soleira
    for sx in (x0 - jamb - 8, x1 + jamb + 8):
        sconce(cv, sx, ywall + 3, 40, lights)

def front_gap(cv, yfront, x0, x1):
    """passagem na parede da frente (cortada): ombreiras baixas de pedra aparelhada"""
    for (a0, a1) in [(x0 - 10, x0), (x1, x1 + 10)]:
        cv.box(a0, a1, yfront, yfront + T + 3, 0, FH + 16, dressed(.4, 9, 12, 4))

# ---------- caixões de cenário ----------
def wood_tex(shift=0.0, axis="y"):
    def tex(face, wx, wy, wz, a, b, B):
        base = {"top": 4.3, "py": 3.7, "px": 2.9}[face] + shift
        along = wy if axis == "y" else wx
        g = (vnoise(along * .12, (wx if axis == "y" else wy) * .9 + wz * .9, 31) - .5) * 1.4
        v = base + g
        if face == "top":
            across = (wx if axis == "y" else wy)
            v = v - ((across % 6) < .7) * 1.2
        else:
            v = v - ((wz % 5) < .7) * 1.0
        return np.full(v.shape, WARM), v
    return tex

def coffin_profile(t):
    """meia-largura do caixão de madeira (forma de losango alongado) ao longo do comprimento t∈[0,1] (0 = cabeça)"""
    return np.where(t < .28, 7 + (t / .28) * 4, 11 - (t - .28) / .72 * 5)

def wood_coffin(cv, cx, cy, axis="y", state="closed", seed=0, L=56, H=13):
    head = -L / 2
    for k in np.arange(0, L, 2.0):
        t = (k + 1) / L; w = float(coffin_profile(t))
        a0, a1 = head + k, head + k + 2
        if axis == "y": cv.box(cx - w, cx + w, cy + a0, cy + a1, 0, H, wood_tex(-.6, "y"), step=.5)
        else: cv.box(cx + a0, cx + a1, cy - w, cy + w, 0, H, wood_tex(-.6, "x"), step=.5)
        lid = state == "closed" or (state == "broken" and t > .55)
        if lid:
            if axis == "y": cv.box(cx - w - 1, cx + w + 1, cy + a0, cy + a1, H, H + 3, wood_tex(-.2, "y"), step=.5)
            else: cv.box(cx + a0, cx + a1, cy - w - 1, cy + w + 1, H, H + 3, wood_tex(-.2, "x"), step=.5)
        elif state in ("broken", "open"):
            inner = max(w - 2, 1)
            if axis == "y": cv.box(cx - inner, cx + inner, cy + a0, cy + a1, H - .5, H, shadow_tex(-.4), step=.5)
            else: cv.box(cx + a0, cx + a1, cy - inner, cy + inner, H - .5, H, shadow_tex(-.4), step=.5)
    if state in ("broken", "open"):
        hx, hy = (cx, cy + head + 12) if axis == "y" else (cx + head + 12, cy)
        cv.sprite(SKULL, SKULL_C, hx, hy, H - 4, depth_bias=8)
    rng = np.random.default_rng(seed)
    if state == "open":       # tampa largada no chão ao lado
        ox, oy = (cx + 26, cy + 6) if axis == "y" else (cx + 6, cy + 26)
        if axis == "y": cv.box(ox - 9, ox + 9, oy - L / 2, oy + L / 2, 0, 3, wood_tex(-.4, "y"))
        else: cv.box(ox - L / 2, ox + L / 2, oy - 9, oy + 9, 0, 3, wood_tex(-.4, "x"))
    if state == "broken":     # tábuas soltas
        for k in range(3):
            bx, by = cx + rng.uniform(-24, 24), cy + rng.uniform(-30, 30)
            ln = rng.uniform(10, 20)
            if rng.random() < .5: cv.box(bx, bx + ln, by, by + 3, 0, 1.5, wood_tex(-.5, "x"), step=.5)
            else: cv.box(bx, bx + 3, by, by + ln, 0, 1.5, wood_tex(-.5, "y"), step=.5)

def stone_coffin(cv, cx, cy, axis="y", state="closed"):
    w, l = 12, 28
    if axis == "x": W_, L_ = l, w
    else: W_, L_ = w, l
    cv.box(cx - W_ - 3, cx + W_ + 3, cy - L_ - 3, cy + L_ + 3, 0, 3, sarc(2))
    cv.box(cx - W_, cx + W_, cy - L_, cy + L_, 3, 21, sarc(2.5, cavity=False))
    if state == "closed":
        cv.box(cx - W_ - 1.5, cx + W_ + 1.5, cy - L_ - 1.5, cy + L_ + 1.5, 21, 25, sarc(2))
    else:   # tampa partida: metade caída no chão
        if axis == "y":
            cv.box(cx - W_ - 1.5, cx + W_ + 1.5, cy, cy + L_ + 1.5, 21, 25, sarc(2))
            cv.box(cx + W_ + 4, cx + W_ + 26, cy - L_, cy - 4, 0, 4, sarc(2))
        else:
            cv.box(cx, cx + L_ + 1.5, cy - W_ - 1.5, cy + W_ + 1.5, 21, 25, sarc(2))
            cv.box(cx - W_ - 2, cx + 4, cy + W_ + 4, cy + W_ + 26, 0, 4, sarc(2))
        cv.box(cx - W_ + 2.5, cx + W_ - 2.5, cy - L_ + 2.5, cy - 2 if axis == "y" else cy + L_ - 2.5, 20.5, 21, shadow_tex(-.5))
        for k in range(4):
            cv.box(cx + W_ + 6 + k * 4, cx + W_ + 9 + k * 4, cy + 10 + (k % 2) * 6, cy + 13 + (k % 2) * 6, 0, 2.5, rubble_tex(500 + k), step=.34)

def virgil_coffin(cv, cx, cy, lights):
    w, l = 12, 29
    cv.box(cx - w - 6, cx + w + 6, cy - l - 6, cy + l + 6, 0, 3, sarc(2))           # degrau extra: ele fica num estrado
    cv.box(cx - w - 4, cx + w + 4, cy - l - 4, cy + l + 4, 3, 7, sarc(2))
    cv.box(cx - w, cx + w, cy - l, cy + l, 7, 27, sarc(2.5, cavity=True))
    cv.box(cx - w - 2, cx + w + 2, cy - l + 12, cy + l + 8, 27, 32, sarc(2, cross=True))
    for k, (dx, dy) in enumerate([(-28, -32), (28, -36), (-30, 20), (29, 30), (-6, 52), (8, 50)]):
        cluster(cv, cx + dx, cy + dy, lights, 40 + k)
    for (dx, dy) in [(-w - 4, -l - 4), (w + 4, -l - 4)]:       # velas altas nos cantos do estrado
        candle(cv, cx + dx, cy + dy, 14, lights, z=7, power=1.1, rad=40)

def carpet(face, wx, wy, wz, a, b, B):
    x0, x1 = B[0], B[1]
    e = np.minimum(wx - x0, x1 - wx)
    v = 1.7 + (vnoise(wx * .4, wy * .4, 88) - .5) * 1.2 - (e < 1.5) * .8 + ((e > 2.5) & (e < 3.5)) * .9
    return np.full(wx.shape, BLOOD), v


