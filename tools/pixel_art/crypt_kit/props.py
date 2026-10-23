"""Objetos soltos da cripta, posicionados em coordenadas locais do tile (0..64)."""
import numpy as np
from iso import FID
from materials import (sarc, stone_coffin, wood_coffin, candle, cluster, sconce as _sconce, carpet,
                       rubble_tex, BONES, BONES_C, SCONCE, SCONCE_C, FLAME, FLAME_C, wax)

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
