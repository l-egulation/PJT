"""정비소 진열 구매·새로고침 연출 시안(움직이는 그림). 게임 화소(1x)로 그려 2배로 키운다.

    python anim.py <워크트리 루트>

구매: F → 카드 닫힘 → 상품이 눌렸다 튀어오르며 하얗게 번쩍 → 주인공 가슴으로 포물선을 그리며 작아져 빨려듦(반짝 꼬리)
      → 주인공에게 작은 점 터짐 / 가격판은 비고 그 자리에서 「-가격」 금색 숫자가 떠오르며 사라짐.
새로고침: F → 종이 좌우로 흔들리며 소리 줄 → 그 줄의 안 산 상품이 차례로 먼지 고리 속에 사라졌다 새 상품이 통 튀어나옴.
          이미 산 자리(빈 판)는 그대로. 카드의 남은 횟수가 하나 줄어듦.
"""
import math
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import mock as m
import final_scenes as fs
import reroll_objects as ro_obj
from PIL import Image, ImageDraw, ImageFont

FPS_MS = 40
GOLD = (224, 204, 122)
NUM = ImageFont.truetype(str(m.FONTS / 'Inventory/Resources/PixelInventory/Fonts/Galmuri11-Bold.ttf'), 11)


def ease_out(t): return 1 - (1 - t) ** 2


def scaled(sp, sx, sy):
    return sp.resize((max(1, round(sp.width * sx)), max(1, round(sp.height * sy))), Image.NEAREST)


def flash(sp, k):
    """white over the sprite's own shape, k = 0..1"""
    w = Image.new('RGBA', sp.size, (255, 255, 255, 0)); a = sp.split()[3].point(lambda v: int(v * k))
    w.putalpha(a); out = sp.copy(); out.alpha_composite(w); return out


def put_bottom(c, sp, cx, bottom):
    c.alpha_composite(sp, (round(cx - sp.width / 2), round(bottom - sp.height)))


def accent(sp):
    """the piece's most common saturated colour, for its sparkle trail"""
    best, cnt = (240, 236, 226), {}
    for p in sp.getdata():
        if p[3] < 200: continue
        mx, mn = max(p[:3]), min(p[:3])
        if mx - mn > 60: cnt[p[:3]] = cnt.get(p[:3], 0) + 1
    return max(cnt, key=cnt.get) if cnt else best


def dots(c, cx, cy, r, n, col, size=2, rot=0.0, alpha=255):
    d = ImageDraw.Draw(c)
    for i in range(n):
        a = rot + i * 2 * math.pi / n; x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        d.rectangle((round(x), round(y), round(x) + size - 1, round(y) + size - 1), fill=col + (alpha,))


def float_text(c, text, cx, y, alpha):
    lay = Image.new('RGBA', c.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    tw = d.textlength(text, font=NUM)
    for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)): d.text((cx - tw / 2 + dx, y + dy), text, font=NUM, fill=(63, 62, 60, alpha))
    d.text((cx - tw / 2, y), text, font=NUM, fill=GOLD + (alpha,)); c.alpha_composite(lay)


def save(frames, name, crop, holds):
    out = [f.crop(crop).resize(((crop[2] - crop[0]) * 2, (crop[3] - crop[1]) * 2), Image.NEAREST) for f in frames]
    durs = [FPS_MS] * len(out)
    for i, ms in holds.items(): durs[i] = ms
    rgb = [o.convert('RGB') for o in out]
    # one shared adaptive palette from a strip of every frame (fewer colour jumps between frames)
    strip = Image.new('RGB', (rgb[0].width, rgb[0].height * min(len(rgb), 8)))
    for i, f in enumerate(rgb[::max(1, len(rgb) // 8)][:8]): strip.paste(f, (0, i * f.height))
    pal = strip.quantize(255, method=Image.MEDIANCUT)
    q = [f.quantize(palette=pal, dither=Image.NONE) for f in rgb]
    q[0].save(HERE / f'{name}.gif', save_all=True, append_images=q[1:], duration=durs, loop=0, disposal=1)
    out[0].save(HERE / f'{name}.webp', save_all=True, append_images=out[1:], duration=durs, loop=0, lossless=True)
    sheet_n = [0, len(out) // 4, len(out) // 2, len(out) * 3 // 4]
    return name


# ------------------------------------------------------------------ chapter hooks
class Ch1:
    def __init__(self):
        import ch1_scene_new as sc; import ch1_building as cb
        self.sc, self.cb = sc, cb; self.plate = (cb.FACE, cb.HI, cb.SH, cb.OUT, cb.OUT); self.name = '1챕터'

    def base(self, hide=(), sold=(), swap=None, bells=False):
        sc = self.sc
        sc.HIDE, sc.SOLD, sc.SWAP, sc.BELLS, sc.NO_HERO = set(hide), set(sold), dict(swap or {}), bells, True
        c, W = sc.scene(('none', 0), ''); return c, dict(sc.RET)

    def crop(self, ret):
        x0 = ret['xs'][0] - 80; return (x0, m.FLOOR - 262, ret['xs'][-1] + 80 if len(ret['xs']) > 3 else x0 + 420, m.FLOOR + 12)


class Ch2:
    def __init__(self):
        import ch2_recess as sc
        self.sc = sc; import pal; self.plate = (pal.STEEL[1], pal.STEEL[3], pal.STEEL[0], pal.INK, pal.PAPER[3]); self.name = '2챕터'

    def base(self, hide=(), sold=(), swap=None, bells=False):
        sc = self.sc
        sc.HIDE, sc.SOLD, sc.SWAP, sc.BELLS, sc.NO_HERO = set(hide), set(sold), dict(swap or {}), bells, True
        c = sc.scene('merged', ('none', 0)); return c, dict(sc.RET)

    def crop(self, ret):
        x0 = ret['xs'][0] - 80; return (x0, ret['floor'] - 262, ret['xs'][-1] + 80 if len(ret['xs']) > 3 else x0 + 420, ret['floor'] + 12)


# ------------------------------------------------------------------ purchase
def purchase(ch, key, card_args):
    """key = ('i', k) or ('w', k)"""
    still, ret = ch.base()
    empty, _ = ch.base(hide={key}, sold={key})                            # after: plate emptied, piece gone
    it = ret[key]; sp, x, py = it['sprite'], it['x'], it['y']
    hero_x, hero_floor = x + 40, it['ground'] if key[0] == 'w' else ret['shelf_y']
    if key[0] == 'w': hero_floor = ret['floor']
    chest = (hero_x - 2, hero_floor - 44)
    col = accent(sp)
    frames = []
    T_PRESS, T_FLY, T_END = 8, 16, 27
    for t in range(48):
        bg = still if t < T_PRESS + 1 else empty
        c = bg.copy()
        if t <= T_PRESS: pass                                             # piece is on the still frame
        m.hero(c, hero_x, hero_floor)
        if t < T_PRESS:
            fs.card(c, x, py - sp.height, *card_args, ret['W'])
        elif t == T_PRESS:                                                # F pressed: the card's key goes in
            fs.card(c, x, py - sp.height, *card_args, ret['W'])
        elif t <= 10:                                                     # squash on the plate
            put_bottom(c, flash(scaled(sp, 1.14, .8), .35), x, py)
        elif t < T_FLY:                                                   # hop up, stretched, white flash
            u = (t - 10) / (T_FLY - 10); lift = 16 * ease_out(u)
            s = scaled(sp, .92, 1.12) if t < 13 else sp
            put_bottom(c, flash(s, .85 if t < 13 else .3), x, py - lift)
            if t == 11: dots(c, x, py - 4, 14, 6, (255, 255, 255), 2, .3)
        elif t <= T_END - 2:                                              # arc into the hero, shrinking, sparkle trail
            u = (t - T_FLY) / (T_END - 2 - T_FLY); e = u * u
            sx, sy = x, py - 16 - sp.height / 2; ex, ey = chest; mx, my = (sx + ex) / 2, min(sy, ey) - 40
            px_ = (1 - e) ** 2 * sx + 2 * (1 - e) * e * mx + e * e * ex; py_ = (1 - e) ** 2 * sy + 2 * (1 - e) * e * my + e * e * ey
            for back in (1, 2, 3):
                ub = max(0, u - back * .08); eb = ub * ub
                bx = (1 - eb) ** 2 * sx + 2 * (1 - eb) * eb * mx + eb * eb * ex; by = (1 - eb) ** 2 * sy + 2 * (1 - eb) * eb * my + eb * eb * ey
                dots(c, bx, by, 0, 1, col if back % 2 else (255, 255, 255), 3 - back // 2)
            s = scaled(sp, 1 - .62 * u, 1 - .62 * u)
            c.alpha_composite(s, (round(px_ - s.width / 2), round(py_ - s.height / 2)))
        elif t <= T_END + 4:                                              # caught: a ring of dots off the hero
            k = t - (T_END - 1); dots(c, chest[0], chest[1], 4 + k * 3, 8, (255, 255, 255), 2, k * .2, 255 - k * 30)
            dots(c, chest[0], chest[1], 2 + k * 2, 4, col, 2, .78 + k * .2, 255 - k * 40)
        if T_PRESS < t < T_PRESS + 20:                                    # price leaves the plate
            u = (t - T_PRESS - 1) / 19
            float_text(c, f'-{it["price"]}', x, py - 4 - 22 * ease_out(u), int(255 * (1 if u < .6 else (1 - u) / .4)))
        frames.append(c)
    return frames, ch.crop(ret), {0: 600, len(frames) - 1: 900}


# ------------------------------------------------------------------ reroll
def reroll(ch, row, fresh):
    """row = 'i' or 'w'; the first piece of the row is already sold (stays sold); fresh = [(sprite, price)] for the others"""
    sold = {(row, 0)}
    still, ret = ch.base(sold=sold, bells=True)
    keys = [(row, 1), (row, 2)]
    swap = {k: f for k, f in zip(keys, fresh)}
    bell_key = ('b', row); b = ret[bell_key]
    hero_floor = ret['shelf_y'] if row == 'i' else ret['floor']
    hero_x = b['x'] + 36                                                   # just past the crate
    PAL = ro_obj.palette(ch)
    title = '잉크 새로고침' if row == 'i' else '무기 새로고침'
    frames = []
    T_PRESS = 8; starts = {keys[0]: 14, keys[1]: 19}
    for t in range(52):
        busy = {k for k, s in starts.items() if s <= t < s + 8}             # drawn by hand while it changes
        new = {k for k, s in starts.items() if t >= s + 4}
        bg, _ = ch.base(sold=sold, bells=True, hide={bell_key} | busy, swap={k: swap[k] for k in new})
        c = bg
        m.hero(c, hero_x, hero_floor)
        # the supply crate on its count plate; the row's own crate rattles when struck
        left = 2 if t <= T_PRESS + 4 else 1
        for rk in ('w', 'i'):
            bb = ret[('b', rk)]; y0 = ro_obj.plate_count(c, bb['x'], bb['ground'], left, ch.plate[:4], ch.plate[4])
            dx = dy = 0; sq = 1.0
            if rk == row and T_PRESS < t <= T_PRESS + 10:
                k = t - T_PRESS; dx = [0, -2, 2, -2, 2, -1, 1, 0, 0, 0][k - 1]; dy = [-4, -6, -4, -2, 0, 0, 0, 0, 0, 0][k - 1]
            lay = Image.new('RGBA', c.size, (0, 0, 0, 0)); ro_obj.crate(lay, bb['x'] + dx, y0 + dy, PAL)
            c.alpha_composite(lay)
            if rk == row and T_PRESS + 4 <= t <= T_PRESS + 8:                    # landing dust at the crate's foot
                u = (t - T_PRESS - 4) / 4
                for sgn in (-1, 1): dots(c, bb['x'] + sgn * (22 + 6 * u), y0 - 3 - 2 * u, 0, 1, (204, 199, 190), 3, 0, int(255 * (1 - u)))
        for k, s in starts.items():
            it = ret[k]; x, py = it['x'], it['y']
            if s <= t < s + 4:                                            # old piece shrinks into a dust ring
                u = (t - s) / 4; sp = scaled(it['sprite'], 1 - u, 1 - u)
                if sp.width > 2: put_bottom(c, flash(sp, u), x, py - it['sprite'].height * u / 2)
                dots(c, x, py - it['sprite'].height / 2, 8 + 10 * u, 8, (204, 199, 190), 3, u, int(255 * (1 - u * .5)))
            if s + 4 <= t < s + 8:                                        # new piece pops with an overshoot
                u = (t - s - 4) / 4; sc_ = [.4, 1.18, .95, 1.0][t - s - 4]
                nsp = swap[k][0]
                put_bottom(c, flash(scaled(nsp, sc_, sc_), .5 * (1 - u)), x, py)
                dots(c, x, py - nsp.height / 2, 18 + 6 * u, 8, (255, 255, 255), 2, .4, int(255 * (1 - u)))
        frames.append(c)
    return frames, ch.crop(ret), {0: 600, len(frames) - 1: 1100}


if __name__ == '__main__':
    # purchases have no animation (user 2026-11-14); only the reroll is drawn now
    out = []
    for Ch in (Ch1, Ch2):
        ch = Ch()
        ch.base(); fresh = [(m.ink('blue'), 220), (m.ink('yellow'), 220)]
        f, crop, holds = reroll(ch, 'i', fresh)
        out.append(save(f, f'{ch.name}-새로고침-잉크', crop, holds))
    print(out)
