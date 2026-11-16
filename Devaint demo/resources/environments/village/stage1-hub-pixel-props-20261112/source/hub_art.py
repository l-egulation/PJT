"""Village / tutorial props on the 2x2 grid. Each is drawn once with material names and rendered twice:
SKETCH (colour drained: terrain cream palette) and COLOR (the village after its colour returns)."""
from px import *

SKETCH = {
    'wood_hi': W, 'wood': C, 'wood_lo': L, 'wood_dk': M,
    'stone_hi': W, 'stone': C, 'stone_lo': L,
    'roof_hi': C, 'roof': L, 'roof_lo': M,
    'metal': M, 'metal_hi': L, 'rope': L, 'rope_lo': M,
    'leaf_hi': W, 'leaf': L, 'leaf_lo': M,
    'cloth': W, 'cloth_lo': L, 'paper': W, 'ink': D,
    'paint_r': M, 'paint_y': L, 'paint_b': M, 'paint_g': L,
    'in': D, 'out': K, 'void': D,
}
COLOR = {
    'wood_hi': h('d9a46a'), 'wood': h('b4783f'), 'wood_lo': h('8a5a33'), 'wood_dk': h('5c3a22'),
    'stone_hi': h('fbf1df'), 'stone': h('ead8b8'), 'stone_lo': h('cdb38c'),
    'roof_hi': h('f09a6a'), 'roof': h('d9663f'), 'roof_lo': h('a8452c'),
    'metal': h('474c52'), 'metal_hi': h('7b8288'), 'rope': h('d1ac72'), 'rope_lo': h('a5814b'),
    'leaf_hi': h('a8bd5c'), 'leaf': h('7c9440'), 'leaf_lo': h('536a2e'),
    'cloth': h('f3ead6'), 'cloth_lo': h('d8c8a6'), 'paper': h('f6eedc'), 'ink': h('3b3226'),
    'paint_r': h('c8352c'), 'paint_y': h('e6a92e'), 'paint_b': h('3d5ea8'), 'paint_g': h('3f8a4f'),
    'in': h('5c3a22'), 'out': K, 'void': h('3b2a1e'),
}


def stones(c, P, x0, y0, x1, y1, course=8, widths=(11, 13, 10, 12), offset=0):
    """a stone block face with staggered joints; 1-dot joints in the interior line colour"""
    c.rect(x0, y0, x1, y1, fill=P['stone'])
    k = 0
    for i, y in enumerate(range(y0, y1 + 1, course)):
        yb = min(y + course - 1, y1)
        c.hl(x0, x1, yb, P['in']) if yb < y1 else None
        c.hl(x0, x1, y, P['stone_hi'])
        if yb - 1 > y: c.hl(x0, x1, yb - 1, P['stone_lo'])
        x = x0 + ((offset + i) % 2) * 5
        while x < x1:
            w = widths[k % len(widths)]; k += 1
            x += w
            if x < x1:
                c.vl(x, y, yb, P['in'])
                if x + 1 <= x1: c.vl(x + 1, y + 1, yb - 1, P['stone_hi'])


def wood_post(c, P, x0, y0, x1, y1):
    c.rect(x0, y0, x1, y1, fill=P['wood'])
    c.vl(x0, y0, y1, P['wood_hi']); c.vl(x1, y0, y1, P['wood_lo']); c.vl(x1 - 1, y0 + 1, y1, P['wood_lo'])
    c.hl(x0, x1, y0, P['wood_hi'])
    for y in range(y0 + 6, y1 - 3, 11): c.vl(x0 + 2 + (y // 11) % 2, y, y + 3, P['wood_dk'])
    c.rect(x0, y0, x1, y1, line=P['in'])


def tuft(c, P, x, y, flip=False):
    rows = ["..o.o..", ".oGoGo.", "oGgoGgo"]
    if flip: rows = [r[::-1] for r in rows]
    paint(c, rows, x, y, {'o': P['leaf_lo'], 'G': P['leaf'], 'g': P['leaf_hi']})


# ---------------------------------------------------------------- 우물 return-well 127x138 -> 63x69
def well(P):
    c = Canvas(63, 69)
    # cross beam (behind the posts), rounded ends
    c.rect(0, 5, 62, 10, fill=P['wood'])
    c.hl(1, 61, 5, P['wood_hi']); c.hl(0, 62, 9, P['wood_lo']); c.hl(0, 62, 10, P['wood_dk'])
    for x, y in ((0, 5), (62, 5), (0, 10), (62, 10)): c.px(x, y, CLEAR)
    c.rect(0, 5, 62, 10, line=P['in'])
    for x, y in ((0, 5), (62, 5), (0, 10), (62, 10)): c.px(x, y, CLEAR)
    c.px(9, 7, P['wood_dk']); c.px(53, 7, P['wood_dk'])                         # pegs
    # diagonal braces between posts and beam (edge, face, edge)
    for x0, d in ((14, 1), (48, -1)):
        for k in range(6):
            x = x0 + d * k; y = 17 - k
            c.px(x, y, P['in']); c.px(x, y - 1, P['wood_lo']); c.px(x, y - 2, P['wood']); c.px(x, y - 3, P['in'])
    # posts
    wood_post(c, P, 5, 1, 13, 67); wood_post(c, P, 49, 1, 57, 67)
    c.hl(5, 13, 4, P['in']); c.hl(49, 57, 4, P['in'])
    # windlass drum wound with rope
    c.rect(26, 3, 35, 11, fill=P['rope'])
    for x in (27, 29, 31, 33): c.vl(x, 4, 10, P['rope_lo'])
    c.hl(26, 35, 3, P['in']); c.hl(26, 35, 11, P['in']); c.vl(26, 3, 11, P['in']); c.vl(35, 3, 11, P['in'])
    # rope and hook block
    c.vl(30, 12, 15, P['rope_lo'])
    c.rect(28, 16, 33, 20, fill=P['metal']); c.hl(29, 32, 17, P['metal_hi'])
    # bucket handle (one-dot arc) and bucket
    c.line([(25, 29), (27, 23), (30, 21)], P['metal']); c.line([(31, 21), (34, 23), (37, 29)], P['metal'])
    for y in range(28, 43):
        inset = (y - 28) // 7
        c.hl(24 + inset, 40 - inset, y, P['wood'])
        c.px(24 + inset, y, P['wood_hi']); c.px(40 - inset, y, P['wood_lo']); c.px(39 - inset, y, P['wood_lo'])
    c.hl(25, 39, 28, P['void']); c.hl(24, 40, 27, P['in'])
    for y in range(28, 43):
        inset = (y - 28) // 7
        c.px(24 + inset, y, P['in']); c.px(40 - inset, y, P['in'])
    c.hl(26, 38, 42, P['in'])
    for x in (29, 34): c.vl(x, 30, 41, P['wood_lo'])
    for yb in (31, 39): c.hl(24 + (yb - 28) // 7, 40 - (yb - 28) // 7, yb, P['metal']); c.hl(25, 26, yb, P['metal_hi'])
    # stone curb in front of the posts
    stones(c, P, 10, 43, 52, 58, course=8)
    stones(c, P, 3, 60, 59, 68, course=9, widths=(12, 10, 13, 11), offset=1)
    c.rect(10, 42, 52, 59, line=P['in']); c.hl(3, 59, 59, P['in'])
    c.vl(3, 59, 68, P['in']); c.vl(59, 59, 68, P['in'])
    # grass at the foot
    tuft(c, P, 1, 62); tuft(c, P, 52, 62, True); tuft(c, P, 38, 63); tuft(c, P, 13, 64, True)
    c.outline(P['out'])
    lint(c, colors=[K, D], name='well')
    return c


if __name__ == '__main__':
    import os
    os.makedirs('out/hub', exist_ok=True)
    for name, P in (('sketch', SKETCH), ('color', COLOR)):
        well(P).native().save(f'out/hub/return-well-{name}.png')
