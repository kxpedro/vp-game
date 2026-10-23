import numpy as np
from sdf import *

SKIN, HAIR, COAT, LINING, STEEL, LEATHER, CLOTH, GOLD, BLADE, GRIP = range(1, 11)

# ---------- pose (metros, y para cima, personagem de frente para +z) ----------
J = dict(
    head=(0.0, 1.715, 0.015), neck0=(0, 1.53, 0), neck1=(0, 1.64, 0.005),
    shR=(-0.205, 1.49, -0.005), shL=(0.205, 1.505, -0.005),
    elR=(-0.245, 1.21, -0.02), wrR=(-0.255, 0.97, 0.05), haR=(-0.255, 0.915, 0.07),
    elL=(0.265, 1.22, -0.05), wrL=(0.235, 1.03, 0.07), haL=(0.205, 1.0, 0.10),
    hipR=(-0.095, 0.97, 0.0), hipL=(0.095, 0.975, 0.0),
    knR=(-0.115, 0.52, 0.035), anR=(-0.125, 0.10, -0.005),
    knL=(0.165, 0.535, 0.07), anL=(0.235, 0.10, 0.03),
)
J = {k: np.array(v, float) for k, v in J.items()}
J0 = {k: v.copy() for k, v in J.items()}
ANIM = dict(up=0.0, breath=0.0, sway=0.0, dy=0.0, lean=0.0, sword_dir=None, coat_back=0.0)
def set_pose(up=0.0, head_up=0.0, breath=0.0, sway=0.0, arm_lag=0.0):
    for k, v in J0.items(): J[k] = v.copy()
    for k in ("neck1", "shR", "shL"): J[k][1] += up
    J["neck0"][1] += up * 0.8
    J["head"][1] += head_up
    for s in ("R", "L"):
        J["el" + s][1] += up * 0.85; J["wr" + s][1] += arm_lag; J["ha" + s][1] += arm_lag
    ANIM.update(up=up, breath=breath, sway=sway, dy=0.0, lean=0.0, sword_dir=None, coat_back=0.0, coat_sz=1.0, solid=False,
                coat_open=0.80, coat_drag=0.0, coat_flap=0.0, coat_ph=0.0, coat_lift=0.0)
import os
_az = np.radians(float(os.environ.get('AZ', '-30')))
_el = np.radians(float(os.environ.get('EL', '35')))
CAM_DIR = np.array([np.sin(_az) * np.cos(_el), np.sin(_el), np.cos(_az) * np.cos(_el)])
SWORD_TIP = np.array([-0.50, 0.03, 0.30])

def _leg(hip, a, k, Lt=0.44, Ls=0.43):
    knee = hip + Lt * np.array([0, -np.cos(a), np.sin(a)])
    b = a - k
    ankle = knee + Ls * np.array([0, -np.cos(b), np.sin(b)])
    return knee, ankle

def set_run(ph, lean=0.16):
    """Corrida: ph em [0, 2pi). Pernas em oposição, braços ao contrário das pernas, corpo inclinado."""
    for k, v in J0.items(): J[k] = v.copy()
    legs = {}
    for s, off in (("R", 0.0), ("L", np.pi)):
        f = ph + off
        a = 0.62 * np.sin(f)
        k = 0.22 + 1.15 * max(0.0, -np.cos(f)) ** 1.2
        hip = np.array([-0.095 if s == "R" else 0.095, 0.97, 0.0])
        legs[s] = (hip,) + _leg(hip, a, k)
    low = min(legs["R"][2][1], legs["L"][2][1])
    dy = 0.10 - low + 0.018 * np.sin(2 * ph) ** 2
    for s in ("R", "L"):
        hip, kn, an = legs[s]
        J["hip" + s] = hip + [0, dy, 0]; J["kn" + s] = kn + [0, dy, 0]; J["an" + s] = an + [0, dy, 0]
    # braços: ombro fixo, cotovelo dobrado, balanço oposto à perna do mesmo lado
    for s, off in (("R", np.pi), ("L", 0.0)):
        sw = 0.55 * np.sin(ph + off)
        sh = J["sh" + s]; sx = -1 if s == "R" else 1
        el = sh + 0.29 * np.array([0, -np.cos(sw), np.sin(sw)]) + [sx * 0.03, 0, 0]
        fa = sw + 1.25
        wr = el + 0.25 * np.array([0, -np.cos(fa), np.sin(fa)]) + [-sx * 0.03, 0, 0]
        J["el" + s] = el; J["wr" + s] = wr; J["ha" + s] = wr + 0.05 * np.array([0, -np.cos(fa), np.sin(fa)])
    # braço da espada: mão na frente do ombro e lâmina apoiada nele, apontando para trás e para cima;
    # só um quique leve no ritmo dos passos
    bob = 0.012 * np.cos(2 * ph)
    J["elR"] = np.array([-0.30, 1.25 + bob, 0.09])
    J["wrR"] = np.array([-0.235, 1.455 + bob, 0.145])
    J["haR"] = np.array([-0.215, 1.50 + bob, 0.165])
    J["head"][2] += 0.02
    ANIM.update(up=0.0, breath=0.4 + 0.3 * np.sin(2 * ph), sway=0.0, dy=dy - 0.0, lean=lean,
                sword_dir=norm([0.45, 0.30 + 0.03 * np.sin(2 * ph), -1.0]),
                # casaco solto: aberto na frente para as pernas passarem, barra arrastada para trás e ondulando
                coat_back=-0.08, coat_sz=1.0, solid=False, coat_open=1.30,
                coat_drag=0.17 + 0.03 * np.sin(2 * ph), coat_flap=0.03, coat_ph=2 * ph,
                coat_lift=0.12 + 0.04 * np.sin(2 * ph + 0.8))
    return dy

def to_world(q):
    q = np.array(q, float); dy = ANIM["dy"]; lean = ANIM["lean"]
    return np.array([q[0], q[1] + dy, q[2] + lean * (q[1] - 1.0)])

def scene(p, with_sword=True, want_mat=False):
    pw = p
    if ANIM["dy"] or ANIM["lean"]:
        p = pw.copy(); p[:, 1] -= ANIM["dy"]; p[:, 2] -= ANIM["lean"] * (p[:, 1] - 1.0)
    parts = []
    def add(d, m): parts.append((d, m))
    # cabeça: crânio + mandíbula mais estreita (formato adulto, não bola)
    h = J["head"]
    skull = sd_ellipsoid(p, h + [0, 0.018, -0.012], np.array([0.082, 0.086, 0.095]))
    jaw = sd_ellipsoid(p, h + [0, -0.045, 0.022], np.array([0.064, 0.068, 0.074]))
    chin = sd_sphere(p, h + [0, -0.088, 0.05], 0.03)
    headd = smin(smin(skull, jaw, 0.03), chin, 0.025)
    add(headd, SKIN)
    add(sd_capsule(p, J["neck0"], J["neck1"], 0.045, 0.042), SKIN)
    # cabelo: casca justa no crânio, recortada na linha do cabelo
    q = p - h
    shell = sd_ellipsoid(p, h + [0, 0.024, -0.02], np.array([0.094, 0.094, 0.106]))
    zf = np.clip((q[:, 2] + 0.06) / 0.14, 0, 1)                 # 0 atrás .. 1 na frente
    hairline = -0.14 + zf * 0.205                                  # cai até a nuca atrás, testa livre na frente
    side = np.clip(np.abs(q[:, 0]) / 0.09, 0, 1) * (zf > 0.55)
    hairline = hairline - side * 0.06                              # costeletas descem nas laterais
    hair = np.maximum(shell, hairline - q[:, 1])
    # volume solto nas costas e mechas
    hair = smin(hair, sd_ellipsoid(p, h + [0, -0.06, -0.075], np.array([0.085, 0.09, 0.05])), 0.03)
    for a0, a1, r0, r1 in (((-0.03, 0.09, 0.07), (-0.06, 0.045, 0.095), 0.018, 0.005),
                           ((0.035, 0.09, 0.07), (0.06, 0.05, 0.09), 0.016, 0.005),
                           ((-0.07, 0.05, 0.03), (-0.092, -0.04, 0.045), 0.02, 0.008),
                           ((0.07, 0.05, 0.03), (0.092, -0.045, 0.04), 0.02, 0.008),
                           ((-0.06, -0.02, -0.07), (-0.07, -0.13, -0.06), 0.03, 0.01),
                           ((0.06, -0.02, -0.07), (0.075, -0.13, -0.06), 0.03, 0.01),
                           ((0.0, -0.04, -0.09), (0.0, -0.15, -0.07), 0.035, 0.012)):
        hair = smin(hair, sd_capsule(p, h + a0, h + a1, r0, r1), 0.01)
    # fios: ondulação leve na superfície
    ang = np.arctan2(q[:, 0], q[:, 2])
    hair = hair - 0.004 * np.sin(ang * 14 + q[:, 1] * 40)
    add(hair, HAIR)
    # tronco
    U = ANIM['up']; Bb = ANIM['breath']
    chest = sd_ellipsoid(p, np.array([0, 1.36 + U, 0.0]), np.array([0.20 * (1 + .025 * Bb), 0.17, 0.125 * (1 + .05 * Bb)]))
    abd = sd_ellipsoid(p, np.array([0, 1.15 + U * .5, 0.0]), np.array([0.15, 0.15, 0.10]))
    pelv = sd_ellipsoid(p, np.array([0, 1.0, 0.0]), np.array([0.165, 0.10, 0.11]))
    torso = smin(smin(chest, abd, 0.06), pelv, 0.05)
    add(torso, COAT)
    # cinto
    belt = np.maximum(np.abs(sd_ellipsoid(p, np.array([0, 1.02, 0.0]), np.array([0.168, 0.5, 0.113]))) - 0.008,
                      np.abs(p[:, 1] - 1.02) - 0.022)
    add(belt, LEATHER)
    belt2 = np.maximum(np.abs(sd_ellipsoid(p, np.array([0, 0.965, 0.0]), np.array([0.172, 0.5, 0.118]))) - 0.008,
                       np.abs(p[:, 1] - (0.965 + p[:, 0] * 0.18)) - 0.016)
    add(belt2, LEATHER)
    # aba do casaco (aberta na frente, cai até abaixo do joelho)
    sk, inner = sd_skirt(p, 1.05 + U * .3, 0.34, 0.165, 0.31, 1.18, 0.40, ANIM.get('coat_open', 0.80), 0.011,
                         back=-0.07 + ANIM['sway'] + ANIM['coat_back'], sz=ANIM.get('coat_sz', 1.0), solid=ANIM.get('solid', False),
                         drag=ANIM.get('coat_drag', 0.0), flap=ANIM.get('coat_flap', 0.0), ph=ANIM.get('coat_ph', 0.0),
                         lift=ANIM.get('coat_lift', 0.0))
    add(sk, COAT)
    # gola alta
    col, _ = sd_skirt(p, 1.66 + U, 1.44 + U, 0.135, 0.09, 1.15, 0.75, 0.55, 0.012)
    add(col, COAT)
    # pernas
    for s in ("R", "L"):
        add(sd_capsule(pw, J["hip" + s], J["kn" + s], 0.075, 0.058), CLOTH)
        add(sd_capsule(pw, J["kn" + s], J["an" + s], 0.058, 0.048), LEATHER)
        kn = J["kn" + s]; an = J["an" + s]
        add(sd_ellipsoid(pw, kn + [0, 0.0, 0.035], np.array([0.068, 0.062, 0.05])), STEEL)
        mid = kn + (an - kn) * 0.42 + [0, 0, 0.03]
        add(sd_ellipsoid(pw, mid, np.array([0.05, 0.16, 0.042])), STEEL)
        fdir = norm([0.05 if (s == "R" or ANIM["lean"]) else 0.55, 0, 1])
        u, v, w = frame(fdir, (0, 1, 0))
        add(sd_box_oriented(pw, an + [0, -0.055, 0] + fdir * 0.055, (u, v, w), (0.05, 0.045, 0.12), 0.03), LEATHER)
        add(sd_capsule(pw, an + [0, 0.02, 0], an + [0, 0.10, 0], 0.062, 0.062) * 1.0, LEATHER)
    # braços
    for s in ("R", "L"):
        add(sd_capsule(p, J["sh" + s], J["el" + s], 0.07, 0.058), COAT)
        add(sd_capsule(p, J["el" + s], J["wr" + s], 0.06, 0.05), STEEL)
        add(sd_sphere(p, J["ha" + s], 0.05), LEATHER)
        sx = -1 if s == "R" else 1
        sh = J["sh" + s]
        for i, (dy, r) in enumerate(((0.03, (0.092, 0.062, 0.095)), (-0.025, (0.088, 0.055, 0.09)))):
            add(sd_ellipsoid(p, sh + [sx * (0.012 + i * 0.018), dy, 0], np.array(r)), STEEL)
    # espada
    if with_sword:
        ha = J["haR"]; tip = SWORD_TIP + (ha - J0["haR"]) * 0.4
        if ANIM['sword_dir'] is not None: tip = ha + ANIM['sword_dir'] * 0.98
        b = norm(tip - ha)
        u, v, w = frame(b, np.cross(b, CAM_DIR))
        L = np.linalg.norm(tip - ha)
        g = ha + b * 0.07
        add(sd_box_oriented(p, g + b * (L - 0.07) / 2, (u, v, w), (0.006, 0.026, (L - 0.07) / 2), 0.002), BLADE)
        add(sd_box_oriented(p, g, (u, v, w), (0.018, 0.11, 0.014), 0.008), GOLD)
        add(sd_capsule(p, ha - b * 0.11, ha + b * 0.05, 0.017, 0.017), GRIP)
        add(sd_sphere(p, ha - b * 0.13, 0.028), GOLD)
    D = np.stack([d for d, _ in parts], 1)
    i = D.argmin(1); d = D[np.arange(len(p)), i]
    if not want_mat: return d
    mats = np.array([m for _, m in parts])[i]
    # forro: lado de dentro da aba e da gola, e as bordas da abertura
    sk_i = [k for k, (dd, m) in enumerate(parts) if dd is sk][0]
    mats = np.where((i == sk_i) & inner, LINING, mats)
    col_i = [k for k, (dd, m) in enumerate(parts) if dd is col][0]
    _, cin = sd_skirt(p, 1.66 + ANIM['up'], 1.44 + ANIM['up'], 0.135, 0.09, 1.15, 0.75, 0.55, 0.012)
    mats = np.where((i == col_i) & cin, LINING, mats)
    # colete na frente do tronco, com lapelas vermelhas
    tor_i = [k for k, (dd, m) in enumerate(parts) if dd is torso][0]
    ax = np.abs(p[:, 0]); front = p[:, 2] > 0.02
    U = ANIM['up']
    vest = (i == tor_i) & front & (ax < 0.055 + (1.45 + U - p[:, 1]) * 0.05) & (p[:, 1] < 1.47 + U)
    lap = (i == tor_i) & front & ~vest & (ax < 0.078 + (1.45 + U - p[:, 1]) * 0.05) & (p[:, 1] < 1.49 + U)
    mats = np.where(vest, CLOTH, mats); mats = np.where(lap, LINING, mats)
    return d, mats
