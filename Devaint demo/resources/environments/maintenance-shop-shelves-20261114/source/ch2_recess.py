"""2챕터 시안: 방 벽(2챕터 램프의 큰 벽돌)을 파낸 벽감으로 정비소와 창고를 짓는다.
벽감 안쪽은 같은 벽돌을 두 단 어둡게, 위 안쪽 면은 그늘, 왼쪽 안쪽 면은 그늘·오른쪽은 빛. 돌 테두리는 따로 두지 않는다.
  separate : 정비소 벽감과 창고 벽감을 따로 판다(사이는 벽)
  merged   : 정비소와 창고를 한 벽감으로 합친다

    python ch2_recess.py <워크트리 루트>
"""
import math
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1])
sys.path.insert(0, str(HERE)); sys.path.insert(0, str(ROOT / 'resources/environments/maintenance-shops-stage2-pixel-20261113/source'))
import mock as m
import pal
from PIL import Image, ImageDraw
from final_scenes import card, stand, weapon_card

C, Wr, INK, STEEL = pal.C, pal.W, pal.INK, pal.STEEL
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
import os
EXTRA = int(os.environ.get('SHOP_EXTRA_PX', '0'))     # a longer display bay (more goods at the same spacing)
W, H = 1000 + EXTRA, 330
CEIL, FLOOR = 34, 34 + 256
X = 330
DARK = [(13, 13, 13), (20, 23, 32), (27, 31, 42)]


def brick_level(x, y, shift=0):
    """room wall: 64x32 bricks in the chapter 2 ramp (face 5, top lit 6, mortar 3, lower lip 4), with Bayer grain"""
    row = (y - CEIL) // 32; off = 0 if row % 2 else 32
    bx = (x + off) % 64; by = (y - CEIL) % 32
    if by < 2 or bx < 2: lv = 3
    elif by == 2: lv = 6
    elif by == 31: lv = 4
    else: lv = 5 if BAYER[y % 4][x % 4] > 2 else 4
    return lv + shift


def shaft(x, y):
    """the room's soft light shafts (diagonal bands), one step up, dithered"""
    t = ((x + y * .45) % 260) / 260
    return 1 if (.12 < t < .2 or .58 < t < .66) and BAYER[y % 4][x % 4] > 6 else 0


def base():
    c = Image.new('RGBA', (W, H)); px = c.load()
    for y in range(H):
        for x in range(W):
            if y < CEIL or y > FLOOR + 2:
                px[x, y] = DARK[1] if (x // 48 + y // 24) % 3 else DARK[2]
            else:
                px[x, y] = C[min(10, brick_level(x, y) + shaft(x, y))]
    d = ImageDraw.Draw(c)
    d.rectangle((0, CEIL - 3, W, CEIL - 1), fill=DARK[0])
    d.line((0, FLOOR + 1, W, FLOOR + 1), fill=C[7]); d.line((0, FLOOR + 2, W, FLOOR + 2), fill=C[5])
    for x in range(0, W, 5):                                   # moss tufts on the floor lip
        if (x * 7) % 11 < 3: d.line((x, FLOOR, x, FLOOR - 2), fill=(117, 150, 125))
    return c


def recess(c, x0, x1, top, warm=None):
    """carve the wall: darker bricks inside, a shaded soffit, shaded left and lit right reveals, a lit rim"""
    px = c.load(); soffit, reveal = 10, 7
    for y in range(top, FLOOR + 1):
        for x in range(x0, x1 + 1):
            lv = brick_level(x, y, -2)
            if y < top + soffit: lv = 2 if BAYER[y % 4][x % 4] > 5 else 1                        # underside of the lintel
            elif x < x0 + reveal: lv = 2 if (y // 32) % 2 else 3                                 # left reveal (shade)
            elif x > x1 - reveal: lv = 5 if (y // 32) % 2 else 4                                 # right reveal (lit)
            elif y < top + soffit + 12 and BAYER[y % 4][x % 4] > (y - top - soffit): lv -= 1      # shadow under the lintel
            if warm and x0 + reveal <= x <= x1 - reveal and y >= top + soffit:
                for lx, ly, r in warm:
                    dd = math.hypot((x - lx) * .8, (y - ly) * 1.1)
                    if dd < r: lv += 1 if dd > r * .45 or BAYER[y % 4][x % 4] > 7 else 2
            px[x, y] = C[max(0, min(10, lv))]
            if warm and x0 + reveal <= x <= x1 - reveal and y >= top + soffit:                  # warm lamp tint, like the map's lit lamps
                for lx, ly, r in warm:
                    if math.hypot((x - lx) * .8, (y - ly) * 1.1) < r * .55 and BAYER[y % 4][x % 4] > 9:
                        px[x, y] = Wr[5]
    d = ImageDraw.Draw(c)
    d.line((x0 - 1, top - 1, x1 + 1, top - 1), fill=C[7])                                       # lit top rim
    d.line((x0 - 1, top, x0 - 1, FLOOR), fill=C[3]); d.line((x1 + 1, top, x1 + 1, FLOOR), fill=C[7])
    d.line((x0, FLOOR, x1, FLOOR), fill=C[1])                                                     # recess floor meets the room floor


def string_lights(c, x0, x1, y):
    """sagging wire with small warm bulbs, drawn on the recess (no backdrop bricks behind)"""
    d = ImageDraw.Draw(c); n = max(2, (x1 - x0) // 40)
    for i in range(n):
        a, b = x0 + (x1 - x0) * i // n, x0 + (x1 - x0) * (i + 1) // n
        for x in range(a, b + 1):
            t = (x - a) / max(1, b - a); d.point((x, y + round(6 * 4 * t * (1 - t))), fill=INK)
        bx = (a + b) // 2; by = y + 6
        d.rectangle((bx - 1, by + 1, bx + 1, by + 2), fill=STEEL[2]); d.rectangle((bx - 2, by + 3, bx + 2, by + 7), fill=(255, 214, 140), outline=INK)
        d.point((bx - 1, by + 4), fill=(255, 246, 214))


def unbrick(img):
    """drop the backdrop's dark wall pixels around a prop crop"""
    img = img.copy(); px = img.load(); wall = {tuple(C[i][:3]) for i in range(0, 5)} | {(6, 9, 11)}
    for y in range(img.height):
        for x in range(img.width):
            r, g, b_, a = px[x, y]
            if (r, g, b_) in wall or (abs(r - g) < 14 and abs(g - b_) < 14 and r < 60): px[x, y] = (0, 0, 0, 0)
    return img


def shop_contents(c, x0, x1, top):
    """the adopted shop's props, taken from its backdrop, inside the shop recess"""
    b = m.img(m.SHOP / 'stage2-backdrop.png')
    string_lights(c, x0 + 40, x1 - 20, top + 12)
    poster = b.crop((178, 130, 306, 233)); c.alpha_composite(poster, (X - poster.width // 2 + 6, FLOOR - 76 - poster.height + 20))
    tools = unbrick(b.crop((103, 162, 152, 228))); c.alpha_composite(tools, (X - 140, FLOOR - 34 - tools.height + 12))
    jars = unbrick(b.crop((326, 134, 374, 160))); c.alpha_composite(jars, (X + 74, FLOOR - 150))
    ln = m.img(m.SHOP / 'lantern.png'); c.alpha_composite(ln, (X - 123 - ln.width // 2, top + 8))
    npc = m.img(m.SHOP / 'npc2.png'); c.alpha_composite(npc, (X + 16 - npc.width // 2, FLOOR - npc.height))
    dk = m.img(m.SHOP / 'stage2-desk.png'); c.alpha_composite(dk, (X - dk.width // 2, FLOOR - dk.height))


PIPE = [INK, C[1], C[3], C[4], C[5], C[6], C[7], C[8], C[9], STEEL[3], STEEL[4]]


def hpipe(c, x0, x1, y, t=20):
    """the shop's sewer pipe: lit top, dark belly (the desk.py post ramp), flanges and wall clamps"""
    d = ImageDraw.Draw(c); ramp = [0, 5, 7, 6, 5, 4, 4, 3, 3, 2, 1, 0]
    for i in range(t): d.line((x0, y + i, x1, y + i), fill=PIPE[ramp[min(11, i * 12 // t)]])
    for fx in range(x0 + 40, x1 - 10, 112):
        d.rectangle((fx, y - 2, fx + 6, y + t + 1), fill=PIPE[4], outline=INK); d.line((fx + 1, y - 1, fx + 1, y + t), fill=PIPE[7])
    for cx in range(x0 + 96, x1 - 10, 112):
        d.rectangle((cx, y - 4, cx + 3, y - 1), fill=STEEL[1], outline=INK)


def vpipe(c, x, y0, y1, t=20):
    d = ImageDraw.Draw(c); ramp = [0, 5, 7, 6, 5, 4, 4, 3, 3, 2, 1, 0]
    for i in range(t): d.line((x + i, y0, x + i, y1), fill=PIPE[ramp[min(11, i * 12 // t)]])
    for fy in range(y0 + 50, y1 - 10, 90):
        d.rectangle((x - 2, fy, x + t + 1, fy + 6), fill=PIPE[4], outline=INK); d.line((x - 1, fy + 1, x + t, fy + 1), fill=PIPE[7])


def elbow(c, x, y, t=20, down_left=True):
    """quarter bend from the horizontal run into the vertical drop"""
    d = ImageDraw.Draw(c); ramp = [0, 5, 7, 6, 5, 4, 4, 3, 3, 2, 1, 0]
    r0 = 10
    for i in range(t):
        r = r0 + t - 1 - i
        cx, cy = (x + r0 + t, y + r0 + t) if down_left else (x - r0, y + r0 + t)
        box = (cx - r, cy - r, cx + r, cy + r)
        d.arc(box, 180 if down_left else 270, 270 if down_left else 360, fill=PIPE[ramp[min(11, i * 12 // t)]], width=2)


def valve(c, x, y):
    d = ImageDraw.Draw(c)
    d.rectangle((x, y, x + 16, y + 10), fill=STEEL[1], outline=INK); d.line((x + 1, y + 1, x + 15, y + 1), fill=STEEL[4])
    d.line((x + 8, y - 8, x + 8, y), fill=INK, width=2); d.ellipse((x + 2, y - 14, x + 14, y - 6), outline=INK, width=2)
    d.arc((x + 3, y - 13, x + 13, y - 7), 180, 300, fill=(150, 70, 50), width=1)
    for i in range(3): d.point((x + 8, y + 14 + i * 4), fill=C[8])                             # a slow drip


def backdrop_pipes(c, x0, x1, py, right_valve=True):
    """the adopted shop's own pipe pieces: a horizontal run tiled between two bends that drop to the floor"""
    b = m.img(m.SHOP / 'stage2-backdrop.png')
    run = b.crop((60, 34, 340, 66)); bend = b.crop((430, 30, 480, 66)); drop = b.crop((452, 66, 480, 270))
    x = x0 + bend.width
    while x < x1 - bend.width:
        piece = run.crop((0, 0, min(run.width, x1 - bend.width - x), run.height)); c.alpha_composite(piece, (x, py + 4)); x += run.width
    c.alpha_composite(bend, (x1 - bend.width, py)); c.alpha_composite(bend.transpose(Image.FLIP_LEFT_RIGHT), (x0, py))
    for dx, img in ((x1 - 28, drop), (x0, drop.transpose(Image.FLIP_LEFT_RIGHT))):
        y = py + bend.height
        while y < FLOOR:
            part = img.crop((0, 0, img.width, min(img.height, FLOOR - y + 1))); c.alpha_composite(part, (dx, y)); y += img.height
    if right_valve:
        valve_img = unbrick(b.crop((414, 150, 452, 212))); c.alpha_composite(valve_img, (x1 - 28 - valve_img.width + 6, FLOOR - 120))


def hang_sign(c, x, pipe_bottom):
    """the palette sign hangs on a short chain from the pipe"""
    d = ImageDraw.Draw(c)
    for i in range(3): d.ellipse((x - 2, pipe_bottom + i * 6, x + 2, pipe_bottom + i * 6 + 6), outline=INK)
    sg = m.img(HERE / 'sign-stage2-palette.png'); c.alpha_composite(sg, (x - sg.width // 2 + 1, pipe_bottom + 16))


def sign(c, x, y):
    sg = m.img(m.SHOP / 'sign.png'); c.alpha_composite(sg, (x - sg.width // 2, y))


def stock(c, x0, x1, top, focus):
    d = ImageDraw.Draw(c); sy = FLOOR - 80
    d.rectangle((x0, sy, x1, sy + 9), fill=Wr[6], outline=INK)
    d.line((x0 + 1, sy + 1, x1 - 1, sy + 1), fill=Wr[9]); d.line((x0 + 1, sy + 2, x1 - 1, sy + 2), fill=Wr[8]); d.line((x0 + 1, sy + 8, x1 - 1, sy + 8), fill=Wr[4])
    for bx in (x0 + 34, (x0 + x1) // 2 - 6, x1 - 46):
        d.rectangle((bx, sy + 10, bx + 12, sy + 12), fill=STEEL[2], outline=INK)
        for i in range(10): d.point((bx + 1, sy + 13 + i), fill=INK); d.point((bx + 2 + i, sy + 13 + i), fill=STEEL[1])
    cols = ITEMS + COVER + (1 if BELLS else 0)
    span = x1 - x0; xs = [x0 + span // (2 * cols) + i * span // cols for i in range(cols)]; tops = {}
    m.USED.clear(); m.SHOWN.clear()
    RET.clear(); RET['shelf_y'], RET['floor'], RET['W'] = sy, FLOOR, W
    for i, x in enumerate(xs):
        if ITEMS <= i < ITEMS + COVER:
            import reroll_objects as ro_obj
            P = ro_obj.cloth_palette(type('C', (), {'name': '2챕터'})())
            for g in (FLOOR, sy): ro_obj.cover(c, x, ro_obj.plate_count(c, x, g, None, (STEEL[1], STEEL[3], STEEL[0], INK), pal.PAPER[3], w=56), P)
            continue
        if i == cols - 1 and BELLS:
            import reroll_objects as ro_obj                              # the supply crate (user pick 2026-11-14)
            bell = m.img(m.SHOP / 'stage2-reroll-crate.png'); P = ro_obj.palette(type('C', (), {'name': '2챕터'})())
            for g, key in ((FLOOR, ('b', 'w')), (sy, ('b', 'i'))):
                if key not in HIDE: ro_obj.crate(c, x, ro_obj.plate_count(c, x, g, 2, (STEEL[1], STEEL[3], STEEL[0], INK), pal.PAPER[3]), P)
                RET[key] = dict(x=x, y=g, sprite=bell, ground=g)
            continue
        for ground, sprite, price, key in ((FLOOR, m.fit(m.weapon(m.WEAPONS[i]), 42), m.WP[i], ('w', i)), (sy, m.ink(m.INKS[i]), m.IP[i], ('i', i))):
            if key in SWAP: sprite, price = SWAP[key]
            y = stand(c, x, ground, None if key in SOLD else price, STEEL[1], STEEL[3], STEEL[0], INK, pal.PAPER[3], h=16)
            if key not in HIDE and key not in SOLD: c.alpha_composite(sprite, (x - sprite.width // 2, y - sprite.height))
            tops[key] = y - sprite.height; RET[key] = dict(x=x, y=y, sprite=sprite, price=price, ground=ground)
    RET['xs'] = xs
    if NO_HERO: return
    kind, i = focus
    if kind == 'talk':
        import bubble as bb
        m.hero(c, X + 16 - round(3.8 * 32), FLOOR); bb.show(c, '뭐야, 넌 누구냐?', '수로 정비공', (X + 16, FLOOR - 120), W, sketch=False)
    elif kind == 'b':
        m.hero(c, xs[3] + 36, sy)                                    # no card at the crate (user 2026-11-14)
    elif kind == 'none':
        m.hero(c, xs[1] + 40, FLOOR)
    elif kind == 'i':
        m.hero(c, xs[i] + 40, sy); k = m.SHOWN[-3 + i]
        card(c, xs[i], tops[focus], k['name'], '+5회', k['description'], m.IP[i], m.GOLD, W)
    else:
        m.hero(c, xs[i] + 40, FLOOR); t = weapon_card(m.WEAPONS[i])
        card(c, xs[i], tops[focus], *t, m.WP[i], (166, 207, 220), W)


HIDE, SOLD, SWAP, RET = set(), set(), {}, {}
BELLS, NO_HERO = False, False
ITEMS, COVER = 3, 0


MOSS = [(58, 92, 70), (84, 122, 88), (117, 150, 125)]


def details(c, x0, x1, top, py):
    """light touches: moss strands from the lintel rim and the pipe, wall cracks, a puddle under the valve, a wrench on the lit reveal"""
    d = ImageDraw.Draw(c)
    for i, (mx, ln) in enumerate(((x0 + 70, 9), (x0 + 112, 5), (x0 + 240, 12), (x0 + 330, 6), (x0 + 410, 10), (x1 - 90, 7), (x1 - 30, 11))):
        for j in range(ln):
            d.point((mx + (j // 4) % 2, top + j), fill=MOSS[2 if j < 2 else 1 if j < ln - 2 else 0])
        d.point((mx - 1, top), fill=MOSS[2]); d.point((mx + 1, top + 1), fill=MOSS[1])
    for mx in (x0 + 20, x0 + 190, x0 + 380, x1 - 10):
        for j in range(5): d.point((mx + j % 2, py + 30 + j), fill=MOSS[1 if j < 3 else 0])
    for cx, cy, pts in ((x0 - 120, top + 40, [(0, 0), (4, 3), (5, 8), (9, 11), (10, 16)]), (x1 + 70, top + 90, [(0, 0), (-3, 4), (-3, 9), (-7, 12)]),
                        (x0 + 140, top - 50, [(0, 0), (5, 2), (8, 6), (14, 7)])):
        for (ax, ay), (bx, by) in zip(pts, pts[1:]): d.line((cx + ax, cy + ay, cx + bx, cy + by), fill=C[2])
    vx = x1 + 30
    d.ellipse((vx - 16, FLOOR - 2, vx + 18, FLOOR + 3), fill=C[3]); d.line((vx - 10, FLOOR, vx + 6, FLOOR), fill=C[7])
    wx = x1 - 4
    d.rectangle((wx - 2, top + 70, wx + 1, top + 102), fill=STEEL[2], outline=INK)
    d.ellipse((wx - 5, top + 62, wx + 4, top + 72), outline=INK, fill=STEEL[3]); d.rectangle((wx - 1, top + 62, wx, top + 66), fill=C[4])
    d.line((wx - 1, top + 71, wx - 1, top + 100), fill=STEEL[4])


def scene(kind, focus):
    c = base()
    shop0, shop1, top = X - 150, X + 150, FLOOR - 190
    store0, store1 = X + 150, X + 150 + 316 + EXTRA
    if kind == 'separate':
        recess(c, shop0, shop1, top, warm=[(X - 123, top + 40, 120), (X + 40, top + 30, 140)])
        recess(c, store0, store1, top, warm=[(store0 + 80, top + 24, 110), (store1 - 80, top + 24, 110)])
        shop_contents(c, shop0, shop1, top)
        string_lights(c, store0 + 14, store1 - 14, top + 12)
        sign(c, (shop1 + store0) // 2, top + 6)
        stock(c, store0 + 7, store1 - 7, top, focus)
    else:
        recess(c, shop0, store1, top, warm=[(X - 123, top + 40, 120), (X + 40, top + 30, 140), (store0 + 120, top + 24, 150)])
        shop_contents(c, shop0, shop1, top)
        string_lights(c, store0 + 14, store1 - 14, top + 12)
        # the shop's own pipe run over the whole recess, bending down at both ends, its valve at the right
        py = top - 34
        backdrop_pipes(c, shop0 - 44, store1 + 44, py)
        hang_sign(c, shop0 + 26, py + 30)
        details(c, shop0, store1, top, py)
        stock(c, store0 + 7, store1 - 7, top, focus)
    return c


def labelled(c, text):
    d = ImageDraw.Draw(c); d.rectangle((0, 0, W, 20), fill=(23, 23, 17)); d.text((8, 2), text, font=m.UI, fill=(255, 255, 255))


if __name__ == '__main__':
    m.W = W
    a = scene('separate', ('i', 1)); labelled(a, '2챕터 A  정비소와 창고를 각각 벽에 판 벽감 (방 벽과 같은 벽돌을 두 단 어둡게, 안쪽 면 그늘)')
    a.resize((W * 2, H * 2), Image.NEAREST).save(HERE / '2챕터-A-따로.png')
    b = scene('merged', ('i', 1)); labelled(b, '2챕터 B  팔레트 팻말을 1챕터와 같은 모양(2챕터 색) / 가격 상자 가운데 / 위 칸 잉크에 다가감')
    b.resize((W * 2, H * 2), Image.NEAREST).save(HERE / '2챕터-B-합침.png')
    for focus, name, text in ((('talk', 0), '2챕터-말풍선', '2챕터 첫 방문 정비공 말풍선 / 예전 말풍선 / 정비공 발밑에서 3.75칸 위'),
                              (('w', 0), '2챕터-카드-무기', '2챕터 아래 칸 무기에 다가감 / 카드는 상품 바로 위, 설명 전체')):
        e = scene('merged', focus); labelled(e, text); e.resize((W * 2, H * 2), Image.NEAREST).save(HERE / f'{name}.png')
