"""팀원의 2스테이지 컷씬 모션 요청(boss-arena-cutscenes-20261106 motion-requests)을 5배 Aether 입력으로 다시 만든다.

- 프롬프트: 팀원 요청문 + 공통 캐릭터·화면 문구 + 크기·가면 고정 문구 (블루의 「Keep both hands empty」는 기본 자세 구체와 맞지 않아 뺀다)
- 그림: size-review runtime 의 승인 384x256 을 1920x1080 흰 바탕에 5배 nearest. 땅에 선 그림은 발 y 1000, 공중 참고 그림은 세로 가운데.

    python -I tools/build_stage2_cutscene_aether_inputs.py
"""
import hashlib
import io
import json
import shutil
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
TEST = ROOT.parents[2] / '04_테스트' / '컷씬 Aether 입력'
OUT = ROOT / 'monsters/stage-02/boss-cutscene-motions-20261107/source'
REQ = 'monsters/stage-02/boss-arena-cutscenes-20261106/preview/motion-requests/requests.json'
RUNTIME = 'monsters/stage-02/boss-motion-size-review-20261106/runtime/'
REV = 'origin/master'

# 새로 뽑을 것: id -> 사본 접두어. B1 은 채택한 들썩 대기, R2·R3 은 2페이즈 조준 사격을 다시 쓴다.
PICK = {
    'R1-rose-smug-laugh': '입장_R1_Rose-거만한웃음',
    'R6-rose-panic-reload': '입장_R6_Rose-허둥지둥장전',
    'B3-blue-scene-cast': '입장_B3_Blue-환각전장시전',
    'R4-rose-surprise-point': '전환_R4_Rose-놀람손가락질',
    'R7-rose-battle-ready': '전환_R7_Rose-전투자세진입',
    'R5-rose-backstep': '흡수_R5_Rose-뒷걸음질',
    'R8-rose-downed-aim': '흡수_R8_Rose-쓰러진채겨눔',
    'B2-blue-after-absorb': '흡수_B2_Blue-흡수직후정적',
}
AIRBORNE = {'rose-shot/00.png', 'blue-palm/07.png'}

LOCK = """HIGHEST PRIORITY: use the START image as the exact first frame. Every frame keeps exactly the same character size in pixels, the same position and the same ground line as the START image. Do NOT zoom in, enlarge, crop, reframe or scale the character at any point. Keep the same coarse pixel-art resolution and simple shapes. The pale face is a rigid, inanimate MASK with a narrow eye opening: absolutely no mouth, lips, nose, blinking or facial expression in any frame."""


def git_bytes(path):
    raw = subprocess.run(['git', 'show', f'{REV}:{path}'], cwd=ROOT, capture_output=True, check=True).stdout
    return subprocess.run(['git', 'lfs', 'smudge'], cwd=ROOT, input=raw, capture_output=True, check=True).stdout


def big(src, airborne):
    im = Image.open(io.BytesIO(src)).convert('RGBA')
    assert im.size == (384, 256), im.size
    b = im.resize((1920, 1280), Image.NEAREST)
    if airborne:
        x0, y0, x1, y1 = b.getbbox()
        top = max(0, min(1280 - 1080, (y0 + y1) // 2 - 540))
    else:
        top = 160  # 발 y 232*5-160 = 1000
    out = Image.new('RGBA', (1920, 1080), 'white')
    out.alpha_composite(b.crop((0, top, 1920, top + 1080)))
    return out.convert('RGB')


def main():
    req = json.loads(subprocess.run(['git', 'show', f'{REV}:{REQ}'], cwd=ROOT, capture_output=True, check=True).stdout)
    common = req['common']
    TEST.mkdir(parents=True, exist_ok=True)
    for m in req['motions']:
        if m['id'] not in PICK:
            continue
        who = m['character']
        char = common[who].replace(' Keep both hands empty.', '')
        prompt = '\n\n'.join([m['prompt'].strip(), LOCK, char, common['frame']]) + '\n'
        d = OUT / m['id']
        d.mkdir(parents=True, exist_ok=True)
        (d / 'prompt.txt').write_text(prompt, encoding='utf-8')
        info = {'format': 'stage2-cutscene-aether-input-v1', 'status': 'prompt-ready-awaiting-aether-output',
                'request': m['id'], 'name': m['name'], 'requestSource': f'{REQ} ({REV})', 'images': {}}
        prefix = PICK[m['id']]
        shutil.copyfile(d / 'prompt.txt', TEST / f'{prefix}-프롬프트.txt')
        for role, label in (('start', '시작'), ('end', '끝'), ('reference', '참고')):
            rel = m.get(role)
            if not rel:
                continue
            raw = git_bytes(RUNTIME + rel)
            (d / f'approved-{role}.png').write_bytes(raw)
            img = big(raw, rel in AIRBORNE)
            img.save(d / f'{role}-white-5x-1920.png')
            shutil.copyfile(d / f'{role}-white-5x-1920.png', TEST / f'{prefix}-{label}.png')
            info['images'][role] = {'approved': RUNTIME + rel, 'sha256': hashlib.sha256(raw).hexdigest(),
                                    'file': f'{role}-white-5x-1920.png',
                                    'layout': '1920x1080 white, 5x nearest, ' + ('vertically centred (airborne reference)' if rel in AIRBORNE else 'feet y 1000')}
        (d / 'input.json').write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(m['id'], list(info['images']))


if __name__ == '__main__':
    main()
