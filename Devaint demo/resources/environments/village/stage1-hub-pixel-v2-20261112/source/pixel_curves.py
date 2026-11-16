"""Pixel-art line rules (the 'jaggies' rule, user reference video 2026-11-12):
- a straight line is made of runs of one equal length (1-1-1, 2-2-2, ...), so slopes are whole ratios
- a curve's runs only shrink then grow (...3,2,1,1,2,3...), never up and down
Tools: midpoint ellipse rings (monotone runs by construction), even straight lines, and a checker that measures runs."""
import math


def _quadrant(rx, ry):
    """first-quadrant points of the midpoint ellipse with integer radii (8-connected, monotone runs)"""
    pts = []
    if rx == 0 or ry == 0:
        if rx == 0: return [(0, y) for y in range(ry + 1)]
        return [(x, 0) for x in range(rx + 1)]
    x, y = 0, ry
    rx2, ry2 = rx * rx, ry * ry
    p = ry2 - rx2 * ry + rx2 / 4
    while ry2 * x < rx2 * y:
        pts.append((x, y))
        x += 1
        if p < 0: p += ry2 * (2 * x + 1)
        else: y -= 1; p += ry2 * (2 * x + 1) - 2 * rx2 * y
    p = ry2 * (x + .5) ** 2 + rx2 * (y - 1) ** 2 - rx2 * ry2
    while y >= 0:
        pts.append((x, y))
        y -= 1
        if p > 0: p += rx2 * (1 - 2 * y)
        else: x += 1; p += ry2 * 2 * x + rx2 * (1 - 2 * y)
    return pts


def ellipse_ring(x0, y0, x1, y1):
    """outline (set) and filled interior (set, outline excluded) of the ellipse in the inclusive box; even widths or
    heights get a flat two-dot top/side, so the ring stays symmetric"""
    Wd, Hd = x1 - x0 + 1, y1 - y0 + 1
    rx, ry = (Wd - 1) // 2, (Hd - 1) // 2
    ox, oy = Wd - 1 - 2 * rx, Hd - 1 - 2 * ry          # 1 when even
    cx, cy = x0 + rx, y0 + ry
    ring = set()
    for x, y in _quadrant(rx, ry):
        ring |= {(cx + x + ox, cy - y), (cx - x, cy - y), (cx + x + ox, cy + y + oy), (cx - x, cy + y + oy)}
    if oy:                                              # even height: the flat sides join
        ring |= {(cx - rx, cy), (cx + rx + ox, cy), (cx - rx, cy + 1), (cx + rx + ox, cy + 1)}
    inside = set()
    for y in range(y0, y1 + 1):
        xs = [x for x, yy in ring if yy == y]
        if xs: inside |= {(x, y) for x in range(min(xs) + 1, max(xs))}
    return ring, inside - ring


def even_line(x0, y0, x1, y1):
    """a straight line whose runs all have one length: the run length is the rounded ratio, the far end moves to fit"""
    dx, dy = x1 - x0, y1 - y0
    sx, sy = (1 if dx >= 0 else -1), (1 if dy >= 0 else -1)
    adx, ady = abs(dx), abs(dy)
    pts = []
    if adx >= ady:
        n = max(1, round(adx / max(1, ady))) if ady else adx + 1
        x, y = x0, y0
        for k in range(ady + 1 if ady else 1):
            for i in range(n if ady else adx + 1):
                pts.append((x, y)); x += sx
            y += sy
    else:
        n = max(1, round(ady / max(1, adx))) if adx else ady + 1
        x, y = x0, y0
        for k in range(adx + 1 if adx else 1):
            for i in range(n if adx else ady + 1):
                pts.append((x, y)); y += sy
            x += sx
    return pts


def runs_of(path, horizontal=True):
    """run lengths along an ordered 8-connected path (counting along x if horizontal else along y)"""
    runs, cur = [], 1
    for (xa, ya), (xb, yb) in zip(path, path[1:]):
        same = (ya == yb) if horizontal else (xa == xb)
        if same: cur += 1
        else: runs.append(cur); cur = 1
    runs.append(cur)
    return runs


def monotone(runs):
    """runs only shrink then grow (or only one way)"""
    i = 0
    while i + 1 < len(runs) and runs[i + 1] <= runs[i]: i += 1
    while i + 1 < len(runs) and runs[i + 1] >= runs[i]: i += 1
    return i + 1 >= len(runs)
