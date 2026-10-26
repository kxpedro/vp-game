"""Morte do Virgil: golpe fatal, recuo, joelhos no chão, espada solta, tomba de costas e fica no chão.

    py death.py <n|ne|e|se|s|sw|w|nw> [quadro ...]   -> out/death_<dir>_<n>.png

Mesma célula, câmera e luz do combat.py (160x128, pés 40 px abaixo do centro). O corpo deitado usa
virgil3d.ANIM["pitch"] (queda em torno do pivô) e ["shift"] (altura do corpo deitado).
"""
import sys, os
DIR_AZ = {"n": 180, "ne": -135, "e": -90, "se": -45, "s": 0, "sw": 45, "w": 90, "nw": 135}
if __name__ == "__main__":
    os.environ["AZ"] = str(DIR_AZ[sys.argv[1]])
os.environ.setdefault("EL", "30"); os.environ["HEAD"] = "0"
import numpy as np
from PIL import Image
import virgil3d as V
from combat import keys, sdir, arm, pose, reset, shot, W, H, lerp
from sdf import norm

FRAMES = 12

def legs(aR, kR, aL, kL, spread=0.0, knee_pad=0.075):
    """Como combat.stance, mas o ponto mais baixo pode ser o joelho (ajoelhado)."""
    out = {}
    for s, a, k in (("R", aR, kR), ("L", aL, kL)):
        sx = -1 if s == "R" else 1
        hip = np.array([sx * 0.095, 0.97, 0.0])
        kn, an = V._leg(hip, a, k)
        kn = kn + [sx * spread * 0.5, 0, 0]; an = an + [sx * spread, 0, 0]
        out[s] = (hip, kn, an)
    low = min(min(v[2][1] - 0.0 for v in out.values()) - 0.10 * 0, 99)
    floor = min(min(v[2][1] - 0.10 for v in out.values()), min(v[1][1] - knee_pad for v in out.values()))
    dy = -floor
    for s in ("R", "L"):
        hip, kn, an = out[s]
        V.J["hip" + s] = hip + [0, dy, 0]; V.J["kn" + s] = kn + [0, dy, 0]; V.J["an" + s] = an + [0, dy, 0]
    return dy

def death(t):
    reset()
    # tempo: 0 golpe, .14 recuo máximo, .45 ajoelhado, .62 desaba, .86 no chão, 1 parado
    lean = keys([(0, 0.0), (.14, -0.22), (.36, -0.04), (.50, 0.10), (.62, 0.0), (1, 0.0)], t)
    yaw = keys([(0, 0), (.14, 25), (.45, 35), (.62, 60), (1, 90)], t)
    # pernas: em pé -> passo atrás -> ajoelha (joelho R no chão, perna L dobrada) -> estica deitada
    kneel = keys([(0, 0), (.20, 0.15), (.45, 1.0), (.58, 1.0), (.70, 0.55), (.82, 0.1), (.90, 0.0), (1, 0.0)], t)
    aR = 0.0 + 0.0 * kneel; kR = 0.12 + 1.45 * kneel
    aL = -0.20 + 1.15 * kneel; kL = 0.10 + 1.15 * kneel
    kR = np.clip(kR, 0.05, 1.55)
    dy = legs(aR - 0.12 * keys([(0, 0), (.14, 1), (.3, 0), (1, 0)], t), kR, aL, kL, spread=0.07)
    # braço da espada: levantado no golpe, cai solto com a espada apontando para o chão
    sw_th = keys([(0, -110), (.14, -150), (.45, -170), (.7, -150), (1, -100)], t)
    sw_ph = keys([(0, 20), (.14, 45), (.45, -62), (.7, -78), (1, -82)], t)
    handR = keys([(0, [-0.30, 1.12, 0.22]), (.14, [-0.36, 1.30, -0.10]), (.45, [-0.28, 0.62, 0.20]),
                  (.7, [-0.30, 0.55, 0.18]), (1, [-0.33, 0.38, 0.14])], t)
    handL = keys([(0, [0.24, 1.18, 0.22]), (.14, [0.46, 1.36, -0.22]), (.45, [0.30, 0.78, 0.26]),
                  (.7, [0.34, 0.60, 0.12]), (1, [0.38, 0.30, -0.02])], t)
    arm("R", handR, out=0.06, down=0.08)
    arm("L", handL, out=0.06, down=0.08)
    # cabeça: tomba para trás no golpe, cai para o peito quando ajoelha, volta de lado no chão
    V.J["head"] = V.J["head"] + keys([(0, [0, 0, 0]), (.14, [0, 0.02, -0.07]), (.45, [0, -0.06, 0.09]), (.62, [0, -0.05, 0.10]),
                                      (.86, [0, -0.01, -0.02]), (1, [0.0, 0.0, -0.02])], t)
    pitch = keys([(0, 0), (.50, 0), (.64, -0.45), (.76, -1.35), (.84, -1.62), (.90, -1.52), (1, -1.55)], t)
    # no chão: ergue o corpo até o dorso encostar (espessura ~0,15) e acompanha o balanço da queda
    shift = keys([(0, [0, 0, 0]), (.50, [0, 0, 0]), (.76, [0, 0.10, -0.15]), (.84, [0, 0.14, -0.20]), (1, [0, 0.14, -0.20])], t)
    pose(dy, yaw=yaw, lean=lean, sword=sdir(sw_th, sw_ph),
         coat=dict(coat_drag=0.20 * keys([(0, 0), (.14, -.5), (.5, 1), (1, 1)], t), coat_flap=0.05, coat_ph=t * 5, coat_lift=0.0,
                   coat_open=1.0 + 0.4 * kneel, pitch=float(pitch), shift=np.asarray(shift)))

if __name__ == "__main__":
    d = sys.argv[1]
    idx = [int(a) for a in sys.argv[2:]] or range(FRAMES)
    os.makedirs("out", exist_ok=True)
    for i in idx:
        death(i / (FRAMES - 1))
        shot().save(f"out/death_{d}_{i}.png"); print(d, i, flush=True)
