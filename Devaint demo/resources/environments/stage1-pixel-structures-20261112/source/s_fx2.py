"""Acquire / discard redrawn around the ink drop the altar cards show.
acquire (UI, rises from the taken card's drop): lift, swell, burst into a ring of gold, motes drift up.
discard (world, one per unpicked candidate above the altar): wobble, sag, melt into drips that fall and vanish."""
import math
from px import *

GOLD_HI, GOLD, GOLD_LO = h('fbe7b0'), h('f0c860'), h('c8962a')
GREY_HI = h('8c8a85')


def drop_mask(cx, cy, r, tip, sx=1.0, sy=1.0):
    """teardrop: round body centred (cx, cy), pointed tip `tip` dots above it"""
    m = set()
    for x in range(32):
        for y in range(32):
            X, Y = (x + .5 - cx) / sx, (y + .5 - cy) / sy
            if X * X + Y * Y <= r * r: m.add((x, y))
            elif -r - tip <= Y < 0 and abs(X) <= r * (Y + r + tip) / (r + tip) * .9: m.add((x, y))
    return smooth_mask(m)


def fill_drop(c, m, hi, mid, lo):
    xs = [x for x, _ in m]; ys = [y for _, y in m]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    for x, y in m: c.px(x, y, mid)
    for x, y in m:
        if x > cx + 1 and y > cy: c.px(x, y, lo)
    gx, gy = int(cx - 2), int(cy)
    if (gx, gy) in m: c.px(gx, gy, hi)
    if (gx, gy - 1) in m: c.px(gx, gy - 1, hi)


def ring(c, cx, cy, R, n, col, size):
    for k in range(n):
        a = k / n * math.pi * 2 + .3
        x, y = int(round(cx + math.cos(a) * R)), int(round(cy + math.sin(a) * R * .85))
        if size >= 2:
            paint(c, [".h.", "hHh", ".h."], x - 1, y - 1, {'h': col[1], 'H': col[0]})
        else:
            c.px(x, y, col[0])


def acquire(f):
    c = Canvas(32, 32)
    pal = (GOLD_HI, GOLD, GOLD_LO)
    drops = {0: (16, 22, 3.0, 2), 1: (16, 18, 3.4, 3), 2: (16, 15, 4.2, 2), 3: (16, 14, 4.6, 1)}
    if f in drops:
        cx, cy, r, tip = drops[f]
        sx, sy = (1.15, .85) if f == 3 else (1, 1)
        m = drop_mask(cx, cy, r, tip, sx, sy)
        fill_drop(c, m, *pal); c.outline(K)
        if f == 1: c.px(16, 25, GOLD); c.px(16, 27, GOLD_LO)                # trail left on the card
    if f == 4:
        paint(c, ["...H...", "...h...", ".h.h.h.", "HhhHhhH", ".h.h.h.", "...h...", "...H..."], 13, 11, {'h': GOLD, 'H': GOLD_HI})
        ring(c, 16, 14, 7, 8, pal, 2)
    if f == 5:
        paint(c, [".h.", "hHh", ".h."], 15, 13, {'h': GOLD, 'H': GOLD_HI})
        ring(c, 16, 13, 11, 8, pal, 2)
    if f == 6:
        ring(c, 16, 12, 13, 8, pal, 1)
        for x, y in ((12, 6), (20, 4), (16, 2)): c.px(x, y, GOLD)
    if f == 7:
        for x, y in ((12, 3), (20, 1), (16, 0), (9, 8)): c.px(x, y, GOLD_HI)
    return c


def discard(f):
    c = Canvas(32, 32)
    pal = (GREY_HI, D, INK)
    if f <= 3:
        shapes = {0: (16, 12, 4.0, 2, 1, 1), 1: (16, 12, 4.0, 2, 1.2, .8), 2: (16, 13, 4.0, 1, .85, 1.2), 3: (16, 15, 3.6, 0, .8, 1.45)}
        cx, cy, r, tip, sx, sy = shapes[f]
        m = drop_mask(cx, cy, r, tip, sx, sy)
        fill_drop(c, m, *pal)
        if f >= 2: c.line([(15, int(cy) - 2), (17, int(cy) + 1)], GREY_HI)    # crack
        c.outline(K)
    falls = {4: [(13, 18, 2.0), (19, 20, 1.6), (16, 23, 1.2)],
             5: [(13, 23, 1.6), (19, 25, 1.3), (16, 28, 1.0)],
             6: [(13, 28, 1.1), (19, 30, .8)],
             7: []}
    if f in falls:
        for x, y, r in falls[f]:
            m = smooth_mask(drop_mask(x, y, r, 1))
            fill_drop(c, m, *pal)
        c.outline(K)
        smoke = {4: [(10, 12), (22, 11)], 5: [(9, 9), (23, 8), (16, 10)], 6: [(9, 6), (23, 5), (16, 7)], 7: [(16, 4), (22, 3)]}
        for x, y in smoke[f]: c.px(x, y, GREY_HI)
    return c


if __name__ == '__main__':
    for i in range(8):
        acquire(i).native().save(f'out/anim/altar-choice-acquire-{i + 1:02d}.png')
        discard(i).native().save(f'out/anim/altar-choice-discard-{i + 1:02d}.png')
