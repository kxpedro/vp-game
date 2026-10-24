"""Monta as peças de HUD em pixel art (modo 2D) a partir de recortes de pacotes de terceiros, remapeados para a
paleta AAP-64 do jogo (a mesma do kit da cripta, do Virgil e dos monstros).

Os recortes originais ficam em `tools/pixel_art/hud/source/` (intocados; origem em `assets/pixel_art/hud/SOURCE.md`).
As versões remapeadas e ampliadas (vizinho mais próximo) vão para `assets/pixel_art/hud/`. Para regerar:

    py tools/pixel_art/hud/build_hud_kit.py [--preview arquivo.png]

O remapeamento é o de `tools/pixel_art/monsters/recolor_monsters.py` (OKLab, vermelhos presos à rampa de sangue,
cinzas neutros à rampa fria da pedra), aplicado peça a peça.
"""
import argparse
import pathlib
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "tools" / "pixel_art" / "hud" / "source"
TARGET = ROOT / "assets" / "pixel_art" / "hud"

sys.path.insert(0, str(ROOT / "tools" / "pixel_art" / "monsters"))
import recolor_monsters as recolor  # noqa: E402

# Peça -> (escala na tela, rampa, miolo). Os quadros (9-slice) usam 2x para a borda ficar com a espessura das molduras
# do HUD; os ícones de recurso usam 1x (32 px, reduzidos pelo TextureRect para o tamanho do glifo).
# rampa: em vez do remapeamento geral, as cores do original vão por ordem de luminosidade para esta rampa da AAP-64
#        (o violeta do Sangue Amaldiçoado cairia no azul-lavanda e a moeda no salmão pelo remapeamento geral).
# miolo: a cor mais frequente (o fundo do quadro) vira esta cor com este alfa, escura como os painéis do HUD.
# Uma cor da rampa por cor do original (5 -> 5), sem o preto do fundo do painel, para o ícone não sumir nele.
CURSED_RAMP = ["242234", "793a80", "bc4a9b", "b9bffb", "e3e6ff"]
# Moeda de Sangue: rampa de sangue da poça, deslocada para o claro (a moeda do pacote é vermelho-alaranjada).
BLOOD_RAMP = ["3b1725", "b4202a", "e86a73", "f5a097", "fef3c0"]
PANEL_FILL = ("141013", 224)
PIECES = {
    "bar_frame.png": (2, None, None),
    "bar_track_thin.png": (2, None, None),
    "panel_frame.png": (2, None, PANEL_FILL),
    "coin_blood.png": (1, BLOOD_RAMP, None),
    "gem_cursed.png": (1, CURSED_RAMP, None),
}


def colors_of(image):
    arr = np.array(image).reshape(-1, 4)
    return sorted({tuple(int(v) for v in px) for px in np.unique(arr[arr[:, 3] > 0][:, :3], axis=0)})


def ramp_mapping(colors, ramp):
    """Ordena as cores do original pela luminosidade (OKLab L) e distribui pela rampa, do escuro ao claro."""
    ordered = sorted(colors, key=lambda c: recolor.srgb_to_oklab(c)[0])
    last = max(len(ordered) - 1, 1)
    return {c: tuple(recolor.hex_to_rgb(ramp[round(i * (len(ramp) - 1) / last)])) for i, c in enumerate(ordered)}


def fill_center(image, fill):
    arr = np.array(image)
    flat = arr.reshape(-1, 4)
    visible = flat[:, 3] > 0
    values, counts = np.unique(flat[visible][:, :3], axis=0, return_counts=True)
    target = values[np.argmax(counts)]
    mask = visible & np.all(flat[:, :3] == target, axis=1)
    flat[mask, :3] = recolor.hex_to_rgb(fill[0])
    flat[mask, 3] = fill[1]
    return Image.fromarray(arr, "RGBA")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--preview", help="grava uma comparação antes/depois de todas as peças")
    args = parser.parse_args()

    allowed = {tuple(recolor.hex_to_rgb(h)) for h in recolor.AAP64}
    TARGET.mkdir(parents=True, exist_ok=True)
    rows = []
    for name, (scale, ramp, fill) in PIECES.items():
        src = Image.open(SOURCE / name).convert("RGBA")
        colors = colors_of(src)
        mapping = ramp_mapping(colors, ramp) if ramp else recolor.build_mapping(colors)
        dst = recolor.recolor(src, mapping)
        if fill:
            dst = fill_center(dst, fill)
        assert set(colors_of(dst)) <= allowed, name
        if scale != 1:
            dst = dst.resize((dst.width * scale, dst.height * scale), Image.NEAREST)
        dst.save(TARGET / name)
        rows.append((src, dst))
        print(f"{name}: {src.width}x{src.height} -> {dst.width}x{dst.height}")

    if args.preview:
        k = 6
        width = sum(max(s.width * k, d.width * k // 2) + 16 for s, d in rows)
        height = max(s.height * k for s, _ in rows) * 2 + 16
        sheet = Image.new("RGBA", (width, height), (36, 34, 52, 255))
        x = 0
        for src, dst in rows:
            sheet.alpha_composite(src.resize((src.width * k, src.height * k), Image.NEAREST), (x, 0))
            scaled = dst.resize((src.width * k, src.height * k), Image.NEAREST)
            sheet.alpha_composite(scaled, (x, height // 2 + 8))
            x += src.width * k + 16
        sheet.save(args.preview)


if __name__ == "__main__":
    main()
