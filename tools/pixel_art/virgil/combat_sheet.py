"""Monta assets/pixel_art/characters/virgil/virgil_combat.png a partir de out/combat_<dir>_<anim>_<n>.png.

Linhas = 8 direções (mesma ordem da folha de idle/corrida); colunas = golpes em sequência, na ordem de ANIMS."""
import os
from PIL import Image
from combat import ANIMS, W, H
DIRS = ["n", "ne", "e", "se", "s", "sw", "w", "nw"]
cols = sum(n for _, n in ANIMS)
sheet = Image.new("RGBA", (W * cols, H * len(DIRS)), (0, 0, 0, 0))
for r, d in enumerate(DIRS):
    c = 0
    for name, n in ANIMS:
        for i in range(n):
            im = Image.open(f"out/combat_{d}_{name}_{i}.png").convert("RGBA")
            sheet.paste(im, (c * W, r * H)); c += 1
dst = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../../assets/pixel_art/characters/virgil/virgil_combat.png")
sheet.save(dst); print(sheet.size, os.path.normpath(dst))
