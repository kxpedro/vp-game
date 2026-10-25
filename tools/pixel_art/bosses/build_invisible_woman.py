"""Monta a arte da Guardiã Invisível (boss 1) para o modo 2D: o sprite dela e os efeitos dos golpes.

Sprite: pacote **Blue Witch** (folhas verticais de 32x48, 104x46 e 48x48, cada uma com a bruxa num ponto diferente).
Os quadros vão para uma tela única por animação, alinhados pelos pés e pelo centro do corpo, sem a sombra
desenhada (o jogo tem a dele), com os detalhes ciano puxados para lavanda (o psiônico da Guardiã) e a paleta
remapeada para a AAP-64 com o mesmo método dos monstros (tools/pixel_art/monsters/recolor_monsters.py).

Efeitos: folhas do **VFX Free Pack** e do **FXpack13** reduzidas para pixel art: o brilho de cada pixel vira um
degrau de uma rampa da AAP-64 (violeta, ou sangue na Singularidade Rubra) e a transparência fica binária.

Os originais ficam em tools/pixel_art/bosses/source/ (fora do Godot pelo .gdignore). Saídas:
- assets/pixel_art/bosses/invisible_woman/<anim>.png: faixas horizontais, quadros de SPRITE_W x SPRITE_H.
- assets/pixel_art/effects/boss_invisible_woman/<efeito>.png: faixas horizontais (tamanhos em EFFECTS).

    py tools/pixel_art/bosses/build_invisible_woman.py [--preview prévia.png]

Requer numpy e Pillow. Ao mudar tamanho ou número de quadros, atualize scripts/pixel/PixelEnemySprite.cs e
scripts/bosses/BossVfx.cs.
"""
import argparse
import colorsys
import importlib.util
import pathlib

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "tools" / "pixel_art" / "bosses" / "source"
SPRITE_OUT = ROOT / "assets" / "pixel_art" / "bosses" / "invisible_woman"
EFFECT_OUT = ROOT / "assets" / "pixel_art" / "effects" / "boss_invisible_woman"

_spec = importlib.util.spec_from_file_location("recolor", ROOT / "tools" / "pixel_art" / "monsters" / "recolor_monsters.py")
recolor = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(recolor)

# ---------------- Sprite ----------------

# Tela comum: a bruxa centrada em x = SPRITE_W/2, pés (última linha do corpo) em y = FEET_Y.
SPRITE_W, SPRITE_H, FEET_Y = 176, 52, 45
# (arquivo, saída, altura do quadro no original; 0 = recortar pelo conteúdo)
SHEETS = [
    ("B_witch_idle.png", "idle", 48),
    ("B_witch_run.png", "walk", 48),
    ("B_witch_attack.png", "attack", 46),
    ("B_witch_charge.png", "cast", 48),
    ("B_witch_take_damage.png", "hit", 48),
    ("B_witch_death.png", "death", 0),
]


def frames_of(path, frame_h):
    image = np.array(Image.open(path).convert("RGBA"))
    if frame_h:
        return [image[i * frame_h:(i + 1) * frame_h] for i in range(image.shape[0] // frame_h)]
    # Morte: os quadros não seguem uma grade; cada bloco de linhas com conteúdo (vãos < 6 px unidos) é um quadro.
    rows = (image[:, :, 3] == 255).any(axis=1)
    blocks, start, gap = [], None, 0
    for y, filled in enumerate(rows):
        if filled:
            if start is None:
                start = y
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= 6:
                blocks.append((start, y - gap + 1))
                start, gap = None, 0
    if start is not None:
        blocks.append((start, len(rows)))
    return [image[a:b] for a, b in blocks]


def body_center_x(frame):
    """Centro do corpo: mediana das colunas opacas na faixa do tronco (ignora projéteis e brilhos à direita)."""
    opaque = frame[:, :, 3] == 255
    ys, xs = np.nonzero(opaque)
    bottom = ys.max()
    torso = (ys > bottom - 22) & (ys < bottom - 4)
    cols = xs[torso] if torso.any() else xs
    # O corpo é o primeiro aglomerado da esquerda (os cristais do ataque saem para a direita).
    cols = np.sort(cols)
    cluster = cols[cols <= cols[0] + 20]
    return int(round(np.median(cluster)))


def hue_shift_cyan(rgb):
    """Ciano/teal -> lavanda (rampa psiônica); o resto fica como está."""
    r, g, b = (v / 255.0 for v in rgb)
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    if s > 0.15 and 0.42 < h < 0.6:
        h = 0.70
        s = min(s * 0.85, 1.0)
        l = min(l * 1.1, 1.0)
    return tuple(int(round(v * 255)) for v in colorsys.hls_to_rgb(h, l, s))


def build_sprite():
    sheets = {}
    for file, name, frame_h in SHEETS:
        frames = []
        all_frames = frames_of(SOURCE / "blue_witch" / file, frame_h)
        # Referência horizontal de toda a folha: o 1º quadro (no ataque os cristais não mexem o corpo).
        reference_x = body_center_x(all_frames[0])
        for frame in all_frames:
            frame = frame.copy()
            frame[frame[:, :, 3] < 255] = 0          # sombra semitransparente fora
            ys = np.nonzero(frame[:, :, 3])[0]
            if len(ys) == 0:
                continue
            canvas = np.zeros((SPRITE_H, SPRITE_W, 4), np.uint8)
            dx = SPRITE_W // 2 - reference_x
            # Morte: blocos recortados; o fim de cada bloco é o chão. Demais: pés pela última linha da folha inteira.
            dy = FEET_Y - (ys.max() if frame_h == 0 else sheet_feet(all_frames))
            h, w = frame.shape[:2]
            for y in range(h):
                ty = y + dy
                if not 0 <= ty < SPRITE_H:
                    continue
                for x in range(w):
                    tx = x + dx
                    if 0 <= tx < SPRITE_W and frame[y, x, 3]:
                        canvas[ty, tx] = frame[y, x]
            frames.append(canvas)
        sheets[name] = frames

    # Uma paleta só para o sprite inteiro: o mesmo tecido tem a mesma cor em todas as animações.
    colors = set()
    for frames in sheets.values():
        for f in frames:
            for px in np.unique(f[f[:, :, 3] > 0][:, :3], axis=0):
                colors.add(tuple(int(v) for v in px))
    shifted = {c: hue_shift_cyan(c) for c in colors}
    mapping = recolor.build_mapping(sorted(set(shifted.values())))
    SPRITE_OUT.mkdir(parents=True, exist_ok=True)
    out = {}
    # Todas as faixas com o mesmo número de quadros (as que têm menos ficam com quadros vazios no fim): o Sprite3D
    # mantém o mesmo hframes ao trocar de animação (com mistura, a troca de folha e de quadro não chega junta).
    # O jogo conta os quadros de verdade pela área usada da imagem.
    longest = max(len(frames) for frames in sheets.values())
    for name, frames in sheets.items():
        frames = frames + [np.zeros_like(frames[0])] * (longest - len(frames))
        strip = np.concatenate(frames, axis=1)
        flat = strip.reshape(-1, 4)
        for original, new in shifted.items():
            mask = (flat[:, 3] > 0) & (flat[:, 0] == original[0]) & (flat[:, 1] == original[1]) & (flat[:, 2] == original[2])
            flat[mask, :3] = mapping[new]
        image = Image.fromarray(strip, "RGBA")
        image.save(SPRITE_OUT / f"{name}.png")
        out[name] = image
        print(f"sprite {name}: {len(sheets[name])} quadros de {SPRITE_W}x{SPRITE_H} (faixa de {longest})")
    return out


def sheet_feet(frames):
    # Pés = linha opaca mais baixa entre os quadros da folha (a sombra já foi apagada).
    bottom = 0
    for frame in frames:
        ys = np.nonzero(frame[:, :, 3] == 255)[0]
        if len(ys):
            bottom = max(bottom, ys.max())
    return bottom


# ---------------- Efeitos ----------------

VIOLET = ["242234", "403353", "793a80", "849be4", "b9bffb", "e3e6ff", "ffffff"]
BLOOD = ["3b1725", "73172d", "b4202a", "df3e23", "e86a73", "f5a097", "ffffff"]

# Fração dos pixels visíveis de cada efeito em cada degrau da rampa (do escuro ao branco): os originais têm o
# núcleo quase todo branco, então a cor sai da posição do brilho do pixel entre os do efeito, não do valor bruto.
RAMP_SHARE = [0.12, 0.18, 0.22, 0.20, 0.14, 0.09, 0.05]

# saída: (origem, (largura x altura do quadro na grade, ou 0 = pasta de quadros), quadros usados, tamanho final,
# rampa, limiar de brilho abaixo do qual o pixel fica transparente)
EFFECTS = {
    "orb": ("FXpack13_Effect1", 0, 12, 24, VIOLET, 0.10),
    "burst": ("Effect_Explosion2_1_355x355.png", (355, 355), 15, 56, VIOLET, 0.10),
    "shield": ("Effect_ElectricShield_1_265x265.png", (265, 265), 15, 56, VIOLET, 0.12),
    "ring": ("Effect_EldenRing_1_421x425.png", (421, 425), 12, 64, VIOLET, 0.12),
    "vortex": ("Effect_TheVortex_1_427x431.png", (427, 431), 15, 64, BLOOD, 0.10),
}


def effect_frames(source, grid):
    path = SOURCE / "vfx" / source
    if grid == 0:
        def number(p):
            return int(p.stem)
        return [Image.open(p).convert("RGBA") for p in sorted(path.glob("*.png"), key=number)]
    sheet = Image.open(path).convert("RGBA")
    fw, fh = grid
    cols, rows = sheet.width // fw, sheet.height // fh
    return [sheet.crop((c * fw, r * fh, (c + 1) * fw, (r + 1) * fh)) for r in range(rows) for c in range(cols)]


def intensity_map(frame, size):
    """Reduz por média de área para size x size (centrado) e devolve o brilho de cada pixel (0 a 1)."""
    w, h = frame.size
    scale = size / max(w, h)
    small = frame.resize((max(round(w * scale), 1), max(round(h * scale), 1)), Image.BOX)
    arr = np.asarray(small, dtype=np.float64) / 255.0
    luminance = arr[:, :, :3].max(axis=2) * 0.6 + arr[:, :, :3].mean(axis=2) * 0.4
    out = np.zeros((size, size))
    oy, ox = (size - arr.shape[0]) // 2, (size - arr.shape[1]) // 2
    out[oy:oy + arr.shape[0], ox:ox + arr.shape[1]] = arr[:, :, 3] * luminance
    return out


def quantize(maps, ramp, threshold):
    """Troca o brilho por um degrau da rampa, pelos quantis do efeito inteiro (RAMP_SHARE); alfa binário."""
    visible = np.concatenate([m[m >= threshold] for m in maps])
    edges = np.quantile(visible, np.cumsum(RAMP_SHARE)[:-1]) if len(visible) else []
    colors = np.array([recolor.hex_to_rgb(h) for h in ramp], np.uint8)
    cells = []
    for m in maps:
        cell = np.zeros(m.shape + (4,), np.uint8)
        mask = m >= threshold
        cell[mask, :3] = colors[np.searchsorted(edges, m[mask], side="right")]
        cell[mask, 3] = 255
        cells.append(cell)
    return cells


def build_effects():
    EFFECT_OUT.mkdir(parents=True, exist_ok=True)
    out = {}
    for name, (source, grid, count, size, ramp, threshold) in EFFECTS.items():
        frames = effect_frames(source, grid)
        # Quadros igualmente espaçados; descarta os que ficam vazios depois da redução.
        picks = [frames[round(i * (len(frames) - 1) / (count - 1))] for i in range(count)]
        cells = quantize([intensity_map(f, size) for f in picks], ramp, threshold)
        cells = [c for c in cells if c[:, :, 3].any()] or cells[:1]
        image = Image.fromarray(np.concatenate(cells, axis=1), "RGBA")
        image.save(EFFECT_OUT / f"{name}.png")
        out[name] = image
        print(f"efeito {name}: {len(cells)} quadros de {size}x{size}")
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--preview", help="grava uma prévia ampliada de todas as folhas")
    args = parser.parse_args()
    sprites = build_sprite()
    effects = build_effects()
    if args.preview:
        images = list(sprites.values()) + list(effects.values())
        scale = 2
        width = max(i.width for i in images) * scale
        height = sum(i.height * scale + 6 for i in images)
        sheet = Image.new("RGBA", (min(width, 4000), height), (36, 34, 52, 255))
        y = 0
        for image in images:
            big = image.resize((image.width * scale, image.height * scale), Image.NEAREST)
            sheet.alpha_composite(big.crop((0, 0, min(big.width, 4000), big.height)), (0, y))
            y += big.height + 6
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
