# 폭탄 스킬 몸 모션(v2)과 시한폭탄 시계를 리소스 저장소 bomb 에셋에 새 세트로 등록한다(2026-11-10).
#   source/skill-motion-20261110/ : 에테르 원본 시트 5장과 움직이는 판(기록 zip), 프롬프트·입력, 사용자 배경 제거본, 시간표, 스크립트
#   runtime/skill-motion-20261110/<clip>/NN.png + motion.json : 6색 정리본(640x360, 발밑 (220,327), PPU 106.667, 크기·발 맞춤) 중 게임에 쓰는 장과 시간표
#   runtime/skill-fx-20261110/timed_clock/NN.png + fx.json : 땅에 멈춘 시한폭탄 둘레 시계(PPU32, 64x64, 가운데 원점). 00 = 나타나는 장, 01~12 = 바늘 한 칸씩
# 기존 세트(approved-thirteen·skill-fx-20261105·skill-fx-20261107-bullet)는 그대로 둔다.
# 사용: python register_bomb_resources.py <resources 경로>
import io, json, shutil, sys, zipfile, hashlib
from pathlib import Path
HERE = Path(__file__).resolve().parent; B = HERE.parent
sys.path.insert(0, str(HERE))
import bomb_pick as P
RES = Path(sys.argv[1]); A = RES / 'characters/ink/weapons/bomb'
TAG = '20261110'
SRC = A / f'source/skill-motion-{TAG}'; RT = A / f'runtime/skill-motion-{TAG}'; FX = A / f'runtime/skill-fx-{TAG}'
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
put(B / '2_에테르결과/aether-history/aether-history-1794269025197.zip', SRC / 'aether/aether-history-1794269025197.zip')
put(B / '폭탄-스킬-입력.zip', SRC / 'input/폭탄-스킬-입력-v2.zip')
put(B / '1_프롬프트·입력/v1/폭탄-스킬-입력-v1.zip', SRC / 'input/폭탄-스킬-입력-v1.zip')
put(B / '3_보정·추린프레임/폭탄v2_배경제거_외곽색보정_59장.zip', SRC / 'bgremoved/폭탄v2_배경제거_외곽색보정_59장.zip')
put(B / '3_보정·추린프레임/폭탄v2-원본칸-59장.zip', SRC / 'bgremoved/폭탄v2-원본칸-59장.zip')
for f in ('bomb_pick.py', 'bomb-v2-selection.json', 'bomb-v2-shift.json', 'number_sheets.py', 'make_raw_zip.py', 'write_prompts.py', 'skills_v2.py', 'make_input.py', 'register_bomb_resources.py'):
    put(HERE / f, SRC / 'tools' / f)
# 2) runtime body
SHEET = {'01': 'triple_kick', '02': 'big_heave', '03': 'missile_launch', '03b': 'missile_emerge', '04': 'timed_toss'}
cells = {}
with zipfile.ZipFile(B / '3_보정·추린프레임/폭탄v2_6색정리.zip') as z:
    for i in z.infolist():
        n = zname(i)
        if n.endswith('.png'): cells[(n.split('/')[0], int(n.split('/')[-1][:-4]))] = z.read(i)
used = set()
def clip(key, impact):
    tl = P.TL[key]; end = P.END[key]; out = []
    for j, (t, f) in enumerate(tl):
        nxt = tl[j + 1][0] if j + 1 < len(tl) else end
        out.append([f'{f:02d}', round(nxt - t, 4)]); used.add((key, f))
    return {'sheet': SHEET[key], 'steps': out, 'sourceImpact': impact}
# 시간은 data/combat/ranged_basic_design.json bomb 그대로: 3연투 던짐 0.20(0.35·0.50)/끝 0.90, 대형 0.30/0.90, 미사일 준비 0.20(그 뒤 몸 숨김), 시한 0.20/0.60
CLIPS = {'attack_triple_kick': clip('01', .20), 'attack_big_heave': clip('02', .30), 'attack_missile': clip('03', .20),
         'missile_emerge': clip('03b', -1), 'attack_timed_toss': clip('04', .20)}
for key, f in sorted(used): put(cells[(P.NAMES[key], f)], RT / SHEET[key] / f'{f:02d}.png')
motion = {'format': 'deviant-bomb-skill-motion-v1', 'source': f'source/skill-motion-{TAG} (폭탄v2_6색정리.zip = 사용자 배경 제거본 + tools/bomb_pick.py 발·크기 맞춤·날아가는 폭탄 지움·6색)',
          'cellPx': [640, 360], 'pixelsPerUnit': round(320 / 3, 4), 'pivot': [round(220 / 640, 6), round(1 - 327 / 360, 6)],
          'note': '칸 1px = 무대 0.3px. 발밑 (220,327). 칸마다 얼굴 크기를 게임 대기 그림(시트 #0)에 맞췄다. 3연속 차기 공중 칸은 뛴 높이를 살렸고, 미사일 복귀는 게임이 몸을 띄우므로 땅에 맞췄다.',
          'clips': CLIPS,
          'tripleKickBombs': {'toss': .08, 'from': [.42, 1.1], 'apex': [[.55, 2.0], [.6, 2.35], [.65, 2.7]], 'releases': [.20, .35, .50],
                              'note': '띄운 폭탄 셋(차일 때까지 몸 앞에 떠 있는 판). 단위 = 유닛, 발밑 기준, 앞쪽 +x. 끝점은 게임이 폭탄을 만드는 자리(앞 0.5, 높이 1).'}}
put(jdump(motion), RT / 'motion.json')
# 3) runtime fx: 시계
fxj = {'format': 'deviant-bomb-skill-fx-v1', 'fps': 30, 'pixelsPerUnit': 32, 'source': 'tools/bomb_pick.py clock_image', 'clips': {}}
from PIL import Image
frames = [P.clock_image(0.0)] + [P.clock_image(.06 + k * .125) for k in range(12)]
for i, im in enumerate(frames):
    buf = io.BytesIO(); im.save(buf, 'PNG', optimize=True); put(buf.getvalue(), FX / 'timed_clock' / f'{i:02d}.png')
fxj['clips']['timed_clock'] = {'frames': len(frames), 'size': [64, 64], 'originTopLeftPx': [32, 32], 'centerAboveFloor': .72,
                               'note': '00 = 땅에 멈춘 뒤 0.06초 동안 작게 나타나는 장, 01~12 = 바늘이 심지 시간/12 마다 30도씩. 고리 밑이 땅에 닿게 가운데를 바닥 위 0.72유닛에 둔다.'}
put(jdump(fxj), FX / 'fx.json')
# 4) 색인
a = json.loads((A / 'asset.json').read_text(encoding='utf-8'))
pre = (f'source/skill-motion-{TAG}/', f'runtime/skill-motion-{TAG}/', f'runtime/skill-fx-{TAG}/')
a['files'] = [f for f in a['files'] if not f.startswith(pre)] + sorted(added)
a.setdefault('generatedSets', {})[f'skill-motion-{TAG}'] = {'runtime': f'runtime/skill-motion-{TAG}', 'fx': f'runtime/skill-fx-{TAG}', 'source': f'source/skill-motion-{TAG}',
    'approval': 'user-approved-in-preview-20261109~10 (인형 자세 설명 페이지로 방향 확정, 3연속 차기·대형 스로인·미사일 다이빙/복귀·시한폭탄 바로 던지기와 폭탄 둘레 시계, 배경 제거는 사용자, 발·크기 맞춤, 눈은 그대로)',
    'gameIntegration': 'prepared-not-playtested',
    'note': 'bomb four special body motions from Aether v2 + user background removal (each special used to reuse the basic throw bomb/attack1); timed bomb clock ring at PPU32'}
(A / 'asset.json').write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8')); prefix = 'characters/ink/weapons/bomb/'
g['files'] = [f for f in g['files'] if not f['path'].startswith(tuple(prefix + p for p in pre))]
for rel in sorted(added):
    d = (A / rel).read_bytes(); g['files'].append({'path': prefix + rel, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
for it in c['assets']:
    if it['id'] == 'characters.ink.weapons.bomb': it['fileCount'] = len(a['files'])
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print(len(added), 'files; body frames', len(used), '; clips', {k: len(v['steps']) for k, v in CLIPS.items()})
