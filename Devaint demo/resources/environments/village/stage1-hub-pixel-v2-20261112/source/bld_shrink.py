"""Buildings at 80 %: the finished 100 % drawings (bld_detail4) lose one row and one column in every five, chosen where
they lose the least (a row/column that repeats its neighbours), so one-dot lines and small details survive; then the
arches are redrawn as clean concentric circles at their new size, the brush handles as clean even bands, and any
doubled ink is thinned."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from px import *
import bld_detail as B
import bld_detail2 as E
import bld_detail4 as F

OUT = Path(__file__).parent.parent / "bld2"


def keep_index(key, axis, keep=4, group=5, protect=()):
    """indices to keep along an axis: in each group of `group`, drop the line that differs least from its neighbours"""
    n = key.shape[axis]
    lines = [key[i] if axis == 0 else key[:, i] for i in range(n)]
    def cost(i):
        if i in protect: return 1e9
        c = 0
        a = lines[i]
        if i > 0: c += np.sum(a != lines[i - 1])
        if i + 1 < n: c += np.sum(a != lines[i + 1])
        # a line that carries ink its neighbours do not (a thin horizontal/vertical stroke) is expensive
        ink = a == 'in'
        if i > 0 and i + 1 < n: c += 6 * np.sum(ink & (lines[i - 1] != 'in') & (lines[i + 1] != 'in'))
        return c
    kept = []
    for g in range(0, n, group):
        idx = list(range(g, min(n, g + group)))
        if len(idx) == group:
            drop = min(idx, key=cost)
            idx.remove(drop)
        kept += idx
    return kept


def shrink(key, protect_rows=(), protect_cols=()):
    rows = keep_index(key, 0, protect=set(protect_rows) | {key.shape[0] - 1})
    cols = keep_index(key, 1, protect=set(protect_cols))
    small = key[np.ix_(rows, cols)]
    fy = lambda y: min(range(len(rows)), key=lambda i: abs(rows[i] - y))
    fx = lambda x: min(range(len(cols)), key=lambda i: abs(cols[i] - x))
    return small, fx, fy


def house80():
    key, fx, fy = shrink(F.house4())
    for x0, x1, top, bottom in ((33, 58, 54, 76), (86, 102, 58, 80), (86, 102, 118, 139), (32, 59, 118, 161)):
        B.clean_arch_c(key, fx(x0), fx(x1), fy(top), fy(bottom))
    E.declump_key(key)
    return key


def redraw_wells(key, region):
    """paint wells refitted to clean pixel circles of their own box: ink ring, paint, lit crescent, shaded crescent"""
    from scipy import ndimage
    x0r, y0r, x1r, y1r = region
    H, W = key.shape
    for name in ('paint_r', 'paint_y', 'paint_g', 'paint_b', 'wood_lo'):
        fam = (name, name + '_hi', name + '_lo') if name != 'wood_lo' else (name,)
        m = np.zeros(key.shape, bool); m[y0r:y1r, x0r:x1r] = np.isin(key[y0r:y1r, x0r:x1r], fam)
        lab, n = ndimage.label(m)
        for i in range(1, n + 1):
            blob = lab == i
            if blob.sum() < (12 if name == 'wood_lo' else 30): continue
            ys, xs = np.nonzero(blob)
            bw, bh = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
            if max(bw, bh) > 1.6 * min(bw, bh): continue
            d = round((bw + bh) / 2) + 2                       # with its ink ring
            cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
            x0, y0 = int(round(cx - (d - 1) / 2)), int(round(cy - (d - 1) / 2))
            import pixel_curves as PC
            if d >= 10:
                edge, ins = PC.ellipse_ring(x0, y0, x0 + d - 1, y0 + d - 1)
                disc = edge | ins; cut = set()
            else:                                          # small: the classic round (runs 3-1-1 / 1-1-3), not a diamond
                SMALL = {7: ["..###..", ".#...#.", "#.....#", "#.....#", "#.....#", ".#...#.", "..###.."],
                         8: ["..####..", ".#....#.", "#......#", "#......#", "#......#", "#......#", ".#....#.", "..####.."],
                         9: ["...###...", ".##...##.", ".#.....#.", "#.......#", "#.......#", "#.......#", ".#.....#.", ".##...##.", "...###..."]}
                rows = SMALL.get(d, SMALL[8] if d > 8 else SMALL[7])
                edge, ins = set(), set()
                for j, r_ in enumerate(rows):
                    xs_ = [i for i, ch in enumerate(r_) if ch == '#']
                    for i, ch in enumerate(r_):
                        if ch == '#': edge.add((x0 + i, y0 + j))
                        elif xs_ and min(xs_) < i < max(xs_): ins.add((x0 + i, y0 + j))
                disc = edge | ins; cut = set()
            # the old well and its ring go to the board colour around it
            grown = ndimage.binary_dilation(blob, iterations=2)
            board = [key[y, x] for y, x in zip(*np.nonzero(ndimage.binary_dilation(grown, iterations=2) & ~grown)) if key[y, x] not in ('in',) + fam]
            bc = max(set(board), key=board.count) if board else 'wood_hi'
            for y, x in zip(*np.nonzero(grown)):
                if key[y, x] in ('in',) + fam: key[y, x] = bc
            for x, y in cut: key[y, x] = bc
            r = d / 2; ccx, ccy = x0 + (d - 1) / 2, y0 + (d - 1) / 2
            for x, y in disc - edge - cut:
                dd = ((x - ccx) ** 2 + (y - ccy) ** 2) ** .5
                v = name
                if name != 'wood_lo':
                    if dd > r - 2.4 and (x - ccx) + (y - ccy) > r * .5: v = name + '_lo'
                    elif r * .3 < dd < r * .7 and x - ccx < -r * .15 and y - ccy < -r * .15: v = name + '_hi'
                key[y, x] = v
            for x, y in edge: key[y, x] = 'in'


def atelier80():
    key, fx, fy = shrink(F.atelier4())
    for x0, x1, top, bottom in ((95, 116, 144, 170), (45, 77, 145, 202)):
        B.clean_arch_c(key, fx(x0), fx(x1), fy(top), fy(bottom))
    py0, py1, px0, px1 = fy(84), fy(92), fx(18), fx(60)
    plank = key[py0:py1 + 1, px0:px1 + 1].copy()
    leaf = lambda x, y: key[y, x] in ('leaf', 'leaf_hi', 'leaf_lo') or (py0 <= y <= py1 and px0 <= x <= px1)
    w = 8
    E.clean_handle(key, fy(57), fy(103), fx(16), fx(20), width=w, keep=leaf, old=(fx(16), fx(20)))
    E.clean_handle(key, fy(54), fy(103), fx(34), fx(34), width=w, keep=leaf, old=(fx(34), fx(34)))
    E.clean_handle(key, fy(62), fy(103), fx(53), fx(49), width=w, keep=leaf, old=(fx(53), fx(49)))
    key[py0:py1 + 1, px0:px1 + 1] = plank
    redraw_wells(key, (fx(60), fy(20), fx(145), fy(125)))
    E.declump_key(key)
    return key


if __name__ == '__main__':
    f = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 18)
    for name, fn in (('house', house80), ('atelier', atelier80)):
        key = fn()
        print(name, key.shape[::-1])
        for v, P in (('color', F.CO4), ('sketch', F.SK4)):
            c = B.paint_keys(key, P); lint(c, colors=[K, P['in']], name=f'{name}80 {v}')
            c.im.save(OUT / f'h80-{name}-{v}.png')
        o = Image.open(OUT / f'g-{name}-color.png'); d = Image.open(OUT / f'h80-{name}-color.png'); k = Image.open(OUT / f'h80-{name}-sketch.png')
        S = 3; s = Image.new('RGBA', (o.width * S + d.width * S * 2 + 60, o.height * S + 30), (244, 241, 233, 255)); dr = ImageDraw.Draw(s)
        x = 0
        for im, t in ((o, '100%'), (d, '80% · 컬러'), (k, '80% · 스케치')):
            s.alpha_composite(im.resize((im.width * S, im.height * S), 0), (x, 30 + (o.height - im.height) * S)); dr.text((x, 4), t, fill=(150, 0, 0), font=f); x += im.width * S + 30
        s.save(OUT / f'h80-{name}-cmp.png')
