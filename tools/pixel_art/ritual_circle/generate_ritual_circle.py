"""Gera o círculo de ritual que marca o spawn dos monstros (modo 2D) em pixel art, paleta AAP-64.

Igual à poça de sangue, o círculo é desenhado já na projeção da tela (2:1): o jogo o deita no chão girado para a
câmera e esticado em profundidade (RitualCircle.cs), então cada pixel daqui vira um pixel da arte na tela.

Saída (pasta passada como argumento, padrão assets/pixel_art/effects/ritual_circle):
- ritual_circle_sheet.png: 1 linha x 24 colunas de 72x40.
  - 0-9   = acender: o anel é traçado em volta, depois as cinco pontas do pentagrama, com uma brasa na frente.
  - 10-15 = pulsar em loop enquanto o monstro não chega (brilho sobe e desce, as runas piscam).
  - 16-23 = estouro quando o monstro surge: clarão, depois as linhas apagam e se desfazem em pontilhado.

    py tools/pixel_art/ritual_circle/generate_ritual_circle.py [pasta] [--gif prévia.gif]

Requer numpy e Pillow.
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

AAP64 = set("""060608 141013 3b1725 73172d b4202a df3e23 e86a73 f5a097 5b3138""".split())


def C(h):
    assert h in AAP64, h
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


# Rampa de brilho das linhas, do apagado (0) ao incandescente (5).
RAMP = [C("3b1725"), C("73172d"), C("b4202a"), C("df3e23"), C("e86a73"), C("f5a097")]
SOOT = C("141013")

CELL_W, CELL_H = 72, 40
IGNITE_FRAMES = 10
PULSE_FRAMES = 6
BURST_FRAMES = 8
CX, CY = CELL_W / 2 - 0.5, CELL_H / 2 - 0.5
# Raios em pixels da tela no eixo x (no y a elipse tem a metade).
R_OUTER = 34.0
R_INNER = 27.5
R_RUNES = (R_OUTER + R_INNER) / 2

# Runas 3x2 (1 = pixel aceso), postas entre os dois anéis.
GLYPHS = [
    ((1, 0, 1), (0, 1, 0)),
    ((1, 1, 0), (0, 1, 1)),
    ((0, 1, 0), (1, 0, 1)),
    ((1, 0, 0), (1, 1, 1)),
    ((1, 1, 1), (0, 0, 1)),
]
RUNE_COUNT = 10


def ellipse_point(r, ang):
    # ang = 0 aponta para cima na tela ("norte" do chão), sentido horário.
    return CX + r * math.sin(ang), CY - r * 0.5 * math.cos(ang)


def ellipse_pixels(r, frac=1.0, start=0.0):
    """Pixels do contorno, em ordem, até a fração frac da volta (traçado do anel)."""
    pts, seen = [], set()
    steps = int(r * 16)
    for i in range(int(steps * frac) + 1):
        ang = start + 2 * math.pi * i / steps
        x, y = ellipse_point(r, ang)
        p = (int(round(x)), int(round(y)))
        if p not in seen:
            seen.add(p)
            pts.append(p)
    return pts


def line_pixels(a, b):
    (x0, y0), (x1, y1) = (int(round(a[0])), int(round(a[1]))), (int(round(b[0])), int(round(b[1])))
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err, pts = dx + dy, []
    while True:
        pts.append((x0, y0))
        if (x0, y0) == (x1, y1):
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def star_pixels():
    """As cinco linhas do pentagrama, na ordem em que o traço é desenhado (ponta de cima primeiro)."""
    verts = [ellipse_point(R_INNER - 0.5, i * 2 * math.pi / 5) for i in range(5)]
    order = [0, 2, 4, 1, 3, 0]
    return [line_pixels(verts[order[i]], verts[order[i + 1]]) for i in range(5)]


def rune_pixels():
    out = []
    for i in range(RUNE_COUNT):
        ang = (i + 0.5) * 2 * math.pi / RUNE_COUNT
        x, y = ellipse_point(R_RUNES, ang)
        glyph = GLYPHS[i % len(GLYPHS)]
        x0, y0 = int(round(x)) - 1, int(round(y)) - 1
        out.append([(x0 + gx, y0 + gy) for gy, row in enumerate(glyph) for gx, v in enumerate(row) if v])
    return out


def inside_inner(xs, ys, scale=1.0):
    return ((xs - CX) / (R_INNER * scale)) ** 2 + ((ys - CY) / (R_INNER * 0.5 * scale)) ** 2 < 1.0


class Canvas:
    def __init__(self):
        self.level = np.full((CELL_H, CELL_W), -1, int)
        self.soot = np.zeros((CELL_H, CELL_W), bool)

    def put(self, pts, level):
        for x, y in pts:
            if 0 <= x < CELL_W and 0 <= y < CELL_H:
                self.level[y, x] = max(self.level[y, x], level)

    def image(self, halo):
        img = np.zeros((CELL_H, CELL_W, 4), np.uint8)
        img[self.soot] = SOOT
        lit = self.level >= 0
        if halo > 0:
            # Brilho em volta das linhas: um anel de pixels dois degraus abaixo da linha que o gera.
            grown = np.full_like(self.level, -1)
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                grown = np.maximum(grown, np.roll(np.roll(self.level, dy, 0), dx, 1))
            ring = (~lit) & (grown >= 0)
            for y, x in zip(*np.nonzero(ring)):
                # Só as linhas mais quentes irradiam; com halo 1 o brilho é mais fraco.
                if grown[y, x] < (3 if halo == 2 else 4):
                    continue
                lv = grown[y, x] - 3 - (1 if halo == 1 else 0)
                if lv >= 0:
                    img[y, x] = RAMP[lv]
        for y, x in zip(*np.nonzero(lit)):
            img[y, x] = RAMP[min(self.level[y, x], 5)]
        return img


def soot_mask(amount):
    """Fuligem pontilhada dentro do anel: o chão escurece enquanto o ritual queima."""
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W].astype(float)
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    thr = bayer[ys.astype(int) % 4, xs.astype(int) % 4]
    # Mais densa no centro, rala na borda.
    d = np.sqrt(((xs - CX) / R_INNER) ** 2 + ((ys - CY) / (R_INNER * 0.5)) ** 2)
    density = amount * np.clip(1.15 - d, 0, 1) * 0.55
    return inside_inner(xs, ys) & (thr < density)


RING_OUT = ellipse_pixels(R_OUTER)
RING_IN = ellipse_pixels(R_INNER)
STAR = star_pixels()
RUNES = rune_pixels()


def ignite_frame(k):
    t = (k + 1) / IGNITE_FRAMES
    cv = Canvas()
    cv.soot = soot_mask(t * 0.7)
    # 0-45%: os anéis se fecham; 35-95%: as linhas da estrela; runas acendem no fim.
    ring_t = min(t / 0.45, 1.0)
    for ring in (RING_OUT, RING_IN):
        n = int(len(ring) * ring_t)
        cv.put(ring[:n], 2)
        if ring_t < 1.0 and n > 0:
            cv.put(ring[max(0, n - 2):n], 5)
    star_t = min(max((t - 0.35) / 0.6, 0.0), 1.0) * 5
    for i, seg in enumerate(STAR):
        part = min(max(star_t - i, 0.0), 1.0)
        n = int(len(seg) * part)
        cv.put(seg[:n], 3)
        if 0.0 < part < 1.0 and n > 0:
            cv.put(seg[max(0, n - 2):n], 5)
    if t >= 0.8:
        for i, rune in enumerate(RUNES):
            if i % 2 == 0 or t >= 1.0:
                cv.put(rune, 3 if t >= 1.0 else 2)
    return cv.image(halo=1 if t >= 0.5 else 0)


def pulse_frame(k):
    # Onda de brilho: 0 apagado .. 1 aceso, em seno pelo loop.
    w = 0.5 - 0.5 * math.cos(2 * math.pi * k / PULSE_FRAMES)
    cv = Canvas()
    cv.soot = soot_mask(0.7 + 0.15 * w)
    ring_lv = 2 + int(round(w))
    star_lv = 3 + int(round(w * 1.4))
    cv.put(RING_OUT, ring_lv)
    cv.put(RING_IN, ring_lv)
    for seg in STAR:
        cv.put(seg, star_lv)
    # As runas piscam fora de fase umas das outras.
    for i, rune in enumerate(RUNES):
        phase = (k + i * 2) % PULSE_FRAMES
        cv.put(rune, 4 if phase in (0, 1) else 2)
    # Brasa correndo pelo anel de fora.
    n = len(RING_OUT)
    pos = int(n * k / PULSE_FRAMES)
    cv.put([RING_OUT[(pos + j) % n] for j in range(3)], 5)
    cv.put([RING_OUT[(pos + n // 2 + j) % n] for j in range(3)], 5)
    return cv.image(halo=2 if w > 0.5 else 1)


def burst_frame(k):
    cv = Canvas()
    ys, xs = np.mgrid[0:CELL_H, 0:CELL_W].astype(float)
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
    thr = bayer[ys.astype(int) % 4, xs.astype(int) % 4]
    if k < 2:
        # Clarão: tudo incandescente e o miolo cheio de sangue vivo.
        cv.soot = np.zeros_like(cv.soot)
        fill = inside_inner(xs, ys, 0.55 + 0.25 * k) & (thr < (0.6 if k == 0 else 0.35))
        cv.level[fill] = 3
        for pts in [RING_OUT, RING_IN] + STAR + RUNES:
            cv.put(pts, 5)
        return cv.image(halo=2)
    # Depois apaga: o brilho desce a rampa e as linhas se desfazem em pontilhado de fora para dentro.
    fade = (k - 2) / (BURST_FRAMES - 2)
    lv = max(4 - int(fade * 5), 0)
    cv.soot = soot_mask(0.8 * (1 - fade))
    for pts in [RING_OUT, RING_IN] + STAR + RUNES:
        cv.put(pts, lv)
    keep = thr >= fade * 1.05
    cv.level[~keep] = -1
    return cv.image(halo=1 if fade < 0.3 else 0)


def build_sheet():
    frames = [ignite_frame(k) for k in range(IGNITE_FRAMES)]
    frames += [pulse_frame(k) for k in range(PULSE_FRAMES)]
    frames += [burst_frame(k) for k in range(BURST_FRAMES)]
    return frames


def save_gif(frames, path, scale=4):
    """Prévia: acende, pulsa duas vezes, estoura, sobre um chão escuro."""
    seq = list(range(IGNITE_FRAMES)) + list(range(IGNITE_FRAMES, IGNITE_FRAMES + PULSE_FRAMES)) * 3
    seq += list(range(IGNITE_FRAMES + PULSE_FRAMES, IGNITE_FRAMES + PULSE_FRAMES + BURST_FRAMES)) + [-1] * 6
    durations = [70] * IGNITE_FRAMES + [110] * PULSE_FRAMES * 3 + [70] * BURST_FRAMES + [90] * 6
    bg = (0x22, 0x1c, 0x1a, 255)
    out = []
    for idx in seq:
        canvas = Image.new("RGBA", (CELL_W + 16, CELL_H + 16), bg)
        if idx >= 0:
            canvas.alpha_composite(Image.fromarray(frames[idx], "RGBA"), (8, 8))
        out.append(canvas.convert("RGB").resize((canvas.width * scale, canvas.height * scale), Image.NEAREST))
    out[0].save(path, save_all=True, append_images=out[1:], duration=durations, loop=0)


def main():
    args = sys.argv[1:]
    gif = None
    if "--gif" in args:
        i = args.index("--gif")
        gif = Path(args[i + 1])
        del args[i:i + 2]
    root = Path(__file__).resolve().parents[3]
    out = Path(args[0]) if args else root / "assets/pixel_art/effects/ritual_circle"
    out.mkdir(parents=True, exist_ok=True)
    frames = build_sheet()
    sheet = np.concatenate(frames, axis=1)
    Image.fromarray(sheet, "RGBA").save(out / "ritual_circle_sheet.png")
    if gif:
        save_gif(frames, gif)
    print("ok:", out)


if __name__ == "__main__":
    main()
