"""Desenha os 8 ícones das famílias de herança de sangue (32x32, paleta AAP-64, alfa binário).

Arte própria, gerada por código. Saída: assets/pixel_art/family_icons/<id>.png.
Uso: py tools/pixel_art/hud/draw_family_icons.py [--preview arquivo.png]
"""
import argparse
import math
import pathlib

from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "assets" / "pixel_art" / "family_icons"
S = 32
OL = "141013"  # contorno


def c(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


class Canvas:
    def __init__(self):
        self.img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def poly(self, pts, col):
        self.d.polygon(pts, fill=c(col))

    def ell(self, box, col):
        self.d.ellipse(box, fill=c(col))

    def rect(self, box, col):
        self.d.rectangle(box, fill=c(col))

    def px(self, x, y, col):
        if 0 <= x < S and 0 <= y < S:
            self.img.putpixel((x, y), c(col))

    def line(self, pts, col, w=1):
        self.d.line(pts, fill=c(col), width=w)

    def outline(self):
        src = self.img.copy()
        for y in range(S):
            for x in range(S):
                if src.getpixel((x, y))[3] == 0:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < S and 0 <= ny < S and src.getpixel((nx, ny))[3]:
                            self.img.putpixel((x, y), c(OL))
                            break
        return self.img


def dracul():  # cálice coroado, violeta
    k = Canvas()
    for pts in ([(8, 7), (11, 2), (14, 9)], [(12, 8), (16, 0), (20, 8)], [(18, 9), (21, 2), (24, 7)]):
        k.poly(pts, "793a80")
    k.poly([(10, 6), (11, 3), (13, 8)], "bc4a9b")
    k.ell((6, 7, 25, 13), "403353")
    k.poly([(6, 10), (25, 10), (22, 20), (9, 20)], "793a80")
    k.ell((6, 8, 25, 14), "bc4a9b")
    k.ell((8, 9, 23, 13), "403353")
    k.ell((10, 10, 17, 12), "793a80")
    k.rect((14, 20, 17, 25), "793a80")
    k.rect((14, 20, 15, 25), "bc4a9b")
    k.poly([(9, 26), (22, 26), (24, 30), (7, 30)], "793a80")
    k.rect((9, 26, 22, 27), "bc4a9b")
    k.line([(9, 12), (11, 18)], "e3e6ff")
    k.px(12, 11, "ffffff")
    return k.outline()


def bathory():  # gota de sangue sobre poça
    k = Canvas()
    k.ell((3, 24, 28, 30), "73172d")
    k.ell((6, 25, 25, 29), "b4202a")
    k.ell((11, 26, 20, 28), "df3e23")
    k.poly([(15, 1), (23, 13), (24, 18)], "b4202a")
    k.poly([(16, 1), (8, 13), (7, 18)], "b4202a")
    k.ell((7, 11, 24, 25), "b4202a")
    k.ell((12, 14, 24, 25), "73172d")
    k.ell((8, 13, 20, 24), "df3e23")
    k.poly([(16, 4), (9, 14), (13, 14)], "df3e23")
    k.line([(10, 15), (10, 19)], "f5a097", 2)
    k.px(12, 13, "f5a097")
    k.px(11, 14, "ffffff")
    return k.outline()


def orlok():  # cabeça de rato, pestilência verde
    k = Canvas()
    k.ell((2, 3, 12, 13), "23674e")
    k.ell((20, 3, 30, 13), "23674e")
    k.ell((4, 5, 10, 11), "92dcba")
    k.ell((22, 5, 28, 11), "92dcba")
    k.ell((6, 8, 26, 24), "328464")
    k.poly([(11, 20), (21, 20), (16, 30)], "328464")
    k.ell((14, 26, 18, 30), "e86a73")
    k.ell((9, 13, 14, 18), "d6f264")
    k.ell((18, 13, 23, 18), "d6f264")
    k.rect((11, 14, 12, 17), "141013")
    k.rect((20, 14, 21, 17), "141013")
    k.rect((15, 20, 16, 22), "141013")
    k.rect((14, 23, 14, 24), "ffffff")
    k.rect((17, 23, 17, 24), "ffffff")
    k.line([(0, 20), (8, 22)], "5daf8d")
    k.line([(0, 25), (8, 24)], "5daf8d")
    k.line([(31, 20), (23, 22)], "5daf8d")
    k.line([(31, 25), (23, 24)], "5daf8d")
    for x, y in ((5, 1), (16, 0), (26, 1)):
        k.px(x, y, "9cdb43")
    return k.outline()


def karnstein():  # coração com presas
    k = Canvas()
    k.ell((3, 4, 17, 18), "bc4a9b")
    k.ell((14, 4, 28, 18), "bc4a9b")
    k.poly([(3, 13), (28, 13), (15, 29)], "bc4a9b")
    k.poly([(18, 10), (28, 12), (16, 29), (15, 29)], "793a80")
    k.ell((18, 6, 27, 14), "793a80")
    k.ell((5, 6, 14, 15), "e86a73")
    k.ell((6, 6, 12, 11), "f5a097")
    k.poly([(7, 14), (14, 14), (15, 27)], "e86a73")
    k.px(7, 8, "ffffff")
    k.px(8, 7, "ffffff")
    k.poly([(9, 17), (13, 17), (11, 23)], "e3e6ff")
    k.poly([(18, 17), (22, 17), (20, 23)], "e3e6ff")
    k.px(11, 24, "b4202a")
    k.px(20, 24, "b4202a")
    k.px(11, 26, "b4202a")
    return k.outline()


def strigoi():  # olho na névoa, cinza azulado
    k = Canvas()
    k.poly([(1, 16), (8, 9), (16, 6), (24, 9), (31, 16), (24, 23), (16, 26), (8, 23)], "6d758d")
    k.poly([(4, 16), (10, 11), (16, 9), (22, 11), (28, 16), (22, 21), (16, 23), (10, 21)], "dae0ea")
    k.ell((10, 9, 22, 23), "849be4")
    k.ell((12, 11, 20, 21), "242234")
    k.ell((14, 13, 18, 19), "b9bffb")
    k.rect((15, 11, 16, 20), "242234")
    k.px(13, 12, "ffffff")
    for x, y, w in ((2, 27, 12), (8, 29, 10), (18, 28, 12)):
        k.ell((x, y, x + w, y + 2), "8b93af")
        k.ell((x + 2, y, x + w - 2, y + 1), "b3b9d1")
    for x, y in ((4, 3), (27, 3), (14, 2)):
        k.px(x, y, "8b93af")
        k.px(x + 1, y, "8b93af")
    return k.outline()


def varcolac():  # garras sobre lua crescente, marrom
    k = Canvas()
    k.ell((4, 2, 28, 28), "a08662")
    k.ell((11, 0, 35, 26), "000000")  # recorte (apagado abaixo)
    img = k.img
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.ell((6, 4, 20, 26), "a08662")
    k.ell((13, 2, 33, 24), "000000")
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.ell((4, 3, 15, 28), "f4d29c")
    k.ell((8, 3, 21, 29), "000000")
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    # garras: três cortes diagonais
    for i, x0 in enumerate((11, 17, 23)):
        pass
    for i, x0 in enumerate((11, 17, 23)):
        k.poly([(x0 + 1, 4), (x0 + 4, 4), (x0 + 3, 28), (x0 + 2, 28)], "bb7547")
        k.line([(x0 + 2, 5), (x0 + 2, 26)], "dba463")
        k.line([(x0 + 3, 12), (x0 + 3, 27)], "71413b")
    return k.outline()


def liaozhai():  # octógono de jade com trigramas
    k = Canvas()
    pts = [(10, 1), (21, 1), (30, 10), (30, 21), (21, 30), (10, 30), (1, 21), (1, 10)]
    k.poly(pts, "14a02e")
    k.poly(pts, "1a7a3e")
    inner = [(11, 4), (20, 4), (27, 11), (27, 20), (20, 27), (11, 27), (4, 20), (4, 11)]
    k.poly(inner, "328464")
    k.poly([(11, 4), (20, 4), (27, 11), (4, 11)], "5daf8d")
    k.ell((9, 9, 22, 22), "24523b")
    k.ell((10, 10, 21, 21), "92dcba")
    k.rect((13, 13, 18, 18), "24523b")
    k.rect((14, 14, 17, 17), "5daf8d")
    for x0, y0, x1, y1 in ((13, 5, 18, 5), (13, 26, 18, 26)):
        k.line([(x0, y0), (x1, y1)], "cdf7e2")
    for y in (6, 25):
        k.px(15, y, "cdf7e2")
        k.px(16, y, "cdf7e2")
    for x in (5, 26):
        k.line([(x, 13), (x, 18)], "cdf7e2")
    return k.outline()


def nachtalb():  # lua com olho fechado e Z
    k = Canvas()
    k.ell((3, 2, 27, 26), "285cc4")
    k.ell((12, 0, 36, 22), "000000")
    img = k.img
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.ell((4, 3, 20, 27), "143464")
    k.ell((6, 3, 29, 25), "000000")
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.ell((3, 2, 15, 27), "285cc4")
    k.ell((9, 0, 30, 26), "000000")
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.line([(5, 13), (6, 16), (8, 19), (10, 21)], "849be4", 1)
    # olho fechado
    k.line([(11, 19), (14, 22), (18, 23), (22, 22), (25, 19)], "e3e6ff", 2)
    for x, y in ((12, 24), (15, 26), (19, 26), (22, 25)):
        k.px(x, y, "b9bffb")
    # Z de sono
    for (x0, y0, w) in ((19, 6, 6), (24, 1, 4)):
        k.line([(x0, y0), (x0 + w, y0)], "e3e6ff")
        k.line([(x0 + w, y0), (x0, y0 + w)], "e3e6ff")
        k.line([(x0, y0 + w), (x0 + w, y0 + w)], "e3e6ff")
    return k.outline()


FAMILIES = {"dracul": dracul, "bathory": bathory, "orlok": orlok, "karnstein": karnstein,
            "strigoi": strigoi, "varcolac": varcolac, "liaozhai": liaozhai, "nachtalb": nachtalb}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview")
    args = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    imgs = {}
    for name, fn in FAMILIES.items():
        imgs[name] = fn()
        imgs[name].save(OUT / f"{name}.png")
    if args.preview:
        sheet = Image.new("RGBA", (8 * 144 + 16, 144 + 16), c("242234"))
        for i, name in enumerate(FAMILIES):
            sheet.alpha_composite(imgs[name].resize((128, 128), Image.NEAREST), (16 + i * 144, 8))
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
