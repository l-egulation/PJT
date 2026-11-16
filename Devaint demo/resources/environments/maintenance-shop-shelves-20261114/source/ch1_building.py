"""1챕터 정비소 건물 전체를 한 번에 새로 그린다(왼쪽 포함, 이어 붙인 자국 없음).
돌은 줄 없이 쌓는다(stones.py). 넓은 문 하나에 정비공 자리와 진열 구역이 이어진다.
정비소 그림 8색, 연회색 외곽선, 2px 점.

    python ch1_building.py <워크트리 루트>
"""
import os
import random
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1])
PXDIR = ROOT / 'resources/environments/stage1-pixel-structures-20261112/source'
sys.path.insert(0, str(PXDIR)); sys.path.insert(0, str(HERE))
from px import Canvas  # noqa: E402
import stones as st  # noqa: E402
import right_end as re_  # noqa: E402
OUT, FACE, HI, SH, MARK, BACK, BACK2, BACK3 = st.OUT, st.FACE, st.HI, st.SH, st.MARK, st.BACK, st.BACK2, st.BACK3

EXTRA = int(os.environ.get('SHOP_EXTRA_DOTS', '0'))   # a longer display bay (more goods at the same spacing)
BW, BH = 500 + EXTRA, 166             # 10 rows of headroom for the grass and flowers on the top stones
FLOOR = BH - 1
O0, O1 = 130, 378 + EXTRA            # the one wide opening (shop + display)
SPLIT = 212                  # where the shop corner ends and the display begins (no wall, just the cloth's end)
OH = 91                      # opening height above the floor (182 px)
PROFILE = [(0, 22), (18, 40), (40, 60), (62, 80), (84, 98), (104, 110), (124, 118), (196, 129), (233, 118), (298, 123),
           (350, 115), (385, 100), (405, 82), (425, 64), (445, 46), (463, 28), (478, 0)]
PROFILE = [(x0 + (EXTRA if x0 >= 298 else 0), hh) for x0, hh in PROFILE]


def top_of(x):
    t = 0
    for x0, hh in PROFILE:
        if x >= x0: t = hh
    return t


def wall_inside(x, y):
    if not (0 <= x < BW and 0 <= y <= FLOOR): return False
    up = FLOOR - y
    if up >= top_of(x): return False
    if O0 <= x <= O1 and up < OH: return False
    return True


def beam(c):
    y = FLOOR - OH - 4; xe = SIGN_X + 9                       # the lintel runs on out of the wall; the sign hangs off its end
    c.rect(O0 - 6, y, xe, y + 4, fill=FACE); c.hl(O0 - 6, xe, y, OUT); c.hl(O0 - 6, xe, y + 4, OUT)
    c.vl(O0 - 6, y, y + 4, OUT); c.vl(xe, y + 1, y + 3, OUT); c.px(xe, y, (0, 0, 0, 0)); c.px(xe, y + 4, (0, 0, 0, 0))
    c.px(xe - 1, y, OUT); c.px(xe - 1, y + 4, OUT); c.hl(O0 - 5, xe - 2, y + 1, HI); c.hl(O0 - 5, xe - 2, y + 3, SH); c.px(xe - 1, y + 2, MARK)
    c.px(SIGN_X, y + 5, OUT)                                                                                 # hook ring
    for x in range(O0 + 14, O1, 31): c.px(x, y + 2, OUT); c.px(x + 1, y + 2, MARK)                          # pegs
    for x in (O0 + 70, O0 + 140, O0 + 200): c.vl(x, y + 1, y + 3, MARK)                                    # plank joins
    for bx, sgn in ((O0, 1), (O1, -1)):                                                                     # corner braces
        for i in range(12):
            xx = bx + sgn * i; yy = y + 5 + i
            c.px(xx, yy, OUT); c.px(xx + sgn, yy, FACE); c.px(xx + 2 * sgn, yy, OUT)


def awning(c):
    """the shop's draped cloth over its corner, torn bottom edge"""
    x0, x1, y = O0 + 34, SPLIT + 8, FLOOR - OH - 1
    bottoms = []
    r = random.Random(4)
    for i, x in enumerate(range(x0, x1 + 1)):
        t = (x - x0) / (x1 - x0)
        b = y + 6 + round(3 * (1 - (2 * t - 1) ** 2)) + (r.choice([0, 1]) if t > .55 else 0)
        bottoms.append(b)
        for yy in range(y, b + 1): c.px(x, yy, HI if yy < y + 3 else FACE)
        c.px(x, b, OUT)
    for x, b in zip(range(x0, x1 + 1), bottoms):
        if (x - x0) % 13 == 6:
            for yy in range(y + 3, b - 1): c.px(x, yy, SH)
    c.vl(x0, y, bottoms[0], OUT); c.vl(x1, y, bottoms[-1], OUT)


def barrel(c, x0, y1):
    rows = ['..DDDDDDDDDD..', '.DSMMMMMMMMSD.', 'DFHFFFFFFFFFSD', 'DMMMMMMMMMMMMD', 'DFHFFFFFFFFFSD', 'DFHFDFFFDFFFSD', 'DFHFDFFFDFFFSD',
            'DFHFDFFFDFFFSD', 'DMMMMMMMMMMMMD', 'DFHFDFFFDFFFSD', 'DFHFDFFFDFFFSD', 'DFHFDFFFDFFFSD', 'DMMMMMMMMMMMMD', 'DFHFFFFFFFFFSD',
            '.DSFFFFFFFFSD.', '..DDDDDDDDDD..']
    for yy, row in enumerate(rows):
        for xx, ch in enumerate(row):
            col = {'D': OUT, 'F': FACE, 'H': HI, 'S': SH, 'M': MARK}.get(ch)
            if col: c.px(x0 + xx, y1 - len(rows) + 1 + yy, col)


def rail_fence(c, x0, x1, top):
    for px_ in (x0, x1 - 3):
        c.rect(px_, top, px_ + 3, FLOOR, fill=FACE); c.vl(px_, top + 1, FLOOR, OUT); c.vl(px_ + 3, top + 1, FLOOR, OUT); c.hl(px_ + 1, px_ + 2, top, OUT)
        c.vl(px_ + 1, top + 1, FLOOR - 1, HI); c.px(px_ + 1, top + 4, OUT); c.px(px_ + 1, FLOOR - 4, OUT)
    for ry in (top + 5, top + 14):
        c.rect(x0 + 4, ry, x1 - 4, ry + 3, fill=FACE); c.hl(x0 + 4, x1 - 4, ry, OUT); c.hl(x0 + 4, x1 - 4, ry + 3, OUT); c.hl(x0 + 4, x1 - 4, ry + 2, SH)
        for x in range(x0 + 8, x1 - 6, 7): c.px(x, ry + 1, MARK); c.px(x + 1, ry + 1, MARK)


def garland(c, r, x, y, n, step=8):
    """a hanging vine: leaf clusters down a stem, the original painting's vine"""
    for i in range(n * step): c.px(x + (1 if (i // 5) % 2 else 0), y + i, OUT)
    for i in range(n):
        re_.leaf_cluster(c, x + r.randint(-1, 1), y + i * step + 3, r, n=r.choice([4, 5]), rot=r.uniform(0, 6.28), size=r.choice([7, 8, 9]))


SIGN_ARM = FLOOR - OH + 1      # first row under the lintel; the sign hangs from there
SIGN_X = O1 + 26              # sign centre (dots)


def sign_bracket(c, x, y):
    """a wooden arm out of the wall over the sign, a thick brace under it, an iron ring the sign's hook hangs on"""
    x1 = SIGN_X + 6
    c.rect(x, y, x1, y + 3, fill=FACE); c.hl(x, x1, y, OUT); c.hl(x, x1, y + 3, OUT); c.vl(x1, y, y + 3, OUT)
    c.hl(x + 1, x1 - 1, y + 1, HI); c.hl(x + 1, x1 - 1, y + 2, FACE)
    c.px(x1 - 1, y + 1, MARK)                                                          # end grain
    for i in range(11):                                                                # brace: wall foot -> under the arm
        xx, yy = x + 1 + i, y + 15 - i
        c.px(xx, yy, OUT); c.px(xx + 1, yy, FACE); c.px(xx + 2, yy, OUT)
    c.px(x + 1, y + 16, OUT); c.px(x + 2, y + 16, OUT); c.px(x + 3, y + 16, OUT)
    c.px(SIGN_X, y + 4, OUT)                                                           # ring the hook takes


def tuft(c, x, y, r):
    """a grass tuft standing on row y (its foot)"""
    for k, (dx, hh) in enumerate(((0, 2), (1, 3), (2, 4), (3, 3), (4, 2))):
        if r.random() < .2 and k not in (1, 2): continue
        lean = 1 if dx > 2 else -1 if dx < 2 else 0
        for i in range(hh): c.px(x + dx + (lean if i == hh - 1 else 0), y - i, OUT if (i == 0 or k % 2 == 0) else MARK)


def flower(c, x, y):
    """a small daisy on a stem, foot on row y"""
    for i in range(3): c.px(x, y - i, OUT)
    c.px(x + 1, y - 1, OUT)                                                            # leaf
    cy = y - 5
    for dx, dy in ((0, -2), (-1, -1), (1, -1), (-2, 0), (2, 0), (-1, 1), (1, 1), (0, 2)): c.px(x + dx, cy + dy, OUT)
    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)): c.px(x + dx, cy + dy, HI)
    c.px(x, cy, MARK)


def plants(c, owner, r):
    """grass and a few flowers here and there on the top stones (only where the top runs flat for a while)"""
    top = {}
    for (x, y) in owner: top[x] = min(top.get(x, 10 ** 6), y)
    spots, x = [], 2
    while x < BW - 8:
        flat = all(top.get(x + k) == top.get(x) for k in range(7)) and top.get(x) is not None
        if flat and not (O0 - 16 <= x <= O0 + 4) and r.random() < .16: spots.append(x); x += r.randint(18, 34)
        else: x += 1
    for i, x in enumerate(spots):
        y = top[x] - 1
        if i % 3 == 1: flower(c, x + 2, y); tuft(c, x + 4, y, r)
        else: tuft(c, x, y, r)


def draw():
    c = Canvas(BW, BH); r = random.Random(21)
    # cream inside wall of the opening, packed the same way but only as faint joints
    st.inner_wall(c, O0, O1, FLOOR - OH + 1, FLOOR, seed=5)
    for y in range(FLOOR - OH + 1, FLOOR - OH + 5):                                       # soft shade under the beam
        for x in range(O0, O1 + 1):
            if y < FLOOR - OH + 3 or (x + y) % 2 == 0: c.px(x, y, BACK3)
    # the stone mass, uncoursed, filled from the floor up
    # bigger stones; the wall's outline is whatever the top stones make (no cut profile), the opening is left by starting
    # those columns at the beam
    # three sections filled on their own (left wall, over the opening, right wall) so no sliver is left where their heights differ
    stones = []
    for a, b, base, seed in ((0, O0 - 1, 0, 13), (O0, O1, OH + 5, 14), (O1 + 1, BW - 1, 0, 15)):
        part = st.pack(b - a + 1, lambda x, a=a: top_of(x + a), seed=seed, wmin=18, wmax=46, hmin=14, hmax=26, base_of=lambda x, base=base: base, ceiling=154)
        stones += [(x0 + a, h0, x1 + a, h1) for x0, h0, x1, h1 in part]
    # drop thin slivers sitting on top (narrow leftovers at a step edge); the outline simply dips there
    def covered(sx0, sx1, h1):
        return any(o[1] == h1 + 1 and o[0] <= sx1 and o[2] >= sx0 for o in stones)
    stones = [t for t in stones if t[2] - t[0] + 1 >= 14 or covered(t[0], t[2], t[3])]
    owner = st.paint_union(c, stones, FLOOR, seed=13)
    for x in range(O0, O1 + 1): c.px(x, FLOOR, OUT)
    beam(c); awning(c)
    # vines like the painting: garlands left of the door and down the right shoulder, tufts at the foot
    garland(c, r, O0 - 12, FLOOR - 112, 6); garland(c, r, 98, FLOOR - 104, 4)
    for x, y in ((62, FLOOR - 74), (446 + EXTRA, FLOOR - 44), (300, FLOOR - 123)): re_.leaf_cluster(c, x, y, r, n=6, rot=r.uniform(0, 6.28), size=9)
    # left props: rail fence, barrel, crate
    rail_fence(c, 0, 40, FLOOR - 24); barrel(c, 42, FLOOR); re_.crate(c, 60, FLOOR, 24, 16, cross=True)
    # right props: ladder, crates and sack, bucket, pickets
    re_.ladder.__globals__['FLOOR'] = FLOOR
    re_.crate.__globals__['FLOOR'] = FLOOR
    re_.ladder(c, 440 + EXTRA, FLOOR - 60, 426 + EXTRA)
    re_.crate(c, 458 + EXTRA, FLOOR, 18, 15, cross=True); re_.crate(c, 461 + EXTRA, FLOOR - 15, 12, 10, cross=False)
    re_.sack(c, 446 + EXTRA, FLOOR); re_.bucket(c, 392 + EXTRA, FLOOR)
    re_.pickets.__globals__['FLOOR'] = FLOOR
    re_.pickets(c, 478 + EXTRA, 498 + EXTRA, FLOOR - 22)
    plants(c, owner, random.Random(31))
    return c


if __name__ == '__main__':
    draw().native().save(HERE / os.environ.get('SHOP_BUILDING', 'building-stage1.png'))
