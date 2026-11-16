"""상점 진열 확장 시안: 간격(약 2.45칸)은 그대로 두고 진열칸을 넓힌다. 공용 리롤 해금 런(줄 끝 궤짝) 기준.

    python expand_mock.py <워크트리 루트>

A 단계마다 길어짐: 기본(3개) / 확장 1단계(4개) / 확장 2단계(5개). 그림이 단계마다 넓어진다.
B 처음부터 긴 진열칸: 아직 안 연 자리는 천으로 덮어 둔다. 기본(3개 + 덮인 2자리) / 1단계(4개 + 1자리) / 2단계(5개).
"""
import os
import subprocess
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
ROOT = sys.argv[1]
PY = sys.executable
TMP = HERE / '_expand'; TMP.mkdir(exist_ok=True)

CH1 = """
import sys; sys.argv=[sys.argv[0], {root!r}]; sys.path.insert(0, {here!r})
import ch1_scene_new as s
s.ITEMS, s.COVER, s.BELLS = {items}, {cover}, True
img, W = s.scene(('none', 1), '')
img.crop((0, 22, W, img.height)).save({out!r})
"""
CH2 = """
import sys; sys.argv=[sys.argv[0], {root!r}]; sys.path.insert(0, {here!r})
import mock as m, ch2_recess as s
m.W = s.W; s.ITEMS, s.COVER, s.BELLS = {items}, {cover}, True
img = s.scene('merged', ('none', 0))
img.crop((0, 20, img.width, img.height)).save({out!r})
"""


def run(code, env):
    e = dict(os.environ, **env)
    subprocess.run([PY, '-c', code], check=True, env=e, cwd=HERE)


def render(extra_cols, items, cover):
    out1 = str(TMP / f'ch1-{extra_cols}-{items}-{cover}.png'); out2 = str(TMP / f'ch2-{extra_cols}-{items}-{cover}.png')
    building = f'_expand/building-x{extra_cols}.png'
    env1 = {'SHOP_EXTRA_DOTS': str(39 * extra_cols), 'SHOP_BUILDING': building}
    if not (HERE / building).exists():
        subprocess.run([PY, 'ch1_building.py', ROOT], check=True, env=dict(os.environ, **env1), cwd=HERE)
    run(CH1.format(root=ROOT, here=str(HERE), items=items, cover=cover, out=out1), env1)
    run(CH2.format(root=ROOT, here=str(HERE), items=items, cover=cover, out=out2), {'SHOP_EXTRA_PX': str(76 * extra_cols)})
    return out1, out2


if __name__ == '__main__':
    from PIL import Image, ImageDraw
    sys.argv = [sys.argv[0], ROOT]; sys.path.insert(0, str(HERE))
    import mock as m
    rows = [('A 단계마다 길어짐  기본 3개 + 궤짝', render(0, 3, 0)),
            ('A  확장 1단계 4개 + 궤짝', render(1, 4, 0)),
            ('A  확장 2단계 5개 + 궤짝', render(2, 5, 0)),
            ('B 처음부터 긴 진열칸  기본 3개 + 천 덮인 2자리 + 궤짝', render(2, 3, 2)),
            ('B  확장 1단계 4개 + 천 덮인 1자리 + 궤짝', render(2, 4, 1))]
    ims = [(t, Image.open(a), Image.open(b)) for t, (a, b) in rows]
    w1 = max(a.width for _, a, _ in ims); w2 = max(b.width for _, _, b in ims); hh = max(max(a.height, b.height) for _, a, b in ims)
    pad = 24
    sheet = Image.new('RGB', (w1 + w2 + 12, 34 + len(ims) * (hh + pad)), (23, 23, 17)); d = ImageDraw.Draw(sheet)
    d.text((8, 8), '상점 진열 확장 시안   왼쪽 1챕터, 오른쪽 2챕터 / 간격 그대로, 리롤 해금 런 기준 / B 의 2단계는 A 의 2단계와 같다', font=m.UI, fill=(255, 255, 255))
    for i, (t, a, b) in enumerate(ims):
        y = 34 + i * (hh + pad); d.text((8, y + 4), t, font=m.UI, fill=(255, 220, 120))
        sheet.paste(a.convert('RGB'), (0, y + pad)); sheet.paste(b.convert('RGB'), (w1 + 12, y + pad))
    sheet.save(HERE / '진열확장-시안.png'); print(sheet.size)
