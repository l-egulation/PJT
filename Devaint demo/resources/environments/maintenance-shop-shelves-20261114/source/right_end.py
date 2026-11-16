"""1챕터 정비소 건물의 오른쪽 끝(새로 그림, 왼쪽을 뒤집지 않음). 정비소 그림의 8색, 연회색 외곽선, 2px 점.
계단식 불규칙 벽과 머릿돌, 문기둥, 벽에 기댄 사다리, 궤짝 두 개와 자루, 양동이, 뾰족한 판자 울타리, 흘러내리는 덩굴.

    python right_end.py <px.py 폴더>
"""
import math
import random
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, sys.argv[1]); sys.path.insert(0, str(HERE))
from px import Canvas, h  # noqa: E402

OUT, FACE, HI, SH, MARK = h('6f6e69'), h('fef7ee'), h('fffcf7'), h('ccc7be'), h('a09d96')
CLEAR = (0, 0, 0, 0)
CW, CH = 124, 129                      # dots (248 x 258 px, the backdrop's height)
FLOOR = CH - 1
TOPS = [(0, 9), (34, 36), (58, 64), (84, 92)]    # stepped wall: (from column, top row)
WALL_END = 104


def top_at(x):
    t = None
    for x0, ty in TOPS:
        if x >= x0: t = ty
    return t if x < WALL_END else None


def wall(c, r):
    """irregular stones (rubble.py): uneven course heights and widths, chipped corners, light joints on the lit side"""
    from rubble import rubble, outline
    jitter = {x: (1 if (x // 7) % 3 == 1 else 0) for x in range(CW)}
    inside = lambda x, y: top_at(x) is not None and y >= top_at(x) + jitter[x]
    rubble(c, inside, 0, 0, WALL_END - 1, FLOOR, seed=r.randint(0, 999))
    outline(c, inside, 0, 0, WALL_END - 1, FLOOR)
    for x0, x1, ty in ((32, 46, 36), (56, 70, 64), (82, 96, 92)):                   # cap stones on the steps, corners chipped
        y = ty - 3
        c.rect(x0, y, x1, y + 3, fill=FACE); c.hl(x0 + 1, x1 - 1, y, OUT); c.hl(x0 + 1, x1 - 1, y + 1, HI); c.hl(x0, x1, y + 3, OUT); c.hl(x0 + 1, x1 - 1, y + 2, SH)
        c.vl(x0, y + 1, y + 3, OUT); c.vl(x1, y + 1, y + 3, OUT); c.px(x0, y, CLEAR); c.px(x1, y, CLEAR)


def jamb(c):
    """the opening's right jamb: narrow stacked blocks, like the shop's"""
    for y0 in range(38, FLOOR, 12):
        y1 = min(FLOOR, y0 + 11)
        c.rect(0, y0, 5, y1, fill=FACE); c.vl(0, y0, y1, OUT); c.vl(5, y0, y1, OUT); c.hl(0, 5, y0, OUT); c.vl(4, y0 + 1, y1, SH); c.vl(1, y0 + 1, y1, HI)


def ladder(c, x_foot, y_top, x_top, gap=9):
    """two poles (outline, face, outline) leaning on the wall, rungs every 7 dots"""
    n = FLOOR - y_top
    for i in range(n + 1):
        y = FLOOR - i; x = round(x_foot + (x_top - x_foot) * i / n)
        for rail in (0, gap):
            c.px(x + rail - 1, y, OUT); c.px(x + rail, y, FACE if rail else HI); c.px(x + rail + 1, y, OUT)
        if i % 7 == 4:
            c.hl(x + 2, x + gap - 2, y - 1, OUT); c.hl(x + 2, x + gap - 2, y, FACE); c.hl(x + 2, x + gap - 2, y + 1, OUT)


def crate(c, x0, y1, w, hgt, cross=True):
    y0 = y1 - hgt + 1
    c.rect(x0, y0, x0 + w - 1, y1, fill=FACE, line=OUT)
    c.rect(x0 + 2, y0 + 2, x0 + w - 3, y1 - 2, line=OUT)
    c.hl(x0 + 1, x0 + w - 2, y0 + 1, HI); c.vl(x0 + w - 2, y0 + 1, y1 - 1, SH)
    if cross:
        for i in range(w - 5):
            yy = y0 + 3 + round(i * (hgt - 6) / (w - 6)); c.px(x0 + 3 + i, yy, OUT); c.px(x0 + w - 4 - i, yy, OUT)
    else:
        for yy in range(y0 + 5, y1 - 2, 4): c.hl(x0 + 3, x0 + w - 4, yy, SH)


def sack(c, x0, y1):
    rows = ['....DDD....', '...DSMSD...', '....DSD....', '..DDFFFDD..', '.DFFHFFFSD.', 'DFHFFFFFFSD', 'DFFFFFFFFSD', 'DFFFFFMFFSD',
            'DFFFFFFFSSD', 'DSFFFFFSSSD', '.DSSSSSSSD.', '..DDDDDDD..']
    for yy, row in enumerate(rows):
        for xx, ch in enumerate(row):
            col = {'D': OUT, 'F': FACE, 'H': HI, 'S': SH, 'M': MARK}.get(ch)
            if col: c.px(x0 + xx, y1 - len(rows) + 1 + yy, col)


def bucket(c, x0, y1):
    rows = ['..DDDDDDD..', '.D.......D.', 'D.........D', 'DDDDDDDDDDD', 'DHFFFFFFFSD', '.DFFMMMFSD.', '.DFFFFFFSD.', '.DMMMMMMMD.', '..DFFFFSD..', '..DDDDDDD..']
    for yy, row in enumerate(rows):
        for xx, ch in enumerate(row):
            col = {'D': OUT, 'F': FACE, 'H': HI, 'S': SH, 'M': MARK}.get(ch)
            if col: c.px(x0 + xx, y1 - len(rows) + 1 + yy, col)


def pickets(c, x0, x1, top):
    """vertical boards with pointed tops and one rail (the left side has a rail fence; this end has pickets)"""
    for x in range(x0, x1, 5):
        c.rect(x, top + 2, x + 3, FLOOR, fill=FACE); c.vl(x, top + 2, FLOOR, OUT); c.vl(x + 3, top + 2, FLOOR, OUT)
        c.px(x + 1, top, OUT); c.px(x + 2, top, OUT); c.px(x, top + 1, OUT); c.px(x + 3, top + 1, OUT); c.px(x + 1, top + 1, HI); c.px(x + 2, top + 1, FACE)
        c.vl(x + 2, top + 3, FLOOR - 1, SH)
    for ry in (top + 8, FLOOR - 6):
        c.rect(x0 - 1, ry, x1, ry + 2, fill=FACE); c.hl(x0 - 1, x1, ry, OUT); c.hl(x0 - 1, x1, ry + 2, OUT); c.hl(x0, x1 - 1, ry + 1, SH)


def leaf(c, cx, cy, ang, ln, wd):
    """one rounded-pointed leaf: cream face, the far half shaded, its own grey edge (so overlaps read)"""
    ca, sa = math.cos(ang), math.sin(ang); cells = {}
    for y in range(int(cy - ln - 2), int(cy + ln + 3)):
        for x in range(int(cx - ln - 2), int(cx + ln + 3)):
            u = (x - cx) * ca + (y - cy) * sa; v = -(x - cx) * sa + (y - cy) * ca
            if 0 <= u <= ln and abs(v) <= wd * math.sin(math.pi * u / ln) ** .7: cells[(x, y)] = v
    for (x, y), v in cells.items(): c.px(x, y, SH if v > .6 else HI if v < -1.2 else FACE)
    for (x, y) in cells:
        if any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): c.px(x, y, OUT)
    for t in range(2, int(ln) - 1):                                       # midrib
        c.px(round(cx + t * ca), round(cy + t * sa), SH)


def leaf_cluster(c, cx, cy, r, n=6, rot=0.0, size=9):
    for k in range(n):
        a = rot + k * 2 * math.pi / n + r.uniform(-.3, .3)
        leaf(c, cx + r.randint(-1, 1), cy + r.randint(-1, 1), a, size + r.randint(-2, 1), 2.6 + r.random())


def vine(c, r):
    """a vine falling from the high step down the wall face, clusters along it"""
    pts = [(20, 8), (24, 20), (22, 34), (28, 48), (26, 62), (32, 76)]
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        for t in range(0, 11):
            x = round(xa + (xb - xa) * t / 10); y = round(ya + (yb - ya) * t / 10); c.px(x, y, OUT)
    for (x, y), size in zip(pts, (10, 8, 10, 8, 9, 8)):
        leaf_cluster(c, x, y, r, n=r.choice([5, 6]), rot=r.uniform(0, 6.28), size=size)
    for x, y in ((98, 90), (8, 118)):
        leaf_cluster(c, x, y, r, n=6, rot=r.uniform(0, 6.28), size=9)


def draw():
    r = random.Random(11)
    c = Canvas(CW, CH)
    wall(c, r); jamb(c)
    ladder(c, 54, 66, 40)
    crate(c, 92, FLOOR, 18, 15, cross=True); crate(c, 96, FLOOR - 15, 12, 10, cross=False)
    sack(c, 80, FLOOR); bucket(c, 64, FLOOR)
    pickets(c, 110, 124, FLOOR - 22)
    vine(c, r)
    return c


if __name__ == '__main__':
    draw().native().save(HERE / 'right-end-stage1.png')
