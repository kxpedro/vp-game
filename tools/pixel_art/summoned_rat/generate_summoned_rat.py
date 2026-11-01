"""Rato invocado (Orlok): recorta de source/Rats.png (pacote de F:\\ASSETS\\Rats, 48x48 por quadro) o ciclo de 3 quadros do
rato marrom em perfil (linha 1 = encara a esquerda, linha 2 = encara a direita), reduz para 30x30, recolore o corpo numa
rampa VERDE-acinzentada e a cauda/patas/orelhas num rosa doente, tudo na AAP-64. Folha final: 6 quadros 30x30
(0-2 esquerda, 3-5 direita). Uso: py generate_summoned_rat.py"""
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


src = Image.open(SRC).convert("RGBA")
sheet = Image.new("RGBA", (OUTSZ * 6, OUTSZ), (0, 0, 0, 0))
for side, row in enumerate((1, 2)):
    for f in range(3):
        frame = src.crop((f * FRAME, row * FRAME, (f + 1) * FRAME, (row + 1) * FRAME))
        small = frame.resize((OUTSZ, OUTSZ), Image.LANCZOS)
        out = Image.new("RGBA", small.size)
        for y in range(OUTSZ):
            for x in range(OUTSZ):
                out.putpixel((x, y), recolor(small.getpixel((x, y))))
        sheet.paste(out, ((side * 3 + f) * OUTSZ, 0))
OUT.parent.mkdir(parents=True, exist_ok=True)
sheet.save(OUT)
bg = Image.new("RGBA", sheet.size, (36, 34, 52, 255)); bg.alpha_composite(sheet)
bg.resize((sheet.width * 5, sheet.height * 5), Image.NEAREST).save("/tmp/rat_new.png")
print("ok")
