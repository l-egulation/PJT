# 채찍 가시 그물(whip_range) 그물 키우기 시안(2026-11-13). 사용자: 「판정이 저만큼 큰데 가시 그물이 너무 작다」(사진: 판정 폭 10.53 x 높이 4.97, 중심 x+4.73 y+2.48).
# 몸 모션(CharacterWhipSkills/thorn-net, 640x360, PPU 88.22, 피벗 (0.29078,0.14722) = (186.1, 306.997))에서 그물이 든 장(15~19)만 손댄다.
# 1) 그물 떼기: 먹색 덩어리 가운데 손 쪽 큰 덩어리(주먹+채찍+그물)와, 손보다 오른쪽에 떨어진 가시 조각. 주먹(쥔 자리 반지름 10px)은 몸에 남긴다.
# 2) 선 굵기 그대로 키우기: 그림을 통째로 늘리면 선도 굵어지므로, 뼈대(skeleton)만 옮기고 원래 굵기(거리 변환)로 다시 긋는다.
#    옮기기는 쥔 자리 기준, 쥔 자리보다 앞(오른쪽)은 가로 SX 배, 쥔 자리보다 위는 세로 SY 배. 뒤쪽·아래(땅으로 내려가는 채찍)는 그대로라 손과 땅에 붙은 채 늘어난다.
# 3) 배율은 타격 장(16)의 그물 끝이 판정 오른쪽 끝, 꼭대기가 판정 위쪽 끝 조금 안에 닿게.
import json, re, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as nd
from skimage.morphology import skeletonize
SRC = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Combat/Resources/CharacterWhipSkills/thorn-net')
OUT = Path(__file__).resolve().parent.parent
PPU, PX, GROUND = 88.22, 0.29078 * 640, 360 - 0.14722 * 360          # 피벗 화소(왼쪽 위 기준)
INK = np.array([27, 22, 22])
BOX_RIGHT, BOX_TOP = 4.73438 + 10.53125 / 2, 4.96875                 # 판정 오른쪽 끝·위쪽 끝(u, 발 기준)
NET = ['15', '16', '17', '18', '19']
CUT = 14                                                              # 쥔 자리에서 이만큼 앞부터 옮긴다(손잡이가 끊기지 않게 옮김의 기준점도 여기)

def split(n):
    a = np.asarray(Image.open(SRC / f'{n}.png').convert('RGBA')).copy()
    ink = (a[..., 3] > 0) & (np.abs(a[..., :3].astype(int) - INK).max(axis=2) < 40)
    lab, k = nd.label(ink, structure=np.ones((3, 3)))
    sizes = nd.sum(ink, lab, range(1, k + 1)); main = 1 + int(np.argmax(sizes))
    body = (a[..., 3] > 0) & ~ink; touch = nd.binary_dilation(body) & (lab == main)
    ty, tx = np.nonzero(touch & (np.arange(a.shape[0])[:, None] > 225)); grip = (float(tx.mean()), float(ty.mean()))   # 주먹 = 소매에 닿은 먹
    net = lab == main
    for i in range(1, k + 1):                                           # 손보다 오른쪽에 떨어진 가시 조각
        if i != main and np.nonzero(lab == i)[1].min() > grip[0] + 12: net |= lab == i
    yy, xx = np.mgrid[:a.shape[0], :a.shape[1]]
    net &= (xx > grip[0] + CUT) | (yy < grip[1] - 12)                   # 주먹과 채찍 손잡이 앞 CUT 화소는 몸에 남긴다(원래 화소 그대로). 머리 위로 지나는 그물은 옮긴다
    return a, net, (grip[0] + CUT, grip[1])

def remap(x, y, grip, sx, sy):
    gx, gy = grip
    return (gx + (x - gx) * sx if x > gx else x, gy - (gy - y) * sy if y < gy else y)

def regrow(a, net, grip, sx, sy, pad, mapper=None):
    """그물 화소를 뼈대로 줄여 옮긴 뒤 원래 굵기로 다시 긋는다. pad = (왼, 위, 오른, 아래) 캔버스 넓힘."""
    L, T, R, B = pad; H, W = net.shape
    out = Image.new('RGBA', (W + L + R, H + T + B)); d = ImageDraw.Draw(out)
    sk = skeletonize(net); dist = nd.distance_transform_edt(net)
    ys, xs = np.nonzero(sk); pts = set(zip(ys.tolist(), xs.tolist()))
    # 뼈대의 계단(1화소 꺾임)이 배율만큼 커져 선이 울퉁불퉁해지지 않게, 둘레 2화소 안 뼈대 점의 평균 자리로 옮긴다
    box = nd.uniform_filter(sk.astype(float), 5); sy_ = nd.uniform_filter(sk * np.arange(H)[:, None].astype(float), 5); sx_ = nd.uniform_filter(sk * np.arange(W)[None, :].astype(float), 5)
    smooth = {q: (sx_[q] / box[q], sy_[q] / box[q]) for q in pts}
    col = tuple(int(v) for v in INK) + (255,)
    def P(y, x):
        x2, y2 = smooth[(y, x)]
        mx, my = mapper(x2 + .5, y2 + .5) if mapper else remap(x2 + .5, y2 + .5, grip, sx, sy)
        return (mx + L, my + T)
    def width(y, x): return max(1, int(round(2 * dist[y, x] - .5)))
    for y, x in pts:
        w = width(y, x); px, py = P(y, x)
        for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
            if (y + dy, x + dx) in pts:
                qx, qy = P(y + dy, x + dx); d.line((px, py, qx, qy), fill=col, width=w)
        r = (w - 1) / 2
        if w > 2: d.ellipse((px - r - .5, py - r - .5, px + r - .5, py + r - .5), fill=col)
    return out

def build(sx, sy, pad, make_mapper=None):
    frames = {}
    for n in [f.stem for f in sorted(SRC.glob('*.png'))]:
        im = Image.open(SRC / f'{n}.png').convert('RGBA')
        canvas = Image.new('RGBA', (im.width + pad[0] + pad[2], im.height + pad[1] + pad[3]))
        if n in NET:
            a, net, grip = split(n)
            body = a.copy(); yy, xx = np.mgrid[:net.shape[0], :net.shape[1]]
            body[net & ~((xx <= grip[0] + 5) & (yy > grip[1] - 12))] = 0     # 손잡이 이음매는 원래 화소를 5화소 겹쳐 남겨 틈이 없게
            canvas.alpha_composite(Image.fromarray(body), (pad[0], pad[1]))
            canvas.alpha_composite(regrow(a, net, grip, sx, sy, pad, make_mapper(grip) if make_mapper else None))
        else:
            canvas.alpha_composite(im, (pad[0], pad[1]))
        frames[n] = canvas
    return frames

if __name__ == '__main__':
    a, net, grip = split('16')
    ys, xs = np.nonzero(net)
    reach, top = (xs.max() - grip[0]), (grip[1] - ys.min())
    want_x = (PX + BOX_RIGHT * .97 * PPU) - grip[0]; want_y = (GROUND - BOX_TOP * .95 * PPU) * -1 + grip[1]
    SX, SY = want_x / reach, want_y / top
    print('grip', grip, 'reach px', reach, 'top px', top, 'SX', round(SX, 3), 'SY', round(SY, 3))
    (OUT / '_작업' / 'scale.json').write_text(json.dumps({'SX': SX, 'SY': SY, 'grip16': grip}), encoding='utf-8')
