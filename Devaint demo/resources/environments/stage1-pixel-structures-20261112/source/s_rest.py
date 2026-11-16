"""Exit arch, iron gate, ink altar, chests, seals: same 2x2-dot grid, terrain palette, 1-dot lines."""
from px import *

GEMS = [h('c8352c'), h('e6a92e'), h('3f8a4f'), h('3d5ea8'), h('7b4fa8')]
GLOW = h('f0c860')
VOID = h('4b4a46')   # sealed door: a fill, not a line


def lighten(c, t=.45):
    return tuple(int(v + (255 - v) * t) for v in c[:3]) + (255,)


def fill_mask(c, mask, col):
    for x, y in mask: c.px(x, y, col)


# ---------------------------------------------------------------- exit arch 48x64
def exit_arch(sealed):
    c = Canvas(48, 64)
    cx, cy = 24.0, 33.0
    disc = lambda R: {(x, y) for x in range(48) for y in range(64)
                      if y < cy and (x + .5 - cx) ** 2 + (y + .5 - cy) ** 2 <= R * R}
    outer = disc(20) | {(x, y) for x in range(4, 44) for y in range(33, 61)}
    opening = disc(9.2) | {(x, y) for x in range(15, 33) for y in range(33, 61)}
    opening = {(x, y) for x in range(48) for y in range(64)} - smooth_mask({(x, y) for x in range(48) for y in range(64)} - opening)
    shape = smooth_mask(outer - opening)
    fill_mask(c, shape, C)
    # voussoir joints: radial lines between the inner and outer circles
    import math
    for ang in (20, 45, 70, 110, 135, 160):
        a = math.radians(ang)
        x0, y0 = cx - math.cos(a) * 10.2, cy - math.sin(a) * 10.2
        x1, y1 = cx - math.cos(a) * 19.0, cy - math.sin(a) * 19.0
        c.line([(int(x0), int(y0)), (int(x1), int(y1))], L)
    # keystone
    ks = {(x, y) for x in range(21, 27) for y in range(12, 25)} & (shape | {(x, 12) for x in range(21, 27)} | {(x, 13) for x in range(21, 27)})
    fill_mask(c, ks, C)
    c.vl(20, 13, 23, L); c.vl(27, 13, 23, L)
    paint(c, ["..o..", ".oWo.", "oWMMo", ".oMo.", "..o.."], 22, 15, {'o': D, 'W': W, 'M': M})
    # pillar courses
    for x0, x1 in ((4, 14), (33, 43)):
        for y in range(38, 60, 5):
            c.hl(x0, x1, y, L)
        for i, y in enumerate(range(33, 60, 5)):
            c.vl(x0 + (4 if i % 2 else 7), y + 1, min(y + 4, 59), L)
        c.vl(x0, 33, 60, W) if x0 == 4 else c.vl(x1, 33, 60, L)
    # imposts and bases (wider mouldings)
    for x0, x1 in ((3, 15), (32, 44)):
        c.rect(x0, 31, x1, 33, fill=C); c.hl(x0, x1, 31, W); c.hl(x0, x1, 33, D)
        c.rect(x0, 56, x1, 60, fill=C); c.hl(x0, x1, 56, D); c.hl(x0 + 1, x1 - 1, 57, W); c.hl(x0, x1, 59, L)
    if sealed:
        fill_mask(c, opening, VOID)
        for x in (19, 24, 29):
            c.vl(x, 26 if x == 24 else 28, 60, M)
    c.outline(K)
    if sealed:   # frame the dark void with the ink line too
        frame = {(x, y) for x, y in opening if (x, y) not in shape and
                 any((x + dx, y + dy) in shape for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
        for x, y in list(frame):     # drop L-elbow dots so the frame stays one dot thick on the curve
            hs = [dx for dx in (-1, 1) if (x + dx, y) in frame]; vs = [dy for dy in (-1, 1) if (x, y + dy) in frame]
            if len(hs) == 1 and len(vs) == 1 and (x + hs[0], y + vs[0]) not in frame:
                frame.discard((x, y))
        for x, y in frame: c.px(x, y, K)
    lint(c, name='exit-sealed' if sealed else 'exit-open')
    return c


# ---------------------------------------------------------------- iron gate
def gate_body():
    c = Canvas(12, 64)
    for x0 in (0, 7):
        c.rect(x0, 0, x0 + 3, 63, fill=L); c.vl(x0 + 1, 0, 63, W)
        for y in (15, 47): c.hl(x0 + 1, x0 + 2, y, M)
    c.rect(4, 30, 6, 33, fill=L); c.hl(4, 6, 31, W)
    c.outline(K, where=lambda x, y: x not in (0, 3, 7, 10) or True)
    # bars run off the top and bottom of the canvas: keep the ends open
    for x in range(12):
        for y in (0, 63):
            if c.get(x, y)[3] and c.get(x, y) == K and x not in (0, 3, 7, 10):
                c.px(x, y, W if x in (1, 8) else L)
    lint(c, name='iron-gate-body')
    return c


def gate_tip():
    c = Canvas(12, 12)
    rows = [
        "oWLo...oWLo.",
    ] * 9 + [
        "oWLo...oWLo.",
        ".oo.....oo..",
        "............",
    ]
    paint(c, rows, 0, 0, {'o': K, 'W': W, 'L': L})
    lint(c, name='iron-gate-tip')
    return c


# ---------------------------------------------------------------- ink altar 24x20
def altar(used):
    c = Canvas(24, 20)
    c.rect(1, 3, 22, 7, fill=C); c.hl(2, 21, 2, C)                 # top slab, chamfered
    c.hl(2, 21, 3, W); c.hl(1, 22, 7, D)
    c.rect(3, 8, 20, 14, fill=C)                                 # body
    for x in (5, 18): c.vl(x, 8, 14, L)
    c.vl(4, 8, 14, W); c.vl(19, 8, 14, L)
    c.hl(4, 19, 13, L)
    c.rect(2, 15, 21, 16, fill=C); c.hl(2, 21, 15, D)               # step
    c.rect(0, 17, 23, 19, fill=C); c.hl(1, 22, 18, W); c.hl(0, 23, 17, D)   # base
    for i, g in enumerate(GEMS):
        x = 5 + i * 3
        col = INK if used else g
        c.rect(x, 4, x + 1, 5, fill=col)
        c.px(x, 4, D if used else lighten(g))
    c.hl(4, 19, 6, L)                                            # gem ledge
    c.outline(K)
    lint(c, colors=[K, D], name='altar')
    return c


# ---------------------------------------------------------------- chests 30x27
def chest_body(c, y0=15):
    c.rect(1, y0, 28, 25, fill=C)
    c.hl(2, 27, y0 + 1, W); c.hl(1, 28, 24, L)
    c.px(2, 26, K); c.px(3, 26, K); c.px(26, 26, K); c.px(27, 26, K)    # feet (single dark row)


def straps(c, y0, y1):
    for x in (5, 22):
        c.vl(x, y0, y1, D); c.vl(x + 2, y0, y1, D); c.vl(x + 1, y0, y1, L)


def locked_chest(open_):
    c = Canvas(30, 27)
    chest_body(c)
    if open_:
        c.rect(1, 2, 28, 7, fill=L); c.hl(2, 27, 3, C); c.hl(1, 28, 7, D)   # lid seen from inside, tipped back
        c.rect(2, 8, 27, 13, fill=INK); c.hl(2, 27, 8, M)                    # dark interior
        straps(c, 2, 7)
    else:
        c.rect(0, 9, 29, 14, fill=C)
        c.hl(1, 28, 10, W); c.hl(0, 29, 13, L)
        straps(c, 9, 14)
    c.hl(0, 29, 14, D)                                            # lid / body seam
    straps(c, 15, 25)
    for x in (6, 23):
        for y in ((17, 22) if True else ()):
            c.px(x, y, M)
    # lock plate with keyhole
    c.rect(11, 16, 19, 22, fill=L, line=D); c.hl(12, 18, 17, C)
    if not open_:
        c.hl(14, 16, 18, K); c.vl(15, 19, 20, K)
    else:
        c.hl(14, 16, 19, M)                                       # hasp hanging open
    c.outline(K)
    lint(c, name='locked-chest')
    return c


def reward_chest(open_):
    c = Canvas(30, 27)
    chest_body(c, 16)
    if open_:
        dome = smooth_mask(ellipse_mask(1, 0, 28, 12) & {(x, y) for x in range(30) for y in range(1, 7)})
        fill_mask(c, dome, L); c.hl(3, 26, 2, C); c.hl(1, 28, 6, D)
        c.rect(2, 7, 27, 14, fill=INK); c.hl(2, 27, 7, M)
        paint(c, [".o.", "oWo", ".o."], 13, 4, {'o': D, 'W': W})        # clasp on the raised lid
    else:
        dome = smooth_mask(ellipse_mask(0, 9, 29, 25) & {(x, y) for x in range(30) for y in range(9, 16)})
        fill_mask(c, dome, C)
        for x, y in dome:
            if y == 11 and 3 < x < 26: c.px(x, y, W)
            if y == 14: c.px(x, y, L)
    c.hl(0, 29, 15, D)                                            # seam
    # band across the body
    c.hl(1, 28, 19, D); c.hl(1, 28, 20, L); c.hl(1, 28, 21, D)
    # corner guards
    for x0 in (1, 25):
        c.rect(x0, 16, x0 + 3, 25, fill=M); c.vl(x0 + (1 if x0 == 1 else 2), 17, 24, L)
    if not open_:
        paint(c, ["..o..", ".oWo.", "oWMMo", ".oMo.", "..o.."], 13, 13, {'o': D, 'W': W, 'M': M})
    c.outline(K)
    lint(c, name='reward-chest')
    return c


# ---------------------------------------------------------------- seals 23x33
def seal(state):
    c = Canvas(23, 33)
    stone = ellipse_mask(1, 8, 21, 22) | {(x, y) for x in range(1, 22) for y in range(15, 30)}
    stone = {(x, y) for x, y in stone if y >= 8}
    if state == 'broken':
        stone = {(x, y) for x, y in stone if not (x - 11 > (y - 9) * 1.0 and y < 21)}
    stone = smooth_mask(stone)
    fill_mask(c, stone, C)
    for x, y in stone:
        if x == 2 and y > 12: c.px(x, y, W)
        if x == 20 and y > 12: c.px(x, y, L)
    if state == 'broken':                                          # broken face reads lighter
        for x, y in stone:
            if (x + 1, y - 1) not in stone and x > 11 and y < 22: c.px(x, y, L)
    cx, cy = 11, 18
    ring = {(cx + dx, cy + dy) for dx in range(-4, 5) for dy in range(-4, 5) if abs(dx) + abs(dy) == 4}
    core = {(cx + dx, cy + dy) for dx in range(-2, 3) for dy in range(-2, 3) if abs(dx) + abs(dy) <= 2}
    if state == 'broken':
        ring = {(x, y) for x, y in ring if not (x > 12 and y < 18)}
    for x, y in ring:
        if (x, y) in stone: c.px(x, y, D)
    for x, y in core:
        if (x, y) in stone:
            c.px(x, y, GLOW if state == 'active' else L)
    if state == 'active':
        c.px(cx, cy, W); c.px(cx - 1, cy, lighten(GLOW, .5)); c.px(cx, cy - 1, lighten(GLOW, .5))
    if state == 'broken':
        c.line([(8, 22), (7, 23)], D); c.vl(7, 24, 26, D)
    # rubble plinth
    c.rect(0, 29, 22, 32, fill=L); c.hl(0, 22, 29, D); c.hl(1, 21, 30, C)
    for x in (5, 11, 17): c.vl(x, 30, 32, M)
    c.outline(K)
    lint(c, name='seal-' + state)
    return c


if __name__ == '__main__':
    out = {
        'exit-sealed': exit_arch(True), 'exit-open': exit_arch(False),
        'iron-gate-body': gate_body(), 'iron-gate-tip': gate_tip(),
        'altar-before': altar(False), 'altar-after': altar(True),
        'locked-chest-closed': locked_chest(False), 'locked-chest-open': locked_chest(True),
        'reward-chest-closed': reward_chest(False), 'reward-chest-open': reward_chest(True),
        'seal-dormant': seal('dormant'), 'seal-active': seal('active'), 'seal-broken': seal('broken'),
    }
    for k, v in out.items():
        v.native().save(f'out/{k}.png')
