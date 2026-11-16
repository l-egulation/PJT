"""Build art-only hub assemblies and a local gallery from actual exported files."""
from pathlib import Path
from html import escape
import json
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
V = ROOT / 'environments/village'
UI = ROOT / 'ui/roguelite-hub'
OUT = V / 'preview/starting-hub'
NAMES = [('palette-atelier', '염색공방'), ('supply-office', '보급소'),
         ('reclaim-workshop', '회수공방')]
PROPS = [('palette-bench', '팔레트 작업대'), ('supply-counter', '보급 접수대'),
         ('reclaim-bench', '회수 작업대'), ('record-desk', '기록 책상'),
         ('return-well', '귀환 우물'), ('departure-gate', '출발 문'),
         ('departure-table', '출발 준비대')]
LABELS = {'palette_slots_4': '팔레트 4칸', 'palette_slots_5': '팔레트 5칸',
          'palette_wildcard_1': '무기 1 첫 칸 무색', 'palette_wildcard_2': '무기 2 첫 칸 무색',
          'starting_gold_100': '시작 골드 +100', 'destroyed_ink_gold_20': '잉크 파괴 +20',
          'magic_pigment': '마법안료', 'palette_atelier': '염색공방',
          'supply_office': '보급소', 'reclaim_workshop': '회수공방',
          'record_farmhouse': '기록 농가', 'departure_lane': '출발 농로',
          'locked': '잠김', 'unlocked': '해금 완료', 'story_record': '이야기 기록',
          'destroy_ink': '잉크 파괴'}


def image(path): return Image.open(path).convert('RGBA')


def paste_foot(canvas, sprite, x, y, line=None):
    if line is None: line = sprite.height - 8
    canvas.alpha_composite(sprite, (round(x-sprite.width/2), round(y-line)))


def assembly(state):
    width, height, ground = 2880, 540, 438
    paper = (245, 239, 221, 255)
    canvas = Image.new('RGBA', (width, height), paper)
    folder = V / 'runtime/backgrounds/reference-map-phase2' / state
    sky = image(folder / 'sky_native.png')
    sky = sky.resize((960, 540), Image.Resampling.NEAREST)
    village = image(folder / 'village_mid_native.png')
    village.thumbnail((1100, 340), Image.Resampling.NEAREST)
    for x in range(0, width, 960):
        canvas.alpha_composite(sky, (x, 0))
        canvas.alpha_composite(village, (x, ground-village.height+10))
    d = ImageDraw.Draw(canvas)
    d.rectangle((0, ground+8, width, height), fill=(83, 65, 45, 255) if state == 'color' else paper)
    cap = image(V / f'runtime/terrain/reference-map-phase2/{state}/platform.png')
    cap.thumbnail((160, 40), Image.Resampling.NEAREST)
    for x in range(0, width, cap.width): canvas.alpha_composite(cap, (x, ground))
    farmhouse = image(V / f'runtime/buildings/reference-map-phase2/{state}/house_balcony.png')
    paste_foot(canvas, farmhouse, 185, ground, farmhouse.height)
    for x, name in [(850, 'palette-atelier'), (1410, 'supply-office'), (1970, 'reclaim-workshop')]:
        paste_foot(canvas, image(V / f'runtime/buildings/starting-hub/{state}/{name}.png'), x, ground)
    for x, name in [(350, 'record-desk'), (470, 'return-well'), (1060, 'palette-bench'),
                    (1615, 'supply-counter'), (2175, 'reclaim-bench'),
                    (2460, 'departure-table'), (2710, 'departure-gate')]:
        paste_foot(canvas, image(V / f'runtime/props/starting-hub/{state}/{name}.png'), x, ground)
    player = image(ROOT / 'characters/ink/weapons/sword/runtime/approved-eleven-package/character/I01.png')
    player = player.crop(player.getbbox())
    paste_foot(canvas, player, 585, ground, player.height)
    OUT.mkdir(parents=True, exist_ok=True)
    canvas.convert('RGB').save(OUT / (state+'-assembly.png'))
    return canvas


def relative(path, base): return os.path.relpath(path, base).replace('\\', '/')


def build():
    OUT.mkdir(parents=True, exist_ok=True)
    for state in ('color', 'sketch'): assembly(state)
    pairs = Image.new('RGB', (1120, 640), '#eee9dc')
    draw = ImageDraw.Draw(pairs)
    for row, state in enumerate(('color', 'sketch')):
        for col, (name, _) in enumerate(NAMES):
            im = image(V / f'runtime/buildings/starting-hub/{state}/{name}.png')
            pairs.paste(im, (col*280, row*320), im)
            draw.text((col*280+7, row*320+270), name+' '+state, fill='#494638')
        im = image(V / f'runtime/buildings/reference-map-phase2/{state}/house_balcony.png')
        pairs.paste(im, (840, row*320), im)
        draw.text((847, row*320+298), 'existing reference', fill='#494638')
    pairs.save(OUT / 'building-pairs.png')
    preview = UI / 'preview'; preview.mkdir(parents=True, exist_ok=True)
    icons = sorted((UI / 'runtime/icons').glob('*.png'))
    sheet = Image.new('RGBA', (256, 64))
    for i, path in enumerate(icons): sheet.alpha_composite(image(path), ((i%8)*32, (i//8)*32))
    sheet.save(preview / 'icons-sheet.png')
    cards = []
    for category, members in [('buildings', NAMES), ('props', PROPS)]:
        for name, title in members:
            paths = {state: relative(V / f'runtime/{category}/starting-hub/{state}/{name}.png', preview)
                     for state in ('color', 'sketch')}
            cards.append('<article class="sprite-card"><div class="sprite-bg"><img loading="lazy" '
                         'class="world-sprite" data-color="'+paths['color']+'" data-sketch="'+paths['sketch']
                         +'" src="'+paths['color']+'" alt="'+title+'"></div><h3>'+title
                         +'</h3><code>'+name+'</code></article>')
    icon_cards = ''.join('<article class="glyph-card"><img src="../runtime/icons/'+p.name
                         +'" alt="'+LABELS[p.stem]+'"><span>'+LABELS[p.stem]+'</span></article>' for p in icons)
    intro_cards = ''.join('<figure><img loading="lazy" src="'
                          +relative(ROOT / 'sequences/starting-village-intro/runtime' / (name+'.png'), preview)
                          +'" alt="'+title+'"><figcaption>'+title+'</figcaption></figure>'
                          for name, title in [('record-hall', '기록 보관소'), ('erased-farm', '지워진 농가와 우물'),
                                              ('awakening-courtyard', '각성 이후 같은 자리')])
    color_assembly = relative(OUT / 'color-assembly.png', preview)
    sketch_assembly = relative(OUT / 'sketch-assembly.png', preview)
    pair_path = relative(OUT / 'building-pairs.png', preview)
    effects = ''.join('<figure><img class="effect-sheet" src="'
                      +relative(ROOT / 'effects/hub-unlock/preview' / (name+'-sheet.png'), preview)
                      +'" alt="'+title+'"><figcaption>'+title+' · 8프레임 · 80ms/프레임</figcaption></figure>'
                      for name, title in [('unlock', '해금 완료'), ('pigment-pickup', '안료 획득')])
    html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Deviant · 시작 마을 에셋</title><style>
*{box-sizing:border-box}body{margin:0;background:#f2eedf;color:#3b453c;font:15px/1.8 'Malgun Gothic',sans-serif}main{max-width:1280px;margin:auto;padding:38px 24px}h1{font:700 40px Georgia,serif;margin:0 0 8px}h2{font-size:23px;margin:42px 0 17px}p{color:#76816e}button{cursor:pointer;font:inherit;background:#faf8eb;border:1px solid #b9c8ad;border-radius:4px;padding:8px 18px;color:#486c52}button[aria-pressed=true]{background:#326a58;color:#fff}button:focus-visible,a:focus-visible{outline:3px solid #997b40;outline-offset:4px}img{max-width:100%;display:block}.wide{overflow:auto;border:1px solid #ced4be;background:#ece8d9}.assembly{width:2880px;max-width:none;image-rendering:pixelated}.toolbar{display:flex;gap:9px;align-items:center;flex-wrap:wrap;margin:18px 0}.toolbar span{font-size:12px;color:#8a927e}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:15px}.sprite-card{border:1px solid #d3d8c5;background:#faf8ed;border-radius:6px;overflow:hidden}.sprite-bg{height:280px;display:flex;align-items:end;justify-content:center;background:repeating-conic-gradient(#ebe7d8 0% 25%,#f6f1e4 0% 50%) 50% /16px 16px;padding:10px}.world-sprite{image-rendering:pixelated;max-height:256px;width:auto}.sprite-card h3{font-size:14px;margin:12px 14px 0}.sprite-card code{font-size:10px;color:#8c947f;display:block;margin:0 14px 14px}.glyph-grid{display:grid;grid-template-columns:repeat(8,1fr);gap:10px}.glyph-card{display:flex;flex-direction:column;align-items:center;gap:14px;padding:18px 5px;background:#e5ead8;border:1px solid #ced7bf;border-radius:5px;text-align:center;font-size:11px}.glyph-card img{width:64px;height:64px;image-rendering:pixelated;filter:brightness(0) saturate(100%) invert(29%) sepia(12%) saturate(820%) hue-rotate(92deg)}.story-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}figure{margin:0}figcaption{font-size:12px;color:#7d896f;padding-top:8px}.effect-sheet{image-rendering:pixelated;background:#e0e4d3;border:1px solid #c6d1b8}.notes{font-size:12px;border-left:3px solid #7d9c72;padding:14px 18px;background:#e6eadb}footer{margin-top:45px;padding-top:18px;border-top:1px solid #cdd4bf;font-size:12px;color:#859075}code{word-break:break-all}@media(max-width:850px){.grid{grid-template-columns:repeat(2,1fr)}.glyph-grid{grid-template-columns:repeat(4,1fr)}.story-grid{grid-template-columns:1fr}.sprite-bg{height:270px}h1{font-size:32px}}@media print{button{display:none}.wide{overflow:hidden}.assembly{max-width:100%;width:100%}.grid{grid-template-columns:repeat(3,1fr)}.sprite-bg{height:240px}}
</style><main><h1>Deviant</h1><p>시작 마을 · 건물별 해금과 1챕터 연결을 위한 실제 에셋 파일</p>
<div class="notes"><strong>게임용 PNG 55개</strong> · 건물/작업대/우물/출발 문 20개, 도트 문양 16개, 효과 프레임 16개, 정지 이야기 배경 3개.<br>기존 농가·원경·지형·주인공은 재사용했습니다. 아래 합성은 아트 배치 미리보기이며 Unity 실행 화면이 아닙니다. 최종 사용자 미술 승인·게임 통합은 별도입니다.</div>
<h2>같은 게임 미술에서 이어지는 마을</h2><div class="toolbar"><button data-state="color" aria-pressed="true">컬러</button><button data-state="sketch" aria-pressed="false">스케치</button><span>가로로 이동하면 전체 배치를 볼 수 있습니다. 같은 스프라이트 캔버스·접촉선을 사용합니다.</span></div>
<div class="wide"><img id="assembly" class="assembly" src="__COLOR__" data-color="__COLOR__" data-sketch="__SKETCH__" alt="기존 마을 아트와 새 건물·작업대·주인공의 합성"></div>
<h2>새 건물과 기존 농가 비교</h2><img src="__PAIRS__" alt="신규 건물 3종과 기존 집의 컬러·스케치 비교">
<h2>독립 스프라이트 · 클릭 없이 파일로 바로 사용</h2><div class="grid">__CARDS__</div>
<h2>기존 GUI와 같은 32px 단색 문양</h2><div class="glyph-grid">__ICONS__</div><p>실제 파일은 흰색 알파 마스크입니다. 게임에서는 기존 uGUI tint를 적용하고 설명·수치·문구는 Galmuri 텍스트로 따로 표시합니다.</p>
<h2>이야기용 정지 배경</h2><div class="story-grid">__INTRO__</div><p>캐릭터와 대사는 별도 레이어로 조립합니다. 구형 StoryReference의 미복원 장면 패키지 전체를 새로 제작한 결과는 아닙니다.</p>
<h2>짧은 표현 효과</h2><div class="story-grid">__EFFECTS__</div>
<footer>원본·요청문·내보내기 도구·PPU/피벗·파일 해시는 함께 보존합니다. 파일 검증과 아트 검수, Unity 임포트와 실행 검증은 구분합니다.</footer>
<script>document.querySelectorAll('[data-state]').forEach(b=>b.addEventListener('click',()=>{const state=b.dataset.state;document.querySelectorAll('[data-state]').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));document.querySelectorAll('[data-color][data-sketch]').forEach(im=>im.src=im.dataset[state])}));</script></main></html>'''
    for key, value in {'COLOR': color_assembly, 'SKETCH': sketch_assembly,
                       'PAIRS': pair_path, 'CARDS': ''.join(cards), 'ICONS': icon_cards,
                       'INTRO': intro_cards, 'EFFECTS': effects}.items():
        html = html.replace('__'+key+'__', value)
    (preview / 'index.html').write_bytes(html.encode('utf-8'))
    print('Built color/sketch assemblies, native comparisons and relative-link gallery.')


if __name__ == '__main__': build()
