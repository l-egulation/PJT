"""Hand-drawn handcart and palette bench (2nd pass, 2026-11-12): follow the approved art's shapes on the 2x2 grid
instead of the 10-colour reduction (cart) and the open-frame bench."""
from px import *
import hub_small as HS, hub_small2 as HS2

SK = dict(HS.SK, sack_hi=W, sack=W, sack_lo=L, clay_hi=W, clay=C, clay_lo=L, bristle=W, bristle_lo=L, apron=L)
CO = dict(HS2.CO, sack_hi=h('f3dcb2'), sack=h('e2bd8a'), sack_lo=h('c39560'), clay_hi=h('e0d2bf'), clay=h('c4b2a0'), clay_lo=h('9c8a78'),
          bristle=h('ead9b8'), bristle_lo=h('c2a77c'), apron=h('6e4528'), paint_g=h('6f8a3a'))


def wheel(c, P, x0, y0, d):
    """rim: ink, wood, darker wood; shadowed inside with eight 1-dot wooden spokes; iron hub"""
    m = smooth_mask(ellipse_mask(x0, y0, x0 + d - 1, y0 + d - 1))
    rr = rings(m)
    cx, cy = x0 + (d - 1) / 2, y0 + (d - 1) / 2
    inner = set().union(*rr[3:]) if len(rr) > 3 else set()
    for x, y in rr[0]: c.px(x, y, P['in'])
    for x, y in rr[1]: c.px(x, y, P['wood_hi'] if (y < cy and x < cx) else P['wood'])
    for x, y in rr[2]: c.px(x, y, P['wood_lo'])
    for x, y in inner:
        dx, dy = x - cx, y - cy
        spoke = abs(dx) < .6 or abs(dy) < .6 or abs(abs(dx) - abs(dy)) < .6
        c.px(x, y, P['wood'] if spoke else P['void'])
    ix, iy = int(cx), int(cy)
    if d % 2:
        paint(c, [".o.", "oMo", ".o."], ix - 1, iy - 1, {'o': P['in'], 'M': P['metal_hi']})
    else:
        paint(c, [".oo.", "oMmo", "omMo", ".oo."], ix - 1, iy - 1, {'o': P['in'], 'M': P['metal'], 'm': P['metal_hi']})


SACK = ["...oo...oo...",
        "..oHSo.oSso..",
        "...oSSoSso...",
        "....oSSSo....",
        "...oRRRRRo...",
        "..oHSSSSSso..",
        ".oHSSSSSSSso.",
        "oHSSSSsSSSSso",
        "oHSSSSsSSSsso",
        "oHSSSSSsSSsso",
        "oSSSSSSsSSsso",
        "oSSSSSSSSssso",
        "oSSSSSSSsssso"]


def handcart(P):
    c = Canvas(48, 31)
    # a plank box on the right of the bed
    c.rect(24, 6, 36, 11, fill=P['wood']); c.hl(25, 35, 7, P['wood_hi']); c.rect(24, 6, 36, 11, line=P['in'])
    c.vl(30, 7, 10, P['wood_lo'])
    # rolled blanket lying across: body, strap, end disc with the roll's spiral
    c.rect(14, 2, 25, 11, fill=P['leaf']); c.hl(14, 25, 3, P['leaf_hi']); c.hl(14, 25, 10, P['leaf_lo'])
    c.hl(14, 25, 2, P['in']); c.hl(14, 25, 11, P['in'])
    c.vl(18, 3, 10, P['wood']); c.vl(19, 3, 10, P['wood_lo'])
    end = smooth_mask(ellipse_mask(22, 2, 29, 11))
    for x, y in end: c.px(x, y, P['leaf_hi'])
    for x, y in rings(end)[0]: c.px(x, y, P['in'])
    paint(c, ["..oo..", ".o..o.", ".o.o.o", ".o..o.", "..oo.."], 23, 4, {'o': P['leaf_lo']})
    # tied grain sack
    paint(c, SACK, 3, 0, {'o': P['in'], 'H': P['sack_hi'], 'S': P['sack'], 's': P['sack_lo'], 'R': P['rope_lo']})
    # the bed: top rail and side board, iron corner straps
    c.rect(1, 11, 37, 21, fill=P['wood'])
    c.hl(2, 36, 12, P['wood_hi']); c.hl(1, 37, 13, P['in']); c.hl(2, 36, 14, P['wood_hi']); c.hl(2, 36, 20, P['wood_lo'])
    c.rect(1, 11, 37, 21, line=P['in'])
    for x0 in (4, 31):
        c.rect(x0, 11, x0 + 2, 19, fill=P['metal']); c.vl(x0 + 1, 12, 18, P['metal_hi']); c.rect(x0, 11, x0 + 2, 19, line=P['in'])
    c.hl(20, 24, 17, P['wood_lo']); c.hl(26, 28, 16, P['wood_lo'])
    # handle reaching right, the grip end lifted a little
    c.hl(38, 44, 16, P['in']); c.hl(38, 44, 17, P['wood']); c.hl(38, 44, 18, P['in'])
    paint(c, ["..ooo", ".oWWo", "oWWo.", "ooo.."], 43, 15, {'o': P['in'], 'W': P['wood']})
    wheel(c, P, 3, 15, 16); wheel(c, P, 26, 19, 12)
    c.outline(P['out']); lint(c, colors=[K, P['in']], name='handcart'); return c


def jar(c, P, x0, y0, paint_key, w=7, hgt=9):
    """rounded jar: dark lid, shouldered body filled with paint, a glint on the left"""
    body = smooth_mask({(x, y) for x in range(x0, x0 + w) for y in range(y0 + 2, y0 + hgt)})
    for x, y in body: c.px(x, y, P[paint_key])
    c.vl(x0 + 1, y0 + 4, y0 + hgt - 3, P['cloth'])
    for x, y in rings(body)[0]: c.px(x, y, P['in'])
    c.rect(x0 + 1, y0, x0 + w - 2, y0 + 2, fill=P['wood_lo']); c.hl(x0 + 2, x0 + w - 3, y0 + 1, P['wood'])
    c.rect(x0 + 1, y0, x0 + w - 2, y0 + 2, line=P['in'])


def brush(c, P, x_head, y_head, x_base, y_base):
    """3-dot handle (ink, wood, ink) stepping from the head down into the cup, ferrule and round bristle head"""
    top = y_head + 6
    n = y_base - top
    for k in range(n + 1):
        y = top + k
        x = round(x_head + (x_base - x_head) * k / max(1, n))
        c.px(x - 1, y, P['in']); c.px(x, y, P['wood_dk']); c.px(x + 1, y, P['in'])
    paint(c, [".ooo.", "oBBbo", "oBBbo", "oBbbo", ".oMo.", ".oMo."], x_head - 2, y_head,
          {'o': P['in'], 'B': P['bristle'], 'b': P['bristle_lo'], 'M': P['metal_hi']})


def palette_bench(P):
    c = Canvas(53, 44)
    # legs with wider feet, stretcher between them
    for x0 in (3, 43):
        c.rect(x0, 23, x0 + 7, 43, fill=P['wood']); c.vl(x0 + 1, 24, 42, P['wood_hi']); c.vl(x0 + 6, 24, 42, P['wood_lo'])
        c.vl(x0 + 3, 28, 35, P['wood_lo'])
        c.rect(x0, 23, x0 + 7, 43, line=P['in'])
        c.rect(x0 - 1, 40, x0 + 8, 43, fill=P['wood_lo']); c.hl(x0, x0 + 7, 41, P['wood']); c.rect(x0 - 1, 40, x0 + 8, 43, line=P['in'])
    c.rect(10, 36, 43, 39, fill=P['wood']); c.hl(11, 42, 37, P['wood_hi']); c.rect(10, 36, 43, 39, line=P['in'])
    # apron under the top (in its shadow)
    c.rect(10, 23, 43, 28, fill=P['apron']); c.rect(10, 23, 43, 28, line=P['in'])
    # table top with rounded ends
    c.rect(0, 19, 52, 24, fill=P['wood']); c.hl(1, 51, 20, P['wood_hi']); c.hl(1, 51, 23, P['wood_lo'])
    c.hl(8, 14, 21, P['wood_lo']); c.hl(22, 28, 22, P['wood_lo'])
    c.rect(0, 19, 52, 24, line=P['in'])
    for x, y in ((0, 19), (52, 19), (0, 24), (52, 24)): c.px(x, y, CLEAR)
    for x, y in ((1, 20), (51, 20), (1, 23), (51, 23)): c.px(x, y, P['in'])
    # wooden palette hung on the apron, four paint dabs
    pal = smooth_mask(ellipse_mask(13, 26, 32, 34))
    for x, y in pal: c.px(x, y, P['wood_hi'])
    for x, y in rings(pal)[1]: c.px(x, y, P['wood'] if y > 30 else P['wood_hi'])
    for x, y in rings(pal)[0]: c.px(x, y, P['in'])
    for (x, y), key, rows in (((17, 28), 'paint_r', ["XX.", "XXX", ".XX"]), ((22, 27), 'paint_b', ["XXX", ".XX"]),
                              ((23, 31), 'paint_g', ["XXX", "XX."]), ((28, 28), 'cloth', ["XX.", "XXX"])):
        paint(c, rows, x, y, {'X': P[key]})
    # cloth hanging over the front edge, pointed hem, a red cross
    paint(c, ["oooooooooo", "oWWWWWWWLo", "oWWWWWWWLo", "oWWWWWWWLo", "oWWWWWWWLo", "oWWWWWWWLo", "oWWWWWWWLo", "oWWWWWrWLo",
              "oWWWWrrrLo", "oWWWWWrWLo", "oWWWWWWWLo", "oWWWWWWWLo", "oWWWWWWWLo", ".oWWWWWWLo", "..oWWWWLo.", "...oWWLo..", "....ooo..."],
          34, 22, {'o': P['in'], 'W': P['cloth'], 'L': P['cloth_lo'], 'r': P['paint_r']})
    # four paint jars
    for i, key in enumerate(('paint_r', 'paint_y', 'paint_g', 'cloth_lo')):
        jar(c, P, 4 + i * 8, 11, key)
    # clay brush cup with three brushes standing in it, ink pot in front of it
    for base, lean, top in ((40, -3, 1), (44, 0, 0), (48, 2, 2)):          # fanned out: heads lean away from the middle
        rows = 10 - top
        for k in range(rows - 3):
            y = top + 4 + k
            x = base + round(lean * (1 - (k + 1) / (rows - 3)))
            c.px(x - 1, y, P['in']); c.px(x, y, P['wood']); c.px(x + 1, y, P['in'])
        paint(c, [".ooo.", "oBBbo", "oBbbo", ".oMo."], base + lean - 2, top,
              {'o': P['in'], 'B': P['bristle'], 'b': P['bristle_lo'], 'M': P['metal_hi']})
    cup = smooth_mask({(x, y) for x in range(38, 51) for y in range(11, 20)})
    for x, y in cup: c.px(x, y, P['clay'])
    for x, y in rings(cup)[1]: c.px(x, y, P['clay_hi'] if x < 41 else P['clay_lo'] if x > 47 else P['clay'])
    for x, y in rings(cup)[0]: c.px(x, y, P['in'])
    c.hl(39, 49, 12, P['in'])
    for xc in (40, 44, 48): c.px(xc, 11, P['wood'])
    c.rect(36, 15, 41, 19, fill=P['void']); c.hl(37, 40, 16, P['metal']); c.rect(36, 15, 41, 19, line=P['in'])
    c.px(37, 17, P['metal_hi'])
    c.outline(P['out'])
    lint(c, colors=[K, P['in']], name='palette-bench'); return c


if __name__ == '__main__':
    for name, fn in (('handcart', handcart), ('palette-bench', palette_bench)):
        for v, P in (('sketch', SK), ('color', CO)):
            fn(P).native().save(f'out/hub/{name}-{v}.png')
