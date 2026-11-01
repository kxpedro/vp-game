import os
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


