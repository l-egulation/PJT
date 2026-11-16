"""시안 8차: 1챕터 1-B 궤짝(테두리 없음), 2챕터 뒷벽 그림에서 떼어 온 돌로 지은 벽감(나무 안쪽, 위아래 칸 4칸씩).
정보 카드는 설명 전체를 줄바꿈해 다 보이고, 가격에는 금화 아이콘.

    python final_scenes.py <워크트리 루트>
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
import json

COIN = Image.open(ROOT / 'unity/Assets/Deviant/TemporaryGUI/Resources/ApprovedCombatHUD/sprites/gold-coin.png').convert('RGBA')
COIN = COIN.crop(COIN.getbbox()).resize((11, 11), Image.LANCZOS)
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def wrap(text, font, width):
    lines, cur = [], ''
    for word in text.split(' '):
        trial = (cur + ' ' + word).strip()
        if ImageDraw.Draw(Image.new('L', (1, 1))).textlength(trial, font=font) <= width: cur = trial
        else: lines.append(cur); cur = word
    if cur: lines.append(cur)
    return lines


def card(c, cx, item_top, title, sub, body, value, subcol, canvas_w):
    """info card right above the piece; the whole description wraps, the card grows with it"""
    import re
    body = re.sub(r'\(강화 [^)]*\)', '', body)          # the game shows the base value (LiveInkText)
    w = 236; lines = wrap(body, m.UIS, w - 16) if body else []; h = 46 + 15 * len(lines) + 22
    x = max(4, min(canvas_w - w - 4, cx - w // 2)); y = item_top - 10 - h
    d = ImageDraw.Draw(c)
    d.rounded_rectangle((x, y, x + w, y + h), 4, fill=m.PLATE + (244,), outline=(92, 98, 106))
    d.polygon([(cx - 6, y + h), (cx + 6, y + h), (cx, y + h + 7)], fill=m.PLATE + (244,))
    d.text((x + 8, y + 6), title, font=m.UI, fill=(243, 239, 229)); d.text((x + 8, y + 25), sub, font=m.UIS, fill=subcol)
    for i, line in enumerate(lines): d.text((x + 8, y + 42 + 15 * i), line, font=m.UIS, fill=(220, 220, 214))
    by = y + h - 21
    d.rounded_rectangle((x + 8, by, x + 26, by + 15), 3, fill=(240, 236, 226), outline=m.K); d.text((x + 14, by + 1), 'F', font=m.PIX, fill=m.K)
    if value is None: return
    t = str(value); tw = d.textlength(t, font=m.PIX); c.alpha_composite(COIN, (int(x + w - 10 - tw - 14), by + 2)); d.text((x + w - 10 - tw, by + 1), t, font=m.PIX, fill=m.GOLD)


def stand(c, cx, ground, price, face, top, shade, edge, text, w=56, h=16):
    """price plate: corners shaved one step, coin and price centred in the box"""
    x0, y0 = cx - w // 2, ground - h
    lay = Image.new('RGBA', (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    d.rectangle((0, 0, w - 1, h - 1), fill=face, outline=edge)
    d.rectangle((2, 1, w - 3, 1), fill=top); d.rectangle((2, h - 2, w - 3, h - 2), fill=shade)
    for cxp, cyp, sx, sy in ((0, 0, 1, 1), (w - 1, 0, -1, 1), (0, h - 1, 1, -1), (w - 1, h - 1, -1, -1)):
        for ax, ay in ((0, 0), (sx, 0), (0, sy)): lay.putpixel((cxp + ax, cyp + ay), (0, 0, 0, 0))
        lay.putpixel((cxp + sx, cyp + sy), edge if len(edge) == 4 else tuple(edge) + (255,))
    c.alpha_composite(lay, (x0, y0)); d = ImageDraw.Draw(c)
    if price is None: return y0                                   # sold: the empty plate stays
    t = str(price); tw = d.textlength(t, font=m.PIX); x = int(cx - (tw + 14) / 2)
    ty = y0 + (h - 10) // 2 - 1                                      # glyph rows 1..10 of the font box -> centred in the plate
    c.alpha_composite(COIN, ((x, ty + 1))); d.text((x + 14, ty), t, font=m.PIX, fill=text)
    return y0


def weapon_card(form):
    return {'bomb': ('목탄', '대형 목탄', '크게 터지는 대형 목탄을 던진다. 터진 자리에 잉크가 번져 잠시 적을 느리게 한다.'),
            'dagger': ('단검', '그림자 찌르기', '순간 이동해 적의 등 뒤를 찌른다.'),
            'greatsword': ('대검', '회전 전진', '검을 돌리며 앞으로 나아가 닿는 적을 여러 번 벤다.')}[form]


# ------------------------------------------------------------------ chapter 1
def chapter1(focus):
    m.H, m.FLOOR = 400, 380
    X, FLOOR = m.X, m.FLOOR
    meta = json.loads((HERE / 'box1-meta.json').read_text(encoding='utf-8'))['crate-white']
    m.USED.clear(); m.SHOWN.clear()
    c = m.base(); m.actors(c, sign_at=None)
    sg = m.img(m.SHOP / 'sign-stage1.png'); c.alpha_composite(sg, (X - 150 - sg.width // 2, FLOOR - 160))
    art = Image.open(HERE / 'box1-crate-white.png').convert('RGBA'); sx = X + 112
    c.alpha_composite(art, (sx, FLOOR - art.height))
    x0, x1 = sx + meta['inner'][0], sx + meta['inner'][1]; span = x1 - x0
    xs = [x0 + span // 6 + i * span // 3 for i in range(3)]
    floor_y, shelf_y = FLOOR - meta['floor'], FLOOR - meta['shelf']
    tops = {}
    for i, x in enumerate(xs):
        for ground, sprite, price, key in ((floor_y, m.weapon(m.WEAPONS[i]), m.WP[i], ('w', i)), (shelf_y, m.ink(m.INKS[i]), m.IP[i], ('i', i))):
            y = stand(c, x, ground, price, m.C, m.Wh, m.L, m.K, m.K)
            if key[0] == 'w': sprite = m.fit(sprite, 42)
            c.alpha_composite(sprite, (x - sprite.width // 2, y - sprite.height)); tops[key] = y - sprite.height
    kind, i = focus
    if kind == 'i':
        m.hero(c, xs[i] + 40, shelf_y); k = m.SHOWN[-3 + i]
        card(c, xs[i], tops[focus], k['name'], f'+5회', k['description'], m.IP[i], m.GOLD, m.W)
    else:
        m.hero(c, xs[i] + 40, floor_y); t = weapon_card(m.WEAPONS[i])
        card(c, xs[i], tops[focus], *t, m.WP[i], (166, 207, 220), m.W)
    return c


# ------------------------------------------------------------------ chapter 2
BD = None


def bd():
    global BD
    if BD is None: BD = m.img(m.SHOP / 'stage2-backdrop.png')
    return BD


def tile_v(strip, height):
    out = Image.new('RGBA', (strip.width, height)); y = 0
    while y < height: out.alpha_composite(strip, (0, y)); y += strip.height
    return out


def tile_h(strip, width):
    out = Image.new('RGBA', (width, strip.height)); x = 0
    while x < width: out.alpha_composite(strip, (x, 0)); x += strip.width
    return out


def wood_back(w, h, lamps):
    """2챕터 나무 램프 판자 벽: 단계 → 램프 + 4×4 베이어 디더, 전구 쪽은 한 단 밝게"""
    Wr = pal.W; img = Image.new('RGBA', (w, h)); px = img.load()
    for y in range(h):
        for x in range(w):
            b = x % 14
            level = 5 if b > 1 else 3 if b == 0 else 6
            level -= 2 if y < 10 else 1 if y < 22 else 0
            near = min(math.hypot((x - lx) * .8, (y - ly) * 1.2) for lx, ly in lamps)
            level += max(0, 2.4 - near / 40) + BAYER[y % 4][x % 4] / 16 - .5
            level = max(1, min(9, int(level)))
            px[x, y] = Wr[level]
    for x0 in range(7, w, 14):
        for y in range(30, h, 44): px[x0, y] = Wr[2]; px[x0, y - 1] = Wr[8]
    return img


def chapter2(focus):
    W, H, FLOOR, X = 1000, 400, 370, 300
    Cr, Wr, INK, STEEL = pal.C, pal.W, pal.INK, pal.STEEL
    m.USED.clear(); m.SHOWN.clear()
    b = bd()
    brick = b.crop((210, 236, 360, 268))                         # plain interior bricks of the shop backdrop
    c = Image.new('RGBA', (W, H), Cr[1])
    dark = Image.new('RGBA', (W, FLOOR), (0, 0, 0, 0))
    for y in range(0, FLOOR, brick.height):
        for x in range(0, W, brick.width): dark.alpha_composite(brick, (x, y))
    shade = Image.new('RGBA', dark.size, (0, 0, 0, 70)); dark.alpha_composite(shade)       # outside the lit shop, one step darker
    c.alpha_composite(dark)
    d = ImageDraw.Draw(c); d.rectangle((0, FLOOR, W, H), fill=Cr[3]); d.line((0, FLOOR, W, FLOOR), fill=Cr[6], width=2)
    for x in range(0, W, 32): d.line((x, FLOOR + 2, x, H), fill=Cr[2])
    c.alpha_composite(b, (X - b.width // 2, FLOOR - b.height))
    ln = m.img(m.SHOP / 'lantern.png'); c.alpha_composite(ln, (X - 123 - ln.width // 2, FLOOR - 146 - ln.height))
    sg = m.img(m.SHOP / 'sign.png'); c.alpha_composite(sg, (X + 175 - sg.width // 2, FLOOR - 109 - sg.height))
    npc = m.img(m.SHOP / 'npc2.png'); c.alpha_composite(npc, (X + 16 - npc.width // 2, FLOOR - npc.height))
    dk = m.img(m.SHOP / 'stage2-desk.png'); c.alpha_composite(dk, (X - dk.width // 2, FLOOR - dk.height))

    # alcove: the backdrop's own stone pillars and lintel, a wood-lined inside, two equal 4-cell bays
    bay, shelf_t = 80, 10                      # shelf top 2.5 cells above the floor (one jump reaches it)
    inner_h = bay * 2 + 6
    ax0 = X + 270; inner_w = 300
    left = b.crop((62, 96, 100, 270)); right = b.crop((377, 140, 413, 270))     # below the sign bracket
    lintel_mid = b.crop((97, 72, 377, 96)); lintel_l = b.crop((62, 72, 97, 96)); lintel_r = b.crop((377, 72, 413, 96))
    top = FLOOR - inner_h
    lamps = [(inner_w // 4, 18), (inner_w * 3 // 4, 18), (inner_w // 2, bay + shelf_t + 20)]
    c.alpha_composite(wood_back(inner_w, inner_h, lamps), (ax0 + left.width, top))
    # shadow the stone throws into the recess
    g = Image.new('RGBA', c.size, (0, 0, 0, 0)); gd = ImageDraw.Draw(g)
    for i in range(10):
        gd.line((ax0 + left.width + i, top, ax0 + left.width + i, FLOOR), fill=(0, 0, 0, 110 - i * 11))
        gd.line((ax0 + left.width, top + i, ax0 + left.width + inner_w, top + i), fill=(0, 0, 0, 120 - i * 12))
    c.alpha_composite(g)
    c.alpha_composite(tile_v(left, inner_h), (ax0, top))
    c.alpha_composite(tile_v(right, inner_h), (ax0 + left.width + inner_w, top))
    lin = Image.new('RGBA', (left.width + inner_w + right.width, lintel_mid.height))
    lin.alpha_composite(lintel_l, (0, 0)); lin.alpha_composite(tile_h(lintel_mid, inner_w + left.width - lintel_l.width), (lintel_l.width, 0))
    lin.alpha_composite(lintel_r, (lin.width - lintel_r.width, 0))
    c.alpha_composite(lin, (ax0, top - lin.height))
    bulbs = b.crop((135, 100, 372, 122)); c.alpha_composite(tile_h(bulbs, inner_w).crop((0, 0, inner_w, bulbs.height)), (ax0 + left.width, top + 2))
    # shelf: the desk's thick plank language (wood ramp, lit front edge), iron brackets
    ix0 = ax0 + left.width; ix1 = ix0 + inner_w - 1; sy = FLOOR - bay
    d = ImageDraw.Draw(c)
    d.rectangle((ix0, sy, ix1, sy + shelf_t), fill=Wr[6], outline=INK)
    d.line((ix0 + 1, sy + 1, ix1 - 1, sy + 1), fill=Wr[9]); d.line((ix0 + 1, sy + 2, ix1 - 1, sy + 2), fill=Wr[8]); d.line((ix0 + 1, sy + shelf_t - 1, ix1 - 1, sy + shelf_t - 1), fill=Wr[4])
    for gx in range(ix0 + 12, ix1, 40): d.line((gx, sy + 5, gx + 14, sy + 5), fill=Wr[5])
    for bx in (ix0 + 36, (ix0 + ix1) // 2 - 6, ix1 - 48):
        d.rectangle((bx, sy + shelf_t + 1, bx + 12, sy + shelf_t + 3), fill=STEEL[2], outline=INK)
        for i in range(12): d.point((bx + 1, sy + shelf_t + 4 + i), fill=INK); d.point((bx + 2 + i * 10 // 12, sy + shelf_t + 4 + i), fill=STEEL[1])
    # floor lip inside the recess
    d.rectangle((ix0, FLOOR - 6, ix1, FLOOR - 1), fill=Wr[4]); d.line((ix0, FLOOR - 6, ix1, FLOOR - 6), fill=INK)

    for gx in range(0, W, 8): d.line((gx, FLOOR - 256, gx + 3, FLOOR - 256), fill=(255, 120, 120))
    d.text((6, FLOOR - 272), '방 천장(안쪽 8칸)', font=m.UIS, fill=(255, 150, 150))
    xs = [ix0 + inner_w // 6 + i * inner_w // 3 for i in range(3)]
    floor_y, shelf_y = FLOOR - 6, sy
    tops = {}
    for i, x in enumerate(xs):
        for ground, sprite, price, key in ((floor_y, m.weapon(m.WEAPONS[i]), m.WP[i], ('w', i)), (shelf_y, m.ink(m.INKS[i]), m.IP[i], ('i', i))):
            y = stand(c, x, ground, price, STEEL[1], STEEL[3], STEEL[0], INK, pal.PAPER[3], h=16)
            if key[0] == 'w': sprite = m.fit(sprite, 42)
            c.alpha_composite(sprite, (x - sprite.width // 2, y - sprite.height)); tops[key] = y - sprite.height
    kind, i = focus
    if kind == 'i':
        m.hero(c, xs[i] + 40, shelf_y); k = m.SHOWN[-3 + i]
        card(c, xs[i], tops[focus], k['name'], '+5회', k['description'], m.IP[i], m.GOLD, W)
    else:
        m.hero(c, xs[i] + 40, floor_y); t = weapon_card(m.WEAPONS[i])
        card(c, xs[i], tops[focus], *t, m.WP[i], (166, 207, 220), W)
    return c, W, H


def labelled(c, w, text):
    d = ImageDraw.Draw(c); d.rectangle((0, 0, w, 22), fill=(23, 23, 17)); d.text((8, 3), text, font=m.UI, fill=(255, 255, 255))


if __name__ == '__main__':
    a = chapter1(('i', 1)); labelled(a, m.W, '1챕터 궤짝 / 선반 2.5칸(1단 점프로 올라섬), 위아래 칸 같은 높이 / 실제 주인공 크기 / 위 칸 잉크')
    a.resize((a.width * 2, a.height * 2), Image.NEAREST).save(HERE / '최종1-잉크.png')
    a = chapter1(('w', 0)); labelled(a, m.W, '1챕터 궤짝 / 아래 칸 무기에 다가감 / 실제 주인공 크기')
    a.resize((a.width * 2, a.height * 2), Image.NEAREST).save(HERE / '최종1-무기.png')
    b2, w2, h2 = chapter2(('i', 1)); labelled(b2, w2, '2챕터 벽감 / 뒷벽 그림의 돌 그대로, 나무 안쪽 / 선반 2.5칸(1단 점프), 위아래 칸 같은 높이 / 방 높이 8칸 그대로(점선)')
    b2.resize((w2 * 2, h2 * 2), Image.NEAREST).save(HERE / '최종2-잉크.png')
