# 레이피어 평타 연속 v1 의 2타(27~35번 칸) 칼날 각도 고치기(2026-11-08).
# 사용자: 「2타가 칼이 아래로 가네」 → 다시 뽑지 말고 그 부분 잘라 각도만 고친다. 방향은 「살짝 올려 찌르기」.
# 26번 칸이 이미 약 +12도(위로)라서, 27~35번의 칼날만 코등이(손잡이 끝)를 축으로 돌려 위로 올린다. 몸·손·코등이는 그대로.
#   - 칼끝 쪽 검은 화소로 직선을 맞추고, 칼끝에서 손 쪽으로 따라가다 두께가 28px 넘게 뛰는 곳(코등이)을 축으로 잡는다.
#   - 축 오른쪽의 칼날 화소(직선에서 12px 안, 밝기 235 미만)를 지우고, 같은 화소를 축 기준으로 돌려 다시 얹는다.
#   - 돌린 칼날은 알파 0.5 로 끊어 같은 먹색(칼날 중앙값)으로 칠한다(번짐 없이).
# 입력: 3_보정/cells/NNN.png (원본 시트 1280x720 칸, 원본은 2_에테르결과 에 그대로)  출력: 3_보정/hit2-angle-v1/NNN.png
import json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image
B = Path(__file__).resolve().parent.parent
VER = sys.argv[1] if len(sys.argv) > 1 else 'v2'
SRC, OUT = B / '3_보정/cells', B / f'3_보정/hit2-angle-{VER}'; OUT.mkdir(parents=True, exist_ok=True)
# v1: 위로 약 11도(27번 12, 35번 10). v2: 사용자 「수평이 되어야 할 거 같아」 → 0도
TARGET = {i: 11.0 for i in range(27, 36)} if VER == 'v1' else {i: 0.0 for i in range(27, 36)}          # 화면 기준 위쪽 + 도
if VER == 'v1': TARGET[27] = 12.0; TARGET[35] = 10.0
log = {}
for i, tgt in TARGET.items():
    im = np.array(Image.open(SRC / f'{i:03d}.png').convert('RGB')).astype(int)
    lum = im.mean(2); dark = lum < 90
    ys, xs = np.nonzero(dark)
    tip_x = xs.max()
    far = (xs > tip_x - 140)                                   # 칼끝 쪽 140px 로 직선 맞춤
    a, b = np.polyfit(xs[far], ys[far], 1)
    def thick(x):                                              # x 열에서 직선 근처 검은 화소 두께
        y0 = a * x + b; col = dark[max(0, int(y0 - 30)):int(y0 + 30), x]
        return col.sum()
    x = tip_x - 10
    while x > 0 and thick(x) <= 28: x -= 1                    # 칼날은 끝 5px → 뿌리 18px 로 굵어지고, 코등이에서 40px 넘게 뛴다
    sel = (xs > x + 6) & (np.abs(a * xs + b - ys) < 14)        # 코등이~칼끝 전체로 직선을 다시 맞춘다
    a, b = np.polyfit(xs[sel], ys[sel], 1)
    gx = x + 3; gy = a * gx + b
    cur = -math.degrees(math.atan(a)); delta = tgt - cur
    # 칼날 화소: 축 오른쪽, 직선에서 12px 안, 배경보다 어두운 것
    H, W = lum.shape; YY, XX = np.mgrid[0:H, 0:W]
    dist = np.abs(a * XX - YY + b) / math.hypot(a, 1)
    blade = (XX > gx) & (dist <= 12) & (lum < 235)
    ink = np.median(im[blade & dark], axis=0).astype(np.uint8)
    out = im.copy(); out[(XX > gx) & (dist <= 15) & (lum < 253)] = 255   # 옛 칼날의 옅은 번짐까지 지운다
    alpha = Image.fromarray((blade * 255).astype(np.uint8))
    alpha = alpha.rotate(delta, resample=Image.BICUBIC, center=(gx, gy))
    m = np.array(alpha) > 127
    out[m] = ink
    Image.fromarray(out.astype(np.uint8)).save(OUT / f'{i:03d}.png')
    log[i] = {'before_deg': round(cur, 1), 'after_deg': tgt, 'pivot': [int(gx), round(float(gy), 1)]}
    print(i, log[i])
(OUT / 'angles.json').write_text(json.dumps(log, indent=1), encoding='utf-8')
