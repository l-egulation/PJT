"""Summit bell tower (종탑) alone, 2x2-dot pixel art. Proportions follow the old picture's tower (scaled ~1.3x):
a slim shaft, a belfry a little wider, a low pyramid roof. Volume comes from tone, not detail: a lit front face and
a narrow shaded right face, ledge shadows, a warm-grey ramp between the terrain colours; the bell and finial in gold.
Logical 50x184 (100x368 px); the bottom row is the foot on the goal stone."""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from px import *

OUT = os.path.join(os.path.dirname(__file__), 'sky_out')
# ramp: terrain colours plus three in-between tones
X1 = h('ece7de'); X2 = h('b7b3ab'); X3 = h('8a8882')
GOLD_L = h('f6dc8c'); GOLD = h('d9a441'); GOLD_M = h('b9852f'); GOLD_D = h('86601f')
WD, HT = 56, 184
OX = 3             # drawing offset: the widened foot reaches left of x 0


def face(c, x0, x1, y0, y1, ramp):
    """fill columns x0..x1 with a left-to-right tone ramp (list of (fraction, colour))"""
    for x in range(x0, x1 + 1):
        t = (x - x0) / max(1, x1 - x0)
        col = ramp[0][1]
        for f, cc in ramp:
            if t >= f: col = cc
        for y in range(y0, y1 + 1): c.px(x, y, col)


def shade_rows(c, x0, x1, y0, rows, front_split):
    """shadow under a ledge: rows of (front colour, side colour)"""
    for i, (fc, sc) in enumerate(rows):
        for x in range(x0, x1 + 1):
            if c.get(x, y0 + i)[3]: c.px(x, y0 + i, fc if x <= front_split else sc)


FRONT = [(0, W), (.1, C), (.62, X1), (.9, L)]
SIDE = [(0, X2), (.35, M), (.8, X3)]


def block(c, fx0, fx1, sx1, y0, y1):
    """a section: front face fx0..fx1, side face fx1+1..sx1"""
    face(c, fx0, fx1, y0, y1, FRONT)
    face(c, fx1 + 1, sx1, y0, y1, SIDE)


def ledge(c, x0, split, x1, y, rows):
    """a projecting band; rows of (front, side) tones top to bottom"""
    for i, (fc, sc) in enumerate(rows):
        for x in range(x0, x1 + 1): c.px(x, y + i, fc if x <= split else sc)


def round_head(x0, y0, x1, y1):
    m = set(); r = (x1 - x0 + 1) / 2; cx = x0 + r; cy = y0 + r
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if y < cy and ((x + .5 - cx) / r) ** 2 + ((y + .5 - cy) / r) ** 2 > 1: continue
            m.add((x, y))
    return smooth_mask(m)


class Shifted(Canvas):
    def px(self, x, y, c): Canvas.px(self, x + OX, y, c)
    def get(self, x, y): return Canvas.get(self, x + OX, y)


def tower():
    c = Shifted(WD, HT)
    # -- shaft (front 6..33, side 34..43) on a plinth
    # the shaft widens toward its foot: each side steps out one dot every ~26 rows (4 dots in all)
    for y in range(72, 176):
        d = min(4, (y - 72) * 5 // 104)
        fx0, split, sx1 = 6 - d, 33 + (d + 1) // 2, 43 + d
        face(c, fx0, split, y, y, FRONT); face(c, split + 1, sx1, y, y, SIDE)
    shade_rows(c, 6, 43, 72, [(L, X3), (X1, M), (X1, M)], 33)          # under the cornice
    ledge(c, 0, 36, 49, 176, [(W, X2), (C, M), (X1, M), (L, X3)])        # plinth steps
    ledge(c, -1, 37, 50, 180, [(W, X2), (C, M), (X1, X3), (L, X3)])
    # -- cornice between shaft and belfry
    ledge(c, 3, 35, 46, 68, [(W, X2), (C, M), (L, X3), (X2, D)])
    # -- belfry (front 4..34, side 35..45)
    block(c, 4, 34, 45, 33, 67)
    shade_rows(c, 4, 45, 33, [(X2, X3), (L, X3), (X1, M)], 34)          # eave shadow
    # -- eave band
    ledge(c, 1, 36, 48, 28, [(C, X2), (X1, M), (L, X3), (X2, D), (M, D)])
    # -- pyramid roof: apex (25, 6); lit front face to the corner over x 36, shaded side face beyond
    apex_x, apex_y, eave_y = 25, 6, 27
    for y in range(apex_y, eave_y + 1):
        t = (y - apex_y) / (eave_y - apex_y)
        left = round(apex_x - t * (apex_x - 1))
        right = round(apex_x + t * (48 - apex_x))
        hip = round(apex_x + t * (36 - apex_x))
        band = (y - apex_y) % 4
        for x in range(left, right + 1):
            if x <= hip:
                u = (x - left) / max(1, hip - left)
                col = (C if u < .2 else X1 if u < .65 else L) if band != 3 else (X1 if u < .2 else L if u < .65 else X2)
            else:
                col = [M, M, M, X3][band] if x < hip + 4 else [X3, X3, X3, D][band]
            c.px(x, y, col)
    # -- tone texture: soft mottled patches one step darker, and a darker band at the shaft's foot
    step = {W: C, C: X1, X1: L, L: X2, X2: M}
    for y in range(166, 176):
        for x in range(1, 49):
            p = c.get(x, y)
            if p in step and p != W: c.px(x, y, step[p])
    # -- belfry opening on the front face: dark depth, lit right reveal, shadowed left jamb, sill
    ax0, ax1, ay0, ay1 = 11, 27, 38, 62
    m = round_head(ax0, ay0, ax1, ay1)
    for x, y in m:
        depth = (y - ay0) / (ay1 - ay0)
        col = K if depth < .25 else D if depth < .8 else X3
        if (x + 1, y) not in m or (x + 2, y) not in m: col = L if depth > .15 else X2
        if (x - 1, y) not in m: col = K
        c.px(x, y, col)
    for x in range(ax0 - 1, ax1 + 2):
        c.px(x, ay1 + 1, W); c.px(x, ay1 + 2, L)
    for y in range(ay0 + 2, ay0 + 6): c.px(19, y, X3)                    # bell rope
    bell_rows = [
        "....KKK....",
        "...KYGGK...",
        "...KYGGmK..",
        "...KYGGmK..",
        "..KYGGGmK..",
        "..KYGGGGmK.",
        ".KYGGGGGmK.",
        ".KYGGGGGmdK",
        "KYYGGGGmmdK",
        "KKKKKKKKKKK",
        "....KdK....",
    ]
    paint(c, bell_rows, 19 - 4, ay0 + 6, {'K': K, 'Y': GOLD_L, 'G': GOLD, 'm': GOLD_M, 'd': GOLD_D})
    # -- shaft window: a dark slit with a lit right reveal and a sill
    wy = 88
    w = round_head(17, wy, 21, wy + 12)
    for x, y in w:
        col = K if y < wy + 4 else D
        if (x + 1, y) not in w: col = X2
        c.px(x, y, col)
    for x in range(16, 23): c.px(x, wy + 13, W); c.px(x, wy + 14, L)
    # -- door at the foot: boards in tone, shadowed head
    dx0, dx1, dy0, dy1 = 13, 26, 150, 175
    door = round_head(dx0, dy0, dx1, dy1)
    for x, y in door:
        col = M if (x - dx0) % 4 != 3 else X3
        if (x - 1, y) not in door: col = D
        if (x + 1, y) not in door: col = X2
        if y - dy0 < 3 and col != X2: col = D
        c.px(x, y, col)
    c.px(23, 164, GOLD)
    # -- finial
    paint(c, ["..K..", ".KYK.", "KYGmK", ".KmK.", "..K..", "..K.."], apex_x - 2, 0, {'K': K, 'Y': GOLD_L, 'G': GOLD, 'm': GOLD_M})
    # -- one-dot ink outline around the whole silhouette
    raw = Canvas(WD, HT); raw.im = c.im; raw.d = c.d; raw.outline(K)
    return c


if __name__ == '__main__':
    t = tower()
    lint(t, [K], 'tower')
    print('bbox', t.im.getbbox())
    t.native().save(os.path.join(OUT, 'bell-tower.png'))
    big = t.native().resize((WD * 2 * 3, HT * 2 * 3), Image.NEAREST)
    bg = Image.new('RGBA', big.size, (80, 70, 90, 255)); bg.alpha_composite(big); bg.save(os.path.join(OUT, 'sheet-tower.png'))
