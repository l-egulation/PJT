# 리소스 저장소 등록(2026-11-12): 맨손 기본 공격 새 잽(몸 11장 + 초승달 이펙트 10장)과 대형 폭탄 전용 투사체 1장.
#   unarmed  source/jab-combo-20261112/  에테르 원본 시트·움직이는 판, 원본 칸 zip, 사용자 배경 제거본, 정리본 16장 zip, 스크립트, 확정안
#            source/jab-combo.json       게임에 쓰는 장과 시간(approved-combo.json 과 같은 꼴)
#            runtime/jab-combo/frame-NN.png   640x360, 발밑 (220,327), 1유닛 106.6667px, 6색, 크기·발 맞춤
#            runtime/jab-fx/{jab,jab_big}/NN.png + fx.json   흰 초승달(PPU32, 104x88, 원점 (34,44) = 주먹 끝)
#            preview/jab-combo-20261112/  확정안 미리보기 GIF, 고른 칸, 이펙트 장
#   bomb     runtime/large-projectile-20261112/F00.png + projectile.json   대형 폭탄 투사체(144x144, PPU 106.6667, 가운데 피벗)
#            source/large-projectile-20261112/tools/draw_large_bomb.py, preview/large-projectile-20261112/비교.png
# 기존 세트(approved-combo, approved-thirteen 등)는 그대로 둔다.
# 사용: python register_resources.py <resources 경로>
import hashlib, json, shutil, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent; J = HERE.parent; BOMB = J.parent / 'bomb-large-projectile-20261112'
RES = Path(sys.argv[1]); TAG = '20261112'
plan = json.loads((J / '확정안.json').read_text(encoding='utf-8')); fxdraft = json.loads((J / '5_이펙트' / 'fx.json').read_text(encoding='utf-8'))
added = {}   # asset path -> [relative files]
def put(asset, data, rel):
    dst = RES / asset / rel; dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(data if isinstance(data, bytes) else Path(data).read_bytes()); added.setdefault(asset, []).append(rel)
def jdump(obj): return (json.dumps(obj, ensure_ascii=False, indent=1) + '\n').encode('utf-8')
def clear(asset, *dirs):
    for d in dirs:
        if (RES / asset / d).exists(): shutil.rmtree(RES / asset / d)

# ── 맨손 ──
U = 'characters/ink/weapons/unarmed'; SRC = f'source/jab-combo-{TAG}'
clear(U, SRC, 'runtime/jab-combo', 'runtime/jab-fx', f'preview/jab-combo-{TAG}')
put(U, J / '2_에테르결과/31dccac8-edf1-4c5f-a153-bcee66cc00db.png', f'{SRC}/aether/31dccac8-edf1-4c5f-a153-bcee66cc00db.png')
put(U, J / '2_에테르결과/aeff88d6-9ac3-45d9-ab36-ade7427e5a28.gif', f'{SRC}/aether/aeff88d6-9ac3-45d9-ab36-ade7427e5a28.gif')
put(U, J / '맨손잽-원본칸-16장.zip', f'{SRC}/bgremoved/맨손잽-원본칸-16장.zip')
put(U, J / '맨손잽_배경제거_외곽색보정_16장.zip', f'{SRC}/bgremoved/맨손잽_배경제거_외곽색보정_16장.zip')
put(U, J / '맨손잽_정리본_16장.zip', f'{SRC}/cleaned/맨손잽_정리본_16장.zip')
put(U, J / '확정안.json', f'{SRC}/확정안.json')
for f in ('number_sheet.py', 'unarmed_pick.py', 'make_raw_zip.py', 'unarmed_clean.py', 'unarmed_snappy.py', 'unarmed_fx.py', 'unarmed_fx_v3.py', 'unarmed_final_preview.py', 'register_resources.py'):
    put(U, HERE / f, f'{SRC}/tools/{f}')
cells = {int(p.stem[-2:]): p for p in (J / '4_정리본').glob('*타_*_칸*.png')}
frames = [[s['cell'] for s in plan['sequence'][k]] for k in ('1타', '2타')]; ms = [[round(s['seconds'] * 1000) for s in plan['sequence'][k]] for k in ('1타', '2타')]
for c in sum(frames, []): put(U, cells[c], f'runtime/jab-combo/frame-{c:02d}.png')
put(U, jdump({'approval': 'user-approved-20261112-jab-combo-timing-B', 'canvas': [640, 360], 'spatialPixelization': False, 'pivotFromTop': [220, 327], 'pixelsPerUnit': 106.6667,
              'reference': 'Aether cells at 192x108 stage x10/3; face size matched to UnarmedBody/idle/F01 (128px, PPU32) and feet on y=327', 'frames': frames, 'milliseconds': ms,
              'impactFrameIndex': [2, 2], 'sourceFramesOneBased': False, 'source': SRC,
              'note': '맨손 기본 공격 2타를 곧은 잽 두 번으로. 끊어치기: 자세로 0.10초 참음 → 중간 0.02초 → 타격 0.12초에 다 뻗은 장을 길게 → 빨리 거둠. 전체 시간과 타격 순간은 게임 수치 그대로(0.36/0.32초, 0.12초).'}), 'source/jab-combo.json')
W, H = fxdraft['size']; ox, oy = fxdraft['originTopLeftPx']; tip = plan['effect']['fistTipCellPx']
for clip, folder in (('jab', '나_jab'), ('jab_big', '나_jab_big')):
    for i in range(fxdraft['frames']): put(U, J / '5_이펙트' / folder / f'{i:02d}.png', f'runtime/jab-fx/{clip}/{i:02d}.png')
put(U, jdump({'format': 'deviant-unarmed-jab-fx-v1', 'pixelsPerUnit': 32, 'size': [W, H], 'originTopLeftPx': [ox, oy], 'pivot': [round(ox / W, 6), round(1 - oy / H, 6)],
              'frames': fxdraft['frames'], 'secondsPerFrame': 0.03, 'startsAt': 0.12,
              'clips': {'jab': {'attack': 'attack1', 'offsetUnits': [round((tip['1타'][0] - 220) / 106.6667, 4), round((327 - tip['1타'][1]) / 106.6667, 4)]},
                        'jab_big': {'attack': 'attack2', 'offsetUnits': [round((tip['2타'][0] - 220) / 106.6667, 4), round((327 - tip['2타'][1]) / 106.6667, 4)]}},
              'shape': fxdraft['variants']['나'], 'approval': 'user-approved-20261112-crescent-variant-나',
              'note': '주먹 앞의 흰 초승달(가운데 두껍고 앞으로 깊이 내민 모양). 5장: 작고 얇게 → 가장 두껍고 큼 → 나가며 가늘어짐 → 끊김 → 흰 점. 흰색 + 오목한 쪽 회색 #969CA8 한 줄. offsetUnits = 발밑 기준 주먹 끝(앞, 위).'}), 'runtime/jab-fx/fx.json')
put(U, J / '5_이펙트/맨손-확정안-게임속도.gif', f'preview/jab-combo-{TAG}/맨손-확정안-게임속도.gif')
put(U, J / '5_이펙트/이펙트-장.png', f'preview/jab-combo-{TAG}/이펙트-장.png')
put(U, J / '4_정리본/정리본-한눈에.png', f'preview/jab-combo-{TAG}/정리본-한눈에.png')
put(U, J / '4_정리본/맨손-시간표.png', f'preview/jab-combo-{TAG}/맨손-시간표.png')

# ── 폭탄 ──
B = 'characters/ink/weapons/bomb'
clear(B, f'runtime/large-projectile-{TAG}', f'source/large-projectile-{TAG}', f'preview/large-projectile-{TAG}')
put(B, BOMB / '대형폭탄-투사체-v1.png', f'runtime/large-projectile-{TAG}/F00.png')
put(B, jdump({'format': 'deviant-bomb-large-projectile-v1', 'size': [144, 144], 'pixelsPerUnit': 106.6667, 'pivot': [0.5, 0.5], 'bodyRadiusPx': 40, 'bodyDiameterUnits': 0.75,
              'for': 'bomb_range_special (대형 폭탄, 판정 반지름 0.4)', 'approval': 'user-approved-20261112',
              'note': '모션(skill-motion-20261110/big_heave) 속 손에 든 큰 폭탄과 같은 크기·밀도. 완전히 둥근 몸통, 위 노란 띠, 휘는 심지, 파란 반짝임. 그림 가운데 = 몸통 가운데.'}), f'runtime/large-projectile-{TAG}/projectile.json')
put(B, BOMB / '_작업/draw_large_bomb.py', f'source/large-projectile-{TAG}/tools/draw_large_bomb.py')
put(B, BOMB / '대형폭탄-투사체-v1-비교.png', f'preview/large-projectile-{TAG}/대형폭탄-투사체-비교.png')

# ── 색인 ──
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8')); c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
SETS = {U: (f'jab-combo-{TAG}', {'runtime': 'runtime/jab-combo', 'fx': 'runtime/jab-fx', 'source': SRC, 'selection': 'source/jab-combo.json',
                                 'approval': 'user-approved-20261112 (끊어치기 시간 배분 B, 초승달 이펙트 나)', 'gameIntegration': 'prepared-not-playtested',
                                 'note': 'unarmed two-hit basic as two straight jabs from an Aether sheet + user background removal; white crescent impact effect at PPU32. approved-combo stays as the previous set.'}),
        B: (f'large-projectile-{TAG}', {'runtime': f'runtime/large-projectile-{TAG}', 'source': f'source/large-projectile-{TAG}', 'approval': 'user-approved-20261112', 'gameIntegration': 'prepared-not-playtested',
                                       'note': 'dedicated round projectile for the large-bomb skill, drawn to the bomb held in its throw motion'})}
for asset, rels in added.items():
    ap = RES / asset / 'asset.json'; a = json.loads(ap.read_text(encoding='utf-8')); rels = sorted(set(rels))
    a['files'] = sorted(set(a['files']) - set(rels)) + rels if False else [f for f in a['files'] if f not in set(rels)] + rels
    a.setdefault('generatedSets', {})[SETS[asset][0]] = SETS[asset][1]
    ap.write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    paths = {asset + '/' + r for r in rels}; g['files'] = [f for f in g['files'] if f['path'] not in paths]
    for r in rels:
        d = (RES / asset / r).read_bytes(); g['files'].append({'path': asset + '/' + r, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
    for it in c['assets']:
        if it['path'] == asset: it['fileCount'] = len(a['files'])
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print({k: len(v) for k, v in added.items()}, 'frames', frames, 'ms', ms)
