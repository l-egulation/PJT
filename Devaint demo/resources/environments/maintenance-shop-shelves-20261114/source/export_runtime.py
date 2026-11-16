"""정비소 진열 확정안(2026-11-14)을 게임용 그림으로 뽑는다. 32px = 1칸, 바닥 줄은 그림 맨 아래 행 바로 밑.

    python export_runtime.py <워크트리 루트> <출력 폴더>

stage1-shop-N.png 1챕터 건물(돌 건물 10차) + 공구걸이·끈 + 진열 선반 판. N = 한 줄 칸 수 3~6(상품 3~5개 + 리롤 궤짝).
                  칸 수가 늘면 같은 간격(약 2.45칸)으로 진열칸이 길어진다(상점 진열 확장, 사용자 선택 2026-11-14 A안).
                  장인·책상·등불·간판은 게임이 따로 놓는다.
stage2-shop-N.png 2챕터 방 벽을 판 벽감(정비소+진열) + 관·전구 줄·포스터·공구·병·이끼·금·렌치·간판 사슬·선반 판. N 은 위와 같다.
sign-stage2.png   2챕터 팔레트 팻말(1챕터 팻말 모양, 2챕터 색).
stage1-reroll-crate.png, stage2-reroll-crate.png   리롤 보급 궤짝(옆면에 둥근 화살표).
layout.json       게임 코드가 쓰는 자리(장인 발밑 기준 칸 단위, 칸 수별): 그림 중심·폭, 선반 판 윗면·좌우 끝, 등불·간판.
"""
import json
import os
import subprocess
import sys
from pathlib import Path
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
OUT = Path(sys.argv[2]); OUT.mkdir(parents=True, exist_ok=True)
import mock as m
from PIL import Image, ImageDraw

U = 32
COLUMNS = int(os.environ.get('SHOP_COLUMNS', '0'))


def ch1():
    import ch1_building as cb, ch1_scene_new as sc
    m.H, m.FLOOR = 340, 320
    FLOOR = m.FLOOR; W = 1080
    c = Image.new('RGBA', (W, m.H), (0, 0, 0, 0))
    bld = Image.open(HERE / os.environ.get('SHOP_BUILDING', 'building-stage1.png')).convert('RGBA'); B0 = 40; top_y = FLOOR - bld.height + 2
    c.alpha_composite(bld, (B0, top_y))
    px = lambda dots: B0 + dots * 2
    X = (px(cb.O0) + px(cb.SPLIT)) // 2 + 6
    bd = m.img(m.SHOP / 'stage1-backdrop.png')
    rack = bd.crop((282, 92, 408, 131)); rp = rack.load()
    for yy in range(rack.height):
        for xx in range(rack.width):
            if rp[xx, yy][:3] in ((246, 238, 226), (230, 221, 207), (216, 206, 191)): rp[xx, yy] = (0, 0, 0, 0)
    c.alpha_composite(rack, (X - rack.width // 2 - 4, FLOOR - cb.OH * 2 + 26))
    pc = c.load(); wall = {cb.BACK, cb.BACK2, cb.BACK3}
    for hx in (X - rack.width // 2 + 14, X + rack.width // 2 - 22):
        for yy in range(FLOOR - cb.OH * 2, FLOOR - cb.OH * 2 + 28):
            for dx in (0, 1):
                if pc[hx + dx, yy] in wall: pc[hx + dx, yy] = cb.OUT
    i0, i1 = px(cb.SPLIT) + 8, px(cb.O1) - 10; shelf_y = FLOOR - 80
    sc.plank(ImageDraw.Draw(c), i0, i1, shelf_y)
    box = (B0, top_y, B0 + bld.width, FLOOR)                          # the building's own ground line stays out (the map has its floor)
    art = c.crop(box); art.save(OUT / f'stage1-shop-{COLUMNS}.png')
    row = lambda dot: FLOOR - 2 * (cb.FLOOR - dot)
    lantern_top = FLOOR - cb.OH * 2 + 14
    return {'art': [((box[0] + box[2]) / 2 - X) / U, 0], 'width': (box[2] - box[0]) / U,
            'shelf': {'top': (FLOOR - shelf_y) / U, 'from': (i0 - X) / U, 'to': (i1 + 1 - X) / U},
            'lantern': [(px(cb.O0) + 18 - X) / U, (FLOOR - lantern_top - 38) / U],
            'sign': [(px(cb.SIGN_X) + 1 - X) / U, (FLOOR - row(cb.SIGN_ARM) - 60) / U]}


def ch2():
    import ch2_recess as r
    X, FLOOR = r.X, r.FLOOR
    c = Image.new('RGBA', (r.W, r.H), (0, 0, 0, 0))
    shop0, shop1, top = X - 150, X + 150, FLOOR - 190
    store0, store1 = X + 150, X + 150 + 316 + r.EXTRA
    r.recess(c, shop0, store1, top, warm=[(X - 123, top + 40, 120), (X + 40, top + 30, 140), (store0 + 120, top + 24, 150)])
    b = m.img(m.SHOP / 'stage2-backdrop.png')
    r.string_lights(c, shop0 + 40, shop1 - 20, top + 12)
    poster = b.crop((178, 130, 306, 233)); c.alpha_composite(poster, (X - poster.width // 2 + 6, FLOOR - 76 - poster.height + 20))
    tools = r.unbrick(b.crop((103, 162, 152, 228))); c.alpha_composite(tools, (X - 140, FLOOR - 34 - tools.height + 12))
    jars = r.unbrick(b.crop((326, 134, 374, 160))); c.alpha_composite(jars, (X + 74, FLOOR - 150))
    r.string_lights(c, store0 + 14, store1 - 14, top + 12)
    py = top - 34
    r.backdrop_pipes(c, shop0 - 44, store1 + 44, py)
    d = ImageDraw.Draw(c); sx = shop0 + 26
    for i in range(3): d.ellipse((sx - 2, py + 30 + i * 6, sx + 2, py + 30 + i * 6 + 6), outline=r.INK)   # the sign's chain (the sign swings in game)
    r.details(c, shop0, store1, top, py)
    x0, x1, sy = store0 + 7, store1 - 7, FLOOR - 80                     # the shelf plank (stock() without the goods)
    Wr, INK, STEEL = r.Wr, r.INK, r.STEEL
    d.rectangle((x0, sy, x1, sy + 9), fill=Wr[6], outline=INK)
    d.line((x0 + 1, sy + 1, x1 - 1, sy + 1), fill=Wr[9]); d.line((x0 + 1, sy + 2, x1 - 1, sy + 2), fill=Wr[8]); d.line((x0 + 1, sy + 8, x1 - 1, sy + 8), fill=Wr[4])
    for bx in (x0 + 34, (x0 + x1) // 2 - 6, x1 - 46):
        d.rectangle((bx, sy + 10, bx + 12, sy + 12), fill=STEEL[2], outline=INK)
        for i in range(10): d.point((bx + 1, sy + 13 + i), fill=INK); d.point((bx + 2 + i, sy + 13 + i), fill=STEEL[1])
    bb = c.getbbox(); box = (bb[0], bb[1], bb[2] + (bb[2] - bb[0]) % 2, FLOOR + 1)    # even width: a whole-pixel centre
    art = c.crop(box); art.save(OUT / f'stage2-shop-{COLUMNS}.png')
    return {'art': [((box[0] + box[2]) / 2 - X) / U, 0], 'width': (box[2] - box[0]) / U,
            'shelf': {'top': (FLOOR - sy) / U, 'from': (x0 - X) / U, 'to': (x1 + 1 - X) / U},
            'lantern': [(X - 123 - X) / U, (FLOOR - (top + 8) - 38) / U],
            'sign': [(sx + 1 - X) / U, (FLOOR - (py + 46) - 60) / U]}


def crates():
    import reroll_objects as ro, ch1_building as cb
    for name, ch in (('stage1', type('C', (), {'name': '1챕터', 'cb': cb})()), ('stage2', type('C', (), {'name': '2챕터'})())):
        c = Image.new('RGBA', (40, 36), (0, 0, 0, 0)); ro.crate(c, 20, 36, ro.palette(ch)); c.save(OUT / f'{name}-reroll-crate.png')


def sign2():
    import sign_stage2
    sign_stage2.make().save(OUT / 'sign-stage2.png')


if __name__ == '__main__':
    if COLUMNS:                                                       # one width (run by the loop below in its own process)
        part = {'stage1': ch1(), 'stage2': ch2()}
        (OUT / f'layout-{COLUMNS}.json').write_text(json.dumps(part), encoding='utf-8')
    else:
        build = HERE / '_build'; build.mkdir(exist_ok=True); layout = {'stage1': {}, 'stage2': {}}
        for n in (3, 4, 5, 6):
            env = dict(os.environ, SHOP_COLUMNS=str(n), SHOP_EXTRA_DOTS=str(39 * (n - 4)), SHOP_EXTRA_PX=str(76 * (n - 4)),
                       SHOP_BUILDING=f'_build/building-{n}.png')
            subprocess.run([sys.executable, str(HERE / 'ch1_building.py'), sys.argv[1]], check=True, env=env, cwd=HERE)
            subprocess.run([sys.executable, str(HERE / 'export_runtime.py')] + sys.argv[1:3], check=True, env=env, cwd=HERE)
            part = json.loads((OUT / f'layout-{n}.json').read_text(encoding='utf-8')); (OUT / f'layout-{n}.json').unlink()
            for k in part: layout[k][str(n)] = part[k]
            (build / f'building-{n}.png').unlink()
        build.rmdir()
        crates(); sign2()
        (OUT / 'layout.json').write_text(json.dumps(layout, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        print(json.dumps(layout, indent=1))
