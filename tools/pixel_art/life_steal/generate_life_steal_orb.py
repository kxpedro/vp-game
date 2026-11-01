"""Orbe do roubo de vida (Dracul): 4 quadros de 16x16, uma gota-orbe roxa pulsando, AAP-64. Uso: py generate_life_steal_orb.py"""
import math
from pathlib import Path
from PIL import Image

OUT = Path(__file__).resolve().parents[3] / "assets/pixel_art/effects/life_steal/life_steal_orb_sheet.png"
H = lambda s: tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)
RAMP = [H(x) for x in ("403353", "793a80", "849be4", "b9bffb")]
SIZE, FRAMES = 16, 4
sheet = Image.new("RGBA", (SIZE * FRAMES, SIZE), (0, 0, 0, 0))
for f in range(FRAMES):
    r = 4.6 + 0.9 * math.sin(math.tau * f / FRAMES)
    cx = cy = SIZE / 2 - 0.5
    for y in range(SIZE):
        for x in range(SIZE):
            d = math.hypot(x - cx, y - cy)
            if d > r + 1.2:
                continue
            if d > r:
                c = RAMP[0]
            elif d > r - 1.4:
                c = RAMP[1]
            elif d > r - 3.0:
                c = RAMP[2]
            else:
                c = RAMP[3]
            sheet.putpixel((f * SIZE + x, y), c)
    sheet.putpixel((f * SIZE + 5, 5), H("ffffff"))  # brilho
OUT.parent.mkdir(parents=True, exist_ok=True)
sheet.save(OUT)
print("ok")
