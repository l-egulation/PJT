"""줄 없이 쌓는 돌 벽(1챕터 정비소 원본 그림처럼 크고 제각각). 가장 낮은 자리부터 크기 다른 돌을 채워 넣어
가로·세로 줄눈이 일자로 이어지지 않는다. 모든 모서리는 둥글게, 아래 줄눈만 진하고 옆 줄눈은 대부분 연하다.
정비소 그림 8색만 쓴다. px.Canvas(2px 점)에 그린다.
"""
import random

from px import h

OUT, FACE, HI, SH, MARK = h('6f6e69'), h('fef7ee'), h('fffcf7'), h('ccc7be'), h('a09d96')
BACK, BACK2, BACK3 = h('f6eee2'), h('e6ddcf'), h('d8cebf')


def pack(width, top_of, seed, wmin=12, wmax=34, hmin=10, hmax=20, base_of=None, ceiling=None):
    """skyline packing from the floor up: returns stones (x0, y0_up, x1, y1_up) in 'height above floor' units.
    top_of(x) is the wall height at column x; every column is filled up to it."""
    r = random.Random(seed)
    level = [base_of(x) if base_of else 0 for x in range(width)]
    stones = []
    while True:
        open_cols = [x for x in range(width) if level[x] < top_of(x)]
        if not open_cols: break
        low = min(level[x] for x in open_cols)
        x0 = next(x for x in open_cols if level[x] == low)
        x1 = x0
        while x1 + 1 < width and level[x1 + 1] == low and level[x1 + 1] < top_of(x1 + 1): x1 += 1
        run = x1 - x0 + 1
        left = level[x0 - 1] if x0 > 0 else 10 ** 6
        right = level[x1 + 1] if x1 + 1 < width else 10 ** 6
        if run < 2 * wmin and left > low and right > low:
            # a narrow pit between taller stones: one long stone up to the lower neighbour (no stacked column of equal stones)
            w = run; hh = min(left, right) - low
            if hh > 2 * hmax: hh = r.randint(hmin, hmax)
        else:
            w = r.randint(wmin, wmax)
            if run - w < wmin: w = run if run <= wmax + wmin else run - r.randint(wmin, wmax)
            w = max(1, min(w, run))
            hh = r.randint(hmin, hmax)
            # never let the new top line up with the stone beside it (that is how coursed rows appear)
            for _ in range(4):
                top = low + hh
                if (x0 > 0 and abs(level[x0 - 1] - top) <= 1) or (x0 + w < width and abs(level[x0 + w] - top) <= 1): hh += r.choice([3, -3]); hh = max(hmin - 2, hh)
                else: break
        if ceiling is not None: hh = max(1, min(hh, ceiling - low))                    # never past the canvas top (no clipped stones)
        stones.append((x0, low, x0 + w - 1, low + hh - 1))
        for x in range(x0, x0 + w): level[x] = low + hh
    return stones


def paint(c, stones, floor_row, inside, x_off=0, seed=0, face_alt=.2):
    """draw packed stones onto canvas rows (floor_row is the canvas row of height 0), clipped by inside(x, y)"""
    r = random.Random(seed)
    for sx0, h0, sx1, h1 in stones:
        top, bot = floor_row - h1, floor_row - h0
        size = r.choice([2, 2, 3]); deep = {k for k in 'abcd' if r.random() < .3}
        face = BACK if r.random() < face_alt else FACE
        head = OUT if r.random() < .2 else MARK
        for yy in range(top, bot + 1):
            for xx in range(sx0 + x_off, sx1 + x_off + 1):
                if not inside(xx, yy): continue
                dx0, dx1, dy0, dy1 = xx - sx0 - x_off, sx1 + x_off - xx, yy - top, bot - yy
                cut = None
                for k, a, b in (('a', dx0, dy0), ('b', dx1, dy0), ('c', dx0, dy1), ('d', dx1, dy1)):
                    if a + b < (size + 1 if k in deep else size): cut = k
                if cut: col = MARK if cut in 'ab' else OUT
                elif dy1 == 0: col = OUT
                elif dx1 == 0: col = head
                elif dy0 == 0: col = HI
                elif dy1 == 1 or dx1 == 1: col = SH
                else: col = face
                c.px(xx, yy, col)
        w_, h_ = sx1 - sx0, bot - top
        if r.random() < .3 and w_ > 12 and h_ > 7:
            mx, my = sx0 + x_off + r.randint(4, w_ - 8), top + r.randint(3, h_ - 3)
            for i in range(2):
                for dx in (0, 2):
                    if inside(mx + dx + i, my - i): c.px(mx + dx + i, my - i, SH)
        elif r.random() < .25 and w_ > 10 and h_ > 8:
            cx, cy = sx0 + x_off + r.randint(3, w_ - 3), bot - 1
            for i in range(r.randint(3, 6)):
                if inside(cx + (i % 2), cy - i): c.px(cx + (i % 2), cy - i, MARK)


def outline(c, inside, x0, y0, x1, y1):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and any(not inside(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, -1))): c.px(x, y, OUT)


def inner_wall(c, x0, x1, top, floor_row, seed):
    """the cream inside wall: the same uncoursed packing, drawn only as faint joints"""
    for y in range(top, floor_row + 1):
        for x in range(x0, x1 + 1): c.px(x, y, BACK)
    for sx0, h0, sx1, h1 in pack(x1 - x0 + 1, lambda x: floor_row - top + 1, seed, 14, 40, 12, 22):
        ty, by = floor_row - h1, floor_row - h0
        for x in range(sx0 + x0, sx1 + x0 + 1):
            if top <= by <= floor_row: c.px(x, by, BACK2)
        for y in range(max(top, ty), by + 1): c.px(min(x1, sx1 + x0), y, BACK2)


def paint_union(c, stones, floor_row, seed=0, face_alt=.2, x_off=0):
    """draw stones unclipped; the wall's outline is the stones' own outline (their rounded corners included).
    Chipped corners inside the wall show a light joint (no dark knots where four stones meet); on the outside they are cut away."""
    r = random.Random(seed)
    owner = {}
    for i, (sx0, h0, sx1, h1) in enumerate(stones):
        for yy in range(floor_row - h1, floor_row - h0 + 1):
            for xx in range(sx0 + x_off, sx1 + x_off + 1): owner[(xx, yy)] = i
    chips = set()
    for i, (sx0, h0, sx1, h1) in enumerate(stones):
        top, bot = floor_row - h1, floor_row - h0
        size = r.choice([1, 1, 1, 2]); deep = {k for k in 'abcd' if r.random() < .12}
        face = BACK if r.random() < face_alt else FACE
        head = OUT if r.random() < .12 else MARK
        for yy in range(top, bot + 1):
            for xx in range(sx0 + x_off, sx1 + x_off + 1):
                dx0, dx1, dy0, dy1 = xx - sx0 - x_off, sx1 + x_off - xx, yy - top, bot - yy
                cut = any(a + b < (size + 1 if k in deep else size)
                          for k, a, b in (('a', dx0, dy0), ('b', dx1, dy0), ('c', dx0, dy1), ('d', dx1, dy1)))
                if cut: chips.add((xx, yy, dy0 < dy1)); continue
                if dy1 == 0: col = OUT
                elif dx1 == 0: col = head
                elif dy0 == 0: col = HI
                elif dy1 == 1 or dx1 == 1: col = SH
                else: col = face
                c.px(xx, yy, col)
        w_, h_ = sx1 - sx0, bot - top
        if r.random() < .35 and w_ > 14 and h_ > 9:
            mx, my = sx0 + x_off + r.randint(5, w_ - 9), top + r.randint(4, h_ - 4)
            for k in range(2):
                for dx in (0, 2): c.px(mx + dx + k, my - k, SH)
        elif r.random() < .3 and w_ > 12 and h_ > 10:
            cx, cy = sx0 + x_off + r.randint(4, w_ - 4), bot - 1
            for k in range(r.randint(3, 6)): c.px(cx + (k % 2), cy - k, MARK)
    exterior = lambda p: p not in owner
    for (xx, yy, upper) in chips:
        # only a top corner on the outside is cut away (rounded silhouette); every other worn corner is a pale joint
        if upper and any(exterior((xx + dx, yy + dy)) for dx, dy in ((1, 0), (-1, 0), (0, -1), (-1, -1), (1, -1))):
            owner.pop((xx, yy), None); c.px(xx, yy, (0, 0, 0, 0))
        else:
            c.px(xx, yy, SH)
    for (xx, yy) in list(owner):
        if any(exterior((xx + dx, yy + dy)) for dx, dy in ((1, 0), (-1, 0), (0, -1))) and yy < floor_row: c.px(xx, yy, OUT)
    return owner
