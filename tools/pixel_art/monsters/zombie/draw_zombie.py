"""Desenha por código o zumbi do Jardim Sangrento (modo 2D), no padrão das plantas predadoras: quadros de 64x64
olhando para a direita, pés em y=51, 1 pixel de arte = 1 pixel da folha (sem ampliação), paleta AAP-64 e alfa
binário. A `Ghoul Sprite Sheet.png` do Pedro serviu só de referência de pose e de clima (curvado, braços à
frente, boca ensanguentada); nenhum pixel dela é usado.

Grava `assets/pixel_art/monsters/zombie/{idle,walk,attack,hit,death}.png` (faixas horizontais) e, com
`--preview arquivo.png`, uma prancha ampliada com todos os quadros. Para regerar:

    py tools/pixel_art/monsters/zombie/draw_zombie.py [--preview prancha.png]

O corpo é um esqueleto de juntas (quadril, ombro, cabeça, joelhos, cotovelos, mãos e pés) posado quadro a quadro;
cada parte é uma cápsula sombreada em faixas pela luz de cima à esquerda, com contorno escuro por fora e linha de
contato onde uma parte da frente passa sobre outra.
"""
import argparse
import math
import pathlib

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[4]
TARGET = ROOT / "assets" / "pixel_art" / "monsters" / "zombie"
FRAME = 64
GROUND = 51


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# Rampas da AAP-64 (sombra -> luz).
SKIN = [rgb(h) for h in ("4a5462", "6d758d", "8b93af", "b3b9d1")]
SKIN_BACK = [rgb(h) for h in ("242234", "333941", "4a5462", "6d758d")]
ROT = [rgb(h) for h in ("24523b", "23674e", "477d85", "477d85")]
RAGS = [rgb(h) for h in ("3b1725", "5b3138", "8e5252", "8e5252")]
RAGS_BACK = [rgb(h) for h in ("242234", "3b1725", "5b3138", "5b3138")]
PANTS = [rgb(h) for h in ("221c1a", "322b28", "5a4e44", "796755")]
PANTS_BACK = [rgb(h) for h in ("141013", "221c1a", "322b28", "5a4e44")]
OUTLINE = rgb("141013")
BLOOD_DARK = rgb("73172d")
BLOOD = rgb("b4202a")
EYE = rgb("e86a73")
TEETH = rgb("c7b08b")
MOUTH = rgb("141013")
SMEAR = [rgb("8b93af"), rgb("b3b9d1"), rgb("dae0ea")]
LIGHT = np.array([-0.55, -0.83])

YS, XS = np.mgrid[0:FRAME, 0:FRAME]
PX = XS + 0.5
PY = YS + 0.5


class Canvas:
    def __init__(self):
        self.color = np.zeros((FRAME, FRAME, 3), np.uint8)
        self.alpha = np.zeros((FRAME, FRAME), bool)

    def part(self, mask, shade, ramp, contact=True):
        """Pinta uma parte sombreada; a borda dela sobre o que já existe vira linha de contato (sombra)."""
        if not mask.any():
            return
        band = np.where(shade > 0.45, 3, np.where(shade > -0.05, 2, np.where(shade > -0.6, 1, 0)))
        cols = np.array(ramp, np.uint8)[band]
        if contact:
            inner = mask & shifted_all(mask)
            edge = mask & ~inner & self.alpha
            cols[edge] = ramp[0]
        self.color[mask] = cols[mask]
        self.alpha |= mask

    def dot(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < FRAME and 0 <= y < FRAME:
            self.color[y, x] = c
            self.alpha[y, x] = True

    def finish(self):
        outline = ~self.alpha & neighbors_any(self.alpha)
        img = np.zeros((FRAME, FRAME, 4), np.uint8)
        img[self.alpha, :3] = self.color[self.alpha]
        img[self.alpha, 3] = 255
        img[outline, :3] = OUTLINE
        img[outline, 3] = 255
        return Image.fromarray(img, "RGBA")


def shifted_all(m):
    out = m.copy()
    out[1:, :] &= m[:-1, :]
    out[:-1, :] &= m[1:, :]
    out[:, 1:] &= m[:, :-1]
    out[:, :-1] &= m[:, 1:]
    out[0, :] = out[-1, :] = False
    out[:, 0] = out[:, -1] = False
    return out


def neighbors_any(m):
    out = np.zeros_like(m)
    out[1:, :] |= m[:-1, :]
    out[:-1, :] |= m[1:, :]
    out[:, 1:] |= m[:, :-1]
    out[:, :-1] |= m[:, 1:]
    return out


def capsule(a, b, ra, rb):
    """Máscara e sombreado de uma cápsula de a até b (raios ra e rb nas pontas)."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    length2 = max(dx * dx + dy * dy, 1e-6)
    t = np.clip(((PX - ax) * dx + (PY - ay) * dy) / length2, 0.0, 1.0)
    cx, cy = ax + t * dx, ay + t * dy
    r = ra + (rb - ra) * t
    ox, oy = PX - cx, PY - cy
    d = np.hypot(ox, oy)
    mask = d <= r
    nx, ny = ox / np.maximum(r, 1e-6), oy / np.maximum(r, 1e-6)
    shade = nx * LIGHT[0] + ny * LIGHT[1]
    return mask, shade


def ellipse(c, rx, ry):
    ox, oy = (PX - c[0]) / rx, (PY - c[1]) / ry
    d = np.hypot(ox, oy)
    return d <= 1.0, ox * LIGHT[0] + oy * LIGHT[1]


def limb(cv, root, mid, end, r0, r1, r2, ramp_top, ramp_bottom, split=1.0):
    """Membro em dois segmentos; `split` = até onde do segundo segmento vai a roupa (1 = só o primeiro)."""
    m1, s1 = capsule(root, mid, r0, r1)
    m2, s2 = capsule(mid, end, r1, r2)
    cv.part(m2, s2, ramp_bottom)
    cv.part(m1, s1, ramp_top)


def hand(cv, at, toward, ramp):
    m, s = ellipse(at, 1.6, 1.4)
    cv.part(m, s, ramp)
    # Garras: dois pixels escuros na ponta.
    dx, dy = toward
    n = math.hypot(dx, dy) or 1.0
    dx, dy = dx / n, dy / n
    cv.dot(at[0] + dx * 2.2, at[1] + dy * 2.2 - 0.6, OUTLINE)
    cv.dot(at[0] + dx * 2.2, at[1] + dy * 2.2 + 0.9, OUTLINE)


def draw(p):
    """p: dicionário de juntas (tuplas x,y) mais `smear` (lista de pontos do rastro do golpe) e `blood`."""
    cv = Canvas()
    for x, y in p.get("pool", []):
        cv.dot(x, y, BLOOD_DARK)

    # Fundo: perna e braço de trás.
    limb(cv, p["hip_b"], p["knee_b"], p["foot_b"], 2.4, 2.0, 1.7, PANTS_BACK, SKIN_BACK)
    fm, fs = ellipse((p["foot_b"][0] + 1.0, p["foot_b"][1] + 0.4), 2.2, 1.1)
    cv.part(fm, fs, SKIN_BACK)
    limb(cv, p["shoulder_b"], p["elbow_b"], p["hand_b"], 2.0, 1.7, 1.5, RAGS_BACK, SKIN_BACK)
    hand(cv, p["hand_b"], np.subtract(p["hand_b"], p["elbow_b"]), SKIN_BACK)

    # Tronco: cápsula curvada (quadril -> meio -> ombro), camisa rasgada por cima da pele.
    hip, chest, shoulder = p["hip"], p["chest"], p["shoulder"]
    m1, s1 = capsule(hip, chest, 4.6, 5.2)
    m2, s2 = capsule(chest, shoulder, 5.2, 4.4)
    torso, tshade = m1 | m2, np.where(m2, s2, s1)
    cv.part(torso, tshade, SKIN)
    # Trapos: cobrem o tronco menos rasgos fixos no referencial do tronco (não piscam entre quadros).
    ux, uy = shoulder[0] - hip[0], shoulder[1] - hip[1]
    ul = math.hypot(ux, uy) or 1.0
    ux, uy = ux / ul, uy / ul
    along = (PX - hip[0]) * ux + (PY - hip[1]) * uy
    side = (PX - hip[0]) * -uy + (PY - hip[1]) * ux
    hem = 1.5 + 1.6 * np.sin(side * 1.7) + 0.8 * np.sin(side * 3.1 + 1.0)
    tear = (np.abs(along - 7.0 - side * 0.4) < 0.9) & (side > -1.5) & (side < 2.5)
    rags = torso & (along > hem) & ~tear & (along < ul + 2.0)
    cv.part(rags, tshade, RAGS, contact=False)
    # Mancha de podridão na barriga.
    rot = torso & ((along - 4.0) ** 2 / 3.0 + (side - 3.0) ** 2 / 1.5 < 1.0) & ~rags
    cv.part(rot, tshade, ROT, contact=False)
    # Sangue escorrido no peito.
    for i in range(4):
        cv.dot(shoulder[0] + 2.5 + ux * -i * 1.2, shoulder[1] + 1.5 + uy * -i * 1.2 + i * 0.3, BLOOD if i < 2 else BLOOD_DARK)

    # Perna da frente (calça rasgada até o joelho).
    limb(cv, p["hip_f"], p["knee_f"], p["foot_f"], 2.6, 2.1, 1.8, PANTS, SKIN)
    fm, fs = ellipse((p["foot_f"][0] + 1.0, p["foot_f"][1] + 0.4), 2.3, 1.1)
    cv.part(fm, fs, SKIN)

    # Cabeça: crânio, mandíbula caída, olho vermelho, boca escura com dentes e sangue.
    head, jaw = p["head"], p["jaw"]
    jm, js = ellipse(jaw, 2.6, 1.7)
    cv.part(jm, js, SKIN)
    hm, hs = ellipse(head, 4.4, 4.0)
    cv.part(hm, hs, SKIN)
    face = p.get("face", 1.0)
    ex, ey = head[0] + 1.6 * face, head[1] - 0.6
    cv.dot(ex, ey, EYE)
    cv.dot(ex - 1.0 * face, ey, BLOOD_DARK)
    cv.dot(head[0] - 1.5 * face, head[1] - 2.2, SKIN[0])
    mx, my = head[0] + 2.6 * face, head[1] + 2.4
    for dx in range(-1, 2):
        cv.dot(mx + dx * face, my, MOUTH)
        cv.dot(mx + dx * face, my + 1, MOUTH)
    cv.dot(mx - 1 * face, my - 0.2, TEETH)
    cv.dot(mx + 1 * face, my + 1.2, TEETH)
    for i in range(p.get("drool", 2)):
        cv.dot(mx, my + 2 + i, BLOOD if i == 0 else BLOOD_DARK)
    # Cabelo ralo atrás.
    for dx, dy in ((-3, -3), (-4, -2), (-2, -4), (-4, 0)):
        cv.dot(head[0] + dx * face, head[1] + dy, SKIN_BACK[0])

    # Braço da frente por cima de tudo.
    limb(cv, p["shoulder_f"], p["elbow_f"], p["hand_f"], 2.2, 1.8, 1.6, RAGS, SKIN)
    hand(cv, p["hand_f"], np.subtract(p["hand_f"], p["elbow_f"]), SKIN)

    for i, (x, y) in enumerate(p.get("smear", [])):
        cv.dot(x, y, SMEAR[min(2, i * 3 // max(1, len(p["smear"])))])
    return cv.finish()


# --------------------------------------------------------------------------------------------- poses

def base(bob=0.0, lean=0.0, sway=0.0, dx=0.0):
    """Pose parada: curvado para a frente, braços estendidos. `lean` empurra ombro e cabeça (px)."""
    hx = 28.0 + dx
    hip = (hx, 37.0 + bob)
    chest = (hx + 2.5 + lean * 0.5, 31.0 + bob)
    shoulder = (hx + 5.5 + lean, 25.5 + bob)
    head = (hx + 10.0 + lean * 1.2, 22.0 + bob * 1.2)
    jaw = (head[0] + 2.2, head[1] + 3.2)
    return {
        "hip": hip, "chest": chest, "shoulder": shoulder, "head": head, "jaw": jaw,
        "hip_f": (hip[0] + 1.0, hip[1] + 1.0), "knee_f": (hx + 3.5, 44.0 + bob * 0.5), "foot_f": (hx + 2.0, GROUND - 1.0),
        "hip_b": (hip[0] - 1.0, hip[1] + 0.5), "knee_b": (hx - 1.5, 44.0 + bob * 0.5), "foot_b": (hx - 3.5, GROUND - 1.0),
        "shoulder_f": (shoulder[0] + 0.5, shoulder[1] + 1.0),
        "elbow_f": (shoulder[0] + 5.0 + lean * 0.5, shoulder[1] + 6.5 + sway),
        "hand_f": (shoulder[0] + 11.5 + lean, shoulder[1] + 9.0 + sway * 1.6),
        "shoulder_b": (shoulder[0] - 0.5, shoulder[1] - 0.5),
        "elbow_b": (shoulder[0] + 5.5 + lean * 0.5, shoulder[1] + 4.0 - sway),
        "hand_b": (shoulder[0] + 12.0 + lean, shoulder[1] + 5.0 - sway * 1.6),
    }


def leg(p, side, foot, lift, knee_fwd=2.5):
    hipk, kneek, footk = "hip_" + side, "knee_" + side, "foot_" + side
    fx, fy = foot, GROUND - 1.0 - lift
    hx, hy = p[hipk]
    p[footk] = (fx, fy)
    p[kneek] = ((hx + fx) / 2 + knee_fwd, (hy + fy) / 2 - 0.5 - lift * 0.4)


def idle_frames():
    out = []
    for i in range(4):
        a = i / 4 * 2 * math.pi
        out.append(base(bob=round(math.sin(a) * 0.8 + 0.2), sway=math.sin(a + 0.8) * 1.2))
    return out


def walk_frames():
    out = []
    for i in range(8):
        a = i / 8 * 2 * math.pi
        bob = -0.5 * abs(math.sin(a)) + 0.6
        p = base(bob=round(bob), sway=math.sin(a) * 1.5, lean=0.5)
        hx = p["hip"][0]
        # Passo arrastado: a perna da frente avança e levanta pouco; a de trás arrasta o pé.
        leg(p, "f", hx + 2.0 + 4.5 * math.sin(a), max(0.0, math.cos(a)) * 1.6)
        leg(p, "b", hx - 2.0 - 4.5 * math.sin(a), max(0.0, -math.cos(a)) * 0.6, knee_fwd=2.0)
        out.append(p)
    return out


def attack_frames():
    poses = []
    # 0-1: prepara (recua o tronco e ergue o braço da frente); 2-3: golpe com rastro; 4-5: volta.
    specs = [
        dict(lean=-1.5, hand=(31.0, 15.0), elbow=(30.0, 21.0), smear=[]),
        dict(lean=-2.5, hand=(28.0, 12.0), elbow=(29.0, 19.0), smear=[]),
        dict(lean=2.5, dx=1.5, hand=(47.0, 22.0), elbow=(40.0, 21.0), smear="high"),
        dict(lean=4.0, dx=2.5, hand=(49.0, 34.0), elbow=(43.0, 28.0), smear="low"),
        dict(lean=2.0, dx=1.5, hand=(46.0, 34.0), elbow=(40.0, 30.0), smear=[]),
        dict(lean=0.5, dx=0.5, hand=None, elbow=None, smear=[]),
    ]
    for s in specs:
        p = base(lean=s["lean"], dx=s.get("dx", 0.0))
        if s["hand"]:
            p["hand_f"] = (s["hand"][0] + s.get("dx", 0.0), s["hand"][1])
            p["elbow_f"] = (s["elbow"][0] + s.get("dx", 0.0), s["elbow"][1])
        if s["smear"] == "high":
            p["smear"] = arc(40, 27, 15, -95, -15)
        elif s["smear"] == "low":
            p["smear"] = arc(41, 27, 15, -40, 55)
        leg(p, "f", p["hip"][0] + 5.0, 0.0)
        poses.append(p)
    return poses


def arc(cx, cy, r, a0, a1):
    pts = []
    for k in range(14):
        a = math.radians(a0 + (a1 - a0) * k / 13)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def hit_frames():
    out = []
    for lean, sway, dx in ((-4.0, -2.5, -1.5), (-3.0, -1.5, -1.0), (-1.5, -0.5, -0.5), (0.0, 0.0, 0.0)):
        p = base(lean=lean, sway=sway, dx=dx)
        p["face"] = 1.0
        p["drool"] = 3
        out.append(p)
    return out


def rotate(p, angle, pivot):
    c, s = math.cos(angle), math.sin(angle)
    q = {}
    for k, v in p.items():
        if isinstance(v, tuple) and len(v) == 2:
            dx, dy = v[0] - pivot[0], v[1] - pivot[1]
            x, y = pivot[0] + dx * c - dy * s, pivot[1] + dx * s + dy * c
            q[k] = (x, min(y, GROUND - 2.0))
        else:
            q[k] = v
    return q


def death_frames():
    out = []
    steps = [(0.0, 0.0), (0.18, 1.0), (0.45, 2.5), (0.85, 3.5), (1.25, 4.5), (1.45, 5.0)]
    for i, (angle, sink) in enumerate(steps):
        p = base(lean=1.0 + i * 0.6, sway=2.0 + i)
        # Joelhos dobram primeiro, depois o corpo tomba para a frente sobre os pés.
        for key in ("hip", "chest", "shoulder", "head", "jaw", "hip_f", "hip_b", "shoulder_f", "shoulder_b",
                    "elbow_f", "elbow_b", "hand_f", "hand_b"):
            p[key] = (p[key][0], p[key][1] + sink)
        p["knee_f"] = (p["knee_f"][0] + sink * 0.8, p["knee_f"][1] + sink * 0.5)
        p["knee_b"] = (p["knee_b"][0] + sink * 0.6, p["knee_b"][1] + sink * 0.5)
        p = rotate(p, angle, (p["foot_f"][0] + 1.0, GROUND - 1.0))
        if i >= 4:
            p["pool"] = [(x, GROUND - 1 + (abs(x - 44) > 4)) for x in range(38, 51)]
        p["drool"] = 1
        out.append(p)
    return out


ANIMS = {
    "idle": idle_frames,
    "walk": walk_frames,
    "attack": attack_frames,
    "hit": hit_frames,
    "death": death_frames,
}


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--preview", help="prancha ampliada com todos os quadros, um clipe por linha")
    args = parser.parse_args()
    TARGET.mkdir(parents=True, exist_ok=True)
    strips = {}
    for name, fn in ANIMS.items():
        frames = [draw(p) for p in fn()]
        strip = Image.new("RGBA", (FRAME * len(frames), FRAME), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.alpha_composite(f, (i * FRAME, 0))
        strip.save(TARGET / f"{name}.png")
        strips[name] = strip
        print(f"{name}: {len(frames)} quadros")
    if args.preview:
        scale = 4
        width = max(s.width for s in strips.values())
        sheet = Image.new("RGBA", (width, FRAME * len(strips)), (36, 34, 52, 255))
        for row, s in enumerate(strips.values()):
            sheet.alpha_composite(s, (0, row * FRAME))
        sheet.resize((sheet.width * scale, sheet.height * scale), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
