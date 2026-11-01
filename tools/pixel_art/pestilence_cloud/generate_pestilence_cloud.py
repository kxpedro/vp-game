"""Nuvem de pestilência do Orlok (Pulmão Negro): folha de 6 quadros de 192x96 (projeção 2:1), loop sem emenda.
Tufos que derivam, borda esfumaçada em dither (sem contorno), paleta AAP-64 (rampa verde). Uso: py generate_pestilence_cloud.py"""
import math, random
from pathlib import Path
from PIL import Image

OUT = Path(__file__).resolve().parents[3] / "assets/pixel_art/effects/pestilence_cloud/pestilence_cloud_sheet.png"
RAMP = [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in ("24523b", "1a7a3e", "14a02e", "59c135", "9cdb43")]
W, H, FRAMES = 192, 96, 6
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
rnd = random.Random(7)
PUFFS = [(rnd.uniform(-58, 58), rnd.uniform(-20, 20), rnd.uniform(18, 32), rnd.uniform(0, math.tau), rnd.choice((1, 2))) for _ in range(15)]


def frame(k):
    t = math.tau * k / FRAMES
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            dens = 0.0
            for px, py, r, ph, sp in PUFFS:
                ox = px + 5 * math.sin(sp * t + ph)
                oy = py + 2.5 * math.cos(sp * t + ph)
                d2 = ((x - W / 2 - ox) / r) ** 2 + ((y - H / 2 - oy) * 2.0 / r) ** 2
                dens += max(0.0, 1 - d2) ** 1.5
            dens = min(dens, 1.6) / 1.6
            dens *= max(0.0, 1 - (((x - W / 2) / (W / 2 - 2)) ** 2 + ((y - H / 2) / (H / 2 - 2)) ** 2)) ** 0.5
            lvl = dens * 4.2 + (BAYER[y % 4][x % 4] / 16 - 0.5) * 1.4
            if lvl < 0.9:
                continue
            img.putpixel((x, y), RAMP[min(4, int(lvl))] + (255,))
    return img


sheet = Image.new("RGBA", (W * FRAMES, H), (0, 0, 0, 0))
for k in range(FRAMES):
    sheet.paste(frame(k), (k * W, 0))
OUT.parent.mkdir(parents=True, exist_ok=True)
sheet.save(OUT)
print("ok", OUT)
