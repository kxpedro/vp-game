"""Gera layouts/hub_completo.txt a partir da planta baixa (só peças do kit)."""
import os
N = 31
grid = [[" "] * N for _ in range(N)]
def fill(c0, c1, r0, r1, ch="."):
    for r in range(r0, r1 + 1):
        for c in range(c0, c1 + 1): grid[r][c] = ch
A = (14, 16)                       # eixo de 3 tiles (corredores)
fill(11, 19, 11, 19)               # centro (fonte)
fill(11, 19, 3, 6); fill(11, 19, 24, 27)          # sala de cima / sala do caixão (baixo)
fill(3, 6, 11, 19); fill(24, 27, 11, 19)          # sala da esquerda / direita
fill(*A, 7, 10); fill(*A, 20, 23)                 # corredores verticais internos
fill(7, 10, *A); fill(20, 23, *A)                 # corredores horizontais internos
fill(*A, 0, 2, ","); fill(*A, 28, 30, ",")        # saídas que somem no escuro
fill(0, 2, *A, ","); fill(28, 30, *A, ",")
for (c0, c1, r0, r1) in [(14, 16, 2, 2), (14, 16, 28, 28)]: fill(c0, c1, r0, r1, ";")
for (c0, c1, r0, r1) in [(2, 2, 14, 16), (28, 28, 14, 16)]: fill(c0, c1, r0, r1, ";")
edges = []
def back_y(row):   # sala abaixo do corredor: parede NE nos tiles (14..16, row)
    edges.extend([f"plain ne 14 {row}", f"arch ne 15 {row}", f"plain ne 16 {row}"])
def front_y(row):  # corredor abaixo da sala: parede SW nos tiles (14..16, row)
    edges.extend([f"low sw 14 {row}", f"gap sw 15 {row}", f"low sw 16 {row}"])
def back_x(col):
    edges.extend([f"plain nw {col} 14", f"arch nw {col} 15", f"plain nw {col} 16"])
def front_x(col):
    edges.extend([f"low se {col} 14", f"gap se {col} 15", f"low se {col} 16"])
back_y(3); front_y(6); back_y(11); front_y(19); back_y(24); front_y(27)
back_x(3); front_x(6); back_x(11); front_x(19); back_x(24); front_x(27)
for c in (14, 15, 16): edges += [f"none ne {c} 0", f"none sw {c} 30"]
for r in (14, 15, 16): edges += [f"none nw 0 {r}", f"none se 30 {r}"]
# nichos nas paredes do fundo das salas
for c in (12, 18): edges += [f"niche ne {c} 3", f"niche ne {c} 11", f"niche ne {c} 24"]
for r in (12, 18): edges += [f"niche nw 3 {r}", f"niche nw 11 {r}", f"niche nw 24 {r}"]
for r in (12, 18): edges += [f"niche nw 0 {r}"] if False else []
props = []
# --- ala do caixão do Virgil (mesmos objetos da ala aprovada, deslocados +11 col, +18 linha) ---
wing = """carpet_sw 4 6
carpet_sw 4 7
coffin_virgil_sw 4 8 0 8
coffin_wood_closed_sw 0 6 18 42
coffin_stone_closed_sw 1 6 46 40
coffin_wood_broken_sw 2 6 42 40
coffin_stone_broken_sw 6 6 16 40
coffin_wood_closed_sw 7 6 14 40
coffin_wood_open_sw 8 6 8 40
coffin_wood_closed_se 0 8 16 6
coffin_stone_closed_se 0 9 20 10
coffin_wood_broken_se 8 9 8 10
candles_large_a 0 6 -14 -18
candles_large_b 8 6 18 -18
candles_large_a 0 9 -14 20
candles_large_b 8 9 18 18
candles_medium_a 3 6 -2 -18
candles_medium_b 5 6 2 -18
candles_small_a 1 6 0 -20
candles_small_b 6 6 30 -20
candles_small_a 0 7 -20 10
candles_medium_a 3 8 6 -8
candles_medium_b 5 8 -10 0
candles_small_a 3 9 30 20
candles_small_b 5 9 -26 30
sconce_ne 3 6 0 0
sconce_ne 5 6 0 0
sconce_nw 0 7 0 0
bones 2 8
bones 6 9
rubble_a 1 9
rubble_b 7 7"""
for line in wing.splitlines():
    t = line.split(); t[1] = str(int(t[1]) + 11); t[2] = str(int(t[2]) + 18)
    if t[0] == "sconce_ne" and t[2] == "24": pass
    props.append(" ".join(t))
# tapete do centro até a ala do caixão
for r in range(20, 24): props.append(f"carpet_sw 15 {r}")
# --- centro: lugar da fonte marcado por um anel de velas (a fonte não faz parte do kit) ---
import math
for k in range(16):
    a = k / 16 * 2 * math.pi
    wx, wy = 15.5 * 64 + math.cos(a) * 150, 15.5 * 64 + math.sin(a) * 150
    gx, gy = int(wx // 64), int(wy // 64)
    props.append(f"candles_{'small' if k % 2 else 'medium'}_{'ab'[k % 2]} {gx} {gy} {wx - gx * 64 - 32:.0f} {wy - gy * 64 - 32:.0f}")
for (c, r, dx, dy) in [(11, 11, -14, -16), (19, 11, 16, -16), (11, 19, -14, 18), (19, 19, 16, 18)]:
    props.append(f"candles_large_{'ab'[(c + r) % 2]} {c} {r} {dx} {dy}")
for (gx, gy, name) in [(13, 11, "sconce_ne"), (17, 11, "sconce_ne"), (11, 13, "sconce_nw"), (11, 17, "sconce_nw")]:
    props.append(f"{name} {gx} {gy}")
props += ["candle_tall 13 13 0 0", "candle_tall 17 13 0 0", "candle_tall 13 17 0 0", "candle_tall 17 17 0 0"]
# --- salas de cima, esquerda e direita (futuras salas de recursos/NPCs): só ambientação ---
def room_decor(c0, c1, r0, r1, axis_wall, seed):
    out = [f"candles_large_a {c0} {r0} -14 -16", f"candles_large_b {c1} {r0} 16 -16",
           f"candles_medium_a {c0} {r1} -14 18", f"candles_medium_b {c1} {r1} 16 18"]
    if axis_wall == "ne":
        out += [f"sconce_ne 13 {r0}", f"sconce_ne 17 {r0}", f"coffin_stone_closed_sw {c0} {r0} 30 40",
                f"coffin_wood_broken_sw {c1} {r0} 30 40", f"rubble_a {c0 + 1} {r1}", f"bones {c1 - 1} {r1}"]
    else:
        out += [f"sconce_nw {c0} 13", f"sconce_nw {c0} 17", f"coffin_stone_closed_se {c0} {r0} 40 30",
                f"coffin_wood_open_se {c0} {r1} 40 0", f"rubble_b {c1} {r0 + 1}", f"bones {c1} {r1 - 1}"]
    return out
props += room_decor(11, 19, 3, 6, "ne", 1)
props += room_decor(3, 6, 11, 19, "nw", 2)
props += room_decor(24, 27, 11, 19, "nw", 3)
# arandelas nos corredores
props += ["sconce_nw 14 8", "sconce_nw 14 21", "sconce_ne 8 14", "sconce_ne 21 14"]
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "layouts", "hub_completo.txt"), "w", encoding="utf-8") as f:
    f.write("# Hub completo (planta baixa do Pedro): fonte no centro, 4 alas em cruz, caixão do Virgil na ala de baixo.\n")
    f.write("# Feito só com peças do kit; o lugar da fonte está marcado por um anel de velas.\n[grid]\n")
    for row in grid: f.write("".join(row).rstrip() + "\n")
    f.write("[edges]\n" + "\n".join(edges) + "\n[props]\n" + "\n".join(props) + "\n[settings]\nambient -0.6\nseed 21\n")
print("ok")
