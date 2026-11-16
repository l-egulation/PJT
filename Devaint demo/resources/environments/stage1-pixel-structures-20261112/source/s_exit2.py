"""Chapter-1 exit = entrance to the chapter-2 sewer. Stage-2 stone colours, thin arch, big opening.
Native 128x160 (4u x 5u). Sealed: iron grate down. Open: grate raised, stairs into the dark water."""
import math
from px import *

# stage2-sewer-modular-20261111 colours
CAP = [h('b7c2b6'), h('a1b1a5'), h('8ba095'), h('788f87'), h('67817b'), h('556f6d'), h('3d5255')]
STONE_HI, STONE, STONE_LO, MORTAR = h('45585e'), h('40535a'), h('3b4e55'), h('374a51')
VOID, VOID2 = h('1c2b32'), h('283b40')
BAR_HI, BAR, BAR_LO = h('8ba095'), h('67817b'), h('3d5255')
WATER, WATER2 = h('4a6466'), h('5f7a78')
MOSS, MOSS_HI = h('586f55'), h('7a9370')
SOFFIT = h('2e4146')
FAR = h('223239')

CX, CY, RIN, ROUT = 32.0, 33.0, 22.6, 28.6
TOP_PAD = 0


def disc(R):
    return {(x, y) for x in range(64) for y in range(80) if y < CY and (x + .5 - CX) ** 2 + (y + .5 - CY) ** 2 <= R * R}


def draw(sealed, gb=None, drips=()):
    c = Canvas(64, 80)
    outer = disc(ROUT) | {(x, y) for x in range(4, 60) for y in range(33, 77)}
    opening = disc(RIN) | {(x, y) for x in range(10, 54) for y in range(33, 77)}
    opening = {(x, y) for x in range(64) for y in range(80)} - smooth_mask({(x, y) for x in range(64) for y in range(80)} - opening)
    ring = smooth_mask(outer) - opening
    # ---- interior: dark tunnel with steps going down to water
    for x, y in opening: c.px(x, y, VOID)
    for i, y in enumerate(range(16, 41, 5)):                       # far wall bricks, barely visible, fading into the dark
        for x in range(10, 54):
            if (x, y) in opening: c.px(x, y, FAR)
        for x in range(12 + (5 if i % 2 else 0), 54, 10):
            for yy in range(y + 1, y + 5):
                if (x, yy) in opening and y < 36: c.px(x, yy, FAR)
    for i, y in enumerate((60, 66, 72)):
        x0, x1 = 12 + i * 3, 51 - i * 3
        c.hl(x0, x1, y, VOID2)
    for y in (75, 76): c.hl(16, 47, y, WATER)
    c.hl(20, 27, 75, WATER2); c.hl(36, 42, 76, WATER2)
    # soffit: the arch's own thickness seen from below, one dot inside the opening
    soffit = {(x, y) for x, y in opening if any((x + dx, y + dy) not in opening for dx, dy in ((1, 0), (-1, 0), (0, -1)))}
    for x, y in list(soffit):
        hs = [dx for dx in (-1, 1) if (x + dx, y) in soffit]; vs = [dy for dy in (-1, 1) if (x, y + dy) in soffit]
        if len(hs) == 1 and len(vs) == 1 and (x + hs[0], y + vs[0]) not in soffit: soffit.discard((x, y))
    for x, y in soffit:
        if y < 76: c.px(x, y, SOFFIT)
    # ---- arch ring: pale cap stones (voussoirs) with 1-dot joints
    for x, y in ring:
        d = math.hypot(x + .5 - CX, y + .5 - CY)
        if y < CY:
            c.px(x, y, CAP[1] if d < RIN + 1.6 else CAP[2] if d < ROUT - 1.4 else CAP[3])
        else:
            c.px(x, y, STONE)
    for ang in (14, 34, 54, 72, 108, 126, 146, 166):
        a = math.radians(ang)
        p0 = (round(CX - .5 - math.cos(a) * (RIN + .6)), round(CY - .5 - math.sin(a) * (RIN + .6)))
        p1 = (round(CX - .5 - math.cos(a) * (ROUT - .4)), round(CY - .5 - math.sin(a) * (ROUT - .4)))
        c.line([p0, p1], CAP[5])
    # keystone: taller, pokes above the ring
    ks = {(x, y) for x in range(28, 36) for y in range(2, 12)}
    for x, y in ks: c.px(x, y, CAP[0] if y < 5 else CAP[1])
    opening = opening - ks                                          # bars and drips stay behind the keystone
    c.vl(28, 4, 11, CAP[5]); c.vl(35, 4, 11, CAP[5])
    paint(c, [".oo.", "o..o", "o..o", ".oo."], 30, 5, {'o': CAP[4]})     # small drain mark
    # ---- pillars: sewer bricks, 1-dot mortar
    for x0, x1 in ((4, 9), (54, 59)):
        for y in range(33, 77):
            for x in range(x0, x1 + 1):
                c.px(x, y, STONE)
        for i, y in enumerate(range(33, 77, 6)):
            c.hl(x0, x1, y, MORTAR)
            c.hl(x0 + 1, x1 - 1, y + 1, STONE_HI)
            c.vl(x0 + (2 if i % 2 else 4), y + 1, min(y + 5, 76), MORTAR)
    # imposts where the arch springs
    for x0, x1 in ((3, 10), (53, 60)):
        c.rect(x0, 31, x1, 33, fill=CAP[1]); c.hl(x0, x1, 31, CAP[0]); c.hl(x0, x1, 33, CAP[5])
    # ---- base step
    c.rect(1, 77, 62, 79, fill=CAP[2]); c.hl(1, 62, 77, CAP[0]); c.hl(1, 62, 79, CAP[5])
    for x in (12, 24, 39, 51): c.vl(x, 78, 79, CAP[4])
    # pillar bricks: a few lighter faces so the courses are not uniform
    for x0, y0 in ((5, 46), (55, 40), (6, 64), (56, 58)):
        c.hl(x0, x0 + 2, y0, STONE_HI); c.hl(x0, x0 + 2, y0 + 1, STONE_HI)
    # water stain under the keystone drain and down a pillar
    c.vl(31, 9, 11, CAP[3]); c.vl(32, 10, 11, CAP[3])
    c.vl(7, 35, 41, STONE_LO); c.vl(57, 35, 38, STONE_LO)
    # moss on the imposts, the ring foot and the base
    for x, y, n in ((4, 30, 3), (57, 30, 2), (2, 76, 4), (57, 76, 3), (11, 27, 2), (51, 25, 2)):
        c.hl(x, x + n - 1, y, MOSS)
        c.px(x + 1, y - 1, MOSS_HI)
    c.px(5, 31, MOSS); c.px(5, 32, MOSS)                            # strand hanging off the impost
    # wet patch where water seeps over the step
    c.hl(20, 43, 77, CAP[2]); c.hl(26, 37, 78, CAP[3])
    # ---- iron grate
    def grate(y_bottom):
        for x in range(13, 52, 6):
            for y in range(0, y_bottom + 1):
                if (x, y) in opening:
                    c.px(x, y, BAR_HI); c.px(x + 1, y, BAR_LO)
            for dx, col in ((0, BAR_HI), (1, BAR_LO)):           # spike tip
                if (x + dx, y_bottom + 1) in opening: c.px(x + dx, y_bottom + 1, col) if dx == 0 else None
        rails = tuple(y for y in (y_bottom - 22, y_bottom - 8) if y > 20) or (y_bottom - 2,)
        for y in rails:
            for x in range(10, 54):
                if (x, y) in opening and c.get(x, y) not in (BAR_HI, BAR_LO):
                    c.px(x, y, BAR)
            for x in range(13, 52, 6):
                if (x, y) in opening: c.px(x, y, CAP[0])               # rivet where bar meets rail
    grate(gb if gb is not None else (74 if sealed else 17))
    for x, y in drips: c.px(x, y, WATER2)
    c.outline(K)
    lint(c, colors=[K, CAP[5], MORTAR], name='exit-sealed' if sealed else 'exit-open')
    return c


if __name__ == '__main__':
    draw(True).native().save('out/exit-sealed.png')
    draw(False).native().save('out/exit-open.png')
