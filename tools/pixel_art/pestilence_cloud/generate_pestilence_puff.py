"""Tufo de névoa em pé (billboard) da nuvem de pestilência: 6 quadros de 64x48 em loop, rampa verde-acinzentada da AAP-64,
borda esfumaçada em dither, sem contorno. Uso: py generate_pestilence_puff.py"""
import math, random
from pathlib import Path
from PIL import Image

DIR = Path(__file__).resolve().parents[3] / "assets/pixel_art/effects/pestilence_cloud"
RAMPS = {  # nome do arquivo -> rampa AAP-64
    "pestilence_puff_sheet.png": ("24523b", "1a7a3e", "23674e", "328464", "5daf8d"),
    "charm_puff_sheet.png": ("5b3138", "793a80", "bc4a9b", "e86a73", "f5a097"),  # explosão do encanto (Karnstein)
}
RAMP = []
W, H, FRAMES = 64, 48, 6
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
rnd = random.Random(11)
PUFFS = [(rnd.uniform(-14, 14), rnd.uniform(-9, 9), rnd.uniform(9, 15), rnd.uniform(0, math.tau), rnd.choice((1, 2))) for _ in range(7)]


def frame(k):
    t = math.tau * k / FRAMES
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            dens = 0.0
            for px, py, r, ph, sp in PUFFS:
                ox = px + 2.5 * math.sin(sp * t + ph)
                oy = py + 2 * math.cos(sp * t + ph)
                d2 = ((x - W / 2 - ox) / r) ** 2 + ((y - H / 2 - oy) / r) ** 2
                dens += max(0.0, 1 - d2) ** 1.5
            dens = min(dens, 1.4) / 1.4
            dens *= max(0.0, 1 - (((x - W / 2) / (W / 2 - 1)) ** 2 + ((y - H / 2) / (H / 2 - 1)) ** 2)) ** 0.5
            lvl = dens * 4.2 + (BAYER[y % 4][x % 4] / 16 - 0.5) * 1.4
            if lvl < 1.1:
                continue
            img.putpixel((x, y), RAMP[min(4, int(lvl))] + (255,))
    return img


for name, hexes in RAMPS.items():
    RAMP[:] = [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in hexes]
    sheet = Image.new("RGBA", (W * FRAMES, H), (0, 0, 0, 0))
    for k in range(FRAMES):
        sheet.paste(frame(k), (k * W, 0))
    DIR.mkdir(parents=True, exist_ok=True)
    sheet.save(DIR / name)
    print("ok", name)
