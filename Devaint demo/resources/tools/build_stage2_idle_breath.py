"""Breathing idle loops for the stage-2 bosses made only from the approved idle pixels (user: 픽셀 단위 조금씩 쪼개서 들썩거리며 대기 모션).

Each sprite is cut by two horizontal lines: a lower line and a NECK line under the hood. The lower line is the WAIST
(below the hands and the crossbow) for Rose and the KNEES for Blue and the fused Blue (user: 블루랑 흑화는 무릎쪽도 들썩해야할거 같아),
so their whole body above the knees dips. Moving a band down by one pixel deletes one row at its line and shifts every row above it down, so nothing is resized,
redrawn or recoloured; the feet and robe hem below the waist line never move. The line is the row inside the given range
whose removal changes the fewest pixels (it is most like the row below it). The head follows the shoulders one frame late.
Approved idles are read only. Output: <group>/runtime/<id>/frames/NN.png, sheet.png, motion.json; preview/index.html.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-idle-breath-20261107'
# (lower band, head band) downward offsets per frame; the head lags the shoulders by one frame.
OFFSETS = [(0, 0), (1, 0), (1, 1), (1, 1), (0, 1), (0, 0)]
CHARACTERS = [
    # id, source, waist search range and neck search range (rows below the sprite top), frame ms
    {'id': 'rose-idle-breath', 'name': '로즈 · 들썩 대기', 'source': 'monsters/stage-02/boss-siblings-lineup-20261103/runtime/pixel-poses/rose-idle.png',
     'waist': (64, 74), 'neck': (30, 38), 'frameMs': 150, 'mood': '가볍고 들뜬 호흡'},
    {'id': 'blue-idle-breath', 'name': '블루 · 들썩 대기', 'source': 'monsters/stage-02/boss-siblings-lineup-20261103/runtime/pixel-poses/blue-idle.png',
     'waist': (104, 114), 'lowerName': 'knee', 'neck': (34, 42), 'frameMs': 330, 'mood': '무심하고 느린 호흡 (팀원 요청 B1: 반복 2초, 어깨·두건 1~2px)'},
    {'id': 'fused-idle-breath', 'name': '흑화 블루 · 들썩 대기', 'source': 'monsters/stage-02/boss-phase3-redesign-20261107/runtime/idle/00.png',
     'waist': (132, 146), 'lowerName': 'knee', 'neck': (40, 48), 'frameMs': 300, 'mood': '무겁고 느린 호흡'},
]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def best_cut(a, top, lo, hi):
    """Row (absolute) in [top+lo, top+hi] whose removal is least visible: fewest pixels differing from the row below."""
    best, score = None, None
    for r in range(top + lo, top + hi + 1):
        diff = int(np.any(a[r] != a[r + 1], axis=-1).sum())
        if score is None or diff < score:
            best, score = r, diff
    return best, score


def drop_row(a, row):
    """Delete `row` and move every row above it down by one (row 0 becomes transparent)."""
    out = a.copy()
    out[1:row + 1] = a[0:row]
    out[0] = 0
    return out


def main():
    (GROUP / 'runtime').mkdir(parents=True, exist_ok=True)
    clips = []
    for c in CHARACTERS:
        src = ROOT / c['source']
        base = np.array(Image.open(src).convert('RGBA'))
        assert set(np.unique(base[:, :, 3])) <= {0, 255}, c['id']
        top = int(np.nonzero(base[:, :, 3].any(1))[0].min())
        waist, waist_cost = best_cut(base, top, *c['waist'])
        neck, neck_cost = best_cut(base, top, *c['neck'])
        assert neck < waist
        out_dir = GROUP / 'runtime' / c['id'] / 'frames'
        out_dir.mkdir(parents=True, exist_ok=True)
        frames, records = [], []
        for n, (lower, head) in enumerate(OFFSETS):
            a = base.copy()
            # Head first (its line sits above the waist line, so the waist drop below does not move it).
            for _ in range(head):
                a = drop_row(a, neck)
            for _ in range(lower):
                a = drop_row(a, waist)
            colours = {tuple(p) for p in a[a[:, :, 3] > 0][:, :3]}
            assert colours <= {tuple(p) for p in base[base[:, :, 3] > 0][:, :3]}, 'no new colours'
            assert np.array_equal(a[waist + 1:], base[waist + 1:]), 'feet and hem stay fixed'
            img = Image.fromarray(a)
            p = out_dir / f'{n:02}.png'
            img.save(p)
            frames.append(img)
            records.append({'file': f'frames/{n:02}.png', 'durationMs': c['frameMs'], 'shoulderDown': lower, 'headDown': lower + head, 'sha256': sha(p)})
        cols = len(frames)
        sheet = Image.new('RGBA', (384 * cols, 256))
        for n, f in enumerate(frames):
            sheet.paste(f, (n * 384, 0))
        sheet.save(GROUP / 'runtime' / c['id'] / 'sheet.png')
        motion = {'format': 'stage2-idle-breath-v1', 'id': c['id'], 'name': c['name'], 'status': 'candidate-not-approved', 'canvas': [384, 256], 'pivot': [192, 232],
                  'repeat': True, 'totalMs': c['frameMs'] * cols, 'mood': c['mood'], 'source': '../../../../' + c['source'], 'sourceSha256': sha(src),
                  'cuts': {'lowerLine': c.get('lowerName', 'waist'), 'waistRow': waist, 'waistRemovalCost': waist_cost, 'neckRow': neck, 'neckRemovalCost': neck_cost},
                  'method': 'row deletion only: no resampling, redraw or new colours; rows below the waist line identical to the approved idle',
                  'frames': records}
        (GROUP / 'runtime' / c['id'] / 'motion.json').write_text(json.dumps(motion, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
        clips.append({'id': c['id'], 'name': c['name'], 'mood': c['mood'], 'sheet': f'../runtime/{c["id"]}/sheet.png', 'frames': cols, 'frameMs': c['frameMs'],
                      'source': '../../../../' + c['source'], 'waist': waist, 'neck': neck})
        print(c['id'], 'waist', waist, waist_cost, 'neck', neck, neck_cost)
    (GROUP / 'preview').mkdir(exist_ok=True)
    (GROUP / 'preview' / 'clips.json').write_text(json.dumps(clips, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
