"""Backdrop v5: neat stepped wall like the original (whole stones, ~12-dot courses, running bond, big blocks),
ivy where the original grows leaves, crisp props, worked niche. 330x129 logical -> 660x258."""
import random
import numpy as np
from scipy import ndimage
from px import *
import s_backdrop as V2
import s_backdrop_trace as T
import s_backdrop4 as B4

LINE = D
# (top y, bottom y, left x, right x) per course, measured from the original's steps
COURSES = [(0, 9, 182, 205), (9, 18, 134, 226), (18, 31, 122, 230), (31, 43, 107, 246), (43, 55, 104, 260),
           (55, 67, 95, 271), (67, 79, 84, 284), (79, 91, 70, 292), (91, 103, 40, 305), (103, 116, 16, 314),
           (116, 128, 8, 322)]


def stones(rng):
    out = []
    for i, (y0, y1, xa, xb) in enumerate(COURSES):
        x = xa - (rng.randint(6, 16) if i % 2 else 0)
        while x <= xb:
            w = rng.choice([26, 28, 30, 32, 34, 36, 40])
            s0, s1 = max(x, xa), min(x + w, xb)
            if s1 - s0 >= 8: out.append((s0, y0, s1, y1))
            elif out and out[-1][1] == y0: out[-1] = (out[-1][0], y0, s1, y1)   # merge a sliver into its neighbour
            x += w
    return out


def draw():
    rng = random.Random(21)
    Hd, Wd = 129, 330
    c = Canvas(Wd, Hd)
    st = stones(rng)
    wall = np.zeros((Hd, Wd), bool)
    for x0, y0, x1, y1 in st: wall[y0:y1 + 1, x0:x1 + 1] = True
    for x0, y0, x1, y1 in st:
        c.rect(x0, y0, x1, y1, fill=C)
        c.hl(x0 + 1, x1 - 1, y0 + 1, W); c.vl(x0 + 1, y0 + 2, y1 - 2, W)       # light top and left
        c.hl(x0 + 1, x1 - 1, y1 - 1, L); c.vl(x1 - 1, y0 + 2, y1 - 2, L)       # shade bottom and right
        if rng.random() < .38 and x1 - x0 > 12 and y1 - y0 > 8:                # terrain-style // hatch
            hx = rng.randint(x0 + 3, x1 - 8); hy = rng.randint(y0 + 3, y1 - 5)
            paint(c, ["..o.o", ".o.o.", "o.o.."], hx, hy, {'o': L})
    for x0, y0, x1, y1 in st:                                                  # one-dot joints between stones
        c.hl(x0, x1, y0, LINE); c.hl(x0, x1, y1, LINE); c.vl(x0, y0, y1, LINE); c.vl(x1, y0, y1, LINE)
    # soften the outer corners of the steps
    for x0, y0, x1, y1 in st:
        for x, y, dx, dy in ((x0, y0, -1, -1), (x1, y0, 1, -1)):
            if not wall[y + dy if 0 <= y + dy < Hd else y, x] and not (0 <= x + dx < Wd and wall[y, x + dx]):
                c.px(x, y, CLEAR)
    # ---- niche, pilasters, tool rack (from v4)
    for x in range(131, 213):
        for y in range(38, Hd): c.px(x, y, V2.NICHE)
    yy = 38
    for i, hgt in enumerate((15, 18, 16, 20, 22)):
        y0, y1 = yy, min(Hd - 1, yy + hgt)
        c.hl(131, 212, y1, V2.NICHE_LO)
        x = 131 + (0 if i % 2 else 13)
        while x < 212:
            w = rng.choice([24, 28, 31, 35])
            if x > 131: c.vl(x, y0 + 1, y1 - 1, V2.NICHE_LO)
            x += w
        yy = y1 + 1
        if yy >= Hd: break
    for y in range(38, 45):
        for x in range(131, 213):
            if y < 42 or (x + y) % 2 == 0: c.px(x, y, V2.NICHE_SH)
    for y in range(38, Hd): c.px(131, y, V2.NICHE_SH)
    for x0 in (125, 213):
        c.rect(x0, 36, x0 + 5, Hd - 1, fill=C)
        c.vl(x0, 36, Hd - 1, LINE); c.vl(x0 + 5, 36, Hd - 1, LINE); c.vl(x0 + 1, 37, Hd - 1, W); c.vl(x0 + 4, 37, Hd - 1, L)
        y = 36
        for hgt in (11, 9, 12, 10, 13, 11, 12, 14, 12):
            y += hgt
            if y < Hd - 1: c.hl(x0 + 1, x0 + 4, y, LINE); c.hl(x0 + 1, x0 + 4, y + 1, W)
    V2.tool_rack(c)
    # ---- props
    c.im.alpha_composite(V2.draw(layer_only=True, foliage=False, skip_props=True).im)
    import props2 as P2
    P2.cloth(c); P2.fence(c, 1, 32, 3); P2.fence(c, 290, 323, 7); P2.barrel(c); P2.crate(c)
    # ---- ivy where the original grows leaves
    owall, sk = T.lines_and_wall()
    props = np.array([[any(f(x, y) for f in B4.PROPS) for x in range(Wd)] for y in range(Hd)])
    edge = owall & ~ndimage.binary_erosion(owall, iterations=2)
    dens = ndimage.uniform_filter((sk & ~edge).astype(float), size=7)
    fol = ndimage.binary_opening(dens > .30, iterations=1) & owall
    fol |= ndimage.binary_dilation(fol, iterations=2) & edge & (ndimage.uniform_filter(sk.astype(float), 5) > .35)
    fol &= ~props
    lab, n = ndimage.label(fol)
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    for i, s in enumerate(sizes, 1):
        if s < 30: fol[lab == i] = False
    import leaves2 as LV
    # rosettes on a jittered grid inside the original's leaf areas; shaded ones behind, lit ones in front
    cents = []
    for y in range(2, Hd, 7):
        for x in range((y // 7 % 2) * 5, Wd, 10):
            jx, jy = x + rng.randint(-2, 2), y + rng.randint(-2, 2)
            if 0 <= jx < Wd and 0 <= jy < Hd and fol[jy, jx]: cents.append((jx, jy))
    rng.shuffle(cents)
    back, front = cents[: len(cents) // 3], cents[len(cents) // 3:]
    for group, lit in ((sorted(back, key=lambda p: p[1]), False), (sorted(front, key=lambda p: p[1]), True)):
        for x, y in group: LV.rosette(c, x, y, lit, rng)
    # hanging vines where the original drapes them
    for x0, y0, ln in ((119, 30, 50), (112, 44, 34), (226, 2, 44), (104, 60, 24)):
        LV.vine(c, x0, y0, ln, rng)
    # a few fallen leaves on the steps and at the foot of the wall
    for (_a, _b, ux0, ux1), (y0, y1, x0, x1) in zip(COURSES, COURSES[1:]):
        for a, b in ((x0 + 1, ux0 - 8), (ux1 + 2, x1 - 7)):             # exposed ledges only
            if b - a >= 1 and rng.random() < .6:
                LV.leaf(c, LV.orient(LV.DIAG, fh=rng.random() < .5, fv=True), rng.randint(a, b), y0 - 6, True)
    for x in (110, 222, 236, 300):
        LV.leaf(c, LV.orient(LV.DIAG, fh=x % 2 == 0, fv=True), x, Hd - 7, True)
    # ---- silhouette
    opaque = {(x, y) for x in range(Wd) for y in range(Hd) if c.get(x, y)[3]}
    for x, y in opaque - smooth_mask(opaque): c.px(x, y, CLEAR)
    c.outline(LINE)
    declump(c, LINE, C)
    lint(c, colors=[LINE], name='backdrop-v5')
    return c


if __name__ == '__main__':
    draw().native().save('out/maintenance-backdrop.png')
