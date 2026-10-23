import sys, numpy as np
from PIL import Image
from sdf import norm
import virgil3d as V

def camera(el_deg, az_deg):
    el, az = np.radians(el_deg), np.radians(az_deg)
    back = np.array([np.sin(az) * np.cos(el), np.sin(el), np.cos(az) * np.cos(el)])   # do alvo para a câmera
    f = -back; r = norm(np.cross(f, [0, 1, 0])); u = np.cross(r, f)
    return f, r, u

AZ = float(__import__("os").environ.get("AZ", "-30"))
def render(el=35, az=None, px=0.0185, ss=4, W=64, H=104, with_sword=True, center=(0.0, 0.0)):
    az = AZ if az is None else az
    f, r, u = camera(el, az)
    w, h = W * ss, H * ss; s = px / ss
    xs = (np.arange(w) + .5 - w / 2) * s + center[0]
    ys = (h - (np.arange(h) + .5)) * s - 0.12 + center[1]        # base do canvas um pouco abaixo dos pés
    X, Y = np.meshgrid(xs, ys)
    target = np.array([0, 0.0, 0])
    o = target[None] + X.reshape(-1, 1) * r + Y.reshape(-1, 1) * u - f * 3.0
    t = np.zeros(len(o)); hit = np.zeros(len(o), bool); alive = np.ones(len(o), bool)
    for it in range(160):
        idx = np.where(alive)[0]
        if len(idx) == 0: break
        pts = o[idx] + f * t[idx, None]
        d = V.scene(pts, with_sword)
        t[idx] += d * 0.9
        hh = d < 0.0012; hit[idx[hh]] = True; alive[idx[hh]] = False
        alive[idx[t[idx] > 6.0]] = False
    P = o + f * t[:, None]
    hi = np.where(hit)[0]
    mats = np.zeros(len(o), int); N = np.zeros((len(o), 3))
    _, m = V.scene(P[hi], with_sword, want_mat=True); mats[hi] = m
    e = 0.0015
    for k in range(3):
        dv = np.zeros(3); dv[k] = e
        N[hi, k] = V.scene(P[hi] + dv, with_sword) - V.scene(P[hi] - dv, with_sword)
    N[hi] /= np.linalg.norm(N[hi], axis=1, keepdims=True)
    depth = np.full(len(o), np.inf); depth[hi] = t[hi]
    return mats.reshape(h, w), N.reshape(h, w, 3), depth.reshape(h, w), (f, r, u), P.reshape(h, w, 3)

if __name__ == "__main__":
    import pickle, time
    t0 = time.time()
    for ws in (True, False):
        out = render(with_sword=ws)
        pickle.dump(out, open(f"buf_{int(ws)}.pkl", "wb"))
    print("ok", time.time() - t0)
