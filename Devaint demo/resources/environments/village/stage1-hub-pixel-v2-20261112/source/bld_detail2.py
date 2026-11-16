"""Detail pass 2 on the buildings (after bld_detail): wall shading, fuller ivy, chimney bricks and its shadow on the
roof, the atelier's brush ends / pot / banner. Same rules: palette materials only, one-dot ink, no scattered dots."""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage
from px import *
from hub_bld import SK, CO
import bld_detail as B

OUT = Path(__file__).parent.parent / "bld2"
PLASTER = ('plaster',)
LEAF = ('leaf', 'leaf_hi', 'leaf_lo')


def drop_shadow(key, min_size=15):
    """everything drawn on the wall casts a one-dot shadow down and to the right (light from the upper left)"""
    H, W = key.shape
    ink = key == 'in'
    lab, n = ndimage.label(ink, structure=np.ones((3, 3)))
    sizes = ndimage.sum(ink, lab, range(1, n + 1))
    big = np.isin(lab, np.nonzero(sizes >= min_size)[0] + 1)
    solid_obj = np.isin(key, ('in', 'wood', 'wood_hi', 'wood_lo', 'stone', 'stone_hi', 'stone_lo', 'metal', 'metal_hi', 'glass'))
    out = key.copy()
    for y in range(H - 1):
        for x in range(W - 1):
            if big[y, x] and key[y + 1, x + 1] == 'plaster' and not solid_obj[y + 1, x + 1]:
                out[y + 1, x + 1] = 'plaster_lo'
    return out


def base_band(key, rows=2):
    """the foot of the wall, just above the plinth stones, sits in a soft shadow band"""
    H, W = key.shape
    plinth_top = None
    for y in range(H - 1, H // 2, -1):
        if (key[y] == 'in').sum() > W * .45 and (np.isin(key[y + 1:y + 3], ('stone', 'stone_hi')).sum() > W * .3 if y + 2 < H else False):
            plinth_top = y; break
    if plinth_top is None: return
    for y in range(plinth_top - rows, plinth_top):
        for x in range(W):
            if key[y, x] == 'plaster': key[y, x] = 'plaster_lo'


LEAF_STAMP = ["..HL..LL..",
              ".HHLLLLLD.",
              "LHLLLLLLLD",
              "LLLLLLLLDD",
              ".LLLLLLDD.",
              "..LLLLDD..",
              "...LDD....",
              "....D....."]


def fuller_ivy(key, x0, x1, y0, y1, step=6):
    """fill the gaps along the ivy with more leaves behind the drawn ones (only over bare wall)"""
    H, W = key.shape
    m = {'H': 'leaf_hi', 'L': 'leaf', 'D': 'leaf_lo'}
    k = 0
    for y in range(y0, y1 - 6, step):
        win = key[y:y + 7, x0:x1]
        if np.isin(win, LEAF).mean() > .55: continue
        for x in (x0 + (k % 2) * 4, x0 + 6 + (k % 2) * 3):
            rows = LEAF_STAMP if (k + x) % 2 else [r[::-1] for r in LEAF_STAMP]
            for j, r in enumerate(rows):
                for i, ch in enumerate(r):
                    yy, xx = y + j, x + i
                    if ch != '.' and 0 <= yy < H and 0 <= xx < W and key[yy, xx] in ('plaster', 'plaster_lo'):
                        key[yy, xx] = m[ch]
        k += 1


def chimney(key, x0, x1, y0, y1, course=4):
    """brick courses on the chimney body (stone_lo joints, staggered), and its shadow on the roof to its right"""
    for i, y in enumerate(range(y0 + course - 1, y1, course)):
        for x in range(x0, x1 + 1):
            if key[y, x] in ('stone', 'stone_hi'): key[y, x] = 'stone_lo'
        for xs in range(x0 + (3 if i % 2 else 7), x1 - 2, 8):
            for yy in range(y + 1, min(y + course, y1)):
                if key[yy, xs] in ('stone', 'stone_hi'): key[yy, xs] = 'stone_lo'


def chimney_shadow(key, x_right, y0, y1, width=3):
    for y in range(y0, y1 + 1):
        for x in range(x_right + 1, x_right + 1 + width):
            if key[y, x] in ('roof', 'roof_hi'): key[y, x] = 'roof_lo'


def house():
    key = B.house()
    key = drop_shadow(key)
    base_band(key)
    fuller_ivy(key, 6, 20, 46, 150)
    chimney(key, 22, 39, 9, 13)
    return key


def brush_ends(key, centres, cut_y, board=(40, 56, 109)):
    """the giant brush handles end in a flat, rounded butt a little below their holder instead of a stake point"""
    H, W = key.shape
    WOOD = ('wood', 'wood_hi', 'wood_lo')
    for cx in centres:
        a = cx
        while key[cut_y, a - 1] in WOOD: a -= 1
        b = cx
        while key[cut_y, b + 1] in WOOD: b += 1
        # everything of the handle below the butt goes (only what hangs straight below it; palette and board stay)
        width = b - a
        cleared = set()
        for y in range(cut_y + 1, cut_y + 14):
            shift = (y - cut_y) // 4 if cx < 30 else -((y - cut_y) // 4) if cx > 45 else 0
            for x in range(a - 1 + min(0, shift), b + 2 + max(0, shift)):
                if board[0] <= x <= board[1] and y >= board[2]: continue
                if key[y, x] == 'wood_hi' and cx > 45 and x > b: continue
                if key[y, x] in ('in',) + WOOD:
                    left = [key[y, i] for i in range(x - 1, a - 6, -1) if key[y, i] not in ('in',) + WOOD]
                    key[y, x] = left[0] if left and left[0] in ('leaf', 'leaf_hi', 'leaf_lo') else 'plaster'
                    cleared.add((x, y))
        # whatever the handle used to hide (the palette's edge) gets its ink edge back
        for x, y in cleared:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if q not in cleared and key[q[1], q[0]] == 'wood_hi': key[q[1], q[0]] = 'in'
        for x in range(a, b + 1): key[cut_y + 1, x] = 'in'
        for x in range(a, b + 1):
            if key[cut_y, x] in ('wood', 'wood_hi'): key[cut_y, x] = 'wood_lo'
        key[cut_y, a - 1] = 'in'; key[cut_y, b + 1] = 'in'
        key[cut_y + 1, a - 1] = key[cut_y + 1, a - 2] if key[cut_y + 1, a - 2] != 'in' else 'plaster'
        key[cut_y + 1, b + 1] = key[cut_y + 1, b + 2] if key[cut_y + 1, b + 2] != 'in' else 'plaster'


def pot(key, x0, x1, y0, y1):
    """a band round the pot's shoulder and a soil line under the rim"""
    for x in range(x0, x1 + 1):
        if key[y0, x] == 'clay': key[y0, x] = 'clay_lo'
    for x in range(x0 + 1, x1):
        if key[y0 + 1, x] == 'clay': key[y0 + 1, x] = 'roof_hi'


def banner(key, stripes, y_top, rows=2):
    shade = {'paint_r': 'roof_lo', 'paint_y': 'wood_hi', 'paint_b': 'glass'}
    for xa, xb in stripes:
        for y in range(y_top, y_top + rows):
            for x in range(xa, xb + 1):
                v = key[y, x]
                if v in shade: key[y, x] = shade[v]


def declump_key(key):
    H, W = key.shape
    for _ in range(4):
        hit = False
        for y in range(H - 1):
            for x in range(W - 1):
                blk = [(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)]
                if not all(key[q[1], q[0]] == 'in' for q in blk): continue
                def nb(p):
                    return [key[p[1] + dy, p[0] + dx] for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                            if (dx or dy) and 0 <= p[0] + dx < W and 0 <= p[1] + dy < H and key[p[1] + dy, p[0] + dx] not in ('in', 'out', '')]
                p = max(blk, key=lambda q: len(nb(q)))
                v = nb(p)
                if v: key[p[1], p[0]] = max(set(v), key=v.count); hit = True
        if not hit: break


def atelier():
    key = B.atelier()
    brush_ends(key, (23, 36, 49), 98)
    key[102, 58] = 'in'; key[109, 52] = 'in'                # the palette edge and board top the third handle hid
    declump_key(key)
    key = drop_shadow(key)
    base_band(key)
    fuller_ivy(key, 6, 20, 92, 196)
    chimney(key, 52, 72, 9, 21)
    pot(key, 20, 33, 186, 200)
    banner(key, [(129, 136), (138, 145), (147, 153)], 103)
    return key


if __name__ == '__main__':
    for name, fn in (('house', house), ('atelier', atelier)):
        key = fn()
        for v, P in (('color', CO), ('sketch', SK)):
            c = B.paint_keys(key, P); lint(c, colors=[K, P['in']], name=f'{name} {v}')
            c.im.save(OUT / f'e-{name}-{v}.png')
        o = Image.open(OUT / f'd-{name}-color.png'); n = Image.open(OUT / f'e-{name}-color.png')
        S = 3; s = Image.new('RGBA', (o.width * S * 2 + 30, o.height * S), (244, 241, 233, 255))
        s.alpha_composite(o.resize((o.width * S, o.height * S), 0), (0, 0)); s.alpha_composite(n.resize((n.width * S, n.height * S), 0), (o.width * S + 30, 0))
        s.save(OUT / f'e-{name}-cmp.png')


# ---------------------------------------------------------------- pass 3 (2026-11-12): extra shades, clean handles
from px import h as _h
CO3 = dict(CO, wood_mid=_h('c58c52'),
           paint_r_hi=_h('e2625a'), paint_r_lo=_h('9a2a22'), paint_y_hi=_h('f4c95c'), paint_y_lo=_h('b9821f'),
           paint_g_hi=_h('62a86c'), paint_g_lo=_h('2c6638'), paint_b_hi=_h('6584c8'), paint_b_lo=_h('2c4682'))
SK3 = dict(SK, wood_mid=SK['wood_hi'],
           paint_r_hi=L, paint_r_lo=M, paint_y_hi=C, paint_y_lo=M, paint_g_hi=C, paint_g_lo=M, paint_b_hi=L, paint_b_lo=M)
WOODS = ('in', 'wood', 'wood_hi', 'wood_lo', 'wood_mid')


def clean_handle(key, top, bottom, left_top, left_bottom, width=9, keep=None, old=None):
    """redraw one brush handle as a straight band of even steps from (top, left_top) to (bottom, left_bottom):
    ink | hi | mid | wood x3 | lo | ink. The old handle (ink to ink, `width` wide, found row by row near `old`
    = (old_left_top, old_left_bottom)) is cleared to wall first; pixels in `keep` stay in front."""
    H, W = key.shape
    keep = keep or (lambda x, y: False)
    olt, olb = old or (left_top, left_bottom)
    for y in range(top, bottom + 1):
        t = (y - top) / max(1, bottom - top)
        guess = int(round(olt + (olb - olt) * t))
        cands = [x for x in range(guess - 3, guess + 4) if key[y, x] == 'in' and key[y, x + 1] in ('wood', 'wood_hi', 'wood_lo')]
        oldL = min(cands, key=lambda x: abs(x - guess)) if cands else guess
        bg = 'plaster_lo' if y <= 56 else 'plaster'
        for x in range(oldL, oldL + width):
            if not keep(x, y) and key[y, x] in WOODS: key[y, x] = bg
    for y in range(top, bottom + 1):
        t = (y - top) / max(1, bottom - top)
        L = int(round(left_top + (left_bottom - left_top) * t))
        cols = ['in', 'wood_hi', 'wood_mid'] + ['wood'] * (width - 5) + ['wood_lo', 'in']
        for i, v in enumerate(cols):
            if not keep(L + i, y): key[y, L + i] = v


def paint_wells(key, region):
    """each paint well on the palette: a lit crescent upper left, a shaded crescent lower right"""
    x0, y0, x1, y1 = region
    for name in ('paint_r', 'paint_y', 'paint_g', 'paint_b'):
        m = np.zeros(key.shape, bool); m[y0:y1, x0:x1] = key[y0:y1, x0:x1] == name
        lab, n = ndimage.label(m)
        for i in range(1, n + 1):
            blob = lab == i
            if blob.sum() < 20: continue
            ys, xs = np.nonzero(blob); cx, cy = xs.mean(), ys.mean(); r = (xs.max() - xs.min() + 1) / 2
            for y, x in zip(ys, xs):
                d = np.hypot(x - cx, y - cy)
                if d > r - 2.2 and (x - cx) + (y - cy) > r * .55: key[y, x] = name + '_lo'
                elif r * .35 < d < r * .75 and (x - cx) < -r * .15 and (y - cy) < -r * .15: key[y, x] = name + '_hi'


def palette_board(key, region):
    """the palette board: its lower right rim in a mid tone"""
    x0, y0, x1, y1 = region
    m = np.zeros(key.shape, bool); m[y0:y1, x0:x1] = key[y0:y1, x0:x1] == 'wood_hi'
    lab, n = ndimage.label(m); sizes = ndimage.sum(m, lab, range(1, n + 1))
    board = lab == (np.argmax(sizes) + 1)
    ys, xs = np.nonzero(board); cx, cy = xs.mean(), ys.mean()
    rx, ry = (xs.max() - xs.min()) / 2, (ys.max() - ys.min()) / 2
    for y, x in zip(ys, xs):
        e = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
        if e > .72 and (x - cx) / rx + (y - cy) / ry > .5: key[y, x] = 'wood_mid'


def banner3(key, stripes, y_top, rows=2):
    for xa, xb, name in stripes:
        for y in range(y_top, y_top + rows):
            for x in range(xa, xb + 1):
                if key[y, x] in (name, 'roof_lo', 'wood_hi', 'glass'): key[y, x] = name + '_lo'
        # a fold down each stripe: a shade column two dots in from its right edge
        for y in range(y_top + rows, y_top + 36):
            if key[y, xb - 1] == name: key[y, xb - 1] = name + '_lo'
        for y in range(y_top + rows, y_top + 36):
            if key[y, xa + 1] == name: key[y, xa + 1] = name + '_hi'


def atelier3():
    key = B.atelier()
    plank = key[84:93, 18:61].copy()
    leaf = lambda x, y: key[y, x] in ('leaf', 'leaf_hi', 'leaf_lo') or (84 <= y <= 92 and 18 <= x <= 60)
    # straight, evenly stepped handles down to where the drawn stake points begin (row 104); the points stay as drawn
    clean_handle(key, 57, 98, 16, 20, keep=leaf, old=(16, 20)); clean_handle(key, 99, 103, 20, 20, keep=leaf, old=(20, 20))
    clean_handle(key, 54, 103, 34, 34, keep=leaf, old=(34, 34))
    clean_handle(key, 62, 98, 53, 49, keep=leaf, old=(53, 49)); clean_handle(key, 99, 103, 49, 49, keep=leaf, old=(49, 49))
    key[84:93, 18:61] = plank
    declump_key(key)
    key = drop_shadow(key)
    base_band(key)
    fuller_ivy(key, 6, 20, 92, 196)
    chimney(key, 52, 72, 9, 21)
    pot(key, 20, 33, 186, 200)
    palette_board(key, (60, 20, 145, 125))
    paint_wells(key, (60, 20, 145, 125))
    banner3(key, [(129, 136, 'paint_r'), (138, 145, 'paint_y'), (147, 153, 'paint_b')], 103)
    return key


if __name__ == '__main__':
    key = atelier3()
    for v, P in (('color', CO3), ('sketch', SK3)):
        c = B.paint_keys(key, P); lint(c, colors=[K, P['in']], name=f'atelier3 {v}')
        c.im.save(OUT / f'f-atelier-{v}.png')
