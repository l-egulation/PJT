"""Hand-drawn leaf set: pointed ovate leaf in four diagonal orientations + an upright leaf; rosettes and vines."""
from px import *

DIAG = ["....DDD",          # tip up-right, stem down-left, vein along the diagonal
        "..DDWWD",
        ".DWWWMD",
        "DWWMLD.",
        "DWMLLD.",
        "DMLDD..",
        ".DD...."]
UP = ["..D..",
      ".DWD.",
      "DWWWD",
      "DWMLD",
      "DWMLD",
      ".DML.".replace('.DML.', '.DMD.'),
      "..D.."]


def orient(rows, fh=False, fv=False):
    if fh: rows = [r[::-1] for r in rows]
    if fv: rows = rows[::-1]
    return rows


def leaf(c, rows, x, y, lit=True, line=D):
    pal = {'D': line, 'W': W if lit else C, 'M': L if lit else M, 'L': L if lit else M}
    paint(c, rows, x, y, pal)


def rosette(c, cx, cy, lit=True, rng=None, line=D):
    """five leaves radiating from one point, like the original's star-shaped clumps"""
    parts = [(orient(DIAG), cx, cy - 6), (orient(DIAG, fh=True), cx - 6, cy - 6),
             (orient(DIAG, fv=True), cx, cy), (orient(DIAG, fh=True, fv=True), cx - 6, cy), (UP, cx - 2, cy - 9)]
    if rng is not None: rng.shuffle(parts)
    for rows, x, y in parts:
        leaf(c, rows, x, y, lit, line)


def vine(c, x0, y0, length, rng, line=D):
    import math
    ph = rng.random() * 6
    stem = [(int(round(x0 + 1.4 * math.sin(k / 7 + ph))), y0 + k) for k in range(length)]
    for x, y in stem: c.px(x, y, M)
    k = 2
    side = rng.random() < .5
    while k < length - 3:
        x, y = stem[k]
        if side: leaf(c, orient(DIAG, fv=k > length * .5), x + 1, y - 3, rng.random() < .7, line)
        else: leaf(c, orient(DIAG, fh=True, fv=k > length * .5), x - 7, y - 3, rng.random() < .7, line)
        side = not side
        k += rng.randint(4, 6)
    leaf(c, UP, stem[-1][0] - 2, stem[-1][1] - 3, True, line) if False else c.px(*stem[-1], line)
