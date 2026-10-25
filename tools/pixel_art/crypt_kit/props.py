"""Objetos soltos da cripta, posicionados em coordenadas locais do tile (0..64)."""
import numpy as np
from iso import FID, vnoise
from materials import (SKULL, SKULL_C, sarc, stone_coffin, wood_coffin, candle, cluster, sconce as _sconce, carpet,
                       rubble_tex, BONES, BONES_C, SCONCE, SCONCE_C, FLAME, FLAME_C, wax,
                       stone_cyl, side_light, HEART, HEART_C, wood_tex, grain, plain_stone,
                       iron, SWORD, SWORD_C, AXE, AXE_C, WHIP, WHIP_C, REVOLVER, REVOLVER_C, x2)

# Saída do Virgil ao renascer (CoffinRise.cs): deslocamento da tampa ao longo do caixão (0 = posição de sempre,
# entreaberta; -10 = fechada sobre o forro) e quanto ela sobe ao ser empurrada por dentro, quadro a quadro: duas
# pancadas, a tampa erguida desliza e cai de volta no lugar de sempre.
VIRGIL_LID_FRAMES = [(-10, 0), (-10, 3), (-10, 0), (-9, 5), (-6, 5), (-3, 4), (-1, 2), (0, 0)]

def _slid(tex, ox, oy):
    """textura presa à tampa: avalia nas coordenadas de antes do deslocamento, então o grão não escorrega"""
    def t(face, wx, wy, wz, a, b, B):
        return tex(face, wx - ox, wy - oy, wz, a, b, B)
    return t

def coffin_virgil(cv, axis, lights, lid=(0, 0)):
    """caixão do Virgil num estrado de dois degraus, tampa deslocada, forro vermelho e velas altas;
    lid = (deslocamento, pulo) da tampa ou None (sem tampa)"""
    cx = cy = 32
    w, l = 12, 29
    W, L = (w, l) if axis == "y" else (l, w)
    cv.box(cx - W - 6, cx + W + 6, cy - L - 6, cy + L + 6, 0, 3, sarc(2))
    cv.box(cx - W - 4, cx + W + 4, cy - L - 4, cy + L + 4, 3, 7, sarc(2))
    cv.box(cx - W, cx + W, cy - L, cy + L, 7, 27, sarc(2.5, cavity=True))
    if lid is not None:
        s, up = lid
        if axis == "y":
            cv.box(cx - W - 2, cx + W + 2, cy - L + 12 + s, cy + L + 8 + s, 27 + up, 32 + up, _slid(sarc(2, cross=True), 0, s))
        else:
            cv.box(cx - W + 12 + s, cx + W + 8 + s, cy - L - 2, cy + L + 2, 27 + up, 32 + up, _slid(sarc(2, cross=False), s, 0))
    corners = [(-W - 4, -L - 4), (W + 4, -L - 4)] if axis == "y" else [(-W - 4, -L - 4), (-W - 4, L + 4)]
    for (dx, dy) in corners:
        candle(cv, cx + dx, cy + dy, 14, lights, z=7, power=1.1, rad=40)

def coffin_stone(cv, axis, state, lights): stone_coffin(cv, 32, 32, axis, state)
def coffin_wood(cv, axis, state, lights, seed=0): wood_coffin(cv, 32, 32, axis, state, seed)

def candles(cv, size, seed, lights):
    n, sp = {"small": ((2, 4), 4), "medium": ((3, 6), 6), "large": ((7, 11), 9)}[size]
    cluster(cv, 32, 32, lights, seed, n, spread=sp)

def candle_tall(cv, lights): candle(cv, 32, 32, 16, lights, power=1.1, rad=40)

def sconce(cv, side, lights):
    if side == "ne": _sconce(cv, 32, 3, 40, lights)
    else: _sconce(cv, 3, 32, 40, lights)

def carpet_runner(cv, axis, lights):
    if axis == "y": cv.box(22, 42, 0, 64, 0, .8, carpet)
    else:
        def tex(face, wx, wy, wz, a, b, B):
            f, v = carpet(face, wy, wx, wz, b, a, (B[2], B[3], B[0], B[1], B[4], B[5]))
            return f, v
        cv.box(0, 64, 22, 42, 0, .8, tex)

def bones(cv, lights): cv.sprite(BONES, BONES_C, 32, 32, 0, depth_bias=3)

def rubble(cv, seed, lights):
    r = np.random.default_rng(seed)
    for k in range(r.integers(4, 8)):
        x, y, s = 32 + r.uniform(-14, 14), 32 + r.uniform(-14, 14), r.uniform(2.5, 6)
        cv.box(x, x + s, y, y + s * r.uniform(.6, 1.3), 0, s * r.uniform(.4, .8), rubble_tex(seed * 10 + k), step=.34)

# ---------- fonte de sangue (concept hub_cripta_v2, escala ~0,75) ----------
FOUNTAIN_FRAMES = 8

def _pool(cx, cy, t, seed=77):
    """superfície do sangue: ondas saindo do centro e manchas que giram devagar; t em 0..1 fecha o laço"""
    ang = t * 2 * np.pi
    ox, oy = np.cos(ang) * 6, np.sin(ang) * 6
    def tex(face, wx, wy, wz, th, rr):
        ring = np.sin(rr * .5 - ang * 2) * .5
        v = .9 + ring * .7 + (vnoise((wx + ox) * .15, (wy + oy) * .15, seed) - .5) * 1.1
        hi = (vnoise((wx - ox) * .3, (wy - oy) * .3, seed + 1) > .8) & (rr > 6)
        v = np.where(hi, 2.6, v)
        return np.full(np.shape(v), FID["blood"]), v
    return tex

def _stream(shift, density, seed, t, period=12):
    """pedra com filetes de sangue escorrendo: o brilho de cada filete desce um período a cada volta"""
    base = stone_cyl(shift)
    def tex(face, wx, wy, wz, th, rr):
        f, v = base(face, wx, wy, wz, th, rr)
        if face == "side":
            streak = vnoise(th * 9, np.zeros_like(th), seed) + (vnoise(th * 30, np.zeros_like(th), seed + 1) - .5) * .6
            m = streak > 1 - density
            drop = ((wz + t * period + vnoise(th * 30, np.zeros_like(th), seed + 2) * period) % period) < 2.5
            f = np.where(m, FID["blood"], f)
            v = np.where(m, 1.6 + (side_light(th) - 2.95) * 2 + drop * 1.1, v)
        return f, v
    return tex

def blood_fountain(cv, lights, t=0.0):
    """tanque baixo de pedra, coluna com taça e coração de pedra de onde o sangue brota (sem velas: o layout põe)"""
    cx = cy = 32
    cv.cyl(cx, cy, 62, 0, 5, stone_cyl(.0, frame=3))
    cv.cyl(cx, cy, 56, 5, 22, stone_cyl(.3, frame=3), r_in=48)
    cv.cyl(cx, cy, 48, 5, 22, stone_cyl(-1.2), top=False)              # parede interna do tanque
    cv.cyl(cx, cy, 48, 5, 17, _pool(cx, cy, t), side=False)
    cv.cyl(cx, cy, 11, 17, 45, _stream(.2, .35, 3, t))
    cv.cyl(cx, cy, 15, 45, 48, stone_cyl(.4))
    cv.cyl(cx, cy, 25, 48, 57, _stream(.3, .55, 9, t, period=8), r_in=20)
    cv.cyl(cx, cy, 20, 48, 55, _pool(cx, cy, t + .5, 91), side=False)
    cv.cyl(cx, cy, 5, 55, 66, _stream(.1, .7, 5, t, period=6))
    beat = 1 if (t * FOUNTAIN_FRAMES) % FOUNTAIN_FRAMES in (0, 1) else 0   # pulso do coração nos 2 primeiros quadros
    cv.sprite(HEART if not beat else HEART_BEAT, HEART_C, cx + 1, cy + 1, 63, depth_bias=10)
    lights.append((cx, cy, 60, .8, 70))

HEART_BEAT = [r.replace("o", "O") for r in HEART]

def _wide(rows): return ["".join(c * 2 for c in r) for r in rows]   # só a largura em dobro (cabe no armário baixo)

# ---------- bancada de armas (concept hub_cripta_v2, na altura das paredes do kit) ----------
def weapon_bench(cv, lights, X=0, y0=-56, y1=72):
    """armário de armas encostado na parede NW do tile (plano x = X), de y0 a y1, com a mesa de trabalho na frente"""
    d, H = 12, 62                                                      # fundo do armário e altura (paredes = 76)
    cv.box(X, X + d, y0, y1, 0, 5, wood_tex(-.4))                      # rodapé
    cv.box(X, X + 3, y0, y1, 5, H, wood_tex(-1.0))                     # fundo
    for yy in (y0, (y0 + y1) / 2 - 2, y1 - 5):
        cv.box(X, X + d, yy, yy + 5, 5, H, wood_tex(0))                # montantes
    cv.box(X, X + d + 3, y0 - 2, y1 + 2, H, H + 5, wood_tex(.2))       # topo
    cv.box(X, X + 5, y0, y1, 16, 19, wood_tex(-.2)); cv.box(X, X + 5, y0, y1, 46, 49, wood_tex(-.2))  # travessas
    for (spr, col, off) in [(SWORD, SWORD_C, 14), (AXE, AXE_C, 40), (SWORD, SWORD_C, 78), (SWORD, SWORD_C, 104)]:
        cv.sprite(_wide(spr), col, X + 8, y0 + off, 21, depth_bias=40)
    for k in range(4):
        candle(cv, X + 6, y0 + 8 + k * (y1 - y0 - 16) / 3, 4 + (k * 5) % 7, lights, z=H + 5)
    # mesa de trabalho na frente
    tx0, tx1, ty0, ty1, th = X + 30, X + 60, y0 + 8, y1 - 8, 24
    for (lx, ly) in [(tx0 + 2, ty0 + 2), (tx1 - 5, ty0 + 2), (tx0 + 2, ty1 - 5), (tx1 - 5, ty1 - 5)]:
        cv.box(lx, lx + 3, ly, ly + 3, 0, th - 3, wood_tex(-.5))
    cv.box(tx0, tx1, ty0, ty1, th - 3, th, wood_tex(.3))
    cv.sprite(x2(REVOLVER), REVOLVER_C, (tx0 + tx1) / 2, ty0 + 22, th, depth_bias=6)
    cv.sprite(x2(REVOLVER), REVOLVER_C, (tx0 + tx1) / 2 + 3, ty0 + 40, th, depth_bias=6)
    cv.sprite(x2(WHIP), WHIP_C, (tx0 + tx1) / 2, ty1 - 18, th, depth_bias=6)
    candle(cv, tx0 + 6, ty0 + 6, 7, lights, z=th); candle(cv, tx0 + 9, ty0 + 10, 4, lights, z=th)
    candle(cv, tx1 - 7, ty1 - 7, 6, lights, z=th)

# ---------- bebedouro de sangue (sala da primeira herança; referência: bebedouro de jardim em pedestal) ----------
def _lathe_stone(shift=0.0, flutes=0):
    """pedra torneada: luz do lado, caneluras verticais (folhas do pedestal) quando flutes > 0"""
    def tex(face, wx, wy, wz, th, rr):
        if face == "top":
            v = 3.6 + shift + grain(wx, wy, wz, .5)
        else:
            v = side_light(th) + shift + grain(wx, wy, wz, .5)
            if flutes:
                u = (th / (2 * np.pi) * flutes) % 1
                v = v + np.where(u < .18, -1.1, 0) + np.where((u > .45) & (u < .6), .6, 0)
        return np.full(np.shape(v), FID["cool"]), v
    return tex

# perfil do pedestal: (z0, z1, raio embaixo, raio em cima, caneluras)
BASIN_PROFILE = [
    (7, 9, 11, 10, 0), (9, 11, 9, 9, 0), (11, 13, 10, 10, 0),         # anéis sobre a base
    (13, 33, 6, 8, 10),                                                # bulbo com folhas
    (33, 36, 10, 10, 0), (36, 40, 6, 5, 0),                            # colar e pescoço
]
BASIN_RIM = 48                                                         # altura da borda da taça

def blood_basin(cv, lights, t=0.0):
    """bebedouro: base quadrada, pedestal torneado e taça larga e rasa de borda virada, cheia de sangue"""
    cx = cy = 32
    cv.box(cx - 14, cx + 14, cy - 14, cy + 14, 0, 4, plain_stone(.1, frame=2))
    cv.box(cx - 11, cx + 11, cy - 11, cy + 11, 4, 7, plain_stone(.3, frame=2))
    for (z0, z1, r0, r1, fl) in BASIN_PROFILE:
        for z in np.arange(z0, z1, 1.0):
            r = r0 + (r1 - r0) * ((z - z0) / max(z1 - z0, 1))
            if fl: r = r + 2.6 * np.sin((z - z0) / (z1 - z0) * np.pi)     # barriga do bulbo
            cv.cyl(cx, cy, r, z, z + 1, _lathe_stone(0, fl), top=(z == z1 - 1))
    R = BASIN_RIM
    for z in np.arange(40, R - 2, 1.0):                                # taça: abre de 5 a 21
        r = 5 + 16 * ((z - 40) / (R - 42)) ** .6
        cv.cyl(cx, cy, r, z, z + 1, _lathe_stone(-.2 + (z - 40) * .08), top=False)
    cv.cyl(cx, cy, 23, R - 2, R, _lathe_stone(.5), r_in=19)            # borda virada
    cv.cyl(cx, cy, 19, R - 4, R, stone_cyl(-1.2), top=False)            # parede interna
    cv.cyl(cx, cy, 19, R - 4, R - 1.5, _pool(cx, cy, t, 63), side=False)
    for k, ang in enumerate([1.9, 1.1, -.2]):                           # pingos escorrendo pela borda
        x, y = cx + np.cos(ang) * 22.6, cy + np.sin(ang) * 22.6
        drop = 3 * ((t * 8 + k * 3) % 8) / 8
        cv.box(x - .6, x + .6, y - .6, y + .6, R - 4 - drop, R - 1, _blood_flat, step=.34)
    lights.append((cx, cy, R + 6, .6, 50))

def _blood_flat(face, wx, wy, wz, a, b, B): return np.full(wx.shape, FID["blood"]), np.full(wx.shape, 2.4)

# ---------- Câmara de Hemomancia: altar do sacrifício e bancada de artefatos ----------
def _blood_tex(v0=1.4, seed=41, emit_hi=False):
    """sangue parado (poça, canaletas): manchas e brilhos pontuais"""
    def tex(face, wx, wy, wz, a, b, B):
        v = v0 + (vnoise(wx * .25, wy * .25 + wz * .3, seed) - .5) * 1.4
        v = np.where(vnoise(wx * .6, wy * .6, seed + 1) > .82, 3.0, v)
        return np.full(np.shape(v), FID["blood"]), v
    return tex

def _altar_slab(cv, x0, x1, y0, y1, z0, z1):
    """tampo do altar: pedra emoldurada com canaletas de sangue em cruz e uma poça no centro"""
    stone = plain_stone(.2, frame=2)
    def tex(face, wx, wy, wz, a, b, B):
        f, v = stone(face, wx, wy, wz, a, b, B)
        if face != "top":
            # sangue escorrendo pelas faces da frente, saindo das canaletas
            along = wx if face == "py" else wy
            mid = (x0 + x1) / 2 if face == "py" else (y0 + y1) / 2
            run = (np.abs(along - mid) < 1.3) & (wz > z1 - 3 - (vnoise(along * .7, 0 * along, 5) * 3))
            f = np.where(run, FID["blood"], f); v = np.where(run, 2.6, v)
            return f, v
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        ch = ((np.abs(wx - cx) < 1.6) & (np.abs(wy - cy) < (y1 - y0) / 2 - 3)) | ((np.abs(wy - cy) < 1.6) & (np.abs(wx - cx) < (x1 - x0) / 2 - 3))
        pool = np.hypot(wx - cx, (wy - cy) * .8) < 5.5
        stain = vnoise(wx * .2, wy * .2, 47) > .72
        m = ch | pool | stain
        f = np.where(m, FID["blood"], f)
        v = np.where(m, np.where(pool, 3.2, 2.2) + (vnoise(wx * .5, wy * .5, 48) - .5), v)
        v = np.where((ch | pool) & ~m, v, v)
        return f, v
    cv.box(x0, x1, y0, y1, z0, z1, tex)

DAGGER = ["   o   ", "   O   ", "  .O.  ", "  .O.  ", "  .O.  ", "  .O.  ", "  .O.  ", "  .O.  ", "ggggggg", "  ggg  ", "   w   ", "   w   ", "   w   ", "  ggg  "]
DAGGER_C = {"o": ("cool", 7), "O": ("cool", 7), ".": ("cool", 5), "g": ("warm", 7), "w": ("wood", 4)}

def _sigil(cv, x, y0, y1, z0, z1):
    """sigilo aceso gravado na face +x de uma pedra (plano x): círculo, gota de sangue e raios, com brilho próprio"""
    cy, cz = (y0 + y1) / 2, (z0 + z1) / 2
    R = min(y1 - y0, z1 - z0) / 2 - 1
    Y, Z = np.meshgrid(np.arange(y0, y1, .3) + .15, np.arange(z0, z1, .3) + .15)
    dy, dz = Y - cy, Z - cz
    r = np.hypot(dy, dz * .9)
    ring = np.abs(r - R) < 1.0
    inner = np.abs(r - R * .62) < .7
    # gota: círculo embaixo e ponta em cima
    drop = (np.hypot(dy, dz + R * .12) < R * .3) | ((dz > -R * .12) & (dz < R * .48) & (np.abs(dy) < (R * .48 - dz) * .55))
    rays = np.zeros_like(ring)
    for k in range(8):
        a = k * np.pi / 4 + np.pi / 8
        u, w = dy * np.cos(a) + dz * np.sin(a), -dy * np.sin(a) + dz * np.cos(a)
        rays |= (np.abs(w) < .55) & (u > R * .66) & (u < R - .5)
    m = ring | inner | drop | rays
    v = np.where(drop | ring, 4.0, 3.0)
    Ym, Zm, vm = Y[m], Z[m], v[m]
    X = np.full_like(Ym, x + .02)
    cv.plot(X, Ym, Zm, np.full(Ym.shape, FID["blood"]), vm, emit=True, depth_bias=.5)

def sacrifice_altar(cv, lights):
    """altar de pedra num estrado de dois degraus, com estela do sigilo de sangue atrás (lado -x, encostada na
    parede NW do braço da câmara), canaletas e poça de sangue no tampo, cálice e adaga ritual"""
    cx = cy = 32
    cv.box(cx - 30, cx + 30, cy - 30, cy + 30, 0, 4, sarc(2))                     # degrau de baixo
    cv.box(cx - 24, cx + 24, cy - 24, cy + 24, 4, 8, sarc(2))                     # degrau de cima
    # estela atrás do altar, com o sigilo aceso na face voltada para a sala (+x)
    sx0, sx1 = cx - 27, cx - 18
    cv.box(sx0, sx1, cy - 18, cy + 18, 8, 86, sarc(2.5))
    cv.box(sx0 - 2, sx1 + 2, cy - 20, cy + 20, 86, 91, sarc(2))                   # coroamento
    cv.box(sx0 + 1, sx1 + 1, cy - 7, cy + 7, 91, 98, sarc(1.5))
    _sigil(cv, sx1, cy - 15, cy + 15, 46, 80)
    # altar: corpo com cantoneiras e tampo com as canaletas
    ax0, ax1, ay0, ay1 = cx - 12, cx + 14, cy - 20, cy + 20
    cv.box(ax0, ax1, ay0, ay1, 8, 28, sarc(2.5))
    for (px, py) in [(ax0, ay0), (ax1 - 4, ay0), (ax0, ay1 - 4), (ax1 - 4, ay1 - 4)]:
        cv.box(px - 1, px + 5, py - 1, py + 5, 8, 30, plain_stone(.6, frame=1))
    cv.sprite(SKULL, SKULL_C, cx + 1, ay1 + 1, 12, depth_bias=6)                  # caveira na face da frente
    _sigil(cv, ax1 + 1, cy - 8, cy + 8, 11, 26)                                    # sigilo menor na face +x
    _altar_slab(cv, ax0 - 2, ax1 + 2, ay0 - 2, ay1 + 2, 28, 32)
    # cálice dourado de sangue e adaga
    gx, gy = ax0 + 6, ay0 + 8
    gold = lambda face, wx, wy, wz, th, rr: (np.full(np.shape(wx), FID["warm"]), (7.6 if face == "top" else side_light(th) + 4.4) + grain(wx, wy, wz, .4))
    cv.cyl(gx, gy, 3, 32, 33.5, gold)
    cv.cyl(gx, gy, 1.1, 33.5, 38, gold)
    cv.cyl(gx, gy, 4, 38, 42, gold, top=False)
    cv.cyl(gx, gy, 3.3, 38, 41.5, _pool(gx, gy, 0.0, 29), side=False)
    cv.sprite(DAGGER, DAGGER_C, ax1 - 2, ay1 - 12, 32, depth_bias=8)
    # sangue escorrido no estrado (na frente do altar)
    cv.box(ax1 + 2, ax1 + 9, cy - 3, cy + 4, 8, 8.4, _blood_tex(1.6), step=.34)
    cv.box(ax1 + 9, ax1 + 16, cy - 1, cy + 3, 4, 4.4, _blood_tex(1.4, 43), step=.34)
    cv.box(ax0 + 4, ax1 - 4, ay1 + 2, ay1 + 8, 8, 8.4, _blood_tex(1.3, 44), step=.34)
    # velas nos cantos do estrado e ao pé da estela
    for (dx, dy, h) in [(-26, -26, 12), (-26, 26, 9), (26, -26, 7), (26, 26, 10)]:
        candle(cv, cx + dx, cy + dy, h, lights, z=4, power=1.0, rad=38)
    for (dy, h) in [(-13, 6), (13, 8)]:
        candle(cv, sx1 + 3, cy + dy, h, lights, z=8, power=.8, rad=30)
    lights.append((cx, cy, 44, .7, 60))

def _velvet(shift=0.0, axis="x"):
    """veludo vermelho com galão dourado nas bordas (toalha do balcão)"""
    def tex(face, wx, wy, wz, a, b, B):
        x0, x1, y0, y1, z0, z1 = B
        v = {"top": 2.6, "py": 2.2, "px": 1.6}[face] + shift + (vnoise(wx * .3, wy * .3 + wz * .4, 61) - .5) * .9
        f = np.full(np.shape(v), FID["blood"])
        if face == "top":
            e = np.minimum(np.minimum(wx - x0, x1 - wx), np.minimum(wy - y0, y1 - wy))
        else:
            e = wz - z0                                                    # barra de baixo da toalha
            along = wx if face == "py" else wy
            v = v - ((along % 8) < 1.2) * .9                               # dobras
        trim = (e > .6) & (e < 2.0)
        f = np.where(trim, FID["warm"], f); v = np.where(trim, 7.4, v)
        return f, v
    return tex

def _gem(cv, x, y, z, fam, idx, r=2.6):
    """joia lapidada com brilho próprio: losango com faceta clara em cima"""
    rows = ["  a  ", " aba ", "abbba", " aca ", "  a  "] if r < 3 else ["   a   ", "  aba  ", " abbba ", "abbcbba", " abbba ", "  aca  ", "   a   "]
    cv.sprite(rows, {"a": (fam, idx, True), "b": (fam, idx + 1, True), "c": ("flame", 4, True)}, x, y, z, depth_bias=10)

AMULET = [" ggg ", "g   g", "g   g", " g g ", "  r  ", " rRr ", "  r  "]
AMULET_C = {"g": ("warm", 8), "r": ("blood", 3, True), "R": ("blood", 4, True)}
BOOK = ["bbbbbbb", "bpppppb", "bpppppb", "bpppppb", "bbbbbbb"]
BOOK_C = {"b": ("blood", 2), "p": ("warm", 8)}
COINS = ["  ggg  ", " gGGGg ", "gGGgGGg", "ggggggg", " gGgGg ", "ggggggg"]
COINS_C = {"g": ("warm", 6), "G": ("warm", 8)}

ARTIFACT_COUNTER_CANVAS = (256, 224, 120, 136)

def artifact_counter(cv, lights, X0=-36, X1=100, Y=0):
    """bancada de artefatos encostada na parede NE do tile (plano y = Y), de X0 a X1: armário-relicário com nichos
    e relíquias no fundo e balcão com toalha de veludo na frente (relicários, joias, balança e moedas)"""
    d, H = 12, 60
    # armário do fundo: rodapé, costas, montantes, prateleiras e topo
    cv.box(X0, X1, Y, Y + d, 0, 5, wood_tex(-.4, "x"))
    cv.box(X0, X1, Y, Y + 3, 5, H, wood_tex(-1.0, "x"))
    posts = [X0, X0 + (X1 - X0) / 3 - 2, X0 + 2 * (X1 - X0) / 3 - 3, X1 - 5]
    for xx in posts:
        cv.box(xx, xx + 5, Y, Y + d, 5, H, wood_tex(0, "x"))
    cv.box(X0 - 2, X1 + 2, Y, Y + d + 3, H, H + 5, wood_tex(.2, "x"))
    for z in (24, 42):
        cv.box(X0, X1, Y, Y + d, z, z + 2, wood_tex(-.1, "x"))
    # relíquias nas prateleiras (uma por nicho e por andar)
    bays = [(posts[i] + 5, posts[i + 1]) for i in range(3)]
    for k, (a, b) in enumerate(bays):
        mx = (a + b) / 2
        if k == 0:
            cv.sprite(SKULL, SKULL_C, mx, Y + 7, 44, depth_bias=30)
            for j in range(3):                                                  # frascos de sangue
                cv.cyl(a + 7 + j * 9, Y + 7, 3.2, 26, 33 + j * 2, _lathe_stone(.3 - j * .2))
                cv.cyl(a + 7 + j * 9, Y + 7, 2.4, 27, 32 + j * 2, lambda f, wx, wy, wz, t, r: (np.full(np.shape(wx), FID["blood"]), 3.4 + 0 * wx), emit=True, side=False)
        elif k == 1:
            _gem(cv, mx, Y + 7, 46, "wet", 1, r=4)
            cv.box(mx - 7, mx + 7, Y + 3, Y + 10, 26, 33, wood_tex(.3, "x"))       # caixinha
            cv.box(mx - 7, mx + 7, Y + 3, Y + 10, 33, 35, iron)
        else:
            cv.sprite(AMULET, AMULET_C, mx, Y + 7, 45, depth_bias=30)
            cv.sprite(BOOK, BOOK_C, mx, Y + 8, 27, depth_bias=30)
    for xx in (posts[1] + 2, posts[2] + 2):
        candle(cv, xx + 1, Y + 7, 6, lights, z=H + 5, power=.8, rad=30)
    # balcão na frente: pedra com tampo de madeira coberto pela toalha
    ty0, ty1, th = Y + 24, Y + 44, 26
    tx0, tx1 = X0 + 6, X1 - 6
    cv.box(tx0, tx1, ty0, ty1, 0, th - 6, sarc(2))
    cv.box(tx0 - 2, tx1 + 2, ty0 - 2, ty1 + 2, th - 6, th - 3, wood_tex(.2, "x"))
    cv.box(tx0 - 1, tx1 + 1, ty0 - 1, ty1 + 1, th - 3, th, _velvet())
    cv.box(tx0 + 14, tx1 - 14, ty1 + 1, ty1 + 1.6, th - 14, th, _velvet(-.2))      # aba da toalha na frente
    # mercadorias: almofadas com joias, relicário dourado no meio e balança de latão
    def brass(face, wx, wy, wz, *rest):
        v = {"top": 7.6, "py": 6.6, "px": 5.4, "side": 6.6}[face] + grain(wx, wy, wz, .4)
        if len(rest) == 3 and face != "side":                             # caixas: friso escuro e cravos
            a, b, B = rest
            x0, x1, y0, y1, z0, z1 = B
            dims = {"top": (x1 - x0, y1 - y0), "py": (x1 - x0, z1 - z0), "px": (y1 - y0, z1 - z0)}[face]
            e = np.minimum(np.minimum(a, dims[0] - a), np.minimum(b, dims[1] - b))
            v = v - np.where((e > 1.2) & (e < 2.2), 2.6, 0) - np.where(e > 4, 1.2, 0)
        return np.full(np.shape(wx), FID["warm"]), v
    for ix, fam, idx in [(tx0 + 16, "wet", 1), (tx0 + 72, "moss", 3)]:
        cv.box(ix - 7, ix + 7, ty0 + 4, ty0 + 16, th, th + 3, _velvet(.6))
        _gem(cv, ix, ty0 + 10, th + 3, fam, idx, r=4)
    rx = tx0 + 44                                                               # relicário com a joia de sangue
    cv.box(rx - 8, rx + 8, ty0 + 4, ty0 + 15, th, th + 9, brass)
    cv.box(rx - 9, rx + 9, ty0 + 3, ty0 + 16, th + 9, th + 11, brass)
    cv.box(rx - 4, rx + 4, ty0 + 6, ty0 + 13, th + 11, th + 13, _velvet(.4))
    _gem(cv, rx, ty0 + 9.5, th + 13, "blood", 3, r=4)
    lights.append((rx, ty0 + 10, th + 18, .5, 30))
    bx = tx1 - 14
    cv.box(bx - 4, bx + 4, ty0 + 6, ty0 + 14, th, th + 2, brass)
    cv.box(bx - 1, bx + 1, ty0 + 9, ty0 + 11, th + 2, th + 20, brass)
    cv.box(bx - 9, bx + 9, ty0 + 9.5, ty0 + 10.5, th + 19, th + 20.5, brass)
    for sx in (bx - 9, bx + 9):
        cv.box(sx - .4, sx + .4, ty0 + 9.6, ty0 + 10.4, th + 12, th + 19, iron, step=.34)
        cv.cyl(sx, ty0 + 10, 4, th + 11, th + 12.5, brass)
    cv.sprite(COINS, COINS_C, tx0 + 60, ty1 - 4, th, depth_bias=6)
    cv.sprite(COINS, COINS_C, tx0 + 31, ty1 - 3, th, depth_bias=6)
    candle(cv, tx0 + 4, ty0 + 4, 8, lights, z=th, power=1.0, rad=36)
    candle(cv, tx1 - 4, ty1 - 5, 6, lights, z=th, power=1.0, rad=36)
    lights.append(((X0 + X1) / 2, Y + 34, th + 20, .6, 60))
