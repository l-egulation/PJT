"""1챕터 장면: 새로 그린 건물(building-stage1.png, ch1_building.py) + 정비공·작업대·등불·공구걸이·진열 선반·상품·간판. 정보 카드 없음.

    python ch1_scene_new.py <워크트리 루트>
"""
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import final_scenes as fs
import mock as m
from PIL import Image, ImageDraw
import ch1_building as cb
import bubble as bb

LINE1 = '오, 너는 누구니? 못 보던 얼굴인데.'
# animation hooks: HIDE = pieces left off (plate drawn), SOLD = empty plates, SWAP = key -> (sprite, price),
# BELLS = a reroll bell column, NO_HERO = no hero/card (the animation draws them), RET = where everything went
HIDE, SOLD, SWAP, RET = set(), set(), {}, {}
BELLS, NO_HERO = False, False
ITEMS, COVER = 3, 0


def plank(d, x0, x1, y):
    O, F, H, S = cb.OUT, cb.FACE, cb.HI, cb.SH
    d.rectangle((x0, y, x1, y + 9), fill=F); d.rectangle((x0, y, x1, y + 1), fill=O); d.rectangle((x0, y + 8, x1, y + 9), fill=O)
    d.rectangle((x0, y + 2, x1, y + 3), fill=H); d.rectangle((x0, y + 6, x1, y + 7), fill=S)
    for bx in (x0 + 24, (x0 + x1) // 2, x1 - 36):
        for i in range(10):
            d.rectangle((bx, y + 10 + i, bx + 1, y + 10 + i), fill=O); d.rectangle((bx + 2, y + 10 + i, bx + 11 - i, y + 10 + i), fill=S)
            d.rectangle((bx + 11 - i, y + 10 + i, bx + 12 - i, y + 10 + i), fill=O)


def scene(focus=('none', 1), label=''):
    m.H, m.FLOOR = 340, 320
    FLOOR = m.FLOOR; W = 1080 + 2 * cb.EXTRA
    c = Image.new('RGBA', (W, m.H), m.SKY + (255,)); d = ImageDraw.Draw(c)
    for i, (y, col) in enumerate(((150, (238, 233, 222)), (180, (232, 226, 214)))):
        d.ellipse((-200 + i * 400, y, 600 + i * 600, y + 260), fill=col)
    d.rectangle((0, FLOOR, W, m.H), fill=m.C); d.rectangle((0, FLOOR, W, FLOOR + 1), fill=cb.OUT)
    import os; bld = Image.open(HERE / os.environ.get('SHOP_BUILDING', 'building-stage1.png')).convert('RGBA'); B0 = 40; top_y = FLOOR - bld.height + 2
    c.alpha_composite(bld, (B0, top_y))
    px = lambda dots: B0 + dots * 2
    X = (px(cb.O0) + px(cb.SPLIT)) // 2 + 6                                            # desk centre in the shop corner
    bd = m.img(m.SHOP / 'stage1-backdrop.png')
    rack = bd.crop((282, 92, 408, 131)); rp = rack.load()
    for yy in range(rack.height):                                                    # only the rail and tools, not the painted wall behind
        for xx in range(rack.width):
            if rp[xx, yy][:3] in ((246, 238, 226), (230, 221, 207), (216, 206, 191)): rp[xx, yy] = (0, 0, 0, 0)
    c.alpha_composite(rack, (X - rack.width // 2 - 4, FLOOR - cb.OH * 2 + 26))
    pc = c.load(); wall = {cb.BACK, cb.BACK2, cb.BACK3}
    for hx in (X - rack.width // 2 + 14, X + rack.width // 2 - 22):                  # two cords from the lintel, behind the cloth
        for yy in range(FLOOR - cb.OH * 2, FLOOR - cb.OH * 2 + 28):
            for dx in (0, 1):
                if pc[hx + dx, yy] in wall: pc[hx + dx, yy] = cb.OUT
    ln = m.img(m.SHOP / 'lantern-stage1.png'); c.alpha_composite(ln, (px(cb.O0) + 18 - ln.width // 2, FLOOR - cb.OH * 2 + 14))
    npc = m.img(m.SHOP / 'npc1.png'); c.alpha_composite(npc, (X + 16 - npc.width // 2, FLOOR - npc.height))
    dk = m.img(m.SHOP / 'stage1-desk.png'); c.alpha_composite(dk, (X - dk.width // 2, FLOOR - dk.height))
    row = lambda dot: FLOOR - 2 * (cb.FLOOR - dot)
    sg = m.img(m.SHOP / 'sign-stage1.png'); c.alpha_composite(sg, (px(cb.SIGN_X) + 1 - sg.width // 2, row(cb.SIGN_ARM)))
    i0, i1 = px(cb.SPLIT) + 8, px(cb.O1) - 10; shelf_y = FLOOR - 80
    plank(ImageDraw.Draw(c), i0, i1, shelf_y)
    m.USED.clear(); m.SHOWN.clear()
    n_items, n_cover = ITEMS, COVER                                   # goods per row, cloth-covered (not yet unlocked) places
    cols = n_items + n_cover + (1 if BELLS else 0)
    span = i1 - i0; xs = [i0 + span // (2 * cols) + k * span // cols for k in range(cols)]; tops = {}
    RET.clear(); RET['shelf_y'], RET['floor'], RET['W'] = shelf_y, FLOOR - 1, W
    for k, x in enumerate(xs):
        if n_items <= k < n_items + n_cover:                          # a covered place: blank plate under a cloth
            import reroll_objects as ro_obj
            P = ro_obj.cloth_palette(type('C', (), {'name': '1챕터', 'cb': cb})())
            for g in (FLOOR - 1, shelf_y): ro_obj.cover(c, x, ro_obj.plate_count(c, x, g, None, (cb.FACE, cb.HI, cb.SH, cb.OUT), cb.OUT, w=56), P)
            continue
        if k == cols - 1 and BELLS:                                  # the reroll crates stand at the row ends
            import reroll_objects as ro_obj                              # the supply crate (user pick 2026-11-14)
            bell = m.img(m.SHOP / 'stage1-reroll-crate.png'); P = ro_obj.palette(type('C', (), {'name': '1챕터', 'cb': cb})())
            for g, key in ((FLOOR - 1, ('b', 'w')), (shelf_y, ('b', 'i'))):
                if key not in HIDE: ro_obj.crate(c, x, ro_obj.plate_count(c, x, g, 2, (cb.FACE, cb.HI, cb.SH, cb.OUT), cb.OUT), P)
                RET[key] = dict(x=x, y=g, sprite=bell, ground=g)
            continue
        for g, sp, pr, key in ((FLOOR - 1, m.fit(m.weapon(m.WEAPONS[k]), 42), m.WP[k], ('w', k)), (shelf_y, m.ink(m.INKS[k]), m.IP[k], ('i', k))):
            if key in SWAP: sp, pr = SWAP[key]
            y = fs.stand(c, x, g, None if key in SOLD else pr, cb.FACE, cb.HI, cb.SH, cb.OUT, cb.OUT)
            if key not in HIDE and key not in SOLD: c.alpha_composite(sp, (x - sp.width // 2, y - sp.height))
            tops[key] = y - sp.height; RET[key] = dict(x=x, y=y, sprite=sp, price=pr, ground=g)
    RET['xs'] = xs; RET['desk'] = X
    if NO_HERO: return c, W
    kind, k = focus
    if kind == 'talk':
        m.hero(c, X + 16 - round(3.8 * 32), FLOOR)
        bb.show(c, LINE1, '마을 장인', (X + 16, FLOOR - 120), W)
    elif kind == 'b':
        m.hero(c, xs[3] + 36, shelf_y)
    else:
        m.hero(c, xs[k] + 40, shelf_y if kind == 'i' else FLOOR)
    if kind == 'i':
        it = m.SHOWN[-3 + k]; fs.card(c, xs[k], tops[focus], it['name'], '+5회', it['description'], m.IP[k], m.GOLD, W)
    elif kind == 'w':
        fs.card(c, xs[k], tops[focus], *fs.weapon_card(m.WEAPONS[k]), m.WP[k], (166, 207, 220), W)
    d = ImageDraw.Draw(c); d.rectangle((0, 0, W, 22), fill=(23, 23, 17)); d.text((8, 3), label, font=m.UI, fill=(255, 255, 255))
    return c, W


if __name__ == '__main__':
    for focus, name, label in ((('none', 1), '1챕터-새건물', '1챕터 10차  안쪽 벽 끝 메움, 윗돌 풀과 꽃, 천막 짧게, 간판은 들보 끝에 / 가격 상자 가운데'),
                               (('talk', 0), '1챕터-말풍선', '1챕터 첫 방문 장인 말풍선 / 예전 말풍선 / 장인 발밑에서 3.75칸 위, 주인공은 3.8칸 앞에 멈춤'),
                               (('i', 1), '1챕터-카드-잉크', '1챕터 위 칸 잉크에 다가감 / 카드는 상품 바로 위, 설명 전체'),
                               (('w', 0), '1챕터-카드-무기', '1챕터 아래 칸 무기에 다가감 / 카드는 상품 바로 위, 설명 전체')):
        img, W = scene(focus, label)
        img.resize((W * 2, m.H * 2), Image.NEAREST).save(HERE / f'{name}.png')
