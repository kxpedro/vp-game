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


def dracul():  # cálice coroado de espinhos, vinho e violeta escuro
    k = Canvas()
    for pts in ([(8, 8), (10, 1), (14, 9)], [(12, 9), (16, 0), (20, 9)], [(18, 9), (22, 1), (24, 8)]):
        k.poly(pts, "793a80")
    k.poly([(9, 7), (10, 3), (12, 8)], "849be4")
    k.poly([(15, 7), (16, 2), (17, 8)], "849be4")
    k.poly([(5, 9), (26, 9), (22, 21), (9, 21)], "242234")
    k.ell((5, 7, 26, 13), "793a80")
    k.ell((7, 8, 24, 12), "141013")
    k.ell((9, 9, 22, 11), "403353")
    k.poly([(9, 13), (22, 13), (20, 20), (11, 20)], "403353")
    k.poly([(18, 13), (22, 13), (20, 20), (17, 20)], "242234")
    k.rect((14, 21, 17, 25), "793a80")
    k.rect((14, 21, 14, 25), "849be4")
    k.poly([(9, 26), (22, 26), (25, 30), (6, 30)], "242234")
    k.rect((9, 26, 22, 26), "793a80")
    k.line([(8, 12), (10, 17)], "849be4")
    k.px(11, 10, "bc4a9b")
    k.px(14, 16, "bc4a9b")
    k.px(14, 17, "bc4a9b")
    return k.outline()


def bathory():  # gota pesada de sangue coagulado sobre poça escura
    k = Canvas()
    k.ell((2, 25, 29, 31), "3b1725")
    k.ell((5, 26, 26, 30), "73172d")
    k.ell((11, 27, 20, 29), "b4202a")
    k.poly([(16, 1), (24, 14), (25, 19)], "73172d")
    k.poly([(16, 1), (8, 14), (7, 19)], "73172d")
    k.ell((7, 11, 25, 26), "73172d")
    k.ell((13, 14, 25, 26), "3b1725")
    k.ell((8, 13, 19, 24), "b4202a")
    k.poly([(16, 5), (10, 14), (14, 14)], "b4202a")
    k.line([(10, 16), (10, 20)], "e86a73")
    k.px(12, 14, "e86a73")
    k.px(15, 8, "e86a73")
    k.line([(21, 22), (22, 20)], "141013")
    return k.outline()


def orlok():  # caveira de rato com olhos de podridão
    k = Canvas()
    k.ell((3, 3, 12, 12), "14a02e")
    k.ell((19, 3, 28, 12), "14a02e")
    k.ell((5, 5, 10, 10), "141013")
    k.ell((21, 5, 26, 10), "141013")
    k.ell((6, 6, 25, 21), "9cdb43")
    k.ell((15, 8, 25, 21), "59c135")
    k.poly([(9, 18), (22, 18), (20, 26), (11, 26)], "59c135")
    k.poly([(9, 18), (15, 18), (14, 26), (11, 26)], "9cdb43")
    k.ell((8, 11, 14, 17), "141013")
    k.ell((17, 11, 23, 17), "141013")
    k.rect((10, 13, 12, 15), "fffc40")
    k.rect((19, 13, 21, 15), "fffc40")
    k.px(11, 14, "d6f264")
    k.px(20, 14, "d6f264")
    k.poly([(15, 18), (16, 18), (16, 20)], "141013")
    for x in (11, 13, 15, 17, 19):
        k.line([(x, 23), (x, 26)], "141013")
    k.line([(1, 21), (7, 23)], "59c135")
    k.line([(30, 21), (24, 23)], "59c135")
    k.px(4, 28, "1a7a3e")
    k.px(27, 28, "1a7a3e")
    k.px(16, 29, "1a7a3e")
    return k.outline()


def karnstein():  # coração escuro perfurado por presas
    k = Canvas()
    k.ell((3, 4, 17, 18), "bc4a9b")
    k.ell((14, 4, 28, 18), "bc4a9b")
    k.poly([(3, 13), (28, 13), (15, 29)], "bc4a9b")
    k.poly([(18, 10), (28, 12), (16, 29), (15, 29)], "793a80")
    k.ell((18, 6, 27, 14), "793a80")
    k.ell((5, 6, 14, 14), "e86a73")
    k.ell((6, 6, 11, 10), "f5a097")
    k.poly([(7, 14), (14, 14), (15, 26)], "e86a73")
    k.px(7, 8, "fad6b8")
    k.poly([(8, 8), (11, 8), (10, 17), (9, 17)], "141013")
    k.poly([(19, 8), (22, 8), (21, 17), (20, 17)], "141013")
    k.poly([(8, 3), (12, 3), (10, 13)], "dae0ea")
    k.poly([(19, 3), (23, 3), (21, 13)], "dae0ea")
    k.line([(10, 14), (10, 20)], "b4202a")
    k.line([(21, 14), (21, 22)], "b4202a")
    k.px(10, 22, "b4202a")
    return k.outline()


def strigoi():  # capuz vazio com um olho pálido na névoa
    k = Canvas()
    k.poly([(16, 1), (25, 6), (28, 18), (31, 30), (1, 30), (4, 18), (7, 6)], "333941")
    k.poly([(16, 1), (7, 6), (4, 18), (1, 30), (8, 30), (10, 14), (11, 6)], "4a5462")
    k.ell((8, 7, 24, 25), "141013")
    k.ell((10, 9, 22, 23), "060608")
    k.poly([(10, 16), (13, 12), (19, 12), (22, 16), (19, 20), (13, 20)], "dae0ea")
    k.ell((13, 13, 19, 19), "ffd541")
    k.rect((15, 13, 16, 19), "141013")
    k.px(13, 14, "fef3c0")
    k.line([(10, 16), (13, 12)], "6d758d")
    for x, y, w in ((0, 27, 14), (10, 29, 12), (19, 27, 12)):
        k.ell((x, y, x + w, y + 2), "6d758d")
    return k.outline()


def varcolac():  # crânio de lobo com garras
    k = Canvas()
    k.poly([(5, 2), (11, 8), (7, 12)], "71413b")
    k.poly([(26, 2), (20, 8), (24, 12)], "71413b")
    k.poly([(6, 4), (9, 8), (7, 10)], "141013")
    k.poly([(25, 4), (22, 8), (24, 10)], "141013")
    k.ell((6, 7, 25, 20), "dba463")
    k.ell((15, 9, 25, 20), "bb7547")
    k.poly([(10, 17), (21, 17), (19, 29), (12, 29)], "dba463")
    k.poly([(16, 17), (21, 17), (19, 29), (16, 29)], "bb7547")
    k.ell((8, 11, 14, 17), "141013")
    k.ell((17, 11, 23, 17), "141013")
    k.rect((10, 13, 11, 14), "f9a31b")
    k.rect((19, 13, 20, 14), "f9a31b")
    k.poly([(14, 19), (17, 19), (16, 22)], "141013")
    for x in (12, 14, 16, 18):
        k.poly([(x, 25), (x + 1, 25), (x, 28)], "f4d29c")
    for x0 in (2, 5, 8):
        k.line([(x0, 18), (x0 + 3, 31)], "71413b", 2)
    for x0 in (22, 25, 28):
        k.line([(x0 + 3, 18), (x0, 31)], "71413b", 2)
    return k.outline()


def liaozhai():  # talismã (fulu) selado: papel velho, glifo vermelho, fitas de jade
    k = Canvas()
    k.rect((8, 1, 23, 30), "477d85")
    k.rect((8, 1, 11, 30), "5daf8d")
    k.rect((20, 1, 23, 30), "23674e")
    k.rect((8, 1, 23, 3), "24523b")
    k.rect((8, 28, 23, 30), "24523b")
    k.line([(14, 5), (14, 12)], "cdf7e2", 2)
    k.line([(11, 8), (20, 8)], "cdf7e2", 2)
    k.line([(12, 13), (19, 13)], "cdf7e2", 2)
    k.rect((14, 14, 17, 19), "cdf7e2")
    k.rect((15, 15, 16, 18), "477d85")
    k.line([(11, 21), (20, 21)], "cdf7e2")
    k.line([(12, 24), (14, 26)], "cdf7e2")
    k.line([(19, 24), (17, 26)], "cdf7e2")
    k.ell((13, 21, 18, 26), "060608")
    k.ell((14, 22, 17, 25), "92dcba")
    k.ell((15, 23, 16, 24), "141013")
    k.line([(9, 6), (22, 6)], "23674e")
    for y in (9, 17):
        k.px(9, y, "23674e")
    k.rect((1, 14, 7, 16), "23674e")
    k.rect((24, 14, 30, 16), "23674e")
    k.px(2, 14, "92dcba")
    k.px(25, 14, "92dcba")
    return k.outline()


def nachtalb():  # lua crescente com olho costurado fechado
    k = Canvas()
    k.ell((2, 2, 28, 28), "143464")
    k.ell((11, -1, 37, 24), "000000")
    img = k.img
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.ell((3, 4, 19, 27), "143464")
    k.ell((9, 1, 32, 24), "000000")
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.ell((2, 2, 17, 28), "143464")
    k.ell((9, -1, 34, 24), "000000")
    for y in range(S):
        for x in range(S):
            if img.getpixel((x, y)) == (0, 0, 0, 255):
                img.putpixel((x, y), (0, 0, 0, 0))
    k.line([(4, 10), (5, 17), (8, 22)], "285cc4")
    k.poly([(9, 18), (13, 15), (19, 15), (23, 18), (19, 21), (13, 21)], "242234")
    k.line([(9, 18), (13, 21), (19, 21), (23, 18)], "849be4", 1)
    for x in (12, 15, 18, 21):
        k.line([(x, 17), (x - 1, 22)], "b3b9d1")
    for (x0, y0, w) in ((18, 5, 5), (24, 1, 4)):
        k.line([(x0, y0), (x0 + w, y0)], "6d758d")
        k.line([(x0 + w, y0), (x0, y0 + w)], "6d758d")
        k.line([(x0, y0 + w), (x0 + w, y0 + w)], "6d758d")
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
        sheet = Image.new("RGBA", (8 * 144 + 16, 144 + 16), c("060608"))
        for i, name in enumerate(FAMILIES):
            sheet.alpha_composite(imgs[name].resize((128, 128), Image.NEAREST), (16 + i * 144, 8))
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
