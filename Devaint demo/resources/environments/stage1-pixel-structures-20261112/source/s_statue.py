"""Wave-challenge statue options. 'Wave' = combat rounds: pedestal lamps light per wave."""
from px import *

GLOW, GLOW_HI = h('f0c860'), h('fbe7b0')


def shade(c, mask):
    """terrain-style volume: light left rim, shaded right rim, silhouette ink comes later"""
    for x, y in mask: c.px(x, y, C)
    cx = (min(x for x, _ in mask) + max(x for x, _ in mask)) / 2
    for x, y in rings(mask)[1]: c.px(x, y, W if x < cx else L)


def mask_of(rows, ox=0, oy=0):
    return {(x + ox, y + oy) for x, y in ascii_mask(rows)}


def pedestal(c, x0, x1, y0, y1, lit):
    """stone pedestal with three wave lamps; `lit` = how many waves have started"""
    c.rect(x0, y0, x1, y1, fill=C)
    c.hl(x0, x1, y0, W); c.hl(x0, x1, y0 + 1, D); c.hl(x0 + 1, x1 - 1, y1 - 1, L)
    mid = (x0 + x1) // 2
    for i, x in enumerate((mid - 6, mid, mid + 6)):
        on = i < lit
        paint(c, [".o.", "oxo", ".o."], x - 1, (y0 + y1) // 2 - 0, {'o': GLOW if on else D, 'x': GLOW_HI if on else INK})


# ---------------------------------------------------------------- A: hooded guardian leaning on a sword
GUARD = [
    "..........####..........",
    "........########........",
    ".......##########.......",
    "......############......",
    "......############......",
    ".....##############.....",
    ".....##############.....",
    ".....##############.....",
    "....################....",
    "...##################...",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    "..####################..",
    ".######################.",
    ".######################.",
]


def statue_a(lit, broken=False):
    c = Canvas(24, 36)
    m = mask_of(GUARD)
    if broken:   # a chunk of the hood has fallen away
        m = {(x, y) for x, y in m if not (y < 12 and x - 12 > (y - 2) * .8)}
    m = smooth_mask(m)
    shade(c, m)
    # hood opening with two eyes
    paint(c, ["..dddd..", ".dIIIId.", "dIIIIIId", "dIeIIeId", ".dIIIId."][:0] or
          ["..dddd..", ".diiiid.", "diiiiiid", "dieiieid", ".diiiid."], 8, 3,
          {'d': D, 'i': INK, 'e': GLOW if (lit and not broken) else D})
    # hands on the pommel, crossguard, blade down the front
    paint(c, [".oooo.", "oCCCCo", "oCWWCo", ".oooo."][:0] or [".oooo.", "oCWWCo", ".oCCo."], 9, 9, {'o': D, 'C': C, 'W': W})   # hands on the pommel
    c.hl(7, 16, 13, D)
    blade_end = 18 if broken else 22
    c.vl(10, 14, blade_end, D); c.vl(13, 14, blade_end, D); c.vl(11, 14, blade_end, W); c.vl(12, 14, blade_end, L)
    if broken:
        c.px(11, blade_end + 1, D); c.px(12, blade_end, D)               # snapped edge
        c.line([(15, 6), (16, 9), (15, 12)], D)                          # cracks through the stone
        c.line([(6, 16), (8, 19), (7, 22)], D)
        c.px(17, 22, L); c.px(19, 22, L); c.hl(16, 18, 21, L)             # rubble at the hem
    else:
        c.hl(11, 12, 23, D)
    # robe folds
    for x in (5, 18): c.vl(x, 13, 22, L)
    c.hl(2, 21, 23, L)
    pedestal(c, 1, 22, 25, 35, 3 if broken else lit)
    c.outline(K); lint(c, name='statue-a'); return c


# ---------------------------------------------------------------- B: seated haetae (guardian beast)
BEAST = [
    "....##............##....",
    "...####..######..####...",
    "...######################"[:24],
    "..######################",
    "..######################",
    ".########################"[:24],
    ".########################"[:24],
    ".#######################.",
    "..#####################..",
    "..####################...",
    "...##################....",
    "....################.....",
    "...##################....",
    "..####################...",
    "..#####################..",
    "..#####################..",
    "..#####################..",
    "..######.######.######...",
    "..######.######.######...",
    "..######.######.######...",
    ".#######.#######.######..",
    ".#######.#######.#######.",
]


def statue_b(lit):
    c = Canvas(26, 36)
    rows = [r.ljust(24, '.')[:24] for r in BEAST]
    m = smooth_mask(mask_of(rows, 1, 0))
    shade(c, m)
    # mane curls around the face
    for x, y in ((4, 3), (21, 3), (3, 7), (22, 7), (5, 10), (20, 10)):
        paint(c, [".o.", "o.o"], x - 1, y, {'o': L})
    # face: brows, eyes, snout, mouth
    for x in (7, 15):                                                   # big round eyes under heavy brows
        paint(c, [".ooo.", "o...o", "o.e.o", ".ooo."], x, 3, {'o': D, 'e': GLOW if lit else INK})
        if lit: c.px(x + 1, 4, GLOW_HI); c.px(x + 2, 4, GLOW_HI); c.px(x + 3, 4, GLOW_HI); c.px(x + 1, 5, GLOW); c.px(x + 3, 5, GLOW)
    paint(c, [".ooo.", "o.o.o"], 11, 7, {'o': D})                       # snout
    paint(c, ["ooooooooo", "oWoWoWoWo", ".ooooooo."], 9, 9, {'o': D, 'W': W})   # wide toothy mouth
    # chest scroll and paws
    paint(c, [".o.o.", "o.o.o"], 11, 13, {'o': L})
    for x in (5, 12, 19): c.hl(x, x + 3, 21, L)
    pedestal(c, 1, 24, 23, 35, lit)
    c.outline(K); lint(c, name='statue-b'); return c


# ---------------------------------------------------------------- C: three-face totem, one face per wave
def statue_c(lit):
    c = Canvas(22, 40)
    blocks = [(3, 18, 2, 11), (2, 19, 12, 21), (1, 20, 22, 31)]          # top, middle, bottom (wider)
    m = set()
    for x0, x1, y0, y1 in blocks:
        m |= {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)}
    m |= {(x, y) for x in range(8, 14) for y in range(0, 2)}          # top knob
    m = smooth_mask(m)
    shade(c, m)
    for _, _, _, y1 in blocks[:2]: c.hl(1, 20, y1, D)                  # seams between faces
    # faces: bottom = wave 1, middle = wave 2, top = wave 3
    for i, (x0, x1, y0, y1) in enumerate(reversed(blocks)):
        on = i < lit
        cx = (x0 + x1) // 2
        ey = y0 + 3
        col = GLOW if on else D
        paint(c, ["oo...oo", "......."], cx - 3, ey, {'o': col})         # eyes
        if on:
            c.px(cx - 3, ey + 1, GLOW_HI); c.px(cx + 3, ey + 1, GLOW_HI)
        c.vl(cx, ey + 1, ey + 3, L)                                      # nose ridge
        c.hl(cx - 2, cx + 2, ey + 4, col)                                # mouth
    c.rect(0, 32, 21, 39, fill=L); c.hl(0, 21, 32, D); c.hl(1, 20, 33, C); c.vl(7, 35, 39, M); c.vl(14, 35, 39, M)
    c.outline(K); lint(c, name='statue-c'); return c


def final():
    for name, lit, br in (('seal-dormant', 0, False), ('seal-active', 1, False), ('seal-wave2', 2, False),
                          ('seal-wave3', 3, False), ('seal-broken', 3, True)):
        statue_a(lit, br).native().save(f'out/{name}.png')


if __name__ == '__main__':
    final()
    for n, f in (('a', statue_a), ('b', statue_b), ('c', statue_c)):
        f(0).native().save(f'out/opt-statue-{n}.png')
        f(1).native().save(f'out/opt-statue-{n}-w1.png')
        f(2).native().save(f'out/opt-statue-{n}-w2.png')
