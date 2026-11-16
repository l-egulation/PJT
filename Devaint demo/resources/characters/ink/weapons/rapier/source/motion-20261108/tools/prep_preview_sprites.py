# 확인 HTML 용 임시 스프라이트(2026-11-08). 후보 A·B·C 에 쓰인 칸만 흰 배경을 빼고 같은 창으로 자른다.
# 정식 배경 제거·6색 정리 전의 미리보기 전용이다(게임 적용본 아님).
#   - 0번 칸이 입력 대기 그림과 화소까지 같은 자리라 카메라가 고정된 것으로 보고, 발밑은 모든 칸 공통 (440, 653.33) = 무대 (66,98) x 1280/192.
#   - PPU = 32 x 1280/192 = 213.33 (게임 PPU32 대기 그림과 같은 크기)
#   - 배경: 가장 어두운 채널 < 232 이거나 파랑이 흰색보다 22 이상 낮으면 그림(크림 #FDEEDA 의 파랑 218), 300화소 미만 덩어리는 버린다.
# 출력: 3_보정/preview-sprites/NNN.png + sprites.json {crop, pivot, ppu}
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage
B = Path(__file__).resolve().parent.parent
J = json.loads((Path(__file__).parent / 'rapier-basic-candidates.json').read_text(encoding='utf-8'))
OUT = B / '3_보정/preview-sprites'; OUT.mkdir(parents=True, exist_ok=True)
GS = json.loads((Path(__file__).parent / 'ground-shift.json').read_text(encoding='utf-8'))
frames = sorted({0, 90} | {f for o in ('A', 'B', 'C') for h in ('attack1', 'attack2', 'attack3') for f in J[o][h]['frames']})   # 0·90 = 앞뒤 대기 장(미리보기용)
def src(i):
    p = B / f'3_보정/hit2-angle-v2/{i:03d}.png'
    return p if p.exists() else B / f'3_보정/cells/{i:03d}.png'
masks = {}
for i in frames:
    a = np.array(Image.open(src(i)).convert('RGB')).astype(int)
    dy = int(GS['basic'].get(str(i), 0))   # 발밑 맞춤(ground_shift.py)
    if dy > 0: a = np.concatenate([np.full((dy, a.shape[1], 3), 255, int), a[:-dy]], 0)
    elif dy < 0: a = np.concatenate([a[-dy:], np.full((-dy, a.shape[1], 3), 255, int)], 0)
    m = (a.min(2) < 232) | ((255 - a[..., 2]) > 22)
    lab, n = ndimage.label(m); sizes = ndimage.sum(m, lab, range(1, n + 1))
    keep = np.isin(lab, [k + 1 for k, s in enumerate(sizes) if s >= 300])
    masks[i] = (a, keep)
ys = np.concatenate([np.nonzero(k)[0] for _, k in masks.values()]); xs = np.concatenate([np.nonzero(k)[1] for _, k in masks.values()])
x0, y0, x1, y1 = int(xs.min()) - 4, int(ys.min()) - 4, int(xs.max()) + 5, int(ys.max()) + 5
FX, FY = 66 * 1280 / 192, 98 * 1280 / 192
for i, (a, k) in masks.items():
    rgba = np.dstack([a, k * 255]).astype(np.uint8)
    Image.fromarray(rgba, 'RGBA').crop((x0, y0, x1, y1)).save(OUT / f'{i:03d}.png', optimize=True)
meta = {'crop': [x0, y0, x1, y1], 'pivot': [(FX - x0) / (x1 - x0), (y1 - FY) / (y1 - y0)], 'ppu': 32 * 1280 / 192, 'frames': frames}
(OUT / 'sprites.json').write_text(json.dumps(meta, indent=1), encoding='utf-8'); print(meta['crop'], meta['pivot'], len(frames))
