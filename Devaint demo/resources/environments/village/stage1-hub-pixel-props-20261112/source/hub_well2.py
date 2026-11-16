"""Well v2: faithful to the current art. Rope coil on the beam, ring, bail, banded bucket, rounded stones,
capped posts with grain, knee braces on both sides of each post."""
from px import *
import hub_art as HA

# sketch reads with pencil shading: wood a touch darker than stone so they separate
SK = dict(HA.SKETCH, wood_hi=C, wood=h('f3ece0'), wood_lo=L, wood_dk=M, stone_hi=W, stone=C, stone_lo=L, rope=L, rope_lo=M)
CO = dict(HA.COLOR)


def rstone(c, P, x0, y0, x1, y1):
    """one rounded stone inside its cell; the 1-dot joint around it is drawn by the caller"""
    c.rect(x0, y0, x1, y1, fill=P['stone'])
    c.hl(x0 + 1, x1 - 1, y0, P['stone_hi']); c.vl(x0, y0 + 1, y1 - 1, P['stone_hi'])
    c.hl(x0 + 1, x1, y1, P['stone_lo']); c.vl(x1, y0 + 1, y1, P['stone_lo'])
    if y1 - y0 > 4: c.hl(x0 + 2, x1 - 1, y1 - 1, P['stone_lo'])
    for x, y in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)): c.px(x, y, P['stone_lo'])


def course(c, P, y0, y1, xs):
    """stones between joint columns xs (first and last are the course's outer edges)"""
    c.rect(xs[0], y0 - 1, xs[-1], y1 + 1, fill=P['in'])
    for a, b in zip(xs, xs[1:]):
        rstone(c, P, a + 1, y0, b - 1, y1)


def post(c, P, x0, x1, y0, y1):
    c.rect(x0, y0, x1, y1, fill=P['wood'])
    c.vl(x0 + 1, y0 + 1, y1, P['wood_hi'])
    c.vl(x1 - 1, y0 + 1, y1, P['wood_lo']); c.vl(x1 - 2, y0 + 2, y1, P['wood_lo'])
    for y, dx in ((12, 3), (24, 4), (33, 3), (46, 4), (57, 2)):         # grain
        c.vl(x0 + dx, y, y + 4, P['wood_dk'])
    c.rect(x0, y0, x1, y1, line=P['in'])
    c.px(x0, y0, CLEAR); c.px(x1, y0, CLEAR)                              # rounded cap
    c.px(x0 + 1, y0 + 1, P['in']); c.px(x1 - 1, y0 + 1, P['in'])
    c.hl(x0 + 2, x1 - 2, y0 + 1, P['wood_hi'])


def knee(c, P, x, flip):
    """small triangular brace under the beam"""
    rows = ["wwwwo", "wwwo.", "wwo..", "wo...", "o...."]
    if flip: rows = [r[::-1] for r in rows]
    paint(c, rows, x, 11, {'o': P['in'], 'w': P['wood']})


def well(P):
    c = Canvas(63, 69)
    # beam with rounded ends and grain, passing behind the posts
    c.rect(0, 5, 62, 10, fill=P['wood'])
    c.hl(1, 61, 6, P['wood_hi']); c.hl(1, 61, 9, P['wood_lo'])
    for x in (18, 42): c.hl(x, x + 4, 7, P['wood_dk'])
    c.rect(0, 5, 62, 10, line=P['in'])
    for x, y in ((0, 5), (62, 5), (0, 10), (62, 10)): c.px(x, y, CLEAR)
    knee(c, P, 14, False); knee(c, P, 44, True)                          # inner braces only (outer ones stuck out)
    # rope coil wound round the beam: bulges above and below it
    c.rect(25, 3, 37, 12, fill=P['rope'])
    for x in range(26, 37, 2): c.vl(x, 4, 11, P['rope_lo'])
    c.hl(26, 36, 4, P['rope']); c.rect(25, 3, 37, 12, line=P['in'])
    c.px(25, 3, CLEAR); c.px(37, 3, CLEAR); c.px(25, 12, CLEAR); c.px(37, 12, CLEAR)
    # rope down to an iron ring
    c.vl(31, 13, 15, P['rope_lo'])
    paint(c, ["..ooo..", ".o...o.", "o.....o", "o.....o", ".o...o.", "..ooo.."], 28, 16, {'o': P['metal']})
    # bail: two sides from the ring to the bucket rim
    c.line([(29, 21), (26, 27)], P['in']); c.line([(33, 21), (36, 27)], P['in'])
    # bucket: tapered staves, two iron bands, dark mouth
    for y in range(29, 43):
        inset = (y - 29) // 7
        c.hl(24 + inset, 39 - inset, y, P['wood'])
        c.px(25 + inset, y, P['wood_hi']); c.px(38 - inset, y, P['wood_lo']); c.px(37 - inset, y, P['wood_lo'])
    for x in (28, 31, 35):
        c.vl(x, 31, 41, P['wood_lo'])
    c.hl(25, 38, 29, P['void'])
    for yb in (31, 40):
        ins = (yb - 29) // 7
        c.hl(24 + ins, 39 - ins, yb, P['metal']); c.px(25 + ins, yb, P['metal_hi'])
    for y in range(28, 43):
        ins = (y - 29) // 7 if y >= 29 else 0
        c.px(24 + ins, y, P['in']); c.px(39 - ins, y, P['in'])
    c.hl(24, 39, 28, P['in'])
    # crossbar ends poking out of the curb on both sides
    for x0 in (2, 56):
        c.rect(x0, 43, x0 + 5, 50, fill=P['wood']); c.hl(x0 + 1, x0 + 4, 44, P['wood_hi']); c.hl(x0 + 1, x0 + 4, 49, P['wood_lo'])
        c.rect(x0, 43, x0 + 5, 50, line=P['in'])
    # the posts stand in front of the crossbar ends but behind the stone curb
    post(c, P, 5, 13, 1, 67); post(c, P, 49, 57, 1, 67)
    c.px(9, 7, P['metal']); c.px(53, 7, P['metal'])                       # bolts through the beam
    # rounded stones in three courses (layout follows the current art)
    course(c, P, 44, 50, [9, 16, 25, 37, 47, 54])
    course(c, P, 52, 58, [9, 19, 30, 42, 54])
    course(c, P, 60, 67, [2, 8, 14, 26, 38, 49, 55, 61])
    # grass at the foot: its base on the bottom course's last row, inside the curb
    HA.tuft(c, P, 15, 65, True); HA.tuft(c, P, 39, 65); HA.tuft(c, P, 26, 65)
    c.outline(P['out'])
    lint(c, colors=[K, D], name='well2')
    return c


if __name__ == '__main__':
    import os
    os.makedirs('out/hub', exist_ok=True)
    well(SK).native().save('out/hub/return-well-sketch.png')
    well(CO).native().save('out/hub/return-well-color.png')
