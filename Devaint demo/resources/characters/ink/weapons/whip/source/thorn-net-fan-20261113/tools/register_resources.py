# 리소스 저장소 등록(2026-11-13): 채찍 가시 그물(whip_range) 부채 그물 이펙트 + 그물을 뺀 몸 모션 5장.
#   characters/ink/weapons/whip
#     runtime/skill-fx-20261113-thorn-net/thorn_net/NN.png + fx.json   부채 그물 33장(PPU 88.22, 피벗 = 발, 시전 0.315초부터 30fps)
#     runtime/skill-motion-20261113-thorn-net/thorn-net/15~19.png       몸 모션 그물 장에서 그물·채찍만 지운 판(주먹 남김, 640x360 그대로)
#     source/thorn-net-fan-20261113/tools/*.py                          그물 떼기·감싸기·부채 그물·굽기 스크립트
#     preview/thorn-net-fan-20261113/*                                  비교 GIF·정지컷(시안 과정과 확정 4단계)
# 기존 skill-motion-20261107 의 15~19 장은 그대로 둔다(배치만 새 판을 가리킨다).
# 사용: python register_resources.py <resources 경로>
import hashlib, json, shutil, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent; J = HERE.parent
RES = Path(sys.argv[1]); TAG = '20261113'
A = 'characters/ink/weapons/whip'
FX, BODY, SRC, PRE = f'runtime/skill-fx-{TAG}-thorn-net', f'runtime/skill-motion-{TAG}-thorn-net', f'source/thorn-net-fan-{TAG}', f'preview/thorn-net-fan-{TAG}'
added = []
def put(src, rel):
    dst = RES / A / rel; dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(Path(src).read_bytes()); added.append(rel)
for d in (FX, BODY, SRC, PRE):
    if (RES / A / d).exists(): shutil.rmtree(RES / A / d)
for p in sorted((J / '게임용/thorn_net').glob('*.png')): put(p, f'{FX}/thorn_net/{p.name}')
put(J / '게임용/thorn_net/fx.json', f'{FX}/fx.json')
for p in sorted((J / '게임용/body').glob('*.png')): put(p, f'{BODY}/thorn-net/{p.name}')
for f in ('net_scale.py', 'net_wrap.py', 'net_fan.py', 'preview_fan.py', 'preview_fan_levels.py', 'bake_thorn_net.py', 'register_resources.py', 'apply_main.py'):
    if (HERE / f).exists(): put(HERE / f, f'{SRC}/tools/{f}')
for f in ('가시그물-부드러움비교-게임속도.gif', '가시그물-부드러움비교-0.52초.png', '가시그물-부채v5-게임속도.gif', '가시그물-감싸기-정지컷.png', '가시그물-키운그물-정지컷.png'):
    if (J / f).exists(): put(J / f, f'{PRE}/{f}')
# 색인
ap = RES / A / 'asset.json'; a = json.loads(ap.read_text(encoding='utf-8')); rels = sorted(set(added)); keep = set(rels)
a['files'] = [f for f in a['files'] if f not in keep and not f.startswith((FX + '/', BODY + '/', SRC + '/', PRE + '/'))] + rels
a.setdefault('generatedSets', {})[f'thorn-net-fan-{TAG}'] = {
    'runtime': FX, 'body': BODY, 'source': SRC, 'preview': PRE,
    'approval': 'user-approved-20261113 (부채 그물, 부드러움 4 아주 강하게)', 'gameIntegration': 'prepared-not-playtested',
    'note': 'whip_range thorn net redrawn as a soft fan rooted on the whip near the first enemy: opens 0.315-0.40s, swings over and grows past the enemies (to about 10.4u), falls through them to the ground, pixels drop out 1.15-1.4s. The body frames 15-19 have the old baked net and whip removed (fist kept). skill-motion-20261107 frames stay as the previous set.'}
ap.write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8')); c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
paths = {A + '/' + r for r in rels}; g['files'] = [f for f in g['files'] if f['path'] not in paths and not f['path'].startswith(tuple(A + '/' + d + '/' for d in (FX, BODY, SRC, PRE)))]
for r in rels:
    d = (RES / A / r).read_bytes(); g['files'].append({'path': A + '/' + r, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
for it in c['assets']:
    if it['path'] == A: it['fileCount'] = len(a['files'])
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print('added', len(rels))
