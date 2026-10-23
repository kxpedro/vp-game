"""Exporta o kit da cripta: cada peça vira um PNG 224x208 (paleta AAP-64) alinhado ao tile 128x64.

O vértice de cima do tile fica no pixel (112, 116) e o centro do tile em (112, 148), em todas as peças.
No TileSet do Godot (tile_size 128x64) isso equivale a texture_origin = (0, 44).
Gera também manifest.json com o tipo de cada peça e as luzes (velas, arandelas) em pixels do PNG.

Uso: py tools/pixel_art/crypt_kit/export_kit.py [pasta_de_saida]
"""
import os, sys, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso import Canvas
import walls as W
import props as Pr

CW, CH, TX, TY = 224, 208, 112, 116
AMBIENT = -.6          # luz base "assada" (sem velas); a luz das velas fica para o jogo

def render(draw, X0=0, Y0=0):
    cv = Canvas(CW, CH, TX - (X0 - Y0), TY - (X0 + Y0) / 2)
    lights = []
    draw(cv, lights)
    img = cv.render([], ambient=AMBIENT, bg=None)
    L = []
    for (lx, ly, lz, power, rad) in lights:
        sx, sy = cv.proj(np.array([lx]), np.array([ly]), np.array([lz]))
        L.append({"x": int(sx[0]), "y": int(sy[0]), "power": round(power, 2), "radius": round(rad * 1.0, 1)})
    return img, L

def pieces():
    P = {}
    for side in ("ne", "nw"):
        for v in range(3):
            P[f"walls/wall_{side}_plain_{'abc'[v]}"] = ("wall", lambda cv, L, s=side, v=v: W.wall_plain(cv, s, v))
        P[f"walls/wall_{side}_niche"] = ("wall", lambda cv, L, s=side: W.wall_niche(cv, s))
        P[f"walls/wall_{side}_arch"] = ("wall", lambda cv, L, s=side: W.wall_arch(cv, s))
        P[f"walls/wall_{side}_low"] = ("wall", lambda cv, L, s=side: W.wall_plain(cv, s, 0, height=W.FH))
    for side in ("se", "sw"):
        P[f"walls/wall_{side}_low"] = ("wall", lambda cv, L, s=side: W.wall_plain(cv, s))
        P[f"walls/wall_{side}_gap"] = ("wall", lambda cv, L, s=side: W.wall_gap(cv, s))
    for c in ("n", "e", "w", "s", "e_low", "w_tall"):
        P[f"walls/corner_{c}"] = ("wall", lambda cv, L, c=c: W.corner(cv, c))
    for ax, nm in (("y", "sw"), ("x", "se")):
        P[f"props/coffin_virgil_{nm}"] = ("prop", lambda cv, L, a=ax: Pr.coffin_virgil(cv, a, L))
        for st in ("closed", "broken"):
            P[f"props/coffin_stone_{st}_{nm}"] = ("prop", lambda cv, L, a=ax, s=st: Pr.coffin_stone(cv, a, s, L))
        for st in ("closed", "open", "broken"):
            P[f"props/coffin_wood_{st}_{nm}"] = ("prop", lambda cv, L, a=ax, s=st: Pr.coffin_wood(cv, a, s, L, 3))
        P[f"props/carpet_{nm}"] = ("decal", lambda cv, L, a=ax: Pr.carpet_runner(cv, a, L))
    for i, (sz, seed) in enumerate([("small", 1), ("small", 2), ("medium", 3), ("medium", 4), ("large", 5), ("large", 6)]):
        P[f"props/candles_{sz}_{'ab'[i % 2]}"] = ("prop", lambda cv, L, sz=sz, seed=seed: Pr.candles(cv, sz, seed, L))
    P["props/candle_tall"] = ("prop", lambda cv, L: Pr.candle_tall(cv, L))
    P["props/sconce_ne"] = ("wall_prop", lambda cv, L: Pr.sconce(cv, "ne", L))
    P["props/sconce_nw"] = ("wall_prop", lambda cv, L: Pr.sconce(cv, "nw", L))
    P["props/bones"] = ("decal", lambda cv, L: Pr.bones(cv, L))
    P["props/rubble_a"] = ("prop", lambda cv, L: Pr.rubble(cv, 7, L))
    P["props/rubble_b"] = ("prop", lambda cv, L: Pr.rubble(cv, 8, L))
    return P

def main(out):
    man = {"canvas": [CW, CH], "tile_top_vertex": [TX, TY], "tile_center": [TX, TY + 32], "tile_size": [128, 64],
           "godot_texture_origin": [0, TY + 32 - CH // 2], "palette": "AAP-64", "baked_ambient": AMBIENT, "pieces": {}}
    for name, (kind, fn) in pieces().items():
        img, L = render(fn)
        path = os.path.join(out, name + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        man["pieces"][name] = {"kind": kind, "lights": L}
        print(name, len(L))
    with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1, ensure_ascii=False)

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "..", "..", "assets", "pixel_art", "crypt"))
