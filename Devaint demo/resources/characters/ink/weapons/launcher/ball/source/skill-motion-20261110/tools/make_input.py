# 발사기 스킬 몸 모션 에테르 입력 묶음(2026-11-10). 폭탄·레이피어·수리검과 같은 방식:
# 총마다 게임 대기 장 CharacterApproved11/<총>/F000(192x160, PPU32, 기준점 0.4375/0.2375)을 192x108 무대(발밑 66,98)에 놓고 20배·10배 최근접 확대. 다시 칠하지 않는다.
import hashlib, json, zipfile
from pathlib import Path
from PIL import Image
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; OUT = ROOT / '1_프롬프트·입력'; OUT.mkdir(exist_ok=True)
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-skill-motion-20261110/unity/Assets/Deviant/Combat/Resources')
GUNS = {'launcher': '01-charge-shot', 'launcher_laser': '02-laser-beam', 'launcher_shotgun': '03-shotgun-blast-jump', 'launcher_grenade': '04-grenade-kneel-reload'}
STAGE, FEET = (192, 108), (66, 98)
files, mapping = [], {'purpose': 'Aether launcher skill body motion input only; nearest-neighbor integer upscale of each ink gun idle frame, no repaint or new colors',
                      'nativeStage': list(STAGE), 'nativePivotOnStage': list(FEET), 'inputPpu': 32,
                      '3840x2160': {'scale': 20, 'pivot': [FEET[0] * 20, FEET[1] * 20]}, '1920x1080': {'scale': 10, 'pivot': [FEET[0] * 10, FEET[1] * 10]},
                      'restoreToNative': 'divide by scale -> 192x108 stage; pivot (66,98) top-left = feet; PPU32. Measure feet and face size per cell (Aether cells drift 1-6%).',
                      'palette': ['#1B1616', '#025673', '#02648C', '#EBA516', '#D2B396', '#FDEEDA'], 'guns': {}}
for gun, prompt in GUNS.items():
    src = RES / f'CharacterApproved11/{gun}/F000.png'
    im = Image.open(src).convert('RGBA'); piv = (round(im.width * .4375), round(im.height * (1 - .2375)))
    off = (FEET[0] - piv[0], FEET[1] - piv[1]); stage = Image.new('RGBA', STAGE, (0, 0, 0, 0)); stage.alpha_composite(im, off)
    for k, size in ((20, '3840x2160'), (10, '1920x1080')):
        big = stage.resize((STAGE[0] * k, STAGE[1] * k), Image.NEAREST)
        white = Image.new('RGBA', big.size, (255, 255, 255, 255)); white.alpha_composite(big)
        for kind, pic in (('white', white.convert('RGB')), ('transparent', big)):
            n = f'{gun}-idle-{size}-x{k}-{kind}.png'; pic.save(OUT / n, optimize=True); files.append(n)
    mapping['guns'][gun] = {'prompt': prompt, 'input': f'unity/Assets/Deviant/Combat/Resources/CharacterApproved11/{gun}/F000.png',
                            'inputSha256': hashlib.sha256(src.read_bytes()).hexdigest(), 'inputCanvas': list(im.size), 'inputPivotTopLeft': list(piv), 'inputOffsetOnStage': list(off)}
(OUT / 'reference-mapping.json').write_text(json.dumps(mapping, ensure_ascii=False, indent=1), encoding='utf-8')
prompts = sorted(p.name for p in OUT.glob('*-prompt-en.txt'))
# 사용자가 영어 파일 이름으로는 짝을 못 맞춘다(11-10) → 스킬마다 한국어 폴더 하나에 프롬프트와 넣을 그림을 같이 둔다.
KO = {'launcher': '1_모아쏘기', 'launcher_laser': '2_레이저', 'launcher_shotgun': '3_샷건', 'launcher_grenade': '4_유탄'}
with zipfile.ZipFile(ROOT / '발사기-스킬-입력-한국어.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for gun, prompt in GUNS.items():
        d = KO[gun]
        z.write(OUT / f'{prompt}-prompt-en.txt', f'{d}/프롬프트(통째로 붙여넣기).txt')
        z.write(OUT / f'{gun}-idle-3840x2160-x20-white.png', f'{d}/넣을그림_큰판(이걸 먼저).png')
        z.write(OUT / f'{gun}-idle-1920x1080-x10-white.png', f'{d}/넣을그림_작은판(큰판이 안 올라가면).png')
    z.write(OUT / '안내.txt', '0_안내.txt'); z.write(OUT / 'reference-mapping.json', '_기준점정보.json')
print('files', len(files), 'prompts', prompts)
