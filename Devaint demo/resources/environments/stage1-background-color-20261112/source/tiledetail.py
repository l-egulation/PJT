"""Big-shape depth for the coloured soil (option C): a shade band under the grass, lit left / shaded right / dark bottom
edges, and per variant one large feature (a buried rock, a dark soil patch, a lit patch with a small stone).
Everything works on 2px dots inside each 32px tile and never crosses a tile edge, so tiles stay seamless."""
import numpy as np
import tilecolour as TC
from scene import SHAPES

C = TC.OPTIONS['C']['soil']
SH1, SH2, LT = (142, 102, 76), (124, 88, 66), (172, 128, 96)
TOPSOIL = (168, 124, 92)   # the warmer, looser layer right under the grass
TOP_BOTTOM = [13, 13, 13, 14, 14, 13, 13, 12, 12, 13, 13, 14, 14, 14, 13, 13]
ROCK, ROCK_HI, ROCK_SH, ROCK_LINE = (150, 134, 118), (182, 168, 150), (118, 104, 92), (90, 64, 50)
# under-grass shade bottom row per dot column (ends fixed at 9 so neighbours meet), runs of 2+ dots
WAVES = [[9, 9, 9, 10, 10, 10, 9, 9, 8, 8, 8, 9, 9, 10, 10, 9], [9, 9, 8, 8, 8, 9, 9, 9, 10, 10, 10, 10, 9, 9, 9, 9],
         [9, 9, 10, 10, 9, 9, 9, 8, 8, 9, 9, 10, 10, 10, 9, 9], [9, 9, 9, 9, 10, 10, 10, 9, 9, 8, 8, 9, 9, 9, 9, 9]]

STONE = ["..####..",
         ".#hhhh##",
         "#hmmmmh#",
         "#mmmmms#",
         ".##ssss#",
         "...####."]
STONE_C = {'#': ROCK_LINE, 'h': ROCK_HI, 'm': ROCK, 's': ROCK_SH}

def lens(c, ok, x0, y, w, colour):
    """a flat soil lens: 2 dots tall in the middle, 1 dot at the tapered ends (a sediment layer, never a round blob)"""
    for x in range(x0, x0 + w):
        if 0 <= x < 16 and ok[y, x]: c[y, x] = colour
    for x in range(x0 + 2, x0 + w - 2):
        if 0 <= x < 16 and y + 1 < 16 and ok[y + 1, x]: c[y + 1, x] = colour

def stone(c, soil, x0, y0, flip):
    for j, row in enumerate(STONE):
        for i, ch in enumerate(row[::-1] if flip else row):
            x, y = x0 + i, y0 + j
            if ch != '.' and 0 <= x < 16 and 0 <= y < 16 and soil[y, x]: c[y, x] = STONE_C[ch]

def root(c, ok, x, y, steps):
    for dx in steps:
        x += dx
        if 0 <= x < 16 and y < 16 and ok[y, x]: c[y, x] = SH2
        y += 1

def detail_tile(o, c, mask, variant, seed):
    rng = np.random.default_rng(seed)
    ink = np.all(o[..., :3] == TC.INK, -1); opaque = o[..., 3] > 0
    top = not mask & 1
    soil = opaque & ~ink
    if top: soil[:7] = False
    fill = soil & np.all(o[..., :3] == TC.FILL, -1)
    inner = fill.copy(); inner[:, [1, 14]] = False; inner[14] = False
    if top:                                    # shade cast by the grass, and a few roots hanging from it
        for x in range(16):
            col = fill[7:WAVES[variant][x] + 1, x]; c[7:WAVES[variant][x] + 1, x][col] = SH1
        for x in range(16):
            for y in range(WAVES[variant][x] + 1, TOP_BOTTOM[(x + variant * 4) % 16] + 1):
                if fill[y, x]: c[y, x] = TOPSOIL
        for _ in range(1 + variant % 2):
            x = int(rng.integers(3, 13)); y = WAVES[variant][x] + 1; s = int(rng.choice([-1, 1]))
            root(c, inner, x, y, [0, 0, s, 0, s][:3 + int(rng.integers(0, 3))])
    if not mask & 8: c[:, 1][soil[:, 1]] = LT          # lit left face
    if not mask & 2: c[:, 14][soil[:, 14]] = SH1       # shaded right face
    if not mask & 4: c[14][soil[14]] = SH2             # dark underside
    lo = 12 if top else 2
    if top: return                             # the top tile already has grass shade, topsoil and roots
    if variant == 0:
        lens(c, inner, int(rng.integers(1, 4)), int(rng.integers(lo, 13)), int(rng.integers(10, 14)), SH1)
    elif variant == 1:
        lens(c, inner, int(rng.integers(1, 5)), int(rng.integers(lo, 13)), int(rng.integers(9, 12)), LT)
    elif variant == 2:
        lens(c, inner, int(rng.integers(4, 8)), int(rng.integers(lo, 13)), int(rng.integers(6, 8)), SH1)
    # variant 3 stays plain so the ground is not busy

def detail(orig_atlas, coloured):
    out = coloured.copy()
    for k in range(len(SHAPES) * 4):
        ty, tx = k // 16 * 32, k % 16 * 32
        o = orig_atlas[ty:ty + 32:2, tx:tx + 32:2]; c = out[ty:ty + 32:2, tx:tx + 32:2, :3].copy()
        detail_tile(o, c, SHAPES[k // 4], k % 4, k)
        out[ty:ty + 32, tx:tx + 32, :3] = np.repeat(np.repeat(c, 2, 0), 2, 1)
    return out

# buried stones replace the sketch's embedded pebbles (the game scatters them sparsely: 1 in 19 inner cells)
STONES = [STONE,
          ["..###...",
           ".#hhh##.",
           "#hmmmms#",
           ".##sss#.",
           "...###.."],
          ["...####....",
           ".##hhhh###.",
           "#hhmmmmmmh#",
           "#mmmmmmsss#",
           ".###ssss##.",
           "....####..."]]

def stone_sprite(i):
    from PIL import Image
    s = STONES[i]; h, w = len(s), len(s[0])
    a = np.zeros((32, 32, 4), np.uint8); x0, y0 = (16 - w) // 2, (16 - h) // 2
    for j, row in enumerate(s):
        for k, ch in enumerate(row):
            if ch != '.': a[(y0 + j) * 2:(y0 + j) * 2 + 2, (x0 + k) * 2:(x0 + k) * 2 + 2] = STONE_C[ch] + (255,)
    return Image.fromarray(a)

# ---------- option E: hollow like chapter 2 (only a rim of soil along open faces, the inside falls to a dark void)
VOID, VOID_LT = (88, 62, 48), (98, 70, 54)
EDGE_WAVE = [0, 0, 1, 1, 1, 0, 0, -1, -1, 0, 0, 1, 1, 0, -1, -1]   # period 16 dots in x and y -> seamless

def air_regions(mask):
    """open neighbour cells as rectangles in this tile's image dots. The image starts 4 dots above the cell
    (tiles are lifted 8px), the top surface sits at dot 4 and the underside is inset to dot 16."""
    N, E, S, W = mask & 1, mask & 2, mask & 4, mask & 8
    inf = 99; rows = {'n': (-inf, 4), 'm': (4, 20), 's': (20, inf)}; cols = {'w': (-inf, 0), 'c': (0, 16), 'e': (16, inf)}
    out = []
    if not N: out.append((cols['c'], rows['n']))
    if not S: out.append((cols['c'], (16, inf)))
    if not W: out.append((cols['w'], rows['m']))
    if not E: out.append((cols['e'], rows['m']))
    for bit, c, r in ((16, 'e', 'n'), (32, 'e', 's'), (64, 'w', 's'), (128, 'w', 'n')):
        if not mask & bit: out.append((cols[c], rows[r]))
    return out

def depth(mask):
    y, x = np.mgrid[0:16, 0:16] + .5
    d = np.full((16, 16), 99.0)
    for (x0, x1), (y0, y1) in air_regions(mask):
        dx = np.maximum(np.maximum(x0 - x, x - x1), 0); dy = np.maximum(np.maximum(y0 - y, y - y1), 0)
        d = np.minimum(d, np.hypot(dx, dy))
    wave = np.array([[EDGE_WAVE[(i + j + 4) % 16] for i in range(16)] for j in range(16)])
    return d + wave

def void_tile(o, c, mask, variant, seed):
    detail_tile(o, c, mask, 3, seed)           # grass shade, topsoil, roots and lit/shaded faces, no lenses
    if not mask & 1 and variant != 3:
        rng = np.random.default_rng(seed)
        for _ in range(1 + variant % 2):
            x = int(rng.integers(3, 13)); y = WAVES[3][x] + 1; s = int(rng.choice([-1, 1]))
    ink = np.all(o[..., :3] == TC.INK, -1); opaque = o[..., 3] > 0
    soil = opaque & ~ink
    if not mask & 1: soil[:7] = False
    d = depth(mask)
    c[soil & (d >= 9) & (d < 11)] = SH2
    c[soil & (d >= 11)] = VOID
    # a faint long seam in the void on some inner tiles keeps it from reading as a hole
    if mask == 255 and variant == 1:
        rng = np.random.default_rng(seed + 7); y = int(rng.integers(4, 13)); x0 = int(rng.integers(0, 5))
        lens(c, soil & (d >= 11), x0, y, 12, VOID_LT)

def hollow(orig_atlas, coloured):
    out = coloured.copy()
    for k in range(len(SHAPES) * 4):
        ty, tx = k // 16 * 32, k % 16 * 32
        o = orig_atlas[ty:ty + 32:2, tx:tx + 32:2]; c = out[ty:ty + 32:2, tx:tx + 32:2, :3].copy()
        void_tile(o, c, SHAPES[k // 4], k % 4, k)
        out[ty:ty + 32, tx:tx + 32, :3] = np.repeat(np.repeat(c, 2, 0), 2, 1)
    return out

def dim(img, t=.55):
    a = np.array(img).astype(float); m = a[..., 3] > 0
    a[m, :3] = a[m, :3] * (1 - t) + np.array(VOID) * t
    from PIL import Image
    return Image.fromarray(a.astype(np.uint8))
