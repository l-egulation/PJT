"""Detailed props for the maintenance backdrop: fences, barrel, crate, draped cloth."""
import math
from px import *

LINE = D


def post(c, x0, y0, y1):
    """5-wide fence post with a rounded cap, grain and light/shade sides"""
    c.rect(x0, y0 + 1, x0 + 4, y1, fill=C)
    c.hl(x0 + 1, x0 + 3, y0, C)
    c.vl(x0, y0 + 1, y1, LINE); c.vl(x0 + 4, y0 + 1, y1, LINE); c.hl(x0 + 1, x0 + 3, y0, LINE)
    c.px(x0, y0, CLEAR); c.px(x0 + 4, y0, CLEAR)
    c.vl(x0 + 1, y0 + 1, y1, W); c.vl(x0 + 3, y0 + 2, y1, L)
    c.hl(x0 + 1, x0 + 3, y0 + 3, L)                                    # cap groove
    for y in range(y0 + 7, y1 - 2, 9): c.vl(x0 + 2, y, y + 2, M)       # grain


def rail(c, xa, xb, y0, rng_seed):
    c.rect(xa, y0, xb, y0 + 4, fill=C)
    c.hl(xa, xb, y0, LINE); c.hl(xa, xb, y0 + 4, LINE)
    c.hl(xa, xb, y0 + 1, W); c.hl(xa, xb, y0 + 3, L)
    k = rng_seed
    for x in range(xa + 3, xb - 3, 7):                                  # grain dashes
        k = (k * 37 + 11) % 5
        c.hl(x + k % 3, x + k % 3 + 2, y0 + 2, M)


def fence(c, left, right, seed):
    post(c, left, 96, 128); post(c, right, 96, 128)
    for i, y0 in enumerate((102, 113)):
        rail(c, left + 5, right - 1, y0, seed + i)
        for px_ in (left + 2, right + 2): c.px(px_, y0 + 2, LINE)        # nail heads on the posts


def barrel(c):
    x0, x1, y0, y1 = 36, 65, 96, 128
    cx = (x0 + x1 + 1) / 2
    half = lambda y: 10 + 2.6 * math.sin(math.pi * (y - y0) / (y1 - y0))
    m = smooth_mask({(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if abs(x + .5 - cx) <= half(y)})
    for x, y in m: c.px(x, y, C)
    rr = rings(m)
    for x, y in rr[1]: c.px(x, y, W if x < cx else L)
    for x, y in rr[2]:
        if x > cx + 3: c.px(x, y, L)
    # staves follow the bulge
    for f in (-.62, -.25, .12, .5):
        for y in range(y0 + 4, y1):
            x = int(round(cx + f * half(y)))
            if (x, y) in m and (x, y) not in rr[0]: c.px(x, y, M)
    # lid seen from slightly above
    lid = smooth_mask(ellipse_mask(int(cx - 10), y0, int(cx + 9), y0 + 4))
    for x, y in lid: c.px(x, y, L)
    for x, y in rings(lid)[0]: c.px(x, y, LINE)
    c.hl(int(cx - 6), int(cx + 5), y0 + 2, M)
    # iron hoops: dark band with a lit edge
    for hy in (y0 + 7, (y0 + y1) // 2, y1 - 6):
        for x in range(x0, x1 + 1):
            if (x, hy) in m and (x, hy) not in rr[0]: c.px(x, hy, LINE)
            if (x, hy + 1) in m and (x, hy + 1) not in rr[0]: c.px(x, hy + 1, M if x > cx else L)
        c.px(int(cx - half(hy) + 3), hy + 1, W)
    for x, y in rr[0]: c.px(x, y, LINE)


def crate(c):
    x0, y0, x1, y1 = 68, 107, 96, 128
    c.rect(x0, y0, x1, y1, fill=C)
    c.rect(x0, y0, x1, y1, line=LINE)
    c.rect(x0 + 3, y0 + 3, x1 - 3, y1 - 3, line=M)                      # frame boards
    c.hl(x0 + 1, x1 - 1, y0 + 1, W); c.vl(x0 + 1, y0 + 2, y1 - 1, W)
    c.hl(x0 + 1, x1 - 1, y1 - 1, L); c.vl(x1 - 1, y0 + 2, y1 - 2, L)
    for y in (y0 + 8, y0 + 13):                                          # inner planks
        c.hl(x0 + 4, x1 - 4, y, M)
    for x in range(x0 + 4, x1 - 3):                                      # plank shading under each seam
        for y in (y0 + 9, y0 + 14):
            if (x + y) % 2: c.px(x, y, L)
    # diagonal brace, two dots wide with an edge highlight
    n = min(x1 - x0, y1 - y0) - 9
    for k in range(n + 1):                                               # brace board: edge, face, face, edge
        x, y = x0 + 4 + k, y1 - 6 - k
        c.px(x, y, LINE); c.px(x, y + 1, W); c.px(x, y + 2, C); c.px(x, y + 3, LINE)
    for x, y in ((x0 + 2, y0 + 2), (x1 - 2, y0 + 2), (x0 + 2, y1 - 2), (x1 - 2, y1 - 2)): c.px(x, y, LINE)  # nails


def cloth(c):
    """canvas thrown over the beam: smooth sagging hem, a long right corner, soft folds flowing to that corner"""
    xa, xb, top = 169, 226, 28
    hem = {}
    for x in range(xa, xb + 1):
        t = (x - xa) / (xb - xa)
        y = 37 + 7 * math.sin(math.pi * min(1, t * 1.25)) + (max(0, t - .72) / .28) ** 1.6 * 16
        hem[x] = int(round(y))
    for x in range(xa, xb + 1):
        for y in range(top, hem[x]): c.px(x, y, W)
    # right third sits a little in shade
    for x in range(xa, xb + 1):
        for y in range(top + 3, hem[x]):
            if x > xa + (xb - xa) * .66 and c.get(x, y) == W: c.px(x, y, C)
    c.hl(xa, xb, top, LINE); c.hl(xa + 1, xb - 1, top + 1, W)
    # hem: one dot thick, a light inner edge
    for x in range(xa, xb + 1):
        c.px(x, hem[x], LINE)
        if c.get(x, hem[x] - 1) in (W, C): c.px(x, hem[x] - 1, L if x > xa + (xb - xa) * .66 else C)
        if x < xb and abs(hem[x + 1] - hem[x]) > 1:
            lo, hi = sorted((hem[x], hem[x + 1]))
            for y in range(lo + 1, hi): c.px(x if hem[x] > hem[x + 1] else x + 1, y, LINE)
    c.vl(xa, top, hem[xa], LINE); c.vl(xb, top, hem[xb], LINE)
