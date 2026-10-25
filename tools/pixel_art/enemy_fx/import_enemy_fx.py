"""Efeitos dos ataques dos monstros (modo 2D): recorta dos pacotes de efeitos a faixa de cor de cada efeito e a
remapeia para a paleta AAP-64.

Os pacotes "Effect and FX Pixel All Free" e "RPG Effect All Free" (F:\\ASSETS no PC do Pedro) trazem cada efeito
num PNG com 9 linhas de quadros de 64x64, uma por cor. Daqui sai só a linha escolhida para cada ataque, sem os
quadros vazios. Os recortes originais ficam em `tools/pixel_art/enemy_fx/source/` (fora do Godot, pelo `.gdignore`
de `tools/pixel_art/`) e as versões remapeadas em `assets/pixel_art/effects/enemy_attacks/`, usadas por
`scripts/enemies/EnemyAttackFx.cs`.

    py tools/pixel_art/enemy_fx/import_enemy_fx.py                 (remapeia os recortes de source/)
    py tools/pixel_art/enemy_fx/import_enemy_fx.py --packs <pasta>  (recorta de novo dos pacotes descompactados)
"""
import argparse
import pathlib
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "tools" / "pixel_art" / "enemy_fx" / "source"
TARGET = ROOT / "assets" / "pixel_art" / "effects" / "enemy_attacks"
sys.path.insert(0, str(ROOT / "tools" / "pixel_art" / "monsters"))
from recolor_monsters import PALETTE, build_mapping, build_tint_mapping, monster_colors, recolor  # noqa: E402

CELL = 64

# nome no jogo: (arquivo do pacote, linha de cor). Linhas: 0 laranja, 1 violeta, 2 azul, 3 verde, 4 âmbar,
# 5 branco, 6 malva, 7 carmim, 8 índigo.
EFFECTS = {
    "eye_orb": ("1781", 7),          # orbe de sangue girando do olho voador (laço)
    "eye_orb_burst": ("230", 7),     # estouro do orbe no impacto
    "spore_orb": ("1781", 3),        # semente/esporo da planta-bulbo (laço)
    "spore_burst": ("485", 3),       # respingo verde no impacto
    "slash_white": ("438", 5),       # corte em meia-lua do goblin
    "spore_cloud": ("476", 6),       # nuvem de esporos do cogumelo
    "bite_red": ("316", 7),          # mordida da planta vermelha
    "claw_red": ("465", 7),          # garras do zumbi
    "dust_slam": ("70", 5),          # poeira da pancada do esqueleto
    "thorn_burst": ("527", 1),       # espinhos violeta que brotam do chão (planta roxa)
}

# Mesma AAP-64 restrita dos monstros (recolor_monsters.PALETTE): sem os neons, o ciano, o rosa e o verde-limão.
# Efeitos tingidos numa rampa pela luminosidade (do escuro ao claro). O violeta do pacote cairia na rampa de sangue
# pelo mapeamento comum (matiz perto do vermelho) e os verdes claros puxariam para o amarelo: espinhos e esporos vão
# direto para a rampa violeta e a verde da AAP-64.
VIOLET = ["141013", "242234", "403353", "5b3138", "793a80", "793a80", "b9bffb"]
GREEN = ["122020", "24523b", "1a7a3e", "14a02e", "5daf8d", "92dcba"]
TINTED = {"thorn_burst": VIOLET, "spore_orb": GREEN, "spore_burst": GREEN}


def extract(packs):
    index = {p.stem: p for p in pathlib.Path(packs).rglob("*.png")}
    SOURCE.mkdir(parents=True, exist_ok=True)
    for name, (file, row) in EFFECTS.items():
        sheet = Image.open(index[file]).convert("RGBA")
        cell = sheet.height // 9
        strip = sheet.crop((0, row * cell, sheet.width, (row + 1) * cell))
        frames = [strip.crop((i * cell, 0, (i + 1) * cell, cell)) for i in range(sheet.width // cell)]
        frames = [f for f in frames if f.getbbox()]
        out = Image.new("RGBA", (len(frames) * cell, cell))
        for i, f in enumerate(frames):
            out.alpha_composite(f, (i * cell, 0))
        out.save(SOURCE / f"{name}.png")
        print(f"recorte {name}: {file} linha {row}, {len(frames)} quadros")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--packs", help="pasta com os pacotes descompactados (recorta de novo para source/)")
    args = parser.parse_args()
    if args.packs:
        extract(args.packs)

    TARGET.mkdir(parents=True, exist_ok=True)
    for name in EFFECTS:
        path = SOURCE / f"{name}.png"
        src = Image.open(path).convert("RGBA")
        colors = monster_colors([path])
        mapping = build_tint_mapping(colors, TINTED[name]) if name in TINTED else build_mapping(colors, PALETTE)
        dst = recolor(src, mapping)
        alpha = np.array(dst)[:, :, 3]
        dst.putalpha(Image.fromarray(np.where(alpha >= 128, 255, 0).astype(np.uint8)))
        dst.save(TARGET / f"{name}.png")
        print(f"{name}: {src.width // CELL} quadros")


if __name__ == "__main__":
    main()
