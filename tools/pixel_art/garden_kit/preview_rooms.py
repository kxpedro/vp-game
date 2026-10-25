"""Prévia das salas do Jardim Sangrento montadas com as peças exportadas (mesma montagem do jogo).

Gera as duas formas de sala comum do mapa 2, com a mesma regra do RoomGenerator (BuildGardenMap) e do
PixelArenaLayout.BuildGarden: "clareira" (jardim formal) e "corredores" (labirinto com clareira no meio).

Uso: py tools/pixel_art/garden_kit/preview_rooms.py <pasta_de_saida> [semente]
"""
import os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import garden  # noqa: F401  (registra a família hedge antes do build_room montar a tabela de cores)
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import build_room as BR

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
BR.KIT = os.path.join(ROOT, "assets", "pixel_art", "garden")
BR.FLOOR = os.path.join(ROOT, "assets", "pixel_art", "tiles", "garden_floor", "garden_floor_128_")
BR.FLOOR_VARIANTS = (["base", "dark", "moss", "blood"], [.55, .2, .17, .08])


def clearing(seed):
    """clareira 11x11: canteiros 2x2 nos quatro quadrantes, topiaria no centro, lanternas nos cantos, saída no fundo"""
    n = 11
    tiles = {(x, y) for x in range(n) for y in range(n)}
    props = [("parterre", 2, 2), ("parterre", 7, 2), ("parterre", 2, 7), ("parterre", 7, 7), ("topiary_center", 5, 5)]
    props += [("lantern", 1, 1), ("lantern", 9, 1), ("lantern", 1, 9), ("lantern", 9, 9)]
    props += [("topiary_ball", 4, 4), ("topiary_ball", 6, 4), ("topiary_ball", 4, 6), ("topiary_ball", 6, 6)]
    edges = {(4, 0, "ne"): "arch", (5, 0, "ne"): "arch", (6, 0, "ne"): "arch", (0, 3, "nw"): "niche", (0, 7, "nw"): "niche"}
    return tiles, set(), edges, props


def corridors(seed, n=8):
    """labirinto de macrocélulas 2x2 tiles (corredor de 1 tile e sebe de 1), alguns laços e clareira 5x5 no meio"""
    rng = np.random.default_rng(seed)
    S = 2 * n + 1
    wall = np.zeros((S, S), bool)
    wall[::2, :] = True; wall[:, ::2] = True
    seen = np.zeros((n, n), bool); stack = [(0, 0)]; seen[0, 0] = True
    while stack:
        i, j = stack[-1]
        nb = [(i + a, j + b) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)) if 0 <= i + a < n and 0 <= j + b < n and not seen[i + a, j + b]]
        if not nb: stack.pop(); continue
        a, b = nb[rng.integers(len(nb))]
        if a != i: wall[2 * max(a, i), 2 * j + 1] = False
        else: wall[2 * i + 1, 2 * max(b, j)] = False
        seen[a, b] = True; stack.append((a, b))
    for i in range(1, n):
        for j in range(n):
            if rng.random() < .1: wall[2 * i, 2 * j + 1] = False
            if rng.random() < .1: wall[2 * j + 1, 2 * i] = False
    c0, c1 = n - 2, n + 2                       # clareira de combate no meio (5x5 tiles)
    wall[c0:c1 + 1, c0:c1 + 1] = False
    k = n // 2 + int(rng.integers(0, 2))        # saída no fundo: abre a sebe da 1ª fileira na coluna 2k
    wall[1, 2 * k] = False
    tiles, hedges = set(), set()
    for gy in range(1, S - 1):
        for gx in range(1, S - 1):
            tiles.add((gx, gy))
            if wall[gy, gx]: hedges.add((gx, gy))
    mid = n
    props = [("hedge_block_" + "abc"[(x * 7 + y * 3) % 3], x, y) for (x, y) in sorted(hedges)]
    props += [("topiary_center", mid, mid), ("lantern", c0, c0), ("lantern", c1, c0), ("lantern", c0, c1), ("lantern", c1, c1)]
    for (x, y) in sorted(tiles - hedges):
        if c0 <= x <= c1 and c0 <= y <= c1: continue
        free = sum((x + a, y + b) in tiles and (x + a, y + b) not in hedges for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if free == 1 and rng.random() < .6:
            props.append(("dead_tree_" + "ab"[int(rng.integers(0, 2))] if rng.random() < .5 else "rose_bush", x, y))
    edges = {(2 * k - 1, 1, "ne"): "arch", (2 * k, 1, "ne"): "arch", (2 * k + 1, 1, "ne"): "arch"}
    return tiles, hedges, edges, props


def write(path, tiles, edges, props, ambient=-.6):
    xs = [x for x, _ in tiles]; ys = [y for _, y in tiles]
    with open(path, "w", encoding="utf-8") as f:
        f.write("[grid]\n")
        for y in range(0, max(ys) + 1):
            f.write("".join("." if (x, y) in tiles else " " for x in range(0, max(xs) + 1)) + "\n")
        f.write("[edges]\n")
        for (x, y, side), kind in edges.items():
            f.write(f"{kind} {side} {x} {y}\n")
        f.write("[props]\n")
        for p in props:
            f.write(" ".join(str(v) for v in p) + "\n")
        f.write(f"[settings]\nambient {ambient}\nseed 3\n")


if __name__ == "__main__":
    out = sys.argv[1]
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 7
    os.makedirs(out, exist_ok=True)
    for name, fn in (("clareira", clearing), ("corredores", corridors)):
        tiles, hedges, edges, props = fn(seed)
        txt = os.path.join(out, name + ".txt")
        write(txt, tiles, edges, props)
        BR.build(txt, os.path.join(out, name + ".png"))
        print(name)
