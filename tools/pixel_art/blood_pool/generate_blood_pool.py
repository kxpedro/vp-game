"""Gera a poça de sangue dos inimigos (modo 2D e 3D) em pixel art, paleta AAP-64.

A poça é desenhada já na projeção da tela (2:1): o jogo a deita no chão girada para a câmera e esticada em
profundidade (BloodPool.cs), então cada pixel daqui vira um pixel da arte na tela.

Saída (pasta passada como argumento, padrão assets/pixel_art/effects/blood_pool):
- blood_pool_sheet.png: 3 linhas (variantes) x 12 colunas de 64x32. Colunas 0-7 = a poça se espalhando a partir
  do corpo; 8-11 = brilho em loop da poça pronta. Para secar/sugar, o jogo toca 7..0 de trás para frente.

    py tools/pixel_art/blood_pool/generate_blood_pool.py

Requer numpy e Pillow.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

AAP64 = set("""060608 141013 3b1725 73172d b4202a df3e23 e86a73 f5a097 5b3138""".split())


def C(h):
    assert h in AAP64, h
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


RIM = C("3b1725")
BODY = C("73172d")
SHINE = C("b4202a")
SPEC = C("e86a73")
TWINKLE = C("f5a097")

CELL_W, CELL_H = 64, 32
SPREAD_FRAMES = 8
SHIMMER_FRAMES = 4
VARIANTS = 3


def ease_out(t):
    t = min(max(t, 0.0), 1.0)
    return 1.0 - (1.0 - t) ** 2


def make_variant(seed):
    rng = np.random.default_rng(seed)
    # Lóbulos em unidades do chão (y da tela vale o dobro): o principal sai do corpo, os outros escorrem depois.
    lobes = [(0.0, 0.0, 9.5, 0.0), (rng.uniform(-4, 4), rng.uniform(-3, 3), 7.5, 0.05)]
    count = int(rng.integers(6, 9))
    base = rng.uniform(0, 2 * np.pi)
    for i in range(count):
        ang = base + i * 2 * np.pi / count + rng.uniform(-0.4, 0.4)
        dist = rng.uniform(8.0, 13.0)
        r = rng.uniform(3.6, 6.2)
        delay = rng.uniform(0.1, 0.45)
        lobes.append((np.cos(ang) * dist, np.sin(ang) * dist, r, delay))
        # Alguns lóbulos escorrem mais longe, como um filete.
        if rng.random() < 0.45:
            d2 = dist + rng.uniform(4.0, 6.5)
            lobes.append((np.cos(ang) * d2, np.sin(ang) * d2, r * 0.55, min(delay + 0.2, 0.8)))
    # Respingos do impacto: pontos soltos que surgem logo e ficam.
    splats = []
    for _ in range(rng.integers(3, 6)):
        ang = rng.uniform(0, 2 * np.pi)
        dist = rng.uniform(17.0, 22.0)
        splats.append((np.cos(ang) * dist, np.sin(ang) * dist * 0.5, int(rng.integers(1, 3)), int(rng.integers(0, 2))))
    return lobes, splats


def field(lobes, t, xs, ys):
    f = np.zeros_like(xs, dtype=float)
    for lx, ly, r, delay in lobes:
        g = ease_out((t - delay) / (1.0 - delay)) if t > delay else 0.0
        if g <= 0:
            continue
        rr = r * g
        d2 = (xs - lx) ** 2 + ((ys * 2.0) - ly) ** 2 + 0.6
        f += (rr * rr / d2) ** 2
    # União suave dos lóbulos (norma 2): a borda fica recortada, sem virar uma bolha lisa.
    return f ** 0.5


def render(lobes, splats, t, shimmer=None):
    cx, cy = CELL_W / 2 - 0.5, CELL_H / 2 - 0.5
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W].astype(float)
    xs -= cx
    ys -= cy
    f = field(lobes, t, xs, ys)
    inside = f >= 1.0
    img = np.zeros((CELL_H, CELL_W, 4), np.uint8)

    def ins(dx, dy):
        out = np.zeros_like(inside)
        h, w = inside.shape
        src = inside[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)]
        out[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)] = src
        return out

    edge = inside & ~(ins(1, 0) & ins(-1, 0) & ins(0, 1) & ins(0, -1))
    img[inside] = BODY
    # Brilho de líquido junto da borda de cima/esquerda (luz vinda de cima).
    shine = inside & ~edge & ~ins(-2, -1)
    img[shine] = SHINE
    img[edge] = RIM

    # Reflexo especular perto do centro, só quando a poça já tem corpo.
    if t >= 0.5:
        sx, sy = int(round(cx - 4)), int(round(cy - 2))
        if shimmer is not None:
            sx += (0, 1, 1, 0)[shimmer]
        for dx, dy in ((0, 0), (1, 0), (2, 0), (-1, 1)):
            x, y = sx + dx, sy + dy
            if 0 <= x < CELL_W and 0 <= y < CELL_H and inside[y, x] and not edge[y, x]:
                img[y, x] = SPEC
        if shimmer == 2:
            x, y = sx + 1, sy
            if inside[y, x]:
                img[y, x] = TWINKLE
        # Segundo reflexo menor, que pisca no loop.
        if shimmer in (1, 2):
            x, y = int(round(cx + 6)), int(round(cy + 2))
            if inside[y, x] and not edge[y, x]:
                img[y, x] = SPEC

    if t >= 0.12:
        for sx, sy, size, _ in splats:
            x0, y0 = int(round(cx + sx)), int(round(cy + sy))
            for dx in range(size):
                x, y = x0 + dx, y0
                if 0 <= x < CELL_W and 0 <= y < CELL_H and not inside[y, x]:
                    img[y, x] = RIM if size == 1 else BODY
            if size > 1 and 0 <= y0 < CELL_H and 0 <= x0 < CELL_W:
                img[y0, x0] = RIM
    return img


def main():
    root = Path(__file__).resolve().parents[3]
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "assets/pixel_art/effects/blood_pool"
    out.mkdir(parents=True, exist_ok=True)
    cols = SPREAD_FRAMES + SHIMMER_FRAMES
    sheet = np.zeros((CELL_H * VARIANTS, CELL_W * cols, 4), np.uint8)
    for v in range(VARIANTS):
        lobes, splats = make_variant(1701 + v * 37)
        for k in range(SPREAD_FRAMES):
            t = (k + 1) / SPREAD_FRAMES
            sheet[v * CELL_H:(v + 1) * CELL_H, k * CELL_W:(k + 1) * CELL_W] = render(lobes, splats, t)
        for s in range(SHIMMER_FRAMES):
            c = SPREAD_FRAMES + s
            sheet[v * CELL_H:(v + 1) * CELL_H, c * CELL_W:(c + 1) * CELL_W] = render(lobes, splats, 1.0, s)
    Image.fromarray(sheet, "RGBA").save(out / "blood_pool_sheet.png")
    print("ok:", out)


if __name__ == "__main__":
    main()
