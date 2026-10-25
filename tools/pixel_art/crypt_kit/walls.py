"""Paredes modulares da cripta: cada peça ocupa a borda de 1 tile (64 unidades) e encaixa com as vizinhas.
A textura é periódica a cada 64 unidades ao longo da parede, então peças lado a lado não mostram emenda."""
import numpy as np
from iso import FID
from materials import FACE_BASE, dressed, shadow_tex, iron, BONES, BONES_C

COOL, WARM, MOSS = FID["cool"], FID["warm"], FID["moss"]
P = 64                  # período ao longo da parede = 1 tile
T, WH, FH = 22, 76, 12  # espessura, altura da parede, altura da parede cortada (frente)
RH = 76 / 6             # 6 fiadas por parede

def pnoise(x, y, px, seed):
    """value noise periódico em x (px células de rede), livre em y"""
    xi, yi = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - xi, y - yi
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def h(a, b):
        a = a % px
        v = (a * 374761393 + b * 668265263 + seed * 2147483647) & 0xffffffff
        v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
        return (v & 0xffff) / 65535
    return (h(xi, yi) * (1 - fx) + h(xi + 1, yi) * fx) * (1 - fy) + (h(xi, yi + 1) * (1 - fx) + h(xi + 1, yi + 1) * fx) * fy

def pfbm(p, q, seed, f_cells, oct=3):
    """fbm periódico: f_cells células por período P na 1ª oitava"""
    s, amp, tot = 0, 1, 0
    for o in range(oct):
        c = f_cells * 2 ** o
        s += amp * pnoise(p / P * c, q / P * c, c, seed + o); tot += amp; amp *= .5
    return s / tot

ROWS = []
_rng = np.random.default_rng(2024)
for r in range(8):
    k = _rng.integers(4, 7)
    w = _rng.uniform(.75, 1.3, k)
    ROWS.append(np.sort((np.cumsum(w) / w.sum() * P + _rng.uniform(0, P)) % P))

def _edge(p, q):
    pm = p % P
    window = np.clip(np.minimum(pm, P - pm) / 6, 0, 1)
    pw = pm + (pnoise(pm / P * 4, q * .06, 4, 1) - .5) * 4
    qw = q + (pnoise(pm / P * 4, q * .07 + 9, 4, 2) - .5) * 3
    rf = qw / RH; r = np.floor(rf).astype(int); fy = rf - r
    dy = np.minimum(fy, 1 - fy) * RH
    dx = np.zeros_like(p); idx = np.zeros(p.shape, int)
    for rr in np.unique(r):
        m = r == rr
        c = ROWS[int(rr) % len(ROWS)]
        cc = np.concatenate([c - P, c, c + P])
        i = np.clip(np.searchsorted(cc, pw[m]), 1, len(cc) - 1)
        dx[m] = np.minimum(pw[m] - cc[i - 1], cc[i] - pw[m])
        idx[m] = (i - 1) % len(c) + rr * 16
    rad = 2.6
    e = np.where((dx < rad) & (dy < rad), rad - np.hypot(rad - dx, rad - dy), np.minimum(dx, dy))
    return e, idx, fy, window

def wall_tex(variant=0, shift=0.0):
    """mesma pedra do chão: carvão com bisel arredondado, granulado e junta terrosa.
    variant muda tons, lascas e musgo, mas não o desenho das pedras (as emendas continuam batendo)"""
    def tex(face, wx, wy, wz, a, b, B):
        if face == "top":
            p = wx if (B[1] - B[0]) >= (B[3] - B[2]) else wy
            return np.full(wx.shape, COOL), .7 + shift + (pfbm(p, (wy if p is wx else wx) * 1.0, 4, 2) - .5) * 1.2
        p = wx if face == "py" else wy
        s = 1 if face == "py" else -1
        worn = pfbm(p, wz, 9, 2)
        mw = .75 + .9 * np.clip(worn - .5, 0, 1) * 3
        e, sid, fy, win = _edge(p, wz)
        eR, _, _, _ = _edge(p + s, wz); eU, _, _, _ = _edge(p, wz + 1)
        H = lambda ee: np.clip((ee - mw) / 3.2, 0, 1) ** .8
        h0 = H(e)
        light = ((H(eR) - h0) + (h0 - H(eU))) * 3.0
        tone = ((sid * 2654435761) % 1000) / 1000 - .5
        vt = ((sid * 40503 + variant * 977) % 1000) / 1000 - .5
        tone = tone + vt * .5 * win                          # variação por variante, nula nas bordas da peça
        g = pfbm(p, wz, 12, 24, 2)
        pi = np.floor(p % P).astype(int); zi = np.floor(wz).astype(int)
        speck = ((pi * 73856093 ^ zi * 19349663 ^ 7) % 1000) / 1000
        base = {"py": 2.95, "px": 2.35}[face] + shift
        v = base + tone * .9 + light + (g - .5) * 1.8 + (speck > .95) - (speck < .06)
        if variant:
            chip = (pfbm(p, wz + variant * 50, 30 + variant, 6, 2) > .68) & (win > .5)
            v = np.where(chip, v - 1.1, v)
        mortar = e < mw
        outl = ~mortar & (e < mw + 1.2) & (fy > .5)
        v = np.where(outl, np.minimum(v, 1.0), v)
        fam = np.where(mortar, WARM, COOL)
        mv = 2.2 + shift * .5 + (worn - .5) * 2.5 + (g - .5) * 2 - (e < mw * .45) * 1.6 + (speck > .92) * 1.2
        v = np.where(mortar, mv, v)
        mossy = (wz < 12) & mortar & (pfbm(p, wz, 50 + variant, 4, 2) > (.55 if variant == 2 else .64))
        fam = np.where(mossy, MOSS, fam); v = np.where(mossy, 1.6, v)
        return fam, v
    return tex

# ---------- peças (tile local: x,y de 0 a 64; tela: x cresce para baixo-direita, y para baixo-esquerda) ----------
# NW = borda superior-esquerda (plano x=0, face +x), NE = superior-direita (plano y=0, face +y)
# SE = inferior-direita (plano x=64), SW = inferior-esquerda (plano y=64): paredes da frente, cortadas

def _box(cv, side, p0, p1, z0, z1, tex, t0=-T, t1=0.0, X0=0, Y0=0):
    if p1 <= p0 or z1 <= z0: return
    if side == "ne": cv.box(X0 + p0, X0 + p1, Y0 + t0, Y0 + t1, z0, z1, tex)
    elif side == "nw": cv.box(X0 + t0, X0 + t1, Y0 + p0, Y0 + p1, z0, z1, tex)
    elif side == "sw": cv.box(X0 + p0, X0 + p1, Y0 + P - t1, Y0 + P - t0, z0, z1, tex)
    elif side == "se": cv.box(X0 + P - t1, X0 + P - t0, Y0 + p0, Y0 + p1, z0, z1, tex)

def wall_plain(cv, side, variant=0, X0=0, Y0=0, height=None):
    h = height or (WH if side in ("ne", "nw") else FH)
    _box(cv, side, 0, P, 0, h, wall_tex(variant, 0 if side in ("ne", "nw") else -.3), X0=X0, Y0=Y0)

def wall_niche(cv, side, X0=0, Y0=0, lights=None):
    """nicho de sepultamento (loculus) com ossos"""
    tex = wall_tex(1)
    a0, a1, z0, z1, d = 17, 47, 16, 31, 14
    for (p0, p1) in [(0, a0), (a1, P)]: _box(cv, side, p0, p1, 0, WH, tex, X0=X0, Y0=Y0)
    _box(cv, side, a0, a1, 0, z0, tex, X0=X0, Y0=Y0); _box(cv, side, a0, a1, z1, WH, tex, X0=X0, Y0=Y0)
    _box(cv, side, a0, a1, z0, z1, shadow_tex(.2), -T, -d, X0, Y0)
    _box(cv, side, a0, a1, z0 - 2, z0, shadow_tex(1.2), -d, 2, X0, Y0)
    if side == "ne": cv.sprite(BONES, BONES_C, X0 + 32, Y0 - 7, z0, depth_bias=30)
    else: cv.sprite(BONES, BONES_C, X0 - 7, Y0 + 32, z0, depth_bias=30)

def wall_arch(cv, side, X0=0, Y0=0, half=20, post=32, ring=9, jamb=9):
    """arco arredondado com vão estreito (~40 unidades) no meio da borda"""
    tex = wall_tex(0); vous = dressed(1.0, 5, 6, 2); jt = dressed(.5, 9, 40, 3)
    c = P / 2
    _box(cv, side, 0, c - half - jamb, 0, WH, tex, X0=X0, Y0=Y0)
    _box(cv, side, c + half + jamb, P, 0, WH, tex, X0=X0, Y0=Y0)
    for pp in np.arange(c - half - jamb, c + half + jamb, 1.0):
        a = pp + .5 - c
        if abs(a) < half:
            top = post + np.sqrt(max(half * half - a * a, 0))
            _box(cv, side, pp, pp + 1, top, top + ring, vous, -T, 2, X0, Y0)
            _box(cv, side, pp, pp + 1, top + ring, WH, tex, X0=X0, Y0=Y0)
        else:
            _box(cv, side, pp, pp + 1, 0, post + 4, jt, -T, 2, X0, Y0)
            _box(cv, side, pp, pp + 1, post + 4, WH, tex, X0=X0, Y0=Y0)
    _box(cv, side, c - half, c + half, 0, 1.5, dressed(-.2, 10, 44, 4), -T, 3, X0, Y0)

def wall_gap(cv, side, X0=0, Y0=0, half=22):
    """passagem estreita numa parede cortada da frente, com ombreiras baixas"""
    tex = wall_tex(0, -.3); jt = dressed(.4, 9, 12, 4); c = P / 2
    _box(cv, side, 0, c - half, 0, FH, tex, X0=X0, Y0=Y0); _box(cv, side, c + half, P, 0, FH, tex, X0=X0, Y0=Y0)
    for (p0, p1) in [(c - half - 10, c - half), (c + half, c + half + 10)]:
        _box(cv, side, p0, p1, 0, FH + 16, jt, -T - 3, 0, X0, Y0)

def corner(cv, which, X0=0, Y0=0):
    """cantos que fecham o quadrado de espessura entre duas paredes:
    n (fundo, alto), e (direita, alto), w (esquerda, baixo), s (frente, baixo)"""
    if which == "n": cv.box(X0 - T, X0, Y0 - T, Y0, 0, WH, wall_tex(0))
    elif which == "e": cv.box(X0 + P, X0 + P + T, Y0 - T, Y0, 0, WH, wall_tex(0))
    elif which == "w": cv.box(X0 - T, X0, Y0 + P, Y0 + P + T, 0, FH, wall_tex(0, -.3))
    elif which == "s": cv.box(X0 + P, X0 + P + T, Y0 + P, Y0 + P + T, 0, FH, wall_tex(0, -.3))
    elif which == "e_low": cv.box(X0 + P, X0 + P + T, Y0 - T, Y0, 0, FH, wall_tex(0, -.3))
    elif which == "w_tall": cv.box(X0 - T, X0, Y0 + P, Y0 + P + T, 0, WH, wall_tex(0))

def gate_iron(face, wx, wy, wz, a, b, B):
    """ferro escuro da grade: frente quase preta e um fio de brilho na lateral, para destacar da pedra"""
    v = {"top": 3.6, "py": 1.4, "px": 3.0}[face]
    return np.full(wx.shape, FID["iron"]), v + (iron(face, wx, wy, wz, a, b, B)[1] - {"top": 3.5, "py": 3.0, "px": 2.0}[face]) * .6

def portcullis(cv, side, lift=0.0, X0=0, Y0=0, half=20, post=32, bar=2.5, pitch=6.5, rail=12.0):
    """grade de ferro no vão do arco (wall_arch), logo atrás da face da parede: barras verticais e travessas.
    lift = quanto a grade já subiu (0 = fechada); o que passa do topo do vão fica escondido na parede"""
    c = P / 2
    t0, t1 = -6.0, -3.0
    def top(a): return post + np.sqrt(max(half * half - a * a, 0))
    n = int((2 * half) // pitch)
    first = c - (n - 1) * pitch / 2
    for k in range(n):
        p0 = first + k * pitch - bar / 2
        for pp in np.arange(p0, p0 + bar, .5):
            h = top(pp + .25 - c)
            if h <= lift: continue
            _box(cv, side, pp, pp + .5, lift, h, gate_iron, t0, t1, X0, Y0)
    # travessas a cada `rail`, a partir de 5 unidades acima da base da grade
    z = lift + 5
    while z < post + half:
        for pp in np.arange(c - half, c + half, .5):
            if z + 1.5 <= top(pp + .25 - c):
                _box(cv, side, pp, pp + .5, z, z + 1.5, gate_iron, t0 - .5, t1 + .5, X0, Y0)
        z += rail

GATE_FRONT_TOP = FH + 16    # altura das ombreiras do wall_gap: a grade da frente fica cortada nela, como a parede

def portcullis_front(cv, side, lift=0.0, X0=0, Y0=0, half=22, bar=2.5, pitch=6.5, rail=12.0):
    """grade de ferro no vão da parede cortada da frente (wall_gap), entre as ombreiras.
    Segue o corte da parede: só aparece até GATE_FRONT_TOP; ao subir (lift), some de baixo para cima"""
    c = P / 2
    t0, t1 = -6.0, -3.0
    top = GATE_FRONT_TOP
    if lift >= top: return
    n = int((2 * half) // pitch)
    first = c - (n - 1) * pitch / 2
    for k in range(n):
        p0 = first + k * pitch - bar / 2
        _box(cv, side, p0, p0 + bar, lift, top, gate_iron, t0, t1, X0, Y0)
    z = lift + 5
    while z + 1.5 <= top:
        _box(cv, side, c - half, c + half, z, z + 1.5, gate_iron, t0 - .5, t1 + .5, X0, Y0)
        z += rail
    # travessa do corte, presa ao topo da grade enquanto ela não sai de vista
    _box(cv, side, c - half, c + half, top - 1.5, top, gate_iron, t0 - .5, t1 + .5, X0, Y0)
