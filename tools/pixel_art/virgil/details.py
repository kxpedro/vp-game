import numpy as np
import virgil3d as V
from render import camera

def projector(el=35, az=None, px=0.0185, W=64, H=104, center=(0.0, 0.0)):
    from render import AZ
    f, r, u = camera(el, AZ if az is None else az)
    def pr(p):
        p = np.asarray(p, float)
        X = p @ r; Y = p @ u
        return int(np.floor((X - center[0]) / px + W / 2)), int(np.floor(H - (Y + 0.12 - center[1]) / px))
    return pr, f

def paint(img, M, D, with_sword=True, face=True, **kw):
    pr, f = projector(**kw)
    def put(p, col, need=None):
        x, y = pr(p) if len(p) == 3 else p
        if 0 <= x < img.shape[1] and 0 <= y < img.shape[0] and (need is None or M[y, x] in need):
            img[y, x, :3] = [int(col[i:i+2], 16) for i in (0, 2, 4)]; img[y, x, 3] = 255
            return (x, y)
    h = V.J["head"]
    for sx in ((-1, 1) if face else ()):
        e = h + [sx * 0.032, -0.012, 0.085]
        q = put(e, "c42f3f", need=(V.SKIN,))
        if q:
            put((q[0], q[1] - 1), "4d3a33", need=(V.SKIN,))
    if False:
        m = put(h + [0, -0.068, 0.085], "4d3a33", need=(V.SKIN,))
        if m: put((m[0] + 1, m[1]), "4d3a33", need=(V.SKIN,))
        n = pr(h + [0, -0.03, 0.10]); put((n[0] + 1, n[1]), "796755", need=(V.SKIN,))
    # fivela
    b = pr([0.0, 1.02, 0.12])
    if M[b[1], b[0]] == V.LEATHER:
        for dx, dy, c in ((0, 0, "dba463"), (1, 0, "dba463"), (0, 1, "8a6a3a"), (1, 1, "dba463"), (0, -1, "f3d6a0")):
            put((b[0] + dx, b[1] + dy), c)
    b2 = pr([0.12, 0.985, 0.08]); put(b2, "dba463", need=(V.LEATHER,))
    # cruz no peito, com corrente
    c = pr([0.0, 1.36 + V.ANIM['up'], 0.13])
    if M[c[1], c[0]] not in (V.CLOTH, V.LINING): c = (-99, -99)
    for dx, dy in ((0, 0), (0, 1), (0, 2), (0, -1), (-1, 0), (1, 0)):
        put((c[0] + dx, c[1] + dy), "dba463")
    put((c[0], c[1] - 1), "f3d6a0")
    for t in np.linspace(0, 1, 6):
        for sx in (-1, 1):
            put(np.array([sx * 0.04 * (1 - t), 1.52 + V.ANIM['up'] - 0.15 * t, 0.10 + 0.03 * t]), "8a6a3a", need=(V.CLOTH, V.LINING, V.COAT))
    # sangue na lâmina
    if with_sword:
        ha = V.J["haR"]; tip = V.SWORD_TIP + (ha - V.J0["haR"]) * 0.4
        for t in np.linspace(0.30, 0.62, 14):
            put(ha + (tip - ha) * t, "73172d" if t < 0.5 else "3b1725", need=(V.BLADE,))
    return img
