"""Remapeia as folhas dos monstros importados (modo 2D) para a paleta AAP-64, a mesma do kit da cripta,
do Virgil, do chão e da poça de sangue.

Lê os originais do pacote em `tools/pixel_art/monsters/source/<monstro>/*.png` (intocados) e grava as versões
remapeadas em `assets/pixel_art/monsters/<monstro>/*.png`, com os mesmos nomes. Para regerar:

    py tools/pixel_art/monsters/recolor_monsters.py [--preview arquivo.png]

Cada cor do original vira a cor da AAP-64 mais próxima em OKLab, com mais peso na luminosidade (para manter
o volume do sombreado) e menos no croma. Os vermelhos saturados (olhos, sangue) ficam presos à rampa de sangue
e os cinzas neutros (metal) à rampa fria da pedra da cripta. Duas cores do original que caíssem na mesma cor da paleta são
separadas pela segunda opção mais próxima quando a diferença de luminosidade entre elas é visível, para não
apagar detalhes. A transparência vira binária (alfa 0 ou 255, corte em 128), como o corte de alfa do Sprite3D no jogo.

Exceções:
- `mage_guardian` (Guardiã Invisível) pode usar o rosa bc4a9b e os azuis vivos da AAP-64, que são a cor dela e
  dos efeitos dela (`BossRuneFx.Violet`).
- `skeleton_guardian` (lacaios da Guardiã) não tem original próprio: sai do `skeleton` tingido na rampa violeta
  da Guardiã, pela luminosidade (o sombreado é mantido, as cores viram roxo e rosa).
"""
import argparse
import pathlib

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "tools" / "pixel_art" / "monsters" / "source"
TARGET = ROOT / "assets" / "pixel_art" / "monsters"

AAP64 = """060608 141013 3b1725 73172d b4202a df3e23 fa6a0a f9a31b ffd541 fffc40 d6f264 9cdb43 59c135 14a02e 1a7a3e 24523b
122020 143464 285cc4 249fde 20d6c7 a6fcdb ffffff fef3c0 fad6b8 f5a097 e86a73 bc4a9b 793a80 403353 242234 221c1a
322b28 71413b bb7547 dba463 f4d29c dae0ea b3b9d1 8b93af 6d758d 4a5462 333941 422433 5b3138 8e5252 ba756a e9b5a3
e3e6ff b9bffb 849be4 588dbe 477d85 23674e 328464 5daf8d 92dcba cdf7e2 e4d2aa c7b08b a08662 796755 5a4e44 423934""".split()

# Cores berrantes da AAP-64 que não aparecem em nenhum asset nosso: ficam de fora para os monstros não
# destoarem da cripta (neon, ciano, rosa e o verde-limão).
EXCLUDED = set("fffc40 d6f264 9cdb43 59c135 20d6c7 a6fcdb bc4a9b 249fde 285cc4 fa6a0a".split())
PALETTE = [h for h in AAP64 if h not in EXCLUDED]

# Rampa de sangue (a da poça de sangue, sem o laranja df3e23): destino dos vermelhos saturados (olhos, sangue,
# boca), para não puxarem para o laranja.
BLOOD = ["3b1725", "73172d", "b4202a", "e86a73", "f5a097"]

# Rampa fria da pedra da cripta: destino dos cinzas neutros (metal, escudo, elmo), que senão caem nos marrons.
STONE = ["060608", "141013", "242234", "333941", "4a5462", "6d758d", "8b93af", "b3b9d1", "dae0ea", "e3e6ff", "ffffff"]
NEUTRAL_CHROMA = 0.02

# Cores extras permitidas por monstro (além de PALETTE).
EXTRA_COLORS = {"mage_guardian": ["bc4a9b", "249fde", "285cc4"]}

# Monstros derivados: pasta nova = original de outro monstro tingido numa rampa (do escuro ao claro).
GUARDIAN_RAMP = ["060608", "141013", "242234", "403353", "793a80", "bc4a9b", "e9b5a3", "fad6b8"]
TINT_GAMMA = 0.7
DERIVED = {"skeleton_guardian": ("skeleton", GUARDIAN_RAMP)}

LIGHTNESS_WEIGHT = 1.6
CHROMA_WEIGHT = 0.8


def hex_to_rgb(h):
    return [int(h[i:i + 2], 16) for i in (0, 2, 4)]


def srgb_to_oklab(rgb):
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    l = 0.4122214708 * c[..., 0] + 0.5363325363 * c[..., 1] + 0.0514459929 * c[..., 2]
    m = 0.2119034982 * c[..., 0] + 0.6806995451 * c[..., 1] + 0.1073969566 * c[..., 2]
    s = 0.0883024619 * c[..., 0] + 0.2817188376 * c[..., 1] + 0.6299787005 * c[..., 2]
    l, m, s = np.cbrt(l), np.cbrt(m), np.cbrt(s)
    return np.stack([
        0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
        1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
        0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
    ], axis=-1)


def distances(lab, candidates):
    d = candidates - lab
    return LIGHTNESS_WEIGHT * d[:, 0] ** 2 + CHROMA_WEIGHT * (d[:, 1] ** 2 + d[:, 2] ** 2)


def candidates_for(lab, pal):
    chroma = np.hypot(lab[1], lab[2])
    hue = np.degrees(np.arctan2(lab[2], lab[1])) % 360
    if chroma > 0.12 and (hue < 45 or hue > 340):
        return np.array([pal.index(h) for h in BLOOD])
    if chroma < NEUTRAL_CHROMA:
        return np.array([pal.index(h) for h in STONE])
    return np.arange(len(pal))


def build_tint_mapping(colors, ramp):
    """Tinge pela luminosidade: a faixa de luminosidade do original é esticada sobre a da rampa."""
    ramp_l = srgb_to_oklab([hex_to_rgb(h) for h in ramp])[:, 0]
    ls = {rgb: srgb_to_oklab(rgb)[0] for rgb in colors}
    lo, hi = min(ls.values()), max(ls.values())
    mapping = {}
    for rgb, l in ls.items():
        # Gama < 1 clareia os meios-tons: os ossos do esqueleto são escuros no original e sumiriam no roxo.
        t = ((l - lo) / max(hi - lo, 1e-6)) ** TINT_GAMMA
        target = ramp_l[0] + t * (ramp_l[-1] - ramp_l[0])
        mapping[rgb] = tuple(hex_to_rgb(ramp[int(np.argmin(np.abs(ramp_l - target)))]))
    return mapping


def build_mapping(colors, pal=PALETTE):
    """colors: lista de tuplas RGB do monstro inteiro (todas as folhas). Retorna {rgb: rgb da paleta}."""
    pal_lab = srgb_to_oklab([hex_to_rgb(h) for h in pal])
    mapping = {}
    choices = {}
    for rgb in colors:
        lab = srgb_to_oklab(rgb)
        idx = candidates_for(lab, pal)
        d = distances(lab, pal_lab[idx])
        order = idx[np.argsort(d)]
        choices[rgb] = (lab, order)
        mapping[rgb] = int(order[0])

    # Separa cores que colidiram mas que eram visivelmente diferentes no original (mais clara sobe uma opção).
    by_target = {}
    for rgb, t in mapping.items():
        by_target.setdefault(t, []).append(rgb)
    used = set(mapping.values())
    for t, group in by_target.items():
        if len(group) < 2:
            continue
        group.sort(key=lambda c: choices[c][0][0])
        for a, b in zip(group, group[1:]):
            if choices[b][0][0] - choices[a][0][0] < 0.06 or mapping[b] != mapping[a]:
                continue
            for alt in choices[b][1][1:4]:
                alt = int(alt)
                if alt not in used and pal_lab[alt][0] > pal_lab[mapping[a]][0]:
                    mapping[b] = alt
                    used.add(alt)
                    break
    return {rgb: tuple(hex_to_rgb(pal[i])) for rgb, i in mapping.items()}


def recolor(image, mapping):
    arr = np.array(image.convert("RGBA"))
    out = arr.copy()
    flat = arr.reshape(-1, 4)
    oflat = out.reshape(-1, 4)
    visible = flat[:, 3] > 0
    for rgb, new in mapping.items():
        mask = visible & (flat[:, 0] == rgb[0]) & (flat[:, 1] == rgb[1]) & (flat[:, 2] == rgb[2])
        oflat[mask, :3] = new
    return Image.fromarray(out, "RGBA")


def monster_colors(files):
    colors = set()
    for f in files:
        arr = np.array(Image.open(f).convert("RGBA")).reshape(-1, 4)
        for px in np.unique(arr[arr[:, 3] > 0][:, :3], axis=0):
            colors.add(tuple(int(v) for v in px))
    return sorted(colors)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--preview", help="grava uma comparação antes/depois (1º quadro de cada folha)")
    args = parser.parse_args()

    jobs = []
    for folder in sorted(p for p in SOURCE.iterdir() if p.is_dir()):
        files = sorted(folder.glob("*.png"))
        pal = PALETTE + EXTRA_COLORS.get(folder.name, [])
        jobs.append((folder.name, files, build_mapping(monster_colors(files), pal), pal))
    for name, (source, ramp) in DERIVED.items():
        files = sorted((SOURCE / source).glob("*.png"))
        jobs.append((name, files, build_tint_mapping(monster_colors(files), ramp), ramp))

    rows = []
    for name, files, mapping, pal in jobs:
        allowed = {tuple(hex_to_rgb(h)) for h in pal}
        (TARGET / name).mkdir(parents=True, exist_ok=True)
        for f in files:
            src = Image.open(f).convert("RGBA")
            dst = recolor(src, mapping)
            # Pixel art sem meio-termo: alfa vira 0 ou 255 (sombras translúcidas somem, como no corte de alfa do jogo).
            alpha = np.array(dst)[:, :, 3]
            dst.putalpha(Image.fromarray(np.where(alpha >= 128, 255, 0).astype(np.uint8)))
            used = {tuple(int(v) for v in px) for px in np.unique(
                np.array(dst).reshape(-1, 4)[np.array(dst).reshape(-1, 4)[:, 3] > 0][:, :3], axis=0)}
            assert used <= allowed, (f, used - allowed)
            dst.save(TARGET / name / f.name)
            rows.append((src, dst))
        print(f"{name}: {len(files)} folhas, {len(mapping)} cores -> {len(set(mapping.values()))} da AAP-64")

    if args.preview:
        scale, size = 2, 150
        sheet = Image.new("RGBA", (len(rows) * size * scale, 2 * size * scale), (36, 34, 52, 255))
        for i, (src, dst) in enumerate(rows):
            for j, im in enumerate((src, dst)):
                frame = im.crop((0, 0, min(size, im.height), min(size, im.height))).resize((size * scale, size * scale), Image.NEAREST)
                sheet.alpha_composite(frame, (i * size * scale, j * size * scale))
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
