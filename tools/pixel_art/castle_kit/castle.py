"""Peças do kit da Ala Superior do Castelo (mapa 3, `upper_castle`): salão de pedra azulada, colunas, vitrais, brasas de
prata derretida (a assinatura do Dragonkin Prateado), tapete carmim e dourado.

Mesmo renderizador e convenções do kit da cripta e do Jardim (tools/pixel_art/crypt_kit): tile = 64 unidades, paredes na
borda do tile com espessura T, fundo (ne/nw) alto e frente (se/sw) cortada. Os nomes das peças são os mesmos, então o
build_room monta a sala do castelo apontando só para a pasta do kit. Paleta AAP-64 em tudo.
"""
import os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import iso
from iso import FID, vnoise
from walls import _box, P, T, WH

# Rampas novas (só cores da AAP-64). Pedra = família "cool" da cripta (azul-acinzentada), com textura de blocos grandes.
NEW = {
    "violet": "242234 403353 793a80 bc4a9b".split(),
    "gold":   "71413b bb7547 dba463 f4d29c fef3c0".split(),
    "glass":  "143464 285cc4 249fde 849be4 b9bffb".split(),
    "glassv": "242234 403353 793a80 bc4a9b e86a73".split(),
    "silver": "6d758d 8b93af b3b9d1 dae0ea e3e6ff".split(),
}
for _n, _r in NEW.items():
    if _n not in iso.FAM:
        iso.FAM[_n] = _r; iso.FNAMES.append(_n); iso.FID[_n] = len(iso.FNAMES) - 1
COOL, BLOOD, IRON, FLAMEF = (FID[n] for n in ("cool", "blood", "iron", "flame"))
VIOLET, GOLD, GLASS, GLASSV, SILVER = (FID[n] for n in ("violet", "gold", "glass", "glassv", "silver"))
LH = 14           # altura da parede cortada da frente
BLK = WH / 4      # 4 fiadas de blocos grandes por parede


def pn(x, y, px, seed):
    xi, yi = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - xi, y - yi
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def h(a, b):
        a = a % px
        v = (a * 374761393 + b * 668265263 + seed * 2147483647) & 0xffffffff
        v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
        return (v & 0xffff) / 65535
    return (h(xi, yi) * (1 - fx) + h(xi + 1, yi) * fx) * (1 - fy) + (h(xi, yi + 1) * (1 - fx) + h(xi + 1, yi + 1) * fx) * fy


def ashlar(face_shift=0.0, variant=0, deco=None):
    """cantaria: blocos grandes (32 x 19), fiadas alternadas, bisel claro em cima/esquerda e junta escura.
    deco(p, z, fam, v) pode pintar por cima (estandarte, fenda de prata)"""
    def tex(face, wx, wy, wz, a, b, B):
        if face == "top":
            v = 4.4 + face_shift + (pn(wx * .5, wy * .5, 1 << 20, 3) - .5) * .9
            return np.full(v.shape, COOL), v
        p = wx if face == "py" else wy
        row = np.floor(wz / BLK).astype(int)
        pp = (p + np.where(row % 2 == 1, 16, 0)) % 64
        col = np.floor(pp / 32).astype(int)
        dx = np.minimum(pp % 32, 32 - pp % 32)
        zz = wz - row * BLK
        dz = np.minimum(zz, BLK - zz)
        joint = (dx < 1.1) | (dz < 1.0)
        bevel = ((pp % 32 < 3.2) & (pp % 32 >= 1.1)) | ((BLK - zz < 3.0) & (BLK - zz >= 1.0))
        shade = ((row * 7 + col * 13 + variant * 5) % 5 - 2) * .22
        grain = (pn(p * .35, wz * .35, 1 << 20, 11 + variant) - .5) * 1.1
        base = {"py": 3.55, "px": 2.95}[face] + face_shift
        v = base + shade + grain + bevel * .8
        v = np.where(joint, 1.2 + face_shift * .5, v)
        fam = np.full(v.shape, COOL)
        if deco is not None:
            fam, v = deco(p, wz, fam, v, face)
        return fam, v
    return tex


def banner_deco(p, z, fam, v, face):
    """estandarte carmim com bainha dourada e uma gota de prata; pende de z=70 até a ponta em z=22"""
    c = 32
    half = 9
    tip = 22 + np.abs(p % 64 - c) * .9
    inside = (np.abs(p % 64 - c) < half) & (z < 70) & (z > tip)
    border = inside & ((np.abs(p % 64 - c) > half - 1.8) | (z > 67.2) | (z < tip + 1.6))
    drop = inside & (np.hypot((p % 64 - c) * 1.0, (z - 46) * .8) < 3.6)
    fam = np.where(inside, BLOOD, fam); v = np.where(inside, 2.6 + (pn(p * .9, z * .3, 1 << 20, 5) - .5) * .6, v)
    fam = np.where(border, GOLD, fam); v = np.where(border, 3.0, v)
    fam = np.where(drop, SILVER, fam); v = np.where(drop, 3.2, v)
    return fam, v


def seep_deco(p, z, fam, v, face):
    """fenda com prata derretida escorrendo do alto da pedra (chão quente do Dragonkin)"""
    x = p % 64
    wob = (pn(z * .08, 0.0 * z, 1 << 20, 17) - .5) * 9
    crack = (np.abs(x - 22 - wob - (z - 40) * .12) < .9) & (z > 6) & (z < 62)
    fam = np.where(crack, FLAMEF, fam); v = np.where(crack, 1.0 + (pn(z * .2, 0.0 * z, 1 << 20, 8)) * 2, v)
    glow = (~crack) & (np.abs(x - 22 - wob - (z - 40) * .12) < 2.4) & (z > 6) & (z < 62)
    v = np.where(glow, v + .5, v)
    return fam, v


def silver_tex(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, SILVER), {"top": 3.7, "py": 2.2, "px": 1.4}[face] + (vnoise(wx * .6, wy * .6 + wz * .4, 4) - .5) * .8


def iron_tex(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, IRON), {"top": 3.6, "py": 3.0, "px": 2.0}[face]


def gold_tex(face, wx, wy, wz, a, b, B):
    return np.full(wx.shape, GOLD), {"top": 3.2, "py": 2.5, "px": 1.7}[face]


# ---------- paredes ----------

def wall_plain(cv, side, variant=0, height=None):
    h = height or (WH if side in ("ne", "nw") else LH)
    deco = (None, banner_deco, seep_deco)[variant]
    _box(cv, side, 0, P, 0, h, ashlar(0 if side in ("ne", "nw") else -.3, variant, deco if h == WH else None))


def wall_window(cv, side):
    """janela ogival alta com vitral azul e violeta (no lugar do nicho da cripta); o vitral brilha sozinho"""
    tex = ashlar(0, 1)
    a0, a1, z0 = 14, 50, 16
    half = (a1 - a0) / 2; c = (a0 + a1) / 2; top0 = 54
    _box(cv, side, 0, a0, 0, WH, tex); _box(cv, side, a1, P, 0, WH, tex)
    _box(cv, side, a0, a1, 0, z0, tex)
    for pp in np.arange(a0, a1, 1.0):
        a = pp + .5 - c
        top = top0 + (1 - abs(a) / half) * 12                  # ogiva
        _box(cv, side, pp, pp + 1, top, WH, tex)
        def glass(face, wx, wy, wz, aa, bb, B, pp=pp, top=top):
            p = wx if face == "py" else wy
            zz = wz
            lead = ((np.abs((p - c)) < .7) | (np.abs(zz - 36) < .7) | (np.abs(((p - c) + (zz - 36)) % 11) < .8) )
            pane = ((np.floor((p - c) / 5.5) + np.floor((zz - 18) / 7)) % 3).astype(int)
            fam = np.where(pane == 0, GLASS, np.where(pane == 1, GLASSV, GLASS))
            v = np.where(pane == 0, 3.0, np.where(pane == 1, 2.4, 1.8)) + (zz - 18) / (top - 18) * .8
            fam = np.where(lead, IRON, fam); v = np.where(lead, 1.2, v)
            return fam, v
        _box(cv, side, pp, pp + 1, z0, top, glass, -T + 4, -T + 6, 0, 0)
    # peitoril e verga de pedra mais clara
    _box(cv, side, a0 - 2, a1 + 2, z0 - 3, z0, ashlar(.5, 2), -T - 3, 1)


def wall_arch(cv, side, half=20, post=32, ring=9, jamb=9):
    """portal ogival (mesmo vão do arco da cripta, a grade encaixa): aduelas claras e pilares com capitel"""
    tex = ashlar(0, 0); vous = ashlar(.8, 2)
    c = P / 2
    _box(cv, side, 0, c - half - jamb, 0, WH, tex); _box(cv, side, c + half + jamb, P, 0, WH, tex)
    for pp in np.arange(c - half - jamb, c + half + jamb, 1.0):
        a = pp + .5 - c
        if abs(a) < half:
            top = post + (1 - (abs(a) / half) ** 1.7) * half * 1.15      # ogiva
            _box(cv, side, pp, pp + 1, top, top + ring, vous, -T, 2)
            _box(cv, side, pp, pp + 1, top + ring, WH, tex)
        else:
            _box(cv, side, pp, pp + 1, 0, post + 4, vous, -T, 2)
            _box(cv, side, pp, pp + 1, post + 4, WH, tex)
    for s in (-1, 1):                                                   # capitéis dourados nas impostas
        p0 = c + s * half - (0 if s < 0 else 4)
        _box(cv, side, p0 - (4 if s < 0 else 0), p0 + 4 - (4 if s < 0 else 0), post + 3, post + 5, gold_tex, -T - 2, 3)
    _box(cv, side, c - half, c + half, 0, 1.5, ashlar(-.2, 1), -T, 3)


def wall_gap(cv, side, half=22):
    tex = ashlar(-.3, 0); c = P / 2
    _box(cv, side, 0, c - half, 0, LH, tex); _box(cv, side, c + half, P, 0, LH, tex)
    for (p0, p1) in ((c - half - 9, c - half), (c + half, c + half + 9)):
        _box(cv, side, p0, p1, 0, LH + 16, ashlar(.2, 2), -T - 3, 0)


def corner(cv, which):
    hi, lo = ashlar(0, 0), ashlar(-.3, 0)
    if which == "n": cv.box(-T, 0, -T, 0, 0, WH, hi)
    elif which == "e": cv.box(P, P + T, -T, 0, 0, WH, hi)
    elif which == "w": cv.box(-T, 0, P, P + T, 0, LH, lo)
    elif which == "s": cv.box(P, P + T, P, P + T, 0, LH, lo)
    elif which == "e_low": cv.box(P, P + T, -T, 0, 0, LH, lo)
    elif which == "w_tall": cv.box(-T, 0, P, P + T, 0, WH, hi)


# ---------- objetos (coordenadas locais do tile, 0..64) ----------
FLAME = [" a ", " b ", "bcb", "bdb", " c "]
FLAME_C = {"a": ("flame", 0, True), "b": ("flame", 1, True), "c": ("flame", 2, True), "d": ("flame", 4, True)}
BIG_FLAME = ["  a  ", " abb ", " bcb ", "abcba", "bcdcb", "bcdcb", " bcb "]
BIG_C = {"a": ("flame", 0, True), "b": ("flame", 1, True), "c": ("flame", 2, True), "d": ("flame", 3, True)}


def column(cv, lights):
    """coluna redonda de pedra com estrias, base quadrada e capitel dourado (alta: o Virgil passa atrás dela)"""
    c = P / 2
    def shaft(face, wx, wy, wz, th, rr):
        if face == "top":
            return np.full(wx.shape, COOL), 4.2
        flute = np.cos(th * 8) * .35
        light = np.cos(th - .8) * .9
        v = 2.9 + light + flute + (pn(wz * .3, th * 3, 1 << 20, 4) - .5) * .5
        return np.full(wx.shape, COOL), v
    cv.box(c - 14, c + 14, c - 14, c + 14, 0, 6, ashlar(.2, 2))
    cv.box(c - 12, c + 12, c - 12, c + 12, 6, 9, ashlar(.5, 1))
    cv.cyl(c, c, 9, 9, 66, shaft)
    cv.box(c - 13, c + 13, c - 13, c + 13, 66, 70, ashlar(.5, 1))
    cv.box(c - 11, c + 11, c - 11, c + 11, 70, 73, gold_tex)


def brazier(cv, lights):
    """braseiro de ferro sobre tripé, cheio de prata em brasa: a luz fria-quente que anuncia o Dragonkin"""
    c = P / 2
    for (dx, dy) in ((-8, -8), (6, -8), (-8, 6), (6, 6)):
        cv.box(c + dx, c + dx + 2.4, c + dy, c + dy + 2.4, 0, 26, iron_tex, step=.4)
    cv.box(c - 11, c + 11, c - 11, c + 11, 26, 29, iron_tex, step=.4)
    def bowl(face, wx, wy, wz, th, rr):
        if face == "top":
            glow = 1.0 + (pn(wx * .35, wy * .35, 1 << 20, 6) * 2.8)
            return np.full(wx.shape, FLAMEF), glow
        return np.full(wx.shape, IRON), 3.0 + np.cos(th - .8) * .8
    cv.cyl(c, c, 13, 29, 36, bowl, r_in=0)
    cv.sprite(BIG_FLAME, BIG_C, c, c, 36, depth_bias=30)
    lights.append((c, c, 44, 2.0, 90))


def armor(cv, lights):
    """armadura vazia sobre plinto, com espada de prata cravada à frente (lembra a ordem do castelo)"""
    c = P / 2
    cv.box(c - 13, c + 13, c - 13, c + 13, 0, 8, ashlar(.3, 2))
    cv.box(c - 6, c - 1, c - 4, c + 4, 8, 28, silver_tex)           # pernas
    cv.box(c + 1, c + 6, c - 4, c + 4, 8, 28, silver_tex)
    cv.box(c - 8, c + 8, c - 6, c + 6, 28, 46, silver_tex)           # tronco
    cv.box(c - 11, c - 8, c - 5, c + 5, 40, 48, silver_tex); cv.box(c + 8, c + 11, c - 5, c + 5, 40, 48, silver_tex)   # ombros
    cv.cyl(c, c, 5, 46, 56, lambda f, X, Y, Z, th, rr: (np.full(X.shape, SILVER), 3.4 if f == "top" else 2.6 + np.cos(th - .8) * .9))
    cv.box(c - 4, c + 4, c - 1, c + 1, 52, 54, iron_tex)             # fenda do elmo
    cv.box(c - .8, c + .8, c + 8, c + 9.6, 8, 40, silver_tex, step=.3)   # lâmina
    cv.box(c - 4, c + 4, c + 8, c + 9.6, 38, 40, gold_tex, step=.3)       # guarda


def candelabrum(cv, lights):
    c = P / 2
    cv.box(c - 5, c + 5, c - 5, c + 5, 0, 3, gold_tex, step=.4)
    cv.box(c - 1.2, c + 1.2, c - 1.2, c + 1.2, 3, 42, gold_tex, step=.3)
    cv.box(c - 10, c + 10, c - 1, c + 1, 42, 44, gold_tex, step=.3)
    for dx in (-9, 0, 9):
        cv.box(c + dx - 1.2, c + dx + 1.2, c - 1.2, c + 1.2, 44, 50, gold_tex, step=.3)
        cv.sprite(FLAME, FLAME_C, c + dx, c, 50, depth_bias=30)
    lights.append((c, c, 52, 1.5, 70))


def rubble(cv, lights, seed=3):
    r = np.random.default_rng(seed)
    for k in range(r.integers(5, 9)):
        x, y, s = 32 + r.uniform(-15, 15), 32 + r.uniform(-15, 15), r.uniform(2.5, 6)
        cv.box(x, x + s, y, y + s * r.uniform(.6, 1.3), 0, s * r.uniform(.4, .8), ashlar(-.2, 2), step=.34)


def silver_pool(cv, lights):
    """poça de prata derretida no chão (calço), brilha sozinha"""
    c = P / 2
    a = np.arange(0, P, .5) + .25
    X, Y = np.meshgrid(a, a)
    d = np.hypot((X - c) / 1.0, (Y - c) / .8) + (pn(X * .18, Y * .18, 1 << 20, 21) - .5) * 14
    m = d < 17
    core = d < 9
    fam = np.where(core, FLAMEF, SILVER)
    v = np.where(core, 2.0 + pn(X * .3, Y * .3, 1 << 20, 22) * 2, np.where(d < 14, 3.4, 2.4))
    sel = m
    cv.plot(X[sel], Y[sel], np.full(X.shape, .4)[sel], fam[sel], v[sel], emit=True)
    lights.append((c, c, 4, 1.0, 60))


def carpet(axis):
    def draw(cv, lights):
        def tex(face, wx, wy, wz, a, b, B):
            along = wy if axis == "y" else wx
            across = wx if axis == "y" else wy
            border = (np.abs(across - 32) > 11.5) & (np.abs(across - 32) <= 15)
            line = (np.abs(across - 32) > 9.6) & (np.abs(across - 32) <= 11.5)
            lozenge = (np.abs(((along % 32) - 16)) + np.abs(across - 32) < 7)
            weave = (pn(along * .9, across * .9, 1 << 20, 3) - .5) * .5
            fam = np.full(wx.shape, BLOOD); v = 3.0 + weave
            fam = np.where(line | lozenge, GOLD, fam); v = np.where(line | lozenge, 2.8, v)
            fam = np.where(border, VIOLET, fam); v = np.where(border, 1.8 + weave, v)
            return fam, v
        if axis == "y": cv.box(14, 50, 0, 64, 0, .8, tex)
        else: cv.box(0, 64, 14, 50, 0, .8, tex)
    return draw


# ---------- chão (losango 128x64, periódico a cada tile) ----------
def floor_tile(variant, seed=0):
    def draw(cv, lights):
        a = np.arange(0, P, .5) + .25
        X, Y = np.meshgrid(a, a)
        sx, sy = np.floor(X / 32).astype(int), np.floor(Y / 32).astype(int)
        checker = (sx + sy) % 2
        dxj = np.minimum(X % 32, 32 - X % 32); dyj = np.minimum(Y % 32, 32 - Y % 32)
        joint = (dxj < .8) | (dyj < .8)
        bevel = ((X % 32 < 2.4) | (Y % 32 < 2.4)) & ~joint
        grain = (pn(X * .25, Y * .25, 16, 31) - .5) * 1.0 + (pn(X * .9, Y * .9, 58, 32) - .5) * .5
        fam = np.full(X.shape, COOL)
        v = 3.3 + checker * -.55 + grain + bevel * .6
        v = np.where(joint, 1.3, v)
        if variant == "dark":
            v = v - .5
        elif variant == "cracked":
            ck = (np.abs((X * .7 + Y * .4) % 64 - 31 + (pn(Y * .1, 0 * Y, 1 << 20, 41) - .5) * 14) < .8) & (pn(X * .08, Y * .08, 8, 42) > .38)
            v = np.where(ck, 1.0, v)
        elif variant == "ember":
            d = np.abs((X * .6 - Y * .8) % 64 - 32 + (pn(X * .12, Y * .12, 1 << 20, 43) - .5) * 16)
            ck = (d < 1.0) & (pn(X * .08, Y * .08, 8, 44) > .3)
            glow = (d >= 1.0) & (d < 2.8) & (pn(X * .08, Y * .08, 8, 44) > .3)
            fam = np.where(ck, FLAMEF, fam); v = np.where(ck, 1.0 + pn(X * .3, Y * .3, 1 << 20, 45) * 2.5, v)
            v = np.where(glow, v + .6, v)
        elif variant == "inlay":
            cx, cy = np.abs(X - 32), np.abs(Y - 32)
            ring = np.abs(np.hypot(cx, cy) - 20) < 1.0
            star = (np.abs(cx - cy) < 1.0) & (np.hypot(cx, cy) < 20)
            fam = np.where(ring | star, VIOLET, fam); v = np.where(ring | star, 1.6, v)
        cv.plot(X, Y, np.zeros_like(X), fam, v)
    return draw
