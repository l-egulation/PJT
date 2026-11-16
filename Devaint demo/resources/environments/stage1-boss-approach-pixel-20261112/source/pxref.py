"""Pixelize a painted reference onto the 2x2-dot grid with colours sampled from the reference itself:
box-downsample to dot size, k-means palette from the art, joints thinned to one-dot lines in the art's own
joint colour, lone dots cleaned. No outline is added (background art)."""
import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.cluster.vq import kmeans2
from px import Canvas, declump, de_elbow, lint
from s_backdrop_trace import zhang_suen


def downsample(im, w, h):
    """premultiplied box downsample of an RGBA PIL image to w x h"""
    a = np.asarray(im.convert("RGBA")).astype(float) / 255
    pre = np.concatenate([a[..., :3] * a[..., 3:], a[..., 3:]], -1)
    out = np.asarray(Image.fromarray((pre * 255).astype(np.uint8)).resize((w, h), Image.BOX)).astype(float) / 255
    al = out[..., 3]
    rgb = np.where(al[..., None] > 1e-4, out[..., :3] / np.maximum(al[..., None], 1e-4), 0)
    return rgb * 255, al


def source_lines(im, w, h, drop=8, size=15, frac=.22):
    """joint lines found at full resolution (local darkness), then pooled to the dot grid"""
    a = np.asarray(im.convert("RGBA")).astype(float)
    lum = a[..., :3] @ [.299, .587, .114]
    op = a[..., 3] > 128
    loc = ndimage.median_filter(np.where(op, lum, np.median(lum[op]) if op.any() else 0), size=size)
    dark = ((lum < loc - drop) & op).astype(np.uint8) * 255
    pooled = np.asarray(Image.fromarray(dark).resize((w, h), Image.BOX)).astype(float) / 255
    return pooled >= frac


def pixelize(im, w, h, k=8, drop=8, min_line=4, seed=3, palette=None, keep_lines=True, frac=.22, size=15, line_dark=14, line_index=None):
    rgb, al = downsample(im, w, h)
    mask = al >= .5
    lum = rgb @ [.299, .587, .114]
    # light halo left by the cut-out: drop very light dots on the silhouette edge
    edge = mask & ~ndimage.binary_erosion(mask)
    mask &= ~(edge & (lum > np.percentile(lum[mask], 97)))
    line = (source_lines(im, w, h, drop, size, frac) & mask) if keep_lines else np.zeros_like(mask)
    line = zhang_suen(line)
    lab, n = ndimage.label(line, structure=np.ones((3, 3)))
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    for i, s in enumerate(sizes, 1):
        if s < min_line: line[lab == i] = False
    body = mask & ~line
    pts = rgb[body]
    if palette is None:
        np.random.seed(seed)
        palette, _ = kmeans2(pts, k, minit='++', seed=seed)
        palette = palette[np.argsort(palette @ [.299, .587, .114])]
    palette = np.clip(np.asarray(palette, float), 0, 255)
    idx = np.full((h, w), -1)
    wt = np.array([.3, .59, .11]) * 3
    idx[body] = ((pts[:, None, :] - palette[None]) ** 2 * wt).sum(-1).argmin(1)
    for _ in range(2):                                         # lone dots take their neighbours' majority
        out = idx.copy()
        for y in range(h):
            for x in range(w):
                if idx[y, x] < 0: continue
                nb = [idx[yy, xx] for yy in range(max(0, y - 1), min(h, y + 2)) for xx in range(max(0, x - 1), min(w, x + 2))
                      if (yy, xx) != (y, x) and idx[yy, xx] >= 0]
                if nb and nb.count(idx[y, x]) <= 1: out[y, x] = max(set(nb), key=nb.count)
        idx = out
    line_col = palette[line_index] if line_index is not None else np.clip(palette[0] - line_dark, 0, 255) if line_dark is not None else (np.median(rgb[line], 0) if line.any() else palette[0])
    lc = tuple(int(v) for v in line_col) + (255,)
    c = Canvas(w, h)
    for y in range(h):
        for x in range(w):
            if not mask[y, x]: continue
            c.px(x, y, lc if line[y, x] else tuple(int(v) for v in palette[idx[y, x]]) + (255,))
    fill = tuple(int(v) for v in palette[np.bincount(idx[idx >= 0]).argmax()]) + (255,)
    declump(c, lc, fill); de_elbow(c, lc)
    c.line_colour, c.palette = lc, [tuple(int(v) for v in p) + (255,) for p in palette]
    return c


def tidy_lines(c, lc, passes=2, fill=None):
    """straighten joint lines: bridge 1-2 dot gaps along rows/columns between real runs,
    drop specks (line dots with no line neighbour) and short stubs; fill freed dots from neighbours"""
    from collections import Counter
    W, H = c.w, c.h
    isl = lambda x, y: 0 <= x < W and 0 <= y < H and c.get(x, y) == lc
    def refill(x, y):
        nb = Counter(c.get(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                     if (dx or dy) and c.get(x + dx, y + dy) != lc and c.get(x + dx, y + dy)[3])
        if nb: c.px(x, y, nb.most_common(1)[0][0])
    for _ in range(passes):
        # bridge gaps
        for y in range(H):
            for x in range(W):
                if c.get(x, y)[3] == 0 or isl(x, y): continue
                for gap in (1, 2):
                    if isl(x - 1, y) and isl(x - 2, y) and all(isl(x + gap + k, y) for k in range(2)) and \
                            all(c.get(x + k, y)[3] for k in range(gap)):
                        for k in range(gap): c.px(x + k, y, lc)
                        break
                    if isl(x, y - 1) and isl(x, y - 2) and all(isl(x, y + gap + k) for k in range(2)) and \
                            all(c.get(x, y + k)[3] for k in range(gap)):
                        for k in range(gap): c.px(x, y + k, lc)
                        break
        # specks and stubs: components of fewer than 4 dots
        seen = set()
        for y in range(H):
            for x in range(W):
                if not isl(x, y) or (x, y) in seen: continue
                comp, st = [], [(x, y)]
                seen.add((x, y))
                while st:
                    px_, py_ = st.pop(); comp.append((px_, py_))
                    for dx in (-1, 0, 1):
                        for dy in (-1, 0, 1):
                            q = (px_ + dx, py_ + dy)
                            if q not in seen and isl(*q): seen.add(q); st.append(q)
                if len(comp) < 4:
                    for p in comp: refill(*p)
    if fill: declump(c, lc, fill)


def flatten(c, keep, size=3, passes=2):
    """mode filter on face colours (lines in `keep` untouched) for flatter stone faces"""
    from collections import Counter
    for _ in range(passes):
        grid = [[c.get(x, y) for x in range(c.w)] for y in range(c.h)]
        r = size // 2
        for y in range(c.h):
            for x in range(c.w):
                p = grid[y][x]
                if p[3] == 0 or p in keep: continue
                nb = Counter(grid[yy][xx] for yy in range(max(0, y - r), min(c.h, y + r + 1))
                             for xx in range(max(0, x - r), min(c.w, x + r + 1))
                             if grid[yy][xx][3] and grid[yy][xx] not in keep)
                best, n = nb.most_common(1)[0]
                if n > nb[p] + 1: c.px(x, y, best)


def shift(col, d):
    return tuple(max(0, min(255, v + d)) for v in col[:3]) + (255,)


def bevel(c, lc, up=12, down=-12):
    """stone relief: the dot under a joint (top edge) and right of a joint (left edge) catch light,
    the dot over a joint and left of a joint fall in shade. Lifts each face's own colour."""
    grid = [[c.get(x, y) for x in range(c.w)] for y in range(c.h)]
    g = lambda x, y: grid[y][x] if 0 <= x < c.w and 0 <= y < c.h else (0, 0, 0, 0)
    for y in range(c.h):
        for x in range(c.w):
            p = grid[y][x]
            if p[3] == 0 or p == lc: continue
            hrun = lambda yy: all(g(x + k, yy) == lc for k in (-2, -1, 0, 1, 2))     # straight horizontal joint
            vrun = lambda xx: all(g(xx, y + k) == lc for k in (-2, -1, 0, 1, 2))     # straight vertical joint
            if hrun(y - 1) or vrun(x - 1): c.px(x, y, shift(p, up))
            elif hrun(y + 1) or vrun(x + 1): c.px(x, y, shift(p, down))


def smooth_silhouette(c, passes=3):
    """remove one-dot bumps and fill one-dot notches on the outer edge (thin vertical drips are kept)"""
    from collections import Counter
    from px import smooth_mask, CLEAR
    op = lambda x, y: 0 <= x < c.w and 0 <= y < c.h and c.get(x, y)[3] > 0
    for _ in range(passes):
        changed = False
        for y in range(c.h):
            for x in range(c.w):
                n4 = sum(op(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                if op(x, y):
                    drip = op(x, y - 1) and op(x, y - 2) and not op(x - 1, y) and not op(x + 1, y)
                    if n4 <= 1 and not drip: c.px(x, y, CLEAR); changed = True
                elif n4 >= 3:
                    nb = Counter(c.get(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if op(x + dx, y + dy))
                    c.px(x, y, nb.most_common(1)[0][0]); changed = True
        if not changed: break
    opaque = {(x, y) for x in range(c.w) for y in range(c.h) if c.get(x, y)[3]}
    for x, y in opaque - smooth_mask(opaque): c.px(x, y, CLEAR)


def flat_stones(c, lc, min_region=6, two_tone=True):
    """one clean tone per stone: every region enclosed by joint lines takes its median colour;
    big regions keep a second tone where the painting has a clear light/shade split"""
    import numpy as np
    from scipy import ndimage
    a = np.asarray(c.im).astype(int)
    line = (a[..., 0] == lc[0]) & (a[..., 1] == lc[1]) & (a[..., 2] == lc[2]) & (a[..., 3] > 0)
    face = (a[..., 3] > 0) & ~line
    lab, n = ndimage.label(face)
    lum = a[..., :3] @ np.array([.299, .587, .114])
    for i in range(1, n + 1):
        m = lab == i
        if m.sum() < min_region: continue
        cols = a[..., :3][m]
        if two_tone and m.sum() > 40:
            l = lum[m]; split = np.median(l)
            lo, hi = l < split - 6, l >= split - 6
            if lo.sum() > .25 * len(l) and hi.sum() > .25 * len(l):
                for sel, sub in ((lo, cols[lo]), (hi, cols[hi])):
                    col = np.median(sub, 0).astype(int)
                    ys, xs = np.nonzero(m); ys, xs = ys[sel], xs[sel]
                    a[ys, xs, :3] = col
                continue
        a[m, :3] = np.median(cols, 0).astype(int)
    from PIL import Image
    c.im.paste(Image.fromarray(a.astype(np.uint8), 'RGBA'))


def add_shading(c, faces, src_im, step=6, levels=2, gain=1.0, clean=2, smooth=0):
    """keep the traced lines/edges; inside every flat face region, add the painting's own light and shade
    as a few tone steps relative to that stone's colour (quantized, single dots cleaned)"""
    import numpy as np
    from scipy import ndimage
    from collections import Counter
    rgb, al = downsample(src_im, c.w, c.h)
    lum = rgb @ [.299, .587, .114]
    if smooth: lum = ndimage.median_filter(lum, size=smooth)
    a = np.asarray(c.im).astype(int)
    facemask = np.zeros((c.h, c.w), bool)
    for f in faces:
        facemask |= (a[..., 0] == f[0]) & (a[..., 1] == f[1]) & (a[..., 2] == f[2]) & (a[..., 3] > 0)
    lab, n = ndimage.label(facemask)
    q = np.zeros((c.h, c.w), int)
    for i in range(1, n + 1):
        m = lab == i
        med = np.median(lum[m])
        q[m] = np.clip(np.round((lum[m] - med) * gain / (step + 1)), -levels, levels).astype(int)
    for _ in range(clean):                     # mode filter inside each region: no lone dots
        out = q.copy()
        for y, x in zip(*np.nonzero(facemask)):
            nb = Counter(q[yy, xx] for yy in range(max(0, y - 1), min(c.h, y + 2)) for xx in range(max(0, x - 1), min(c.w, x + 2))
                         if lab[yy, xx] == lab[y, x])
            best, k = nb.most_common(1)[0]
            if nb[q[y, x]] <= 2 and k > nb[q[y, x]]: out[y, x] = best
        q = out
    for y, x in zip(*np.nonzero(facemask & (q != 0))):
        c.px(int(x), int(y), shift(tuple(a[y, x]), int(q[y, x]) * step))


def stone_texture(c, lines, seed=1, density=1.0, deep=14, lit=9):
    """crisp stone texture inside each face bounded by joint lines: pits (dark dot + lit rim),
    short grain strokes, worn chips on the top edge and a few light flecks. Tones are the stone's own
    colour stepped up/down, so the texture stays clean (no blur)."""
    import random
    import numpy as np
    from scipy import ndimage
    rng = random.Random(seed)
    a = np.asarray(c.im).astype(int)
    isline = np.zeros((c.h, c.w), bool)
    for l in lines: isline |= (a[..., 0] == l[0]) & (a[..., 1] == l[1]) & (a[..., 2] == l[2])
    face = (a[..., 3] > 0) & ~isline
    lab, n = ndimage.label(face)
    inner = ndimage.binary_erosion(face, iterations=2)
    get = lambda x, y: tuple(int(v) for v in a[y, x])
    def put(x, y, d):
        if 0 <= x < c.w and 0 <= y < c.h and face[y, x]: c.px(x, y, shift(get(x, y), d))
    for i in range(1, n + 1):
        m = lab == i
        area = int(m.sum())
        if area < 30: continue
        ys, xs = np.nonzero(m & inner)
        if len(xs) == 0: continue
        pick = lambda: (int(xs[k := rng.randrange(len(xs))]), int(ys[k]))
        for _ in range(max(1, round(area / 140 * density))):              # pits
            x, y = pick(); put(x, y, -deep); put(x - 1, y - 1, lit)
            if rng.random() < .4: put(x + 1, y, -deep // 2)
        for _ in range(round(area / 260 * density)):                       # grain strokes  //
            x, y = pick()
            for k in range(3): put(x + k, y - k, -deep // 2)
            if rng.random() < .6:
                for k in range(3): put(x + k + 2, y - k, -deep // 2)
        for _ in range(round(area / 180 * density)):                       # light flecks
            x, y = pick(); put(x, y, lit)
        top = ys.min()                                                     # a worn chip on the top edge
        row = xs[ys == top]
        if len(row) > 8 and rng.random() < .7:
            x = int(rng.choice(row[2:-2])); put(x, top, -deep); put(x + 1, top, -deep // 2); put(x, top + 1, lit)


def stone_tint(c, lines, seed=2, warm=(6, 3, -3), cool=(-3, -1, 4), keep=()):
    """each stone (region between joint lines) leans slightly warm, cool or neutral: more colours, still flat"""
    import random
    import numpy as np
    from scipy import ndimage
    rng = random.Random(seed)
    a = np.asarray(c.im).astype(int)
    isline = np.zeros((c.h, c.w), bool)
    for l in list(lines) + list(keep): isline |= (a[..., 0] == l[0]) & (a[..., 1] == l[1]) & (a[..., 2] == l[2])
    face = (a[..., 3] > 0) & ~isline
    lab, n = ndimage.label(face)
    for i in range(1, n + 1):
        m = lab == i
        if m.sum() < 25: continue
        r = rng.random()
        d = warm if r < .38 else cool if r < .72 else None
        if d is None: continue
        a[m, :3] = np.clip(a[m, :3] + np.array(d), 0, 255)
    from PIL import Image
    c.im.paste(Image.fromarray(a.astype(np.uint8), 'RGBA'))


def light_temperature(c, warm=(5, 2, -4), cool=(-4, -1, 5), lo=170, hi=214):
    """one light for the whole piece: dots darker than `lo` lean cool, dots lighter than `hi` lean warm"""
    import numpy as np
    from PIL import Image
    a = np.asarray(c.im).astype(int)
    op = a[..., 3] > 0
    lum = a[..., :3] @ np.array([.299, .587, .114])
    k_cool = np.clip((lo - lum) / 40, 0, 1)[..., None] * (op[..., None])
    k_warm = np.clip((lum - hi) / 18, 0, 1)[..., None] * (op[..., None])
    a[..., :3] = np.clip(np.round(a[..., :3] + k_cool * np.array(cool) + k_warm * np.array(warm)), 0, 255)
    c.im.paste(Image.fromarray(a.astype(np.uint8), 'RGBA'))
