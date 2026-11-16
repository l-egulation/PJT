"""Timing-only candidates for the second Bounce Aether output (prompt v3); no pixel cleanup."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
OUT = GROUP / 'preview/bounce-v3-candidates'

PEAKS = [6, 12, 17, 23, 28]
CUES = [500, 1000, 1500, 2000, 2500]
DIRECTIONS = ['upper-left', 'right', 'upper-left', 'right', 'upper-left']
ROLES = {1: 'idle', 2: 'coil', 3: 'coil', 4: 'coil', 5: 'strike',
         6: 'hit', 7: 'lock', 8: 'lock', 9: 'lock', 10: 'lock', 11: 'strike',
         12: 'hit', 13: 'lock', 14: 'lock', 15: 'lock', 16: 'coil',
         17: 'hit', 18: 'lock', 19: 'lock', 20: 'lock', 21: 'lock', 22: 'strike',
         23: 'hit', 24: 'lock', 25: 'lock', 26: 'lock', 27: 'coil',
         28: 'hit', 29: 'lock', 30: 'lock', 31: 'lock', 32: 'hold'}
ALL = list(range(1, 33))
NO_SMEAR = [f for f in ALL if f not in (11, 22)]

CANDIDATES = [
    {'id': 'A', 'name': '원본 박자 그대로', 'sourceFrames': ALL,
     'durationsMs': [200, 70, 70, 100, 60,
                     90, 90, 90, 90, 90, 50,
                     100, 100, 90, 90, 120,
                     90, 90, 90, 90, 90, 50,
                     100, 100, 90, 90, 120,
                     80, 80, 80, 80, 130],
     'note': '01~32를 모두 사용. 뻗은 자세 장을 90~100ms씩 고르게 보여 주고 번짐 장(11·22)은 50ms. Aether가 그린 박자를 가장 그대로 따름.'},
    {'id': 'B', 'name': '감기 강조 · 짧은 연결', 'sourceFrames': ALL,
     'durationsMs': [220, 50, 50, 140, 40,
                     100, 100, 90, 90, 85, 35,
                     90, 90, 80, 70, 170,
                     100, 100, 90, 90, 85, 35,
                     90, 90, 80, 70, 170,
                     80, 80, 80, 80, 130],
     'note': 'A와 같은 32장. 번짐 장과 뻗기 직전 장을 35~40ms로 줄이고, 가슴 앞에서 감는 장(04·16·27)에 140~170ms 힘을 모음. 튕겨 나가는 순간이 가장 빠름.'},
    {'id': 'C', 'name': '번짐 장 제외', 'sourceFrames': NO_SMEAR,
     'durationsMs': [220, 50, 50, 140, 40,
                     100, 100, 100, 100, 100,
                     90, 90, 80, 70, 170,
                     100, 100, 100, 100, 100,
                     90, 90, 80, 70, 170,
                     80, 80, 80, 80, 130],
     'note': 'B에서 소매가 번진 11·22를 뺀 30장. 왼쪽 위 정지에서 오른쪽 정지로 곧바로 넘어감. 정리 작업은 가장 깔끔하지만 두 번의 전환이 순간 교체로 보일 수 있음.'},
]


def main():
    OUT.mkdir(exist_ok=True)
    for c in CANDIDATES:
        f, d = c['sourceFrames'], c['durationsMs']
        assert len(f) == len(d), (c['id'], len(f), len(d))
        starts = [sum(d[:i]) for i in range(len(d))]
        assert [starts[f.index(p)] for p in PEAKS] == CUES, (c['id'], [starts[f.index(p)] for p in PEAKS])
        c.update({'roles': [ROLES[x] for x in f], 'frameCount': len(f), 'totalMs': sum(d),
                  'releaseCuesMs': CUES, 'repeat': False, 'holdLastFrame': True})
    data = {
        'format': 'stage2-bounce-timing-candidates-v1',
        'status': 'timing-review-pending-user-selection-no-cleanup',
        'attempt': 2, 'prompt': '../../source/bounce-aether/prompt.txt (v3 crisp)',
        'source': '../../source/bounce-aether-02/aether-original.png',
        'frameBase': '../../source/bounce-aether-02/frames/',
        'frameNumbering': 'zero-based row-major',
        'sourceCellSize': [640, 360],
        'logicalCanvas': [384, 256],
        'commonTransform': {
            'scale': [666 / 640, 374 / 360], 'destinationSize': [666, 374], 'rasterOffset': [-141, -59],
            'sourceFootRow': 280, 'outputPivot': [192, 232], 'resampling': 'nearest-neighbor',
            'perFrameAutoFit': False,
            'basis': 'Same size as attempt 1: frame 01 body 92x171 at feet row 280, about 96% of approved fused-idle.'},
        'previewMatte': {'scope': 'browser memory only; not final cleanup', 'whiteMinimum': 250, 'maximumChannelDifference': 4},
        'peakFrames': PEAKS, 'releaseCuesMs': CUES, 'visualDirections': DIRECTIONS,
        'observations': {
            'locks': 'Each release is followed by 4-5 nearly still extended frames (06-10, 12-15, 17-21, 23-26, 28-32).',
            'smears': 'Frames 11 and 22 (and the sleeve in 28) contain motion streaks; cleanup must repaint or drop them.',
            'angles': 'Left releases rise about 20-25 degrees; right releases are at shoulder height, near horizontal.',
            'mask': 'Same mask shape across frames, no mouth, small violet eye accents visible. Hand passes beside the lower-left of the mask in 05.',
            'arm': 'One arm only; right releases cross the chest from the original shoulder.',
            'excluded': {'00': 'input frame', '33-35': 'automatic return to idle'}},
        'crossbow': 'Generated crossbow bobs 6 source px vertically; cleanup will reuse the approved 107x41 crop with translation only.',
        'candidates': CANDIDATES,
    }
    (OUT / 'candidates.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for c in CANDIDATES:
        print(c['id'], c['frameCount'], c['totalMs'])


if __name__ == '__main__':
    main()
