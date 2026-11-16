"""Backdrop v4: the original's stone layout (joints straightened to clean 1-dot lines), foliage regrown as ivy
exactly where the original had leaves, crisp props, worked niche. 330x129 logical -> 660x258."""
import random
import numpy as np
from scipy import ndimage
from px import *
import s_backdrop as V2
import s_backdrop_trace as T

LINE = D
IVY = [".DD.DD.",
       "DWWDWWD",
       "DWWWWWD",
       ".DWWLD.",
       "..DLD..",
       "...D..."]
IVY_SIDE = [".D.D.",
            "DWDWD",
            "DWWWD",
            ".DLD.",
            "..D.."]

PROPS = [lambda x, y: x <= 97 and y >= 95, lambda x, y: x >= 288 and y >= 96,
         lambda x, y: 115 <= x <= 229 and 28 <= y <= 38, lambda x, y: 168 <= x <= 229 and 28 <= y <= 56,
         lambda x, y: 122 <= x <= 141 and 35 <= y <= 54, lambda x, y: 224 <= x <= 265 and 48 <= y <= 73,
         lambda x, y: 125 <= x <= 218 and y >= 36]                         # niche + pilasters (redrawn)


def straighten(sk):
    """keep only horizontal / vertical runs of 3+ dots: stone joints become straight"""
    Hh, Ww = sk.shape
    sk = sk.copy()
    # bridge one-dot gaps and one-row jogs so broken joints join into runs
    for y in range(Hh):
        for x in range(1, Ww - 1):
            if not sk[y, x] and sk[y, x - 1] and sk[y, x + 1]: sk[y, x] = True
    for x in range(Ww):
        for y in range(1, Hh - 1):
            if not sk[y, x] and sk[y - 1, x] and sk[y + 1, x]: sk[y, x] = True
    for y in range(1, Hh):
        for x in range(1, Ww - 1):                        # a stepped near-horizontal line: pull the step onto one row
            if sk[y, x] and not sk[y, x - 1] and sk[y - 1, x - 1] and not sk[y - 1, x]:
                sk[y, x - 1] = True
    out = np.zeros_like(sk)
    for y in range(Hh):
        x = 0
        while x < Ww:
            if sk[y, x]:
                x0 = x
                while x < Ww and sk[y, x]: x += 1
                if x - x0 >= 3: out[y, x0:x] = True
            else: x += 1
    for x in range(Ww):
        y = 0
        while y < Hh:
            if sk[y, x]:
                y0 = y
                while y < Hh and sk[y, x]: y += 1
                if y - y0 >= 3: out[y0:y, x] = True
            else: y += 1
    lab, n = ndimage.label(out, structure=np.ones((3, 3)))
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    for i, s in enumerate(sizes, 1):
        if s < 5: out[lab == i] = False
    return out


def draw():
    rng = random.Random(4)
    wall, sk = T.lines_and_wall()
    Hd, Wd = wall.shape
    props = np.array([[any(f(x, y) for f in PROPS) for x in range(Wd)] for y in range(Hd)])
    edge = wall & ~ndimage.binary_erosion(wall, iterations=2)
    sk_in = sk & ~edge
    dens = ndimage.uniform_filter(sk_in.astype(float), size=7)
    fol = ndimage.binary_opening(dens > .30, iterations=1) & wall
    # original leaves that spill over the wall edge also count
    fol |= ndimage.binary_dilation(fol, iterations=2) & edge & (ndimage.uniform_filter(sk.astype(float), 5) > .35)
    fol &= ~props
    lab, n = ndimage.label(fol)
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    for i, s in enumerate(sizes, 1):
        if s < 30: fol[lab == i] = False
    joints = straighten(sk_in & ~fol & ~props)
    # split faces that are much bigger than the original's stones (where a faint joint was lost)
    for _ in range(4):
        face = wall & ~joints & ~props & ~edge
        lab, n = ndimage.label(face)
        for i, sl in enumerate(ndimage.find_objects(lab), 1):
            ys, xs = sl
            hgt, wid = ys.stop - ys.start, xs.stop - xs.start
            reg = lab[sl] == i
            if reg.sum() < 120: continue
            if wid > 34:
                x = xs.start + wid // 2 + rng.randint(-4, 4)
                for y in range(ys.start, ys.stop):
                    if lab[y, x] == i: joints[y, x] = True
            if hgt > 15:
                y = ys.start + hgt // 2 + rng.randint(-2, 2)
                for x in range(xs.start, xs.stop):
                    if lab[y, x] == i: joints[y, x] = True

    c = Canvas(Wd, Hd)
    J = lambda x, y: 0 <= x < Wd and 0 <= y < Hd and joints[y, x]
    for y in range(Hd):
        for x in range(Wd):
            if not wall[y, x]: continue
            if joints[y, x]: c.px(x, y, LINE); continue
            if J(x, y - 1) or J(x - 1, y): col = W                    # stone top / left catch the light
            elif J(x, y + 1) or J(x + 1, y): col = L                  # stone bottom / right in shade
            elif J(x, y + 2): col = L if (x + y) % 2 else C           # soft second shade row
            else: col = C
            c.px(x, y, col)
    # pits and small cracks on open stone faces
    for _ in range(140):
        x, y = rng.randrange(Wd), rng.randrange(Hd)
        if wall[y, x] and c.get(x, y) == C and not fol[y, x] and not props[y, x]:
            if rng.random() < .8: c.px(x, y, L)
            elif all(c.get(x + k, y + k) == C for k in range(3)):
                c.px(x, y, M); c.px(x + 1, y + 1, M); c.px(x + 1, y + 2, M)
    # ---- niche, pilasters, tool rack
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
    # ---- props (beam, cloth, sign arm, fences, barrel, crate)
    c.im.alpha_composite(V2.draw(layer_only=True, foliage=False).im)
    # ---- ivy where the original grows leaves: back leaves first (shaded), front leaves lit
    spots = []
    for y in range(0, Hd, 4):
        for x in range((y // 4 % 2) * 3, Wd, 6):
            jx, jy = x + rng.randint(-1, 1), y + rng.randint(-1, 1)
            if 0 <= jx < Wd and 0 <= jy < Hd and fol[jy, jx]: spots.append((jx, jy))
    rng.shuffle(spots)
    back, front = spots[: len(spots) // 3], spots[len(spots) // 3:]
    for group, lit in ((sorted(back, key=lambda p: p[1]), False), (sorted(front, key=lambda p: p[1]), True)):
        for x, y in group:
            r_ = rng.random()
            rows = IVY if r_ < .35 else (IVY_SIDE if r_ < .55 else V2.LEAVES['mid'])
            if rng.random() < .5: rows = [r[::-1] for r in rows]
            pal = {'D': LINE, 'W': W if lit else C, 'C': C if lit else L, 'L': L if lit else M, 'M': M}
            paint(c, rows, x - len(rows[0]) // 2, y - 3, pal)
    # ---- silhouette
    opaque = {(x, y) for x in range(Wd) for y in range(Hd) if c.get(x, y)[3]}
    for x, y in opaque - smooth_mask(opaque): c.px(x, y, CLEAR)
    c.outline(LINE)
    declump(c, LINE, C)
    lint(c, colors=[LINE], name='backdrop-v4')
    return c


if __name__ == '__main__':
    draw().native().save('out/maintenance-backdrop.png')
