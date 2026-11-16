"""정비소 방 안 진열 시안. 실제 정비소 그림과 아이콘을 게임 화소(32px = 1칸) 그대로 얹고 2배로 키워 저장한다.

    python mock.py <워크트리 루트>
"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, str(Path(__file__).resolve().parent))
from approved_art import Approved

ROOT = Path(sys.argv[1])
SHOP = ROOT / 'unity/Assets/Deviant/Game/Resources/MaintenanceShops'
HUD = ROOT / 'unity/Assets/Deviant/TemporaryGUI/Resources/ApprovedCombatHUD/sprites'
LOOT = ROOT / 'unity/Assets/Deviant/Inventory/Resources/LootPickups'
FONTS = ROOT / 'unity/Assets/Deviant'
PIX = ImageFont.truetype(str(FONTS / 'Inventory/Resources/PixelInventory/Fonts/Galmuri11-Bold.ttf'), 11)
PIXS = ImageFont.truetype(str(FONTS / 'Inventory/Resources/PixelInventory/Fonts/Galmuri11.ttf'), 11)
UI = ImageFont.truetype(str(FONTS / 'UI/Fonts/Pretendard-Bold.ttf'), 13)
UIS = ImageFont.truetype(str(FONTS / 'UI/Fonts/Pretendard-Regular.ttf'), 11)
OUT = Path(__file__).resolve().parent
ART = Approved(ROOT)
INK_IDS = ['red_basic_attack_stun' if False else i for i in list(ART.inks)]

U = 32
W, H, FLOOR = 960, 330, 250            # canvas px, floor line y
X = 300                                # desk centre x
K, D, M, L, C, Wh = (63, 62, 60), (111, 110, 105), (160, 157, 150), (204, 199, 190), (254, 247, 238), (255, 252, 247)
SKY, GOLD, PLATE = (246, 241, 231), (224, 204, 122), (36, 39, 43)


def img(p): return Image.open(p).convert('RGBA')


def outlined(sprite, color=K):
    """1-dot dark contour around a sprite (visibility)."""
    w, h = sprite.size; out = Image.new('RGBA', (w + 4, h + 4), (0, 0, 0, 0))
    a = sprite.split()[3]
    for dx, dy in ((-2, 0), (2, 0), (0, -2), (0, 2)):
        m = Image.new('RGBA', sprite.size, color + (255,)); out.paste(m, (2 + dx, 2 + dy), a)
    out.alpha_composite(sprite, (2, 2)); return out


def base():
    c = Image.new('RGBA', (W, H), SKY + (255,)); d = ImageDraw.Draw(c)
    for i, (y, col) in enumerate(((150, (238, 233, 222)), (180, (232, 226, 214)))):   # far hills
        d.ellipse((-200 + i * 300, y, 500 + i * 500, y + 260), fill=col)
    d.rectangle((0, FLOOR, W, H), fill=C)
    d.rectangle((0, FLOOR, W, FLOOR + 1), fill=K)
    for x in range(6, W, 46): d.line((x, FLOOR + 10 + (x * 7) % 30, x + 8, FLOOR + 10 + (x * 7) % 30), fill=L, width=2)
    for x in range(0, W, 18): d.rectangle((x, FLOOR - 4, x + 2, FLOOR - 1), fill=D)       # grass tufts
    bd = img(SHOP / 'stage1-backdrop.png'); c.alpha_composite(bd, (X - bd.width // 2, FLOOR - bd.height))
    ln = img(SHOP / 'lantern-stage1.png'); c.alpha_composite(ln, (X - 80 - ln.width // 2, FLOOR - 109 - ln.height))
    return c


def actors(c, sign_at=(156, 149)):
    if sign_at:
        sg = img(SHOP / 'sign-stage1.png'); c.alpha_composite(sg, (X + sign_at[0] - sg.width // 2, FLOOR - sign_at[1]))
    npc = img(SHOP / 'npc1.png'); c.alpha_composite(npc, (X + 16 - npc.width // 2, FLOOR - npc.height))
    dk = img(SHOP / 'stage1-desk.png'); c.alpha_composite(dk, (X - dk.width // 2, FLOOR - dk.height))


HERO = None


def hero(c, x, floor=None):
    """the real hero sprite (UnarmedBody idle F01, 32 px per cell) with its feet on the floor line"""
    global HERO
    if HERO is None: HERO = img(ROOT / 'unity/Assets/Deviant/Combat/Resources/UnarmedBody/idle/F01.png')
    f = floor or globals()['FLOOR']
    c.alpha_composite(HERO, (x - 64, f - 121))


def plank(c, x0, x1, top):
    d = ImageDraw.Draw(c)
    d.rectangle((x0, top, x1, top + 13), fill=Wh, outline=K, width=2)
    d.rectangle((x0 + 2, top + 9, x1 - 2, top + 11), fill=L)
    for x in range(x0 + 8, x1 - 10, 24): d.rectangle((x, top + 5, x + 8, top + 6), fill=M)


def fit(im, size):
    sc = size / max(im.size); return im.resize((max(1, round(im.width * sc)), max(1, round(im.height * sc))), Image.BOX)


def weapon(form):
    # the bag/shop's approved weapon art, 1.5 cells on its long side
    return fit(ART.texture('ui-assets-v2/' + form), 48)


def ink(color):
    # the bag/shop's approved ink bottle with its effect and condition marks, 1.4 cells tall
    iid = next(i for i, d in ART.inks.items() if d['color'] == color and i not in USED); USED.add(iid); SHOWN.append(ART.inks[iid])
    return fit(ART.ink(iid), 45)
USED = set(); SHOWN = []


def place(c, sprite, cx, ground, glow=True, outline=True):
    if glow:  # contact shadow
        g = Image.new('RGBA', c.size, (0, 0, 0, 0)); ImageDraw.Draw(g).ellipse((cx - 18, ground - 4, cx + 18, ground + 3), fill=(63, 62, 60, 90)); c.alpha_composite(g)
    s = sprite
    c.alpha_composite(s, (cx - s.width // 2, ground - 3 - s.height)); return ground - 3 - s.height


def coin(d, x, y):
    d.ellipse((x, y, x + 8, y + 8), fill=(232, 182, 60), outline=(120, 82, 20)); d.rectangle((x + 3, y + 2, x + 4, y + 5), fill=(255, 230, 140))


def price_under(c, cx, y, value, dark=True):
    """coin + number, no plate: ink-dark digits on the cream world"""
    d = ImageDraw.Draw(c); t = str(value); w = d.textlength(t, font=PIX) + 11
    x = int(cx - w / 2); coin(d, x, y + 2); d.text((x + 11, y), t, font=PIX, fill=K if dark else GOLD)


def price_tag(c, cx, y, value):
    d = ImageDraw.Draw(c); t = str(value); w = int(d.textlength(t, font=PIX)) + 18
    x = cx - w // 2; d.rounded_rectangle((x, y, x + w, y + 14), 3, fill=PLATE + (235,), outline=(92, 98, 106)); coin(d, x + 3, y + 3); d.text((x + 14, y + 1), t, font=PIX, fill=GOLD)


def card(c, cx, bottom, title, sub, body, value, subcol=(166, 207, 220)):
    d = ImageDraw.Draw(c); w = 230; h = 74; x = max(4, min(W - w - 4, cx - w // 2)); y = bottom - h
    d.rounded_rectangle((x, y, x + w, y + h), 4, fill=PLATE + (240,), outline=(92, 98, 106))
    d.text((x + 8, y + 6), title, font=UI, fill=(243, 239, 229)); d.text((x + 8, y + 24), sub, font=UIS, fill=subcol)
    d.text((x + 8, y + 38), body, font=UIS, fill=(220, 220, 214))
    d.rounded_rectangle((x + 8, y + 54, x + 26, y + 69), 3, fill=(240, 236, 226), outline=K); d.text((x + 14, y + 55), 'F', font=PIX, fill=K)
    t = str(value); tw = d.textlength(t, font=PIX); coin(d, int(x + w - 10 - tw - 11), y + 58); d.text((x + w - 10 - tw, y + 55), t, font=PIX, fill=GOLD)


def short(t):
    import re
    t = re.sub(r'\(강화 [^)]*\)', '', t); return t if len(t) <= 27 else t[:26] + '…'


def label(c, text):
    d = ImageDraw.Draw(c); d.rectangle((0, 0, W, 22), fill=(23, 23, 17)); d.text((8, 3), text, font=UI, fill=(255, 255, 255))


WEAPONS, INKS, WP, IP = ['bomb', 'dagger', 'greatsword', 'hammer', 'whip'], ['red', 'green', 'purple', 'blue', 'yellow'], [400, 400, 300, 450, 500], [220, 220, 220, 220, 220]


def option_a():
    """platform right beside the desk; sign moves to the backdrop's left"""
    c = base(); actors(c, sign_at=None)
    sg = img(SHOP / 'sign-stage1.png'); c.alpha_composite(sg, (X - 150 - sg.width // 2, FLOOR - 160))
    top = FLOOR - 3 * U; x0, x1 = X + 96, X + 96 + int(8.5 * U)
    plank(c, x0, x1, top)
    xs = [x0 + 56 + i * 80 for i in range(3)]
    for i, x in enumerate(xs):
        place(c, weapon(WEAPONS[i]), x, FLOOR); price_under(c, x, FLOOR + 6, WP[i])
        place(c, ink(INKS[i]), x, top); price_under(c, x, top + 16, IP[i])
    hero(c, xs[1] + 14, top)
    k = SHOWN[-2]; card(c, xs[1], top - 52, k['name'], '+5회', short(k['description']), 220, subcol=GOLD)
    label(c, 'A  작업대 옆 발판 8.5칸 (위 잉크, 아래 무기) / 간격 2.5칸 / 가격은 아이템 바로 아래 / 그림은 상점 가방과 같은 승인 그림')
    return c


def option_b():
    """no jumping: inks on the desk itself, weapons on the floor right of the desk"""
    c = base(); actors(c)
    dk_top = FLOOR - 78 + 18
    for i, x in enumerate([X - 46, X - 4, X + 38]):
        s = place(c, ink(INKS[i]), x, dk_top, glow=False); price_tag(c, x, s - 17, IP[i])
    xs = [X + 118 + i * 52 for i in range(3)]
    for i, x in enumerate(xs):
        s = place(c, weapon(WEAPONS[i]), x, FLOOR); price_tag(c, x, s - 17, WP[i])
    hero(c, xs[0] - 4)
    card(c, xs[0], FLOOR - 92, '목탄', '대형 목탄', '크게 터지는 대형 목탄을 던진다.', 400)
    label(c, 'B  점프 없음 — 잉크는 작업대 위, 무기는 작업대 옆 바닥 — 가격은 아이템 위 어두운 꼬리표')
    return c


def option_c():
    """two rows on the backdrop's right wall: a low ledge (decor) for inks, staggered weapons on the floor"""
    c = base(); actors(c)
    top = FLOOR - 2 * U - 8
    d = ImageDraw.Draw(c); x0, x1 = X + 96, X + 96 + int(8.5 * U)
    d.rectangle((x0, top, x1, top + 7), fill=Wh, outline=K, width=2)
    for bx in (x0 + 10, x1 - 14): d.polygon([(bx, top + 7), (bx + 6, top + 7), (bx + 6, top + 18)], fill=D)
    inks = [x0 + 96 + i * 80 for i in range(3)]; weps = [x - 40 for x in inks]
    for i, x in enumerate(weps):
        place(c, weapon(WEAPONS[i]), x, FLOOR); price_under(c, x, FLOOR + 6, WP[i])
    for i, x in enumerate(inks):
        place(c, ink(INKS[i]), x, top); price_under(c, x, top + 9, IP[i])
    hero(c, inks[0] + 2)
    k = SHOWN[-3]; card(c, inks[0], top - 52, k['name'], '+5회', short(k['description']), 220, subcol=GOLD)
    label(c, 'B  점프 없음 / 뒷벽 선반 8.5칸에 잉크, 바닥 무기와 1.25칸 엇갈림 / 바닥에서 가장 가까운 것을 F')
    return c


if __name__ == '__main__':
    shots = [option_a(), option_c()]
    for name, s in zip('AB', shots):
        s.resize((W * 2, H * 2), Image.NEAREST).save(OUT / f'시안-{name}.png')
    sheet = Image.new('RGBA', (W, H * 2 + 4), (255, 255, 255, 255))
    for i, s in enumerate(shots): sheet.alpha_composite(s, (0, i * (H + 4)))
    sheet.resize((W * 2, sheet.height * 2), Image.NEAREST).save(OUT / '시안-비교.png')
