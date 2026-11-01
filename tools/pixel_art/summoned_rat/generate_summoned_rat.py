"""Rato invocado (Orlok): 4 quadros de corrida de 20x12, virado para a direita, paleta AAP-64 (pelo pardo, olho vermelho,
cauda rosada, manchas verdes de pestilência). Uso: py generate_summoned_rat.py"""
from pathlib import Path
from PIL import Image

OUT = Path(__file__).resolve().parents[3] / "assets/pixel_art/effects/summoned_rat/summoned_rat_sheet.png"
C = {k: tuple(int(v[i:i + 2], 16) for i in (0, 2, 4)) + (255,) for k, v in dict(
    o="141013", d="422433", b="5a4e44", m="796755", l="a08662", p="e9b5a3", e="df3e23", g="59c135", w="9cdb43").items()}
W, H, N = 20, 12, 4
# Corpo (x 6..16), cabeça à direita, cauda à esquerda; patas mudam por quadro.
BODY = [
    "......................",
    "........oooo..........",
    ".......obbmmoo...oo...",
    "..p...obbmmmmloo.ow...",
    "..pp.obbgmmmmmlloeoo..",
    "...ppobbbmmmmmmllpo...",
    "....pobbbbmmmmllo.....",
    ".....oobbbbbbbo.......",
]
LEGS = [
    [(7, 8), (8, 9), (13, 8), (14, 9)],
    [(7, 9), (9, 9), (12, 9), (14, 8)],
    [(8, 9), (7, 8), (14, 9), (13, 8)],
    [(7, 9), (8, 8), (13, 9), (14, 8)],
]
sheet = Image.new("RGBA", (W * N, H), (0, 0, 0, 0))
for f in range(N):
    dy = 1 if f % 2 else 0
    for y, row in enumerate(BODY):
        for x, ch in enumerate(row[:W]):
            if ch != "." and ch in C:
                sheet.putpixel((f * W + x, y + 1 + dy), C[ch])
    for x, y in LEGS[f]:
        sheet.putpixel((f * W + x, y + 2), C["o"])
Path(OUT).parent.mkdir(parents=True, exist_ok=True)
sheet.save(OUT)
big = sheet.resize((W * N * 8, H * 8), Image.NEAREST)
bg = Image.new("RGBA", big.size, (36, 34, 52, 255)); bg.alpha_composite(big); bg.save("/tmp/rat_preview.png")
print("ok", OUT)
