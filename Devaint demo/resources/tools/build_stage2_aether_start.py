"""승인 384x256 캔버스 한 장으로 Aether 시작 이미지 두 가지를 만든다.

- start-white-3x.png: 512x384 흰 바탕의 (64,64)에 놓고 3배 nearest (1536x1152, 바닥 발 y 888)
- start-white-5x-1920.png: 1920x1080 흰 바탕에 5배 nearest를 (0,-160)에 (바닥 발 y 1000)

    python -I tools/build_stage2_aether_start.py <clip 폴더> <승인 그림> [<04_테스트 사본 접두어>]
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
TEST = ROOT.parents[2] / '04_테스트'


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def build(folder, approved, copy_prefix=None, copy_dir=None, feet_y='1000'):
    """feet_y: 5배 그림에서 발이 놓이는 y. 공중 자세는 900 으로 올려 아래 여백을 둔다."""
    folder = ROOT / folder
    feet_y = int(feet_y)
    top = 232 * 5 - feet_y
    src = Image.open(folder / approved).convert('RGBA')
    assert src.size == (384, 256), src.size
    a = Image.new('RGBA', (512, 384), 'white')
    a.alpha_composite(src, (64, 64))
    a.convert('RGB').resize((1536, 1152), Image.NEAREST).save(folder / 'start-white-3x.png')
    big = src.resize((1920, 1280), Image.NEAREST)
    b = Image.new('RGBA', (1920, 1080), 'white')
    b.alpha_composite(big.crop((0, top, 1920, top + 1080)), (0, 0))
    b.convert('RGB').save(folder / 'start-white-5x-1920.png')
    info = {
        'format': 'stage2-aether-input-v1',
        'status': 'prompt-ready-awaiting-aether-output',
        'approvedSource': approved,
        'approvedSourceSha256': sha(folder / approved),
        'startImage': {'file': 'start-white-5x-1920.png', 'sha256': sha(folder / 'start-white-5x-1920.png'),
                       'layout': f'1920x1080 white, approved 384x256 canvas at 5x nearest at (0,-{top}); feet y {feet_y}'},
        'startImage3x': {'file': 'start-white-3x.png', 'sha256': sha(folder / 'start-white-3x.png'),
                         'layout': '384x256 on 512x384 white at (64,64), 3x nearest; feet y 888'},
        'sheet': '6x6, 36 cells, frame 00 = input',
        'prompt': 'prompt.txt',
    }
    old = folder / 'input.json'
    if old.exists():
        info = {**json.loads(old.read_text(encoding='utf-8')), **info}
    old.write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    if copy_prefix:
        d = TEST / copy_dir
        d.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(folder / 'start-white-5x-1920.png', d / f'{copy_prefix}-시작-확대.png')
        shutil.copyfile(folder / 'start-white-3x.png', d / f'{copy_prefix}-시작.png')
        shutil.copyfile(folder / 'prompt.txt', d / f'{copy_prefix}-프롬프트.txt')
    print(folder.name, 'ok')


if __name__ == '__main__':
    build(*sys.argv[1:])
