"""Desenha o ícone do Sangue Amaldiçoado (32x32, paleta AAP-64): uma gota de sangue que escurece para o violeta
da maldição na base, com veias violetas subindo e um brilho claro no alto (o sangue que Virgil purifica).

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

# Contorno sem o preto do fundo dos painéis, para a gota não sumir neles: vinho em cima, violeta na base.
OUTLINE = "3b1725"
OUTLINE_CURSED = "403353"
# Corpo: do claro (alto, à esquerda) ao escuro, vermelho em cima e violeta da maldição embaixo.
CRIMSON = ["df3e23", "b4202a", "73172d", "3b1725"]
CURSED = ["bc4a9b", "bc4a9b", "793a80", "793a80"]
VEIN = "793a80"
GLOW = "f389f5"
SHINE = ["f389f5", "fef3c0"]

CX, CY, R = 15.5, 20.0, 10.0
TIP = (15.5, 2.5)


def rgb(hex_color):
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def inside(x, y):
    """Gota: círculo embaixo + cone tangente até a ponta."""
    if (x - CX) ** 2 + (y - CY) ** 2 <= R * R:
        return True
    tx, ty = TIP
    if y < ty or y > CY:
        return False
    d = CY - ty
    half_angle = math.asin(R / d)
    tangent_y = ty + d * math.cos(half_angle) ** 2
    if y > tangent_y:
        return False
    width = (y - ty) * math.tan(half_angle)
    return abs(x - tx) <= width


def shade(x, y):
    # Luz do alto à esquerda: quanto mais longe do ponto de luz, mais escuro.
    lx, ly = CX - 4.5, CY - 6.0
    dist = math.hypot(x - lx, y - ly) / (R * 1.6)
    level = min(3, max(0, int(dist * 4)))
    # Maldição: o violeta toma a base da gota (com uma borda ondulada).
    wave = 1.4 * math.sin(x * 0.9) + 0.8 * math.sin(x * 2.1 + 1.0)
    cursed = y > CY + 1.5 + wave
    return (CURSED if cursed else CRIMSON)[level]


def is_vein(x, y):
    # Duas veias finas subindo da base pelo vermelho, em zigue-zague.
    for base_x, phase in ((11.5, 0.0), (19.5, 1.7)):
        vx = base_x + 1.2 * math.sin(y * 0.8 + phase)
        if abs(x - vx) < 0.55 and CY - 4 <= y <= CY + 4:
            return True
    return False


def draw():
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    mask = [[inside(x + 0.5, y + 0.5) for x in range(SIZE)] for y in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            if not mask[y][x]:
                continue
            edge = any(
                not (0 <= x + dx < SIZE and 0 <= y + dy < SIZE and mask[y + dy][x + dx])
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge:
                px[x, y] = rgb(OUTLINE_CURSED if shade(x + 0.5, y + 0.5) in CURSED else OUTLINE)
                continue
            color = shade(x + 0.5, y + 0.5)
            if color in CRIMSON and is_vein(x + 0.5, y + 0.5):
                color = VEIN
            elif color in CURSED and shade(x + 0.5, y - 0.5) in CRIMSON:
                # Borda da maldição: uma linha rosa-violeta clara onde o vermelho vira violeta.
                color = GLOW
            px[x, y] = rgb(color)
    # Brilho: um traço curvo no alto à esquerda e um ponto quase branco.
    for x, y in ((10, 15), (10, 16), (10, 17), (11, 13), (11, 14), (12, 12)):
        px[x, y] = rgb(SHINE[0])
    px[11, 15] = rgb(SHINE[1])
    px[10, 19] = rgb(SHINE[0])
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
