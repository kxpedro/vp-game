"""Confere se as folhas de pixel art usam só cores da AAP-64 e alfa binário (0 ou 255).

Sem argumentos, confere o kit da cripta, o kit do Jardim Sangrento, os chãos, os efeitos e os monstros (menos a
Guardiã, que fica em magenta de propósito; ver assets/pixel_art/monsters/SOURCE.md). As alturas (heights/) ficam
de fora: não são cor. Sai com 1 se alguma folha tiver cor fora da paleta ou alfa parcial.

Uso: py tools/pixel_art/check_palette.py [pasta ...]
"""
import pathlib
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "pixel_art" / "monsters"))
from recolor_monsters import AAP64, hex_to_rgb  # noqa: E402

DEFAULT = ["assets/pixel_art/crypt", "assets/pixel_art/garden", "assets/pixel_art/tiles", "assets/pixel_art/effects",
           "assets/pixel_art/monsters"]
EXCEPTIONS = ["assets/pixel_art/monsters/mage_guardian"]


def main():
    palette = {tuple(hex_to_rgb(h)) for h in AAP64}
    folders = sys.argv[1:] or DEFAULT
    errors = checked = 0
    for folder in folders:
        for path in sorted((ROOT / folder).rglob("*.png")):
            rel = path.relative_to(ROOT).as_posix()
            if "/heights/" in rel or any(rel.startswith(e + "/") for e in EXCEPTIONS):
                continue
            checked += 1
            rgba = np.array(Image.open(path).convert("RGBA"))
            alpha = rgba[..., 3]
            colors = {tuple(int(v) for v in c) for c in np.unique(rgba[alpha > 0][:, :3], axis=0)}
            off = colors - palette
            partial = int(((alpha > 0) & (alpha < 255)).sum())
            if off or partial:
                errors += 1
                print(f"ERRO {rel}: {len(off)} cor(es) fora da AAP-64, {partial} pixel(s) com alfa parcial")
    print(f"check_palette: {checked} folha(s), {errors} com erro.")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
