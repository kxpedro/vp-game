"""Boneco de treino do hub (modo 2D): folha de quadros em pixel art no estilo do kit da cripta.

Poste de madeira num pedestal de pedra, saco de estopa recheado de palha amarrado com corda, braços de madeira
com trapos, cabeça de saco costurada e um alvo de sangue pintado no peito. Mesmo renderizador e paleta das peças
(iso.py, AAP-64), mas fica fora da sala: o jogo o desenha como sprite (TrainingDummySprite.cs) para ele balançar
ao apanhar.

Quadros (faixa horizontal, CELL x CELL): 0 = parado; 1..N-1 = balanço do golpe (pende para a direita da tela e
volta, amortecendo). O jogo espelha os quadros quando o golpe vem da direita.
Os pés (centro da base do pedestal) ficam no pixel (CELL/2, FEET_Y) de cada quadro.

Uso: py tools/pixel_art/crypt_kit/training_dummy.py [arquivo_de_saida.png]
"""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso import Canvas, FID, vnoise
from materials import grain, plain_stone, side_light

CELL, FEET_Y = 96, 84
# Inclinação (graus, positivo = para a direita da tela) de cada quadro: parado e o balanço amortecido do golpe.
LEAN = [0, 15, 10, -6, -3, 2]
AMBIENT = -.5
# Luz fixa vinda da frente e do alto (o sprite não recebe a luz das velas da sala).
LIGHTS = [(30, 30, 60, 1.2, 55)]

WARM, WOOD, BLOOD, IRON = FID["warm"], FID["wood"], FID["blood"], FID["iron"]
PIVOT_Z = 6.0      # o boneco balança a partir do topo do pedestal


class LeanCanvas(Canvas):
    """Canvas que gira o que for desenhado acima do pivô em torno do eixo de profundidade da tela (1, 1, 0)."""
    lean = 0.0
    def plot(s, wx, wy, wz, fam, val, emit=False, depth_bias=0.0):
        if s.lean:
            a = np.radians(s.lean)
            u = (wx - wy) / np.sqrt(2)                       # eixo horizontal da tela (direita = +)
            v = (wx + wy) / np.sqrt(2)                       # profundidade (para a câmera = +)
            h = np.maximum(wz - PIVOT_Z, 0)
            u2 = u * np.cos(a) + h * np.sin(a)
            h2 = -u * np.sin(a) + h * np.cos(a)
            lift = wz > PIVOT_Z
            wx = np.where(lift, (u2 + v) / np.sqrt(2), wx)
            wy = np.where(lift, (v - u2) / np.sqrt(2), wy)
            wz = np.where(lift, h2 + PIVOT_Z, wz)
        Canvas.plot(s, wx, wy, wz, fam, val, emit, depth_bias)
    def sprite(s, rows, colors, wx, wy, wz, depth_bias=2):
        # sprites à mão seguem o balanço só pela âncora
        if s.lean and wz > PIVOT_Z:
            a = np.radians(s.lean)
            u, v, h = (wx - wy) / np.sqrt(2), (wx + wy) / np.sqrt(2), wz - PIVOT_Z
            u, h = u * np.cos(a) + h * np.sin(a), -u * np.sin(a) + h * np.cos(a)
            wx, wy, wz = (u + v) / np.sqrt(2), (v - u) / np.sqrt(2), h + PIVOT_Z
        Canvas.sprite(s, rows, colors, wx, wy, wz, depth_bias)


def wood(shift=0.0):
    """madeira velha do poste e dos braços: veio vertical"""
    def tex(face, wx, wy, wz, th, rr):
        v = side_light(th) + .4 + shift + (vnoise(th * 3, wz * .25, 31) - .5) * 1.4 if face == "side" else 4.0 + shift + wx * 0
        return np.full(np.shape(v), WOOD), v
    return tex


def burlap(shift=0.0, target_z=None):
    """estopa do saco: trama miúda, manchas e, no peito, um alvo pintado de sangue virado para a câmera"""
    def tex(face, wx, wy, wz, th, rr):
        if face == "top":
            v = 4.6 + shift + grain(wx, wy, wz, .5)
            return np.full(np.shape(v), WARM), v
        weave = ((np.floor(wz) + np.floor(th * 9)) % 2) * .35
        v = side_light(th) + 2.4 + shift + weave + (vnoise(th * 4, wz * .3, 17) - .5) * 1.3
        fam = np.full(np.shape(v), WARM)
        if target_z is not None:
            # frente do saco = th perto de pi/4 (lado +x +y, o mais perto da câmera)
            d = np.hypot((th - np.pi / 4) * 9.5, (wz - target_z) * 1.0)
            ring = (d < 2.2) | ((d > 4.2) & (d < 6.2))
            worn = vnoise(th * 12, wz * .9, 5) > .78                  # tinta descascada
            fam = np.where(ring & ~worn, BLOOD, fam)
            v = np.where(ring & ~worn, 2.3 + (d < 2.2) * .9, v)
            cut = (np.abs((th - np.pi / 4) * 9.5 + (wz - target_z) * .9 - 7) < .6) & (np.abs(wz - target_z + 3) < 3)
            fam = np.where(cut, WARM, fam); v = np.where(cut, 1.0, v)   # rasgo de lâmina
        return fam, v
    return tex


def rope(face, wx, wy, wz, th, rr):
    v = (side_light(th) + 2.0 + ((np.floor(wz * 2) + np.floor(th * 7)) % 2) * .6) if face == "side" else 4.0 + wx * 0
    return np.full(np.shape(v), WARM), v


STRAW = ["O   o ", " o O  o", "  oOo  ", " ooOoo "]
DANGLE = ["oOo", "o o", "O  "]
STRAW_C = {"o": ("warm", 8), "O": ("warm", 9)}
FACE = [
    "x x x x",
    " x   x ",
    "x x x x",
    "       ",
    " -.-.- ",
]
FACE_C = {"x": ("blood", 3), ".": ("warm", 1), "-": ("warm", 2)}


def draw(cv, t_lean):
    cv.lean = t_lean
    cx = cy = 0.0
    # pedestal de pedra (não balança: fica abaixo do pivô)
    cv.box(cx - 13, cx + 13, cy - 13, cy + 13, 0, 3, plain_stone(.1, frame=2))
    cv.box(cx - 10, cx + 10, cy - 10, cy + 10, 3, 6, plain_stone(.3, frame=2))
    # poste
    cv.cyl(cx, cy, 2.2, 6, 34, wood(-.3))
    # saco do corpo (estopa) amarrado com corda em cima e embaixo, palha escapando
    for z in np.arange(18, 40, 1.0):
        k = (z - 18) / 22
        r = 5.6 + 1.6 * np.sin(k * np.pi) + (k > .75) * 1.0 - (k > .95) * 2.0
        cv.cyl(cx, cy, r, z, z + 1, burlap(0, target_z=28), top=(z == 39))
    for zr, rr in ((19.5, 6.0), (37.5, 7.4)):
        cv.cyl(cx, cy, rr, zr - .8, zr + .8, rope, top=False)
    # braços: travessa de madeira na horizontal da tela, com trapos nas pontas
    for t in np.arange(-16, 16.1, .8):
        x, y = cx + t / np.sqrt(2), cy - t / np.sqrt(2)
        cv.cyl(x, y, 1.4, 33, 35.5, wood(.1), top=True)
    for sgn in (-1, 1):
        x, y = cx + sgn * 15 / np.sqrt(2), cy - sgn * 15 / np.sqrt(2)
        cv.cyl(x, y, 2.2, 31, 36.5, burlap(-.6))
        cv.cyl(x, y, 2.4, 33.2, 34.2, rope, top=False)
    # pescoço e cabeça de saco
    cv.cyl(cx, cy, 2.2, 39, 43, rope)
    for z in np.arange(42, 53, 1.0):
        r = 5.4 * np.sqrt(max(1 - ((z + .5 - 47.5) / 5.6) ** 2, .05))
        cv.cyl(cx, cy, r, z, z + 1, burlap(.2), top=(z == 52))
    # tufo de palha no alto da cabeça e no corte do peito, costura do rosto
    fx, fy = cx + 4.6 / np.sqrt(2), cy + 4.6 / np.sqrt(2)
    cv.sprite(FACE, FACE_C, fx + 1, fy + 1, 44, depth_bias=6)
    cv.sprite(STRAW, STRAW_C, cx + .5, cy + .5, 54, depth_bias=2)
    cv.sprite(DANGLE, STRAW_C, cx + 4, cy + 4, 15.5, depth_bias=8)


def render_frame(lean):
    cv = LeanCanvas(CELL, CELL, CELL // 2, FEET_Y)
    draw(cv, lean)
    return cv.render(LIGHTS, ambient=AMBIENT, bg=None)


def main(out):
    sheet = Image.new("RGBA", (CELL * len(LEAN), CELL), (0, 0, 0, 0))
    for i, lean in enumerate(LEAN):
        sheet.paste(render_frame(lean), (i * CELL, 0))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.save(out)
    print(out, sheet.size)


if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "..", "..", "assets", "pixel_art", "crypt", "sprites", "training_dummy.png"))
