# 특수 4종 확인 HTML 용 임시 스프라이트(2026-11-08). rapier-special-candidates.json 의 C 후보에 쓰인 칸만 흰 배경을 빼고 같은 창으로 자른다.
# 시트 칸 640x360 = 무대 192x108 의 10/3배. 0번 칸이 입력 대기 그림과 같은 자리(137..402, 113..326)라 발밑은 모든 칸 공통 (220, 326.67), PPU = 32 x 10/3.
# 배경 빼기는 평타 미리보기(prep_preview_sprites.py)와 같은 기준. 정식 배경 제거·6색 정리 전의 미리보기 전용.
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage
B = Path(__file__).resolve().parent.parent
J = json.loads((Path(__file__).parent / 'rapier-special-candidates.json').read_text(encoding='utf-8'))
K = 640 / 192; FX, FY = 66 * K, 98 * K
GS = json.loads((Path(__file__).parent / 'ground-shift.json').read_text(encoding='utf-8'))
meta = {}
for skill, c in J['C'].items():
    sh = c['sheet']; OUT = B / f'3_보정/preview-sprites-{sh}'; OUT.mkdir(parents=True, exist_ok=True)
    frames = sorted(set(c['frames'])); masks = {}
    for i in frames:
        a = np.array(Image.open(B / f'3_보정/cells-{sh}/{i:03d}.png').convert('RGB')).astype(int)
        SHEETN = {'04': 'charge', '05': 'needle', '06': 'slip', '07': 'flurry'}[sh]
        dy = int(GS[SHEETN].get(str(i), 0)) - int(c.get('lift', {}).get(str(i), 0))   # 발밑 맞춤(ground_shift.py) 뒤 장침만 일부러 띄운다
        if dy > 0: a = np.concatenate([np.full((dy, a.shape[1], 3), 255, int), a[:-dy]], 0)
        elif dy < 0: a = np.concatenate([a[-dy:], np.full((-dy, a.shape[1], 3), 255, int)], 0)
        m = (a.min(2) < 232) | ((255 - a[..., 2]) > 22)
        lab, n = ndimage.label(m); sizes = ndimage.sum(m, lab, range(1, n + 1))
        masks[i] = (a, np.isin(lab, [k + 1 for k, s in enumerate(sizes) if s >= 80]))
    ys = np.concatenate([np.nonzero(k)[0] for _, k in masks.values()]); xs = np.concatenate([np.nonzero(k)[1] for _, k in masks.values()])
    x0, y0, x1, y1 = int(xs.min()) - 3, int(ys.min()) - 3, int(xs.max()) + 4, int(ys.max()) + 4
    for i, (a, k) in masks.items():
        Image.fromarray(np.dstack([a, k * 255]).astype(np.uint8), 'RGBA').crop((x0, y0, x1, y1)).save(OUT / f'{i:03d}.png', optimize=True)
    meta[sh] = {'crop': [x0, y0, x1, y1], 'pivot': [(FX - x0) / (x1 - x0), (y1 - FY) / (y1 - y0)], 'ppu': 32 * K, 'frames': frames}
    (OUT / 'sprites.json').write_text(json.dumps(meta[sh], indent=1), encoding='utf-8')
print({k: (v['crop'], len(v['frames'])) for k, v in meta.items()})
