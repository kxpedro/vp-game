"""Desenha o ícone do Ícor (o antigo Sangue Amaldiçoado; 32x32, paleta AAP-64): um frasco de vidro de fundo redondo,
com rolha, cheio de sangue que escurece para o violeta da maldição no fundo, com uma linha rosa onde as duas cores se
encontram e reflexos claros no vidro.

Arte própria, gerada por código (sem pacote de terceiros). Para regerar:

    py tools/pixel_art/hud/draw_cursed_blood.py [--preview arquivo.png]
"""
import argparse
import math
import pathlib

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "assets" / "pixel_art" / "hud" / "cursed_blood.png"
SIZE = 32

# Vidro: contorno cinza-azulado claro (não some no fundo escuro dos painéis), miolo vazio translúcido e reflexos.
GLASS_OUTLINE = "8b93af"
GLASS_EMPTY = ("4a5462", 150)
GLASS_SHINE = ["e3e6ff", "ffffff"]
# Rolha.
CORK = ["dba463", "bb7547", "71413b"]
# Sangue: vermelho em cima, violeta da maldição embaixo, do claro (esquerda) ao escuro (direita).
CRIMSON = ["df3e23", "b4202a", "73172d"]
CURSED = ["bc4a9b", "793a80", "403353"]
GLOW = "f389f5"

# Corpo redondo, gargalo e boca do frasco.
CX, CY, R = 15.5, 21.0, 9.6
NECK = (13, 18, 7, 13)        # x0, x1, y0, y1 (inclusivos)
LIP = (12, 19, 6, 7)
CORK_BOX = (13, 18, 2, 5)
LIQUID_TOP = 15.5


def rgba(hex_color, alpha=255):
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def in_box(x, y, box):
    x0, x1, y0, y1 = box
    return x0 <= x <= x1 and y0 <= y <= y1


def in_glass(x, y):
    return (x + 0.5 - CX) ** 2 + (y + 0.5 - CY) ** 2 <= R * R or in_box(x, y, NECK) or in_box(x, y, LIP)


def liquid_color(x, y):
    fx, fy = x + 0.5, y + 0.5
    # Luz da esquerda: quanto mais à direita, mais escuro.
    level = min(2, max(0, int((fx - (CX - R)) / (2 * R) * 3.2)))
    wave = 1.0 * math.sin(fx * 0.9) + 0.6 * math.sin(fx * 2.3 + 1.0)
    boundary = CY + 1.5 + wave
    if fy > boundary:
        return GLOW if fy - 1.0 <= boundary else CURSED[level]
    return CRIMSON[level]


def draw():
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if in_box(x, y, CORK_BOX):
                edge = x in (CORK_BOX[0], CORK_BOX[1]) or y == CORK_BOX[2]
                px[x, y] = rgba(CORK[2] if edge else (CORK[0] if x <= 14 else CORK[1]))
                continue
            if not in_glass(x, y):
                continue
            edge = any(not in_glass(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge:
                px[x, y] = rgba(GLASS_OUTLINE)
            elif y + 0.5 >= LIQUID_TOP and not in_box(x, y, NECK):
                px[x, y] = rgba(liquid_color(x, y))
            else:
                px[x, y] = rgba(*GLASS_EMPTY)
    # Superfície do sangue: uma linha mais clara logo abaixo do nível.
    for x in range(SIZE):
        y = int(LIQUID_TOP)
        if px[x, y][3] == 255 and px[x, y][:3] in [rgba(c)[:3] for c in CRIMSON]:
            px[x, y] = rgba("e86a73")
    # Reflexos no vidro: arco à esquerda do corpo e um traço no gargalo.
    for x, y in ((9, 17), (8, 18), (8, 19), (8, 20), (8, 21), (9, 23)):
        px[x, y] = rgba(GLASS_SHINE[0])
    px[8, 19] = rgba(GLASS_SHINE[1])
    for y in (8, 9, 10):
        px[14, y] = rgba(GLASS_SHINE[0])
    return img


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", help="grava também uma ampliação 8x para conferência")
    args = parser.parse_args()
    img = draw()
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    img.save(TARGET)
    print(f"gravado {TARGET.relative_to(ROOT)}")
    if args.preview:
        img.resize((SIZE * 8, SIZE * 8), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
