"""Backdrop v3: the original painting's own line work, thinned to one dot at the 2x2 grid, refilled with the
terrain palette. Stone layout, ruin outline, vines, fence, barrel, crate, beam and cloth all follow the original.
The niche interior is redrawn (plaster, awning shadow, tool rack)."""
from PIL import Image
import numpy as np
from scipy import ndimage
from px import *
import s_backdrop as V2

SRC = r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/unarmed-approved-motion-20261111/resources/environments/maintenance-shops/runtime/stage1-backdrop.png"
LINE = D
JOINT = D


def zhang_suen(img):
    img = img.copy().astype(np.uint8)
    changed = True
    while changed:
        changed = False
        for step in (0, 1):
            P = np.pad(img, 1)
            p2, p3, p4, p5 = P[:-2, 1:-1], P[:-2, 2:], P[1:-1, 2:], P[2:, 2:]
            p6, p7, p8, p9 = P[2:, 1:-1], P[2:, :-2], P[1:-1, :-2], P[:-2, :-2]
            nb = [p2, p3, p4, p5, p6, p7, p8, p9]
            B = sum(n.astype(int) for n in nb)
            A = sum(((nb[i] == 0) & (nb[(i + 1) % 8] == 1)).astype(int) for i in range(8))
            if step == 0: c1, c2 = p2 * p4 * p6, p4 * p6 * p8
            else: c1, c2 = p2 * p4 * p8, p2 * p6 * p8
            m = (img == 1) & (B >= 2) & (B <= 6) & (A == 1) & (c1 == 0) & (c2 == 0)
            if m.any(): img[m] = 0; changed = True
    return img.astype(bool)


def lines_and_wall():
    im = np.asarray(Image.open(SRC).convert("RGBA")).astype(float)
    H, W_ = im.shape[0] // 2, im.shape[1] // 2
    alpha = im[..., 3] > 128
    lum = im[..., :3] @ [.299, .587, .114]
    loc = ndimage.median_filter(lum, size=9)
    dark = ((lum < loc - 14) | (lum < 190)) & alpha
    d4 = dark[:H * 2, :W_ * 2].reshape(H, 2, W_, 2).mean((1, 3))
    a4 = alpha[:H * 2, :W_ * 2].reshape(H, 2, W_, 2).mean((1, 3))
    wall = a4 >= .5
    ln = (d4 >= .25) & wall
    sk = zhang_suen(ln)
    lab, n = ndimage.label(sk, structure=np.ones((3, 3)))
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    for i, s in enumerate(sizes, 1):
        if s < 3: sk[lab == i] = False
    # pixel-perfect pass: drop L-elbow dots so every joint stays one dot thick
    for _ in range(3):
        P = np.pad(sk, 2)
        drop = np.zeros_like(sk)
        ys, xs = np.nonzero(sk)
        for y, x in zip(ys, xs):
            Y, X = y + 2, x + 2
            hs = [d for d in (-1, 1) if P[Y, X + d]]; vs = [d for d in (-1, 1) if P[Y + d, X]]
            if len(hs) == 1 and len(vs) == 1 and not P[Y + vs[0], X + hs[0]]                     and not P[Y, X + 2 * hs[0]] and not P[Y + 2 * vs[0], X]:
                drop[y, x] = True
        if not drop.any(): break
        sk &= ~drop
    return wall, sk


def draw():
    wall, sk = lines_and_wall()
    Hd, Wd = wall.shape
    c = Canvas(Wd, Hd)
    for y in range(Hd):
        for x in range(Wd):
            if wall[y, x]: c.px(x, y, C)
    isl = lambda x, y: 0 <= x < Wd and 0 <= y < Hd and sk[y, x]
    for y in range(Hd):
        for x in range(Wd):
            if not wall[y, x]: continue
            if sk[y, x]: c.px(x, y, JOINT); continue
            if isl(x, y - 1) or isl(x - 1, y): c.px(x, y, W)
            elif isl(x, y + 1) or isl(x + 1, y): c.px(x, y, L)
    # erase the original's prop strokes where the crisp redrawn props go (foliage is overlaid, not erased)
    def erase(fn):
        for y in range(Hd):
            for x in range(Wd):
                if wall[y, x] and fn(x, y): c.px(x, y, C)
    erase(lambda x, y: x <= 97 and y >= 95 and not (64 <= x <= 67))           # left fence, barrel, crate
    erase(lambda x, y: x >= 288 and y >= 96)                                   # right fence
    erase(lambda x, y: 115 <= x <= 229 and 28 <= y <= 38)                      # beam
    erase(lambda x, y: 168 <= x <= 229 and 28 <= y <= 56)                      # cloth
    erase(lambda x, y: 122 <= x <= 141 and 35 <= y <= 54)                      # bracket and brace
    erase(lambda x, y: 224 <= x <= 265 and 48 <= y <= 73)                      # sign arm
    erase(lambda x, y: ((x - 258) / 22) ** 2 + ((y - 122) / 10) ** 2 <= 1)     # under the bushes
    erase(lambda x, y: ((x - 22) / 16) ** 2 + ((y - 122) / 8) ** 2 <= 1)
    # niche interior redrawn over the original's blank plaster
    rng = V2.random.Random(1107)
    for x in range(133, 211):
        for y in range(40, Hd): c.px(x, y, V2.NICHE)
    yy = 40
    for i, hgt in enumerate((15, 18, 16, 20, 22)):
        y0, y1 = yy, min(Hd - 1, yy + hgt)
        c.hl(133, 210, y1, V2.NICHE_LO)
        x = 133 + (0 if i % 2 else 13)
        while x < 210:
            w = rng.choice([24, 28, 31, 35])
            if x > 133: c.vl(x, y0 + 1, y1 - 1, V2.NICHE_LO)
            x += w
        yy = y1 + 1
        if yy >= Hd: break
    for y in range(40, 47):
        for x in range(133, 211):
            if y < 44 or (x + y) % 2 == 0: c.px(x, y, V2.NICHE_SH)
    for y in range(40, Hd): c.px(133, y, V2.NICHE_SH)
    V2.tool_rack(c)
    layer = V2.draw(layer_only=True)
    c.im.alpha_composite(layer.im)
    opaque = {(x, y) for x in range(Wd) for y in range(Hd) if c.get(x, y)[3]}
    for x, y in opaque - smooth_mask(opaque): c.px(x, y, CLEAR)
    c.outline(LINE)
    declump(c, LINE, C); declump(c, M, C)
    lint(c, colors=[LINE, M], name='backdrop-trace')
    return c


if __name__ == '__main__':
    draw().native().save('out/maintenance-backdrop.png')
