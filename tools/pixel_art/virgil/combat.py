"""Golpes do Virgil: combo de 3 (corte, corte de volta, estocada), giro do ataque rápido e avanço (dash).

    py combat.py <n|ne|e|se|s|sw|w|nw> [anim ...]   -> out/combat_<dir>_<anim>_<n>.png

Célula maior que a da folha de idle/corrida (a espada estendida passa dos 80 px): 160x128, pés 40 px abaixo
do centro, como na outra folha. A ordem e o número de quadros de cada golpe estão em ANIMS.
"""
import sys, os
DIR_AZ = {"n": 180, "ne": -135, "e": -90, "se": -45, "s": 0, "sw": 45, "w": 90, "nw": 135}
if __name__ == "__main__":
    os.environ["AZ"] = str(DIR_AZ[sys.argv[1]])
os.environ.setdefault("EL", "30"); os.environ["HEAD"] = "0"
import numpy as np
from PIL import Image
import virgil3d as V, render as R, shade as S, details
from sdf import norm

W, H = 160, 128
R.BASE = (H - (H / 2 + 40)) * 0.0185      # pés 40 px abaixo do centro da célula
ANIMS = [("slash", 6), ("slash_reverse", 6), ("thrust", 6), ("spin", 8), ("dash", 5)]

def ease(t): return t * t * (3 - 2 * t)
def lerp(a, b, t): return np.asarray(a, float) + (np.asarray(b, float) - np.asarray(a, float)) * t

def keys(track, t):
    """track: [(t, valor), ...] com t crescente; interpola suave entre as chaves."""
    for (t0, a), (t1, b) in zip(track, track[1:]):
        if t <= t1:
            u = 0.0 if t1 == t0 else np.clip((t - t0) / (t1 - t0), 0, 1)
            return lerp(a, b, ease(u))
    return np.asarray(track[-1][1], float)

def sdir(theta, phi):
    """Direção pelo ângulo horizontal (0 = frente, -90 = direita do Virgil, +90 = esquerda) e pela altura."""
    th, ph = np.radians(theta), np.radians(phi)
    return np.array([np.sin(th) * np.cos(ph), np.sin(ph), np.cos(th) * np.cos(ph)])

def stance(aR, kR, aL, kL, spread=0.0):
    """Pernas: ângulo da coxa (para a frente > 0) e dobra do joelho; spread abre os pés para os lados."""
    legs = {}
    for s, a, k in (("R", aR, kR), ("L", aL, kL)):
        sx = -1 if s == "R" else 1
        hip = np.array([sx * 0.095, 0.97, 0.0])
        kn, an = V._leg(hip, a, k)
        kn = kn + [sx * spread * 0.5, 0, 0]; an = an + [sx * spread, 0, 0]
        legs[s] = (hip, kn, an)
    dy = 0.10 - min(legs["R"][2][1], legs["L"][2][1])
    for s in ("R", "L"):
        hip, kn, an = legs[s]
        V.J["hip" + s] = hip + [0, dy, 0]; V.J["kn" + s] = kn + [0, dy, 0]; V.J["an" + s] = an + [0, dy, 0]
    return dy

def arm(s, hand, out=0.06, down=0.06):
    """Mão em `hand` (espaço do corpo); cotovelo no meio do caminho, puxado para fora e para baixo."""
    sh = V.J["sh" + s]; sx = -1 if s == "R" else 1
    hand = np.asarray(hand, float)
    el = (sh + hand) / 2 + [sx * out, -down, 0]
    d = norm(hand - el)
    V.J["el" + s] = el; V.J["wr" + s] = hand - d * 0.05; V.J["ha" + s] = hand

def pose(t_dy, yaw=0.0, lean=0.0, sword=None, coat=None, up=0.0):
    V.ANIM.update(up=up, breath=0.4, sway=0.0, dy=t_dy, lean=lean, sword_dir=norm(sword) if sword is not None else None,
                  coat_back=0.0, coat_sz=1.0, solid=False, coat_open=1.10, coat_drag=0.0, coat_flap=0.0,
                  coat_ph=0.0, coat_lift=0.0, yaw=np.radians(yaw))
    if coat: V.ANIM.update(coat)

def reset():
    for k, v in V.J0.items(): V.J[k] = v.copy()

PIVOT = np.array([-0.04, 1.20, 0.05])      # centro do arco da mão nos cortes (frente do peito)

def slash(t, reverse=False):
    """Corte horizontal amplo. Ida: da direita (atrás) para a esquerda. Volta: o contrário, de revés e mais alto."""
    if not reverse:
        th = keys([(0, -125), (0.30, -140), (0.62, 40), (0.80, 95), (1.0, 85)], t)
        ph = keys([(0, 5), (0.30, 8), (0.62, -8), (1.0, -14)], t)
        yaw = keys([(0, -22), (0.30, -30), (0.62, 12), (0.80, 26), (1.0, 20)], t)
    else:
        th = keys([(0, 85), (0.28, 100), (0.60, -40), (0.80, -105), (1.0, -95)], t)
        ph = keys([(0, 18), (0.28, 22), (0.60, 4), (1.0, -10)], t)
        yaw = keys([(0, 22), (0.28, 30), (0.60, -12), (0.80, -26), (1.0, -20)], t)
    reset()
    lunge = keys([(0, 0.0), (0.3, 0.0), (0.62, 1.0), (1.0, 0.7)], t)
    dy = stance(0.10 + 0.32 * lunge if not reverse else -0.25 * lunge, 0.35 + 0.25 * lunge,
                -0.25 * lunge if not reverse else 0.10 + 0.32 * lunge, 0.30 + 0.30 * lunge, spread=0.06)
    r = 0.36
    hth = th * 0.82                                      # mão anda um pouco atrás da lâmina
    hand = PIVOT + r * np.array([np.sin(np.radians(hth)), 0.0, np.cos(np.radians(hth))]) + [0, np.radians(ph) * 0.25, 0]
    arm("R", hand, out=0.05, down=0.08)
    # braço livre: contrapeso do golpe. Na ida começa na frente (guarda) e é puxado para trás quando a espada passa;
    # na volta começa recolhido atrás e se abre para a frente e para o lado.
    if not reverse:
        handL = keys([(0, [0.20, 1.24, 0.30]), (0.30, [0.16, 1.30, 0.36]), (0.62, [0.36, 1.18, -0.16]),
                      (0.80, [0.40, 1.16, -0.28]), (1.0, [0.36, 1.12, -0.22])], t)
    else:
        handL = keys([(0, [0.34, 1.14, -0.20]), (0.28, [0.36, 1.12, -0.28]), (0.60, [0.22, 1.26, 0.30]),
                      (0.80, [0.38, 1.28, 0.22]), (1.0, [0.36, 1.20, 0.12])], t)
    arm("L", handL, out=0.06, down=0.05)
    pose(dy - 0.03 * lunge, yaw=yaw, lean=0.10 * lunge, sword=sdir(th, ph),
         coat=dict(coat_drag=0.05 * lunge, coat_flap=0.02, coat_ph=t * 4, coat_lift=0.04 * lunge))

def thrust(t):
    """Estocada: puxa a espada para trás na altura do quadril e avança o corpo com a lâmina reta para a frente."""
    reset()
    k = keys([(0, 0.0), (0.38, 0.0), (0.58, 1.0), (0.82, 1.0), (1.0, 0.75)], t)       # 0 = recuo, 1 = estendido
    back = keys([(0, 0.4), (0.38, 1.0), (0.58, 0.0), (1.0, 0.0)], t)
    dy = stance(-0.10 - 0.25 * k, 0.30 + 0.10 * k, 0.15 + 0.55 * k, 0.40 + 0.35 * k, spread=0.04)
    # duas mãos no cabo: recua junto ao quadril e empurra a lâmina reta para a frente
    hand = lerp([-0.16, 1.10, -0.14 - 0.14 * back], [0.0, 1.22, 0.58], k)
    sw = sdir(keys([(0, -8), (0.58, 0), (1, 0)], t), keys([(0, 6), (0.38, 4), (0.58, -4), (1, -6)], t))
    arm("R", hand, out=0.06, down=0.05 * (1 - k))
    arm("L", hand - sw * 0.10 + [0.01, 0, 0], out=0.07, down=0.05 * (1 - k))
    pose(dy - 0.06 * k, yaw=keys([(0, -28), (0.38, -34), (0.58, -8), (1.0, -10)], t), lean=0.04 + 0.26 * k,
         sword=sw,
         coat=dict(coat_drag=0.12 * k, coat_flap=0.02, coat_ph=t * 5, coat_lift=0.10 * k))

def spin(t):
    """Giro em área: uma volta inteira com a espada estendida na horizontal, corpo agachado e casaco aberto."""
    reset()
    dy = stance(0.20, 0.55, -0.20, 0.55, spread=0.10)
    # duas mãos no cabo, braços estendidos para a frente e para a direita
    sw = sdir(-62, -4)
    hand = np.array([-0.34, 1.18, 0.32])
    arm("R", hand, out=0.03, down=0.03)
    arm("L", hand - sw * 0.10, out=0.05, down=0.05)
    # yaw > 0 leva a frente para a esquerda: o lado direito (espada) varre a frente primeiro
    pose(dy - 0.05, yaw=360.0 * t, lean=0.06, sword=sw,
         coat=dict(coat_open=1.40, coat_drag=0.0, coat_flap=0.05, coat_ph=t * 2 * np.pi * 2, coat_lift=0.20, coat_sz=1.18))

def dash(t):
    """Avanço rápido (como o dash do Hades): corpo baixo e inclinado, pernas esticadas, casaco arrastado."""
    reset()
    k = keys([(0, 0.45), (0.2, 1.0), (0.75, 1.0), (1.0, 0.35)], t)
    dy = stance(0.75 * k, 0.95 * k, -0.85 * k, 0.10 * k)
    arm("R", lerp([-0.30, 1.10, 0.00], [-0.32, 1.02, -0.38], k), out=0.03, down=0.03)
    arm("L", keys([(0, [0.24, 1.18, 0.20]), (0.2, [0.36, 1.10, -0.36]), (0.75, [0.38, 1.06, -0.42]),
                   (1.0, [0.28, 1.12, 0.05])], t), out=0.04, down=0.03)
    pose(dy - 0.16 * k, lean=0.62 * k, sword=sdir(-165, -22),
         coat=dict(coat_drag=0.45 * k, coat_flap=0.05, coat_ph=t * 6, coat_lift=0.42 * k, coat_open=1.40))

POSES = {"slash": lambda t: slash(t), "slash_reverse": lambda t: slash(t, True), "thrust": thrust, "spin": spin, "dash": dash}

def shot(ws=True):
    mats, Nn, depth, cam, P = R.render(with_sword=ws, W=W, H=H)
    lvl = S.light_levels(mats, Nn, cam)
    M, Lv, D = S.downsample(mats, lvl, depth, 4)
    img = S.to_image(M, Lv, D)
    return Image.fromarray(details.paint(img, M, D, with_sword=ws, face=True, W=W, H=H))

if __name__ == "__main__":
    d = sys.argv[1]; want = sys.argv[2:] or [a for a, _ in ANIMS]
    os.makedirs("out", exist_ok=True)
    for name, n in ANIMS:
        if name not in want: continue
        for i in range(n):
            # o giro fecha a volta; os demais vão do primeiro ao último quadro
            POSES[name](i / n if name == "spin" else i / (n - 1))
            shot().save(f"out/combat_{d}_{name}_{i}.png"); print(d, name, i, flush=True)
