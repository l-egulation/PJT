# 2스테이지 지하수로 v26 재제작: v25 + 1u 파낸 물웅덩이(③ 잠김).
# python environments/stage2-sewer-modular-20261111/stage2-sewer-modern-v26/source/build.py
from pathlib import Path
import hashlib, html, json, sys
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import render_modern as rm  # noqa: E402

SET = HERE.parent; PKG = SET.parent; V25 = PKG / 'stage2-sewer-modern-v25'
for d in ['runtime/background', 'runtime/foreground', 'runtime/water-front', 'preview/in-basin']:
    (SET / d).mkdir(parents=True, exist_ok=True)

views = []
for i, v in enumerate(rm.views):
    comp, bg, fg, front, info = rm.render(i, layers=True)
    name = v['screen']
    bg.save(SET / 'runtime/background' / name); fg.save(SET / 'runtime/foreground' / name); front.save(SET / 'runtime/water-front' / name)
    comp.save(SET / 'preview' / name)
    cx, cy = v['camera']; left = round(cx * 32 - 480); top = round(cy * 32 + 270)
    vis = [b for b in info['basins'] if b['x0'] * 32 - left >= 40 and b['x1'] * 32 - left <= 920 and 80 <= top - b['floorTop'] * 32 <= 500]
    if vis:   # 캐릭터가 화면 가운데에 가까운 웅덩이 바닥에 선 모습
        b = min(vis, key=lambda b: abs((b['x0'] + b['x1']) * 16 - left - 480))
        im = bg.convert('RGBA'); im.alpha_composite(fg)
        hx = round((b['x0'] + b['x1']) / 2 * 32) - left; hy = top - round(b['bottom'] * 32)
        im.alpha_composite(rm.hero, (hx - rm.hero.width // 2, hy - rm.hero.height)); im.alpha_composite(front)
        im.convert('RGB').save(SET / 'preview/in-basin' / name)
    views.append({'id': v['id'], 'title': v['title'], 'screen': name, 'camera': v['camera'], 'heroFeet': v['heroFeet'],
                  'inBasinPreview': bool(vis), **info})
    print(v['id'], len(info['basins']), 'basins')

font = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 26); small = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 20)
for k, ids in enumerate([[0, 1, 4, 7], [2, 3, 5, 6]], 1):
    b = Image.new('RGB', (2 * 960 + 48, 60 + 4 * 584), '#0d1418'); d = ImageDraw.Draw(b)
    for c, t in enumerate(['v25', 'v26 (캐릭터가 웅덩이에 잠긴 모습)']): d.text((16 + c * 976, 16), t, font=font, fill='#e6ece6')
    for r, i in enumerate(ids):
        v = views[i]; y = 60 + r * 584
        d.text((16, y + 6), f"{v['id']} · {v['title']}", font=small, fill='#9fb3ad')
        right = SET / 'preview/in-basin' / v['screen'] if v['inBasinPreview'] else SET / 'preview' / v['screen']
        b.paste(Image.open(V25 / 'preview' / v['screen']).convert('RGB'), (16, y + 40))
        b.paste(Image.open(right).convert('RGB'), (16 + 976, y + 40))
    b.save(SET / f'preview/compare-v25-v26-{k}.png')
z = Image.new('RGB', (2 * 960 + 48, 600), '#0d1418'); d = ImageDraw.Draw(z)
for c, (n, box, t) in enumerate([('in-basin/02-F01-960x540.png', (470, 330, 710, 465), '물줄기 웅덩이에 잠김 · 4배'), ('in-basin/01-S-960x540.png', (500, 350, 740, 485), '웅덩이 바닥에 선 캐릭터 · 4배')]):
    d.text((16 + c * 976, 12), t, font=font, fill='#e6ece6')
    z.paste(Image.open(SET / 'preview' / n).crop(box).resize((960, 540), Image.NEAREST), (16 + c * 976, 52))
z.save(SET / 'preview/detail-4x.png')

sec = []
for v in views:
    extra = f'<a href="preview/in-basin/{v["screen"]}"><img src="preview/in-basin/{v["screen"]}" width="960" height="540"></a>' if v['inBasinPreview'] else ''
    sec.append(f'<section><h2>{v["id"]} · {html.escape(v["title"])}</h2><a href="preview/{v["screen"]}"><img src="preview/{v["screen"]}" width="960" height="540"></a>{extra}</section>')
(SET / 'review.html').write_text(
    '<!doctype html><html lang="ko"><meta charset="utf-8"><title>2스테이지 지하수로 v26</title>'
    '<style>body{font:16px system-ui;margin:24px;background:#0d1418;color:#dfe7e2}section{margin:30px 0}img{max-width:100%;height:auto;image-rendering:pixelated;display:block;margin:8px 0}a{color:#9fd}</style>'
    '<h1>2스테이지 지하수로 v26</h1><p>v25 + 바닥을 1u 파고 물을 채운 웅덩이. 캐릭터는 웅덩이 바닥에 서고 물 앞면이 몸 아래를 덮는다. 960×540 · 32px/unit, 오프라인 합성.</p>'
    '<p><a href="preview/compare-v25-v26-1.png">v25 대 v26 (1)</a> · <a href="preview/compare-v25-v26-2.png">(2)</a> · <a href="preview/detail-4x.png">4배 확대</a></p>'
    + ''.join(sec) + '</html>', encoding='utf-8')

(SET / 'manifest.json').write_text(json.dumps({
    'base': '../stage2-sewer-modern-v25', 'layout': 'current', 'source': '../source/chapter2-layouts.json',
    'sourceSha256': hashlib.sha256((PKG / 'source/chapter2-layouts.json').read_bytes()).hexdigest(),
    'screenSize': [960, 540], 'pixelsPerUnit': 32,
    'layers': {'background': 'runtime/background (RGB)', 'foreground': 'runtime/foreground (RGBA, 지형·돌판·웅덩이 안벽과 물 뒷면)',
               'waterFront': 'runtime/water-front (RGBA, 액터 앞에 덮는 반투명 물 앞면·수면 줄)', 'preview': 'preview, preview/in-basin'},
    'basin': {'depthUnits': 1, 'widthsUnits': [2, 3, 4], 'surfaceBelowFloorPx': 2, 'liningPx': 3,
              'collision': '판 칸(x0..x1 × bottom..floorTop)을 바닥 충돌에서 뺀다. 안벽·바닥 그림은 칸 바깥 땅 속에 있다.',
              'placement': ['물줄기가 떨어지는 바닥엔 하나(벽 쪽 여유 8px, 좌우 이동해 맞춤)', '그 밖엔 바닥 288px마다 후보, 셋 중 둘', '아래·옆 8px 이상 땅이 꽉 찬 곳만', '문 발치(테 + 30px)·캐릭터 발밑·다른 웅덩이(40px)와 떨어짐']},
    'views': views, 'characterResampled': False, 'unityModified': False}, ensure_ascii=False, indent=2), encoding='utf-8')
print('v26: 8 views x (background, foreground, water-front, preview) + in-basin previews + review.html')
