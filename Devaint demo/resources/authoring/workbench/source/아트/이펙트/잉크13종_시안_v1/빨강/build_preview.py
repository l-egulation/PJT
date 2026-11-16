import sys
if '--rebuild-retired' not in sys.argv:
    print('Retired proposal. Use --rebuild-retired explicitly to regenerate original candidate outputs; current material: '+str(__file__).replace('build_preview.py','preview.html'))
    raise SystemExit(0)
from pathlib import Path
import bisect
import hashlib
import json
import shutil
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
UNITY = ROOT / 'unity/Assets/Deviant/World'
SOURCE = Path(r'C:\Users\SSAFY\.codex\generated_images\01a12e1a-6e0c-71c2-81ce-79ae27b362ad\exec-70e97923-4cad-479a-b19a-3efd38620bcc.png')
NEAREST = Image.Resampling.NEAREST
HERE.mkdir(parents=True, exist_ok=True)
shutil.copy2(SOURCE, HERE / 'generated_source.png')
source = Image.open(SOURCE).convert('RGBA')
assert source.width * 3 == source.height * 4
cell = source.width // 4
frames = []
frame_dir = HERE / 'frames'
frame_dir.mkdir(exist_ok=True)
sheet = Image.new('RGBA', (512, 384))
for n in range(12):
    x, y = n % 4 * cell, n // 4 * cell
    frame = source.crop((x, y, x + cell, y + cell)).resize((128, 128), NEAREST)
    frame.save(frame_dir / f'red_{n+1:02}.png')
    sheet.alpha_composite(frame, (n % 4 * 128, n // 4 * 128))
    frames.append(frame)
sheet.save(HERE / 'red_128x128_4x3.png')

font_path = Path(r'C:\Windows\Fonts\malgun.ttf')
font = ImageFont.truetype(str(font_path), 18)
small = ImageFont.truetype(str(font_path), 14)
labels = ['표식 1단계', '표식 2단계', '표식 3단계', '압축 / 발동', '폭발 시작', '확산', '최대 폭발', '고리 분해', '파편 확산', '잔여 방울', '소멸', '끝']
board = Image.new('RGB', (1088, 948), '#161b22')
d = ImageDraw.Draw(board)
d.text((24, 16), '빨강 잉크 · 표식 누적 / 폭발 · 128 × 128px × 12', font=font, fill='#f2ebe1')
for n, frame in enumerate(frames):
    x, y = 16 + n % 4 * 268, 58 + n // 4 * 292
    d.rectangle((x, y, x+255, y+255), fill='#232b34')
    board.paste(frame.resize((256,256), NEAREST), (x,y), frame.resize((256,256), NEAREST))
    d.text((x+8,y+262), f'{n+1:02}  {labels[n]}', font=small, fill='#eac5bc')
board.save(HERE / 'frame_board.png')

def flat(frame, size=128):
    bg = Image.new('RGBA', (128,128), '#232b34')
    bg.alpha_composite(frame)
    return bg.convert('RGB').resize((size,size), NEAREST)

effect_ms = [320,320,320] + [60]*9
for size, name in [(128,'effect_128.gif'), (384,'effect_preview.gif')]:
    seq = [flat(f,size) for f in frames] + [flat(Image.new('RGBA',(128,128)),size)]
    seq[0].save(HERE/name, save_all=True, append_images=seq[1:], duration=effect_ms+[400], loop=0, disposal=2, optimize=False)

integration = json.loads((UNITY/'Data/integration.json').read_text(encoding='utf-8-sig'))
attack = next(c for c in integration['clips'] if c['id']=='attack')
durations = attack['seconds']
ends = []
elapsed = 0
for v in durations:
    elapsed += v
    ends.append(elapsed)
hero_paths = [UNITY/f'Art/Character/attack/{n:02}.png' for n in range(1,13)]
hero = [Image.open(p).convert('RGBA') for p in hero_paths]
idle = Image.open(UNITY/'Art/Character/idle/01.png').convert('RGBA')
enemy_paths = [UNITY/f'Monsters/Art/boar/move/{n:02}.png' for n in range(1,13)]
enemy = [Image.open(p).convert('RGBA') for p in enemy_paths]
start_times = [0.30, 1.55]
# Unity InkHeroCombat: zero-based frames 4 and 7 trigger hits.
hit_times = sorted(s+sum(durations[:f]) for s in start_times for f in [4,7])
burst_time = hit_times[2]
scene_frames = []
snapshots = []
floor = 174
for tick in range(165):
    t = tick * .02
    canvas = Image.new('RGBA', (320,200), '#232b34')
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0,floor,320,200), fill='#30383e')
    draw.line((0,floor,320,floor), fill='#6d787a')
    draw.ellipse((88,169,135,176), fill='#161b22')
    draw.ellipse((151,168,260,177), fill='#161b22')
    active = next((t-s for s in start_times if 0 <= t-s < elapsed), None)
    hf = bisect.bisect_right(ends, active) if active is not None else None
    hf = min(hf,11) if hf is not None else None
    body = hero[hf] if hf is not None else idle
    feet = attack['feet'][hf] if hf is not None else 121
    count = sum(t >= h - 1e-7 for h in hit_times)
    recent = max((h for h in hit_times if h <= t), default=-100)
    recoil = round(3 * max(0, 1-(t-recent)/.16))
    foe = enemy[int(t/.10)%12]
    enemy_x = 139 + recoil
    canvas.alpha_composite(foe, (enemy_x, floor-foe.getbbox()[3]))
    canvas.alpha_composite(body, (48, floor-feet))
    center = (enemy_x+64, 127)
    if count in [1,2]:
        canvas.alpha_composite(frames[count-1], (center[0]-64,center[1]-64))
    if burst_time <= t < burst_time+.54:
        fi = min(11, 3+int((t-burst_time)/.06))
        canvas.alpha_composite(frames[fi], (center[0]-64,center[1]-64))
    if count >= 4:
        canvas.alpha_composite(frames[0], (center[0]-64,center[1]-64))
    large = canvas.convert('RGB').resize((960,600), NEAREST)
    ld = ImageDraw.Draw(large)
    state = '공격 준비' if count==0 else f'표식 {count}/3' if count<3 else '표식 폭발' if count==3 else '다음 표식 1/3'
    ld.rounded_rectangle((18,16,365,79), radius=8, fill='#161b22')
    ld.text((30,22),'빨강 잉크 · 실제 Unity 공격 리소스',font=font,fill='#f4eee4')
    ld.text((30,49),state,font=small,fill='#ff7b70')
    scene_frames.append(large)
    if tick in [32,45,98,106,117]:
        path=HERE/f'combat_check_{tick:03}.png'
        large.save(path)
        snapshots.append(path.name)
scene_frames[0].save(HERE/'hero_attack_composite.gif', save_all=True, append_images=scene_frames[1:], duration=20, loop=0, disposal=2, optimize=False)

manifest = {
    'ink': 'R', 'role': '표식 누적·폭발', 'prototype': True,
    'frameSize': [128,128], 'sheetSize': [512,384], 'grid': [4,3],
    'order': 'left-to-right, top-to-bottom', 'markFrames': [1,2,3], 'burstFrames': list(range(4,13)),
    'burstFrameSeconds': .06, 'heroAttackSeconds': durations,
    'heroHitFramesOneBased': [5,8], 'demoHitTimesSeconds': hit_times,
    'demoMarkThreshold': 3,
    'note': '표식 3회 누적은 시안용 연출 설정입니다. Unity 실행 녹화가 아니라 실제 Unity PNG와 공격 타이밍을 사용한 합성 GIF입니다.',
    'sourceFiles': [str(p.relative_to(ROOT)) for p in hero_paths+enemy_paths],
    'sourceHashes': {str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in hero_paths+enemy_paths},
    'suggestedUnityImport': {'textureType':'Sprite (2D and UI)','spriteMode':'Multiple','slice':'Grid by Cell Size 128x128','filterMode':'Point','compression':'None','mipMaps':False,'pixelsPerUnit':32,'pivot':[.5,.5]},
    'checkImages': snapshots
}
(HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>deviant · 빨강 잉크 · 이펙트 시안</title><style>
*{box-sizing:border-box}body{margin:0;background:#12171d;color:#f3ede3;font-family:Malgun Gothic,sans-serif;line-height:1.75}main{max-width:1120px;margin:auto;padding:36px 24px 60px}h1{font-size:30px;margin:4px 0 12px}h2{font-size:21px;margin:0 0 16px}p{color:#bdc6ca}section{background:#1b222a;border:1px solid #343d46;border-radius:16px;padding:24px;margin:24px 0}img{max-width:100%;height:auto;image-rendering:pixelated;display:block;margin:auto}.tag{color:#ff9384;font-size:13px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}.preview{width:384px}.role{border-left:4px solid #d64539;padding:2px 20px;margin:22px 0 0}.role p{margin:8px 0}.links{display:flex;gap:20px;flex-wrap:wrap}a{color:#f5b29f}small{color:#a7b1b9}@media(max-width:700px){.grid{grid-template-columns:1fr}main{padding:20px 12px}section{padding:16px}}</style><main>
<div class="tag">deviant · INK EFFECT PROTOTYPE 01</div><h1>빨강 잉크 — 표식이 쌓이고 터지는 순간</h1><p>한 프레임 128×128px · 투명 PNG 12장 · 4열×3행 시트 · 실제 Unity 캐릭터 공격 합성</p>
<section><h2>01. 전체 프레임을 한 캔버스에</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><p>01–03은 표식 상태, 04–12는 폭발과 소멸입니다. 표식은 타격 사이에 유지하고 폭발 프레임은 발동 순간부터 순서대로 재생합니다.</p><a href="../../../../WeaponReference/확정7종_20261019/index.html" download>투명 스프라이트 시트 PNG 다운로드 · 512×384</a></section>
<section class="grid"><div><h2>02. 이펙트만 연결한 GIF</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><p>표식 1 → 2 → 3 → 압축 → 폭발 → 파편 → 소멸. 픽셀을 확인하기 위해 3배 확대했습니다.</p></div><div><h2>실제 역할</h2><div class="role"><p><b>빨강 / R — 표식 누적·폭발</b></p><p>적중한 적에게 표식을 누적합니다. 발동 조건을 채우면 표식을 소모하며 폭발 피해를 줍니다.</p><p>표식은 적 몸통에 붙이고, 폭발은 해당 적을 중심으로 터뜨립니다. 기본 무기의 베기 궤적과 구분되는 적중 후 효과입니다.</p></div></div></section>
<section><h2>03. 우리 주인공의 공격에 합성</h2><p>이전 개별 미리보기 대신 <a href="../../../../WeaponReference/확정7종_20261019/index.html">현재 확정 모션</a>을 확인하세요.</p><div class="role"><p><b>공격 → 표식 누적 → 폭발 → 다음 표식</b></p><p>프로젝트의 주인공·멧돼지 PNG를 사용했습니다. 원래 공격 12프레임의 재생 시간 총 1.05초와 5·8번째 프레임의 타격 시점에 맞췄습니다.</p><p>이 시안은 표식 3회 누적을 예로 듭니다. 실제 발동 횟수와 피해 계수는 게임 규칙에 맞춰 연결해야 합니다.</p></div><small>Unity 실행 녹화가 아니라, Unity에 사용 중인 스프라이트와 공격 타이밍으로 만든 합성 미리보기입니다. 바닥과 배경은 효과 판독용입니다.</small></section>
<section><h2>Unity에 가져갈 파일</h2><div class="links"><a href="../../../../WeaponReference/확정7종_20261019/index.html" download>128px 투명 시트</a><a href="../../../../WeaponReference/확정7종_20261019/index.html" download>128px 원본 GIF</a><a href="../../../../WeaponReference/확정7종_20261019/index.html" download>공격 합성 GIF</a><a href="manifest.json" download>프레임·타이밍 정보</a></div><p>Sprite / Multiple · Grid by Cell Size 128×128 · Point · Compression None · Mip Maps Off · PPU 32 권장. AI 원본을 최근접 방식으로 축소한 시안이며, 정식 적용 전 프레임별 중심과 파편 이동을 다듬을 수 있습니다.</p></section></main></html>'''
(HERE/'preview.html').write_text(html,encoding='utf-8')

assert sheet.size==(512,384)
assert len(list(frame_dir.glob('red_*.png')))==12
assert all(f.size==(128,128) and f.getextrema()[3][0]==0 for f in frames)
gif = Image.open(HERE/'hero_attack_composite.gif')
assert gif.size==(960,600) and gif.n_frames>=24
assert sum((gif.seek(k) or gif.info['duration']) for k in range(gif.n_frames))==3300
for p in hero_paths+enemy_paths:
    assert hashlib.sha256(p.read_bytes()).hexdigest()==manifest['sourceHashes'][str(p.relative_to(ROOT))]
print(json.dumps({'folder':str(HERE),'sheet':sheet.size,'frames':12,'attackSeconds':round(elapsed,2),'hitTimes':hit_times,'compositeGifFrames':gif.n_frames},ensure_ascii=False))
