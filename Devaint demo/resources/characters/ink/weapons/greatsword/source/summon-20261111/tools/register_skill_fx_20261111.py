# 2026-11-11 확정 스킬 이펙트(먹안개 뭉게 연막 · 큰 관통 수리검 빠른 별·바닥 스파크 · 반월참 소환 검·땅 강타)를
# 리소스 저장소 dagger·shuriken·greatsword 에셋에 새 세트로 등록한다.
#   runtime/<set>/... : Unity 에 배치하는 프레임(메인 Resources 사본과 같은 바이트)과 fx.json
#   source/<set>/tools : pixelfx 원본·굽기 스크립트·등록 스크립트, preview/<set> : 실제 스테이지 방 녹화 GIF
# 사용: python register_skill_fx_20261111.py <resources 경로> <메인 Unity Resources 경로> <pixelfx 경로> <GIF 폴더>
import hashlib, json, shutil, sys
from pathlib import Path
RES, UNITY, PFX, GIF = (Path(a) for a in sys.argv[1:5])
HERE = Path(__file__).resolve()
W = RES / 'characters/ink/weapons'

def jdump(obj): return (json.dumps(obj, ensure_ascii=False, indent=1) + '\n').encode('utf-8')

SETS = {
    'dagger': {
        'set': 'skill-fx-20261111',
        'runtime': {'ink_smoke_fog': 'DaggerSkillFx/ink_smoke_fog', 'ink_smoke_fog_ink': 'DaggerSkillFx/ink_smoke_fog_ink'},
        'singles': {},
        'pixelfx': ['ink/dagger-smoke-ref.js'],
        'gifs': ['stage1-먹안개.gif', 'stage2-먹안개.gif'],
        'fx': {'format': 'deviant-dagger-skill-fx-v1', 'pixelsPerUnit': 32, 'fps': 25,
               'source': 'tools/pixelfx/dagger-smoke-ref.js (bake_skill_fx_20261111.mjs, style fog, vanish rise)',
               'clips': {'ink_smoke_fog': {'tone': 'fog', 'use': '먹안개 뭉게 연막 — 시전 화면 배경이 어두울 때(밝은 회색)'},
                         'ink_smoke_fog_ink': {'tone': 'fogInk', 'use': '먹안개 뭉게 연막 — 시전 화면 배경이 밝을 때(옅은 먹색, 아래로 짙게)'}},
               'size': [460, 260], 'originTopLeftPx': [230, 236],
               'rules': '판정 10×5(±5u, 높이 0~5u) 안에서만 그림. 크고 작은 둥근 덩이가 겹친 반투명 구름, 4초 유지 뒤 옅어지며 떠올라 걷힘'},
        'approval': 'user-approved-20261111 (먹안개 후보 B 뭉게 연막·걷힘 B·스테이지 밝기별 색)',
        'note': 'dagger_range ink smoke redesign: billowing translucent smoke-bomb cloud inside the 10x5 hit box; bright/ink tones picked by backdrop brightness',
    },
    'shuriken': {
        'set': 'skill-fx-20261111',
        'runtime': {'star_fast': 'ShurikenSkillFx/star_fast', 'star_fast_sparks': 'ShurikenSkillFx/star_fast_sparks'},
        'singles': {},
        'pixelfx': ['ink/shuriken-big-star.js', 'ink/dagger-flurry-cuts.js'],
        'gifs': ['stage1-큰관통수리검.gif', 'stage2-큰관통수리검.gif'],
        'fx': {'format': 'deviant-shuriken-skill-fx-v1', 'pixelsPerUnit': 32, 'fps': 50,
               'source': 'tools/pixelfx/shuriken-big-star.js (bake_skill_fx_20261111.mjs, quarter 0.06)',
               'clips': {'star_fast': {'options': {'trail': 'none', 'quarter': 0.06}, 'use': '큰 관통 수리검 탄(지름 2u, 0.24초에 한 바퀴). 잔상은 게임이 대시처럼 그린다'},
                         'star_fast_sparks': {'options': {'trail': 'sparksOnly', 'quarter': 0.06, 'sparks': 340, 'sparkPower': 1.35}, 'use': '탄 아래 땅이 가까울 때만 켜는 바닥 스파크'}},
               'size': [440, 120], 'originTopLeftPx': [220, 60]},
        'approval': 'user-approved-20261111 (뾰족한 네 날·2배 회전·대시식 잔상 축소·바닥 스파크)',
        'note': 'big piercing shuriken (shuriken_special) redrawn at its 2u diameter with a separate ground-spark layer',
    },
    'greatsword': {
        'set': 'summon-20261111',
        'runtime': {'slam': 'CharacterGreatswordReviewed/slam'},
        'singles': {'summoned-sword.png': 'CharacterGreatswordReviewed/summoned-sword.png'},
        'pixelfx': ['impacts/greatsword-slam.js'],
        'gifs': ['stage1-반월참.gif', 'stage2-반월참.gif'],
        'fx': {'format': 'deviant-greatsword-summon-v1',
               'source': 'summoned-sword.png = source/reviewed-20261104/summoned-sword-vfx.png 의 검은 바탕을 밝기(최대 채널)로 투명화해 480px 높이로 줄임(PPU64, 7.5u). slam = pixelfx impacts/greatsword-slam.js(반경 2.5u, 게임에서 2배 PPU16)',
               'summonedSword': {'pixelsPerUnit': 64, 'pivot': [0.5, 0.0]},
               'slam': {'pixelsPerUnit': 16, 'fps': 30, 'size': [288, 208], 'originTopLeftPx': [144, 164]},
               'timing': '타격 기준 -0.30초 판정 위쪽 선에 등장, -0.10초부터 0.1초 만에 2.4u 박힘, 0.75~0.95초 사라짐. 기울임·옆면·후광·빛줄기·반짝임은 게임 코드(GreatswordSkillVisual)가 그림'},
        'approval': 'user-approved-20261111 (빠른 낙하·깊게 박힘·입체 기울임·후광 반짝임·땅 강타)',
        'note': 'greatsword_range half-moon cut: separate summoned sword sprite + pixelfx ground slam; old baked fall frames kept as fallback',
    },
}

gen = json.loads((RES / 'generated-assets.json').read_text(encoding='utf-8'))
cat = json.loads((RES / 'catalog.json').read_text(encoding='utf-8'))
for weapon, spec in SETS.items():
    A = W / weapon; s = spec['set']
    RT, SRC, PV = A / 'runtime' / s, A / 'source' / s, A / 'preview' / s
    for d in (RT, SRC, PV):
        if d.exists(): shutil.rmtree(d)
    added = []
    def put(data, dst):
        dst.parent.mkdir(parents=True, exist_ok=True)
        dst.write_bytes(data if isinstance(data, bytes) else Path(data).read_bytes())
        added.append(dst.relative_to(A).as_posix())
    for clip, folder in spec['runtime'].items():
        for png in sorted((UNITY / folder).glob('*.png')): put(png, RT / clip / png.name)
    for name, rel in spec['singles'].items(): put(UNITY / rel, RT / name)
    put(jdump(spec['fx']), RT / 'fx.json')
    for js in spec['pixelfx']: put(PFX / 'src/fx' / js, SRC / 'tools/pixelfx' / Path(js).name)
    put(HERE.parent / 'pfx/bake-drafts.mjs', SRC / 'tools/bake_skill_fx_20261111.mjs')
    put(HERE, SRC / 'tools/register_skill_fx_20261111.py')
    for gif in spec['gifs']: put(GIF / gif, PV / gif)
    a = json.loads((A / 'asset.json').read_text(encoding='utf-8'))
    prefixes = tuple(f'{k}/{s}/' for k in ('runtime', 'source', 'preview'))
    a['files'] = [f for f in a['files'] if not f.startswith(prefixes)] + sorted(added)
    a.setdefault('generatedSets', {})[s] = {'runtime': f'runtime/{s}', 'source': f'source/{s}', 'preview': f'preview/{s}',
        'approval': spec['approval'], 'gameIntegration': 'unity-implemented-recorded-in-stage1-and-stage2-rooms-not-hand-playtested', 'note': spec['note']}
    (A / 'asset.json').write_bytes((json.dumps(a, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    prefix = f'characters/ink/weapons/{weapon}/'
    gen['files'] = [f for f in gen['files'] if not f['path'].startswith(tuple(prefix + p for p in prefixes))]
    for rel in sorted(added):
        d = (A / rel).read_bytes(); gen['files'].append({'path': prefix + rel, 'bytes': len(d), 'sha256': hashlib.sha256(d).hexdigest()})
    for it in cat['assets']:
        if it['id'] == f'characters.ink.weapons.{weapon}': it['fileCount'] = len(a['files'])
    print(weapon, s, len(added), 'files')
(RES / 'generated-assets.json').write_bytes((json.dumps(gen, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
(RES / 'catalog.json').write_bytes((json.dumps(cat, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
