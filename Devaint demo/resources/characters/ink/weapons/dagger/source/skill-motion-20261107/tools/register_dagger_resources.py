# 단검 평타 4연타·특수 넷 몸 모션과 스킬 이펙트를 리소스 저장소 dagger 에셋에 새 세트로 등록한다(2026-11-08).
#   source/skill-motion-20261107/ : 쓴 에테르 시트, 입력 묶음, 6색 정리본 zip, 선택표, 정리·굽기 스크립트, pixelfx 원본
#   runtime/skill-motion-20261107/<판>/NN.png + motion.json : 6색 정리본(640x360, 무대 2배 = PPU64) 중 게임에 쓰는 장과 시간표
#   runtime/skill-fx-20261107/<클립>/NN.png + fx.json : 확정 이펙트(숨죽이기 b·먹안개 b·삼연 도약 b·그림자 난무 e), PPU32
# 사용: python register_dagger_resources.py <resources 경로>
import io, json, shutil, sys, zipfile, hashlib
from pathlib import Path
HERE = Path(__file__).resolve().parent
RES = Path(sys.argv[1]); A = RES / 'characters/ink/weapons/dagger'
SRC = A / 'source/skill-motion-20261107'; RT = A / 'runtime/skill-motion-20261107'; FX = A / 'runtime/skill-fx-20261107'
for d in (SRC, RT, FX):
    if d.exists(): shutil.rmtree(d)
added = []
def put(data, dst):
    dst.parent.mkdir(parents=True, exist_ok=True); dst.write_bytes(data if isinstance(data, bytes) else Path(data).read_bytes()); added.append(dst.relative_to(A).as_posix())
def jdump(obj): return (json.dumps(obj, ensure_ascii=False, indent=1) + '\n').encode('utf-8')
OUT = HERE / '..'; PFX = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/90_도구/pixelfx/src/fx/ink')
ZIP = OUT / '3_보정·추린프레임/단검_6색정리_75프레임.zip'

# 1) source
for f in ('dagger-hold-breath-size-fixed.png', 'dagger-ink-smoke.png', 'dagger-triple-leap.png', 'dagger-flurry-v4-four-strikes.png'):
    put(OUT / '2_에테르결과/aether-output' / f, SRC / 'aether' / f)
for f in ('단검평타-v3-입력.zip', '단검평타-보정-입력.zip', '그림자난무-v4-네방향-입력.zip'): put(OUT / f, SRC / 'input' / f)
put(ZIP, SRC / 'cleaned/단검_6색정리_75프레임.zip')
for f in ('dagger-basic-selection-v2.json', 'dagger-skills-selection.json', 'flurry-v4-selection-B.json', 'dagger_clean.py', 'dagger_all_preview.py',
          'dagger_fx_preview.py', 'dagger_final_preview.py', 'bake_dagger_fx.mjs', 'register_dagger_resources.py'): put(HERE / f, SRC / 'tools' / f)
for f in ('dagger-hold-breath.js', 'dagger-ink-smoke.js', 'dagger-triple-leap.js', 'dagger-flurry-cuts.js', 'dagger-basic-hit.js'): put(PFX / f, SRC / 'tools/pixelfx' / f)

# 2) runtime body
frames = {}
with zipfile.ZipFile(ZIP) as z:
    for i in z.infolist():
        n = i.filename
        if not (i.flag_bits & 0x800):
            try: n = n.encode('cp437').decode('cp949')
            except Exception: pass
        if n.endswith('.png'): frames[(n.split('/')[-2][:2], n.split('/')[-1][:-4])] = z.read(i)
SHEET = {'01': 'basic', '02': 'hold_breath', '03': 'ink_smoke', '04': 'triple_leap', '05': 'flurry'}
PIVOT = {'flurry': [221, 304]}
used = set(); CLIPS = {}
def clip(name, key, tl, end, impact):
    steps = []
    for j, (t, f) in enumerate(tl):
        nxt = tl[j + 1][0] if j + 1 < len(tl) else end
        stem = f if isinstance(f, str) else f'{f:02d}'
        out = stem.replace('부_', '-') if key == '01' else stem
        steps.append([out, round(nxt - t, 4)]); used.add((key, stem, out))
    CLIPS[name] = {'sheet': SHEET[key], 'steps': steps, 'sourceImpact': impact}
basic = json.load(open(HERE / 'dagger-basic-selection-v2.json', encoding='utf-8'))
for n, c in enumerate(basic['clips']):
    clip(f'attack{n + 1}', '01', [(k['t'], f"{k['part']}부_{k['cell']:02d}") for k in c['keys']], c['length'], c['impact'])
sk = json.load(open(HERE / 'dagger-skills-selection.json', encoding='utf-8'))
for name, key, sel in (('attack_hold_breath', '02', 'hold-breath'), ('attack_ink_smoke', '03', 'ink-smoke'), ('attack_triple_leap', '04', 'triple-leap')):
    s = sk[sel]; clip(name, key, [tuple(x) for x in s['timeline']], s['end'], s['impact'])
fl = json.load(open(HERE / 'flurry-v4-selection-B.json', encoding='utf-8'))
clip('attack_flurry', '05', [tuple(x) for x in fl['timeline']], fl['end'], fl['hits'][0])
for key, stem, out in sorted(used):
    put(frames[(key, stem)], RT / SHEET[key] / f'{out}.png')
piv = lambda p: [round(p[0] / 640, 6), round(1 - p[1] / 360, 6)]
motion = {'format': 'deviant-dagger-skill-motion-v1', 'source': 'source/skill-motion-20261107/cleaned/단검_6색정리_75프레임.zip (tools/dagger_clean.py)',
          'cellPx': [640, 360], 'pixelsPerUnit': 64, 'pivot': piv([220, 304]), 'sheetPivot': {k: piv(v) for k, v in PIVOT.items()},
          'note': '칸 1px = 무대 0.5px(무대 2배). 발밑 (220,304), 그림자 난무만 (221,304). 그림자 난무 타격 0.15·0.27·0.39·0.51·0.63·0.75.',
          'clips': CLIPS}
put(jdump(motion), RT / 'motion.json')

# 3) runtime fx (확정 판)
FXC = {'hold_breath': (HERE / 'dagger-fx/dagger-hold-breath/b', False), 'ink_smoke': (HERE / 'dagger-fx/dagger-ink-smoke/b', False),
       'triple_leap': (HERE / 'dagger-fx/dagger-triple-leap/b', False), 'flurry': (HERE / 'flurry-fx/e', False)}
fxj = {'format': 'deviant-dagger-skill-fx-v1', 'pixelsPerUnit': 32, 'source': 'tools/pixelfx (bake_dagger_fx.mjs, flurry e)', 'clips': {}}
for name, (d, loop) in FXC.items():
    m = json.loads((d / 'meta.json').read_text(encoding='utf-8')); fr = sorted(d.glob('*.png'))
    for i, f in enumerate(fr): put(f, FX / name / f'{i:02d}.png')
    fxj['clips'][name] = {'frames': len(fr), 'fps': m['fps'], 'loop': loop, 'size': m['size'], 'originTopLeftPx': m['origin_px'], 'effect': m.get('effect'), 'options': m.get('options')}
put(jdump(fxj), FX / 'fx.json')

# 4) 색인
a = json.loads((A / 'asset.json').read_text(encoding='utf-8'))
a['files'] = [f for f in a['files'] if not f.startswith(('source/skill-motion-20261107/', 'runtime/skill-motion-20261107/', 'runtime/skill-fx-20261107/'))] + sorted(added)
a.setdefault('generatedSets', {})['skill-motion-20261107'] = {"runtime": "runtime/skill-motion-20261107", "fx": "runtime/skill-fx-20261107", "source": "source/skill-motion-20261107",
    "approval": "user-approved-in-preview-20261107 (6색 정리 75장, 이펙트 숨죽이기 b·먹안개 b·삼연 도약 b·그림자 난무 e)", "gameIntegration": "prepared-not-playtested",
    "note": "dagger basic 4-hit and four specials body motions from Aether + 6-colour cleanup; ink pixelfx skill effects at PPU32"}
(A / 'asset.json').write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
g = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8'))
prefix = 'characters/ink/weapons/dagger/'
g['files'] = [f for f in g['files'] if not f['path'].startswith((prefix + 'source/skill-motion-20261107/', prefix + 'runtime/skill-motion-20261107/', prefix + 'runtime/skill-fx-20261107/'))]
for rel in sorted(added):
    d = (A / rel).read_bytes(); g['files'].append({'path': prefix + rel, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
(RES / 'generated-assets.json').write_bytes((json.dumps(g, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
c = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
for it in c['assets']:
    if it['id'] == 'characters.ink.weapons.dagger': it['fileCount'] = len(a['files'])
(RES / 'catalog.json').write_bytes((json.dumps(c, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
print(len(added), 'files; body frames', len(used), '; clips', {k: (v['sheet'], len(v['steps']), round(sum(s for _, s in v['steps']), 3)) for k, v in CLIPS.items()})
