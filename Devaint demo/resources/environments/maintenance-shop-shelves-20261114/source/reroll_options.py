"""리롤(공용 새로고침) 자리 디자인 시안 네 가지, 두 챕터. 진열 줄 끝 오른쪽만 잘라 2배로.

    python reroll_options.py <워크트리 루트>

A 큰 종 매달기: 종을 새로 크게 그려 아래 줄은 선반 밑에, 위 줄은 천장에서 사슬로 늘어뜨림
B 새로고침 팻말: 상품 받침과 같은 판 위에 둥근 화살표 팻말, 판에는 남은 횟수
C 종 + 받침판: 지금 종을 상품과 같은 받침판에 올리고 판에 둥근 화살표와 남은 횟수
D 큰 종 + 받침판: A 의 큰 종을 받침판에 세우고 남은 횟수
"""
import math
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import mock as m
import final_scenes as fs
from PIL import Image, ImageDraw

def make_bell():
    """a bell about item size: 19 dots wide, 18 tall (38x36 px), a loop on top, flared rim, clapper"""
    half = [1, 2, 3, 4, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7, 8, 9]
    W = 19; c0 = W // 2; rows = ['.' * (c0 - 1) + 'O.O' + '.' * (c0 - 1), '.' * c0 + 'O' + '.' * c0]
    for y, hw in enumerate(half):
        row = ['.'] * W
        for x in range(c0 - hw, c0 + hw + 1):
            dx = x - (c0 - hw)
            row[x] = 'O' if x in (c0 - hw, c0 + hw) or y == 0 else 'H' if dx in (1, 2) and y > 1 else 'S' if x >= c0 + hw - 2 else 'M' if y == len(half) - 2 else 'F'
        rows.append(''.join(row))
    rows.append('O' * W)
    rows.append('.' * (c0 - 1) + 'OMO' + '.' * (c0 - 1)); rows.append('.' * c0 + 'O' + '.' * c0)
    return rows


BELL = make_bell()


def stamp(c, rows, x0, y0, pal, dot=2):
    d = ImageDraw.Draw(c)
    for yy, row in enumerate(rows):
        for xx, ch in enumerate(row):
            col = pal.get(ch)
            if col: d.rectangle((x0 + xx * dot, y0 + yy * dot, x0 + xx * dot + dot - 1, y0 + yy * dot + dot - 1), fill=col)


def big_bell(c, cx, bottom, pal):
    w, h = len(BELL[0]) * 2, len(BELL) * 2
    stamp(c, BELL, cx - w // 2, bottom - h, pal); return bottom - h


def chain(c, x, y0, y1, col):
    d = ImageDraw.Draw(c)
    for y in range(y0, y1, 6):
        d.rectangle((x - 1, y, x, y + 3), outline=col)


ARROW = ['...OOOO...',
         '.OO....O.O',
         '.O......OO',
         'O......OOO',
         'O.........',
         'O........O',
         'O........O',
         '.O......O.',
         '.OO....OO.',
         '...OOOO...']


def arrow_ring(c, cx, cy, col, dot=2):
    """a clockwise refresh arrow (10x10 dots), head at the top right"""
    n = len(ARROW) * dot; stamp(c, ARROW, round(cx - n / 2), round(cy - n / 2), {'O': col}, dot)


def count_plate(c, cx, ground, left, plate, text_col, w=48):
    y0 = fs.stand(c, cx, ground, None, *plate[:4], text_col, w=w)
    d = ImageDraw.Draw(c); t = f'{left}회'; tw = d.textlength(t, font=m.PIX)
    tot = 10 + 4 + tw; x = round(cx - tot / 2)
    arrow_ring(c, x + 5, y0 + 8, text_col, dot=1)
    d.text((x + 15, y0 + 2), t, font=m.PIX, fill=text_col)
    return y0


def board(c, cx, ground, pal, w=30, h=34):
    """a little standing notice board with a refresh arrow, on two short legs"""
    d = ImageDraw.Draw(c); x0, y0 = cx - w // 2, ground - h
    for lx in (x0 + 6, x0 + w - 8): d.rectangle((lx, ground - 8, lx + 1, ground - 1), fill=pal['O'])
    d.rectangle((x0, y0, x0 + w - 1, ground - 9), fill=pal['F'], outline=pal['O'])
    d.rectangle((x0 + 2, y0 + 2, x0 + w - 3, y0 + 3), fill=pal['H']); d.rectangle((x0 + 2, ground - 12, x0 + w - 3, ground - 11), fill=pal['S'])
    for px_, py_ in ((x0, y0), (x0 + w - 1, y0), (x0, ground - 9), (x0 + w - 1, ground - 9)): d.point((px_, py_), fill=(0, 0, 0, 0))
    arrow_ring(c, cx, y0 + (h - 9) / 2, pal['O'])


class Ch1:
    name = '1챕터'

    def __init__(self):
        import ch1_scene_new as sc, ch1_building as cb
        self.sc, self.cb = sc, cb
        self.pal = {'O': cb.OUT, 'F': cb.FACE, 'H': cb.HI, 'S': cb.SH, 'M': cb.MARK}
        self.bell = {'O': cb.OUT, 'F': cb.SH, 'H': cb.FACE, 'S': cb.MARK, 'M': cb.OUT}       # grey metal: reads against the cream wall
        self.plate = (cb.FACE, cb.HI, cb.SH, cb.OUT); self.text = cb.OUT

    def base(self):
        sc = self.sc; sc.HIDE, sc.SOLD, sc.SWAP, sc.BELLS, sc.NO_HERO = {('b', 'w'), ('b', 'i')}, set(), {}, True, True
        c, W = sc.scene(('none', 0), ''); r = dict(sc.RET); r['ceil'] = m.FLOOR - self.cb.OH * 2 + 2; return c, r


class Ch2:
    name = '2챕터'

    def __init__(self):
        import ch2_recess as sc, pal
        self.sc = sc; W = pal.W
        self.pal = {'O': pal.INK, 'F': W[8], 'H': W[10], 'S': W[6], 'M': W[5]}
        self.bell = self.pal
        self.plate = (pal.STEEL[1], pal.STEEL[3], pal.STEEL[0], pal.INK); self.text = pal.PAPER[3]

    def base(self):
        sc = self.sc; sc.HIDE, sc.SOLD, sc.SWAP, sc.BELLS, sc.NO_HERO = {('b', 'w'), ('b', 'i')}, set(), {}, True, True
        c = sc.scene('merged', ('none', 0)); r = dict(sc.RET); r['ceil'] = sc.FLOOR - 190 + 4; return c, r


def option(ch, kind):
    c, r = ch.base(); bw, bi = r[('b', 'w')], r[('b', 'i')]
    small = bw['sprite']; O = ch.pal['O']
    for b, row in ((bw, 'w'), (bi, 'i')):
        x, g = b['x'], b['ground']
        if kind == 'A':
            top = (r['shelf_y'] + 11) if row == 'w' else r['ceil']
            bottom = g - 22 if row == 'w' else g - 26
            chain(c, x, top, bottom - len(BELL) * 2 + 2, O); big_bell(c, x, bottom, ch.bell)
        elif kind == 'B':
            board(c, x, count_plate(c, x, g, 2, ch.plate, ch.text), ch.pal)
        elif kind == 'C':
            y0 = count_plate(c, x, g, 2, ch.plate, ch.text); c.alpha_composite(small, (x - small.width // 2, y0 - small.height))
        elif kind == 'D':
            y0 = count_plate(c, x, g, 2, ch.plate, ch.text); big_bell(c, x, y0, ch.bell)
    xs = r['xs']; crop = (xs[1] - 50, r['floor'] - 200, xs[3] + 60, r['floor'] + 10)
    return c.crop(crop)


TITLES = {'A': 'A 큰 종 매달기(위는 천장, 아래는 선반 밑)', 'B': 'B 둥근 화살표 팻말 + 받침판 남은 횟수',
          'C': 'C 지금 종 + 받침판(둥근 화살표, 남은 횟수)', 'D': 'D 큰 종 + 받침판 남은 횟수'}

if __name__ == '__main__':
    for Ch in (Ch1, Ch2):
        ch = Ch(); tiles = [(k, option(ch, k)) for k in 'ABCD']
        w, h = tiles[0][1].size; S = 2; pad = 26
        sheet = Image.new('RGBA', (w * S * 2 + 12, (h * S + pad) * 2 + 4), (23, 23, 17, 255)); d = ImageDraw.Draw(sheet)
        for i, (k, t) in enumerate(tiles):
            x, y = (i % 2) * (w * S + 12), (i // 2) * (h * S + pad)
            d.text((x + 8, y + 4), TITLES[k], font=m.UI, fill=(255, 255, 255))
            sheet.paste(t.resize((w * S, h * S), Image.NEAREST), (x, y + pad))
        sheet.save(HERE / f'{ch.name}-리롤-시안.png'); print(ch.name, sheet.size)
