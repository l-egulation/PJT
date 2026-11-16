"""Sky path (하늘길) parkour props as 2x2-dot pixel art. Logical canvas; native() doubles.
Floating stones / crumbling stone / rope board / arrow launcher / arrow."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
from px import *
from s_tower import X1, X2, X3

OUT = os.path.join(os.path.dirname(__file__), 'sky_out')
os.makedirs(OUT, exist_ok=True)
LAWN = os.path.join(os.path.dirname(__file__), '..', '..', 'stage1-pixel-lawn-wood-32', 'runtime')
HOT = h('d4502a'); HOT2 = h('e98a5c'); HOT3 = h('ffd38a')
TOP = 4          # walking line row (logical) = texel 8, as the lawn tiles


def lawn_rows(variant, rows=range(0, 5)):
    im = Image.open(os.path.join(LAWN, f'terrain/tile-010-{variant}.png')).convert('RGBA')
    return [[im.getpixel((x * 2, y * 2)) for x in range(16)] for y in rows]


def silhouette(width, depth, tip, wob=()):
    """rows TOP..: flat sides for 4 rows, then an even taper to a rounded tip of `tip` dots"""
    span = {}
    for y in range(TOP, TOP + 5): span[y] = (0, width - 1)
    span[TOP] = (1, width - 2)
    rest = depth - 5
    for i in range(rest):
        t = (i + 1) / rest
        half = (width / 2) * math.sqrt(max(0.0, 1 - t ** 1.6)) if i < rest - 1 else tip / 2
        half = max(half, tip / 2)
        cx = (width - 1) / 2
        span[TOP + 5 + i] = (round(cx - half + .5), round(cx + half - .5))
    mask = {(x, y) for y, (a, b) in span.items() for x in range(a, b + 1)}
    return smooth_mask(mask)


def stones(c, mask, seeds, band=TOP + 5):
    """split the mask into stones (nearest seed); slab seeds above `band`, boulders below"""
    def owner(x, y):
        pool = [s for s in seeds if (s[1] < band) == (y < band)]
        return min(range(len(seeds)), key=lambda i: (x - seeds[i][0]) ** 2 * seeds[i][2] + (y - seeds[i][1]) ** 2 if seeds[i] in pool else 1e9)
    own = {p: owner(*p) for p in mask}
    edge = rings(mask)[0]
    seam = set()
    for (x, y), o in own.items():
        if (x, y) in edge: continue
        for nx, ny in ((x + 1, y), (x, y + 1)):
            if (nx, ny) in own and own[(nx, ny)] != o and (nx, ny) not in edge:
                seam.add((x, y)); break
    for p in mask: c.px(*p, C)
    for x, y in mask:
        if (x, y) in edge or (x, y) in seam: continue
        below, above = (x, y + 1), (x, y - 1)
        if below in seam or below in edge: c.px(x, y, L)
        elif above in seam or above in edge: c.px(x, y, W)
    for p in seam: c.px(*p, M)
    for p in edge: c.px(*p, K)
    return own


def overlay_lawn(c, x0, x1, variant=0, cut=0):
    """lawn grass rows 0..4 over the top line between x0..x1 (tile pattern, world-aligned)"""
    rows = lawn_rows(variant)
    for y, r in enumerate(rows):
        for x in range(x0, x1 + 1):
            p = r[(x + cut) % 16]
            if p[3] == 0: continue
            if y == 4 and c.get(x, y)[3] == 0: continue
            c.px(x, y, p)


def root(c, x, y, n, lean):
    """hanging root: one-dot dark line that steps aside once, with a leaf dot"""
    for i in range(n):
        c.px(x + (lean if i >= n // 2 else 0), y + i, D)
    c.px(x + lean + lean, y + n - 2, D)


def rock_mask(width, rocks, slab_rows=5):
    m = {(x, y) for x in range(width) for y in range(TOP, TOP + slab_rows)}
    m -= {(0, TOP), (width - 1, TOP)}
    for cx, cy, rx, ry in rocks:
        m |= {(x, y) for x in range(width) for y in range(TOP, 32)
              if ((x + .5 - cx) / rx) ** 2 + ((y + .5 - cy) / ry) ** 2 <= 1.0}
    return smooth_mask(m)


RAMP = [W, C, X1, L, X2, M, X3]


def tone(i):
    return RAMP[max(0, min(len(RAMP) - 1, i))]


def rock_stones(c, width, rocks, slab_seams):
    """a cluster of rocks shaded by tone (light from the upper left): each rock its own gradient, crevices are the
    darker rims where one rock turns under the next; flat stones on top make the walking surface"""
    mask = rock_mask(width, rocks)
    band = TOP + 5
    def owner(x, y):
        if y < band:
            return sum(1 for s in slab_seams if x > s)
        return 100 + min(range(len(rocks)), key=lambda i: ((x + .5 - rocks[i][0]) / rocks[i][2]) ** 2 + ((y + .5 - rocks[i][1]) / rocks[i][3]) ** 2)
    own = {p: owner(*p) for p in mask}
    edge = rings(mask)[0]
    depth = max(y for _, y in mask) - TOP
    boxes = {}
    for (x, y), o in own.items():
        b = boxes.setdefault(o, [x, y, x, y])
        b[0] = min(b[0], x); b[1] = min(b[1], y); b[2] = max(b[2], x); b[3] = max(b[3], y)
    for (x, y), o in own.items():
        x0, y0, x1, y1 = boxes[o]
        if o < 100:   # flat top stones: lit top plane, front face shading left to right, dark underside
            u = (x - x0) / max(1, x1 - x0)
            i = 0 if y == TOP + 1 else (1 if u < .6 else 2) if y < band - 1 else 3
            if y == TOP + 1 and u > .8: i = 1
        else:         # boulders: rounded tone from the light, darker toward the island's underside
            cx, cy = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2
            nx, ny = (x + .5 - cx) / max(1, (x1 - x0 + 1) / 2), (y + .5 - cy) / max(1, (y1 - y0 + 1) / 2)
            v = nx * .5 + ny * .9 + (y - TOP) / max(1, depth) * .6
            i = 0 if v < -.6 else 1 if v < .45 else 2 if v < 1.0 else 3
        # crevices: a rock's rim turning under its neighbour below/right goes darker, the neighbour's lip lighter
        under = any((q in own and own[q] != o) for q in ((x, y + 1), (x + 1, y)))
        lip = any((q in own and own[q] != o) for q in ((x, y - 1), (x - 1, y)))
        # sketch-like seams: a one-dot mid-grey line where a rock turns under its neighbour
        if under: i = 5
        c.px(x, y, tone(i))
    for p in edge: c.px(*p, K)
    return mask


STONES = {
    1: dict(w=16, seams=(9,), rocks=[(4.5, 10, 4.5, 4), (11.5, 10.5, 4.5, 4.5), (8, 15.5, 4, 4.5)]),
    2: dict(w=32, seams=(11, 22), rocks=[(4.5, 10, 4.5, 4), (12.5, 11, 5, 5), (21, 11, 5, 4.5), (28, 10, 4, 4),
                                          (9, 15.5, 4, 4), (17, 17, 5, 4.5), (24.5, 15, 3.5, 3.5), (15, 21, 3.5, 3)]),
    3: dict(w=48, seams=(10, 23, 36), rocks=[(4.5, 10, 4.5, 4), (12.5, 11, 5, 5), (21.5, 11.5, 5, 5), (31, 11, 5, 5), (39.5, 10.5, 5, 4.5), (44.5, 9.5, 3.5, 3.5),
                                              (9, 15.5, 4, 4), (17, 17, 5, 4.5), (26, 18, 5, 5), (34.5, 16, 4.5, 4), (21.5, 22, 4, 3.5), (29, 22.5, 3.5, 3)]),
}


def floating(k, green):
    s = STONES[k]
    c = Canvas(s['w'], 32)
    rock_stones(c, s['w'], s['rocks'], s['seams'])
    if green:
        spans = {1: [(0, 15, 0)], 2: [(0, 13, 1), (20, 31, 2)], 3: [(0, 15, 0), (26, 39, 3)]}[k]
        for a, b, v in spans: overlay_lawn(c, a, b, v, cut=a)
        roots = {1: [(5, 18, 5, -1)], 2: [(9, 19, 5, -1), (24, 18, 6, 1)], 3: [(10, 19, 6, -1), (37, 19, 5, 1)]}[k]
        for x, y, n, lean in roots: root(c, x, y, n, lean)
    return c


def show(items, name, scale=6, gap=3):
    W_ = sum(i.w for i in items) + gap * (len(items) + 1); H_ = max(i.h for i in items) + 2 * gap
    sheet = Canvas(W_, H_); x = gap
    for i in items: sheet.blit(i, x, gap); x += i.w + gap
    big = sheet.native().resize((W_ * 2 * scale // 2, H_ * 2 * scale // 2), Image.NEAREST)
    bg = Image.new('RGBA', big.size, (236, 232, 222, 255)); bg.alpha_composite(big); bg.save(os.path.join(OUT, name))


# ---- crumbling stone: 00 whole, 01/02 cracking (shaken by turns), 03..05 giving way -----------------------------
CRUMBLE = {
    2: dict(w=32, seams=(11, 21), rocks=[(4.5, 10, 4.5, 3.5), (12, 10.5, 4.5, 4), (20.5, 11, 5, 4), (28, 10, 4, 3.5), (10, 13.5, 4, 3), (19, 14, 4.5, 3)],
            cuts=(11, 21), cracks=[[(6, 5), (6, 6), (7, 7), (7, 8), (6, 9), (6, 10), (7, 11)], [(16, 5), (16, 6), (15, 7), (15, 8), (16, 9), (16, 10)], [(26, 5), (26, 6), (27, 7), (27, 8)]]),
    1: dict(w=16, seams=(8,), rocks=[(4.5, 10, 4.5, 3.5), (11.5, 10.5, 4.5, 4), (8, 13.5, 4.5, 3)],
            cuts=(8,), cracks=[[(4, 5), (4, 6), (5, 7), (5, 8), (4, 9)], [(12, 5), (12, 6), (11, 7), (11, 8), (12, 9), (12, 10)]]),
}


def crumble_whole(k):
    s = CRUMBLE[k]; c = Canvas(s['w'], 32)
    mask = rock_stones(c, s['w'], s['rocks'], s['seams'])
    return c, mask


def chunk(c, mask, x0, x1):
    """the stone's dots between columns x0..x1 as their own outlined piece"""
    piece = {(x, y) for x, y in mask if x0 <= x <= x1}
    piece = smooth_mask(piece)
    col = {p: c.get(*p) for p in piece}
    for p in rings(piece)[0]: col[p] = K
    return col


def put(c, col, dx, dy):
    for (x, y), v in col.items(): c.px(x + dx, y + dy, v)


def pebble(c, x, y, big=False):
    if big:
        paint(c, [".K.", "KcK", ".K."], x - 1, y - 1, {'K': K, 'c': X2})
    else:
        c.px(x, y, D)


ROCKS = [
    [".KKKKK.", "KwcccxK", "KcccxlK", ".KxllK.", "..KKK.."],
    [".KKKK.", "KwccxK", "KcxllK", ".KKKK."],
]


def rock(c, x0, y0, x1, y1):
    m = smooth_mask(ellipse_mask(x0, y0, x1, y1)); edge = rings(m)[0]
    for x, y in m:
        if (x, y) in edge: c.px(x, y, K)
        elif (x, y + 1) in edge: c.px(x, y, L)
        elif (x, y - 1) in edge: c.px(x, y, W)
        else: c.px(x, y, C)


def crumbling(k):
    s = CRUMBLE[k]; frames = []
    whole, mask = crumble_whole(k); frames.append(whole)
    for f in (1, 2):
        c, _ = crumble_whole(k)
        for crack in s['cracks']:
            for x, y in (crack[:4] if f == 1 else crack): c.px(x, y, K)
        if f == 2:
            pebble(c, s['w'] // 2 - 3, 19); pebble(c, s['w'] // 2 + 4, 21)
        frames.append(c)
    bounds = [0] + [x + 1 for x in s['cuts']] + [s['w']]
    parts = [chunk(whole, mask, bounds[i] + (1 if i else 0), bounds[i + 1] - 1) for i in range(len(bounds) - 1)]
    drops3 = {2: [(0, 2), (0, 4), (0, 3)], 1: [(0, 2), (0, 4)]}[k]
    drops4 = {2: [(-2, 8), (0, 11), (2, 9)], 1: [(-2, 8), (2, 10)]}[k]
    c = Canvas(s['w'], 32)
    for p, (dx, dy) in zip(parts, drops3): put(c, p, dx, dy)
    pebble(c, s['w'] // 2 - 2, 22); pebble(c, s['w'] // 2 + 5, 24); pebble(c, 3, 21)
    frames.append(c)
    c = Canvas(s['w'], 32)
    falls = {2: [(2, 15, 0), (13, 19, 1), (24, 16, 0)], 1: [(1, 16, 0), (10, 19, 1)]}[k]
    for x, y, kind in falls: paint(c, ROCKS[kind], x, y, {'K': K, 'w': W, 'c': C, 'x': X1, 'l': L, 'L': X2, 'b': M})
    for x, y in ([(10, 26), (21, 27)] if k == 2 else [(8, 26)]): pebble(c, x, y, True)
    frames.append(c)
    c = Canvas(s['w'], 32)
    for x, y in ([(6, 28), (s['w'] // 2 + 1, 30), (s['w'] - 7, 27)] if k == 2 else [(5, 28), (11, 30)]): pebble(c, x, y)
    frames.append(c)
    return frames


# ---- rope board: the lawn set's wood deck hung on two ropes; plank top on row 39 (texel 78) -------------------------
BOARD_TOP = 39


def wood_piece(name):
    im = Image.open(os.path.join(LAWN, f'wood/{name}.png')).convert('RGBA')
    return [[im.getpixel((x * 2, y * 2)) for x in range(16)] for y in range(16)]


def board(k):
    c = Canvas(16 * k, 48)
    names = {2: ['left', 'right'], 3: ['left', 'middle-a', 'right']}[k]
    oy = BOARD_TOP - 4
    for i, n in enumerate(names):
        rows = wood_piece(n)
        for y, r in enumerate(rows):
            for x, p in enumerate(r):
                if p[3]: c.px(i * 16 + x, oy + y, p)
    for rx in (2, 16 * k - 3):
        for y in range(0, BOARD_TOP - 2):
            c.px(rx, y, (X2, M, M)[y % 3])
        # knot: the rope wraps the plank's edge (lit top, shaded underside)
        paint(c, [".K.", "KxK", "KbK"], rx - 1, BOARD_TOP - 3, {'K': K, 'x': X1, 'b': X2})
    return c


# ---- arrow launcher (faces right; left is the mirror): middle row 8 (texel 16) at the arrow's height -----------------
LAUNCHER = [
    "............",
    "............",
    ".KKKKKKKK...",
    "KwwwwMwwxK..",
    "KwccxMcxlK..",
    "KMMMMMMMMK..",
    "KwcKKKcxKKKK",
    "KwKwwwKxKcxK",
    "KwKwDwKxKcc.",
    "KwKwxwKlKxbK",
    "KwcKKKxlKKKK",
    "KMMMMMMMMK..",
    "KwccxMcxlK..",
    "KxxllMxlbK..",
    ".KKKKKKKK...",
    "............",
]


def launcher(f):
    c = Canvas(12, 16)
    paint(c, LAUNCHER, 0, 0, {'K': K, 'w': W, 'c': C, 'x': X1, 'l': L, 'b': X2, 'L': L, 'M': M, 'D': D})
    if f >= 1: c.px(4, 8, HOT); c.px(10, 8, HOT2)
    if f >= 2:
        for x, y in ((3, 8), (5, 8), (4, 7), (4, 9)): c.px(x, y, HOT2)
        c.px(10, 8, HOT); c.px(9, 8, HOT2)
    if f >= 3:
        for x in (3, 4, 5):
            for y in (7, 8, 9): c.px(x, y, HOT)
        c.px(4, 8, HOT3); c.px(9, 8, HOT); c.px(10, 8, HOT3); c.px(11, 8, HOT)
    return c


def mirror(c):
    m = Canvas(c.w, c.h); m.im = c.im.transpose(Image.FLIP_LEFT_RIGHT); return m


# ---- arrow (faces right), 18x5 dots: one-dot shaft, ink head, light fletching -------------------------------------
ARROW = [
    "MM............K...",
    ".MM...........KK..",
    "DDDDDDDDDDDDDDDKK.",
    ".MM...........KK..",
    "MM............K...",
]


def arrow():
    c = Canvas(18, 5); paint(c, ARROW, 0, 0, {'K': K, 'M': M, 'D': D}); return c


if __name__ == '__main__':
    items = []
    for green in (False, True):
        for k in (1, 2, 3):
            c = floating(k, green)
            nm = f'floating-{"green" if green else "bare"}-{k}tile'
            lint(c, [K, M, D], nm)
            c.native().save(os.path.join(OUT, nm + '.png')); items.append(c)
    show(items, 'sheet-stones.png')
    for k in (2, 1):
        fr = crumbling(k)
        for i, c in enumerate(fr):
            lint(c, [K, M, D], f'crumbling-{k}tile-{i}'); c.native().save(os.path.join(OUT, f'crumbling-{k}tile-0{i}.png'))
        show(fr, f'sheet-crumbling-{k}.png')
    bs = [board(2), board(3)]
    for k, c in zip((2, 3), bs): lint(c, [K, M, D], f'moving-{k}'); c.native().save(os.path.join(OUT, f'moving-{k}tile.png'))
    show(bs, 'sheet-boards.png')
    ls = [launcher(f) for f in range(4)]
    for f, c in enumerate(ls):
        lint(c, [K, M, D], f'launcher-{f}')
        c.native().save(os.path.join(OUT, f'launcher-right-0{f}.png')); mirror(c).native().save(os.path.join(OUT, f'launcher-left-0{f}.png'))
    a = arrow(); lint(a, [K, M, D], 'arrow'); a.native().save(os.path.join(OUT, 'arrow-right.png')); mirror(a).native().save(os.path.join(OUT, 'arrow-left.png'))
    show(ls + [mirror(ls[0]), a], 'sheet-launchers.png', scale=10)
