"""Aether 6x6 시트를 칸별 PNG로 자르고 묶음(1페이즈 공격, 2페이즈 공격, 2페이즈 컷씬 ...)마다 zip 하나로 묶는다.

배경 제거·크기 조정·색 보정은 하지 않는다. 원본 시트 화소를 그대로 자른다.
묶음에 든 동작이 다 뽑힌 뒤에 돌린다.

    python -I tools/pack_aether_frames.py 1페이즈_공격
"""
import io
import json
import sys
import zipfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT.parents[2] / '04_테스트' / '보스 Aether 프레임'
GRID = 6
P1 = 'monsters/stage-02/boss-phase1-attacks-20261107/source'
P2 = 'monsters/stage-02/boss-phase2-attacks-20261107/source'

# 기절은 쓰는 칸만 골라 넣는다. 번호는 원본 칸 번호 그대로.
BLUE_STUN = {
    'parts': {'frames': range(2, 7)},
    'note': """Blue 기절 (게임 기절 2.0초, 반복 없음, 발은 움직이지 않는다) - 후보 B 짧게 숙임
기절 : 03 04 05 06. 맞는 순간 03(구체가 사라짐). 고개를 조금 떨군다. 06 에서 멈춘다. 이 동안 주인공이 공격한다.
회복 : 05 04 03 02 (기절을 거꾸로). 고개를 들고 02 = 구체가 다시 뜬 기본 자세로 돌아온다.
시간 예: 03~05 한 장 70ms(0.21초) -> 06 정지 -> 1.65초에 05~02 한 장 88ms(0.35초) -> 2.0초 기본 자세.
07 부터는 더 숙이다 09 에서 앞발이 들려 뺐다.
""",
}
ROSE_STUN = {
    'parts': {'frames': range(3, 9)},
    'note': """Rose 기절 (게임 기절 2.0초, 반복 없음, 발은 들지 않는다)
기절 : 04 05 06 07 08. 맞는 순간 04. 고개를 떨구고 석궁 끝이 처진다. 08 에서 멈춘다. 이 동안 주인공이 공격한다.
회복 : 07 06 05 04 03 (기절을 거꾸로). 고개를 들고 석궁을 다시 들어 03 = 맞기 직전 자세로 돌아온다.
시간 예: 04~08 한 장 70ms(0.35초) -> 08 정지 -> 1.65초에 07~03 한 장 70ms(0.35초) -> 2.0초 기본 자세.
09 부터는 뒷발을 들어 뺐다(16~24 비틀거림·기절 고리, 25~35 정지도 안 씀).
""",
}

# 묶음 -> {zip 안 폴더 이름: Aether 시트 또는 {'sheet', 'parts', 'note'}}
GROUPS = {
    '1페이즈_공격': {
        'Rose_공중3연사': f'{P1}/rose-triple-shot-aether-01/aether-original.webp',
        'Blue_잉크터뜨리기': f'{P1}/blue-ink-burst-aether-01/aether-original.webp',
    },
    '2페이즈_공격': {
        'Rose_조준사격': f'{P2}/rose-stun-shot-aether-01/aether-original.png',
        'Rose_하늘일제사격': f'{P2}/rose-sky-volley-aether-01/aether-original.png',
        'Rose_기절': {'sheet': f'{P2}/rose-stun-aether-01/aether-original.png', **ROSE_STUN},
        'Blue_잉크던지기': f'{P2}/blue-ink-throw-aether-01/aether-original.png',
        'Blue_회오리시전': f'{P2}/blue-ink-whirl-aether-01/aether-original.png',
        'Blue_기절': {'sheet': f'{P2}/blue-stun-aether-01/aether-original.png', **BLUE_STUN},
    },
}


def cutscene_group():
    """컷씬: source/cutscene-picks.json 의 채택 순서(D판)에 쓰인 칸만, 입 지운 칸이 있으면 그것으로."""
    cs = 'monsters/stage-02/boss-cutscene-motions-20261107/source'
    picks = json.loads((ROOT / cs / 'cutscene-picks.json').read_text(encoding='utf-8'))
    names = {
        'R1-rose-smug-laugh': '입장_R1_Rose-거만한웃음',
        'R6-rose-panic-reload': '입장_R6_Rose-허둥지둥장전',
        'R4-rose-surprise-point': '전환_R4_Rose-놀람손가락질',
        'R7-rose-battle-ready': '전환_R7_Rose-전투자세진입',
        'R5-rose-backstep': '흡수_R5_Rose-뒷걸음질',
        'R8-rose-downed-aim': '흡수_R8_Rose-쓰러진채겨눔',
        'B3-blue-scene-cast': '입장_B3_Blue-환각전장시전',
        'B2-blue-after-absorb': '흡수_B2_Blue-흡수직후정적',
        'R2-rose-boast-aim': '입장_R2_Rose-석궁겨누고멈춤',
        'R3-rose-aim-release': '입장_R3_Rose-겨눔풀고대기',
    }
    group = {}
    for cid, folder in names.items():
        p = picks['motions'][cid]
        seq = p['sequence']
        fixed = ROOT / cs / f'{cid}-aether-01/mouth-removed'
        lines = [f"{p['name']} ({p.get('pick', 'D판')}, 총 {p['totalMs'] / 1000:.2f}초)",
                 '재생 순서: 칸 번호(보여 주는 ms). 같은 번호가 다시 나오면 같은 파일을 다시 쓴다.',
                 ' '.join(f'{f:02d}({ms})' for f, ms in seq)]
        if fixed.exists():
            lines.append('입' + ('·코' if cid.startswith('R4') else '') + '을 지운 칸을 넣었다. 원본 그대로의 칸은 aether-original 에 있다.')
        if cid.startswith('B2'):
            lines.append('17~35번 발밑에 그림자 타원이 그려져 있다(배경 제거 때 함께 지울 것).')
        if cid.startswith('B3'):
            lines.append('바닥 선과 01~15번의 날아가는 구체가 그려져 있다(배경 제거 때 함께 지울 것).')
        if p.get('sheet'):
            lines.append('2페이즈 Rose 조준 사격 시트의 칸을 임시로 다시 쓴다.')
        group[folder] = {'sheet': p.get('sheet') or f'{cs}/{cid}-aether-01/aether-original.png',
                         'parts': {'frames': sorted({f for f, _ in seq})},
                         'override': f'{cs}/{cid}-aether-01/mouth-removed' if fixed.exists() else None,
                         'note': '\n'.join(lines) + '\n'}
    return group


def phase3_group():
    """3페이즈 Blue: source/phase3-picks.json 의 채택 순서에 쓰인 칸만."""
    src = 'monsters/stage-02/boss-phase3-redesign-20261107/source'
    picks = json.loads((ROOT / src / 'phase3-picks.json').read_text(encoding='utf-8'))
    names = {'homing': '1_유도화살', 'bounce': '2_튕기는화살5연사', 'rain': '3_순차폭발화살비', 'fan': '4_확장광역기',
             'hover': '5_떠오르기-떠있기-착지', 'stun': '6_기절2초', 'defeat': '7_처치쓰러짐', 'transform': '8_흡수뒤변신-등장_컷씬'}
    extra = {
        'bounce': '채팅 크기(2000x1125)로만 받은 시트를 3840x2160 으로 키우고, 발 높이를 맞추고(세로 평행이동), 13·19·24·29 칸의 화살 잔상을 앞 칸으로 덮고 떨어진 잔상 조각을 지운 판에서 잘랐다(640x360). 원래 받은 시트는 aether-original.webp.',
        'transform': '채팅 크기(2000x1125) 시트라 칸이 333x187 이다. 01~09 는 비명 지르는 입을 00번 얼굴로 덮은 칸이고, 10~12 는 쓰지 않는다.',
        'defeat': '채팅 크기(2000x1125) 시트라 칸이 333x187 이다.',
        'fan': '19번 칸에 잔상 줄이 있다(B판은 19를 쓴다).',
    }
    group = {}
    for cid, folder in names.items():
        p = picks['motions'][cid]
        seq = p['sequence']
        res = 'result-03' if cid == 'bounce' else 'result-01'
        d = f'{src}/aether-{cid}/{res}'
        if cid == 'bounce':
            sheet, original = f'{d}/preview-fixed-3840.png', f'{d}/aether-original-chat.webp'
        elif cid in ('transform', 'defeat'):
            sheet = original = f'{d}/aether-original.webp'
        else:
            sheet = original = f'{d}/aether-original.png'
        fixed = ROOT / d / 'mouth-removed'
        lines = [f"3페이즈 Blue · {p['name'].split('· ')[-1]} ({p['pick']}, 총 {p['totalMs'] / 1000:.2f}초)",
                 '재생 순서: 칸 번호(보여 주는 ms). 같은 번호가 다시 나오면 같은 파일을 다시 쓴다.',
                 ' '.join(f'{f:02d}({ms})' for f, ms in seq)]
        if cid in extra:
            lines.append(extra[cid])
        group[folder] = {'sheet': sheet, 'original': original,
                         'parts': {'frames': sorted({f for f, _ in seq})},
                         'override': f'{d}/mouth-removed' if fixed.exists() else None,
                         'note': '\n'.join(lines) + '\n'}
    return group


def cut(sheet):
    w, h = sheet.size
    cw, ch = w // GRID, h // GRID
    for r in range(GRID):
        for c in range(GRID):
            x, y = round(c * w / GRID), round(r * h / GRID)
            yield r * GRID + c, sheet.crop((x, y, x + cw, y + ch))


def pack(group):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    dest = OUT_DIR / f'{group}.zip'
    with zipfile.ZipFile(dest, 'w', zipfile.ZIP_DEFLATED) as z:
        for folder, spec in GROUPS[group].items():
            spec = spec if isinstance(spec, dict) else {'sheet': spec}
            src = ROOT / spec['sheet']
            sheet = Image.open(src).convert('RGB')
            where = {}
            for part, idx in spec.get('parts', {}).items():
                for i in idx:
                    where[i] = f'{folder}/{part}'
            n = 0
            for i, im in cut(sheet):
                if spec.get('parts') and i not in where:
                    continue
                ov = ROOT / spec['override'] / f'{i:02d}.png' if spec.get('override') else None
                if ov is not None and ov.exists():
                    im = Image.open(ov).convert('RGB')
                buf = io.BytesIO()
                im.save(buf, 'PNG')
                z.writestr(f'{where.get(i, folder + "/frames")}/{i:02d}.png', buf.getvalue())
                n += 1
            if spec.get('note'):
                z.writestr(f'{folder}/순서.txt', spec['note'].encode('utf-8'))
            orig = ROOT / spec.get('original', spec['sheet'])
            z.write(orig, f'{folder}/aether-original{orig.suffix}')
            print(f'{group}/{folder}: {n}장 {sheet.size[0] // GRID}x{sheet.size[1] // GRID}')
    print('->', dest)


if __name__ == '__main__':
    if len(sys.argv) < 2:
        sys.exit('묶음 이름을 주세요: ' + ', '.join(GROUPS))
    if '컷씬' in sys.argv[1:]:
        GROUPS['컷씬'] = cutscene_group()
    if '3페이즈' in sys.argv[1:]:
        GROUPS['3페이즈'] = phase3_group()
    for g in sys.argv[1:]:
        pack(g)
