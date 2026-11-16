"""리롤 자리: 둥근 화살표를 정비소 물건에 새긴 시안 네 가지, 두 챕터. 물건은 상품과 같은 받침판 위, 판에는 남은 횟수.

    python reroll_objects.py <워크트리 루트>

A 보급 궤짝: 판자 궤짝 옆면에 화살표 낙인
B 크랭크 바퀴: 기둥에 단 바퀴 면에 화살표, 오른쪽 위에 손잡이
C 레버 상자: 상자 앞면에 화살표, 위로 비스듬한 레버와 손잡이 공
D 작은 통: 테 두른 통에 화살표 종이 딱지
"""
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import mock as m
import final_scenes as fs
import reroll_options as ro
from PIL import Image, ImageDraw

SMALL = ['..OOO.O',
         '.O...OO',
         'O...OOO',
         'O......',
         'O.....O',
         '.O...O.',
         '..OOO..']


class Dots:
    """2px-dot drawing with the bottom-centre of the object as the anchor"""
    def __init__(self, c, cx, bottom, w, h, dot=2):
        self.d = ImageDraw.Draw(c); self.c = c; self.dot = dot
        self.x0 = round(cx - w * dot / 2); self.y0 = bottom - h * dot

    def p(self, x, y, col):
        X, Y = self.x0 + x * self.dot, self.y0 + y * self.dot
        self.d.rectangle((X, Y, X + self.dot - 1, Y + self.dot - 1), fill=col)

    def r(self, x0, y0, x1, y1, col):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): self.p(x, y, col)

    def box(self, x0, y0, x1, y1, line, fill):
        self.r(x0, y0, x1, y1, fill)
        for x in range(x0, x1 + 1): self.p(x, y0, line); self.p(x, y1, line)
        for y in range(y0, y1 + 1): self.p(x0, y, line); self.p(x1, y, line)

    def pattern(self, rows, x0, y0, col):
        for yy, row in enumerate(rows):
            for xx, ch in enumerate(row):
                if ch == 'O': self.p(x0 + xx, y0 + yy, col)


def crate(c, cx, bottom, P):
    D = Dots(c, cx, bottom, 20, 18)
    D.box(0, 0, 19, 17, P['O'], P['F'])
    D.r(1, 1, 18, 1, P['H']); D.r(1, 16, 18, 16, P['S'])
    for y in (6, 11): D.r(1, y, 18, y, P['M'])                                  # plank joins
    for x, y in ((1, 1), (18, 1), (1, 16), (18, 16)): D.p(x, y, P['O'])        # corner nails
    D.box(3, 2, 16, 15, P['O'], P['L'])                                         # the stamped panel, one dot clear of the arrow
    D.pattern(ro.ARROW, 5, 4, P['A2'])


def crank(c, cx, bottom, P):
    D = Dots(c, cx, bottom, 22, 24)
    D.r(6, 22, 15, 23, P['O']); D.r(7, 22, 14, 22, P['M'])                      # foot
    D.r(10, 14, 11, 21, P['O'])                                                 # post
    rr = 9.3; cxd, cyd = 11, 10
    for y in range(0, 21):
        for x in range(0, 22):
            dd = ((x - cxd + .5) ** 2 + (y - cyd + .5) ** 2) ** .5
            if dd <= rr: D.p(x, y, P['O'] if dd > rr - 1.1 else P['S'] if dd > rr - 2.1 else P['F'])
    D.pattern(ro.ARROW, 6, 5, P['A'])
    D.r(19, 1, 20, 4, P['O']); D.r(18, 0, 21, 1, P['O']); D.p(19, 2, P['H'])  # handle knob at the rim


def lever(c, cx, bottom, P):
    D = Dots(c, cx, bottom, 20, 22)
    for i in range(9): D.p(10 + i // 2, 9 - i, P['O']); D.p(11 + i // 2, 9 - i, P['M'])   # the stick, leaning right
    D.r(14, 0, 16, 2, P['O']); D.p(15, 1, P['H'])                             # ball
    D.box(0, 9, 19, 21, P['O'], P['F'])
    D.r(1, 10, 18, 10, P['H']); D.r(1, 20, 18, 20, P['S'])
    D.r(8, 9, 12, 9, P['M'])                                                    # slot
    D.pattern(ro.ARROW, 5, 11, P['A'])


def drum(c, cx, bottom, P):
    D = Dots(c, cx, bottom, 18, 20)
    for y in range(20):
        bulge = 1 if 4 <= y <= 15 else 0
        x0, x1 = 1 - bulge, 16 + bulge
        for x in range(x0 + 1, x1): D.p(x, y, P['F'])
        D.p(x0, y, P['O']); D.p(x1, y, P['O'])
    D.r(1, 0, 16, 0, P['O']); D.r(1, 19, 16, 19, P['O'])
    for y in (2, 17): D.r(1, y, 16, y, P['M'])                                  # hoops
    D.r(1, 1, 16, 1, P['H'])
    D.box(2, 3, 15, 16, P['O'], P['L'])                                         # paper label, one dot clear of the arrow
    D.pattern(ro.ARROW, 4, 5, P['A2'])


def cloth_palette(ch):
    if ch.name == '1챕터':
        cb = ch.cb
        return {'O': cb.OUT, 'F': cb.SH, 'H': cb.FACE, 'S': cb.MARK, 'M': cb.OUT}
    import pal
    return {'O': pal.INK, 'F': pal.PAPER[1], 'H': pal.PAPER[2], 'S': pal.PAPER[0], 'M': pal.W[5]}


def cover(c, cx, bottom, P):
    """a place not yet unlocked: a cloth thrown over a lump, folds down its sides, tied with a cord"""
    half = [3, 5, 6, 7, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12]
    D = Dots(c, cx, bottom, 26, len(half) + 1); c0 = 13
    for y, hw in enumerate(half):
        for x in range(c0 - hw, c0 + hw):
            edge = x in (c0 - hw, c0 + hw - 1) or y == 0
            fold = (x - c0) in (-6, -1, 5) and y > 4
            D.p(x, y, P['O'] if edge else P['S'] if fold else P['H'] if x - (c0 - hw) == 1 and y > 1 else P['F'])
    D.r(c0 - 12, len(half), c0 + 11, len(half), P['O'])
    for x in range(c0 - 9, c0 + 9): D.p(x, 9 + (1 if (x // 3) % 2 else 0), P['M'])          # the cord around it
    D.r(c0 + 6, 10, c0 + 7, 13, P['M'])                                                      # its knot ends


OBJECTS = {'A': ('A 보급 궤짝(옆면 화살표 낙인)', crate), 'B': ('B 크랭크 바퀴(바퀴 면 화살표, 손잡이)', crank),
           'C': ('C 레버 상자(앞면 화살표, 레버)', lever), 'D': ('D 작은 통(화살표 종이 딱지)', drum)}


def palette(ch):
    if ch.name == '1챕터':
        cb = ch.cb
        # grey wood/metal so the object reads against the cream wall (the 8-colour shop palette only)
        return {'O': cb.OUT, 'F': cb.SH, 'H': cb.FACE, 'S': cb.MARK, 'M': cb.MARK, 'A': cb.OUT, 'L': cb.HI, 'A2': cb.OUT}
    import pal
    W = pal.W
    return {'O': pal.INK, 'F': W[7], 'H': W[9], 'S': W[5], 'M': W[4], 'A': pal.PAPER[3], 'L': pal.PAPER[2], 'A2': pal.INK}


def plate_count(c, cx, ground, left, plate, text_col, w=48):
    y0 = fs.stand(c, cx, ground, None, *plate[:4], text_col, w=w)
    if left is None: return y0
    d = ImageDraw.Draw(c); t = f'{left}회'; tw = d.textlength(t, font=m.PIX)
    d.text((round(cx - tw / 2), y0 + 2), t, font=m.PIX, fill=text_col)
    return y0


def option(ch, key):
    c, r = ch.base(); P = palette(ch); draw = OBJECTS[key][1]
    for b in (r[('b', 'w')], r[('b', 'i')]):
        y0 = plate_count(c, b['x'], b['ground'], 2, ch.plate, ch.text)
        draw(c, b['x'], y0, P)
    xs = r['xs']; return c.crop((xs[1] - 50, r['floor'] - 200, xs[3] + 60, r['floor'] + 10))


if __name__ == '__main__':
    for Ch in (ro.Ch1, ro.Ch2):
        ch = Ch(); tiles = [(k, option(ch, k)) for k in 'ABCD']
        w, h = tiles[0][1].size; S = 2; pad = 26
        sheet = Image.new('RGBA', (w * S * 2 + 12, (h * S + pad) * 2 + 4), (23, 23, 17, 255)); d = ImageDraw.Draw(sheet)
        for i, (k, t) in enumerate(tiles):
            x, y = (i % 2) * (w * S + 12), (i // 2) * (h * S + pad)
            d.text((x + 8, y + 4), OBJECTS[k][0], font=m.UI, fill=(255, 255, 255))
            sheet.paste(t.resize((w * S, h * S), Image.NEAREST), (x, y + pad))
        sheet.save(HERE / f'{ch.name}-리롤-물건.png'); print(ch.name)
