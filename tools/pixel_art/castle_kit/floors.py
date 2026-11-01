"""Andares do castelo (mapa 3): plantas com várias salas e corredores, para as salas do mapa 3 serem andares inteiros.

Cada andar = retângulos de chão (salas e corredores de 1 a 2 tiles) com vazio entre eles; as paredes saem sozinhas nas
bordas sem vizinho. Props por (nome, x, y). Uso: py floors.py <pasta_do_kit> <pasta_de_saida>
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import castle  # noqa: F401
sys.path.insert(0, os.path.join(HERE, "..", "crypt_kit"))
import build_room as BR
from preview_rooms_lib import write


def R(x0, y0, w, h): return {(x, y) for x in range(x0, x0 + w) for y in range(y0, y0 + h)}


def cols(xs, ys, name="column"): return [(name, x, y) for x in xs for y in ys]


def aposentos():
    """Andar dos Aposentos: salão central de colunas, biblioteca a oeste, quarto e armaria a leste, saguão ao sul"""
    t = R(8, 4, 9, 9) | R(1, 3, 6, 7) | R(19, 2, 6, 5) | R(19, 9, 6, 6) | R(6, 15, 14, 4)
    t |= R(7, 6, 1, 2) | R(17, 7, 2, 2) | R(21, 7, 2, 2) | R(12, 13, 2, 2) | R(12, 0, 1, 4) | R(3, 10, 1, 5) | R(3, 15, 3, 1)
    p = [("carpet_sw", 12, y) for y in range(4, 19)] + cols((10, 14), (6, 8, 10)) + [("brazier", 9, 5), ("brazier", 15, 5), ("armor", 9, 11), ("armor", 15, 11)]
    p += [("bookcase_x", x, 3) for x in (1, 2, 4, 5)] + [("bookcase_y", 1, y) for y in (5, 6, 8)] + [("table", 3, 6), ("candelabrum", 5, 8)]
    p += [("bed", 20, 3), ("bed", 23, 3), ("candelabrum", 22, 5), ("armor", 20, 10), ("armor", 23, 10), ("brazier", 21, 13), ("rubble_a", 24, 12)]
    p += [("brazier", 7, 16), ("brazier", 18, 16), ("silver_pool", 12, 17), ("candelabrum", 3, 13)]
    e = {(12, 0, "ne"): "arch", (12, 4, "ne"): "none"}
    for (x, y) in ((3, 3), (5, 3), (9, 4), (15, 4), (21, 2), (23, 2)): e[(x, y, "ne")] = "niche"
    return t, e, p


def salao_do_trono():
    """Andar do Salão: nave longa em cruz, braços laterais, corredor leste-oeste e antecâmara"""
    t = R(9, 2, 7, 15) | R(2, 7, 7, 5) | R(16, 7, 7, 5) | R(11, 17, 3, 3) | R(0, 8, 2, 3) | R(23, 8, 2, 3)
    t |= R(10, 0, 5, 2)
    p = [("carpet_sw", 12, y) for y in range(0, 20)] + cols((10, 14), (4, 6, 9, 11, 13, 15)) + [("brazier", 10, 2), ("brazier", 14, 2)]
    p += [("armor", 3, 7), ("armor", 7, 7), ("armor", 17, 7), ("armor", 21, 7), ("table", 4, 9), ("table", 19, 9)]
    p += [("candelabrum", 3, 11), ("candelabrum", 21, 11), ("silver_pool", 12, 9), ("rubble_b", 24, 9), ("brazier", 11, 18), ("brazier", 13, 18)]
    e = {(12, 0, "ne"): "arch", (11, 0, "ne"): "niche", (13, 0, "ne"): "niche"}
    for y in (3, 15): e[(9, y, "nw")] = "niche"
    return t, e, p


def torres():
    """Andar das Torres: quatro torres nos cantos ligadas por galerias em anel em volta de um pátio vazado"""
    t = R(0, 0, 6, 6) | R(16, 0, 6, 6) | R(0, 14, 6, 6) | R(16, 14, 6, 6)
    t |= R(6, 2, 10, 2) | R(6, 16, 10, 2) | R(2, 6, 2, 8) | R(18, 6, 2, 8) | R(9, 8, 4, 4) | R(10, 4, 2, 4) | R(10, 12, 2, 4)
    p = [("brazier", 1, 1), ("brazier", 20, 1), ("brazier", 1, 18), ("brazier", 20, 18)]
    p += [("column", x, y) for (x, y) in ((8, 2), (13, 2), (8, 17), (13, 17), (2, 8), (2, 11), (19, 8), (19, 11))]
    p += [("bookcase_x", 3, 0), ("bookcase_x", 4, 0), ("bed", 17, 1), ("table", 17, 4), ("armor", 3, 15), ("armor", 5, 15), ("candelabrum", 18, 16)]
    p += [("silver_pool", 10, 9), ("armor", 9, 8), ("armor", 12, 8), ("rubble_a", 5, 5), ("candelabrum", 11, 6)]
    e = {(3, 0, "ne"): "arch"}
    for x in (1, 18): e[(x, 0, "ne")] = "niche"
    return t, e, p


if __name__ == "__main__":
    kit, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    BR.KIT = kit
    BR.FLOOR = os.path.join(kit, "tiles", "castle_floor", "castle_floor_128_")
    BR.FLOOR_VARIANTS = (["base", "dark", "cracked", "ember", "inlay"], [.46, .2, .14, .12, .08])
    for name, fn in (("andar_aposentos", aposentos), ("andar_salao", salao_do_trono), ("andar_torres", torres)):
        t, e, p = fn()
        txt = os.path.join(out, name + ".txt"); write(txt, t, e, p)
        BR.build(txt, os.path.join(out, name + ".png")); print(name)
