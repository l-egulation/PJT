"""Design options: artisan sign x4, wave-challenge stone x4 (dormant + active)."""
from px import *

RED, YELLOW, BLUE, GREEN = h('c8352c'), h('e6a92e'), h('3d5ea8'), h('3f8a4f')
RED_HI, YELLOW_HI, BLUE_HI = h('e0675c'), h('f3cc6f'), h('6e8cd0')
GLOW, GLOW_HI = h('f0c860'), h('fbe7b0')
PAPER, PAPER_LO = h('f6eedc'), h('d9cdb2')


def outline_shape(c, mask, fill, line=D):
    for x, y in mask: c.px(x, y, fill)
    for x, y in rings(mask)[0]: c.px(x, y, line)


def blob(c, x, y, col, hi):
    paint(c, [".cc.", "chcc", "cccc", ".cc."], x, y, {'c': col, 'h': hi})


def chain_v(c, x, y0, y1):
    for y in range(y0, y1 + 1):
        if (y - y0) % 3 == 1: c.px(x - 1, y, D); c.px(x + 1, y, D)
        else: c.px(x, y, D)


# ------------------------------------------------------------------ signs
def sign_a():
    """palette-shaped wooden board on one chain (closest to the current design)"""
    c = Canvas(30, 30)
    c.rect(11, 0, 18, 2, fill=L); c.hl(12, 17, 1, C)
    chain_v(c, 14, 3, 6)
    board = smooth_mask(ellipse_mask(1, 7, 28, 29) - (ellipse_mask(19, 13, 23, 17) | {(x, y) for x in range(22, 30) for y in range(15, 18)}))
    for x, y in board: c.px(x, y, L)
    rr = rings(board)
    for ring in rr[2:]:
        for x, y in ring: c.px(x, y, C)
    for x, y in rr[1]: c.px(x, y, M if x + y > 36 else W)            # rim bevel
    for x, y, n in ((6, 25, 4), (14, 26, 3)): c.hl(x, x + n - 1, y, L)  # grain
    blob(c, 6, 11, RED, RED_HI); blob(c, 12, 10, YELLOW, YELLOW_HI); blob(c, 5, 17, BLUE, BLUE_HI)
    blob(c, 11, 17, GREEN, h('6fae79'))

    c.outline(K); lint(c, name='sign-a'); return c


def sign_b():
    """iron wall bracket, board below with crossed brush and hammer"""
    c = Canvas(34, 30)
    c.rect(0, 2, 1, 7, fill=M)                                        # wall plate
    c.rect(2, 2, 27, 4, fill=M); c.hl(2, 27, 3, L)                    # arm
    paint(c, ["..ooo", ".o..o", "o...o", "o..o.", ".oo.."], 26, 0, {'o': D})   # scroll end
    c.line([(2, 6), (8, 4)], D)                                       # brace
    for x in (8, 26): chain_v(c, x, 5, 8)
    c.rect(3, 9, 31, 28, fill=C)
    for x, y in ((3, 9), (31, 9), (3, 28), (31, 28)): c.px(x, y, CLEAR)
    c.hl(4, 30, 10, W); c.hl(4, 30, 27, L); c.vl(30, 11, 26, L)
    c.rect(5, 12, 29, 25, line=L)
    # brush (lower-left to upper-right) and hammer (crossed)
    c.line([(10, 23), (22, 13)], D); c.px(23, 12, RED); c.px(24, 11, RED); c.px(23, 11, RED_HI)
    c.line([(11, 13), (22, 23)], M)
    c.rect(8, 12, 12, 14, fill=D)                                     # hammer head
    c.hl(9, 11, 13, M)
    c.outline(K); lint(c, name='sign-b'); return c


def sign_c():
    """cloth banner on a rod, swallowtail hem, ink drop emblem with three colour bands"""
    c = Canvas(26, 34)
    c.rect(0, 0, 25, 2, fill=M); c.hl(1, 24, 1, L)                    # rod with knobs
    cloth = {(x, y) for x in range(3, 23) for y in range(3, 33)}
    cloth -= {(x, y) for x in range(3, 23) for y in range(26, 33) if y - 26 > abs(x - 12.5) * 1.0 - 4.5 and abs(x - 12.5) < 6}
    cloth = smooth_mask(cloth)
    for x, y in cloth: c.px(x, y, PAPER)
    for x, y in rings(cloth)[1]: c.px(x, y, PAPER_LO if x > 12 else PAPER)
    for y in range(4, 26): c.px(7, y, PAPER_LO) if y % 6 < 3 else None  # cloth fold
    for i, col in enumerate((RED, YELLOW, BLUE)):
        c.hl(4, 21, 5 + i, col)
    drop = ["...oo...", "..oiio..", ".oiiiio.", "oiiiiiio", "oiiWiiio", "oiiiiiio", ".oiiiio.", "..oooo.."]
    paint(c, drop, 9, 11, {'o': D, 'i': INK, 'W': M})
    c.outline(K); lint(c, name='sign-c'); return c


def sign_d():
    """giant paintbrush hung on two chains, red-dipped bristles, small tag"""
    c = Canvas(36, 24)
    c.rect(4, 0, 31, 2, fill=L); c.hl(5, 30, 1, C)
    for x in (9, 26): chain_v(c, x, 3, 6)
    # handle
    c.rect(1, 7, 21, 11, fill=C); c.hl(2, 20, 8, W); c.hl(1, 21, 10, L)
    c.rect(0, 8, 0, 10, fill=C)
    # ferrule
    c.rect(22, 6, 25, 12, fill=L); c.vl(23, 7, 11, W); c.vl(25, 7, 11, M)
    # bristles: fanning out, tip dipped in red
    br = {(x, y) for x in range(26, 36) for y in range(5, 14) if abs(y - 9) <= 3 + (x - 26) * .25 and not (x > 33 and abs(y - 9) > 2)}
    br = smooth_mask(br)
    for x, y in br: c.px(x, y, C if x < 31 else RED)
    for x, y in br:
        if x >= 31 and (x + y) % 5 == 0: c.px(x, y, RED_HI)
        if x < 31 and y in (7, 11): c.px(x, y, L)
    c.px(33, 15, RED); c.px(33, 16, RED)                               # drip
    # tag
    c.vl(12, 12, 14, D)
    c.rect(8, 15, 16, 21, fill=PAPER); c.hl(9, 15, 20, PAPER_LO)
    blob(c, 10, 16, BLUE, BLUE_HI)
    c.outline(K); lint(c, name='sign-d'); return c


# ------------------------------------------------------------------ wave stones
def wave_glyph(c, x, y, col):
    paint(c, [".oo....", "o..o..o", "....oo."], x, y, {'o': col})


def stone_a(active):
    """obelisk with three stacked carved waves"""
    c = Canvas(24, 38)
    body = {(x, y) for y in range(6, 33) for x in range(24) if abs(x + .5 - 12) <= 5 + (y - 6) * .12}
    cap = {(x, y) for y in range(0, 7) for x in range(24) if abs(x + .5 - 12) <= y * .85}
    m = smooth_mask(body | cap)
    for x, y in m: c.px(x, y, C)
    for x, y in rings(m)[1]: c.px(x, y, W if x < 12 else L)
    col = GLOW if active else D
    if active:
        for x, y in cap: c.px(x, y, GLOW_HI if (x, y) in m and (x, y) not in rings(m)[0] else c.get(x, y))
    for i in range(3):
        wave_glyph(c, 9, 11 + i * 6, col)
    c.hl(6, 17, 7, L)                                                  # cap seam
    c.rect(2, 33, 21, 37, fill=L); c.hl(2, 21, 33, D); c.hl(3, 20, 34, C); c.vl(8, 35, 37, M); c.vl(15, 35, 37, M)
    if active:
        for x, y in ((5, 9), (19, 12), (4, 2)): c.px(x, y, GLOW)
    c.outline(K); lint(c, name='stone-a'); return c


def stone_b(active):
    """gong: two posts, beam, a hanging stone disc with a wave spiral"""
    c = Canvas(30, 36)
    c.rect(0, 2, 29, 5, fill=C); c.hl(1, 28, 3, W); c.hl(0, 29, 5, L)   # beam
    for x0 in (2, 25):
        c.rect(x0, 6, x0 + 2, 32, fill=C); c.vl(x0, 6, 32, W); c.vl(x0 + 2, 6, 32, L)
    for x in (10, 19): c.vl(x, 6, 8, D)                                # cords
    disc = smooth_mask(ellipse_mask(6, 8, 23, 25))
    for x, y in disc: c.px(x, y, GLOW_HI if active else C)
    for x, y in rings(disc)[0]: c.px(x, y, D)
    for x, y in rings(disc)[1]: c.px(x, y, GLOW if active else (W if x < 15 else L))
    col = GLOW if active else D
    inner = smooth_mask(ellipse_mask(10, 12, 19, 21))
    for x, y in rings(inner)[0]: c.px(x, y, col)
    wave_glyph(c, 12, 15, col)
    c.rect(0, 32, 29, 35, fill=L); c.hl(0, 29, 32, D); c.hl(1, 28, 33, C)
    if active:
        for x, y in ((3, 13), (2, 16), (26, 13), (27, 16)): c.px(x, y, GLOW)   # ring of sound
    c.outline(K); lint(c, name='stone-b'); return c


def stone_c(active):
    """squat rough stone with a paper talisman bearing an ink wave"""
    c = Canvas(26, 32)
    rock = smooth_mask(ellipse_mask(1, 6, 24, 34) & {(x, y) for x in range(26) for y in range(6, 29)} |
                       {(x, y) for x in range(2, 24) for y in range(18, 29)})
    rock -= {(x, y) for x in range(18, 26) for y in range(6, 10)}
    rock = smooth_mask(rock)
    for x, y in rock: c.px(x, y, C)
    for x, y in rings(rock)[1]: c.px(x, y, W if x < 12 else L)
    c.line([(18, 12), (20, 16), (19, 19)], L)                          # crack
    tal = {(x, y) for x in range(9, 16) for y in range(9, 25)}
    for x, y in tal: c.px(x, y, GLOW_HI if active else PAPER)
    for x, y in rings(tal)[0]: c.px(x, y, PAPER_LO)
    c.hl(9, 15, 11, RED); c.hl(9, 15, 22, RED)                        # red seal bands
    col = h('8a5a10') if active else INK
    paint(c, ["..o..", ".o.o.", "o...o", ".....", "..o..", ".o.o.", "o...o"], 10, 13, {'o': col})
    if active:
        c.px(16, 9, GLOW); c.px(17, 8, GLOW); c.px(8, 24, GLOW)        # corner lifting / sparks
    c.rect(0, 28, 25, 31, fill=L); c.hl(0, 25, 28, D); c.hl(1, 24, 29, C); c.vl(9, 30, 31, M); c.vl(17, 30, 31, M)
    c.outline(K); lint(c, name='stone-c'); return c


def stone_d(active):
    """standing ring stone: a round hole framed by carved waves"""
    c = Canvas(28, 36)
    ring = smooth_mask(ellipse_mask(1, 1, 26, 26) - ellipse_mask(8, 8, 19, 19))
    legs = {(x, y) for x in range(6, 22) for y in range(22, 32)}
    m = smooth_mask(ring | legs)
    for x, y in m: c.px(x, y, C)
    for x, y in rings(m)[1]: c.px(x, y, W if x < 13 else L)
    hole = ellipse_mask(8, 8, 19, 19)
    if active:
        for x, y in hole: c.px(x, y, GLOW_HI)
        for x, y in rings(smooth_mask(hole))[0]: c.px(x, y, GLOW)
        c.px(13, 13, W); c.px(14, 13, W)
    col = GLOW if active else D
    for x, y in ((10, 3), (3, 12), (19, 23), (10, 22)):
        wave_glyph(c, x, y, col) if (x, y) != (19, 23) else None
    wave_glyph(c, 18, 3, col); wave_glyph(c, 19, 12, col)
    c.rect(1, 32, 26, 35, fill=L); c.hl(1, 26, 32, D); c.hl(2, 25, 33, C); c.vl(9, 34, 35, M); c.vl(18, 34, 35, M)
    c.outline(K); lint(c, name='stone-d'); return c


if __name__ == '__main__':
    for n, f in (('a', sign_a), ('b', sign_b), ('c', sign_c), ('d', sign_d)):
        f().native().save(f'out/opt-sign-{n}.png')
    for n, f in (('a', stone_a), ('b', stone_b), ('c', stone_c), ('d', stone_d)):
        f(False).native().save(f'out/opt-stone-{n}.png')
        f(True).native().save(f'out/opt-stone-{n}-on.png')
