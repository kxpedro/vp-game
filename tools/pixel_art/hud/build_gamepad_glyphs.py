"""Recorta os glifos de botão do pacote gdb-gamepad-2 (Xbox e PlayStation) e os remapeia para a paleta AAP-64.

As folhas originais ficam em `tools/pixel_art/hud/source/gamepad/` (intocadas; origem em
`assets/pixel_art/hud/SOURCE.md`). Cada glifo vai para `assets/pixel_art/hud/gamepad/<dispositivo>_<nome>.png`
(1x, alfa binário); `UiComponents.ButtonGlyph` os amplia 2x e usa o desenho por código quando a peça falta. Para regerar:

    py tools/pixel_art/hud/build_gamepad_glyphs.py [--preview arquivo.png]

O remapeamento é o de `tools/pixel_art/monsters/recolor_monsters.py` (AAP-64 inteira), feito uma vez por dispositivo para os botões do
mesmo controle ficarem com as mesmas cores.
"""
import argparse
import pathlib
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "tools" / "pixel_art" / "hud" / "source" / "gamepad"
TARGET = ROOT / "assets" / "pixel_art" / "hud" / "gamepad"

sys.path.insert(0, str(ROOT / "tools" / "pixel_art" / "monsters"))
import recolor_monsters as recolor  # noqa: E402

# nome -> (x, y, largura, altura) na folha 560x640 de cada controle (coluna "flat large" e colunas pequenas).
XBOX = {
    "x": (17, 33, 14, 14), "a": (17, 49, 14, 14), "y": (17, 65, 14, 14), "b": (17, 81, 14, 14),
    "view": (18, 98, 12, 12), "menu": (18, 114, 12, 12),
    "lt": (336, 49, 16, 14), "rt": (336, 65, 16, 14), "lb": (336, 85, 16, 8), "rb": (336, 101, 16, 8),
    "ls": (217, 33, 14, 14), "rs": (281, 33, 14, 14),
}
PLAYSTATION = {
    "triangle": (17, 33, 14, 14), "circle": (17, 49, 14, 14), "cross": (17, 65, 14, 14), "square": (17, 81, 14, 14),
    "options": (258, 229, 13, 7), "create": (258, 245, 13, 7),
    "l2": (336, 81, 16, 14), "r2": (336, 97, 16, 14), "l1": (337, 115, 14, 10), "r1": (337, 131, 14, 10),
    "ls": (217, 33, 14, 14), "rs": (281, 33, 14, 14),
}
SHEETS = {"xbox": ("gdb-xbox-2.png", XBOX), "ps": ("gdb-playstation-2.png", PLAYSTATION)}


def colors_of(image):
    arr = np.array(image).reshape(-1, 4)
    return sorted({tuple(int(v) for v in px) for px in np.unique(arr[arr[:, 3] > 0][:, :3], axis=0)})


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--preview", help="grava uma comparação antes/depois de todos os glifos")
    args = parser.parse_args()

    allowed = {tuple(recolor.hex_to_rgb(h)) for h in recolor.AAP64}
    TARGET.mkdir(parents=True, exist_ok=True)
    rows = []
    for device, (sheet_name, boxes) in SHEETS.items():
        sheet = Image.open(SOURCE / sheet_name).convert("RGBA")
        crops = {}
        for name, (x, y, w, h) in boxes.items():
            crop = np.array(sheet.crop((x, y, x + w, y + h)))
            crop[:, :, 3] = np.where(crop[:, :, 3] > 127, 255, 0)
            crops[name] = Image.fromarray(crop, "RGBA")
        # Paleta inteira (sem o EXCLUDED dos monstros): o azul do ✕/X e o verde do A precisam dos tons vivos.
        mapping = recolor.build_mapping(sorted({c for im in crops.values() for c in colors_of(im)}), list(recolor.AAP64))
        for name, src in crops.items():
            dst = recolor.recolor(src, mapping)
            assert set(colors_of(dst)) <= allowed, f"{device}_{name}"
            dst.save(TARGET / f"{device}_{name}.png")
            rows.append((src, dst))
            print(f"{device}_{name}.png: {dst.width}x{dst.height}")

    if args.preview:
        k = 4
        width = sum(s.width * k + 10 for s, _ in rows)
        height = max(s.height for s, _ in rows) * k * 2 + 30
        out = Image.new("RGBA", (width, height), (36, 34, 52, 255))
        x = 0
        for src, dst in rows:
            out.alpha_composite(src.resize((src.width * k, src.height * k), Image.NEAREST), (x, 4))
            out.alpha_composite(dst.resize((dst.width * k, dst.height * k), Image.NEAREST), (x, height // 2 + 4))
            x += src.width * k + 10
        out.save(args.preview)


if __name__ == "__main__":
    main()
