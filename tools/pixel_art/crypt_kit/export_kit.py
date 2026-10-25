"""Exporta o kit da cripta: cada peça vira um PNG 224x208 (paleta AAP-64) alinhado ao tile 128x64.

O vértice de cima do tile fica no pixel (112, 116) e o centro do tile em (112, 148), em todas as peças.
No TileSet do Godot (tile_size 128x64) isso equivale a texture_origin = (0, 44).
Gera também manifest.json com o tipo de cada peça e as luzes (velas, arandelas) em pixels do PNG,
(as peças com tela própria levam "canvas" e "tile_top_vertex"; as animadas, "frames" = quadros <peça>_1..N-1,
e "manual" quando o jogo troca os quadros à mão em vez de tocar em laço)
e heights/<peça>.png: altura (z do mundo) de cada pixel, x100 em 16 bits (R = byte alto, G = byte baixo),
usada pelo modo 2D do jogo para escrever a profundidade de cada pixel (PixelRoomComposer.cs).

Uso: py tools/pixel_art/crypt_kit/export_kit.py [pasta_de_saida]
"""
import os, sys, json
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iso import Canvas
import walls as W
import props as Pr

CW, CH, TX, TY = 224, 208, 112, 116
GATE_FRAMES, GATE_LIFT = 8, 53.0   # quadros da grade e quanto ela sobe até sair do vão (topo do arco = 52)
AMBIENT = -.6          # luz base "assada" (sem velas); a luz das velas fica para o jogo

# Peças maiores que o tile padrão: tela própria (largura, altura, vértice de cima do tile x, y), gravada no manifest.
BENCH_CANVAS = (224, 208, 84, 116)

def render(draw, X0=0, Y0=0, canvas=None):
    cw, ch, tx, ty = canvas or (CW, CH, TX, TY)
    cv = Canvas(cw, ch, tx - (X0 - Y0), ty - (X0 + Y0) / 2)
    lights = []
    draw(cv, lights)
    img = cv.render([], ambient=AMBIENT, bg=None)
    v = np.where(cv.fam >= 0, np.clip(np.round(cv.wz * 100), 0, 65535), 0).astype(np.uint32)
    height = Image.fromarray(np.dstack([(v >> 8).astype(np.uint8), (v & 255).astype(np.uint8), np.zeros_like(v, np.uint8)]), "RGB")
    L = []
    for (lx, ly, lz, power, rad) in lights:
        sx, sy = cv.proj(np.array([lx]), np.array([ly]), np.array([lz]))
        L.append({"x": int(sx[0]), "y": int(sy[0]), "power": round(power, 2), "radius": round(rad * 1.0, 1)})
    return img, L, height

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
        # Caixão do Virgil ao renascer: o quadro 0 (sem tampa) entra no layout no lugar do de sempre e os quadros
        # 1..N-1 são a tampa, da fechada à entreaberta. O jogo troca os quadros à mão (CoffinRise.cs), sem laço.
        P[f"props/coffin_virgil_rise_{nm}"] = ("prop", lambda cv, L, a=ax: Pr.coffin_virgil(cv, a, L, lid=None))
        for k, lid in enumerate(Pr.VIRGIL_LID_FRAMES, 1):
            P[f"props/coffin_virgil_rise_{nm}_{k}"] = ("frame", lambda cv, L, a=ax, lid=lid: Pr.coffin_virgil(cv, a, L, lid=lid))
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
    # Grade do portão do início da run (vão do wall_<lado>_arch), em quadros da fechada (0) à aberta.
    # Não entra em layout: o jogo a desenha por cima da sala e troca o quadro ao abrir (RunStartGate.cs).
    for side in ("ne", "nw"):
        for k in range(GATE_FRAMES):
            P[f"props/portcullis_{side}_{k}"] = ("overlay", lambda cv, L, s=side, k=k: W.portcullis(cv, s, GATE_LIFT * k / (GATE_FRAMES - 1)))
    # Grade da frente (vão wall_<lado>_gap das paredes cortadas): mesmos quadros, cortada na altura das ombreiras.
    for side in ("se", "sw"):
        for k in range(GATE_FRAMES):
            P[f"props/portcullis_{side}_{k}"] = ("overlay", lambda cv, L, s=side, k=k: W.portcullis_front(cv, s, (W.GATE_FRONT_TOP + 1) * k / (GATE_FRAMES - 1)))
    # Fonte de sangue animada: o quadro 0 entra no layout como peça comum (com "frames" no manifest) e os quadros
    # 1..N-1 (tipo "frame") são trocados pelo jogo por cima da sala (PixelRoomComposer + PixelRoom).
    P["props/blood_fountain"] = ("prop", lambda cv, L: Pr.blood_fountain(cv, L, 0.0))
    for k in range(1, Pr.FOUNTAIN_FRAMES):
        P[f"props/blood_fountain_{k}"] = ("frame", lambda cv, L, k=k: Pr.blood_fountain(cv, L, k / Pr.FOUNTAIN_FRAMES))
    # Bebedouro de sangue da sala da primeira herança (mesma ideia: quadro 0 no layout, quadros 1..N-1 trocados).
    P["props/blood_basin"] = ("prop", lambda cv, L: Pr.blood_basin(cv, L, 0.0))
    for k in range(1, Pr.FOUNTAIN_FRAMES):
        P[f"props/blood_basin_{k}"] = ("frame", lambda cv, L, k=k: Pr.blood_basin(cv, L, k / Pr.FOUNTAIN_FRAMES))
    P["props/weapon_bench"] = ("prop", lambda cv, L: Pr.weapon_bench(cv, L), BENCH_CANVAS)
    # Câmara de Hemomancia: altar do sacrifício (braço oeste) e bancada de artefatos (parede NE do braço leste).
    P["props/sacrifice_altar"] = ("prop", lambda cv, L: Pr.sacrifice_altar(cv, L))
    P["props/artifact_counter"] = ("prop", lambda cv, L: Pr.artifact_counter(cv, L), Pr.ARTIFACT_COUNTER_CANVAS)
    return P

FRAMES = {"props/blood_fountain": Pr.FOUNTAIN_FRAMES, "props/blood_basin": Pr.FOUNTAIN_FRAMES}
FRAMES.update({f"props/coffin_virgil_rise_{nm}": len(Pr.VIRGIL_LID_FRAMES) + 1 for nm in ("sw", "se")})
# Peças animadas tocadas pelo jogo (quadro a quadro, sem laço), não pelo relógio da sala.
MANUAL = {f"props/coffin_virgil_rise_{nm}" for nm in ("sw", "se")}

def main(out):
    man = {"canvas": [CW, CH], "tile_top_vertex": [TX, TY], "tile_center": [TX, TY + 32], "tile_size": [128, 64],
           "godot_texture_origin": [0, TY + 32 - CH // 2], "palette": "AAP-64", "baked_ambient": AMBIENT, "pieces": {}}
    for name, (kind, fn, *canvas) in pieces().items():
        canvas = canvas[0] if canvas else None
        img, L, height = render(fn, canvas=canvas)
        path = os.path.join(out, name + ".png")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        hpath = os.path.join(out, "heights", name + ".png")
        os.makedirs(os.path.dirname(hpath), exist_ok=True)
        height.save(hpath)
        man["pieces"][name] = {"kind": kind, "lights": L}
        if canvas:
            man["pieces"][name]["canvas"] = list(canvas[:2])
            man["pieces"][name]["tile_top_vertex"] = list(canvas[2:])
        if name in FRAMES:
            man["pieces"][name]["frames"] = FRAMES[name]
        if name in MANUAL:
            man["pieces"][name]["manual"] = True
        print(name, len(L))
    with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1, ensure_ascii=False)

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "..", "..", "assets", "pixel_art", "crypt"))
