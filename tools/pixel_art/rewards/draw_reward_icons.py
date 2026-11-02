"""Desenha os emblemas de recompensa (32x32, paleta AAP-64, alfa binário): Fruta do Diabo, Bigorna e Moedas de Sangue.
Substituem os antigos emblemas em geometria 3D de DoorRewardIcon e o visual 3D do DevilsFruit.

Arte própria, gerada por código. Para regerar:

    py tools/pixel_art/rewards/draw_reward_icons.py [--preview arquivo.png]
"""
import argparse
import pathlib

from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "assets" / "pixel_art" / "rewards"
S = 32
OUTLINE = "141013"


def c(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def new():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def outline(img):
    """Contorno de 1 px nos pixels transparentes vizinhos (4 direções)."""
    px = img.load()
    add = []
    for y in range(S):
        for x in range(S):
            if px[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < S and 0 <= ny < S and px[nx, ny][3] and px[nx, ny][:3] != c(OUTLINE)[:3]:
                    add.append((x, y))
                    break
    for p in add:
        px[p] = c(OUTLINE)
    return img


def fruit():
    img = new()
    px = img.load()
    cx, cy, rx, ry = 15.5, 19.0, 10.0, 10.5
    shades = ["73172d", "b4202a", "df3e23", "e86a73"]
    for y in range(S):
        for x in range(S):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = dx * dx + dy * dy
            if d <= 1.0:
                # luz no alto à esquerda
                l = (-dx * 0.6 - dy * 0.8)
                v = 0.5 + 0.5 * l - 0.35 * d
                i = 0 if v < 0.15 else 1 if v < 0.45 else 2 if v < 0.8 else 3
                px[x, y] = c(shades[i])
    # reflexo
    for p in [(10, 14), (11, 13), (12, 12), (10, 15), (11, 14)]:
        px[p] = c("fef3c0")
    # haste e folha
    for p in [(15, 8), (16, 8), (16, 7), (16, 6), (17, 5)]:
        px[p] = c("71413b")
    for p in [(18, 6), (19, 6), (20, 6), (19, 7), (20, 7), (21, 7), (20, 8)]:
        px[p] = c("59c135")
    px[19, 7] = c("9cdb43")
    px[20, 7] = c("9cdb43")
    return outline(img)


def anvil():
    img = new()
    d = ImageDraw.Draw(img)
    steel, light, dark, shine = c("8b93af"), c("b3b9d1"), c("4a5462"), c("e3e6ff")
    d.rectangle((8, 25, 23, 27), fill=dark)       # base
    d.rectangle((10, 23, 21, 24), fill=steel)
    d.rectangle((12, 15, 19, 22), fill=steel)     # cintura
    d.rectangle((13, 15, 14, 22), fill=light)
    d.rectangle((4, 9, 25, 14), fill=steel)       # tampo
    d.rectangle((4, 9, 25, 10), fill=light)
    d.polygon([(26, 9), (30, 10), (30, 11), (26, 14)], fill=steel)  # chifre
    d.rectangle((26, 9, 29, 10), fill=light)
    d.rectangle((6, 9, 12, 9), fill=shine)
    d.rectangle((4, 13, 25, 14), fill=dark)
    return outline(img)


def coins():
    img = new()
    d = ImageDraw.Draw(img)
    face, rim, edge, shine = c("dba463"), c("f4d29c"), c("bb7547"), c("fef3c0")

    def coin(cx, y, w=11, h=4):
        d.ellipse((cx - w, y - h, cx + w, y + h), fill=edge)
        d.rectangle((cx - w, y, cx + w, y + 3), fill=edge)
        d.ellipse((cx - w, y - h + 3 - 3, cx + w, y + h - 3 + 3 - 3), fill=edge)
        d.ellipse((cx - w, y - h, cx + w, y + h - 1), fill=rim)
        d.ellipse((cx - w + 2, y - h + 1, cx + w - 2, y + h - 2), fill=face)
        d.point([(cx - 4, y - 2), (cx - 3, y - 2)], fill=shine)

    coin(16, 22)
    coin(16, 17)
    coin(16, 12)
    # gota de sangue sobre a pilha
    d.point([(16, 11), (15, 12), (17, 12)], fill=c("b4202a"))
    d.point([(16, 12), (16, 13)], fill=c("df3e23"))
    return outline(img)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    a = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    icons = {"devils_fruit": fruit(), "anvil": anvil(), "blood_coins": coins()}
    for n, im in icons.items():
        im.save(OUT / f"{n}.png")
    if a.preview:
        sheet = Image.new("RGBA", (S * 3 * 6, S * 6), c("3b1725"))
        for i, im in enumerate(icons.values()):
            sheet.alpha_composite(im.resize((S * 6, S * 6), Image.NEAREST), (i * S * 6, 0))
        sheet.save(a.preview)


if __name__ == "__main__":
    main()
