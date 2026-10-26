"""Monta assets/pixel_art/characters/virgil/virgil_death.png a partir de out/death_<dir>_<n>.png.

Linhas = 8 direções (mesma ordem das outras folhas); colunas = os FRAMES quadros da morte. Célula 160x128.
    py death_sheet.py [destino.png]"""
import os, sys
from PIL import Image
W, H, FRAMES = 160, 128, 12
DIRS = ["n", "ne", "e", "se", "s", "sw", "w", "nw"]
sheet = Image.new("RGBA", (W * FRAMES, H * len(DIRS)), (0, 0, 0, 0))
for r, d in enumerate(DIRS):
    for i in range(FRAMES):
        sheet.paste(Image.open(f"out/death_{d}_{i}.png").convert("RGBA"), (i * W, r * H))
dst = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../../assets/pixel_art/characters/virgil/virgil_death.png")
sheet.save(dst); print(sheet.size, os.path.normpath(dst))
