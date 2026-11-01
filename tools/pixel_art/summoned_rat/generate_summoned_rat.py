"""Rato invocado (Orlok): recorta de source/Rats.png (pacote de F:\\ASSETS\\Rats, 48x48 por quadro) o ciclo de 3 quadros do
rato marrom em perfil (linha 1 = encara a esquerda, linha 2 = encara a direita), reduz para 30x30, recolore o corpo numa
rampa VERDE-acinzentada e a cauda/patas/orelhas num rosa doente, tudo na AAP-64. Folha final 10x3 quadros de 30x30:
linha 0 = corrida (0-2 esquerda, 3-5 direita); linha 1 = ataque (0-3 esquerda, 4-7 direita); linha 2 = morte (0-4 esquerda, 5-9 direita).
Ataque e morte são derivados dos quadros de corrida por transformações (bote, inclinação, queda, dissolução em dither). Uso: py generate_summoned_rat.py"""
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
SRC = HERE / "source" / "Rats.png"
OUT = HERE.parents[2] / "assets/pixel_art/effects/summoned_rat/summoned_rat_sheet.png"
H = lambda s: tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))
BODY = [H(x) for x in ("141013", "24523b", "23674e", "328464", "5daf8d", "92dcba")]
SKIN = [H(x) for x in ("5b3138", "8e5252", "ba756a", "e9b5a3")]
FRAME, OUTSZ = 48, 30


def recolor(px):
    r, g, b, a = px
    if a < 128:
        return (0, 0, 0, 0)
    lum = (0.3 * r + 0.59 * g + 0.11 * b) / 255
    if r - g > 70:  # tons rosados do original: cauda, patas, orelhas, nariz
        return SKIN[max(0, min(3, int((lum - 0.3) / 0.35 * 4)))] + (255,)
    return BODY[max(0, min(5, int((lum - 0.08) / 0.70 * 6)))] + (255,)



def run_frame(side, f):
    frame = src.crop((f * FRAME, (1, 2)[side] * FRAME, (f + 1) * FRAME, ((1, 2)[side] + 1) * FRAME))
    small = frame.resize((OUTSZ, OUTSZ), Image.LANCZOS)
    out = Image.new("RGBA", small.size)
    for y in range(OUTSZ):
        for x in range(OUTSZ):
            out.putpixel((x, y), recolor(small.getpixel((x, y))))
    return out


def bbox_x(img):
    box = img.getbbox()
    return box[0], box[2] - 1


def shifted(img, dx, dy):
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(img, (dx, dy), img)
    return out


def squash(img, sx, sy):
    """Escala em x/y ancorada no pé do rato (base do quadro), vizinho mais próximo."""
    w, h = img.size
    nw, nh = max(1, round(w * sx)), max(1, round(h * sy))
    scaled = img.resize((nw, nh), Image.NEAREST)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(scaled, ((w - nw) // 2, h - 2 - nh + 2), scaled)
    return out


def tilt(img, deg):
    return img.rotate(deg, resample=Image.NEAREST, center=(OUTSZ // 2, OUTSZ - 3))


def darken(img, steps=1):
    ramp = BODY + SKIN
    out = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = img.getpixel((x, y))
            if not a:
                continue
            c = (r, g, b)
            if c in BODY:
                c = BODY[max(0, BODY.index(c) - steps)]
            elif c in SKIN:
                c = SKIN[max(0, SKIN.index(c) - steps)]
            out.putpixel((x, y), c + (255,))
    return out


def dissolve(img, keep):
    out = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            if (x + y) % 2 == keep:
                out.putpixel((x, y), (0, 0, 0, 0))
    return out


def attack_frames(base, d):
    """d = +1 encara a direita, -1 esquerda."""
    tilt_sign = 1 if d > 0 else -1  # PIL gira anti-horário com ângulo positivo: nariz para cima a direita
    f0 = shifted(squash(base, 1.0, 0.86), -2 * d, 1)
    f1 = shifted(tilt(squash(base, 1.1, 1.0), 12 * tilt_sign), 3 * d, -1)
    f2 = shifted(tilt(squash(base, 1.12, 1.0), 22 * tilt_sign), 5 * d, -3)
    # Mordida: faíscas claras na frente do focinho.
    x0, x1 = bbox_x(f2)
    nose = x1 if d > 0 else x0
    for dx, dy in ((1, 7), (2, 5), (2, 9)):
        px, py = nose + dx * d, 8 + dy
        if 0 <= px < OUTSZ and 0 <= py < OUTSZ:
            f2.putpixel((px, py), BODY[5] + (255,))
    f3 = shifted(tilt(base, 6 * tilt_sign), 2 * d, 0)
    return [f0, f1, f2, f3]


def death_frames(base, d):
    flash = Image.new("RGBA", base.size)
    for y in range(base.height):
        for x in range(base.width):
            r, g, b, a = base.getpixel((x, y))
            if a:
                flash.putpixel((x, y), (BODY[5] if (r, g, b) in BODY else SKIN[3]) + (255,))
    f0 = shifted(flash, -2 * d, -1)
    f1 = shifted(tilt(base, -35 * (1 if d > 0 else -1)), -3 * d, -2)
    flipped = base.transpose(Image.FLIP_TOP_BOTTOM)
    f2 = shifted(darken(flipped, 1), -3 * d, -1)
    f3 = shifted(squash(darken(flipped, 1), 1.0, 0.8), -3 * d, 2)
    f4 = dissolve(shifted(squash(darken(flipped, 2), 1.0, 0.6), -3 * d, 4), 0)
    return [f0, f1, f2, f3, f4]


src = Image.open(SRC).convert("RGBA")
COLS = 10
sheet = Image.new("RGBA", (OUTSZ * COLS, OUTSZ * 3), (0, 0, 0, 0))
for side in (0, 1):
    d = 1 if side else -1
    run = [run_frame(side, f) for f in range(3)]
    for f, img in enumerate(run):
        sheet.paste(img, ((side * 3 + f) * OUTSZ, 0))
    for f, img in enumerate(attack_frames(run[1], d)):
        sheet.paste(img, ((side * 4 + f) * OUTSZ, OUTSZ))
    for f, img in enumerate(death_frames(run[1], d)):
        sheet.paste(img, ((side * 5 + f) * OUTSZ, OUTSZ * 2))
OUT.parent.mkdir(parents=True, exist_ok=True)
sheet.save(OUT)
bg = Image.new("RGBA", sheet.size, (36, 34, 52, 255)); bg.alpha_composite(sheet)
bg.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save("/tmp/rat_new.png")
print("ok")
