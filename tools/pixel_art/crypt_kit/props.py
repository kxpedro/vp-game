"""Objetos soltos da cripta, posicionados em coordenadas locais do tile (0..64)."""
import numpy as np
from iso import FID, vnoise
from materials import (sarc, stone_coffin, wood_coffin, candle, cluster, sconce as _sconce, carpet,
                       rubble_tex, BONES, BONES_C, SCONCE, SCONCE_C, FLAME, FLAME_C, wax,
                       stone_cyl, side_light, HEART, HEART_C, wood_tex,
                       SWORD, SWORD_C, AXE, AXE_C, WHIP, WHIP_C, REVOLVER, REVOLVER_C, x2)

def coffin_virgil(cv, axis, lights):
    """caixão do Virgil num estrado de dois degraus, tampa deslocada, forro vermelho e velas altas"""
    cx = cy = 32
    w, l = 12, 29
    W, L = (w, l) if axis == "y" else (l, w)
    cv.box(cx - W - 6, cx + W + 6, cy - L - 6, cy + L + 6, 0, 3, sarc(2))
    cv.box(cx - W - 4, cx + W + 4, cy - L - 4, cy + L + 4, 3, 7, sarc(2))
    cv.box(cx - W, cx + W, cy - L, cy + L, 7, 27, sarc(2.5, cavity=True))
    if axis == "y":
        cv.box(cx - W - 2, cx + W + 2, cy - L + 12, cy + L + 8, 27, 32, sarc(2, cross=True))
        corners = [(-W - 4, -L - 4), (W + 4, -L - 4)]
    else:
        cv.box(cx - W + 12, cx + W + 8, cy - L - 2, cy + L + 2, 27, 32, sarc(2, cross=False))
        corners = [(-W - 4, -L - 4), (-W - 4, L + 4)]
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
