"""Village props: low wall, departure sign, record desk (sketch + color from one drawing)."""
from px import *
import hub_art as HA
from hub_well2 import SK as _SK, CO, rstone, course, post
SK = dict(_SK, leaf_hi=C, leaf=L, leaf_lo=M, void=M)   # sketch leaves: grey leaf with a darker rim (W/C leaves read as noise)

LEAF = [".D.D.", "DgDGD", "DggGD", ".DGD.", "..D.."]


def ivy(c, P, spots):
    for k, (x, y) in enumerate(spots):
        rows = LEAF if k % 2 else [r[::-1] for r in LEAF]
        paint(c, rows, x, y, {'D': P['leaf_lo'], 'G': P['leaf'], 'g': P['leaf_hi']})


def stone_block(c, P, x0, y0, x1, y1, rows):
    """stacked rounded stones: rows = [(y_top, y_bottom, [joint xs incl. edges])]"""
    c.rect(x0, y0, x1, y1, fill=P['in'])
    for ya, yb, xs in rows:
        for a, b in zip(xs, xs[1:]):
            rstone(c, P, a + 1, ya, b - 1, yb)


# ---------------------------------------------------------------- 낮은 담 low-wall 91x48 -> 45x24
def low_wall(P):
    c = Canvas(45, 24)
    stone_block(c, P, 10, 4, 34, 23, [(5, 10, [10, 16, 23, 29, 34]), (12, 17, [10, 14, 21, 28, 34]), (19, 23, [10, 18, 25, 34])])
    for x0 in (1, 33):                                                   # end pillars, a stone higher, with caps
        stone_block(c, P, x0, 0, x0 + 10, 23, [(1, 4, [x0, x0 + 10]), (6, 11, [x0, x0 + 10]), (13, 17, [x0, x0 + 5, x0 + 10]), (19, 23, [x0, x0 + 10])])
    ivy(c, P, [(36, 6), (40, 10), (35, 12), (39, 16), (33, 19), (28, 20), (12, 20)])
    c.outline(P['out']); lint(c, colors=[K, D], name='low-wall'); return c


# ---------------------------------------------------------------- 출발 이정표 departure-sign 63x84 -> 31x42
def departure_sign(P):
    c = Canvas(31, 42)
    # arrow board pointing right (toward the departure lane)
    arrow = {(x, y) for x in range(12, 31) for y in range(0, 18)
             if (x <= 22 and 4 <= y <= 13) or (x > 22 and abs(y - 8.5) <= (30.5 - x))}
    arrow = smooth_mask(arrow)
    for x, y in arrow: c.px(x, y, P['wood'])
    for x, y in rings(arrow)[1]: c.px(x, y, P['wood_hi'] if y < 9 else P['wood_lo'])
    for x, y in rings(arrow)[0]: c.px(x, y, P['in'])
    c.hl(16, 22, 8, P['wood_dk']); c.hl(25, 27, 9, P['wood_dk'])          # grain
    # post with two iron bands
    post(c, P, 6, 12, 1, 29)
    for y0 in (4, 23):
        c.rect(5, y0, 13, y0 + 3, fill=P['metal']); c.hl(6, 12, y0 + 1, P['metal_hi']); c.rect(5, y0, 13, y0 + 3, line=P['in'])
    # stone base
    stone_block(c, P, 3, 28, 17, 41, [(29, 34, [3, 17]), (36, 41, [3, 10, 17])])
    stone_block(c, P, 0, 35, 3, 41, [(36, 41, [0, 3])])
    ivy(c, P, [(16, 30), (20, 34), (15, 36)])
    c.outline(P['out']); lint(c, colors=[K, D], name='departure-sign'); return c


# ---------------------------------------------------------------- 기록 책상 record-desk 93x71 -> 46x35
def record_desk(P):
    c = Canvas(46, 35)
    # legs with splayed feet
    for x0 in (2, 38):
        c.rect(x0, 21, x0 + 4, 34, fill=P['wood']); c.vl(x0 + 1, 22, 33, P['wood_hi']); c.vl(x0 + 3, 22, 33, P['wood_lo'])
        c.rect(x0, 21, x0 + 4, 34, line=P['in'])
        c.rect(x0 - 1, 31, x0 + 5, 34, fill=P['wood_lo']); c.rect(x0 - 1, 31, x0 + 5, 34, line=P['in'])
    # apron with a drawer
    c.rect(6, 21, 38, 27, fill=P['wood_lo']); c.hl(7, 37, 22, P['wood']); c.rect(6, 21, 38, 27, line=P['in'])
    c.rect(27, 23, 36, 26, fill=P['wood']); c.rect(27, 23, 36, 26, line=P['in']); c.px(31, 24, P['metal']); c.px(32, 24, P['metal'])
    # table top
    c.rect(0, 16, 45, 21, fill=P['wood']); c.hl(1, 44, 17, P['wood_hi']); c.hl(1, 44, 20, P['wood_lo'])
    c.hl(8, 14, 18, P['wood_dk']); c.hl(26, 33, 19, P['wood_dk'])
    c.rect(0, 16, 45, 21, line=P['in'])
    # stack of two books
    c.rect(4, 11, 12, 13, fill=P['wood_lo']); c.hl(5, 11, 12, P['paper']); c.rect(4, 11, 12, 13, line=P['in'])
    c.rect(3, 13, 12, 16, fill=P['leaf']); c.hl(4, 11, 14, P['leaf_hi']); c.rect(3, 13, 12, 16, line=P['in']); c.vl(5, 14, 15, P['leaf_lo'])
    # open ledger: two pages lifting from the spine, a few ruled lines
    paint(c, ["..pppppppp.pppppppp..", ".pPPPPPPPPpPPPPPPPPp.", "pPPPPPPPPPpPPPPPPPPPp"], 14, 13, {'p': P['in'], 'P': P['paper']})
    for x0 in (17, 26): c.hl(x0, x0 + 5, 15, P['cloth_lo'])
    # ink pot and a big quill
    c.rect(36, 12, 40, 16, fill=P['metal']); c.hl(37, 39, 13, P['metal_hi']); c.rect(36, 12, 40, 16, line=P['in'])
    c.hl(37, 39, 11, P['in'])
    paint(c, ["....oo",
              "...oWo",
              "..oWWo",
              "..oWLo",
              ".oWWo.",
              ".oWLo.",
              "oWLo..",
              "oLo...",
              "oo....",
              "o....."], 38, 1, {'o': P['in'], 'W': P['paper'], 'L': P['cloth_lo']})
    c.outline(P['out']); lint(c, colors=[K, D], name='record-desk'); return c


if __name__ == '__main__':
    import os
    os.makedirs('out/hub', exist_ok=True)
    for name, fn in (('low-wall', low_wall), ('departure-sign', departure_sign), ('record-desk', record_desk)):
        fn(SK).native().save(f'out/hub/{name}-sketch.png'); fn(CO).native().save(f'out/hub/{name}-color.png')
