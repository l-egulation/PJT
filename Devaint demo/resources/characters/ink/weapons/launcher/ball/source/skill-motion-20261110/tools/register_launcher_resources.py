# 발사기 스킬 몸 모션(v1, 레이저 v3)을 리소스 저장소 발사기 에셋 넷(ball·laser·shotgun·grenade)에 새 세트로 등록한다(2026-11-10).
#   <총 에셋>/runtime/skill-motion-20261110/<clip>/NN.png + motion.json : 정리본(640x360, 발밑 (220,327), PPU 106.667, 발·크기 맞춤, 총 회색 음영 유지)
#     모아쏘기 = ball(attack_charge_shot), 레이저 = laser(attack_laser_beam), 샷건 = shotgun(attack_blast_jump), 유탄 = grenade(attack_kneel_reload)
#     motion.json 에 칸마다 총구 자리(muzzle, 유닛, 발밑 기준, 앞쪽 +x)를 같이 적는다(게임 muzzleX/Y).
#   ball/source/skill-motion-20261110/ : 네 스킬 공용 원본(에테르 시트·움직이는 판, 입력 묶음, 배경 제거본·원본 칸, 시간표, 스크립트)
# 사용: python register_launcher_resources.py <resources 경로>
import io, json, shutil, sys, zipfile, hashlib
from pathlib import Path
HERE = Path(__file__).resolve().parent; B = HERE.parent
sys.path.insert(0, str(HERE))
import launcher_pick as P
RES = Path(sys.argv[1]); TAG = '20261110'
ASSET = {'01': 'ball', '02': 'laser', '03': 'shotgun', '04': 'grenade'}
CLIP = {'01': 'attack_charge_shot', '02': 'attack_laser_beam', '03': 'attack_blast_jump', '04': 'attack_kneel_reload'}
added = {a: [] for a in ASSET.values()}
def base(a): return RES / 'characters/ink/weapons/launcher' / a
def put(a, data, dst):
    dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(data if isinstance(data, bytes) else Path(data).read_bytes()); added[a].append(dst.relative_to(base(a)).as_posix())
def jdump(obj): return (json.dumps(obj, ensure_ascii=False, indent=1) + '\n').encode('utf-8')
def zname(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        try: n = n.encode('cp437').decode('utf-8')
        except Exception: pass
    return n
for a in ASSET.values():
    for sub in (f'source/skill-motion-{TAG}', f'runtime/skill-motion-{TAG}'):
        if (base(a) / sub).exists(): shutil.rmtree(base(a) / sub)
# 1) 공용 원본(ball)
SRC = base('ball') / f'source/skill-motion-{TAG}'
for d in ('v1-20261110', 'v3-laser-20261110'):
    for f in sorted((B / '2_에테르결과/aether-history' / d).iterdir()): put('ball', f, SRC / 'aether' / d / f.name)
for f in ('발사기-스킬-입력-한국어.zip', '레이저-보정-입력.zip', '레이저-보정-입력-v3.zip'): put('ball', B / f, SRC / 'input' / f)
for f in ('발사기_배경제거_외곽색보정_59장.zip', '발사기-원본칸-59장.zip'): put('ball', B / '3_보정·추린프레임' / f, SRC / 'bgremoved' / f)
for f in ('launcher_pick.py', 'launcher-v1-selection.json', 'launcher-v1-shift.json', 'number_sheets.py', 'make_raw_zip.py', 'write_prompts.py', 'make_input.py', 'register_launcher_resources.py'):
    put('ball', HERE / f, SRC / 'tools' / f)
# 2) 몸: 정리본 zip 에서 시간표에 쓰는 칸
cells = {}
with zipfile.ZipFile(B / '3_보정·추린프레임/발사기_정리본.zip') as z:
    for i in z.infolist():
        n = zname(i)
        if n.endswith('.png'): cells[(n.split('/')[0], int(n.split('/')[-1][:-4]))] = z.read(i)
SEL = json.loads((HERE / 'launcher-v1-selection.json').read_text(encoding='utf-8'))
def steps_of(key):
    if key == '01':   # 준비 0.2(마지막 장 = 모으는 동안 붙잡는 버티기 장) → 뗀 뒤 0.3
        tl = [(0, 0), (.04, 2), (.08, 3), (.12, 5), (.16, 8)] + [(.2 + a, b) for a, b in P.REL01]; end = .5; impact = .2
    else:
        tl = P.TL[key]; end = P.END[key]; impact = {'02': .15, '03': .15, '04': .2}[key]
    out = []
    for j, (t, f) in enumerate(tl):
        nxt = tl[j + 1][0] if j + 1 < len(tl) else end
        out.append([f'{f:02d}', round(nxt - t, 4)])
    return out, impact
for key, a in ASSET.items():
    steps, impact = steps_of(key); RT = base(a) / f'runtime/skill-motion-{TAG}'
    for f, _ in steps:
        if not (RT / CLIP[key] / f'{f}.png').exists(): put(a, cells[(P.NAMES[key], int(f))], RT / CLIP[key] / f'{f}.png')
    mz = {f: [round((SEL[key]['muzzle_cell_px'][str(int(f))][0] - 220) / P.CU, 4), round((327 - SEL[key]['muzzle_cell_px'][str(int(f))][1]) / P.CU, 4)] for f, _ in steps}
    motion = {'format': 'deviant-launcher-skill-motion-v1', 'source': f'characters/ink/weapons/launcher/ball/source/skill-motion-{TAG} (발사기_정리본.zip = 사용자 배경 제거본 + tools/launcher_pick.py)',
              'cellPx': [640, 360], 'pixelsPerUnit': round(320 / 3, 4), 'pivot': [round(220 / 640, 6), round(1 - 327 / 360, 6)],
              'note': '칸마다 발을 땅(327)에 맞추고 얼굴 크기를 게임 대기 그림에 맞췄다. 캐릭터만 6색, 총의 무채색 음영은 그대로. muzzle = 총구 자리(유닛, 발밑 기준, 앞쪽 +x).',
              'clips': {CLIP[key]: {'sheet': CLIP[key], 'steps': steps, 'sourceImpact': impact, 'muzzle': [mz[f] for f, _ in steps]}}}
    if key == '01': motion['clips'][CLIP[key]]['note'] = '모으는 동안은 준비 마지막 장(08, 버티기)을 붙잡는다. 0.2초 = 뗀 순간.'
    put(a, jdump(motion), RT / 'motion.json')
# 3) 색인
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8')); c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
for a, files in added.items():
    A = base(a); aj = json.loads((A / 'asset.json').read_text(encoding='utf-8'))
    pre = (f'source/skill-motion-{TAG}/', f'runtime/skill-motion-{TAG}/')
    aj['files'] = [f for f in aj['files'] if not f.startswith(pre)] + sorted(files)
    aj.setdefault('generatedSets', {})[f'skill-motion-{TAG}'] = {'runtime': f'runtime/skill-motion-{TAG}', **({'source': f'source/skill-motion-{TAG}'} if a == 'ball' else {'source': f'../ball/source/skill-motion-{TAG}'}),
        'approval': 'user-approved-in-preview-20261110 (인형 자세 설명 페이지로 방향 확정, 레이저는 총 고정 v3, 배경 제거는 사용자, 발·크기 맞춤)', 'gameIntegration': 'prepared-not-playtested',
        'note': 'launcher ink special body motion from Aether (each special used to reuse the light standing shot attack_single)'}
    (A / 'asset.json').write_bytes((json.dumps(aj, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    prefix = f'characters/ink/weapons/launcher/{a}/'
    g['files'] = [f for f in g['files'] if not f['path'].startswith(tuple(prefix + p for p in pre))]
    for rel in sorted(files):
        d = (A / rel).read_bytes(); g['files'].append({'path': prefix + rel, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
    for it in c['assets']:
        if it['id'] == f'characters.ink.weapons.launcher.{a}': it['fileCount'] = len(aj['files'])
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print({a: len(f) for a, f in added.items()})
