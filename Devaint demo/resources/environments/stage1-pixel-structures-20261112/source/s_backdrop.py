"""Maintenance (artisan) room backdrop v2, 660x258 native = 330x129 logical.
Irregular hand-laid stones (seeded), organic foliage from four leaf shapes, a worked niche interior with a tool rack.
Backdrop silhouette uses D (not the terrain ink K) so it never reads as walkable ground."""
import math, random
from px import *

Wd, Hd = 330, 129
LINE = D
NICHE, NICHE_LO, NICHE_SH = h('f6eee2'), h('e6ddcf'), h('d8cebf')
STONE_ALT = h('f8f0e4')


def profile(x):
    """ragged ruin outline: rises from the left fence to the top block, falls to the right fence"""
    pts = [(14, 99), (40, 92), (60, 84), (80, 72), (96, 62), (108, 46), (120, 30), (128, 12), (150, 4), (214, 2),
           (222, 22), (238, 36), (252, 48), (268, 58), (282, 72), (296, 92), (300, 200)]
    if x < 14 or x >= 300: return 999
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if x0 <= x < x1: return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return 999


def lay_stones(rng):
    """courses of varied height from the ground up, stones of varied width; returns list of rects"""
    stones = []
    y1 = Hd - 1
    while y1 > 0:
        ch = rng.choice([9, 10, 11, 12, 13])
        y0 = max(0, y1 - ch + 1)
        x = rng.randint(-14, 0)
        while x < Wd:
            w = rng.choice([14, 17, 20, 22, 24, 27, 30, 34])
            x0, x1 = x, x + w - 1
            cx = (max(x0, 0) + min(x1, Wd - 1)) / 2
            top = profile(int(cx))
            # a stone survives if its middle sits under the ruin line; some top stones are knocked off at random
            if y0 >= top - 3 and not (y0 < top + 6 and rng.random() < .22):
                stones.append((max(0, x0), y0, min(Wd - 1, x1), y1))
            x += w
        y1 = y0 - 1
    return stones


def draw_stone(c, rng, x0, y0, x1, y1):
    face = STONE_ALT if rng.random() < .3 else C
    c.rect(x0, y0, x1 - 1, y1 - 1, fill=face)
    c.hl(x0 + 1, x1 - 2, y0, W)                       # light top
    c.vl(x0, y0 + 1, y1 - 2, W)                       # light left
    c.hl(x0 + 1, x1 - 1, y1 - 1, L)                   # shaded bottom
    c.vl(x1 - 1, y0 + 1, y1 - 1, L)                   # shaded right
    for x, y in ((x0, y0), (x1 - 1, y0), (x0, y1 - 1), (x1 - 1, y1 - 1)): c.px(x, y, M)   # rounded corners
    r = rng.random()
    if r < .18 and x1 - x0 > 12:                       # crack
        cx = rng.randint(x0 + 3, x1 - 5); c.line([(cx, y0 + 1), (cx + 2, y0 + 4)], M); c.px(cx + 3, y0 + 5, M)
    elif r < .32:                                      # chipped corner
        c.px(x1 - 2, y0, M); c.px(x1 - 1, y0 + 1, M)
    for _ in range(rng.randint(0, 3)):                 # small pits
        c.px(rng.randint(x0 + 2, max(x0 + 2, x1 - 3)), rng.randint(y0 + 2, max(y0 + 2, y1 - 3)), L)
    if r < .45 and x1 - x0 > 10 and r >= .32:          # worn patch
        px_, py_ = rng.randint(x0 + 3, x1 - 6), rng.randint(y0 + 2, max(y0 + 2, y1 - 4))
        c.hl(px_, px_ + 2, py_, L); c.px(px_ + 1, py_ + 1, L)


# ---------------------------------------------------------------- leaves (four shapes)
LEAVES = {
    'big': ["....DDD....", "..DDWWWDD..", ".DWWWWCCCD.", "DWWWMMMMMLD", ".DCCCCLLLD.", "..DDLLLDD..", "....DDD...."],
    'mid': ["..DDD...", ".DWWWDD.", "DWWMMMLD", ".DCCLDD.", "..DDD..."],
    'small': [".DD..", "DWWD.", "DWCLD", ".DLD.", "..D.."],
    'up': ["..D..", ".DWD.", "DWWLD", "DWMLD", "DCMLD", ".DLD.", "..D.."],
}


def put_leaf(c, kind, x, y, flip=False, back=False):
    rows = LEAVES[kind]
    if flip: rows = [r[::-1] for r in rows]
    pal = {'D': LINE, 'W': C, 'C': L, 'L': M, 'M': M} if back else {'D': LINE, 'W': W, 'C': C, 'L': L, 'M': M}
    paint(c, rows, x, y, pal)


def cluster(c, rng, inside, box, density=1.0, kinds=('big', 'big', 'mid')):
    x0, y0, x1, y1 = box
    pts = []
    tries = int((x1 - x0) * (y1 - y0) * .25 * density)
    for _ in range(tries):
        x, y = rng.randint(x0, x1), rng.randint(y0, y1)
        if inside(x, y) and all((x - a) ** 2 + (y - b) ** 2 >= 22 for a, b in pts):
            pts.append((x, y))
    if not pts: return
    ymid = (min(p[1] for p in pts) + max(p[1] for p in pts)) / 2
    order = sorted(pts, key=lambda p: p[1])
    # two passes: shaded leaves behind, lit leaves in front, so the clump reads with volume
    back = [p for k, p in enumerate(order) if k % 3 == 0]
    for x, y in back + [p for p in order if p not in back]:
        k = rng.choice(kinds)
        rows = LEAVES[k]
        put_leaf(c, k, x - len(rows[0]) // 2, y - len(rows) // 2, flip=rng.random() < .5, back=(x, y) in back)


def hanging_vine(c, rng, x0, y0, length):
    ph = rng.random() * 6
    stem = [(int(round(x0 + 1.6 * math.sin(k / 6 + ph))), y0 + k) for k in range(length)]
    for x, y in stem: c.px(x, y, M)
    k = 0
    while k < length - 2:
        x, y = stem[k]
        t = k / length
        kind = 'mid' if t < .45 else 'small'
        side = rng.random() < .5
        rows = LEAVES[kind]
        put_leaf(c, kind, x + 1 if side else x - len(rows[0]), y - 1, flip=not side)
        k += rng.randint(3, 6)
    c.px(*stem[-1], LINE)


def bush(c, rng, cx, by, w, h_, density=1.0):
    inside = lambda x, y: 0 <= by - y <= h_ and abs(x - cx) <= w / 2 * (1 - ((by - y) / h_) ** 1.7) + 1
    cluster(c, rng, inside, (cx - w // 2, by - h_, cx + w // 2, by), density)


# ---------------------------------------------------------------- props
def tool_rack(c):
    c.rect(142, 47, 202, 49, fill=L); c.hl(142, 202, 47, LINE); c.hl(142, 202, 49, LINE); c.hl(143, 201, 48, C)
    c.vl(142, 47, 49, LINE); c.vl(202, 47, 49, LINE)
    for x in (148, 160, 172, 184, 196): c.px(x, 50, LINE)                     # pegs
    # hammer
    c.vl(148, 51, 62, M); c.rect(145, 51, 151, 53, fill=D); c.hl(146, 150, 52, M)
    # tongs
    c.line([(160, 51), (158, 63)], LINE); c.line([(160, 51), (162, 63)], LINE)
    # brush bundle
    for x in (171, 172, 173): c.vl(x, 51, 58, M)
    c.hl(170, 174, 59, LINE); c.vl(170, 60, 63, LINE); c.vl(174, 60, 63, LINE); c.hl(171, 173, 63, LINE)
    for x in (171, 173): c.vl(x, 60, 62, L)
    # coil of rope
    rope = smooth_mask(ellipse_mask(180, 51, 188, 59)) - smooth_mask(ellipse_mask(182, 53, 186, 57))
    for x, y in rope: c.px(x, y, L)
    for x, y in rings(rope)[0]: c.px(x, y, M)
    # saw
    c.rect(193, 51, 199, 52, fill=M); c.line([(194, 53), (198, 61)], LINE); c.line([(193, 53), (193, 60)], M)
    c.line([(193, 60), (198, 61)], M)


def draw(layer_only=False, foliage=True, skip_props=False):
    rng = random.Random(1107)
    c = Canvas(Wd, Hd)
    if not layer_only:
        # ---- stones
        stones = lay_stones(rng)
        wall = set()
        for x0, y0, x1, y1 in stones:
            wall |= {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)}
        for x, y in wall: c.px(x, y, M)                                   # mortar shows between stones
        for s in stones: draw_stone(c, rng, *s)
        # ---- niche: plaster back wall, big faint blocks, awning shadow, side shadow
        for x in range(131, 213):
            for y in range(38, Hd): c.px(x, y, NICHE)
        yy = 38
        for i, hgt in enumerate((15, 18, 16, 20, 22)):
            y0, y1 = yy, min(Hd - 1, yy + hgt)
            c.hl(131, 212, y1, NICHE_LO)
            x = 131 + (0 if i % 2 else 13)
            while x < 212:
                w = rng.choice([24, 28, 31, 35])
                if x > 131: c.vl(x, y0 + 1, y1 - 1, NICHE_LO)
                x += w
            yy = y1 + 1
            if yy >= Hd: break
        for y in range(38, 45):                                           # awning shadow, dithered off
            for x in range(131, 213):
                if y < 42 or (x + y) % 2 == 0: c.px(x, y, NICHE_SH)
        for x in (131, 132):
            for y in range(38, Hd): c.px(x, y, NICHE_SH if x == 131 or y % 2 else NICHE)
        c.hl(131, 212, Hd - 1, NICHE_SH)
        c.line([(150, 75), (153, 79), (152, 83)], NICHE_LO)               # plaster cracks
        c.line([(201, 94), (198, 99)], NICHE_LO)
        tool_rack(c)
        # ---- pilasters of dressed stone
        for x0 in (125, 213):
            c.rect(x0, 36, x0 + 5, Hd - 1, fill=C)
            c.vl(x0, 36, Hd - 1, LINE); c.vl(x0 + 5, 36, Hd - 1, LINE); c.vl(x0 + 1, 37, Hd - 1, W); c.vl(x0 + 4, 37, Hd - 1, L)
            y = 36
            for hgt in (11, 9, 12, 10, 13, 11, 12, 14, 12):
                y += hgt
                if y < Hd - 1: c.hl(x0 + 1, x0 + 4, y, M)
    # ---- awning beam, bracket, brace
    c.rect(116, 31, 228, 35, fill=L); c.hl(116, 228, 31, LINE); c.hl(116, 228, 35, LINE); c.hl(117, 227, 32, C)
    c.vl(116, 31, 35, LINE); c.vl(228, 31, 35, LINE)
    for x0 in (117, 223):                                              # iron straps at the beam ends
        c.rect(x0, 30, x0 + 4, 36, fill=M); c.rect(x0, 30, x0 + 4, 36, line=LINE); c.px(x0 + 2, 33, W)
    for x in (134, 151, 190, 207): c.px(x, 34, M)                     # grain
    c.rect(124, 36, 128, 52, fill=L); c.vl(124, 36, 52, LINE); c.vl(128, 36, 52, LINE); c.hl(124, 128, 52, LINE)
    c.vl(125, 37, 51, C); c.px(126, 49, M)
    c.line([(129, 47), (138, 36)], LINE); c.line([(129, 48), (139, 36)], L); c.line([(130, 48), (140, 36)], LINE)
    if not skip_props:
        # ---- cloth thrown over the beam: sagging hem, a long corner, folds following the sag
        hem = {}
        for x in range(170, 226):
            t = (x - 170) / 55
            y = 38 + 13 * math.sin(math.pi * t) ** .9
            if x > 214: y += (x - 214) * 1.1                               # long corner on the right
            if x % 7 in (3, 4): y += 1                                     # frayed edge
            hem[x] = int(round(y))
        for x, yb in hem.items():
            for y in range(30, yb): c.px(x, y, W)
            c.px(x, yb, LINE)
        for x in range(170, 225):                                         # keep the hem one dot thick
            if abs(hem[x + 1] - hem[x]) > 1:
                lo, hi = sorted((hem[x], hem[x + 1]))
                for y in range(lo + 1, hi): c.px(x if hem[x] > hem[x + 1] else x + 1, y, LINE)
        c.vl(169, 30, hem[170], LINE); c.vl(226, 30, hem[225], LINE)
        c.hl(169, 226, 29, LINE)
        for k, x0 in enumerate((178, 190, 203, 215)):
            for y in range(33, hem[x0] - 2):
                c.px(x0 + int(1.5 * math.sin((y - 33) / 5 + k)), y, L)
        c.hl(171, 224, 30, C)
    # ---- sign arm
    c.rect(226, 50, 262, 53, fill=L); c.hl(226, 262, 50, LINE); c.hl(226, 262, 53, LINE); c.vl(262, 50, 53, LINE)
    c.hl(227, 261, 51, C)
    c.rect(228, 54, 231, 71, fill=L); c.vl(228, 54, 71, LINE); c.vl(231, 54, 71, LINE); c.hl(228, 231, 71, LINE)
    c.line([(232, 61), (238, 55)], LINE)
    for x in (230, 248, 258): c.px(x, 52, M)
    if not skip_props:
        # ---- left fence, barrel, crate, right fence
        for x0 in (1, 32, 290, 323):
            c.rect(x0, 98, x0 + 4, 128, fill=C); c.vl(x0, 98, 128, LINE); c.vl(x0 + 4, 98, 128, LINE); c.hl(x0, x0 + 4, 98, LINE)
            c.vl(x0 + 1, 99, 127, W); c.px(x0 + 2, 104, M); c.px(x0 + 2, 116, M)
        for xa, xb in ((5, 32), (295, 322)):
            for y0 in (102, 113):
                c.rect(xa, y0, xb, y0 + 4, fill=L); c.hl(xa, xb, y0, LINE); c.hl(xa, xb, y0 + 4, LINE); c.hl(xa + 1, xb - 1, y0 + 1, C)
                c.px(xa + 9, y0 + 2, M); c.px(xb - 7, y0 + 3, M)
        bar = smooth_mask({(x, y) for y in range(96, 129) for x in range(36, 66)
                           if abs(x + .5 - 50.5) <= 10 + 2.5 * math.sin(math.pi * (y - 96) / 32)})
        for x, y in bar: c.px(x, y, C)
        for x, y in rings(bar)[1]: c.px(x, y, W if x < 50 else L)
        for x, y in rings(bar)[0]: c.px(x, y, LINE)
        for y in (101, 112, 123):
            for x in range(30, 70):
                if (x, y) in bar and (x, y) not in rings(bar)[0]: c.px(x, y, LINE)
        for x in (45, 50, 55):
            for y in range(97, 128):
                if (x, y) in bar and c.get(x, y) == C: c.px(x, y, L)
        c.rect(68, 108, 95, 128, fill=C); c.rect(68, 108, 95, 128, line=LINE)
        c.rect(70, 110, 93, 126, line=M); c.line([(71, 125), (92, 111)], LINE); c.line([(72, 125), (92, 112)], L)
        c.hl(71, 92, 117, L)
    # ---- foliage
    if not foliage:
        return c
    hanging_vine(c, rng, 118, 30, 52); hanging_vine(c, rng, 111, 44, 30); hanging_vine(c, rng, 226, 0, 44)
    hanging_vine(c, rng, 103, 60, 22)
    bush(c, rng, 20, 127, 30, 14); bush(c, rng, 104, 100, 26, 22); bush(c, rng, 284, 98, 30, 24, 1.2)
    bush(c, rng, 258, 127, 46, 20); bush(c, rng, 140, 127, 14, 6, .8); bush(c, rng, 204, 127, 14, 6, .8)
    bush(c, rng, 230, 38, 18, 10, .9); bush(c, rng, 116, 28, 14, 8, .9)
    # ---- loose rubble resting on the ruined steps
    for x, y in ((86, 66), (140, 2), (200, 0), (250, 44), (60, 80)):
        top = next((yy for yy in range(Hd) if c.get(x + 2, yy)[3]), None)
        if top is None: continue
        paint(c, [".ooo.", "oWCLo", ".ooo."], x, top - 3, {'o': LINE, 'W': W, 'C': C, 'L': L})
    if layer_only:
        return c
    # ---- silhouette
    opaque = {(x, y) for x in range(Wd) for y in range(Hd) if c.get(x, y)[3]}
    for x, y in opaque - smooth_mask(opaque): c.px(x, y, CLEAR)
    c.outline(LINE)
    declump(c, LINE, C)
    lint(c, colors=[LINE], name='backdrop')
    return c


if __name__ == '__main__':
    draw().native().save('out/maintenance-backdrop.png')
