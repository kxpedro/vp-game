"""Mini renderer isométrico em pixel art (2:1), paleta AAP-64.
Mundo: 1 tile = 64 unidades; tela: sx = wx - wy, sy = (wx + wy)/2 - z.
Cada pixel guarda (família de cor, valor contínuo); luz e dithering são aplicados no fim."""
import numpy as np
from PIL import Image

def C(h): return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))
FAM = {
    "cool":  "060608 141013 242234 333941 4a5462 6d758d 8b93af b3b9d1".split(),
    "warm":  "060608 141013 221c1a 322b28 423934 5a4e44 796755 a08662 c7b08b e4d2aa".split(),
    "earth": "060608 141013 221c1a 322b28 71413b 8e5252".split(),
    "moss":  "060608 122020 24523b 23674e 328464".split(),
    "blood": "060608 3b1725 5b3138 73172d b4202a".split(),
    "wet":   "242234 588dbe 849be4".split(),
    "wood":  "060608 141013 221c1a 422433 5b3138 71413b 8e5252".split(),
    "iron":  "060608 141013 242234 333941 4a5462 6d758d".split(),
    "flame": "fa6a0a f9a31b ffd541 fffc40 ffffff".split(),
}
FNAMES = list(FAM)
FID = {n: i for i, n in enumerate(FNAMES)}
LUT = {}
for n in FNAMES:
    for i, h in enumerate(FAM[n]):
        LUT.setdefault(C(h), (FID[n], i))
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16 - .5

def hash1(*a):
    x = np.uint64(1469598103934665603)
    for v in a:
        x = np.uint64((int(x) ^ (int(v) & 0xffffffff)) * 1099511628211 % 2**64)
    return float(x % np.uint64(10007)) / 10007

def vnoise(x, y, seed=0):
    xi, yi = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - xi, y - yi
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def h(a, b):
        v = (a * 374761393 + b * 668265263 + seed * 2147483647) & 0xffffffff
        v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
        return (v & 0xffff) / 65535
    return (h(xi, yi) * (1 - fx) + h(xi + 1, yi) * fx) * (1 - fy) + (h(xi, yi + 1) * (1 - fx) + h(xi + 1, yi + 1) * fx) * fy

class Canvas:
    def __init__(s, w, h, ox, oy):
        s.w, s.h, s.ox, s.oy = w, h, ox, oy
        s.fam = np.full((h, w), -1, int); s.val = np.zeros((h, w)); s.dep = np.full((h, w), -1e9)
        s.wx = np.zeros((h, w)); s.wy = np.zeros((h, w)); s.wz = np.zeros((h, w))
        s.emit = np.zeros((h, w), bool)
    def proj(s, wx, wy, wz):
        return np.floor(wx - wy + s.ox).astype(int), np.floor((wx + wy) / 2 - wz + s.oy).astype(int)
    def plot(s, wx, wy, wz, fam, val, emit=False, depth_bias=0.0):
        sx, sy = s.proj(wx, wy, wz)
        d = wx + wy + wz + depth_bias
        ok = (sx >= 0) & (sx < s.w) & (sy >= 0) & (sy < s.h)
        sx, sy, d = sx[ok], sy[ok], d[ok]
        fam = np.broadcast_to(fam, wx.shape)[ok]; val = np.broadcast_to(val, wx.shape)[ok]
        wx, wy, wz = wx[ok], wy[ok], wz[ok]
        o = np.argsort(d)                                  # o mais próximo escreve por último
        sx, sy, d, fam, val, wx, wy, wz = sx[o], sy[o], d[o], fam[o], val[o], wx[o], wy[o], wz[o]
        win = d >= s.dep[sy, sx]
        sx, sy = sx[win], sy[win]
        s.dep[sy, sx] = d[win]; s.fam[sy, sx] = fam[win]; s.val[sy, sx] = val[win]
        s.wx[sy, sx] = wx[win]; s.wy[sy, sx] = wy[win]; s.wz[sy, sx] = wz[win]
        s.emit[sy, sx] = emit
    def box(s, x0, x1, y0, y1, z0, z1, tex, step=.5, **kw):
        """tex(face, wx, wy, wz, a, b, box) -> (fam, val); face em 'top', 'py' (frente-esq.), 'px' (frente-dir.)"""
        B = (x0, x1, y0, y1, z0, z1)
        a = np.arange(x0, x1, step) + step / 2; b = np.arange(y0, y1, step) + step / 2
        X, Y = np.meshgrid(a, b); Z = np.full_like(X, z1)
        s.plot(X, Y, Z, *tex("top", X, Y, Z, X - x0, Y - y0, B), **kw)
        a = np.arange(x0, x1, step) + step / 2; c = np.arange(z0, z1, step) + step / 2
        X, Z = np.meshgrid(a, c); Y = np.full_like(X, y1 - .01)
        s.plot(X, Y, Z, *tex("py", X, Y, Z, X - x0, Z - z0, B), **kw)
        b = np.arange(y0, y1, step) + step / 2
        Y, Z = np.meshgrid(b, c); X = np.full_like(Y, x1 - .01)
        s.plot(X, Y, Z, *tex("px", X, Y, Z, Y - y0, Z - z0, B), **kw)
    def blit_tile(s, img, gx, gy, top_offset, z=0):
        """cola um PNG de tile (paleta AAP-64) no chão; o fundo não tem profundidade"""
        a = np.array(img.convert("RGBA"))
        px0 = int((gx - gy) * 64 + s.ox - 64); py0 = int((gx + gy) * 32 + s.oy - top_offset - z)
        for yy in range(a.shape[0]):
            for xx in range(a.shape[1]):
                if a[yy, xx, 3] == 0: continue
                X, Y = px0 + xx, py0 + yy
                if not (0 <= X < s.w and 0 <= Y < s.h) or s.dep[Y, X] > -1e8: continue
                f, i = LUT[tuple(a[yy, xx, :3])]
                s.fam[Y, X] = f; s.val[Y, X] = i
                sx, sy = X - s.ox + .5, Y - s.oy + .5 + z
                s.wx[Y, X] = (2 * sy + sx) / 2; s.wy[Y, X] = (2 * sy - sx) / 2; s.wz[Y, X] = 0
    def sprite(s, rows, colors, wx, wy, wz, depth_bias=2):
        """sprite à mão: rows = strings, colors = {char: (família, índice)}; âncora = base central"""
        h = len(rows); w = max(len(r) for r in rows)
        sx0, sy0 = s.proj(np.array([wx]), np.array([wy]), np.array([wz]))
        for j, r in enumerate(rows):
            for i, ch in enumerate(r):
                if ch not in colors: continue
                X, Y = int(sx0[0]) - w // 2 + i, int(sy0[0]) - h + 1 + j
                if not (0 <= X < s.w and 0 <= Y < s.h): continue
                d = wx + wy + wz + depth_bias
                if d < s.dep[Y, X]: continue
                f, v, *e = colors[ch]
                s.fam[Y, X] = FID[f]; s.val[Y, X] = v; s.dep[Y, X] = d
                s.wx[Y, X], s.wy[Y, X], s.wz[Y, X] = wx, wy, wz + (h - j) * .7
                s.emit[Y, X] = bool(e and e[0])
    def render(s, lights=(), ambient=-1.2, warm_at=.85, bg="060608", vignette=None):
        ys, xs = np.mgrid[0:s.h, 0:s.w]
        dith = BAYER[ys % 4, xs % 4]
        glow = np.zeros((s.h, s.w))
        for (lx, ly, lz, power, rad) in lights:
            d = np.sqrt((s.wx - lx) ** 2 + (s.wy - ly) ** 2 + ((s.wz - lz) * 1.2) ** 2)
            glow += power * np.exp(-d / rad)
        glow = 2.3 * (1 - np.exp(-glow / 1.7))          # satura: muitas velas juntas não estouram
        L = ambient + glow
        if vignette is not None: L += vignette(s.wx, s.wy, s.wz)
        L[s.emit] = 0
        out = np.zeros((s.h, s.w, 3), np.uint8)
        if bg is not None: out[:] = C(bg)
        warm = (glow + dith * 1.1 > warm_at) & ~s.emit
        for n in FNAMES:
            m = s.fam == FID[n]
            if not m.any(): continue
            idx = np.round(s.val + L + dith * .85).astype(int)
            pal = np.array([C(h) for h in FAM[n]], np.uint8)
            if n in ("cool", "iron"):
                wpal = np.array([C(h) for h in FAM["warm"]], np.uint8)
                mm = m & warm
                out[mm] = wpal[np.clip(idx[mm] + 0, 0, len(wpal) - 1)]
                m = m & ~warm
            out[m] = pal[np.clip(idx[m], 0, len(pal) - 1)]
        if bg is None:
            a = np.dstack([out, np.where(s.fam >= 0, 255, 0).astype(np.uint8)])
            return Image.fromarray(a, "RGBA")
        return Image.fromarray(out)

def _cyl(s, cx, cy, r, z0, z1, tex, r_in=0.0, step=.5, top=True, side=True, **kw):
    """cilindro (ou anel, com r_in) — tex(face, wx, wy, wz, theta, rr) com face 'top' ou 'side'"""
    if top:
        a = np.arange(cx - r, cx + r, step) + step / 2; b = np.arange(cy - r, cy + r, step) + step / 2
        X, Y = np.meshgrid(a, b); rr = np.hypot(X - cx, Y - cy)
        m = (rr <= r) & (rr >= r_in)
        X, Y, rr = X[m], Y[m], rr[m]; Z = np.full_like(X, z1); th = np.arctan2(Y - cy, X - cx)
        s.plot(X, Y, Z, *tex("top", X, Y, Z, th, rr), **kw)
    if side and z1 > z0:
        n = max(16, int(2 * np.pi * r / step))
        th = np.linspace(0, 2 * np.pi, n, endpoint=False); zz = np.arange(z0, z1, step) + step / 2
        TH, Z = np.meshgrid(th, zz)
        X = cx + np.cos(TH) * (r - .01); Y = cy + np.sin(TH) * (r - .01)
        s.plot(X, Y, Z, *tex("side", X, Y, Z, TH, np.full_like(X, r)), **kw)
Canvas.cyl = _cyl
