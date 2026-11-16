"""Write timing-only Bounce candidates from the preserved Aether cells; no pixel cleanup."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
OUT = GROUP / 'preview/bounce-candidates'

FRAMES = list(range(1, 32))  # 00 is the smaller input frame; 32-35 return to idle and are excluded.
PEAKS = [7, 14, 19, 25, 30]
CUES = [500, 1000, 1500, 2000, 2500]
DIRECTIONS = ['upper-left', 'right', 'upper-left', 'right', 'upper-left']
ROLES = {1: 'idle', 2: 'idle', 3: 'coil', 4: 'coil', 5: 'strike', 6: 'strike', 7: 'hit', 8: 'follow', 9: 'follow',
         10: 'follow', 11: 'coil', 12: 'coil', 13: 'strike', 14: 'hit', 15: 'follow', 16: 'coil', 17: 'strike',
         18: 'strike', 19: 'hit', 20: 'follow', 21: 'follow', 22: 'coil', 23: 'strike', 24: 'strike', 25: 'hit',
         26: 'follow', 27: 'coil', 28: 'strike', 29: 'strike', 30: 'hit', 31: 'hold'}

CANDIDATES = [
    {'id': 'A', 'name': '스냅 · 정점 멈춤',
     'durationsMs': [210, 50, 50, 110, 40, 40,
                     160, 60, 50, 50, 50, 90, 40,
                     180, 70, 150, 60, 40,
                     170, 60, 60, 110, 60, 40,
                     180, 70, 150, 60, 40,
                     150, 300],
     'note': '01~31을 한 장도 빼지 않음. 뻗기 직전 장은 40ms로 빠르게 지나가고, 방출 정점은 150~180ms 멈춤. 가슴 앞에서 팔을 감는 장(04·12·16·22·27)에 90~150ms 힘을 모음.'},
    {'id': 'B', 'name': '강한 스냅 · 짧은 연결',
     'durationsMs': [230, 40, 40, 120, 35, 35,
                     230, 45, 35, 35, 35, 85, 35,
                     240, 45, 140, 40, 35,
                     230, 45, 35, 120, 35, 35,
                     240, 45, 140, 40, 35,
                     200, 250],
     'note': 'A와 같은 31장. 연결 장을 35~45ms로 더 몰아 치고 정점을 230~240ms 세움. 박자가 가장 또렷하지만 정지감도 가장 큼.'},
    {'id': 'R', 'name': '고른 간격 · 비교 기준', 'referenceOnly': True,
     'durationsMs': [85, 83, 83, 83, 83, 83,
                     72, 72, 71, 71, 71, 71, 72,
                     100, 100, 100, 100, 100,
                     84, 84, 83, 83, 83, 83,
                     100, 100, 100, 100, 100,
                     150, 300],
     'note': '방출 시점만 맞추고 장마다 시간을 고르게 나눈 비교용. Aether GIF처럼 부드럽게 흐르는 느낌. 게임용 후보가 아님.'},
]


def main():
    OUT.mkdir(exist_ok=True)
    for c in CANDIDATES:
        d = c['durationsMs']
        assert len(d) == len(FRAMES), (c['id'], len(d))
        starts = [sum(d[:i]) for i in range(len(d))]
        assert [starts[FRAMES.index(p)] for p in PEAKS] == CUES, (c['id'], [starts[FRAMES.index(p)] for p in PEAKS])
        c.update({'sourceFrames': FRAMES, 'roles': [ROLES[f] for f in FRAMES], 'frameCount': len(FRAMES),
                  'totalMs': sum(d), 'releaseCuesMs': CUES, 'repeat': False, 'holdLastFrame': True})
    data = {
        'format': 'stage2-bounce-timing-candidates-v1',
        'status': 'timing-review-pending-user-selection-no-cleanup',
        'userRequest': '뭔가 좀 더 절도 있었으면 좋겠어',
        'source': '../../source/bounce-aether-01/aether-original.png',
        'frameBase': '../../source/bounce-aether-01/frames/',
        'frameNumbering': 'zero-based row-major',
        'sourceCellSize': [640, 360],
        'logicalCanvas': [384, 256],
        'commonTransform': {
            'scale': [666 / 640, 374 / 360], 'destinationSize': [666, 374], 'rasterOffset': [-141, -59],
            'sourceFootRow': 280, 'outputPivot': [192, 232], 'resampling': 'nearest-neighbor',
            'perFrameAutoFit': False,
            'basis': 'Bounce cells are drawn at about 96% of the approved size (frame 01 body 92x171, crossbow 103 px vs approved 95x178, 107 px). The homing 0.75 transform does not apply to this sheet.',
            'check': 'frame 01 body bounds map to about [145,54,241,232]; approved fused-idle body [145,54,240,232].'},
        'previewMatte': {'scope': 'browser memory only; not final cleanup', 'whiteMinimum': 250, 'maximumChannelDifference': 4},
        'peakFrames': PEAKS, 'releaseCuesMs': CUES, 'visualDirections': DIRECTIONS,
        'directionNote': 'Generated left releases rise about 25 degrees; right releases cross the chest at shoulder height, close to horizontal rather than 35-45 degrees up.',
        'excluded': {'00': 'Input frame drawn smaller than the generated frames.', '32-35': 'Automatic return to idle; the clip holds the fifth release.'},
        'crossbow': 'Generated crossbow pixels shown as-is; cleanup will reuse the approved 107x41 crop with translation only, as in homing D V2.',
        'candidates': CANDIDATES,
    }
    (OUT / 'candidates.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for c in CANDIDATES:
        print(c['id'], c['totalMs'], 'ms')


if __name__ == '__main__':
    main()
