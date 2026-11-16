"""The two big roofs, tiled again on one even pattern (2026-11-12, user: 'more detail, all over'): the ridge and the
tile courses keep their rows and outlines; inside each course the seams come every 8 dots (the next course half a
tile over), the first body rows carry a lit stripe beside each seam, and every course ends in the canopy's round
tile ends. The verges: lit on the left, shaded on the right."""
import numpy as np

ROOFK = ('roof', 'roof_hi', 'roof_lo')
INK = ('in', 'out')
P = 8
HOUSE = dict(ridge=(11, 12, 13), courses=[((15, 16, 17, 18, 19), (20, 21, 22), 'roof', 0),
                                          ((23, 24, 25, 26, 27, 28, 29, 30), (31, 32, 33), 'roof_lo', 4)])
ATELIER = dict(ridge=(18, 19, 20), courses=[((22, 23, 24, 25, 26, 27), (28, 29, 30), 'roof', 0),
                                            ((31, 32, 33, 34, 35, 36, 37), (38, 39, 40), 'roof_lo', 4)])


def roof_mask(key, rows, scal_rows):
    H, W = key.shape
    m = set()
    for y in rows:
        for x in range(1, W - 1):
            v = key[y, x]
            if v in ROOFK: m.add((x, y))
            elif v == 'in' and y in scal_rows:
                nb = [key[y + dy, x + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dx or dy)]
                if all(n in ROOFK + INK + ('',) for n in nb): m.add((x, y))
    return m


def retile(key, spec):
    scal_rows = {y for _, s, _, _ in spec['courses'] for y in s} | set(spec['ridge']) | {y for b, _, _, _ in spec['courses'] for y in b}
    rows = list(spec['ridge']) + [y for b, s, _, _ in spec['courses'] for y in b + s]
    m = roof_mask(key, rows, scal_rows)
    def put(x, y, v):
        if (x, y) in m: key[y, x] = v
    for i, y in enumerate(spec['ridge']):                       # ridge cap: one lit row, seams every 8, offset 4
        for x in range(key.shape[1]):
            c = (x - 4) % P
            put(x, y, 'roof_hi' if i == 0 else 'roof_lo' if c == 0 else 'roof')
    for body, (ya, yb, yc), peek, off in spec['courses']:
        n = len(body)
        for i, y in enumerate(body):
            for x in range(key.shape[1]):
                c = (x - off) % P
                if c == 0: v = 'roof_lo'                                  # seam
                elif i == 0: v = 'roof_lo'                                # the course above shades the tile's top
                elif c == 1 and i <= n - 3: v = 'roof_hi'                 # the lit left flank of each tile
                elif c == 2 and i in (1, 2): v = 'roof_hi'                # its rounded top light
                elif c == P - 1 and i >= n - 3: v = 'roof_lo'             # the shaded right flank, low down
                else: v = 'roof'
                put(x, y, v)
        for x in range(key.shape[1]):
            c = (x - off) % P
            put(x, ya, 'roof_lo' if c == 0 else 'roof')
            put(x, yb, 'in' if c == 0 else 'roof_lo')
            put(x, yc, peek if c == 0 else 'in')
    # verges: the dot inside the outline on each slope
    for y in rows:
        xs = [x for x in range(key.shape[1]) if (x, y) in m]
        if not xs: continue
        l, r = min(xs), max(xs)
        if key[y, l - 1] == 'out' and key[y, l] in ROOFK: key[y, l] = 'roof_hi'
        if key[y, r + 1] == 'out' and key[y, r] in ROOFK: key[y, r] = 'roof_lo'
