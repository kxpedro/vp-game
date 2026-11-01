"""Prévias do kit do castelo: folha de peças e duas salas comuns do mapa 3 (salão de colunas e galeria).

Uso: py tools/pixel_art/castle_kit/preview_rooms.py <pasta_do_kit_exportado> <pasta_de_saida>
"""
import os, sys, json
import numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import castle  # noqa: F401  (registra as famílias novas antes do build_room montar a tabela de cores)
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import build_room as BR

kit, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
BR.KIT = kit
BR.FLOOR = os.path.join(kit, "tiles", "castle_floor", "castle_floor_128_")
BR.FLOOR_VARIANTS = (["base", "dark", "cracked", "ember", "inlay"], [.46, .2, .14, .12, .08])


def write(path, tiles, edges, props, ambient=-.6, seed=3):
    xs = [x for x, _ in tiles]; ys = [y for _, y in tiles]
    with open(path, "w", encoding="utf-8") as f:
        f.write("[grid]\n")
        for y in range(0, max(ys) + 1):
            f.write("".join("." if (x, y) in tiles else " " for x in range(0, max(xs) + 1)) + "\n")
        f.write("[edges]\n")
        for (x, y, side), kind in edges.items(): f.write(f"{kind} {side} {x} {y}\n")
        f.write("[props]\n")
        for p in props: f.write(" ".join(str(v) for v in p) + "\n")
        f.write(f"[settings]\nambient {ambient}\nseed {seed}\n")


def hall():
    """salão de colunas 11x9: duas fileiras de colunas, tapete no eixo, braseiros nos cantos, vitrais na parede do fundo"""
    W, H = 11, 9
    tiles = {(x, y) for x in range(W) for y in range(H)}
    edges = {(5, 0, "ne"): "arch"}
    for x in (1, 3, 7, 9): edges[(x, 0, "ne")] = "niche"
    for y in (2, 6): edges[(0, y, "nw")] = "niche"
    props = [("carpet_sw", 5, y) for y in range(0, H)]
    for y in (2, 4, 6):
        props += [("column", 3, y), ("column", 7, y)]
    props += [("brazier", 1, 1), ("brazier", 9, 1), ("brazier", 1, 7), ("brazier", 9, 7)]
    props += [("armor", 2, 0), ("armor", 8, 0), ("candelabrum", 4, 8), ("candelabrum", 6, 8)]
    props += [("silver_pool", 6, 5), ("rubble_a", 1, 4), ("rubble_b", 9, 3)]
    return tiles, edges, props


def gallery():
    """galeria 15x7: vitrais de um lado, estátuas de armadura no outro, tapete no sentido do comprimento"""
    W, H = 15, 7
    tiles = {(x, y) for x in range(W) for y in range(H)}
    edges = {(13, 3, "se"): "gap"}
    for x in (2, 5, 9, 12): edges[(x, 0, "ne")] = "niche"
    edges[(7, 0, "ne")] = "arch"
    props = [("carpet_se", x, 3) for x in range(0, W)]
    for x in (2, 5, 9, 12): props.append(("armor", x, 6))
    for x in (4, 10): props += [("column", x, 1), ("column", x, 5)]
    props += [("brazier", 0, 0), ("brazier", 14, 0), ("candelabrum", 7, 5), ("silver_pool", 7, 3), ("silver_pool", 11, 4), ("rubble_a", 2, 4)]
    return tiles, edges, props


for name, fn in (("salao", hall), ("galeria", gallery)):
    tiles, edges, props = fn()
    txt = os.path.join(out, name + ".txt")
    write(txt, tiles, edges, props)
    BR.build(txt, os.path.join(out, name + ".png"))
    print(name)

# folha de peças (peças soltas, fundo escuro, 2x)
man = json.load(open(os.path.join(kit, "manifest.json"), encoding="utf-8"))
sel = ["walls/wall_ne_plain_a", "walls/wall_ne_plain_b", "walls/wall_ne_plain_c", "walls/wall_ne_niche", "walls/wall_ne_arch",
       "props/column", "props/brazier", "props/armor", "props/candelabrum", "props/silver_pool", "props/carpet_sw", "props/rubble_a"]
cols = 6; cw, ch = 224, 208
sheet = Image.new("RGBA", (cols * cw, ((len(sel) + cols - 1) // cols) * ch), (6, 6, 8, 255))
for i, n in enumerate(sel):
    im = Image.open(os.path.join(kit, n + ".png")).convert("RGBA")
    sheet.alpha_composite(im, ((i % cols) * cw, (i // cols) * ch))
sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
sheet.convert("RGB").save(os.path.join(out, "pecas.png"))
# chão: 5 variantes lado a lado
fl = [Image.open(BR.FLOOR + v + "_flat.png").convert("RGBA") for v in BR.FLOOR_VARIANTS[0]]
fs = Image.new("RGBA", (128 * len(fl) + 8 * (len(fl) - 1), 64), (6, 6, 8, 255))
for i, im in enumerate(fl): fs.alpha_composite(im, (i * 136, 0))
fs.resize((fs.width * 3, fs.height * 3), Image.NEAREST).convert("RGB").save(os.path.join(out, "chao.png"))
