"""Exporta o kit da Ala Superior do Castelo (mapa 3) para uma pasta (PNG 224x208 alinhado ao tile 128x64, alturas,
manifest.json e o chão em tiles/castle_floor). Mesmo formato dos kits da cripta e do Jardim.

Uso: py tools/pixel_art/castle_kit/export_castle.py <pasta_de_saida>
"""
import os, sys, json
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import castle as C
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import export_kit as EK
from iso import Canvas

FLOOR_VARIANTS = ("base", "dark", "cracked", "ember", "inlay")
WEIGHTS = [.46, .2, .14, .12, .08]


def pieces():
    P = {}
    for side in ("ne", "nw"):
        for v in range(3):
            P[f"walls/wall_{side}_plain_{'abc'[v]}"] = ("wall", lambda cv, L, s=side, v=v: C.wall_plain(cv, s, v))
        P[f"walls/wall_{side}_niche"] = ("wall", lambda cv, L, s=side: C.wall_window(cv, s))
        P[f"walls/wall_{side}_arch"] = ("wall", lambda cv, L, s=side: C.wall_arch(cv, s))
        P[f"walls/wall_{side}_low"] = ("wall", lambda cv, L, s=side: C.wall_plain(cv, s, 0, height=C.LH))
    for side in ("se", "sw"):
        P[f"walls/wall_{side}_low"] = ("wall", lambda cv, L, s=side: C.wall_plain(cv, s))
        P[f"walls/wall_{side}_gap"] = ("wall", lambda cv, L, s=side: C.wall_gap(cv, s))
    for c in ("n", "e", "w", "s", "e_low", "w_tall"):
        P[f"walls/corner_{c}"] = ("wall", lambda cv, L, c=c: C.corner(cv, c))
    P["props/column"] = ("prop", C.column)
    P["props/brazier"] = ("prop", C.brazier)
    P["props/armor"] = ("prop", C.armor)
    P["props/candelabrum"] = ("prop", C.candelabrum)
    P["props/rubble_a"] = ("prop", lambda cv, L: C.rubble(cv, L, 3))
    P["props/rubble_b"] = ("prop", lambda cv, L: C.rubble(cv, L, 4))
    P["props/bookcase_x"] = ("prop", lambda cv, L: C.bookcase(cv, L, "x"))
    P["props/bookcase_y"] = ("prop", lambda cv, L: C.bookcase(cv, L, "y"))
    P["props/table"] = ("prop", C.table)
    P["props/bed"] = ("prop", C.bed)
    P["props/silver_pool"] = ("decal", C.silver_pool)
    P["props/carpet_sw"] = ("decal", C.carpet("y"))
    P["props/carpet_se"] = ("decal", C.carpet("x"))
    return P


def main(out):
    floor_root = os.path.join(out, "tiles", "castle_floor", "castle_floor_128_")
    man = {"canvas": [EK.CW, EK.CH], "tile_top_vertex": [EK.TX, EK.TY], "tile_center": [EK.TX, EK.TY + 32], "tile_size": [128, 64],
           "godot_texture_origin": [0, EK.TY + 32 - EK.CH // 2], "palette": "AAP-64", "baked_ambient": EK.AMBIENT,
           "floor": {"variants": list(FLOOR_VARIANTS), "weights": WEIGHTS}, "pieces": {}}
    for name, (kind, fn, *canvas) in pieces().items():
        canvas = canvas[0] if canvas else None
        img, L, height = EK.render(fn, canvas=canvas)
        path = os.path.join(out, name + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True); img.save(path)
        hpath = os.path.join(out, "heights", name + ".png")
        os.makedirs(os.path.dirname(hpath), exist_ok=True); height.save(hpath)
        man["pieces"][name] = {"kind": kind, "lights": L}
        print(name, len(L))
    with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1, ensure_ascii=False)
    os.makedirs(os.path.dirname(floor_root), exist_ok=True)
    for i, variant in enumerate(FLOOR_VARIANTS):
        cv = Canvas(128, 64, 64, 0)
        C.floor_tile(variant, i)(cv, [])
        cv.render([], ambient=EK.AMBIENT, bg=None).save(floor_root + variant + "_flat.png")


if __name__ == "__main__":
    main(sys.argv[1])
