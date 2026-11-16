"""Boss room tower, traced block by block from the reference painting (monumental a-arena, right tower)
on the 2x2-dot grid, in colours sampled from it. Background art: light joint lines, no dark outline,
flat faces with a lit top edge and a shaded bottom edge per stone. 121 x 243 dots; the left tower mirrors it."""
import math
from px import *
from pxref import shift

F = (222, 208, 196, 255); HI = (233, 221, 210, 255); MID = (205, 191, 180, 255); WIN = (199, 186, 175, 255)
SIDE = (176, 163, 154, 255); DARK = (130, 122, 116, 255); BLK = (190, 178, 168, 255); BLK_HI = (202, 190, 180, 255); BLK_LO = (178, 166, 157, 255); SIDE_LO = (163, 150, 141, 255); DOOR = (139, 130, 123, 255); DOOR_LO = (127, 119, 113, 255)
J = (174, 162, 152, 255)          # joints on light faces
JD = (142, 132, 124, 255)         # joints on shaded faces
W_, H_ = 121, 243


def block(c, x0, y0, x1, y1, face, line=None, lit=True):
    """a stone: flat face, one-dot joint frame, lit top/left inner edge, shaded bottom/right inner edge"""
    line = line or (J if face in (F, HI, MID, WIN) else JD)
    c.rect(x0, y0, x1, y1, fill=face)
    if lit and x1 - x0 > 3 and y1 - y0 > 3:
        c.hl(x0 + 1, x1 - 1, y0 + 1, shift(face, 9)); c.vl(x0 + 1, y0 + 2, y1 - 1, shift(face, 5))
        c.hl(x0 + 2, x1 - 1, y1 - 1, shift(face, -8))
    c.hl(x0, x1, y0, line); c.hl(x0, x1, y1, line); c.vl(x0, y0, y1, line); c.vl(x1, y0, y1, line)
    if x1 - x0 > 5 and y1 - y0 > 5:                      # rounded stone corners, like the painting's chunky blocks
        for x, y in ((x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)): c.px(x, y, line)


def course(c, y0, y1, x0, x1, cuts, face):
    xs = [x0] + cuts + [x1]
    for a, b in zip(xs, xs[1:]): block(c, a, y0, b, y1, face)


_CRACKS = []


def crack(c, pts, col=None):
    """queue a crack; draw_cracks() draws them last so later clean-up passes cannot break them"""
    _CRACKS.append((c, pts, col))


def draw_cracks():
    """natural hairlines: a continuous walk (one dot per step, straight or diagonal, never an L corner)
    that follows the guide loosely, with one short side branch; the last two dots are a step lighter"""
    import random
    for c, pts, col in _CRACKS:
        col = col or J
        rng = random.Random(hash(tuple(pts)) & 0xffff)
        def ok(x, y):
            p = c.get(x, y)
            if not p[3] or p in (J, JD, DARK, DOOR, DOOR_LO): return False
            # keep a dot of stone between a crack and a joint line, so they never merge into a clump
            return not any(c.get(x + dx, y + dy) in (J, JD) and (x + dx, y + dy) not in drawn for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        drawn = set()
        path = [tuple(pts[0])]
        for (x1, y1) in pts[1:]:
            x, y = path[-1]
            drift = 0
            while (x, y) != (x1, y1):
                dx, dy = x1 - x, y1 - y
                sx = (dx > 0) - (dx < 0); sy = (dy > 0) - (dy < 0)
                if abs(dy) >= abs(dx):                       # mostly vertical: always step y, wander in x
                    if rng.random() < .22 and abs(dx) < abs(dy) - 1: drift = rng.choice((-1, 0, 1))
                    else: drift = sx
                    x, y = x + drift, y + sy
                else:
                    if rng.random() < .22 and abs(dy) < abs(dx) - 1: drift = rng.choice((-1, 0, 1))
                    else: drift = sy
                    x, y = x + sx, y + drift
                path.append((x, y))
        n = len(path)
        for k, (x, y) in enumerate(path):
            if ok(x, y): c.px(x, y, col if 1 <= k < n - 2 else shift(col, 10)); drawn.add((x, y))
        if n > 9:
            bx, by = path[rng.randrange(n // 3, 2 * n // 3)]
            sx = rng.choice((-1, 1))
            for k in range(3):
                bx, by = bx + sx, by + (1 if k != 1 else 0)
                if ok(bx, by): c.px(bx, by, col if k < 2 else shift(col, 10)); drawn.add((bx, by))
    _CRACKS.clear()

def chip(c, x, y, sx, sy):
    """a broken stone corner: a small stepped notch cut back to shadow, with a lit broken face"""
    pts = [(0, 0), (1, 0), (2, 0), (3, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2), (0, 3)]
    for dx, dy in pts: c.px(x + dx * sx, y + dy * sy, shift(c.get(x + dx * sx, y + dy * sy), -30))
    for dx, dy in ((4, 0), (3, 1), (2, 2), (1, 3), (0, 4)): c.px(x + dx * sx, y + dy * sy, J)
    for dx, dy in ((3, 2), (2, 3)): c.px(x + dx * sx, y + dy * sy, shift(HI, 6))


def rubble(c, x, y, w, h, face):
    """a fallen rock at the foot: rounded, lit upper-left, shaded lower-right, one-dot outline"""
    from px import ellipse_mask
    m = ellipse_mask(x, y, x + w - 1, y + h - 1)
    for px_, py_ in m: c.px(px_, py_, face)
    for px_, py_ in m:
        rx, ry = (px_ - x) / max(1, w - 1), (py_ - y) / max(1, h - 1)
        if rx + ry < .7: c.px(px_, py_, shift(face, 12))
        elif rx + ry > 1.35: c.px(px_, py_, shift(face, -14))
    for px_, py_ in m:
        if any((px_ + dx, py_ + dy) not in m for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): c.px(px_, py_, JD)


def streak(c, x, y0, y1, w=2):
    """an ink stain running down a face from the bracket drips, fading out"""
    for y in range(y0, y1):
        fade = (y - y0) / max(1, y1 - y0)
        for k in range(w if fade < .6 else 1):
            p = c.get(x + k, y)
            if p[3] and p not in (J, JD): c.px(x + k, y, shift(p, -round(20 * (1 - fade)) - 4))


def draw():
    c = Canvas(W_, H_)
    # back mass (the tower's deeper body, in shade)
    for i, (y0, y1) in enumerate(((0, 58), (58, 116), (116, 161))):
        course(c, y0, y1, 98, 120, [], SIDE)
    # top beam running left over the corbels, and the tower's top blocks
    course(c, 0, 23, 0, 47, [20, 38], MID)
    course(c, 0, 16, 47, 98, [72], F)
    crack(c, [(8, 6), (11, 10), (10, 15), (13, 18)]); crack(c, [(28, 8), (31, 12)]); crack(c, [(55, 3), (58, 7)])
    # bracket under the beam (traced from the painting): a deep shadow mass with three lit stones stepping
    # down toward the tower, and ink drips hanging from its lower edge
    shadow = [(0, 24), (47, 24), (47, 106), (45, 106), (43, 88), (40, 79), (36, 74), (31, 71), (27, 67), (24, 61), (21, 56), (17, 51), (14, 46), (10, 42), (5, 40), (0, 39)]
    c.poly(shadow, fill=DARK)
    for cx_, cy_, r_ in ((8, 39, 2.6), (15, 46, 2.8), (21, 55, 2.4), (27, 66, 2.8), (35, 73, 2.5)):   # soft ink bulges
        for yy in range(int(cy_ - r_) - 1, int(cy_ + r_) + 2):
            for xx in range(int(cx_ - r_) - 1, int(cx_ + r_) + 2):
                if (xx - cx_) ** 2 + (yy - cy_) ** 2 <= r_ * r_: c.px(xx, yy, DARK)
    for poly in ([(16, 26), (43, 26), (43, 39), (22, 39), (16, 33)],
                 [(26, 41), (44, 41), (44, 53), (31, 53), (26, 48)],
                 [(34, 56), (44, 56), (44, 67), (40, 67), (34, 61)]):
        c.poly(poly, fill=BLK)
        ys = [p[1] for p in poly]; xs = [p[0] for p in poly]
        for x in range(min(xs), max(xs) + 1):              # lit top edge
            for y in range(min(ys), max(ys)):
                if c.get(x, y) == BLK: c.px(x, y, BLK_HI); break
        for y in range(min(ys), max(ys) + 1):              # shaded bottom row
            for x in range(min(xs), max(xs) + 1):
                if c.get(x, y) == BLK and c.get(x, y + 1) == DARK: c.px(x, y, BLK_LO)
    c.hl(0, 47, 24, JD)
    for x, y0, y1 in ((29, 27, 38), (37, 42, 52)):          # joints splitting the bracket stones
        c.vl(x, y0, y1, DARK)
    c.poly([(5, 26), (15, 26), (15, 33), (8, 33), (5, 30)], fill=BLK_LO)   # small stone tucked under the beam
    # drips: (x, length) along the shadow's lower edge, two dots wide where they are long, with a round tip
    from PIL import Image as _I, ImageDraw as _D
    sm = _I.new('L', (W_, H_), 0); _D.Draw(sm).polygon(shadow, fill=1)
    for x_ in range(W_):
        for y_ in range(H_):
            if c.get(x_, y_) == DARK: sm.putpixel((x_, y_), 1)
    def edge_y(x):
        y = 24
        while y < 110 and sm.getpixel((x, y + 1)): y += 1
        return y
    for x, n in ((9, 3), (22, 4), (27, 9), (30, 3), (33, 12), (37, 5), (40, 15), (43, 18), (45, 9)):
        y0 = edge_y(x) + 1
        wide = n >= 8
        for k in range(n):
            c.px(x, y0 + k, DARK)
            if wide and k < n - 3: c.px(x + 1, y0 + k, DARK)
        if wide: c.px(x + 1, y0 + n - 1, DARK)            # bulb at the tip
    # capital: a thick octagonal slab (lit top face, front face), a dark recess, then three rounded dentils
    def octo(y0, y1, x0, x1, cut, top_face, front_face, split):
        for y in range(y0, y1 + 1):
            k = min(cut, y - y0, y1 - y)
            a, b = x0 + cut - k, x1 - cut + k
            c.hl(a, b, y, front_face)
            c.px(a, y, J); c.px(b, y, J)
            if y > y0 and y < y1 and k < cut:                               # chamfered ends turn into shade
                c.px(a + 1, y, shift(front_face, -10)); c.px(b - 1, y, shift(front_face, -10))
        c.hl(x0 + cut, x1 - cut, y0, J); c.hl(x0 + cut, x1 - cut, y1, J)
        c.hl(x0 + cut + 1, x1 - cut - 1, y0 + 1, shift(front_face, 12))     # lit top lip
        c.hl(x0 + cut, x1 - cut, y0 + 2, shift(front_face, 6))
        c.hl(x0 + 2, x1 - 2, y1 - 1, shift(front_face, -9))                 # shaded underside
        for x in split: c.vl(x, y0 + 1, y1 - 1, J)
    octo(16, 31, 40, 109, 5, HI, F, (58, 76, 93))
    crack(c, [(60, 18), (63, 20), (62, 22)]); crack(c, [(88, 25), (91, 28)])
    for y in range(32, 42):                                                 # recess under the slab, narrowing
        k = min(2, (y - 32) // 3)
        c.hl(44 + k, 106 - k, y, DARK)
    for x0, x1, face in ((48, 61, SIDE), (63, 79, F), (81, 96, F), (98, 103, SIDE)):
        for y in range(33, 41):
            for x in range(x0, x1 + 1):
                corner = (x in (x0, x1)) and (y in (33, 40))
                if not corner: c.px(x, y, face)
        c.hl(x0 + 1, x1 - 1, 33, shift(face, 9)); c.hl(x0 + 1, x1 - 1, 40, shift(face, -10))
        c.vl(x0, 34, 39, shift(face, 4))
    # shaft: front face, right side face, slim left shadow
    course(c, 41, 104, 48, 92, [], F)
    course(c, 104, 161, 48, 92, [], F)
    for y in range(42, 162):
        for x in range(93, 98): c.px(x, y, SIDE)
        c.px(97, y, JD)
        for x in range(44, 48): c.px(x, y, SIDE if y > 106 else c.get(x, y))
    c.hl(93, 97, 104, JD)
    crack(c, [(54, 48), (57, 51), (55, 56)]); crack(c, [(57, 51), (60, 52)]); crack(c, [(64, 140), (67, 143), (66, 148)]); crack(c, [(108, 30), (111, 34)], JD); crack(c, [(103, 120), (106, 124), (104, 128)], JD); crack(c, [(86, 95), (89, 99), (88, 104)]); crack(c, [(53, 133), (56, 137)])
    # slit window: round head, three panes
    win = {(x, y) for x in range(69, 81) for y in range(56, 151) if y >= 61 or math.hypot(x - 74.5, y - 61) <= 5.6}
    for x, y in win: c.px(x, y, WIN)
    for x, y in win:
        if any((x + dx, y + dy) not in win for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): c.px(x, y, J)
    for y in (88, 117): c.hl(70, 79, y, J)
    for x, y in win:
        if (x - 1, y) not in win and (x, y) in win and c.get(x + 1, y) == WIN: c.px(x + 1, y, shift(WIN, -10))
    # ledge: a thick slab with a chamfered end, lit top face and shaded underside
    for y in range(160, 171):
        k = min(4, y - 160)
        a = 44 - k
        c.hl(a, 120, y, HI if y < 164 else F)
        c.px(a, y, J)
    c.hl(44, 120, 160, J); c.hl(40, 120, 164, J); c.hl(40, 120, 170, J)
    c.hl(45, 120, 161, shift(HI, 8)); c.hl(41, 120, 165, shift(F, 8)); c.hl(42, 120, 169, shift(F, -10))
    for x in (66, 98): c.vl(x, 164, 170, J)
    # lower left pilaster with its shaded side, and the plinth
    course(c, 171, 205, 48, 72, [], F); course(c, 205, 228, 48, 72, [], F)
    for y in range(171, 228):
        for x in range(41, 48): c.px(x, y, SIDE)
        c.px(41, y, JD)
    c.hl(41, 47, 205, JD)
    block(c, 38, 228, 73, 242, F); c.hl(39, 72, 229, shift(HI, 4))
    crack(c, [(55, 196), (58, 200), (57, 205)])
    # door wall: big blocks, a ring of thick voussoirs with a keystone, columned jambs, deep opening
    course(c, 171, 189, 73, 120, [], F)
    for y in range(189, H_): c.hl(73, 120, y, F)
    c.hl(73, 120, 189, J)
    cx, cy, ro, ri = 95, 208, 24, 16
    ring = {(x, y) for y in range(cy - ro, cy + 1) for x in range(cx - ro, cx + ro + 1) if ri < math.hypot(x - cx, y - cy) <= ro}
    for x, y in ring:                                   # broad light: lit on the left, shaded on the right, deep soffit inside
        ang = math.atan2(cy - y, x - cx)
        tone = HI if ang > math.pi * .62 else F if ang > math.pi * .30 else shift(F, -13)
        if math.hypot(x - cx, y - cy) < ri + 2.3: tone = shift(tone, -15)
        c.px(x, y, tone)
    key = {(x, y) for x, y in ring if math.pi * .43 < math.atan2(cy - y, x - cx) < math.pi * .57}
    for y in range(cy - ro - 2, cy - ro + 1):          # keystone rises two dots above the ring
        for x in range(cx - 3, cx + 4): key.add((x, y))
    for x, y in key: c.px(x, y, shift(HI, 8) if y < cy - ri - 3 else HI)
    for f in (.2, .32, .68, .8):                        # four clean voussoir joints
        a_ = math.pi * f
        c.line([(cx + (ri + 1) * math.cos(a_), cy - (ri + 1) * math.sin(a_)), (cx + ro * math.cos(a_), cy - ro * math.sin(a_))], J)
    shape = ring | key
    for x, y in shape:                                  # crisp outline: outer edge J, keystone sides J
        if any((x + dx, y + dy) not in shape for dx, dy in ((1, 0), (-1, 0), (0, -1))) and math.hypot(x - cx, y - cy) > ri + 1:
            c.px(x, y, J)
    for x, y in key:
        if (x - 1, y) not in key or (x + 1, y) not in key: c.px(x, y, J)
    de_elbow(c, J)
    for xa, xb in ((cx - ro, cx - ri - 1), (cx + ri + 1, cx + ro)):   # jambs sit exactly under the arch ring
        c.rect(xa - 1, cy - 3, xb + 1, cy, fill=HI)                     # capital, one dot wider each side
        c.hl(xa - 1, xb + 1, cy - 3, J); c.hl(xa - 1, xb + 1, cy, J); c.vl(xa - 1, cy - 3, cy, J); c.vl(xb + 1, cy - 3, cy, J)
        c.hl(xa, xb, cy - 2, shift(HI, 8))
        for y in range(cy + 1, 238):                                    # column: lit left, shaded right
            c.hl(xa + 1, xb - 1, y, F); c.px(xa + 1, y, shift(F, 9)); c.px(xb - 1, y, shift(F, -11))
            c.px(xa, y, J); c.px(xb, y, J)
        c.rect(xa - 1, 238, xb + 1, H_ - 1, fill=MID); c.hl(xa - 1, xb + 1, 238, J); c.vl(xa - 1, 238, H_ - 1, J); c.vl(xb + 1, 238, H_ - 1, J)
    for y in range(cy - ri, H_):                        # deep opening, darker under the arch
        for x in range(cx - ri, cx + ri + 1):
            if (y > cy and cx - ri <= x <= cx + ri) or (y <= cy and math.hypot(x - cx, y - cy) <= ri):
                c.px(x, y, DOOR_LO if math.hypot(x - cx, y - cy) > ri - 3 and y < cy + 2 else DOOR)
    c.vl(120, 171, H_ - 1, J)
    # the painting's own light and shade inside each stone
    from PIL import Image
    from pxref import add_shading
    SRC = r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/map-redesign-research-20261110/resources/environments/stage1-clockroom-monumental-20261111/preview/a-arena.png"
    src = Image.open(SRC).convert("RGBA").crop((1250, 0, 1672, 848))
    add_shading(c, [F, HI, MID, SIDE, SIDE_LO], src, step=8, levels=2, gain=2.5, clean=3, smooth=7)
    # ---- final details (after the shading so they stay crisp)
    for x, y, sx, sy in ((49, 42, 1, 1), (91, 83, -1, 1), (49, 160, 1, -1), (71, 172, -1, 1), (98, 59, 1, 1), (119, 117, -1, 1), (73, 190, 1, 1)):
        chip(c, x, y, sx, sy)
    crack(c, [(84, 128), (86, 133), (84, 139), (86, 145)], JD); crack(c, [(86, 133), (90, 135)], JD)
    crack(c, [(52, 215), (55, 219), (54, 224)], JD); crack(c, [(104, 60), (107, 66), (105, 72)], JD)
    streak(c, 44, 107, 150, 2); streak(c, 94, 42, 75, 1); streak(c, 101, 162, 185, 2)
    declump(c, J, F); declump(c, JD, SIDE)
    draw_cracks()
    lint(c, colors=[J, JD], name='tower-traced')
    from pxref import light_temperature
    light_temperature(c)
    return c


if __name__ == '__main__':
    draw().native().save('out/tower-traced.png')
