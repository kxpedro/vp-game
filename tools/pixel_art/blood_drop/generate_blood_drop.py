"""Gera a Gota de Sangue (pickup de cura do Tributo Carmesim) em pixel art, paleta AAP-64.

Saída (pasta passada como argumento, padrão assets/pixel_art/effects/blood_drop):
- blood_drop_sheet.png: 1 linha x 6 colunas de 24x32. Gota em pé (ponta para cima) com o brilho (reflexo) que
  desliza e pulsa em loop; a flutuação vertical é feita pelo jogo (BloodOrbPickup.cs).
- blood_drop_shadow.png: sombra tracejada 20x10 (losango 2:1), deitada no chão pelo jogo.

    py tools/pixel_art/blood_drop/generate_blood_drop.py

Requer Pillow.
"""
import sys
from pathlib import Path

from PIL import Image

AAP64 = set("""060608 141013 3b1725 73172d b4202a df3e23 e86a73 f5a097""".split())


def C(h):
    assert h in AAP64, h
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


OUTLINE, DARK, BODY, LIGHT, SPEC, GLINT = C("3b1725"), C("73172d"), C("b4202a"), C("df3e23"), C("e86a73"), C("f5a097")
SHADOW = C("141013")

W, H, FRAMES = 24, 32, 6
CX = 12.0


def half_width(y):
    """Largura (raio) da gota a cada linha: ponta em y=3, barriga redonda centrada em y=21."""
    if y < 3 or y > 29:
        return -1.0
    if y < 17.4:  # ponta: cone tangente à barriga, sem degrau
        return 0.3 + (y - 3) * 0.5
    cy, r = 21.0, 8.0  # barriga: círculo
    d = r * r - (y - cy) ** 2
    return d ** 0.5 if d > 0 else -1.0


def drop_mask():
    m = set()
    for y in range(H):
        hw = half_width(y)
        if hw < 0:
            continue
        for x in range(W):
            if abs(x + 0.5 - CX) <= hw:
                m.add((x, y))
    return m


def make_frame(i):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    mask = drop_mask()
    for (x, y) in mask:
        edge = any((x + dx, y + dy) not in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if edge:
            px[x, y] = OUTLINE
            continue
        # Luz vinda de cima à esquerda: borda direita/baixo mais escura, centro-esquerda mais clara.
        u, v = (x + 0.5 - CX) / 8.0, (y - 21.0) / 8.0
        shade = u * 0.7 + v * 0.7
        if shade > 0.55:
            px[x, y] = DARK
        elif shade > -0.05:
            px[x, y] = BODY
        else:
            px[x, y] = LIGHT
    # Reflexo: arco curto na barriga que desliza e pulsa (6 quadros em loop).
    spec = [(7, 17), (7, 18), (8, 19), (8, 16)]
    for (x, y) in spec:
        px[x, y] = SPEC
    if i in (1, 2):
        px[7, 17] = GLINT
        px[7, 18] = GLINT
    if i == 2:
        px[8, 16] = GLINT
    if i in (3, 4):  # pisca apagado: o brilho recua
        px[8, 16] = LIGHT
        px[7, 17] = SPEC
        px[7, 18] = LIGHT
    # Cintilo de estrela na ponta nos quadros 1-2.
    if i in (1, 2):
        for dx, dy in ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)):
            x, y = 12 + dx, 2 + dy
            if 0 <= x < W and 0 <= y < H and (x, y) not in mask:
                px[x, y] = GLINT if (dx, dy) == (0, 0) else SPEC
    return img


def make_shadow():
    w, h = 20, 10
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            nx, ny = (x + 0.5 - w / 2) / (w / 2), (y + 0.5 - h / 2) / (h / 2)
            if nx * nx + ny * ny <= 1.0 and (x + y) % 2 == 0:
                px[x, y] = SHADOW
    return img


def main():
    out = Path(sys.argv[1] if len(sys.argv) > 1 else "assets/pixel_art/effects/blood_drop")
    out.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGBA", (W * FRAMES, H), (0, 0, 0, 0))
    for i in range(FRAMES):
        sheet.paste(make_frame(i), (i * W, 0))
    sheet.save(out / "blood_drop_sheet.png")
    make_shadow().save(out / "blood_drop_shadow.png")


if __name__ == "__main__":
    main()
