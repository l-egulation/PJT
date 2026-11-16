"""Close-up polish (2026-11-12, user: 'scan slowly and fix the small flaws, the house too').
Every fix here is a named spot found on the 8x gridded tiles (scan.py)."""
import numpy as np
from scipy import ndimage
import pixel_curves as PC
import bld_refine80 as R

WALL = ('plaster', 'plaster_lo', 'plaster_mid', 'plaster_sh')
LEAF = ('leaf', 'leaf_hi', 'leaf_lo')
WOODS = ('wood', 'wood_hi', 'wood_lo', 'wood_mid')
ROOFK = ('roof', 'roof_hi', 'roof_lo')
INK = ('in', 'out')


def wall_near(key, x, y, step, skip=()):
    """the wall tone next to (x, y) along the row"""
    H, W = key.shape
    xx = x + step
    while 0 <= xx < W and (key[y, xx] not in WALL or (xx, y) in skip): xx += step
    return key[y, xx] if 0 <= xx < W else 'plaster'


def majority(key, x, y, avoid):
    H, W = key.shape
    vals = [key[y + dy, x + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)
            if (dx or dy) and 0 <= y + dy < H and 0 <= x + dx < W and key[y + dy, x + dx] not in avoid]
    return max(set(vals), key=vals.count) if vals else 'plaster'


def drop_small_leaves(key, limit):
    """ivy crumbs: leaf groups of `limit` dots or fewer that touch no other leaves"""
    m = np.isin(key, LEAF)
    lab, n = ndimage.label(m, structure=np.ones((3, 3)))
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    for i, s in enumerate(sizes):
        if s > limit: continue
        for y, x in np.argwhere(lab == i + 1):
            key[y, x] = majority(key, x, y, LEAF + INK + ('',)) if any(
                key[y + dy, x + dx] not in LEAF + INK + ('',) for dy in (-1, 0, 1) for dx in (-1, 0, 1)) else 'plaster'


# ====================================================================== atelier
def atelier_blue_leftover(key):
    """a grey-blue line and an ink stroke left of the old blue brush, in the roof right of the blue head"""
    for y in range(28, 37):
        row = [key[y, x] for x in range(55, 72)]
        ink_row = row.count('in') > len(row) * .6
        fill = 'in' if ink_row else max((v for v in row if v in ROOFK), key=row.count, default='roof')
        for x in range(50, 55):
            v = key[y, x]
            if v.startswith('glass') or (x >= 53 and v == 'in' and not ink_row): key[y, x] = fill


def atelier_white_well(key):
    """the old blue handle's strip right of the new handle goes back to palette; the white well is redrawn whole,
    two dots to the right, clear of the handle shadow and the plank end"""
    E = R.LAST_E
    for y in range(49, 67):
        for x in (48, 49, 50):
            if (x, y) not in E: continue
            key[y, x] = 'wood' if x == 48 else 'wood_hi'          # x 48: the one-dot shadow of the blue handle
    for y in range(57, 72):
        for x in range(49, 60):
            if (x, y) in E and key[y, x] in WALL + ('in',) and not (y >= 67 and x <= 49):
                key[y, x] = 'wood_hi' if not (x == 50 and 67 <= y <= 74) else 'wood'
    ring, inside = PC.ellipse_ring(51, 59, 60, 70)
    cx, cy, r = 55.5, 64.5, 5.2
    for x, y in inside:
        lo = (x - cx) / 4.5 + (y - cy) / 5.5 > .55 and ((x - cx) / 4.5) ** 2 + ((y - cy) / 5.5) ** 2 > .45
        key[y, x] = 'plaster_mid' if lo else 'plaster'
    for x, y in ring: key[y, x] = 'in'


def atelier_palette_rim(key):
    """the inner rim line: one clean ring (concentric with the rim, 0.835 of it), only on the bare board and only
    on the lower and right side where the user's drawing had it; the old dotted bits go"""
    E = R.LAST_E
    for (x, y) in E:
        if key[y, x] != 'wood': continue
        if x <= 48 or (x == 49 and y == 75) or (x == 50 and 67 <= y <= 75): continue   # drawn shadows of brush/plank
        if 38 <= x <= 49 and 75 <= y <= 86: continue
        key[y, x] = 'wood_hi'
    ring, _ = PC.ellipse_ring(42, 38, 103, 91)
    for x, y in ring:
        if y < 47 or key[y, x] != 'wood_hi': continue
        if (x, y) not in E: continue
        key[y, x] = 'wood'


def atelier_bracket(key):
    """the post that holds the banner rod: one clean '#W' strip down the red banner's edge, behind the palette"""
    E = R.LAST_E
    for y in range(83, 100):
        for x in range(96, 102):
            if (x, y) in E: continue
            v = key[y, x]
            if v in WOODS + ('in',): key[y, x] = wall_near(key, x, y, -1)
    for y in range(83, 99):
        if (100, y) not in E: key[y, 100] = 'in'
        if (101, y) not in E: key[y, 101] = 'wood_hi'
    for x in (100, 101): key[99, x] = 'in'


BANNERS = (('paint_r', 103), ('paint_y', 110), ('paint_b', 117))     # six columns each, ink at 102, 109, 116, 123
B_TOP, B_TAIL = 83, 115                                               # first V row of the swallowtail


def atelier_banners(key):
    """three banners of one width with one swallowtail; what the shorter tails uncover is the wall corner again"""
    E = R.LAST_E
    H, W = key.shape
    # the wall below: the quoin pattern repeats every 14 rows, so rows 111..120 copy rows 125..134
    # the fold shading of the old banners, read from their right edge so it lands on the same folds
    OLD = {'paint_r': (109, 108), 'paint_y': (116, 115), 'paint_b': (122, 121)}
    old = key.copy()
    for p, x0 in BANNERS:
        right, lo_col = OLD[p]
        shade = lambda x, y: (right - (x0 + 5 - x)) != lo_col and old[y, right - (x0 + 5 - x)] == p + '_lo'
        for y in range(B_TOP, B_TAIL + 3):
            for i in range(6):
                x = x0 + i
                if y >= B_TAIL:                                       # V: 1:1 cut, a point at each side
                    d = y - B_TAIL
                    if i in (2 - d, 3 + d): key[y, x] = 'in'; continue
                    if 2 - d < i < 3 + d: key[y, x] = 'cut'; continue
                tone = p + ('_lo' if y == B_TOP or shade(x, y) else '_hi' if i == 0 else '_lo' if i == 4 else '')
                key[y, x] = tone
        for xs in (x0 - 1, x0 + 6):
            for y in range(B_TOP, B_TAIL + 3): key[y, xs] = 'in'
    for y in range(B_TAIL - 4, 121):                                  # the wall below the tails: the quoins repeat
        for x in range(102, 124):                                     # every 14 rows, so these copy rows +14
            if key[y, x] == 'cut' or y > B_TAIL + 2:
                key[y, x] = old[y + 14, x] if x <= 112 else ''
    key[79, 112] = 'in'                                               # the rod's top line ran through a gap here
    for y in range(B_TOP - 1, B_TAIL + 4):                            # ink touching the background is outer outline
        for x in range(100, W):
            if key[y, x] == 'in' and any(0 <= x + dx < W and key[y + dy, x + dx] == '' for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                key[y, x] = 'out'


def atelier_pot(key):
    """the pot's right side had no outline over its widest five rows"""
    for y in range(140, 162):
        xs = [x for x in range(10, 40) if key[y, x].startswith('clay')]
        if not xs: continue
        r = max(xs) + 1
        if key[y, r] in WALL: key[y, r] = 'in'


def atelier_outline(key):
    """outline gaps on the silhouette: the chimney cap's left side, the right eave's end"""
    for y in (1, 2, 3): key[y, 39] = 'out'                             # mirrors the cap's right side (x 59)
    for y in (39, 40, 41): key[y, 119] = 'out'                         # mirrors the left eave's end (x 0)
    key[42, 119] = ''


def atelier_mullion(key):
    """the window's upright bar sat one dot left of the glass centre (panes 6 | 8); it moves under the keystone"""
    for y in range(114, 138):
        if key[y, 83] == 'wood_lo' and key[y, 84] == 'wood':
            key[y, 85] = 'wood'; key[y, 84] = 'wood_lo'; key[y, 83] = key[y, 82]


def atelier(key):
    atelier_outline(key)
    atelier_mullion(key)
    atelier_blue_leftover(key)
    atelier_white_well(key)
    atelier_palette_rim(key)
    atelier_bracket(key)
    atelier_banners(key)
    atelier_pot_redraw(key)
    atelier_palette_volume(key)
    # atelier_smears(key)  -- the user: take the orange and purple smears off
    atelier_drips(key)
    atelier_bricks(key)
    drop_small_leaves(key, 11)


# ====================================================================== house
CORBEL = ["#Wwwww#", "#Wwwww#", "#Wwww#", "#Www#", "#Ww#", "#Ww#", "#Ww#", "#Ww#", "#Wv#", "####"]   # wedge: 1:1 brace, 4-dot stem
CORBEL_X = (13, 51, 89)                 # the right one stays at the corner, clear of the small window's arch
HPOST = ["#Wwv#"] * 10 + ["#####"]      # the same post as the atelier canopy's, rows 88..98
HPOST_C = (17, 55)                      # centred on the door (jambs 22..25 and 47..50, centre 36)


def house_roof(key):
    """outline gaps at the chimney foot, the ridge's right end and the left eave; ivy tucked behind the eave"""
    key[4, 16] = key[4, 30] = 'out'; key[4, 17] = 'in'; key[4, 31] = ''   # chimney cap's lower corners
    key[10, 16] = 'out'; key[10, 17] = 'out'; key[10, 18] = 'in'           # the ridge's left end at the chimney
    key[10, 87] = 'out'; key[10, 88] = 'out'; key[10, 89] = ''             # the ridge's right end
    for y in (31, 32, 33): key[y, 0] = 'out'
    key[34, 1] = 'out'; key[34, 4] = 'roof_lo'
    for x in range(5, 12):
        if key[34, x] in LEAF + WOODS: key[34, x] = 'roof_lo'
    for x in (2, 3, 4): key[35, x] = 'out'


def house_corbels(key):
    wood = {'#': 'in', 'W': 'wood_hi', 'w': 'wood', 'v': 'wood_lo'}
    spans = ((10, 22, 1), (50, 61, 1), (82, 96, -1))
    old = {(x, y) for y in range(36, 46) for a, b, _ in spans for x in range(a, b + 1)
           if key[y, x] in WOODS + LEAF + ('in',) and not (x <= 12 and key[y, x] in LEAF)}
    for x, y in sorted(old):
        step = next(s_ for a, b, s_ in spans if a <= x <= b)
        if y <= 37: key[y, x] = 'plaster_sh'
        else: key[y, x] = wall_near(key, x, y, step, skip=old)
    for x0 in CORBEL_X:
        for j, r in enumerate(CORBEL):
            for i, ch in enumerate(r):
                if ch != ' ': key[36 + j, x0 + i] = wood[ch]
        for j in range(2, 9):                                         # one-dot shadow right of each corbel
            x = x0 + len(CORBEL[j].rstrip())
            if key[36 + j, x] in ('plaster', 'plaster_mid'): key[36 + j, x] = 'plaster_lo'


def house_porch(key):
    """porch posts of one shape, mirrored about the door; stray ink in the porch roof; the balcony beam's hole"""
    wood = {'#': 'in', 'W': 'wood_hi', 'w': 'wood', 'v': 'wood_lo'}
    old = {(x, y) for y in range(88, 100) for x in list(range(13, 21)) + list(range(51, 59)) if key[y, x] in WOODS + ('in',)}
    for x, y in sorted(old): key[y, x] = wall_near(key, x, y, 1, skip=old)
    for c in HPOST_C:
        for j, r in enumerate(HPOST):
            for i, ch in enumerate(r): key[88 + j, c - 2 + i] = wood[ch]
    for y in range(74, 80):                                           # a lone ink dot inside the roof's right slope
        for x in range(60, 75):
            if key[y, x] == 'in' and key[y, x - 1].startswith('roof') and key[y, x + 1].startswith('roof') and key[y - 1, x] in ROOFK + ('in',) and key[y + 1, x] in ROOFK + ('in',):
                if key[y, x + 2] == 'in': key[y, x] = 'roof'
    for x in range(12, 17):                                           # the beam's bottom row was ivy and wall
        if key[74, x] in LEAF + WALL: key[74, x] = 'wood_lo'


BAL_POST = ["######"] + ["#Wwwv#"] * 13          # rows 58..71, on the beam (rows 72..75, x 12..63)
BAL_DX = -1                                        # the user: the railing one dot to the left (beam 11..62)
BAL_POST_X = (13 + BAL_DX, 57 + BAL_DX)            # one dot in from each end of the beam
BAL_RAIL = (19 + BAL_DX, 56 + BAL_DX)              # between the posts
BAL_BALUSTERS = tuple(22 + BAL_DX + 5 * k for k in range(7))  # seven 'wv' balusters, gaps of 3, one pair on the centre 37.5


def house_balcony(key):
    """the balcony railing: two posts of one shape mirrored on the beam, even balusters, clean beam end"""
    wood = {'#': 'in', 'W': 'wood_hi', 'w': 'wood', 'v': 'wood_lo'}
    for y in (73, 74): key[y, 63] = 'in'                               # the beam's right end, then the move
    key[73, 64] = key[73, 65] = 'plaster'
    old = key.copy()
    for y in range(72, 76):
        for x in range(12 + BAL_DX, 64 + BAL_DX): key[y, x] = old[y, x - BAL_DX]
        for x in range(64 + BAL_DX, 66): key[y, x] = old[y, x - BAL_DX] if old[y, x - BAL_DX] in WALL else 'plaster'
    for y in range(58, 72):
        for x in range(57 + BAL_DX, 66): key[y, x] = 'plaster'         # the old right post and its shadow
    for x0 in BAL_POST_X:
        for j, r in enumerate(BAL_POST):
            for i, ch in enumerate(r): key[58 + j, x0 + i] = wood[ch]
    a, b = BAL_RAIL
    for x in range(a, b + 1):
        key[61, x] = key[63, x] = 'in'; key[62, x] = 'wood'
        for y in range(64, 72):
            k = min((x - bx) for bx in BAL_BALUSTERS + (a - 2,) if x - bx >= 0)   # dots right of the last baluster/post
            if k in (0, 1) and x >= BAL_BALUSTERS[0] and x - k in BAL_BALUSTERS: key[y, x] = 'wood' if k == 0 else 'wood_lo'; continue
            g = k - 2                                                       # place in the gap: 0, 1, 2
            if y <= 65: key[y, x] = 'plaster_lo'                            # under the rail
            elif y == 66: key[y, x] = 'plaster_lo' if g == 0 else 'plaster'
            else: key[y, x] = 'plaster' if g >= 2 else 'plaster_lo'
    for y in range(59, 72): key[y, 63 + BAL_DX] = 'plaster_lo'           # right of the right post: its shadow


def house_window_jambs(key):
    """the big upper window's last row above the rail lost its stone jambs (wall on the left, glass on the right)"""
    for x0 in (23, 46):
        for i, v in enumerate(('in', 'stone', 'stone', 'in')): key[60, x0 + i] = v


def house(key):
    house_roof(key)
    house_window_jambs(key)
    house_corbels(key)
    drop_small_leaves(key, 4)
    house_porch(key)
    house_balcony(key)
    for x in range(10, 20):                                           # the porch roof's underside: no ivy, no wall
        if key[86, x - 1] == 'in' and key[86, x] not in INK:
            for xx in range(x, 20):
                if key[86, xx] == 'roof_lo': break
                key[86, xx] = 'roof_lo'
            break


# ====================================================================== atelier: pot and colour (2026-11-12, user:
# 'tidy the lumpy pot; add colour and detail, the atelier feels flat')
import bld_detail4 as _F
_h = lambda s: tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)
NEW_CO = dict(clay_hi=_h('e08a5c'), wood_pale=_h('eac088'),
              paint_o=_h('e8803a'), paint_o_hi=_h('f6aa60'), paint_o_lo=_h('b4581e'),
              paint_v=_h('8256a0'), paint_v_hi=_h('aa80c4'), paint_v_lo=_h('5c3876'))
NEW_SK = dict(clay_hi=_F.SK4['wood_hi'], wood_pale=_F.SK4['wood_hi'],
              paint_o=_F.SK4['paint_r'], paint_o_hi=_F.SK4['paint_r_hi'], paint_o_lo=_F.SK4['paint_r_lo'],
              paint_v=_F.SK4['paint_r'], paint_v_hi=_F.SK4['paint_r_hi'], paint_v_lo=_F.SK4['paint_r_lo'])
_F.CO4.update(NEW_CO); _F.SK4.update(NEW_SK)

POT_BODY = (12, 145, 30, 161)        # one clean ellipse under the rim (rim rows 141..144, x 15..27, centre 21)


def atelier_pot_redraw(key):
    POTK = ('clay', 'clay_lo', 'clay_hi', 'roof_hi')
    x0, y0, x1, y1 = POT_BODY
    old = {(x, y) for y in range(y0, 162) for x in range(8, 34) if key[y, x] in POTK or
           (key[y, x] == 'in' and 11 <= x <= 31)}
    ring, inside = PC.ellipse_ring(x0, y0, x1, y1)
    body = ring | inside
    cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
    for x, y in inside:
        u, v = (x - cx) / rx, (y - cy) / ry
        if ((u + .5) / .08) ** 2 + ((v + .22) / .22) ** 2 < 1: t = 'roof_hi'           # the light, upper left
        elif ((u + .45) / .2) ** 2 + ((v + .15) / .45) ** 2 < 1: t = 'clay_hi'
        elif .72 * u + .62 * v > .5 and u * u + v * v > .45: t = 'clay_lo'          # turning away, lower right
        else: t = 'clay'
        key[y, x] = t
    for x in range(x0, x1 + 1):                                                     # a groove under the shoulder
        if (x, y0 + 4) in inside and key[y0 + 4, x] in ('clay', 'clay_hi'): key[y0 + 4, x] = 'clay_lo'
    for x, y in ring: key[y, x] = 'in'
    rim = {(x, y) for y in range(141, 145) for x in range(15, 28)}
    for x, y in old - body - rim:                                                   # what the old lumps covered
        nb = [key[y, x + d] for d in (-1, -2, 1, 2) if (x + d, y) not in old and 0 <= x + d < key.shape[1]]
        key[y, x] = next((v for v in nb if v not in ('', 'in', 'out') and not v.startswith('leaf')), 'plaster')


def atelier_palette_volume(key):
    """the board had one flat tone: a pale sheen inside the rim on the lit upper left, a deeper edge lower right"""
    E = R.LAST_E
    cx, cy, ex, ey = 73.3, 65.1, 37.3, 32.3
    for x, y in E:
        if key[y, x] not in ('wood_hi', 'wood_mid'): continue
        u, v = (x + .5 - cx) / ex, (y + .5 - cy) / ey
        r = (u * u + v * v) ** .5
        if key[y, x] == 'wood_hi' and .6 < r < .79 and u + v < -.55: key[y, x] = 'wood_pale'
        elif key[y, x] == 'wood_mid' and r > .93 and u + v > .75: key[y, x] = 'wood'


SMEAR = [".bbbbbb.", "bhhbbbbb", ".bbbbbbd", "..dddd.."]
SMEARS = (('paint_o', 64, 58), ('paint_v', 70, 65))      # mixed paint on the board between the wells


def atelier_smears(key):
    for p, x0, y0 in SMEARS:
        for j, r in enumerate(SMEAR):
            for i, ch in enumerate(r):
                if ch == '.': continue
                if key[y0 + j, x0 + i] not in ('wood_hi', 'wood_pale', 'wood_mid'): continue
                key[y0 + j, x0 + i] = p + {'b': '', 'h': '_hi', 'd': '_lo'}[ch]


DRIPS = (('paint_b', 86, 6), ('paint_b', 89, 3))   # x, length below the board's edge


def atelier_drips(key):
    """paint run off the board's lower right edge down the wall: one-dot lines ending in a drop"""
    E = R.LAST_E
    for p, x, n in DRIPS:
        y = max(yy for (xx, yy) in E if xx == x) + 1                    # first wall dot under the rim
        for k in range(n):
            if key[y + k, x] in WALL: key[y + k, x] = p
        if key[y + n, x] in WALL: key[y + n, x] = p + '_lo'               # the drop at the end, darker


BRICKS = ["#########..",
          "#SSS#SSSS#.",
          "#sss#ssss#,",
          "###########",
          ".#SSSS#SSS#",
          ".#ssss#sss#",
          ".##########"]
BRICK_AT = ((19, 121), (88, 102))      # bare plaster: left of the door, under the palette's right side


def atelier_bricks(key):
    """two patches of bare brick where the plaster fell off, like the house's"""
    tone = {'#': 'in', 'S': 'stone_hi', 's': 'stone', ',': 'plaster_lo'}
    for x0, y0 in BRICK_AT:
        for j, r in enumerate(BRICKS):
            for i, ch in enumerate(r):
                if ch == '.' or key[y0 + j, x0 + i] not in WALL: continue
                key[y0 + j, x0 + i] = tone[ch]
        for j in range(1, len(BRICKS)):                              # a one-dot shadow down the right side
            x = x0 + len(BRICKS[j].rstrip('.'))
            if key[y0 + j, x] in ('plaster', 'plaster_mid'): key[y0 + j, x] = 'plaster_lo'


# ====================================================================== detail pass (user: 'more detail, all over')
def door_recess(key, seed):
    """the door sits back in its arch: a shaded top row under the arch and a shaded left column (light upper left)"""
    x0, y0 = seed
    xl = x0
    while key[y0, xl - 1] != 'in': xl -= 1
    xr = x0
    while key[y0, xr + 1] != 'in': xr += 1
    for x in range(xl, xr + 1):
        y = y0
        while y > 0 and key[y - 1, x] != 'in': y -= 1
        if key[y, x] in ('wood', 'wood_hi', 'wood_mid'): key[y, x] = 'wood_lo'
    for y in range(y0 - 40, y0 + 40):
        if 0 <= y < key.shape[0] and key[y, xl] in ('wood', 'wood_hi', 'wood_mid'):
            if key[y, xl - 1] == 'in': key[y, xl] = 'wood_lo'


def wall_stain(key, cx, cy, rx, ry):
    """a soft damp patch on the plaster: one shade darker, an even oval"""
    ring, inside = PC.ellipse_ring(cx - rx, cy - ry, cx + rx, cy + ry)
    for x, y in ring | inside:
        if key[y, x] == 'plaster': key[y, x] = 'plaster_mid'


def house_details(key):
    house_details2(key)
    door_recess(key, (37, 112))
    wall_stain(key, 71, 71, 4, 2)
    wall_stain(key, 60, 101, 3, 2)


def atelier_details(key):
    atelier_details2(key)
    door_recess(key, (49, 140))
    wall_stain(key, 20, 88, 4, 2)
    for x, y in [(x, y) for y in range(134, 142) for x in range(80, 90) if key[y, x] == 'flower']:
        key[y, x] = 'paint_r_hi'                                   # one of the window box's flowers turns pink-red


# ---------------------------------------------------------------------- detail pass 2: walls, pot, doors, rails
def door_more(key, straps, bottom_row, knots):
    """rivets along the iron straps, a dark threshold row, two knots in the planks"""
    for y in straps:
        xs = [x for x in range(key.shape[1]) if key[y, x] == 'metal']
        if not xs: continue
        for x in range(min(xs) + 2, max(xs) + 1, 4):
            if key[y, x] == 'metal': key[y, x] = 'metal_hi'
    for x in range(key.shape[1]):
        if key[bottom_row, x] in ('wood', 'wood_hi', 'wood_mid') and key[bottom_row + 1, x] == 'out': key[bottom_row, x] = 'wood_lo'
    for x, y in knots:                                         # a knot: two dark dots stacked in a plain plank
        for yy in (y, y + 1):
            if key[yy, x] == 'wood': key[yy, x] = 'wood_lo'


def rain_streaks(key, xs, y0, n):
    """rain run off a sill: one-dot streaks, one shade darker than the wall they run over"""
    darker = {'plaster': 'plaster_mid', 'plaster_mid': 'plaster_lo', 'plaster_lo': 'plaster_sh'}
    for x in xs:
        for k in range(n):
            v = key[y0 + k, x]
            if v in darker and not (k == n - 1 and x % 2): key[y0 + k, x] = darker[v]


def pot_more(key):
    """the neck under the rim in shadow, the rim's right end turning away"""
    for x in range(14, 30):                                    # the rim's bottom line over the body's top line made a
        if key[144, x] == 'in' and key[145, x] == 'in' and key[143, x] != 'in':   # double line: that is the neck
            key[144, x] = 'clay_lo'
        if key[143, x] == 'clay' and x >= 23: key[143, x] = 'clay_lo'


def house_details2(key):
    door_more(key, straps=(102, 121), bottom_row=128, knots=((31, 108), (43, 125)))
    rain_streaks(key, (70, 83), 70, 6)                         # under the upper small window's sill
    rain_streaks(key, (71, 82), 116, 5)                        # under the lower one
    for x in range(19, 57):                                    # the balcony: a lit rail top and beam top
        if key[62, x] == 'wood': key[62, x] = 'wood_hi'
    for x in range(13, 63):
        if key[73, x] == 'wood': key[73, x] = 'wood_hi'


def atelier_details2(key):
    door_more(key, straps=(124, 153), bottom_row=161, knots=((44, 131), (57, 146)))
    pot_more(key)
