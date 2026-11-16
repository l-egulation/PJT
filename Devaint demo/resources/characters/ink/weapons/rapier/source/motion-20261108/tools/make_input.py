# 레이피어 평타 1·2·3타 에테르 입력 묶음(2026-11-08). 수리검 v4 와 같은 방식:
# 게임 대기 장 CharacterApproved11/rapier/F030(PPU32)을 192x108 무대(발밑 66,98)에 놓고 20배·10배 최근접 확대. 다시 칠하지 않는다.
import hashlib, json, zipfile
from pathlib import Path
from PIL import Image
HERE = Path(__file__).resolve().parent; OUT = HERE.parent / '1_프롬프트·입력'; OUT.mkdir(exist_ok=True)
SRC = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/shuriken-skill-20261108/unity/Assets/Deviant/Combat/Resources/CharacterApproved11/rapier/F030.png')
im = Image.open(SRC).convert('RGBA'); PIV = (round(im.width / 3), round(im.height * (1 - 0.12)))   # pivot (0.3333,0.12) → 왼쪽 위 기준 (64,88)
STAGE, FEET = (192, 108), (66, 98); off = (FEET[0] - PIV[0], FEET[1] - PIV[1])
stage = Image.new('RGBA', STAGE, (0, 0, 0, 0)); stage.alpha_composite(im, off)
files = []
for k, size in ((20, '3840x2160'), (10, '1920x1080')):
    big = stage.resize((STAGE[0] * k, STAGE[1] * k), Image.NEAREST)
    white = Image.new('RGBA', big.size, (255, 255, 255, 255)); white.alpha_composite(big)
    for kind, pic in (('white', white.convert('RGB')), ('transparent', big)):
        n = f'rapier-holding-{size}-x{k}-{kind}.png'; pic.save(OUT / n, optimize=True); files.append(n)
mapping = {
    'purpose': 'Aether rapier basic attack input only; nearest-neighbor integer upscale of the game idle frame, no repaint or new colors',
    'input': 'unity/Assets/Deviant/Combat/Resources/CharacterApproved11/rapier/F030.png',
    'inputSha256': hashlib.sha256(SRC.read_bytes()).hexdigest(), 'inputCanvas': list(im.size), 'inputPivotTopLeft': list(PIV), 'inputPpu': 32,
    'nativeStage': list(STAGE), 'nativePivotOnStage': list(FEET), 'inputOffsetOnStage': list(off),
    '3840x2160': {'scale': 20, 'pivot': [FEET[0] * 20, FEET[1] * 20]}, '1920x1080': {'scale': 10, 'pivot': [FEET[0] * 10, FEET[1] * 10]},
    'restoreToNative': 'divide by scale -> 192x108 stage; pivot (66,98) top-left = feet; PPU32. Aether output cells are re-measured per sheet.',
    'palette': ['#1B1616', '#025673', '#02648C', '#EBA516', '#D2B396', '#FDEEDA'], 'startAndEndIdentical': True}
(OUT / 'reference-mapping.json').write_text(json.dumps(mapping, ensure_ascii=False, indent=1), encoding='utf-8')
prompts = sorted(p.name for p in OUT.glob('*-prompt-en.txt'))
with zipfile.ZipFile(OUT.parent / '레이피어-평타·특수-입력.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for n in [f for f in files if 'white' in f] + prompts + ['reference-mapping.json', '안내.txt']: z.write(OUT / n, n)
print('offset', off, 'files', files, 'prompts', prompts)
