# 레이피어 새 몸 모션(평타 C·특수 4종)과 평타 찌르기 이펙트 G 를 리소스 저장소 rapier 에셋에 새 세트로 등록한다(2026-11-08).
#   source/motion-20261108/ : 에테르 원본 시트 5장과 움직이는 판, 프롬프트·입력 묶음, 사용자 배경 제거본, 2타 칼 각도 고친 칸, 시간표, 스크립트
#   runtime/motion-20261108/<sheet>/NNN.png + motion.json : 6색 정리본 중 게임에 쓰는 칸(칸 크기·자리 그대로)과 시간표
#       basic(평타, 칸 1280x720, PPU 213.333) / charge·needle·slip·flurry(특수, 칸 640x360, PPU 106.667), 발밑은 무대 (66,98)
#   runtime/fx-20261108/thrust/<hit1|hit3>/NN.png : 평타 찌르기 이펙트 G 납작 충격파(PPU64, 원점 = 발밑). 1·2타는 같은 그림(hit1)
#   runtime/fx-20261108/special/<clip>/NN.png : 특수 이펙트(4차 1·1·2·1, PPU32, 원점 = 발밑). fx.json 에 시작 시각·기준(몸/시전 자리)
#   칸마다 발바닥을 0번 칸 바닥줄에 맞추고(ground-shift.json), 장침 찌르는 장만 일부러 띄운다(lift).
# 기존 세트(reviewed-20261103·approved-eleven 등)는 그대로 둔다.
# 사용: python register_rapier_resources.py <resources 경로>
import json, shutil, sys, zipfile, hashlib
from pathlib import Path
HERE = Path(__file__).resolve().parent; B = HERE.parent
RES = Path(sys.argv[1]); A = RES / 'characters/ink/weapons/rapier'
SRC = A / 'source/motion-20261108'; RT = A / 'runtime/motion-20261108'; FX = A / 'runtime/fx-20261108'
for d in (SRC, RT, FX):
    if d.exists(): shutil.rmtree(d)
added = []
def put(data, dst):
    dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(data if isinstance(data, bytes) else Path(data).read_bytes()); added.append(dst.relative_to(A).as_posix())
def jdump(obj): return (json.dumps(obj, ensure_ascii=False, indent=1) + '\n').encode('utf-8')
def zname(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        try: n = n.encode('cp437').decode('utf-8')
        except Exception: pass
    return n
# 1) source
for f in sorted((B / '2_에테르결과').iterdir()):
    if f.is_file(): put(f, SRC / 'aether' / f.name)
put(B / '2_에테르결과/배경제거본/레이피어_배경제거_외곽정리_72장.zip', SRC / 'bgremoved/레이피어_배경제거_외곽정리_72장.zip')
put(B / '레이피어-평타·특수-입력.zip', SRC / 'input/레이피어-평타·특수-입력.zip')
for f in sorted((B / '1_프롬프트·입력').rglob('*-prompt-en.txt')): put(f, SRC / 'input/prompts' / f.relative_to(B / '1_프롬프트·입력').as_posix())
for f in sorted((B / '3_보정/hit2-angle-v2').glob('*')): put(f, SRC / 'basic-hit2-blade-level' / f.name)
for f in ('rapier-basic-candidates.json', 'rapier-special-candidates.json', 'ground-shift.json', 'ground_shift.py', 'fix_hit2_angle.py', 'rapier_clean.py', 'make_thrust2.py', 'bake_special_fx4.mjs',
          'write_prompts.py', 'make_input.py', 'prep_preview_sprites.py', 'prep_special_sprites.py', 'register_rapier_resources.py'):
    put(HERE / f, SRC / 'tools' / f)
PFX = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/90_도구/pixelfx/src/fx/ink')
for f in ('rapier-special-fx-v4.js', 'dagger-flurry-cuts.js'): put(PFX / f, SRC / 'tools/pixelfx' / f)
# 2) runtime body
BC = json.loads((HERE / 'rapier-basic-candidates.json').read_text(encoding='utf-8')); SC = json.loads((HERE / 'rapier-special-candidates.json').read_text(encoding='utf-8'))
DIRS = {'평타': 'basic', '모아찌르기': 'charge', '장침': 'needle', '흘려찌르기': 'slip', '섬광연침': 'flurry'}
cells = {}
with zipfile.ZipFile(B / '3_보정/레이피어_6색정리_72프레임.zip') as z:
    for i in z.infolist():
        n = zname(i)
        if n.endswith('.png'): cells[(DIRS[n.split('/')[0]], int(n.split('/')[-1][:-4].split('-')[-1]))] = z.read(i)
SHEET_OF = {'04': 'charge', '05': 'needle', '06': 'slip', '07': 'flurry'}
FXSTART = {'attack1': .07, 'attack2': .07, 'attack3': .09, 'attack_charge': .77, 'attack_long_thrust': .27, 'attack_held_thrust': .07, 'attack_flurry': .12}
CLIPS = {}
for name in ('attack1', 'attack2', 'attack3'):
    c = BC['C'][name]; CLIPS[name] = {'sheet': 'basic', 'steps': [[f'{f:03d}', s] for f, s in zip(c['frames'], c['seconds'])], 'sourceImpact': BC['game'][name]['impact'], 'fxStart': FXSTART[name]}
SPCLIP = {'rapier_default': 'attack_charge', 'rapier_range': 'attack_long_thrust', 'rapier_movement': 'attack_held_thrust', 'rapier_follow_up': 'attack_flurry'}
for skill, name in SPCLIP.items():
    c = SC['C'][skill]; CLIPS[name] = {'sheet': SHEET_OF[c['sheet']], 'steps': [[f'{f:03d}', s] for f, s in zip(c['frames'], c['seconds'])], 'sourceImpact': SC['game'][skill]['sourceImpact'], 'fxStart': FXSTART[name]}
used = sorted({(c['sheet'], int(f)) for c in CLIPS.values() for f, _ in c['steps']})
import io
import numpy as np
from PIL import Image
GS = json.loads((HERE / 'ground-shift.json').read_text(encoding='utf-8')); LIFT = {int(k): v for k, v in SC['C']['rapier_range'].get('lift', {}).items()}
def placed(sheet, f):
    a = np.array(Image.open(io.BytesIO(cells[(sheet, f)])).convert('RGBA')); dy = int(GS[sheet].get(str(f), 0)) - (LIFT.get(f, 0) if sheet == 'needle' else 0)
    if dy:
        b = np.zeros_like(a)
        if dy > 0: b[dy:] = a[:-dy]
        else: b[:dy] = a[-dy:]
        a = b
    buf = io.BytesIO(); Image.fromarray(a).save(buf, 'PNG', optimize=True); return buf.getvalue()
for sheet, f in used: put(placed(sheet, f), RT / sheet / f'{f:03d}.png')
K2, K1 = 1280 / 192, 640 / 192
motion = {'format': 'deviant-rapier-motion-v1', 'source': 'source/motion-20261108 (레이피어_6색정리_72프레임.zip = 사용자 배경 제거본 + tools/rapier_clean.py)',
          'sheets': {'basic': {'cellPx': [1280, 720], 'pixelsPerUnit': round(32 * K2, 4), 'pivot': [round(66 * K2 / 1280, 6), round(1 - 98 * K2 / 720, 6)]},
                     **{s: {'cellPx': [640, 360], 'pixelsPerUnit': round(32 * K1, 4), 'pivot': [round(66 * K1 / 640, 6), round(1 - 98 * K1 / 360, 6)]} for s in ('charge', 'needle', 'slip', 'flurry')}},
          'note': '칸 이름 = 원본 시트 칸 번호. 0번 칸이 입력 대기 그림(CharacterApproved11/rapier/F030)과 같은 자리라 발밑은 모든 칸 무대 (66,98). 평타 basic-027~035 는 칼날을 수평으로 돌린 판(tools/fix_hit2_angle.py).',
          'clips': CLIPS}
put(jdump(motion), RT / 'motion.json')
# 3) runtime fx: G 납작 충격파, 빈 꼬리 장(11~17)은 뺀다
fxj = {'format': 'deviant-rapier-fx-v1', 'fps': 50, 'source': '평타 thrust_*: tools/make_thrust2.py G (은빛 칼빛 + 칼끝 납작 타원 고리 둘) / 특수: tools/pixelfx/rapier-special-fx-v4.js', 'clips': {}}
for hit in (1, 3):
    d = HERE / f'thrust2-fx/G-hit{hit}'; m = json.loads((d / 'meta.json').read_text(encoding='utf-8')); n = 11
    for i in range(n): put(d / f'{i:03d}.png', FX / f'thrust/hit{hit}' / f'{i:02d}.png')
    fxj['clips'][f'thrust_hit{hit}'] = {'dir': f'thrust/hit{hit}', 'frames': n, 'pixelsPerUnit': 64, 'size': m['size'], 'originTopLeftPx': m['origin_px'], 'usedBy': ['attack1', 'attack2'] if hit == 1 else ['attack3'], 'startsAt': 'clip fxStart (타격 0.03초 전)'}
# 특수 이펙트: 앞뒤 빈 장을 빼고 시작 시각을 적는다(시각 0 = 시전 시작, 충전 중에는 시전 경과, 놓은 뒤에는 타격 기준)
SPFX = {'charge': ('charge-c1', 'rapier_default', 'character'), 'needle': ('needle-n1', 'rapier_range', 'character'), 'slip_out': ('slip_out-s2', 'rapier_movement', 'cast_start'),
        'slip_in': ('slip_in-s2', 'rapier_movement', 'character'), 'flurry': ('flurry-f1', 'rapier_follow_up', 'character')}
for name, (src, skill, anchor) in SPFX.items():
    d = HERE / 'special-fx4' / src; m = json.loads((d / 'meta.json').read_text(encoding='utf-8')); fs = sorted(d.glob('*.png'))
    full = [i for i, q in enumerate(fs) if Image.open(q).getbbox()]; a0, a1 = full[0], full[-1]
    for k, i in enumerate(range(a0, a1 + 1)): put(fs[i], FX / f'special/{name}' / f'{k:02d}.png')
    fxj['clips'][name] = {'dir': f'special/{name}', 'frames': a1 - a0 + 1, 'pixelsPerUnit': 32, 'size': m['size'], 'originTopLeftPx': m['origin_px'], 'skill': skill, 'anchor': anchor,
                          'startTime': round(m['t0'] + a0 / m['fps'], 4), 'fps': m['fps'], 'source': f"ink/rapier-special-fx-v4 {m['options']}"}
put(jdump(fxj), FX / 'fx.json')
# 4) 색인
a = json.loads((A / 'asset.json').read_text(encoding='utf-8'))
pre = ('source/motion-20261108/', 'runtime/motion-20261108/', 'runtime/fx-20261108/')
a['files'] = [f for f in a['files'] if not f.startswith(pre)] + sorted(added)
a.setdefault('generatedSets', {})['motion-20261108'] = {'runtime': 'runtime/motion-20261108', 'fx': 'runtime/fx-20261108', 'source': 'source/motion-20261108',
    'approval': 'user-approved-in-preview-20261108~09 (평타 후보 C·2타 칼 수평·특수 4종 후보·발밑 맞춤·장침 띄움·평타 이펙트 G·특수 이펙트 4차 1·1·2·1·6색 정리)', 'gameIntegration': 'prepared-not-playtested',
    'note': 'rapier basic 3-hit combo and four specials body motions from Aether + user background removal; basic thrust effect G (silver stroke + flat shock rings, PPU64); special effects in the shared gold-pixel language (PPU32)'}
(A / 'asset.json').write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8')); prefix = 'characters/ink/weapons/rapier/'
g['files'] = [f for f in g['files'] if not f['path'].startswith(tuple(prefix + p for p in pre))]
for rel in sorted(added):
    d = (A / rel).read_bytes(); g['files'].append({'path': prefix + rel, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
for it in c['assets']:
    if it['id'] == 'characters.ink.weapons.rapier': it['fileCount'] = len(a['files'])
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print(len(added), 'files; body frames', len(used), '; clips', list(CLIPS))
