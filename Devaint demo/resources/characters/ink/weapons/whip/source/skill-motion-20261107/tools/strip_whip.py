# 땡기는 장에서 손 앞의 채찍(줄·고리·그물)을 지우고 손잡이 자리·그림 속 채찍 끝 자리를 잰다(2026-11-07, 사용자: 줄은 코드로).
#   손잡이(grip): 몸(먹 아닌 색) 오른쪽 끝 둘레에서 곧게 뻗은 줄이 시작하는 자리.
#   지우기: 손잡이보다 오른쪽에 있는 먹 화소 가운데 줄 끝(가장 오른쪽 먹)과 이어진 덩어리 전부.
#   끝(end): 지우기 전 줄 덩어리의 가장 오른쪽 끝(빗나갔을 때 코드 줄을 이 자리까지 곧게 그린다).
# 결과: v7_fit/<판>/NNh.png(지운 판), strip.json(판·장별 grip/end, 칸 화소 위 기준)
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
INK = np.array([0x1B, 0x16, 0x16])
HOLD = {'1': 18, '4': 19, '3': 16, '5': 23}
# 줄이 처음부터 굵어 두께로 못 가르는 장: 확대판(grip-zoom.png)에서 손잡이가 끝나는 자리를 직접 짚었다.
GRIP_OVERRIDE = {'4': (300, 252), '5': (316, 203)}

def strip(a, override=None):
    op = a[..., 3] > 0; ink = op & np.all(a[..., :3] == INK, -1); body = op & ~ink
    ys, xs = np.where(body)
    # 몸 오른쪽 끝: 몸 화소의 오른쪽 98백분위(목도리·옷 끝 튐 무시)
    bx = int(np.percentile(xs, 98))
    lab, n = ndimage.label(ink, structure=np.ones((3, 3)))
    far_x = np.where(ink.any(0))[0].max(); far_rows = np.where(ink[:, far_x])[0]
    whip_label = lab[far_rows[0], far_x]
    whip = lab == whip_label
    # 손잡이: 몸 끝보다 오른쪽 25화소 구간 줄의 가운데 높이를 줄이 시작하는 높이로, 몸 끝 쪽으로 이어지는 마지막 열을 손잡이 x 로
    win = whip[:, bx + 8:bx + 40]; ry = np.where(win.any(1))[0]
    gy = int(np.median(ry)) if len(ry) else int(np.median(np.where(whip)[0]))
    # 손잡이 끝: 줄 높이 둘레(±10)의 먹 두께가 가는 줄 두께(맨 끝 쪽 중앙값)+1 이하로 처음 내려가는 열.
    #   손잡이·손은 줄보다 두껍다. 몸 끝 30화소 안쪽부터 오른쪽으로 훑는다.
    thick = lambda x: int(whip[max(0, gy - 10):gy + 11, x].sum())
    line_w = int(np.median([thick(x) for x in range(bx + 40, bx + 90) if thick(x) > 0] or [3]))
    gx = bx - 30
    while gx < bx + 60 and not (0 < thick(gx) <= line_w + 1 and 0 < thick(gx + 1) <= line_w + 1 and 0 < thick(gx + 2) <= line_w + 1): gx += 1
    # 줄 높이는 손잡이 앞 순수한 줄 구간(손잡이 끝 +15~+60)에서 다시 잰다(손 덩어리가 섞이면 아래로 쏠린다)
    seg = whip[:, gx + 15:gx + 60]; sy = np.where(seg.any(1))[0]
    if len(sy): gy = int(round(np.median(sy)))
    if override: gx, gy = override
    cut = whip.copy(); cut[:, :gx] = False
    out = a.copy(); out[cut] = 0
    ey, ex = np.where(whip); end = [int(ex.max()), int(np.median(ey[ex >= ex.max() - 6]))]
    return out, [gx, gy], end, int(cut.sum())

if __name__ == '__main__':
    res = {}
    for sh, f in HOLD.items():
        a = np.array(Image.open(HERE / f'v7_fit/{sh}/{f:02d}.png').convert('RGBA'))
        out, grip, end, n = strip(a, GRIP_OVERRIDE.get(sh))
        Image.fromarray(out).save(HERE / f'v7_fit/{sh}/{f:02d}h.png')
        res[sh] = {'frame': f, 'grip': grip, 'end': end, 'removed': n}
        print(sh, res[sh])
    json.dump(res, open(HERE / 'strip.json', 'w', encoding='utf-8'), indent=1)
    # 확인판: 원본 | 지운 판(+손잡이 빨강, 끝 파랑)
    rows = []
    for sh, r in res.items():
        a = Image.open(HERE / f"v7_fit/{sh}/{r['frame']:02d}.png").convert('RGBA'); b = Image.open(HERE / f"v7_fit/{sh}/{r['frame']:02d}h.png").convert('RGBA')
        t = Image.new('RGB', (1290, 360), 'white')
        for i, im in enumerate((a, b)):
            bg = Image.new('RGBA', im.size, (235, 235, 235, 255)); bg.alpha_composite(im); t.paste(bg.convert('RGB'), (i * 650, 0))
        from PIL import ImageDraw
        d = ImageDraw.Draw(t); gx, gy = r['grip']; ex, ey = r['end']
        d.ellipse((650 + gx - 4, gy - 4, 650 + gx + 4, gy + 4), outline='red', width=2); d.ellipse((650 + ex - 4, ey - 4, 650 + ex + 4, ey + 4), outline='blue', width=2)
        d.line((650 + gx, gy, 650 + ex, ey), fill=(30, 30, 30), width=3)
        rows.append(t)
    out = Image.new('RGB', (1290, 360 * len(rows)), 'white')
    for i, t in enumerate(rows): out.paste(t, (0, i * 360))
    out.save(HERE / 'strip-check.png')


def strip2(a, keep_r=30):
    """앞으로 뻗는 장용(2026-11-07): 몸(먹 아닌 색)에서 keep_r 화소 안의 먹은 손·손잡이로 남기고, 그 밖의 먹은 채찍으로 지운다.
    손잡이 끝 = 남긴 먹 가운데 지운 채찍과 맞닿은 화소들의 가운데. 채찍이 없으면 grip=None."""
    op = a[..., 3] > 0; ink = op & np.all(a[..., :3] == INK, -1); body = op & ~ink
    near = ndimage.binary_dilation(body, structure=np.ones((3, 3)), iterations=keep_r)
    whip = ink & ~near
    # 몸에서 떨어진 먹 덩어리(얼굴·바지는 몸 색과 붙어 있어 near 안) 중 작은 티끌은 채찍으로 친다
    if not whip.any(): return a.copy(), None, None, 0
    touch = ink & near & ndimage.binary_dilation(whip, structure=np.ones((3, 3)))
    ys, xs = np.where(touch)
    grip = [int(round(xs.mean())), int(round(ys.mean()))] if len(xs) else None
    out = a.copy(); out[whip] = 0
    ey, ex = np.where(whip); end = [int(ex.max()), int(np.median(ey[ex >= ex.max() - 6]))]
    return out, grip, end, int(whip.sum())
