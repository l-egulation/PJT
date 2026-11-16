# 수리검 스킬 몸 모션·이펙트를 리소스 저장소 shuriken 에셋에 새 세트로 등록한다(2026-11-08).
#   source/skill-motion-20261108/ : 에테르 원본 시트 6장(v4 4장·v5 2장)과 움직이는 판, 프롬프트·입력, 사용자 배경 제거본, 시간표, 스크립트, pixelfx 원본
#   runtime/skill-motion-20261108/<clip>/NN.png + motion.json : 6색 정리본(640x360, 발밑 (220,327), PPU 106.667) 중 게임에 쓰는 장과 시간표
#   runtime/skill-fx-20261108/<clip>/NN.png + fx.json : 확정 이펙트(날아가는 별 b·평타 별 b·폭발 휘두름 a·폭발 a·순간이동 a·전진 회전 c2), PPU32
# 사용: python register_shuriken_resources.py <resources 경로>
import io, json, shutil, sys, zipfile, hashlib
from pathlib import Path
HERE = Path(__file__).resolve().parent
RES = Path(sys.argv[1]); A = RES / 'characters/ink/weapons/shuriken'
SRC = A / 'source/skill-motion-20261108'; RT = A / 'runtime/skill-motion-20261108'; FX = A / 'runtime/skill-fx-20261108'
for d in (SRC, RT, FX):
    if d.exists(): shutil.rmtree(d)
added = []
def put(data, dst):
    dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(data if isinstance(data, bytes) else Path(data).read_bytes()); added.append(dst.relative_to(A).as_posix())
def jdump(obj): return (json.dumps(obj, ensure_ascii=False, indent=1) + '\n').encode('utf-8')

import shuriken_pick as P
# 1) source
H = HERE / '../2_에테르결과/aether-history'
for sub in ('shuriken-v4-20261107', 'shuriken-v5-20261107'):
    for f in sorted((H / sub).iterdir()):
        if f.is_file(): put(f, SRC / 'aether' / sub / f.name)
IN = HERE / '../1_프롬프트·입력'
for f in ('수리검-입력-v4.zip', '수리검-02폭발-04회전-v5.zip'): put(IN / f, SRC / 'input' / f)
put(HERE / '../수리검_배경제거_외곽정리_v3_62장.zip', SRC / 'bgremoved/수리검_배경제거_외곽정리_v3_62장.zip')
for f in ('shuriken_pick.py', 'shuriken_clean.py', 'shuriken_finalize.py', 'shuriken-selection.json', 'bake_shuriken_fx.mjs', 'register_shuriken_resources.py'): put(HERE / f, SRC / 'tools' / f)
put(Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/90_도구/pixelfx/src/fx/ink/shuriken-skill-fx.js'), SRC / 'tools/pixelfx/shuriken-skill-fx.js')
put(Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/90_도구/pixelfx/src/fx/ink/dagger-flurry-cuts.js'), SRC / 'tools/pixelfx/dagger-flurry-cuts.js')

# 2) runtime body: 6색 정리본 zip 에서 시간표에 쓰는 장만
FOLDER = {'00': 'basic', '01': 'pierce', '02': 'explosive', '03': 'step_throw', '03b': 'step_arrive', '04': 'roll'}
ZIPNAME = {'00': '00_평타', '01': '01_큰관통', '02': '02_폭발', '03': '03_적뒤던지기', '03b': '03b_적뒤도착', '04': '04_전진회전'}
frames = {}
with zipfile.ZipFile(HERE / '../3_보정·추린프레임/수리검_6색정리_62프레임.zip') as z:
    for i in z.infolist():
        n = i.filename
        if not (i.flag_bits & 0x800):
            try: n = n.encode('cp437').decode('utf-8')
            except Exception: pass
        if n.endswith('.png'): frames[(n.split('/')[-2], int(n.split('/')[-1][:-4]))] = z.read(i)
def steps(key, end):
    tl = P.SK[key]['tl']; out = []
    for j, (t, f) in enumerate(tl):
        nxt = tl[j + 1][0] if j + 1 < len(tl) else end
        if t >= end: break
        out.append([f'{f:02d}', round(min(nxt, end) - t, 4)])
    return out
used = set()
def clip(key, start, end, impact):
    tl = [(t - start, f) for t, f in P.SK[key]['tl'] if start <= t < end]
    out = []
    for j, (t, f) in enumerate(tl):
        nxt = tl[j + 1][0] if j + 1 < len(tl) else end - start
        out.append([f'{f:02d}', round(nxt - t, 4)]); used.add((key, f))
    return {'sheet': FOLDER[key], 'steps': out, 'sourceImpact': impact}
CLIPS = {
    # 평타: 1타 0.00~0.25(발사 0.10), 2타 0.25~0.60(발사 0.12), 돌아오기 0.60~(입력 없을 때)
    'attack1': clip('00', 0, .25, .10), 'attack1_chain': clip('00', 0, .25, .10), 'attack2': clip('00', .25, .60, .12),
    'recover1': clip('00', .60, .84, -1), 'recover2': clip('00', .60, .84, -1),
    'attack_pierce': clip('01', 0, .60, .20), 'attack_explosive': clip('02', 0, .60, .20),
    'attack_step_throw': clip('03', 0, .60, .20), 'arrive_behind': clip('03b', 0, .40, -1),
    'attack_roll': clip('04', 0, .20, .20),
}
for (key, f) in sorted(used):
    put(frames[(ZIPNAME[key], f)], RT / FOLDER[key] / f'{f:02d}.png')
motion = {'format': 'deviant-shuriken-skill-motion-v1', 'source': 'source/skill-motion-20261108 (수리검_6색정리_62프레임.zip = 사용자 배경 제거본 + tools/shuriken_finalize.py)',
          'cellPx': [640, 360], 'pixelsPerUnit': round(320 / 3, 4), 'pivot': [round(220 / 640, 6), round(1 - 327 / 360, 6)],
          'note': '칸 1px = 무대 0.3px(20배 입력을 6x6 시트로 받은 칸). 발밑 (220,327), 밑창은 장마다 y=327 에 맞춤(shuriken_pick.sole_bottom).',
          'clips': CLIPS}
put(jdump(motion), RT / 'motion.json')

# 3) runtime fx: 확정 판만, 반복 클립은 끊김 없는 30장
FXSRC = HERE / 'shuriken-fx'
FXCLIPS = {'star': ('star-b', 30, True), 'star_basic': ('star-b-basic', 30, True), 'sweep': ('sweep-a', 18, False), 'blast': ('blast-a', 31, False),
           'step_out': ('step-a-out', 18, False), 'step_in': ('step-a-in', 18, False), 'roll': ('giant-c2', 30, True)}
fxj = {'format': 'deviant-shuriken-skill-fx-v1', 'fps': 50, 'pixelsPerUnit': 32, 'source': 'tools/pixelfx/shuriken-skill-fx.js (bake_shuriken_fx.mjs)', 'clips': {}}
for name, (tag, n, loop) in FXCLIPS.items():
    m = json.loads((FXSRC / tag / 'meta.json').read_text(encoding='utf-8'))
    for i in range(n): put(FXSRC / tag / f'{i:03d}.png', FX / name / f'{i:02d}.png')
    fxj['clips'][name] = {'frames': n, 'loop': loop, 'size': m['size'], 'originTopLeftPx': m['origin_px'], 'options': m['options']}
put(jdump(fxj), FX / 'fx.json')

# 4) 색인
a = json.loads((A / 'asset.json').read_text(encoding='utf-8'))
a['files'] = [f for f in a['files'] if not f.startswith(('source/skill-motion-20261108/', 'runtime/skill-motion-20261108/', 'runtime/skill-fx-20261108/'))] + sorted(added)
a.setdefault('generatedSets', {})['skill-motion-20261108'] = {"runtime": "runtime/skill-motion-20261108", "fx": "runtime/skill-fx-20261108", "source": "source/skill-motion-20261108",
    "approval": "user-approved-in-preview-20261108 (프레임·발 맞춤·6색 정리·이펙트 b/a/a/c2)", "gameIntegration": "prepared-not-playtested",
    "note": "shuriken basic 2-hit and four specials body motions from Aether (v4/v5) + user background removal; ink/yellow pixelfx skill effects at PPU32"}
(A / 'asset.json').write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8'))
prefix = 'characters/ink/weapons/shuriken/'
g['files'] = [f for f in g['files'] if not f['path'].startswith((prefix + 'source/skill-motion-20261108/', prefix + 'runtime/skill-motion-20261108/', prefix + 'runtime/skill-fx-20261108/'))]
for rel in sorted(added):
    d = (A / rel).read_bytes(); g['files'].append({'path': prefix + rel, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
for it in c['assets']:
    if it['id'] == 'characters.ink.weapons.shuriken': it['fileCount'] = len(a['files'])
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print(len(added), 'files; body frames', len(used), '; clips', list(CLIPS))
