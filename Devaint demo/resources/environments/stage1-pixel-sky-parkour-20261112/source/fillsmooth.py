"""Fill-only edge smoothing at native (1px) resolution.

The 2x2-dot pictures keep every pixel they have; only the empty notches of their stair-step edges are filled, so the
edge follows straight runs of one consistent slope (and curves whose runs grow or shrink steadily) instead of 2px
steps. Two passes:
1. silhouette: the picture's outline is fitted with straight segments (Douglas-Peucker over the traced boundary) and
   the polygon is filled; new pixels take the outline colour next to them.
2. inside edge of the outline: the inner (non-ink) area is smoothed the same way, so the ink band stays an even width
   instead of thickening at the steps. Inner details (bell, door, eye) are holes of that area and are left alone.
"""
import os, sys
import numpy as np
import cv2
from PIL import Image

INK = np.array([63, 62, 60, 255], np.uint8)


def smooth_mask(mask, eps):
    """fill-only: the outline is traced on the logical (2x2-dot) grid, straightened through the dots' centres
    (Douglas-Peucker, eps in dots) and filled at native resolution with sub-pixel vertices, so each straight edge
    comes out as one consistent run pattern; the result is unioned with the original mask"""
    lg = mask[::2, ::2].astype(np.uint8)
    contours, _ = cv2.findContours(np.pad(lg, 1), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    filled = np.zeros(mask.shape, np.uint8)
    S = 16
    for c in contours:
        a = cv2.approxPolyDP(c, eps, True) if len(c) >= 6 else c
        pts = ((a.astype(np.float64) - 1) * 2 + 1.0)          # dot centre on the native grid (dot x -> 2x+1)
        cv2.fillPoly(filled, [np.round(pts * S).astype(np.int32)], 1, lineType=cv2.LINE_8, shift=4)
    # notches only: never more than one pixel beyond the original edge (keeps gaps such as rope and plank open)
    near = cv2.dilate(mask.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    return mask | (filled.astype(bool) & near)


def nearest_colour(img, ys, xs, allowed):
    """for each new pixel, the most common colour among its 8 neighbours inside `allowed`"""
    h, w = allowed.shape
    out = []
    for y, x in zip(ys, xs):
        best = {}
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                yy, xx = y + dy, x + dx
                if 0 <= yy < h and 0 <= xx < w and allowed[yy, xx]:
                    k = tuple(img[yy, xx]); best[k] = best.get(k, 0) + (2 if dx == 0 or dy == 0 else 1)
        out.append(max(best, key=best.get) if best else tuple(INK))
    return out


def fill_smooth(im, eps=1.2, band=2.0):
    """1. fill the silhouette's notches (straightened outline polygon, fill-only);
    2. repaint the ink outline as an even band `band` px deep from the new edge: band pixels become ink, and old
       outline ink left deeper than the band takes the colour inside it. Only where the old edge was ink; inner
       lines (more than the band from the outside) are never touched."""
    a = np.array(im.convert('RGBA'))
    opaque = a[..., 3] > 0
    ink = np.all(a == INK, axis=-1)
    # the old outline ring: logical dots (2x2) on the silhouette edge that are ink
    lg = opaque[::2, ::2]
    pad = np.pad(lg, 1)
    edge_l = lg & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    old_ring = np.kron(edge_l, np.ones((2, 2), bool))[:a.shape[0], :a.shape[1]] & ink
    grown = smooth_mask(opaque, eps)
    new = grown & ~opaque
    ys, xs = np.nonzero(new)
    for (y, x), c in zip(zip(ys, xs), nearest_colour(a, ys, xs, opaque)): a[y, x] = c
    opaque = a[..., 3] > 0
    dist = cv2.distanceTransform(np.pad(opaque.astype(np.uint8), 1), cv2.DIST_L2, 5)[1:-1, 1:-1]
    near_ink = cv2.dilate(old_ring.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
    in_band = opaque & (dist <= band + .01) & near_ink
    paint_ink = in_band & ~np.all(a == INK, axis=-1)
    # only paint ink over pixels that were outline or newly filled, or inner pixels on a concave step
    a[paint_ink] = INK
    loose = old_ring & ~in_band & opaque
    inner = opaque & ~np.all(a == INK, axis=-1)
    ys, xs = np.nonzero(loose)
    for (y, x), c in zip(zip(ys, xs), nearest_colour(a, ys, xs, inner)): a[y, x] = c
    return Image.fromarray(a)


if __name__ == '__main__':
    src, out = sys.argv[1], sys.argv[2]
    eps = float(sys.argv[3]) if len(sys.argv) > 3 else 1.2
    os.makedirs(out, exist_ok=True)
    for f in sorted(os.listdir(src)):
        if f.endswith('.png'): fill_smooth(Image.open(os.path.join(src, f)), eps).save(os.path.join(out, f))
    print('done', out)
