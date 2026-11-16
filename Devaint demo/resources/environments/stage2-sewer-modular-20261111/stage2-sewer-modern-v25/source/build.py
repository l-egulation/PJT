# 2스테이지 지하수로 v25(B 5차) 재제작. 실제 맵 8구간을 배경·전경 층과 합성 미리보기로 내보낸다.
# python environments/stage2-sewer-modular-20261111/stage2-sewer-modern-v25/source/build.py
from pathlib import Path
import hashlib, html, json, sys
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import render_modern as rm  # noqa: E402

SET = HERE.parent; PKG = SET.parent; V24 = PKG / 'stage2-room-background-v24'
for d in ['runtime/background', 'runtime/foreground', 'preview']:
    (SET / d).mkdir(parents=True, exist_ok=True)

views = []
for i, v in enumerate(rm.views):
    comp, bg, fg, info = rm.render(i, layers=True)
    name = v['screen']
    bg.save(SET / 'runtime/background' / name); fg.save(SET / 'runtime/foreground' / name); comp.save(SET / 'preview' / name)
    views.append({'id': v['id'], 'title': v['title'], 'screen': name, 'camera': v['camera'], 'heroFeet': v['heroFeet'], **info})
    print(v['id'], info)

font = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 26); small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 20)
for k, ids in enumerate([[0, 1, 4, 7], [2, 3, 5, 6]], 1):
    b = Image.new('RGB', (2 * 960 + 48, 60 + 4 * 584), '#0d1418'); d = ImageDraw.Draw(b)
    for c, t in enumerate(['v24 (이전)', 'v25']): d.text((16 + c * 976, 16), t, font=font, fill='#e6ece6')
    for r, i in enumerate(ids):
        v = views[i]; y = 60 + r * 584
        d.text((16, y + 6), f"{v['id']} · {v['title']}", font=small, fill='#9fb3ad')
        b.paste(Image.open(V24 / 'preview' / v['screen']).convert('RGB'), (16, y + 40))
        b.paste(Image.open(SET / 'preview' / v['screen']).convert('RGB'), (16 + 976, y + 40))
    b.save(SET / f'preview/compare-v24-v25-{k}.png')
z = Image.new('RGB', (2 * 960 + 48, 600), '#0d1418'); d = ImageDraw.Draw(z)
for c, (n, box, t) in enumerate([('05-F04-960x540.png', (160, 340, 480, 520), '바닥 위에 선 문·물웅덩이 · 3배'), ('06-F05-960x540.png', (330, 360, 650, 540), '통과 돌판 · 3배')]):
    d.text((16 + c * 976, 12), t, font=font, fill='#e6ece6')
    z.paste(Image.open(SET / 'preview' / n).crop(box).resize((960, 540), Image.NEAREST), (16 + c * 976, 52))
z.save(SET / 'preview/detail-3x.png')

sections = ''.join(f'<section><h2>{v["id"]} · {html.escape(v["title"])}</h2><a href="preview/{v["screen"]}"><img src="preview/{v["screen"]}" width="960" height="540"></a></section>' for v in views)
(SET / 'review.html').write_text(
    '<!doctype html><html lang="ko"><meta charset="utf-8"><title>2스테이지 지하수로 v25</title>'
    '<style>body{font:16px system-ui;margin:24px;background:#0d1418;color:#dfe7e2}section{margin:30px 0}img{max-width:100%;height:auto;image-rendering:pixelated}a{color:#9fd}</style>'
    '<h1>2스테이지 지하수로 v25</h1><p>960×540 · 32px/unit. 실제 current 레이아웃, 캐릭터 원본 그대로. 오프라인 합성이며 Unity 적용 전입니다.</p>'
    '<p><a href="preview/compare-v24-v25-1.png">v24 대 v25 (1)</a> · <a href="preview/compare-v24-v25-2.png">(2)</a> · <a href="preview/detail-3x.png">3배 확대</a></p>'
    + sections + '</html>', encoding='utf-8')

(SET / 'manifest.json').write_text(json.dumps({
    'layout': 'current', 'source': '../source/chapter2-layouts.json',
    'sourceSha256': hashlib.sha256((PKG / 'source/chapter2-layouts.json').read_bytes()).hexdigest(),
    'screenSize': [960, 540], 'pixelsPerUnit': 32,
    'layers': {'background': 'runtime/background (RGB, 벽·기둥·관·문·등·빛줄기·물줄기·사슬·받침)',
               'foreground': 'runtime/foreground (RGBA, 땅·통과 돌판·이끼·물방울·웅덩이)',
               'preview': 'preview (배경+전경+먼지+캐릭터 합성)'},
    'terrainTexture': '../stage2-unified-stone-v21 벽돌을 땅 테두리 2~3줄에만 사용, 속은 바위',
    'oneWayVisiblePx': 11, 'placementRules': [
        '문(아치)은 테 폭 전체가 같은 높이 바닥 위, 위·옆이 땅에 잘리지 않고 통과 발판이 지나지 않을 때만. 큰(76/200)·중간(60/160)·작은(46/124) 문 순으로 맞춤',
        '등·물줄기는 문 테에서 24px, 가로 관과 겹치지 않음, 통과 발판에서 8~16px',
        '캐릭터 머리 뒤에는 등을 두지 않음'],
    'views': views, 'characterResampled': False, 'unityModified': False}, ensure_ascii=False, indent=2), encoding='utf-8')
print('v25: 8 views x (background, foreground, preview) + compare boards + review.html')
