"""Peças do kit do Jardim Sangrento (mapa 2): sebes, topiarias, canteiros, lanternas, árvore morta e o chão de cascalho.

Mesmo renderizador e mesmas convenções do kit da cripta (tools/pixel_art/crypt_kit): tile = 64 unidades, paredes na
borda do tile com espessura T, paredes do fundo (ne/nw) altas e as da frente (se/sw) cortadas. As paredes têm os mesmos
nomes das peças da cripta, então o PixelRoomComposer monta a sala do Jardim com a mesma lógica (só troca a pasta do kit).
O arco tem o mesmo vão do arco da cripta (meia-largura 20, pilar 32): a grade do portão (props/portcullis_*) é a da cripta.
"""
import os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import iso
from iso import FID, vnoise
from walls import _box, P, T, WH

# Sebe: rampa verde-escura da AAP-64 (a "moss" da cripta mais um verde claro no topo). No jogo, as cores comuns caem
# na família moss do PixelRoomComposer e o verde claro, na família hedge.
if "hedge" not in iso.FAM:
    iso.FAM["hedge"] = "060608 141013 122020 24523b 23674e 328464 5daf8d".split()
    iso.FNAMES.append("hedge"); iso.FID["hedge"] = len(iso.FNAMES) - 1
HEDGE, MOSS, WARM, COOL, BLOOD, IRON = (FID[n] for n in ("hedge", "moss", "warm", "cool", "blood", "iron"))

LH = 16          # altura da sebe cortada da frente (se/sw) e da sebe baixa do fundo
BH = 34          # altura dos blocos de sebe do miolo do labirinto (o Virgil passa atrás e a cabeça aparece)


def pnoise2(x, y, px, py, seed):
    """value noise periódico nos dois eixos (px, py células de rede por período)"""
    xi, yi = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - xi, y - yi
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def h(a, b):
        a = a % px; b = b % py
        v = (a * 374761393 + b * 668265263 + seed * 2147483647) & 0xffffffff
        v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
        return (v & 0xffff) / 65535
    return (h(xi, yi) * (1 - fx) + h(xi + 1, yi) * fx) * (1 - fy) + (h(xi, yi + 1) * (1 - fx) + h(xi + 1, yi + 1) * fx) * fy


def leaves(u, v, seed):
    """folhagem periódica a cada tile (64) em u e livre em v (altura): tufos grandes, folhas e buracos escuros"""
    clump = pnoise2(u / P * 6, v / 11, 6, 1 << 20, seed)
    leaf = pnoise2(u / P * 16, v / 4, 16, 1 << 20, seed + 1)
    return (clump - .5) * 1.4 + np.where(leaf > .62, .8, 0) - np.where(leaf < .3, .9, 0)


def leaves_top(x, y, seed):
    clump = pnoise2(x / P * 6, y / P * 6, 6, 6, seed)
    leaf = pnoise2(x / P * 16, y / P * 16, 16, 16, seed + 1)
    return (clump - .5) * 1.4 + np.where(leaf > .62, .8, 0) - np.where(leaf < .3, .9, 0)


def hedge_tex(roses=0.0, base=0.0, seed=21):
    """sebe podada: topo mais claro, faces com tufos e a base escurecida; roses = cachos de rosas vermelhas nas faces"""
    def tex(face, wx, wy, wz, a, b, B):
        z0 = B[4]
        v = {"top": 3.9, "py": 3.2, "px": 2.4}[face] + base
        if face == "top":
            v = v + leaves_top(wx, wy, seed)
            return np.full(v.shape, HEDGE), v
        along = wx if face == "py" else wy
        v = v + leaves(along, wz, seed) - np.clip(8 - (wz - z0), 0, 8) * .12
        fam = np.full(v.shape, HEDGE)
        if roses:
            cl = pnoise2(along / P * 3, wz / 22, 3, 1 << 20, seed + 7) > 1.05 - roses
            dot = pnoise2(along / P * 22, wz / 2.8, 22, 1 << 20, seed + 8) > .68
            m = cl & dot & (wz - z0 > 5)
            fam = np.where(m, BLOOD, fam); v = np.where(m, 3.3 + (face == "py") * .6, v)
        return fam, v
    return tex


def stone_tex(shift=0.0):
    def tex(face, wx, wy, wz, a, b, B):
        v = {"top": 4.6, "py": 3.8, "px": 2.9}[face] + shift + (vnoise(wx * .3, wy * .3 + wz * .3, 4) - .5) * 1.1
        return np.full(v.shape, COOL), v
    return tex


def iron_tex(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, IRON), {"top": 3.6, "py": 3.0, "px": 2.0}[face]


def bark_tex(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, WARM), {"top": 3.6, "py": 3.0, "px": 2.2}[face] + (vnoise(wx * .9, wz * .25 + wy * .9, 6) - .5) * 1.2


# ---------- paredes (mesmos nomes e encaixes das paredes da cripta) ----------

def wall_plain(cv, side, variant=0, height=None):
    h = height or (WH if side in ("ne", "nw") else LH)
    roses = (.22, .3, .14)[variant]
    _box(cv, side, 0, P, 0, h, hedge_tex(roses, 0 if side in ("ne", "nw") else .25, 21 + variant * 5))


def wall_roses(cv, side):
    """variante florida (no lugar do nicho da cripta)"""
    _box(cv, side, 0, P, 0, WH, hedge_tex(.62, .1, 41))


def wall_arch(cv, side, half=20, post=32, ring=10):
    """arco de sebe com o mesmo vão do arco da cripta (a grade da cripta encaixa) e pilares de pedra"""
    tex = hedge_tex(.35, .1, 51); c = P / 2
    _box(cv, side, 0, c - half - 7, 0, WH, tex); _box(cv, side, c + half + 7, P, 0, WH, tex)
    st = stone_tex(.2)
    for (p0, p1) in ((c - half - 7, c - half), (c + half, c + half + 7)):
        _box(cv, side, p0, p1, 0, post + 2, st, -T - 3, 2)
        _box(cv, side, p0 - 1, p1 + 1, post + 2, post + 5, st, -T - 4, 3)
    for pp in np.arange(c - half - 7, c + half + 7, 1.0):
        a = pp + .5 - c
        top = post + 5 if abs(a) >= half else post + np.sqrt(max(half * half - a * a, 0))
        _box(cv, side, pp, pp + 1, top, WH + 6 - abs(a) * .12, tex)
    _box(cv, side, c - half, c + half, 0, 1.2, stone_tex(-.3), -T, 3)


def wall_gap(cv, side, half=22):
    """passagem na sebe cortada da frente, com dois pilaretes de pedra"""
    tex = hedge_tex(.3, .25, 61); c = P / 2
    _box(cv, side, 0, c - half, 0, LH, tex); _box(cv, side, c + half, P, 0, LH, tex)
    for (p0, p1) in ((c - half - 8, c - half), (c + half, c + half + 8)):
        _box(cv, side, p0, p1, 0, LH + 14, stone_tex(.1), -T - 2, 1)


def corner(cv, which):
    tall, low = hedge_tex(.2, 0, 71), hedge_tex(.2, .25, 72)
    if which == "n": cv.box(-T, 0, -T, 0, 0, WH, tall)
    elif which == "e": cv.box(P, P + T, -T, 0, 0, WH, tall)
    elif which == "w": cv.box(-T, 0, P, P + T, 0, LH, low)
    elif which == "s": cv.box(P, P + T, P, P + T, 0, LH, low)
    elif which == "e_low": cv.box(P, P + T, -T, 0, 0, LH, low)
    elif which == "w_tall": cv.box(-T, 0, P, P + T, 0, WH, tall)


# ---------- objetos (coordenadas locais do tile, 0..64) ----------

def sphere(cv, cx, cy, cz, r, famval):
    th, ph = np.meshgrid(np.linspace(0, 2 * np.pi, max(int(r * 9), 24)), np.linspace(0, np.pi, max(int(r * 5), 12)))
    X = cx + r * np.sin(ph) * np.cos(th); Y = cy + r * np.sin(ph) * np.sin(th); Z = cz + r * np.cos(ph)
    nx, ny, nz = np.sin(ph) * np.cos(th), np.sin(ph) * np.sin(th), np.cos(ph)
    fam, v = famval(X, Y, Z)
    cv.plot(X, Y, Z, fam, v + nz * 1.1 + ny * .5 + nx * .2)


def leafy(seed, base=2.6):
    return lambda X, Y, Z: (np.full(X.shape, HEDGE),
        base + (vnoise(X * .45 + Z * .3, Y * .45 - Z * .3, seed) - .5) * 1.4 + (vnoise(X * 1.1 + Z, Y * 1.1 - Z * .7, seed + 1) > .62) * .8)


def rose_ball(seed):
    def f(X, Y, Z):
        m = vnoise(X * .6, Y * .6 + Z * .6, seed) > .55
        return np.where(m, BLOOD, HEDGE), np.where(m, 3.0, 2.1)
    return f


def hedge_block(cv, lights, seed=0):
    """bloco de sebe que ocupa o tile inteiro (miolo do labirinto); vizinhos encaixam sem emenda"""
    cv.box(0, P, 0, P, 0, BH, hedge_tex(.18, .15, 81 + seed))


def parterre(cv, lights):
    """canteiro de 2x2 tiles: borda de ferro, sebe baixa em volta, grama escura e roseiras"""
    S = 2 * P; t = 9; h = 15
    def grass(face, wx, wy, wz, a, b, B):
        return np.full(wx.shape, MOSS), 1.7 + (vnoise(wx * .4, wy * .4, 52) - .5) * 1.4 + (vnoise(wx * 1.5, wy * 1.5, 53) > .7) * .8
    cv.box(2, S - 2, 2, S - 2, 0, 1, grass)
    cv.box(0, S, 0, 2, 0, 3, iron_tex); cv.box(0, 2, 0, S, 0, 3, iron_tex)
    cv.box(0, S, S - 2, S, 0, 3, iron_tex); cv.box(S - 2, S, 0, S, 0, 3, iron_tex)
    tex = hedge_tex(0, .4, 91)
    cv.box(2, S - 2, 2, 2 + t, 0, h, tex); cv.box(2, S - 2, S - 2 - t, S - 2, 0, h, tex)
    cv.box(2, 2 + t, 2, S - 2, 0, h, tex); cv.box(S - 2 - t, S - 2, 2, S - 2, 0, h, tex)
    rng = np.random.default_rng(5)
    for k in range(7):
        rx, ry = rng.uniform(26, S - 26), rng.uniform(26, S - 26)
        sphere(cv, rx, ry, 6, 6, rose_ball(70 + k))


def topiary_center(cv, lights):
    """canteiro redondo com uma topiaria em três bolas (decoração do meio da clareira; não é interativa)"""
    c = P / 2
    def ring(face, wx, wy, wz, th, rr):
        return np.full(wx.shape, HEDGE), (3.4 if face == "top" else 2.4 + np.cos(th - .8) * .7) + (vnoise(wx * .3 + wz * .2, wy * .3 - wz * .2, 25) - .5) * 1.6
    cv.cyl(c, c, 40, 0, 12, ring, r_in=32)
    cv.cyl(c, c, 32, 0, 1.5, lambda f, X, Y, Z, th, rr: (np.full(X.shape, MOSS), 1.6 + (vnoise(X * .5, Y * .5, 26) - .5)), side=False)
    cv.box(c - 3, c + 3, c - 3, c + 3, 0, 14, bark_tex)
    for (z, r) in ((28, 16), (52, 11), (69, 6)):
        sphere(cv, c, c, z, r, leafy(27))
    rng = np.random.default_rng(3)
    for k in range(7):
        t = rng.uniform(0, 2 * np.pi); r = rng.uniform(20, 28)
        sphere(cv, c + np.cos(t) * r, c + np.sin(t) * r, 4, 4, lambda X, Y, Z: (np.full(X.shape, BLOOD), 2.7 + Z * 0))


def topiary_ball(cv, lights):
    c = P / 2
    cv.box(c - 2.5, c + 2.5, c - 2.5, c + 2.5, 0, 9, bark_tex)
    sphere(cv, c, c, 22, 13, leafy(29))


def rose_bush(cv, lights):
    rng = np.random.default_rng(11)
    for k in range(4):
        sphere(cv, 32 + rng.uniform(-8, 8), 32 + rng.uniform(-8, 8), 5, rng.uniform(4, 6), rose_ball(80 + k))


FLAME = [" a ", " b ", "bcb", "bdb", " c "]
FLAME_C = {"a": ("flame", 0, True), "b": ("flame", 1, True), "c": ("flame", 2, True), "d": ("flame", 4, True)}


def lantern(cv, lights):
    c = P / 2
    cv.box(c - 4, c + 4, c - 4, c + 4, 0, 3, stone_tex(0))
    cv.box(c - 1.5, c + 1.5, c - 1.5, c + 1.5, 3, 50, iron_tex, step=.4)
    cv.box(c - 5, c + 5, c - 5, c + 5, 50, 52, iron_tex, step=.4)
    for (dx, dy) in ((-5, -5), (4, -5), (-5, 4), (4, 4)):
        cv.box(c + dx, c + dx + 1, c + dy, c + dy + 1, 52, 61, iron_tex, step=.4)
    cv.box(c - 6, c + 6, c - 6, c + 6, 61, 63, iron_tex, step=.4)
    cv.box(c - 2, c + 2, c - 2, c + 2, 63, 66, iron_tex, step=.4)
    cv.sprite(FLAME, FLAME_C, c + 1, c + 1, 53, depth_bias=30)
    lights.append((c, c, 57, 1.4, 70))


def dead_tree(cv, lights, seed=4):
    """árvore seca e retorcida: tronco e galhos em segmentos de cilindro"""
    rng = np.random.default_rng(seed)
    segs = []
    def grow(x, y, z, dx, dy, dz, r, length, depth):
        steps = int(length / 3)
        for i in range(steps):
            dx += rng.normal(0, .12); dy += rng.normal(0, .12); dz = max(dz + rng.normal(0, .05), .15)
            n = np.sqrt(dx * dx + dy * dy + dz * dz); dx, dy, dz = dx / n, dy / n, dz / n
            x, y, z = x + dx * 3, y + dy * 3, z + dz * 3
            segs.append((x, y, z, max(r * (1 - i / steps * .6), .7)))
        if depth > 0:
            for k in range(2):
                a = rng.uniform(0, 2 * np.pi)
                grow(x, y, z, np.cos(a) * .9, np.sin(a) * .9, .55, r * .6, length * .65, depth - 1)
    grow(32, 32, 0, 0, 0, 1, 5, 36, 3)
    for (x, y, z, r) in segs:
        sphere(cv, x, y, z, r, lambda X, Y, Z: (np.full(X.shape, WARM), 2.6 + (vnoise(X * .8, Y * .8 + Z * .5, 9) - .5) * 1.2))
    cv.box(26, 38, 26, 38, 0, 1.5, lambda f, wx, *a: (np.full(wx.shape, WARM), 2.0))


def floor_tile(variant, seed=0):
    """chão de cascalho (losango 128x64), periódico: tiles vizinhos não mostram emenda"""
    def draw(cv, lights):
        a = np.arange(0, P, .5) + .25
        X, Y = np.meshgrid(a, a)
        peb = (pnoise2(X / P * 22, Y / P * 22, 22, 22, 50) - .5) * 1.6 + (pnoise2(X / P * 64, Y / P * 64, 64, 64, 51) - .5) * 1.2
        fam = np.full(X.shape, WARM); v = 3.7 + peb
        if variant == "dark":
            v = v - .35
        elif variant == "moss":
            m = (pnoise2(X / P * 5, Y / P * 5, 5, 5, 55 + seed) > .6) & (pnoise2(X / P * 32, Y / P * 32, 32, 32, 56) > .45)
            fam = np.where(m, MOSS, fam); v = np.where(m, 2.0 + peb * .5, v)
        elif variant == "blood":
            m = pnoise2(X / P * 2, Y / P * 2, 2, 2, 60 + seed) > .62
            fam = np.where(m, BLOOD, fam); v = np.where(m, 1.9 + peb * .3, v)
        cv.plot(X, Y, np.zeros_like(X), fam, v)
    return draw
