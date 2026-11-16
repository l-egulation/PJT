"""Palette bench, handcart, tutorial bridge pier / crate / fence (sketch + color from one drawing)."""
import math
from px import *
from hub_well2 import CO, rstone
from hub_small import SK, ivy, stone_block


def board(c, P, x0, y0, x1, y1, shade=True):
    c.rect(x0, y0, x1, y1, fill=P['wood'])
    if shade:
        c.hl(x0 + 1, x1 - 1, y0 + 1, P['wood_hi']); c.hl(x0 + 1, x1 - 1, y1 - 1, P['wood_lo'])
    c.rect(x0, y0, x1, y1, line=P['in'])


def jar(c, P, x0, y0, paint_key):
    """jar 7 wide x 9 tall: lid, neck, paint-filled body"""
    c.rect(x0 + 1, y0, x0 + 5, y0 + 2, fill=P['wood_lo']); c.hl(x0 + 2, x0 + 4, y0 + 1, P['wood']); c.rect(x0 + 1, y0, x0 + 5, y0 + 2, line=P['in'])
    c.rect(x0, y0 + 2, x0 + 6, y0 + 8, fill=P[paint_key]); c.rect(x0, y0 + 2, x0 + 6, y0 + 8, line=P['in']); c.hl(x0 + 2, x0 + 4, y0 + 2, P['in'])
    c.vl(x0 + 1, y0 + 3, y0 + 7, P['cloth']); c.hl(x0 + 1, x0 + 5, y0 + 3, P['cloth_lo'])


# ---------------------------------------------------------------- 장인 작업대 palette-bench 106x88 -> 53x44
def palette_bench(P):
    c = Canvas(53, 44)
    for x0 in (3, 44):                                                   # legs
        board(c, P, x0, 23, x0 + 6, 43)
        c.vl(x0 + 2, 27, 40, P['wood_dk'])
    board(c, P, 9, 37, 44, 40)                                            # low stretcher
    # wooden palette tucked behind the table top (its top edge hides under the board: no doubled line)
    pal = smooth_mask(ellipse_mask(12, 22, 32, 33)) - ellipse_mask(24, 27, 26, 29)
    for x, y in pal: c.px(x, y, P['wood_hi'])
    for x, y in rings(pal)[0]: c.px(x, y, P['in'])
    for (x, y), key in (((15, 27), 'paint_r'), ((19, 26), 'paint_b'), ((21, 30), 'paint_g'), ((17, 29), 'paint_y')):
        c.rect(x, y, x + 1, y + 1, fill=P[key])
    board(c, P, 0, 19, 52, 24)                                            # top
    c.hl(10, 16, 21, P['wood_dk']); c.hl(22, 28, 22, P['wood_dk'])
    for i, key in enumerate(('paint_r', 'paint_y', 'paint_g', 'cloth_lo')):
        jar(c, P, 4 + i * 8, 11, key)
    c.rect(41, 13, 48, 19, fill=P['metal']); c.vl(42, 14, 18, P['metal_hi']); c.rect(41, 13, 48, 19, line=P['in'])
    c.rect(36, 16, 41, 19, fill=P['void']); c.rect(36, 16, 41, 19, line=P['in'])   # inkpot shares the brush cup's edge
    # cloth draped over the front edge
    paint(c, ["oooooooo", "oWWWWWLo", "oWWWWWLo", "oWWWWWLo", "oWWWWWLo", "oWWWWWLo", "oWWWWWLo", "oWWWWWLo",
              "oWrrWWLo", "oWrrWWLo", "oWWWWWLo", "oWWWWWLo", ".oWWWLo.", "..oWLo..", "...oo..."], 34, 23,
          {'o': P['in'], 'W': P['cloth'], 'L': P['cloth_lo'], 'r': P['paint_r']})
    c.outline(P['out'])
    for (xa, ya), (xb, yb) in (((43, 12), (41, 5)), ((45, 12), (46, 4)), ((47, 12), (50, 6))):   # brushes in the cup
        c.line([(xa, ya), (xb, yb)], P['wood_dk'])
        paint(c, [".o.", "oWo", "oWo", ".o."], xb - 1, yb - 4, {'o': P['in'], 'W': P['cloth']})
    lint(c, colors=[K, D], name='palette-bench'); return c


# ---------------------------------------------------------------- 손수레 handcart 96x63 -> 48x31
def wheel(c, P, cx, cy, r):
    """rim (outline, wood, outline), spokes, iron hub"""
    m = smooth_mask(ellipse_mask(int(cx - r), int(cy - r), int(cx + r), int(cy + r)))
    rr = rings(m)
    hole = set().union(*rr[3:]) if len(rr) > 3 else set()
    for x, y in rr[1]: c.px(x, y, P['wood'])
    for x, y in rr[0]: c.px(x, y, P['in'])
    for x, y in rr[2]: c.px(x, y, P['in'])
    for x, y in hole: c.px(x, y, CLEAR)
    ix, iy, R = round(cx), round(cy), int(r) - 2
    c.vl(ix, iy - R, iy + R, P['wood_lo']); c.hl(ix - R, ix + R, iy, P['wood_lo'])   # four straight spokes
    paint(c, [".o.", "oMo", ".o."], round(cx) - 1, round(cy) - 1, {'o': P['in'], 'M': P['metal_hi']})


def handcart(P):
    c = Canvas(48, 31)
    # cargo: tied grain sack, a rolled blanket, a small crate
    sack = smooth_mask(ellipse_mask(3, 0, 15, 15))
    for x, y in sack: c.px(x, y, P['cloth'])
    for x, y in rings(sack)[1]: c.px(x, y, P['cloth_lo'] if x > 10 else P['cloth'])
    for x, y in rings(sack)[0]: c.px(x, y, P['in'])
    paint(c, [".o.o.", "..o.."], 7, 1, {'o': P['in']})
    roll = smooth_mask(ellipse_mask(14, 1, 26, 9))
    for x, y in roll: c.px(x, y, P['leaf'])
    for x, y in rings(roll)[1]: c.px(x, y, P['leaf_hi'] if y < 5 else P['leaf_lo'])
    for x, y in rings(roll)[0]: c.px(x, y, P['in'])
    paint(c, [".oo.", "o..o", "o.oo", ".o.."], 22, 3, {'o': P['leaf_lo']})
    board(c, P, 26, 3, 33, 8)
    # bed
    board(c, P, 2, 8, 35, 19)
    for x in (12, 24): c.vl(x, 9, 18, P['wood_lo'])
    c.hl(3, 34, 13, P['wood_lo'])
    for x0 in (5, 30): c.vl(x0, 9, 18, P['metal']); c.vl(x0 + 1, 9, 18, P['metal_hi'])
    # handle reaching right
    c.line([(35, 17), (47, 15)], P['in']); c.line([(35, 18), (47, 16)], P['wood']); c.line([(35, 19), (47, 17)], P['in'])
    wheel(c, P, 11.5, 22.5, 8); wheel(c, P, 33.5, 25.5, 5)
    c.outline(P['out']); lint(c, colors=[K, D], name='handcart'); return c


# ---------------------------------------------------------------- 튜토리얼 다리 기둥 bridge-pier 54x90 -> 27x45
def bridge_pier(P):
    c = Canvas(27, 45)
    top = lambda x: [3, 2, 1, 1, 2, 3, 4, 4, 5, 6, 6, 5, 6, 7, 8, 9, 9][min(16, max(0, x - 4))]
    rows = [(y0, y1, xs) for y0, y1, xs in ((2, 9, [4, 13, 21]), (11, 18, [4, 10, 21]), (20, 27, [4, 15, 21]), (29, 36, [4, 9, 21]))]
    stone_block(c, P, 4, 0, 21, 37, rows)
    for x in range(4, 22):                                               # broken top edge
        for y in range(0, top(x)): c.px(x, y, CLEAR)
    stone_block(c, P, 0, 37, 26, 44, [(38, 44, [0, 9, 18, 26])])
    ivy(c, P, [(1, 34), (19, 35), (12, 36)])
    c.outline(P['out']); lint(c, colors=[K, D], name='bridge-pier'); return c


# ---------------------------------------------------------------- 튜토리얼 상자 crate 52x64 -> 26x32
def crate(P):
    c = Canvas(26, 32)
    c.rect(0, 0, 25, 31, fill=P['wood_lo'])
    for x in (6, 12, 18): c.vl(x, 3, 28, P['in'])                         # plank seams
    for x in (2, 8, 14, 20): c.vl(x, 3, 28, P['wood'])
    board(c, P, 0, 0, 25, 3); board(c, P, 0, 28, 25, 31)                 # rails
    board(c, P, 0, 0, 3, 31); board(c, P, 22, 0, 25, 31)                 # stiles
    for k in range(0, 21):                                               # diagonal brace, edge-face-face-edge
        x, y = 3 + k, 26 - k
        if 3 <= x <= 22 and 3 <= y <= 28:
            c.px(x, y, P['in']); c.px(x, y - 1, P['wood_hi']); c.px(x, y - 2, P['wood_hi']); c.px(x, y - 3, P['in'])
    for x, y in ((1, 1), (24, 1), (1, 30), (24, 30)): c.px(x, y, P['metal'])
    c.outline(P['out']); lint(c, colors=[K, D], name='crate'); return c


# ---------------------------------------------------------------- 튜토리얼 울타리 fence 48x52 -> 24x26
def fence(P):
    c = Canvas(24, 26)
    for y0 in (5, 12, 19): board(c, P, 3, y0, 20, y0 + 3)
    for x0 in (0, 20):
        board(c, P, x0, 0, x0 + 3, 25, shade=False); c.vl(x0 + 1, 1, 24, P['wood_hi'])
        c.px(x0, 0, CLEAR); c.px(x0 + 3, 0, CLEAR)
    c.outline(P['out']); lint(c, colors=[K, D], name='fence'); return c


if __name__ == '__main__':
    import os
    os.makedirs('out/hub', exist_ok=True)
    for name, fn in (('palette-bench', palette_bench), ('handcart', handcart), ('bridge-pier', bridge_pier), ('crate', crate), ('fence', fence)):
        fn(SK).native().save(f'out/hub/{name}-sketch.png'); fn(CO).native().save(f'out/hub/{name}-color.png')
