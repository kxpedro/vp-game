import sys, os, pickle, numpy as np
from PIL import Image
os.environ.setdefault("AZ", "30"); os.environ["HEAD"] = "0"
import virgil3d as V, render as R, shade as S, details
N = 8
def frame(i, ws):
    ph = 2 * np.pi * i / N
    b = (1 - np.cos(ph)) / 2
    hb = (1 - np.cos(ph - 0.7)) / 2
    ab = (1 - np.cos(ph - 1.0)) / 2
    V.set_pose(up=0.021 * b, head_up=0.019 * hb, breath=b, sway=0.012 * np.sin(ph - 1.2), arm_lag=0.015 * ab)
    mats, Nn, depth, cam, P = R.render(with_sword=ws)
    lvl = S.light_levels(mats, Nn, cam)
    M, Lv, D = S.downsample(mats, lvl, depth, 4)
    img = S.to_image(M, Lv, D)
    img = details.paint(img, M, D, with_sword=ws, face=True)
    return Image.fromarray(img)
if __name__ == "__main__":
    ws = sys.argv[1] == "1"
    for i in range(N):
        frame(i, ws).save(f"idle_{int(ws)}_{i}.png"); print(i, flush=True)
