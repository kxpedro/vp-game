"""Folha do Virgil: 8 direções (linhas) x [idle 8 | corrida 8] (colunas)."""
import sys, os
DIR_AZ = {"n": 180, "ne": -135, "e": -90, "se": -45, "s": 0, "sw": 45, "w": 90, "nw": 135}
d = sys.argv[1]; ws = sys.argv[2] == "1"
os.environ["AZ"] = str(DIR_AZ[d]); os.environ.setdefault("EL", "30"); os.environ["HEAD"] = "0"
import numpy as np
from PIL import Image
import virgil3d as V, render as R, shade as S, details
def shot():
    mats, Nn, depth, cam, P = R.render(with_sword=ws)
    lvl = S.light_levels(mats, Nn, cam)
    M, Lv, D = S.downsample(mats, lvl, depth, 4)
    img = S.to_image(M, Lv, D)
    return Image.fromarray(details.paint(img, M, D, with_sword=ws, face=True))
os.makedirs("out", exist_ok=True)
for i in range(8):
    ph = 2 * np.pi * i / 8
    b = (1 - np.cos(ph)) / 2; hb = (1 - np.cos(ph - 0.7)) / 2; ab = (1 - np.cos(ph - 1.0)) / 2
    if os.environ.get("ONLY") != "run":
        V.set_pose(up=0.021 * b, head_up=0.019 * hb, breath=b, sway=0.012 * np.sin(ph - 1.2), arm_lag=0.015 * ab)
        shot().save(f"out/{int(ws)}_{d}_idle_{i}.png")
    V.set_run(ph)
    shot().save(f"out/{int(ws)}_{d}_run_{i}.png")
    print(d, i, flush=True)
