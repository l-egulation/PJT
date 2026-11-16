# 3챕터 출구 A 「서가 두짝문」 2차: 크게(96×112 도트를 2배 = 192×224, 6×7u) 디테일을 더 챙긴다.
# 박공(펼친 책 문장·빛살·꼭대기 장식) · 처마(이빨 장식·책 무늬 띠) · 반원 채광창 · 서가 기둥(기둥머리·받침·기운 책·두루마리·벽등)
# · 두 짝 문(위 칸 서가 새김, 아래 칸 돋을 판, 놋쇠 경첩·리벳·문고리·열쇠판) · 돌계단 두 단. 열리면 계단에 빛이 쏟아진다. 8장.
from pathlib import Path
import math
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
OUT = HERE.parent / 'build'   # python door_a2.py 가 여기에 A2-00~07 과 GIF 를 쓴다(커밋하지 않음). runtime 은 그 사본이다
W, H, N = 96, 112, 8
def C(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], np.uint8)
INK, D0, D1, D2, D3 = C('#1c2b32'), C('#223239'), C('#283b40'), C('#2e4146'), C('#374a51')
S1, S2, S3, S4, S5, S6, S7 = C('#45585e'), C('#4a6466'), C('#556f6d'), C('#67817b'), C('#8ba095'), C('#a1b1a5'), C('#b7c2b6')
EDGE, MOSS, MOSS2 = C('#3f3e3c'), C('#586f55'), C('#7a9370')
WD0, WD1, WD2, WD3, WD4, WD5 = C('#241f1c'), C('#352c25'), C('#4a3d32'), C('#5f4e3f'), C('#78644f'), C('#937c62')
PA0, PA1, PA2 = C('#8f8a78'), C('#b9b39f'), C('#d9d2bb')
BR0, BR1, BR2, BR3 = C('#5a4d31'), C('#7d6c44'), C('#a28f5c'), C('#c9b47a')
LT0, LT1, LT2, LT3 = C('#6b4f31'), C('#b07f43'), C('#e0b46a'), C('#f6e2a6')
GL0, GL1 = C('#26343a'), C('#3a4a4c')
BOOKS = [C('#5a3f3c'), C('#405040'), C('#3e4a5e'), C('#665233'), C('#523f56'), C('#5d4c3c'), C('#4b5a5a')]
BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + .5) / 16
YY, XX = np.mgrid[0:H, 0:W]
CX = 47.5

def canvas(): return np.zeros((H, W, 4), np.uint8)
def R(a, x0, y0, x1, y1, col, cond=None):
    m = (XX >= x0) & (XX < x1) & (YY >= y0) & (YY < y1)
    if cond is not None: m &= cond
    a[m] = (*col, 255); return m
def P(a, x, y, col):
    if 0 <= x < W and 0 <= y < H: a[y, x] = (*col, 255)
def outline(a, col=EDGE):
    al = a[..., 3] > 0; p = np.pad(al, 1)
    ring = (p[2:, 1:-1] | p[:-2, 1:-1] | p[1:-1, 2:] | p[1:-1, :-2]) & ~al
    a[ring] = (*col, 255); return a
def over(d, s): m = s[..., 3] > 0; d[m] = s[m]; return d
def dither(v, levels): return np.floor(v * levels + BAYER[YY % 4, XX % 4] * .9)

# ---------------------------------------------------------------- 재기(jaggies) 규칙: 직선은 같은 마디만, 곡선은 마디가 한 방향으로만
CHECKS = {}
def arc_halfwidths(r, rows):
    """반원(가운데 47.5)의 줄마다 반폭. 아래(밑동)에서 위로 갈수록 줄어든다."""
    return [int(math.floor(math.sqrt(max(0, r * r - (k + .5) ** 2)) + .5)) for k in range(rows)]
def monotone_arc(hw):
    """곡선 검사: 가로 마디(줄마다 줄어든 칸)는 위로 갈수록 늘기만, 세로 마디(같은 폭으로 이어진 줄)는 줄기만."""
    d = [hw[i] - hw[i + 1] for i in range(len(hw) - 1)]
    vert, run = [], 1
    for x in d:
        if x == 0: run += 1
        else: vert.append(run); run = 1
    horiz = [x for x in d if x > 0]
    return all(horiz[i] <= horiz[i + 1] for i in range(len(horiz) - 1)) and all(vert[i] >= vert[i + 1] for i in range(len(vert) - 1)), d
def pick_arc(r0, rows):
    for dr in (0, .1, -.1, .2, -.2, .3, -.3, .4, -.4, .5, -.5):
        hw = arc_halfwidths(r0 + dr, rows)
        if monotone_arc(hw)[0]: return hw
    raise ValueError('no monotone arc near %s' % r0)
def line_pts(x0, y0, sx, sy, run, n):
    """마디 run 칸마다 한 칸씩 꺾이는 직선(같은 마디만). sx,sy 는 ±1, run>=1 이면 가로 run 칸·세로 1칸."""
    pts = []; x, y = x0, y0
    for i in range(n):
        pts.append((x, y))
        if (i + 1) % run == 0: y += sy
        x += sx
    return pts

def books(a, x0, x1, y0, y1, seed, lean=True, scroll=True):
    """선반 한 칸에 책을 꽂는다: 높이·폭이 다른 책, 가끔 기운 책·눕힌 책·두루마리."""
    R(a, x0, y0, x1, y1, WD0)
    x = x0; i = 0
    while x < x1:
        h = (x * 7 + seed * 13 + i * 5) % 11
        if scroll and h == 3 and x + 3 <= x1:                      # 두루마리(눕힌 원통)
            R(a, x, y1 - 2, x + 3, y1, PA1); P(a, x, y1 - 2, PA2); P(a, x + 2, y1 - 1, PA0); x += 3; i += 1; continue
        bw = 1 + (h % 3 == 0); bh = (y1 - y0) - 1 - h % 3
        col = BOOKS[(x + seed + i) % len(BOOKS)]
        if lean and h == 7 and x + 3 <= x1:                        # 기운 책(사선)
            for k in range(bh): P(a, x + k // 3, y1 - 1 - k, col); P(a, x + 1 + k // 3, y1 - 1 - k, col)   # 1:3 기울기
            x += 3; i += 1; continue
        R(a, x, y1 - bh, min(x + bw, x1), y1, col)
        if bh > 3: P(a, x, y1 - bh + 1, PA0 if i % 3 == 0 else BR1)  # 책등 띠
        x += bw; i += 1
    R(a, x0, y1, x1, y1 + 1, WD4)                                   # 선반 앞전

# ---------------------------------------------------------------- 틀
def frame():
    a = canvas()
    # 돌계단 두 단 + 이끼
    R(a, 2, 104, 94, 108, S4); R(a, 2, 104, 94, 105, S6); R(a, 2, 107, 94, 108, S3)
    R(a, 0, 108, 96, 112, S3); R(a, 0, 108, 96, 109, S5); R(a, 0, 111, 96, 112, S1)
    for x in range(4, 92, 11): R(a, x, 105, x + 1, 107, S2)
    for x in range(2, 96, 13): R(a, x, 109, x + 1, 111, S2)
    for (x, y) in [(3, 103), (4, 103), (90, 103), (91, 103), (1, 107), (93, 107)]: P(a, x, y, MOSS); P(a, x, y - 1, MOSS2) if x % 2 else None
    # 기둥(서가 기둥): 받침 · 몸통(서가 다섯 칸) · 기둥머리
    for x0 in (5, 76):
        R(a, x0 - 1, 96, x0 + 16, 104, WD3); R(a, x0 - 1, 96, x0 + 16, 97, WD5); R(a, x0 - 1, 100, x0 + 16, 101, WD2)
        R(a, x0, 40, x0 + 15, 96, WD3)
        R(a, x0, 40, x0 + 1, 96, WD5); R(a, x0 + 14, 40, x0 + 15, 96, WD1)       # 왼쪽 빛·오른쪽 그늘
        for k, (y0, y1) in enumerate([(44, 54), (56, 66), (68, 78), (80, 90)]):
            books(a, x0 + 2, x0 + 13, y0, y1, k * 3 + x0)
        R(a, x0 + 2, 91, x0 + 13, 95, WD2); R(a, x0 + 6, 92, x0 + 9, 94, BR1)   # 맨 아래 서랍 + 손잡이
        R(a, x0 - 2, 34, x0 + 17, 40, WD4); R(a, x0 - 2, 34, x0 + 17, 35, WD5); R(a, x0 - 1, 38, x0 + 16, 40, WD2)
        for x in range(x0 - 1, x0 + 16, 3): P(a, x, 37, WD1)                    # 기둥머리 홈
    # 벽등(기둥 바깥 면): 놋쇠 받침 + 유리 등 + 둥근 빛
    for lx, side in ((4, -1), (91, 1)):
        R(a, lx - (1 if side < 0 else 0), 62, lx + (1 if side > 0 else 1), 64, BR1)
        R(a, lx - 1, 56, lx + 2, 62, LT2); R(a, lx, 57, lx + 1, 61, LT3); R(a, lx - 1, 55, lx + 2, 56, BR2); R(a, lx - 1, 62, lx + 2, 63, BR0)
    # 처마: 아래 들보(놋쇠 줄) · 책 무늬 띠 · 이빨 장식 · 위 돌출
    R(a, 3, 28, 93, 34, WD3); R(a, 3, 33, 93, 34, BR1); R(a, 3, 28, 93, 29, WD5)
    x = 8; i = 0
    while x < 88:                                                       # 띠에 새긴 책등
        bw = 2; R(a, x, 30, x + bw, 33, BOOKS[i % len(BOOKS)]); P(a, x, 31, BR2 if i % 2 else PA0); x += bw + 1; i += 1
    R(a, 1, 22, 95, 28, WD4); R(a, 1, 22, 95, 23, WD5)
    for x in range(2, 94, 3): R(a, x, 26, x + 2, 28, WD2)                # 이빨 장식
    # 박공: 아래가 넓은 세모, 안쪽 판에 펼친 책 문장과 빛살
    hwp = 3 * (YY - 6)                                             # 박공 비탈 1:3(줄마다 정확히 3칸)
    ped = (YY >= 7) & (YY < 22) & (XX >= 48 - hwp) & (XX < 48 + hwp)
    a[ped] = (*WD3, 255)
    rim = ped & ((XX < 48 - hwp + 3) | (XX >= 48 + hwp - 3)); a[rim] = (*WD5, 255)
    inner = ped & ~rim & (YY >= 10); a[inner] = (*WD2, 255)
    # 빛살: 좁은 판이라 짧은 세로 셋(세로선은 재기가 없다)
    for x0 in (41, 47, 54):
        for y in range(10, 13 if x0 != 47 else 14): P(a, x0, y, BR1); P(a, x0 + 1, y, BR1) if x0 == 47 else None
    bk = (YY >= 14) & (YY < 19) & (XX >= 40) & (XX < 56) & (np.abs(XX + .5 - 48) <= 2 * (YY - 14) + 2)   # 펼친 책: 1:2 비탈
    a[bk] = (*BR3, 255); a[bk & ((XX == 47) | (XX == 48)) & (YY > 14)] = (*BR0, 255)
    for yy in (16, 17): R(a, 42, yy, 46, yy + 1, BR2, (XX + yy) % 2 == 0); R(a, 50, yy, 54, yy + 1, BR2, (XX + yy) % 2 == 1)
    CHECKS['pediment'] = [48 - 3 * (y - 6) for y in range(7, 22)]
    # 꼭대기 장식(놋쇠 깃펜 촉)
    R(a, int(CX) - 1, 2, int(CX) + 1, 7, BR2); R(a, int(CX) - 2, 5, int(CX) + 2, 7, BR1); P(a, int(CX), 1, BR3); P(a, int(CX) - 1, 1, BR3)
    # 반원 채광창 둘레(문틀 위)와 모서리 판
    R(a, 20, 34, 76, 46, WD2)
    outer = pick_arc(15, 15); inner_hw = pick_arc(13, 13); lead_hw = pick_arc(7, 7)
    CHECKS['fanlight'] = outer; CHECKS['fanlight-inner'] = inner_hw; CHECKS['fanlight-lead'] = lead_hw
    for k in range(15):                                            # k = 밑동(45줄)에서 위로 k 줄
        y = 45 - k; R(a, 48 - outer[k], y, 48 + outer[k], y + 1, WD5)
        if k < 13 and inner_hw[k] > 0: R(a, 48 - inner_hw[k], y, 48 + inner_hw[k], y + 1, GL0)
    for k in range(13):
        y = 45 - k
        if k < 7 and lead_hw[k] > 0: P(a, 48 - lead_hw[k], y, BR0); P(a, 47 + lead_hw[k], y, BR0)
        if inner_hw[k] > 0: P(a, 47, y, BR0); P(a, 48, y, BR0)
    for k in range(1, 12):                                         # 1:1 대각 납선
        for (x, y) in ((47 - k, 45 - k), (48 + k, 45 - k)):
            kk = 45 - y
            if kk < 13 and inner_hw[kk] > abs(x + .5 - 48): P(a, x, y, BR0)
    for (x, y) in [(24, 37), (70, 37), (24, 42), (70, 42)]: R(a, x, y, x + 2, y + 2, BR1)    # 모서리 장식 못
    # 문틀(문 바깥 테) + 경첩 자리
    R(a, 20, 46, 76, 47, WD5); R(a, 20, 46, 21, 104, WD4); R(a, 75, 46, 76, 104, WD1)
    return outline(a)

OPEN = (XX >= 21) & (XX < 75) & (YY >= 47) & (YY < 104)
FAN = (YY < 46) & (YY >= 33)   # 채광창 유리는 GL0 색으로 가린다

# ---------------------------------------------------------------- 안쪽(열린 뒤): 서가 복도·샹들리에·사다리·떠오르는 종잇장
def inside(t):
    a = canvas(); a[..., 3] = 255; a[..., :3] = INK
    for side in (-1, 1):                                           # 원근으로 모이는 양옆 서가
        for k in range(4):
            x = CX + side * (24 - k * 6); w = 4 - k * .8
            sel = (np.abs(XX - x) < w) & (YY > 48 + k * 3) & (YY < 104 - k * 3)
            a[sel, :3] = [D3, D2, D1, D0][k]
            for r in range(7):
                ry = 54 + k * 3 + r * (7 - k)
                a[sel & (YY == ry), :3] = [WD2, WD1, D2, D1][k]
                if k < 2: a[sel & (YY < ry) & (YY > ry - 5 + k) & ((XX + r) % 2 == 0), :3] = BOOKS[(r + k) % len(BOOKS)]
    lad = (XX == 62 + (YY - 60) // 8) | (XX == 66 + (YY - 60) // 8)   # 사다리(1:8)
    a[lad & (YY > 58) & (YY < 102), :3] = WD3
    a[(YY > 58) & (YY < 102) & (YY % 6 == 0) & (XX > 62 + (YY - 60) // 8) & (XX < 66 + (YY - 60) // 8), :3] = WD3
    d = np.hypot(XX - CX, (YY - 92) / 1.3); g = np.clip(1 - d / 26, 0, 1) * (.3 + .7 * t)  # 끝에서 새는 빛
    q = dither(g, 4); a[q >= 1, :3] = LT0; a[q >= 2, :3] = LT1; a[q >= 3, :3] = LT2
    a[(YY >= 100) & (YY < 104), :3] = D1; a[YY == 100, :3] = D3
    for k in range(5):                                             # 바닥 줄눈(원근)
        x = 48 + (k - 2) * 6; a[(YY > 100) & (XX == x + (YY - 100) * (k - 2)), :3] = D2   # 정수 기울기(0·±1·±2)
    R(a, int(CX) - 4, 52, int(CX) + 4, 53, BR1); R(a, int(CX), 47, int(CX) + 1, 52, BR0)   # 샹들리에
    for dx in (-4, -1, 2, 4): P(a, int(CX) + dx, 51, LT3 if t > .2 else LT1)
    if t > .15:
        for i, (px, py) in enumerate([(38, 76), (55, 68), (47, 60), (34, 88), (60, 84), (44, 70)]):
            y = int(py - t * 8 - i % 2 * 3); R(a, px, y, px + 2, y + 1, PA2 if i % 2 else PA1)
    a[~OPEN] = 0
    return a

# ---------------------------------------------------------------- 문짝
def leaf(w, side, shade):
    """너비 w(최대 27) 문짝 한 짝을 그린 배열. side -1 왼짝, 1 오른짝."""
    a = canvas()
    if w < 1: return a
    x0 = 21 if side < 0 else 75 - w; x1 = x0 + w
    body = R(a, x0, 47, x1, 104, WD3)
    a[body & ((XX - x0) % 5 == 0)] = (*WD2, 255)                    # 판자 줄
    hinge = x0 if side < 0 else x1 - 1; meet = x1 - 1 if side < 0 else x0
    a[body & (XX == meet)] = (*WD1, 255); a[body & (XX == hinge)] = (*WD4, 255)
    f = w / 27
    def sx(u): return int(round(x0 + u * f)) if side < 0 else int(round(x1 - u * f))   # 경첩 쪽에서 잰 자리
    lo, hi = sorted((sx(3), sx(24)))
    if hi - lo >= 4:
        # 위 칸: 서가 새김(두 단)
        R(a, lo, 51, hi, 75, WD1); R(a, lo, 51, hi, 52, WD0); R(a, lo, 74, hi, 75, WD5)
        books(a, lo + 1, hi - 1, 53, 62, 11 + side, lean=w > 18, scroll=w > 18); books(a, lo + 1, hi - 1, 64, 73, 17 + side, lean=w > 18, scroll=w > 18)
        # 아래 칸: 돋을 판(빛 받는 윗변·왼변, 그늘 아랫변·오른변)
        R(a, lo, 79, hi, 99, WD4); R(a, lo + 1, 80, hi - 1, 98, WD3)
        R(a, lo, 79, hi, 80, WD5); R(a, lo, 79, lo + 1, 99, WD5); R(a, lo, 98, hi, 99, WD1); R(a, hi - 1, 79, hi, 99, WD1)
        R(a, lo + 3, 83, hi - 3, 95, WD2); R(a, lo + 3, 83, hi - 3, 84, WD1); R(a, lo + 3, 94, hi - 3, 95, WD4)   # 한 단 들어간 안쪽 판
        mx = (lo + hi) // 2
        if hi - lo >= 10:                                           # 가운데 작은 펼친 책 새김
            R(a, mx - 3, 88, mx, 90, WD4); R(a, mx, 88, mx + 3, 90, WD4); P(a, mx - 3, 87, WD4); P(a, mx + 2, 87, WD4); P(a, mx, 90, WD1); P(a, mx - 1, 90, WD1)
    # 놋쇠 띠 경첩 둘 + 리벳
    for hy in (48, 100):                                           # 판 바깥(위·아래)에 둔다
        a0, a1 = sorted((sx(0), sx(min(12, 27))))
        R(a, a0, hy, a1 + 1, hy + 2, BR1); R(a, a0, hy, a1 + 1, hy + 1, BR2)
        for u in (2, 6, 10):
            if u * f < w: P(a, sx(u), hy + 1, BR3)
    # 문고리(고리) + 열쇠판(오른짝만)
    if w >= 6:
        hx = sx(23)
        for (dx, dy) in [(0, -2), (-1, -1), (1, -1), (-2, 0), (2, 0), (-1, 1), (1, 1), (0, 2), (-2, 1), (2, 1)]: P(a, hx + dx, 77 + dy, BR2)
        P(a, hx, 75, BR0); P(a, hx, 76, BR1)
        if side > 0: R(a, hx - 1, 82, hx + 2, 87, BR1); P(a, hx, 84, WD0); P(a, hx, 85, WD0)
    a[body, :3] = (a[body, :3] * shade).astype(np.uint8)
    return a

def door(f):
    t = f / (N - 1)
    img = canvas(); over(img, inside(t))
    w = int(round(27 * math.cos(t * math.pi / 2 * .95)))
    for side in (-1, 1):
        lf = leaf(w, side, 1 - .4 * t)
        if (lf[..., 3] > 0).any(): over(img, outline(lf, INK) & np.array(255, np.uint8) if False else outline(lf, INK))
    fr = frame()
    if t > 0:                                                      # 채광창이 안에서 밝아진다
        q = dither(np.clip(t * .9 - np.hypot(XX - CX, YY - 46) / 40, 0, 1), 3)
        glass = FAN & (fr[..., :3] == GL0).all(-1)
        fr[glass & (q >= 1)] = (*GL1, 255); fr[glass & (q >= 2)] = (*LT1, 255)
    over(img, fr)
    if t > 0:                                                      # 계단에 쏟아지는 빛
        hs = 6 + (YY - 104) * 2 + int(20 * t); spill = (YY >= 104) & (XX >= 48 - hs) & (XX < 48 + hs)   # 가장자리 1:2
        g = np.clip(1 - np.abs(XX - CX) / (14 + 26 * t), 0, 1) * t * .9
        q = dither(g, 3)
        top = (img[..., :3] == S6).all(-1) | (img[..., :3] == S5).all(-1) | (img[..., :3] == S4).all(-1) | (img[..., :3] == S3).all(-1)
        img[spill & top & (q >= 1)] = (*LT1, 255); img[spill & top & (q >= 2)] = (*LT2, 255)
    return img

def up(a): return Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1))

def check():
    """재기 검사: 박공 비탈은 줄마다 3칸, 반원 셋은 마디가 단조."""
    frame()
    ped = CHECKS['pediment']; d = [ped[i] - ped[i + 1] for i in range(len(ped) - 1)]
    print('박공 비탈 마디:', d, '같은 마디' if len(set(d)) == 1 else '재기!')
    for k in ('fanlight', 'fanlight-inner', 'fanlight-lead'):
        ok, d = monotone_arc(CHECKS[k]); print(k, '반폭', CHECKS[k], '마디', d, '단조' if ok else '재기!')
    return len(set(d)) == 1

if __name__ == '__main__':
    OUT.mkdir(exist_ok=True)
    check()
    frames = [up(door(i)) for i in range(N)]
    for i, im in enumerate(frames): im.save(OUT / f'A2-{i:02d}.png')
    g = []
    for im in frames + [frames[-1]] * 4 + [frames[0]] * 4:
        b = Image.new('RGBA', (576, 672), (40, 48, 52, 255)); b.alpha_composite(im.resize((576, 672), Image.NEAREST)); g.append(b.convert('RGB'))
    g[0].save(OUT / 'A2-open-3x.gif', save_all=True, append_images=g[1:], duration=[90] * N + [200] * 8, loop=0)
    print('A2 ok')
