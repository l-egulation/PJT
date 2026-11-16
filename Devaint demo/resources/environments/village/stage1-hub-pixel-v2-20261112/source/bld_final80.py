"""Final tidy pass on the 80 % buildings (2026-11-12, after the user's reference on pixel line rules).
Atelier: the brush assembly is rebuilt in clear layers - roof and wall behind, the whole palette oval, then three
brushes made from ONE template (same head, ferrule, handle and point; only place, height and paint differ), upright,
in a clean holder plank, casting a one-dot shadow on what is behind them.
House / atelier arches: keystone + level jamb joints (bld_detail.clean_arch_c)."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from px import *
import bld_detail as B
import bld_detail2 as E
import bld_detail4 as F
import bld_shrink as S
import bld_refine80 as R
import bld_polish as PL
import bld_roofs as RF

OUT = Path(__file__).parent.parent / "bld2"
PAINTS = tuple(p + s for p in ('paint_r', 'paint_y', 'paint_b') for s in ('', '_hi', '_lo'))
FERR = ('cloth', 'cloth_lo', 'stone_hi', 'stone_lo', 'stone', 'metal_hi', 'metal')
WOODS = ('wood', 'wood_hi', 'wood_lo', 'wood_mid')
ROOFK = ('roof', 'roof_hi', 'roof_lo')
PLAS = ('plaster', 'plaster_lo', 'plaster_mid', 'plaster_sh')
LEAFK = ('leaf', 'leaf_hi', 'leaf_lo')

# ---------------------------------------------------------------- one brush template
HEAD = ["      #      ",   # runs down each side 2, 2, 2, 3, 4, then the belly 4, and 2, 1 back in
        "     #H#     ",
        "     #H#     ",
        "    #HBB#    ",
        "    #HBB#    ",
        "   #HBBBD#   ",
        "   #HBBBD#   ",
        "  #BHBBBDD#  ",
        "  #BHBBBDD#  ",
        "  #BHBBBDD#  ",
        " #BBHBBBBDD# ",
        " #BBHBBBBDD# ",
        " #BBHBBBBDD# ",
        " #BBBBBBBDD# ",
        "#BBBBBBBBBDD#",
        "#BBDBBBDBBDD#",
        "#BBDBBBDBBDD#",
        "#BBDBBBDBBDD#",
        " #BDBBBDBBD# ",
        " #BDBBBDBBD# ",
        "  #DBBBDBD#  "]
FERRULE = ["#########",
           "#ThTTTTl#",
           "#ThTTTTl#",
           "#ThTTTTl#",
           "#ThTTTTl#",
           "#MMMMMMM#",
           "#ThTTTTl#",
           "#########"]
HANDLE = "#Wmwwv#"
POINT = [" #Wwv# ", " #Wwv# ", "  #w#  ", "  #w#  ", "   #   "]
BRUSHES = [dict(c=18, tip=11, paint='paint_r'), dict(c=31, tip=3, paint='paint_y'), dict(c=44, tip=20, paint='paint_b')]
PLANK = (13, 67, 49, 74)            # x0, y0, x1, y1
HANDLE_LAST = 80                    # last full-width row; the point follows
STUB = (29, 87, 60, 91)


def roof_edge(y):
    return 37 - y                   # the roof's left end: one dot per row (1:1)


def dom(vals, default):
    vals = list(vals)
    return max(set(vals), key=vals.count) if vals else default


def clear_brush_zone(key):
    """take the old brushes, plank and stub out and put back what is behind them: sky, roof rows, wall rows"""
    H, W = key.shape
    bm = np.zeros(key.shape, bool)
    bm[:43, :51] = np.isin(key[:43, :51], PAINTS + FERR + WOODS)
    near = ndimage.binary_dilation(bm, structure=np.ones((3, 3)))
    bm2 = np.zeros(key.shape, bool); bm2[:43, 51:58] = np.isin(key[:43, 51:58], PAINTS)
    near2 = ndimage.binary_dilation(bm2, structure=np.ones((3, 3)))
    for y in range(0, 17):                                           # sky left of the chimney
        for x in range(0, 40): key[y, x] = ''
    for y in range(17, 43):                                          # roof rows
        e = roof_edge(y) if y <= 37 else 0
        row = [key[y, x] for x in range(52, W) if key[y, x] in ROOFK + ('in',)]
        inkrow = row.count('in') > len(row) * .6
        fillv = 'in' if inkrow else dom((v for v in row if v in ROOFK), 'roof')
        for x in range(0, 51):
            v = key[y, x]
            if x < e: key[y, x] = ''
            elif y == 17: key[y, x] = 'out' if x <= 44 else v
            elif x == e and y <= 41: key[y, x] = 'out'
            elif y == 42: key[y, x] = '' if x == 0 else 'out' if x <= 8 else 'in'
            elif v in ROOFK: pass
            elif v == 'in' and (inkrow or not near[y, x]): pass
            else: key[y, x] = fillv
        for x in range(51, 58):                                      # what is left of the old blue head
            if key[y, x] in PAINTS or (key[y, x] == 'in' and near2[y, x] and not inkrow): key[y, x] = fillv
    for y in range(43, 67):                                          # wall behind the brushes
        d = dom((key[y, x] for x in range(9, 48) if key[y, x] in PLAS), 'plaster')
        for x in range(9, 48): key[y, x] = d
    for y in range(67, 75):                                          # plank rows
        d = dom((key[y, x] for x in range(12, 51) if key[y, x] in PLAS), 'plaster')
        for x in range(12, 51):
            if key[y, x] not in LEAFK: key[y, x] = d
    for y in range(75, 93):                                          # old handles, points, stub board, wall shading
        d = dom((key[y, x] for x in range(12, 47) if key[y, x] == 'plaster'), 'plaster')
        for x in range(12, 49):
            v = key[y, x]
            if v in LEAFK or v in ROOFK or (x <= 13 and v == 'wood_lo'): continue
            if y >= 92 and v not in PLAS: continue
            key[y, x] = d


def put(key, rows, x, y, legend, drawn):
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch == ' ': continue
            key[y + j, x + i] = legend[ch]; drawn.add((x + i, y + j))


def draw_stub(key):
    """the small board under the holder, behind the palette"""
    x0, y0, x1, y1 = STUB
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (x, y) in R.LAST_E: continue
            edge = y in (y0, y1) or x == x0
            key[y, x] = 'in' if edge else 'wood_hi' if y == y0 + 1 else 'wood_lo' if y == y1 - 1 else 'wood'
            if y == y1 and key[y + 1, x] == 'plaster': key[y + 1, x] = 'plaster_lo'


def brown_well(key):
    """the small brown well: one clean round (runs 3-1-1), nothing hanging under it"""
    for y in range(71, 84):
        for x in range(54, 68):
            if key[y, x] in ('wood_lo', 'in'): key[y, x] = 'wood_hi'
    rows = ["...###...", ".##vvv##.", ".#vvvvv#.", "#vvvvvvv#", "#vvvvvvv#", "#vvvvvvv#", ".#vvvvv#.", ".##vvv##.", "...###..."]
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch != '.': key[72 + j, 57 + i] = 'in' if ch == '#' else 'wood_lo'


def draw_brushes(key):
    drawn = set()
    wood = {'#': 'in', 'W': 'wood_hi', 'm': 'wood_mid', 'w': 'wood', 'v': 'wood_lo'}
    fer = {'#': 'in', 'T': 'cloth_lo', 'h': 'stone_hi', 'l': 'stone_lo', 'M': 'metal_hi'}
    for b in BRUSHES:                                                # handles first (the plank lies over them)
        c, top = b['c'], b['tip'] + len(HEAD) + len(FERRULE)
        for y in range(top, HANDLE_LAST + 1):
            put(key, [HANDLE], c - 3, y, wood, drawn)
        for x in range(c - 2, c + 3): key[top, x] = 'wood_lo'       # in the ferrule's shadow
        put(key, POINT, c - 3, HANDLE_LAST + 1, wood, drawn)
    x0, y0, x1, y1 = PLANK
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            edge = y in (y0, y1) or x in (x0, x1)
            key[y, x] = 'in' if edge else 'wood_hi' if y == y0 + 1 else 'wood_lo' if y == y1 - 1 else 'wood'
            drawn.add((x, y))
    for x in (x0 + 3, x1 - 3): key[y0 + 3, x] = 'metal'              # nails
    for b in BRUSHES:
        for x in range(b['c'] - 2, b['c'] + 3): key[y1 + 1, x] = 'wood_lo'      # handles in the plank's shadow
    for b in BRUSHES:
        c, p = b['c'], b['paint']
        put(key, FERRULE, c - 4, b['tip'] + len(HEAD), fer, drawn)
        put(key, HEAD, c - 6, b['tip'], {'#': 'in', 'B': p, 'H': p + '_hi', 'D': p + '_lo'}, drawn)
    # a one-dot shadow to the right of the handles and under the plank, on wall and palette
    for x, y in sorted(drawn):
        if y < 49: continue
        for q in ((x + 1, y), (x, y + 1)):
            if q in drawn or (q[1] != y and not (y == y1 and x0 <= x <= x1)): continue
            v = key[q[1], q[0]]
            if v in ('plaster', 'plaster_mid'): key[q[1], q[0]] = 'plaster_lo'
            elif v in ('wood_hi', 'wood_mid') and q in R.LAST_E: key[q[1], q[0]] = 'wood'
    return drawn


# ---------------------------------------------------------------- the door canopy, centred over the door
CANOPY_C = 49                       # the door's centre column (keystone 46..52)
CANOPY_TOP, CANOPY_BOT = 93, 107    # top ink row, bottom ink row
POST = ["#Wwv#"] * 8 + ["#####"]    # one post shape for both, rows 108..116
POST_DX = 20                        # posts at C-20 and C+20


def _cast_local(key, casters, region, long_=8, short=2, tones=((2, 'plaster_sh'), (4, 'plaster_lo'), (8, 'plaster_mid'))):
    """the same light rule as bld_detail4.cast_shadow at 80 % length, only from `casters`, only onto `region`"""
    H, W = key.shape
    src = key.copy()
    STUCK = WOODS + ('stone', 'stone_hi', 'stone_lo')
    order = ['plaster', 'plaster_mid', 'plaster_lo', 'plaster_sh']
    for x, y in casters:
        v = src[y, x]
        if v in F.ROOF: L = long_
        elif v in STUCK: L = short
        elif v == 'in' and any(src[y + dy, x + dx] in F.ROOF for dx, dy in ((0, -1), (-1, 0), (-1, -1))): L = long_
        else: continue
        for k in range(1, L + 1):
            yy, xx = y + k, x + k
            if not (yy < H and xx < W): break
            w = src[yy, xx]
            if w in F.WALL:
                if (xx, yy) not in region: continue
                kk = k if L == long_ else k + 3
                tone = next((t for lim, t in tones if kk <= lim), None)
                if tone is None: break
                if order.index(tone) > order.index(key[yy, xx]): key[yy, xx] = tone
            elif w not in F.ROOF + STUCK + ('in',): break


def canopy(key):
    """redraw the door canopy as one even trapezoid (1:1 sides, 8-dot tiles) centred on the door, with two identical
    posts under it, and recast its shadow; the palette stays in front of its upper right corner"""
    H, W = key.shape
    pal = set(R.LAST_E)
    C, T, Bt = CANOPY_C, CANOPY_TOP, CANOPY_BOT
    half_top = 19                                                     # top 30..68, eaves 20..78
    # 1. the old canopy, its loose bits above and the old posts
    gone = set()
    for y in range(92, Bt + 1):
        for x in range(18, 86):
            if (x, y) in pal: continue
            v = key[y, x]
            if v in ROOFK or (y >= T and v == 'in' and 22 <= x <= 82) or (y == 92 and v in WOODS):
                gone.add((x, y))
    for y in range(Bt + 1, 118):
        for x in list(range(25, 34)) + list(range(64, 72)):
            if key[y, x] in WOODS + ('in',): gone.add((x, y))
    for x, y in gone:
        if y == 92: key[y, x] = 'plaster_lo'; continue              # under the stub board: its shadow
        if y >= 101: key[y, x] = 'plaster'; continue
        step = 1 if x > C else -1                                     # the wall next to it in the same row
        xx = x + step
        while 0 <= xx < W and ((xx, y) in gone or key[y, xx] not in F.WALL): xx += step
        key[y, x] = key[y, xx] if 0 <= xx < W else 'plaster'
    # 2. the shadow below the canopy is drawn again from scratch
    region = {(x, y) for y in range(T, 120) for x in range(18, 91)}
    for y in range(101, 120):
        for x in range(18, 91):
            if key[y, x] in F.WALL: key[y, x] = 'plaster'
    # 3. the roof
    def edges(y):
        d = min(y - T, 10)
        return C - half_top - d, C + half_top + d
    drawn = set()
    def put1(x, y, v):
        if (x, y) in pal: return
        key[y, x] = v; drawn.add((x, y))
    for y in range(T, Bt + 1):
        l, r = edges(y)
        if y == Bt:
            for x in range(l + 1, r): put1(x, y, 'in')
            continue
        for x in range(l, r + 1):
            if y == T or x in (l, r): put1(x, y, 'in'); continue
            col = (x - C) % 7                                         # one-dot seams every 7, one on the centre: symmetric
            if y == T + 1: v = 'roof_lo' if col == 0 else 'roof_hi'   # ridge course
            elif y == T + 2: v = 'roof_lo' if col == 0 else 'roof'
            elif y == T + 3: v = 'in'
            elif y <= 102: v = 'roof_lo' if col == 0 else 'roof_hi' if col == 1 and y <= T + 6 else 'roof'
            elif y == 103: v = 'roof_lo' if col == 0 else 'roof'
            elif y == 104: v = 'in' if col == 0 else 'roof_lo'
            elif y == 105: v = 'roof' if col == 0 else 'in'          # round tile ends, the course below between them
            else: v = 'roof_lo'
            put1(x, y, v)
    # 4. two posts of one shape
    wood = {'#': 'in', 'W': 'wood_hi', 'w': 'wood', 'v': 'wood_lo'}
    for c in (C - POST_DX, C + POST_DX):
        for j, row in enumerate(POST):
            for i, ch in enumerate(row):
                key[Bt + 1 + j, c - 2 + i] = wood[ch]; drawn.add((c - 2 + i, Bt + 1 + j))
    # 5. shadow from the canopy, the posts and the door ring under it
    casters = drawn | {(x, y) for y in range(Bt + 1, 120) for x in range(18, 91) if key[y, x] in ('stone', 'stone_hi', 'stone_lo') + WOODS}
    _cast_local(key, sorted(casters, key=lambda p: (p[1], p[0])), region)



def house_porch_roof(key):
    """the house's porch roof, redrawn like the atelier canopy: one even trapezoid (1:1 sides, 7-dot tiles with a
    seam on the centre) centred on the door and its two posts (door jambs 23..26 / 46..49, centre 36); its top stays
    behind the balcony beam; the shadow under it is cast again"""
    H, W = key.shape
    C, TOP, COURSE, SCAL, BOT = 36, 76, 77, 83, 87
    EAVE_L = 13                                                       # eaves 13..59
    # 1. the old porch roof (its right end reached x 75) and its shadow
    gone = set()
    for y in range(74, BOT + 1):
        for x in range(12, 78):
            v = key[y, x]
            if v in ROOFK or (v == 'in' and (y >= TOP or x >= 64)): gone.add((x, y))
    for x, y in gone: key[y, x] = 'plaster'
    for x in range(12, 62):                                           # the beam's lower row (beam 11..62)
        if key[74, x] in WOODS: key[74, x] = 'wood_lo'
    region = {(x, y) for y in range(74, 102) for x in range(12, 86)}
    for x, y in region:
        if key[y, x] in F.WALL: key[y, x] = 'plaster'
    # 2. the roof
    def edges(y):
        l = max(EAVE_L, 19 - (y - TOP))
        return l, 2 * C - l
    drawn = set()
    for y in range(TOP, BOT + 1):
        l, r = edges(min(y, BOT - 1))
        if y == BOT:
            for x in range(l + 1, r): key[y, x] = 'in'; drawn.add((x, y))
            continue
        for x in range(l, r + 1):
            col = (x - C) % 7
            if x in (l, r) or y == COURSE: v = 'in'
            elif y == TOP: v = 'roof_lo' if col == 0 else 'roof_hi'
            elif y < SCAL: v = 'roof_lo' if col == 0 else 'roof_hi' if col == 1 and y <= COURSE + 2 else 'roof'
            elif y == SCAL: v = 'roof_lo' if col == 0 else 'roof'
            elif y == SCAL + 1: v = 'in' if col == 0 else 'roof_lo'
            elif y == SCAL + 2: v = 'roof' if col == 0 else 'in'
            else: v = 'roof_lo'
            key[y, x] = v; drawn.add((x, y))
    # 3. shadow from the roof, the posts, the beam and the door / window rings
    casters = drawn | {(x, y) for y in range(70, 102) for x in range(0, 90)
                       if key[y, x] in ('stone', 'stone_hi', 'stone_lo') + WOODS}
    _cast_local(key, sorted(casters, key=lambda p: (p[1], p[0])), region)


def outer_outline(key, box):
    """inner ink that borders the empty background is the outer outline"""
    H, W = key.shape
    x0, y0, x1, y1 = box
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if key[y, x] != 'in': continue
            if any(not (0 <= x + dx < W and 0 <= y + dy < H) or key[y + dy, x + dx] == '' for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                key[y, x] = 'out'


def atelier():
    key = S.atelier80()
    R.stray_roof(key)
    R.regrow_ivy(key, 8, 19, 70, 140, 10, seed=3, spread=(-7, 1, -4))
    R.even_edges_atelier(key)
    clear_brush_zone(key)
    R.clean_palette(key, protect=lambda x, y: x >= 103 and y >= 79, hole_box=(49, 59, 58, 70), left=36)
    brown_well(key)
    draw_stub(key)
    draw_brushes(key)
    canopy(key)
    PL.atelier(key)
    # RF.retile(key, RF.ATELIER)  -- the user prefers the drawn tiles
    PL.atelier_details(key)
    outer_outline(key, (0, 0, 60, 95))
    E.declump_key(key)
    return key


def house():
    key = R.house()
    PL.house(key)
    house_porch_roof(key)
    # RF.retile(key, RF.HOUSE)  -- the user prefers the drawn tiles
    E.declump_key(key)
    key = roof_over_wall(key)
    PL.house_details(key)
    return key


ROOF_LAST_ROW = 35                  # the eave's bottom line; the wall starts on row 36


def roof_over_wall(key):
    """the roof (eaves 0..103, centre 51.5) sat one dot right of the wall (4..97, centre 50.5): one column more on
    the canvas, everything below the eave moves one dot right, so both centre on 51.5"""
    H, W = key.shape
    out = np.full((H, W + 1), '', dtype=key.dtype)
    out[:ROOF_LAST_ROW + 1, :W] = key[:ROOF_LAST_ROW + 1]
    out[ROOF_LAST_ROW + 1:, 1:] = key[ROOF_LAST_ROW + 1:]
    out[ROOF_LAST_ROW, 5] = 'out'                                    # above the wall's left outline now
    out[ROOF_LAST_ROW, 97] = 'in'                                     # above the wall now, not the overhang
    return out


if __name__ == '__main__':
    f = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 18)
    for name, fn in (('house', house), ('atelier', atelier)):
        key = fn()
        for v, P in (('color', F.CO4), ('sketch', F.SK4)):
            c = B.paint_keys(key, P); lint(c, colors=[K, P['in'], P['out']], name=f'{name} {v}')
            c.im.save(OUT / f'v80-{name}-{v}.png')
