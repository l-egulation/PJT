"""최종 시안 한눈에: 두 챕터 × (기본, 장인 말풍선, 위 칸 잉크 카드, 아래 칸 무기 카드, 리롤 궤짝 앞). 리롤 해금 런 기준(줄 끝 궤짝).

    python overview.py <워크트리 루트>
"""
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import mock as m
from PIL import Image, ImageDraw
import ch1_scene_new as s1
import ch2_recess as s2

STATES = [(('none', 1), '기본'), (('talk', 0), '첫 방문 말풍선(예전 말풍선)'), (('i', 1), '위 칸 잉크에 다가감'),
          (('w', 0), '아래 칸 무기에 다가감'), (('b', 0), '리롤 궤짝 앞(설명 카드 없음)')]


def ch1(focus):
    s1.HIDE, s1.SOLD, s1.SWAP, s1.BELLS, s1.NO_HERO = set(), set(), {}, True, False
    img, W = s1.scene(focus, ''); return img.crop((0, 22, W, img.height))


def ch2(focus):
    s2.HIDE, s2.SOLD, s2.SWAP, s2.BELLS, s2.NO_HERO = set(), set(), {}, True, False
    m.W = s2.W; img = s2.scene('merged', focus); return img.crop((0, 20, img.width, img.height))


if __name__ == '__main__':
    rows = []
    for focus, title in STATES:
        a, b = ch1(focus), ch2(focus)
        a.resize((a.width * 2, a.height * 2), Image.NEAREST).save(HERE / f'최종-1챕터-{title.split("(")[0].replace(" ", "")}.png')
        b.resize((b.width * 2, b.height * 2), Image.NEAREST).save(HERE / f'최종-2챕터-{title.split("(")[0].replace(" ", "")}.png')
        rows.append((title, a, b))
    w = rows[0][1].width + rows[0][2].width + 12; hh = max(r[1].height for r in rows); pad = 24
    sheet = Image.new('RGB', (w, 34 + len(rows) * (hh + pad)), (23, 23, 17)); d = ImageDraw.Draw(sheet)
    d.text((8, 8), '정비소 진열 최종 시안   왼쪽 1챕터, 오른쪽 2챕터 / 리롤 해금 런 기준', font=m.UI, fill=(255, 255, 255))
    for i, (title, a, b) in enumerate(rows):
        y = 34 + i * (hh + pad); d.text((8, y + 4), title, font=m.UI, fill=(255, 220, 120))
        sheet.paste(a.convert('RGB'), (0, y + pad)); sheet.paste(b.convert('RGB'), (a.width + 12, y + pad))
    sheet.save(HERE / '최종-한눈에.png'); print(sheet.size)
