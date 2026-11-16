"""8-frame animations on the same grid: exit grate rising, altar build-up, gems going dark,
acquire (gold ink ribbon) and discard (black ink ribbon breaking up)."""
import math, os
from PIL import Image
from px import *
import s_exit2, s_rest

OUT = 'out/anim'
os.makedirs(OUT, exist_ok=True)
GOLD_HI, GOLD, GOLD_LO = h('fbe7b0'), h('f0c860'), h('c8962a')
GREY_HI = h('8c8a85')


def save(c, name):
    c.native().save(f'{OUT}/{name}.png')


# ---------------------------------------------------------------- exit: grate hauled up (ease in-out)
def exit_open():
    bottoms = [74, 71, 64, 54, 42, 31, 22, 17]
    drips = {4: [(16, 22), (40, 30)], 5: [(16, 30), (40, 40), (28, 20)], 6: [(16, 40), (28, 28), (46, 24)], 7: [(28, 38), (46, 34)]}
    for i, gb in enumerate(bottoms):
        save(s_exit2.draw(False, gb, drips.get(i, ())), f'exit-open-{i + 1:02d}')


# ---------------------------------------------------------------- altar
def recut(c, top):
    """keep rows >= top; the new top edge gets the silhouette ink"""
    for y in range(top):
        for x in range(c.w): c.px(x, y, CLEAR)
    for x in range(c.w):
        if top < c.h and c.get(x, top)[3]: c.px(x, top, K)
    return c


def altar_appear():
    tops = [20, 18, 16, 13, 10, 7, 4, 0]
    for i, t in enumerate(tops):
        c = s_rest.altar(False)
        recut(c, t)
        if 1 <= i <= 4:                                                # dust kicked up at the base
            for x, y in ((-1, 18), (24, 18), (-2, 16), (25, 15))[: 2 + (i > 2) * 2]:
                c.px(max(0, min(23, x)), y - (i % 2), L)
        save(c, f'altar-appear-{i + 1:02d}')


def lerp(a, b, t):
    return tuple(int(round(a[k] + (b[k] - a[k]) * t)) for k in range(3)) + (255,)


def altar_use():
    for i in range(8):
        t = i / 7
        c = s_rest.altar(False)
        for g, gem in enumerate(s_rest.GEMS):
            x = 5 + g * 3
            # gems go out one after another, left to right, with a short flash before each dims
            local = max(0.0, min(1.0, t * 1.6 - g * .15))
            col = lerp(gem, INK, local)
            hi = lerp(s_rest.lighten(gem), D, local)
            if 0 < local < .35: col, hi = GOLD_HI, W
            c.rect(x, 4, x + 1, 5, fill=col); c.px(x, 4, hi)
        save(c, f'altar-use-{i + 1:02d}')


# ---------------------------------------------------------------- acquire / discard ribbons (32x32 logical = 64x64)
def ribbon_path(head, tail, amp=6.0, phase=0.0, turns=2.4):
    pts = []
    n = 160
    for k in range(n + 1):
        t = k / n
        if tail <= t <= head:
            x = 16 + amp * (0.55 + 0.45 * t) * math.sin(t * math.pi * turns + phase)
            y = 30 - t * 26
            pts.append((int(round(x)), int(round(y))))
    out = []
    for p in pts:
        if not out or out[-1] != p: out.append(p)
    return out


SPARK = ["..o..", ".oho.", "ohHho", ".oho.", "..o.."]


def ribbon(frame, palette, broken):
    hi, mid, lo = palette
    c = Canvas(32, 32)
    head = min(1.0, (frame + 1) / 4.0)
    tail = max(0.0, (frame - 2.5) / 4.2)
    main = ribbon_path(head, tail)
    thin = ribbon_path(head * .92, min(1.0, tail * 1.1), amp=5.0, phase=math.pi * .85)
    if broken and frame >= 4:                                          # discard: the ribbon snaps into pieces
        main = [p for k, p in enumerate(main) if (k // 4) % 2 == 0]
        thin = [p for k, p in enumerate(thin) if (k // 3) % 2 == 1]
    body = {(x + dx, y + dy) for x, y in main for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    for x, y in body: c.px(x, y, lo)
    for x, y in main: c.px(x, y, mid)
    for x, y in main[::5]: c.px(x, y, hi)
    if main: c.px(*main[-1], hi)
    c.outline(K)
    for x, y in thin:                                                   # a fine light strand winding round it
        if c.get(x, y)[3] == 0: c.px(x, y, mid if broken else hi)
    # sparkles once the ribbon starts to rise away (drawn after the outline: light, no ink)
    spots = [(5, 18), (24, 12), (8, 7), (25, 24), (14, 3), (20, 20)]
    for k, (x, y) in enumerate(spots):
        age = frame - 3 - k // 2
        if 0 <= age <= 2:
            if broken:
                c.px(x, y - age, GREY_HI); c.px(x + 1, y - age + 1, D)
            else:
                paint(c, SPARK[1:4] if age == 1 else [".h.", "hHh", ".h."] if age == 0 else ["H"], x - 1, y - 1 - age,
                      {'o': GOLD_LO, 'h': GOLD, 'H': GOLD_HI})
    return c


def acquire_discard():
    for i in range(8):
        save(ribbon(i, (GOLD_HI, GOLD, GOLD_LO), False), f'altar-choice-acquire-{i + 1:02d}')
        save(ribbon(i, (GREY_HI, D, INK), True), f'altar-choice-discard-{i + 1:02d}')


if __name__ == '__main__':
    exit_open(); altar_appear(); altar_use(); acquire_discard()
