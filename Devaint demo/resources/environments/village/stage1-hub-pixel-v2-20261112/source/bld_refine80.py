"""Refinement of the 80 % buildings: the ivy is regrown with clean leaves (shrinking blunted them), stray roof dots
outside the eaves' ink edge go, then the usual checks."""
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from px import *
import bld_detail as B
import bld_detail2 as E
import bld_detail4 as F
import bld_shrink as S

OUT = Path(__file__).parent.parent / "bld2"
LEAFK = ('leaf', 'leaf_hi', 'leaf_lo')
ROOF = ('roof', 'roof_hi', 'roof_lo')

LEAF = [".LL.LL.",
        "LHHLLLD",
        "LHLLLLD",
        ".LLLLD.",
        "..LLD..",
        "...D..."]


def regrow_ivy(key, wall_x, x1, y0, y1, vine_x, seed=0, step=3, spread=(-6, 1, -3)):
    """clear the shrunk ivy (left of x1, rows y0..y1) and grow it again: a wavy one-dot vine with three-lobed leaves
    on alternate sides; leaves past the wall edge get the outer outline"""
    H, W = key.shape
    for y in range(y0, y1 + 1):
        for x in range(0, x1):
            v = key[y, x]
            if x < wall_x:
                if v in LEAFK + ('out', 'wood_lo', 'in'): key[y, x] = ''
            elif x == wall_x:
                key[y, x] = 'out'
            elif v in LEAFK or (v == 'wood_lo' and x < x1):
                nb = [key[y, i] for i in range(x + 1, min(W, x + 8)) if key[y, i] in ('plaster', 'plaster_lo', 'plaster_mid', 'plaster_sh')]
                key[y, x] = nb[0] if nb else 'plaster'
    vine = {}
    for y in range(y0, y1 + 1):
        x = vine_x + int(round(1.3 * math.sin((y + seed) / 6.0)))
        vine[y] = x
        if key[y, x] not in ('in',): key[y, x] = 'wood_lo'
    k = 0
    for y in range(y0 + 1, y1 - 4, step):
        side = k % 3
        vx = vine[min(y + 2, y1)]
        x = vx + spread[side]
        rows = LEAF if side == 1 or (side == 2 and k % 2) else [r[::-1] for r in LEAF]
        for j, r in enumerate(rows):
            for i, ch in enumerate(r):
                yy, xx = y + j, x + i
                if ch == '.' or not (0 <= xx < W and 0 <= yy < H): continue
                if key[yy, xx] in ('in',) and xx > wall_x: continue
                key[yy, xx] = {'L': 'leaf', 'H': 'leaf_hi', 'D': 'leaf_lo'}[ch]
        k += 1
    # leaves outside the wall: their edge against the empty background is the outer outline
    for y in range(y0, y1 + 1):
        for x in range(0, wall_x):
            if key[y, x] in LEAFK and any((not (0 <= x + dx < W and 0 <= y + dy < H)) or key[y + dy, x + dx] == ''
                                          for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                key[y, x] = 'leaf_lo'


def stray_roof(key):
    """a roof dot right of the eaves' ink edge (left neighbour ink, right neighbour not roof) belongs to what is beyond"""
    H, W = key.shape
    for y in range(H):
        for x in range(1, W):
            if key[y, x] in ROOF and key[y, x - 1] == 'in' and (x + 1 >= W or key[y, x + 1] not in ROOF + ('in',)):
                key[y, x] = key[y, x + 1] if x + 1 < W else ''


def finish(key, name):
    E.declump_key(key)
    for v, P in (('color', F.CO4), ('sketch', F.SK4)):
        c = B.paint_keys(key, P); lint(c, colors=[K, P['in'], P['out']], name=f'{name} {v}')
        c.im.save(OUT / f'r80-{name}-{v}.png')


def house():
    key = S.house80()
    stray_roof(key)
    regrow_ivy(key, 4, 16, 34, 122, 8)
    return key


def atelier():
    key = S.atelier80()
    stray_roof(key)
    regrow_ivy(key, 8, 19, 70, 140, 10, seed=3, spread=(-7, 1, -4))
    return key


# ---------------------------------------------------------------- even edges (2026-11-12): slanted roof ends
INSIDE = ROOF + ('in',)


def line_knots(knots):
    """[(y, x), ...] -> {y: x} along straight segments, rounded so each segment steps evenly"""
    out = {}
    for (ya, xa), (yb, xb) in zip(knots, knots[1:]):
        for y in range(ya, yb + 1):
            out[y] = int(round(xa + (xb - xa) * (y - ya) / max(1, yb - ya)))
    return out


def fix_edge(key, side, knots, ink='in', outside=None, x_window=None):
    """redraw one slanted end of a roof as an even line through `knots`: roof dots past the new edge become what lies
    outside (`outside(y)`; default: the dot just outside the old edge), gaps inside it are filled with the row's roof,
    and the edge gets one continuous dot of `ink` per step"""
    H, W = key.shape
    target = line_knots(knots)
    lo, hi = x_window or (0, W - 1)
    prev = None
    for y in sorted(target):
        nx = target[y]
        xs = [x for x in range(lo, hi + 1) if key[y, x] in INSIDE + ('out',)]
        if not xs: prev = nx; continue
        old = max(xs) if side == 'right' else min(xs)
        step = 1 if side == 'right' else -1
        beyond = outside(y) if outside else key[y, old + step] if 0 <= old + step < W else ''
        inner = next((key[y, x] for x in range(old - step, old - 8 * step, -step) if 0 <= x < W and key[y, x] in ROOF), 'roof')
        a, b = sorted((old, nx))
        for x in range(a, b + 1):
            past = (x > nx) if side == 'right' else (x < nx)
            if past: key[y, x] = beyond
            elif key[y, x] not in ROOF or x == old: key[y, x] = inner
        # the edge: from the previous row's edge (exclusive) to this one, so the line stays connected
        if prev is None or abs(nx - prev) <= 1: span = [nx]
        else: span = list(range(prev + step, nx + step, step))
        for x in span: key[y, x] = ink
        prev = nx


def even_edges_house(key):
    # whole-ratio slopes only (1:1), short vertical runs at the ends
    fix_edge(key, 'left', [(10, 15), (13, 15), (14, 14), (28, 0), (30, 0)], ink='out', outside=lambda y: '')
    fix_edge(key, 'right', [(10, 88), (13, 88), (14, 89), (28, 103), (30, 103)], ink='out', outside=lambda y: '')
    fix_edge(key, 'right', [(74, 66), (83, 75)], x_window=(55, 76))
    fix_edge(key, 'left', [(76, 19), (82, 13), (86, 13)], x_window=(10, 30))


def even_edges_atelier(key):
    fix_edge(key, 'right', [(17, 98), (38, 119)], ink='out', outside=lambda y: '')
    fix_edge(key, 'left', [(30, 7), (37, 0)], ink='out', outside=lambda y: '')
    fix_edge(key, 'left', [(98, 27), (103, 22), (106, 22)], x_window=(15, 40))
    fix_edge(key, 'right', [(98, 76), (103, 81), (106, 81)], x_window=(65, 90))


def house():
    key = S.house80()
    stray_roof(key)
    regrow_ivy(key, 4, 16, 34, 122, 8)
    even_edges_house(key)
    return key


def atelier():
    key = S.atelier80()
    stray_roof(key)
    regrow_ivy(key, 8, 19, 70, 140, 10, seed=3, spread=(-7, 1, -4))
    even_edges_atelier(key)
    clean_palette(key, protect=lambda x, y: x <= 47 or (x >= 103 and y >= 79), hole_box=(43, 59, 56, 72))
    E.declump_key(key)
    return key


# ---------------------------------------------------------------- the palette board: one clean oval
def fit_ellipse(P):
    """direct least-squares ellipse (Fitzgibbon): centre, semi-axes, rotation"""
    x, y = P[:, 0], P[:, 1]
    D1 = np.c_[x * x, x * y, y * y]; D2 = np.c_[x, y, np.ones_like(x)]
    S1, S2, S3 = D1.T @ D1, D1.T @ D2, D2.T @ D2
    T = -np.linalg.solve(S3, S2.T)
    M = S1 + S2 @ T
    M = np.array([M[2] / 2, -M[1], M[0] / 2])
    w, v = np.linalg.eig(M)
    v = np.real(v); cond = 4 * v[0] * v[2] - v[1] ** 2
    a1 = v[:, np.argmax(cond > 0)]
    A, Bc, C, Dd, Ee, F = np.r_[a1, T @ a1]
    den = Bc * Bc - 4 * A * C
    cx = (2 * C * Dd - Bc * Ee) / den; cy = (2 * A * Ee - Bc * Dd) / den
    num = 2 * (A * Ee * Ee + C * Dd * Dd - Bc * Dd * Ee + den * F)
    root = np.sqrt((A - C) ** 2 + Bc * Bc)
    ra = -np.sqrt(num * (A + C + root)) / den; rb = -np.sqrt(num * (A + C - root)) / den
    th = 0.5 * np.arctan2(-Bc, C - A)
    return cx, cy, abs(ra), abs(rb), th



LAST_E = set()


def clean_palette(key, seed=(80, 60), protect=lambda x, y: False, hole_box=None, left=None):
    """refit the palette board to an axis-aligned ellipse through its own top / bottom / right edge and redraw it as one
    even pixel oval: one-dot ink ring, the board inside (rim in the mid tone towards the lower right), what lies behind
    outside; the paint wells, things in front (`protect`) and the thumb hole (redrawn as an even oval) are kept"""
    from scipy import ndimage
    H, W = key.shape
    BOARD = ('wood_hi', 'wood_mid', 'wood')
    m = np.isin(key, BOARD)
    lab, _ = ndimage.label(m)
    board = lab == lab[seed[1], seed[0]]
    ys, xs = np.nonzero(board)
    # edge samples: the outer edge of the whole board (wells and grain filled in), away from the brushes and hole
    solid = ndimage.binary_fill_holes(ndimage.binary_dilation(board, iterations=1))
    edge = solid & ~ndimage.binary_erosion(solid)
    pts = []
    for y, x in zip(*np.nonzero(edge)):
        if x < 52 or protect(x, y) or protect(x + 3, y): continue
        if hole_box and hole_box[0] - 4 <= x <= hole_box[2] + 4 and hole_box[1] - 4 <= y <= hole_box[3] + 4: continue
        pts.append((x + .5, y + .5))
    cx, cy, ra, rb, th = fit_ellipse(np.array(pts))
    ct, st = np.cos(th), np.sin(th)
    def ein(x, y):
        u, v = x + .5 - cx, y + .5 - cy
        p, q = u * ct + v * st, -u * st + v * ct
        return (p / ra) ** 2 + (q / rb) ** 2
    rx, ry = ra, rb
    import pixel_curves as PC
    # drawn axis-aligned with the midpoint ellipse, so every run along the rim grows or shrinks evenly
    ex, ey = (ra if abs(np.cos(th)) > .7 else rb), (rb if abs(np.cos(th)) > .7 else ra)
    if left is not None:                                   # the left extreme where the caller wants it to show
        xr = cx + ex; cx = (left + xr) / 2; ex = (xr - left) / 2
    ring, inside = PC.ellipse_ring(int(round(cx - ex)), int(round(cy - ey)), int(round(cx + ex)) - 1, int(round(cy + ey)) - 1)
    E = ring | inside; cut = set()
    LAST_E.clear(); LAST_E.update(E)
    print('palette ellipse', round(cx, 1), round(cy, 1), round(ex, 1), round(ey, 1))
    def ein(x, y):
        return ((x + .5 - cx) / ex) ** 2 + ((y + .5 - cy) / ey) ** 2
    rx, ry = ex, ey
    wells = np.zeros_like(board)
    for name in ('paint_r', 'paint_y', 'paint_g', 'paint_b'):
        wells |= np.isin(key, (name, name + '_hi', name + '_lo'))
    wl = ndimage.label(key == 'wood_lo')[0]
    for x, y in inside:
        if key[y, x] == 'wood_lo': wells |= wl == wl[y, x]
    wells = ndimage.binary_dilation(wells, structure=np.ones((3, 3)), iterations=1) & (wells | (key == 'in'))   # with their own ink rings
    oldring = ndimage.binary_dilation(board, iterations=1) & ~board
    old = board | oldring
    src = key.copy()
    def behind(x, y):
        dx, dy = x + .5 - cx, y + .5 - cy
        n = max(1e-6, (dx * dx + dy * dy) ** .5)
        for t in range(1, 12):
            qx, qy = int(round(x + dx / n * t)), int(round(y + dy / n * t))
            if not (0 <= qx < W and 0 <= qy < H): return ''
            if not old[qy, qx] and (qx, qy) not in E: return src[qy, qx]
        return 'plaster'
    vacate = set()
    for y in range(H):
        for x in range(W):
            if protect(x, y) or wells[y, x]: continue
            p = (x, y)
            if p in ring: key[y, x] = 'in'
            elif p in inside:
                if src[y, x] not in BOARD: key[y, x] = 'wood_hi'
            elif old[y, x] or p in cut: vacate.add(p)
    # vacated dots take what surrounds them from outside, growing inwards
    filled = set()
    while vacate:
        done = {}
        for x, y in vacate:
            nb = [key[y + dy, x + dx] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if 0 <= x + dx < W and 0 <= y + dy < H
                  and (x + dx, y + dy) not in vacate and (x + dx, y + dy) not in E and key[y + dy, x + dx] != 'in'
                  and (not old[y + dy, x + dx] or (x + dx, y + dy) in filled)]
            if nb: done[(x, y)] = max(set(nb), key=nb.count)
        if not done:
            for p in vacate: key[p[1], p[0]] = 'plaster'
            break
        for (x, y), v in done.items(): key[y, x] = v
        vacate -= set(done); filled |= set(done)
    # rim tone, recomputed on the new oval (the shrunk grain goes; it is redrawn below as even arcs)
    for x, y in inside:
        if protect(x, y) or wells[y, x]: continue
        e = ein(x, y)
        key[y, x] = 'wood_mid' if e > .72 and (x - cx) / rx + (y - cy) / ry > .5 else 'wood_hi'
    # wood grain: two even arcs following the rim, broken where they would touch a well or the rim
    near = ndimage.binary_dilation(wells | (key == 'in'), iterations=2)
    for sc, (a0, a1) in ((.84, (-70, 40)), (.84, (110, 200)), (.68, (-40, 20)), (.68, (140, 190))):
        Es = smooth_mask({(x, y) for x, y in inside if ein(x, y) <= sc * sc})
        rs, _ = B.perfect_outline(Es)
        for x, y in rs:
            u, v = x + .5 - cx, y + .5 - cy
            ang = np.degrees(np.arctan2(v, u))
            if not (a0 <= ang <= a1 or a0 <= ang + 360 <= a1): continue
            if near[y, x] or protect(x, y): continue
            key[y, x] = 'wood'
    if hole_box:
        x0, y0, x1, y1 = hole_box
        import pixel_curves as PC
        hr, hi = PC.ellipse_ring(x0, y0, x1, y1)
        Hm = hr | hi
        for x, y in Hm:
            if (x, y) in inside and not protect(x, y):
                key[y, x] = 'in' if (x, y) in hr else 'plaster'
    return cx, cy, rx, ry


if __name__ == '__main__':
    f = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 18)
    for name, fn in (('house', house), ('atelier', atelier)):
        key = fn(); finish(key, name)
        o = Image.open(OUT / f'h80-{name}-color.png'); d = Image.open(OUT / f'r80-{name}-color.png')
        S_ = 4; s = Image.new('RGBA', (o.width * S_ * 2 + 30, o.height * S_ + 30), (244, 241, 233, 255)); dr = ImageDraw.Draw(s)
        for i, (im, t) in enumerate(((o, '80% 이전'), (d, '80% 다듬음'))):
            s.alpha_composite(im.resize((im.width * S_, im.height * S_), 0), (i * (o.width * S_ + 30), 30)); dr.text((i * (o.width * S_ + 30), 4), t, fill=(150, 0, 0), font=f)
        s.save(OUT / f'r80-{name}-cmp.png')


