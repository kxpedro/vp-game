import sys, pickle, numpy as np
from PIL import Image
from sdf import norm

def C(h): return [int(h[i:i+2], 16) for i in (0, 2, 4)]
RAMP = {  # do escuro ao claro
    1: ["4d3a33", "796755", "a08662", "c4a27e"],                        # pele
    2: ["060608", "141013", "221c1a", "2e2a3c", "3d3a52"],              # cabelo
    3: ["141013", "242234", "403353", "5d4a7a"],              # casaco
    4: ["2a0f19", "3b1725", "73172d", "a3243c"],                        # forro
    5: ["141013", "242234", "3a3f50", "4a5462", "6d758d", "b3b9d1", "f0f2f8"],    # aço
    6: ["141013", "221c1a", "322b28", "4a3f36", "6b5a4a"],              # couro
    7: ["0d0b10", "161320", "242234", "33294a"],                        # calça/colete
    8: ["5a4020", "8a6a3a", "dba463", "f3d6a0"],                        # ouro
    9: ["4a5462", "8b93af", "b3b9d1", "e8ecf5", "ffffff"],              # lâmina
    10: ["141013", "322b28", "4a3f36"],                                  # cabo
}
SPEC = {5: (1.2, 5), 9: (1.0, 10), 8: (.7, 12), 2: (.55, 6), 6: (.15, 8)}

def light_levels(mats, N, cam):
    f, r, u = cam
    L = norm(-0.5 * r + 0.6 * u - 0.75 * f); Vv = -f
    nl = (N @ L)
    diff = np.clip(nl, 0, 1)
    wrap = np.clip((nl + 0.35) / 1.35, 0, 1)
    up = np.clip(N[..., 1] * .5 + .5, 0, 1)
    lvl = 0.06 + 0.45 * wrap + 0.50 * diff ** 1.3 + 0.06 * up
    lvl = np.where(mats == 1, lvl + 0.2, lvl)
    lvl = np.where(mats == 3, lvl + 0.06, lvl)
    lvl = np.where(mats == 5, lvl * 0.62, lvl)
    lvl = np.where(mats == 2, lvl * 0.75, lvl)
    lvl = np.where(mats == 6, lvl * 0.9, lvl)
    Rv = 2 * nl[..., None] * N - L
    sp = np.clip(Rv @ Vv, 0, 1)
    for m, (k, e) in SPEC.items():
        mm = mats == m; lvl[mm] += k * sp[mm] ** e
    rim = np.clip(1 - np.abs(N @ Vv), 0, 1) ** 3 * (N[..., 0] < 0)
    lvl += rim * 0.15
    return np.clip(lvl, 0, 1.2)

def downsample(mats, lvl, depth, ss):
    h, w = mats.shape; H, W = h // ss, w // ss
    M = np.zeros((H, W), int); Lv = np.zeros((H, W)); D = np.full((H, W), np.inf)
    for y in range(H):
        for x in range(W):
            bm = mats[y*ss:(y+1)*ss, x*ss:(x+1)*ss].ravel()
            bl = lvl[y*ss:(y+1)*ss, x*ss:(x+1)*ss].ravel()
            bd = depth[y*ss:(y+1)*ss, x*ss:(x+1)*ss].ravel()
            vals, cnt = np.unique(bm, return_counts=True)
            fg = vals != 0
            if not fg.any(): continue
            # cobre o pixel se pelo menos ~40% for personagem
            if cnt[fg].sum() < 0.4 * len(bm): continue
            # detalhes pequenos e claros (ouro, lâmina) têm prioridade
            score = cnt.astype(float); score[vals == 0] = -1
            for pm in (8, 9): score[vals == pm] *= 2.2
            m = vals[score.argmax()]
            sel = bm == m
            M[y, x] = m; Lv[y, x] = np.median(bl[sel]); D[y, x] = np.median(bd[sel])
    return M, Lv, D

def to_image(M, Lv, D, edge=0.05):
    H, W = M.shape; img = np.zeros((H, W, 4), np.uint8)
    idx = np.zeros((H, W), int)
    for y in range(H):
        for x in range(W):
            m = M[y, x]
            if m == 0: continue
            r = RAMP[m]; i = int(np.clip(np.floor(Lv[y, x] * len(r)), 0, len(r) - 1))
            # separação: vizinho bem mais perto da câmera -> linha escura deste lado
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < W and 0 <= yy < H and M[yy, xx] != 0 and D[yy, xx] < D[y, x] - edge:
                    i = 0 if m not in (8, 9) else max(0, i - 2); break
            if m == 9 and x + 1 < W and M[y, x + 1] != 9: i = 1
            if m == 9 and x - 1 >= 0 and M[y, x - 1] != 9: i = len(r) - 1
            idx[y, x] = i
            img[y, x, :3] = C(r[i]); img[y, x, 3] = 255
    a = img[..., 3] > 0; o = np.zeros_like(a)
    o[1:] |= a[:-1]; o[:-1] |= a[1:]; o[:, 1:] |= a[:, :-1]; o[:, :-1] |= a[:, 1:]
    o &= ~a; img[o] = [10, 8, 12, 255]
    return img

if __name__ == "__main__":
    ws = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    mats, N, depth, cam, P = pickle.load(open(f"buf_{ws}.pkl", "rb"))
    lvl = light_levels(mats, N, cam)
    # depuração em alta
    dbg = np.zeros(mats.shape + (3,), np.uint8)
    for m, r in RAMP.items():
        mm = mats == m
        idx = np.clip(np.floor(lvl[mm] * len(r)), 0, len(r) - 1).astype(int)
        dbg[mm] = np.array([C(c) for c in r])[idx]
    Image.fromarray(dbg).save(f"hi_{ws}.png")
    M, Lv, D = downsample(mats, lvl, depth, 4)
    pickle.dump((M, Lv, D), open(f"low_{ws}.pkl", "wb"))
    import os, details, head
    if os.environ.get("HEAD", "1") == "1":
        img = to_image(head.strip_head(M), Lv, D)
        img = details.paint(img, M, D, with_sword=bool(ws), face=False)
        img = head.paste(img)
    else:
        img = to_image(M, Lv, D)
        img = details.paint(img, M, D, with_sword=bool(ws))
    im = Image.fromarray(img); im.save(f"px_{ws}.png")
    big = im.resize((im.width * 6, im.height * 6), Image.NEAREST)
    bg = Image.new("RGBA", big.size, (58, 58, 70, 255)); bg.paste(big, (0, 0), big); bg.save(f"px_{ws}_x6.png")
