"""Exporta o kit do Jardim Sangrento: peças em assets/pixel_art/garden/ (mesmo formato do kit da cripta: PNG 224x208 com
o vértice de cima do tile em (112, 116), heights/<peça>.png e manifest.json) e o chão em
assets/pixel_art/tiles/garden_floor/garden_floor_128_<variante>_flat.png (losango 128x64).

Uso: py tools/pixel_art/garden_kit/export_garden.py
"""
import os, sys, json
import numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import garden as G                      # registra a família "hedge" no renderizador antes do export_kit
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import export_kit as EK
from iso import Canvas

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "assets", "pixel_art", "garden")
FLOOR = os.path.join(ROOT, "assets", "pixel_art", "tiles", "garden_floor", "garden_floor_128_")

# Peças maiores que o tile: (largura, altura, vértice de cima do tile x, y).
PARTERRE_CANVAS = (288, 184, 144, 40)      # 2x2 tiles a partir do tile da âncora
CENTER_CANVAS = (224, 240, 112, 148)       # canteiro redondo maior que o tile e topiaria alta
TREE_CANVAS = (224, 240, 112, 148)

FLOOR_VARIANTS = ("base", "dark", "moss", "blood")


def pieces():
    P = {}
    for side in ("ne", "nw"):
        for v in range(3):
            P[f"walls/wall_{side}_plain_{'abc'[v]}"] = ("wall", lambda cv, L, s=side, v=v: G.wall_plain(cv, s, v))
        P[f"walls/wall_{side}_niche"] = ("wall", lambda cv, L, s=side: G.wall_roses(cv, s))
        P[f"walls/wall_{side}_arch"] = ("wall", lambda cv, L, s=side: G.wall_arch(cv, s))
        P[f"walls/wall_{side}_low"] = ("wall", lambda cv, L, s=side: G.wall_plain(cv, s, 0, height=G.LH))
    for side in ("se", "sw"):
        P[f"walls/wall_{side}_low"] = ("wall", lambda cv, L, s=side: G.wall_plain(cv, s))
        P[f"walls/wall_{side}_gap"] = ("wall", lambda cv, L, s=side: G.wall_gap(cv, s))
    for c in ("n", "e", "w", "s", "e_low", "w_tall"):
        P[f"walls/corner_{c}"] = ("wall", lambda cv, L, c=c: G.corner(cv, c))
    for k in range(3):
        P[f"props/hedge_block_{'abc'[k]}"] = ("prop", lambda cv, L, k=k: G.hedge_block(cv, L, k))
    P["props/parterre"] = ("prop", lambda cv, L: G.parterre(cv, L), PARTERRE_CANVAS)
    P["props/topiary_center"] = ("prop", lambda cv, L: G.topiary_center(cv, L), CENTER_CANVAS)
    P["props/topiary_ball"] = ("prop", lambda cv, L: G.topiary_ball(cv, L))
    P["props/rose_bush"] = ("prop", lambda cv, L: G.rose_bush(cv, L))
    P["props/lantern"] = ("prop", lambda cv, L: G.lantern(cv, L))
    P["props/dead_tree_a"] = ("prop", lambda cv, L: G.dead_tree(cv, L, 4), TREE_CANVAS)
    P["props/dead_tree_b"] = ("prop", lambda cv, L: G.dead_tree(cv, L, 9), TREE_CANVAS)
    return P


def export_floor():
    os.makedirs(os.path.dirname(FLOOR), exist_ok=True)
    for i, variant in enumerate(FLOOR_VARIANTS):
        cv = Canvas(128, 64, 64, 0)
        G.floor_tile(variant, i)(cv, [])
        img = cv.render([], ambient=EK.AMBIENT, bg=None)
        img.save(FLOOR + variant + "_flat.png")
        print("floor", variant)


def main():
    man = {"canvas": [EK.CW, EK.CH], "tile_top_vertex": [EK.TX, EK.TY], "tile_center": [EK.TX, EK.TY + 32], "tile_size": [128, 64],
           "godot_texture_origin": [0, EK.TY + 32 - EK.CH // 2], "palette": "AAP-64", "baked_ambient": EK.AMBIENT,
           "floor": {"root": "res://assets/pixel_art/tiles/garden_floor/garden_floor_128_",
                     "variants": list(FLOOR_VARIANTS), "weights": [.55, .2, .17, .08]},
           "pieces": {}}
    for name, (kind, fn, *canvas) in pieces().items():
        canvas = canvas[0] if canvas else None
        img, L, height = EK.render(fn, canvas=canvas)
        path = os.path.join(OUT, name + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        hpath = os.path.join(OUT, "heights", name + ".png")
        os.makedirs(os.path.dirname(hpath), exist_ok=True)
        height.save(hpath)
        man["pieces"][name] = {"kind": kind, "lights": L}
        if canvas:
            man["pieces"][name]["canvas"] = list(canvas[:2])
            man["pieces"][name]["tile_top_vertex"] = list(canvas[2:])
        print(name, len(L))
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1, ensure_ascii=False)
    export_floor()


if __name__ == "__main__":
    main()
