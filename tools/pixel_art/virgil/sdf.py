"""Virgil 3D por SDF (raymarching em numpy) -> pixel art com câmera alta."""
import numpy as np

def norm(v): v = np.asarray(v, float); return v / np.linalg.norm(v)

# ---------- primitivas (p: (N,3)) ----------
def sd_sphere(p, c, r): return np.linalg.norm(p - c, axis=1) - r
def sd_ellipsoid(p, c, r):
    q = (p - c) / r; k0 = np.linalg.norm(q, axis=1); k1 = np.linalg.norm(q / r, axis=1)
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)
def sd_capsule(p, a, b, ra, rb):
    a = np.asarray(a, float); b = np.asarray(b, float)
    ba = b - a; pa = p - a
    h = np.clip((pa @ ba) / (ba @ ba), 0, 1)
    return np.linalg.norm(pa - h[:, None] * ba, axis=1) - (ra + (rb - ra) * h)
def frame(axis, hint=(0, 0, 1)):
    w = norm(axis); u = np.cross(hint, w)
    if np.linalg.norm(u) < 1e-6: u = np.cross((1, 0, 0), w)
    u = norm(u); v = np.cross(w, u); return u, v, w
def sd_box_oriented(p, c, axes, half, rr=0.0):
    q = p - c
    lq = np.stack([q @ axes[0], q @ axes[1], q @ axes[2]], 1)
    d = np.abs(lq) - (np.asarray(half) - rr)
    return np.linalg.norm(np.maximum(d, 0), axis=1) + np.minimum(d.max(1), 0) - rr
def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)

def sd_skirt(p, y0, y1, r0, r1, sx, open0, open1, th, back=0.0):
    """Casca cônica (aba do casaco), aberta na frente (+z). open em radianos (meia abertura)."""
    y = p[:, 1]; t = np.clip((y - y1) / (y0 - y1), 0, 1)   # 0 embaixo, 1 em cima
    r = r1 + (r0 - r1) * t
    x = p[:, 0] / sx; z = p[:, 2] - back * (1 - t)
    rad = np.sqrt(x * x + z * z)
    d = np.abs(rad - r) - th
    d = np.maximum(d, np.maximum(y - y0, y1 - y))
    ang = np.abs(np.arctan2(x, z))                         # 0 = frente
    op = open1 + (open0 - open1) * t
    cut = (op - ang) * np.maximum(rad, 0.05)               # >0 dentro da abertura
    return np.maximum(d, cut), (rad < r)                   # segundo: lado de dentro
