# 가시 그물 v5(2026-11-13). 사용자가 그림으로 짚어 줌: 「그물은 채찍 위 낮은 한 점(첫 적 근처)에서 부채처럼 펴져
# 높이 선 뒤, 바깥 테두리가 위로 넘어 앞으로(적 너머로) 크게 뻗으며 아래로 덮여야 한다. 머리 위 꼭짓점(엑스 친 곳)은 아니다」.
# 그래서 그물을 부채꼴로 새로 그린다: 뿌리(PIVOT)에서 뻗는 살 + 둥근 그물줄 + 가시 돋은 테두리.
#   ① 0.315~0.40초: 손에서 채찍이 뻗고 뿌리에서 부채가 펴지며 선다(윗변 20°→54°, 반지름 1.5→R0). 앞으로 넘어가는 힘을 가진 채.
#   ② 0.40초~: 윗변이 중력을 받는 문짝처럼 넘어온다(각가속도 ∝ cos θ, 위에선 느리고 내려올수록 빨라짐).
#      넘어오는 동안 반지름이 R0→R1 로 커져 적 너머로 뻗는다. 아랫변(채찍)은 땅에 붙어 있다.
#   ③ 다 넘어오면 살짝 튀었다 가라앉고, 그물줄은 적 몸 위에 걸쳐 처진다. 1.15~1.4초에 화소가 빠지며 사라진다.
import math
import numpy as np
from PIL import Image, ImageDraw
HAND = np.array([(281.8 - 186.1) / 88.22, (307 - 249.1) / 88.22])
PIVOT = np.array([2.2, .12])                       # 부채 뿌리: 채찍 위, 첫 적 근처 땅 가까이
R0, R1 = 4.7, 8.2                                  # 선 때 반지름 → 다 넘어왔을 때 반지름(적 너머 10.4u 까지)
LOW = math.radians(2)                              # 아랫변(채찍) 각도
SPOKES, RINGS = 8, (.22, .40, .56, .70, .82, .92, 1.0)
G_ANG, START, OPEN_END, DISSOLVE_FROM, DISSOLVE_TO = 30.0, .315, .40, 1.15, 1.4
TOP = math.radians(54)                             # 선 때 윗변 각도(늦게 따라오는 테두리까지 판정 높이 5u 안: 최고 4.87u)
MONSTERS = [(np.array([x, .72]), np.array([.78, .78])) for x in (3.0, 6.2, 9.0)]
INK = (27, 22, 22, 255)
HAND_UNTIL = .8                                    # whip_range motion_end
LINE = 3                                           # 몸 그림 화소 밀도(88)에서 3화소 = 화면에서 원래 그물 선과 비슷한 굵기

def ease_out(u): u = min(1, max(0, u)); return 1 - (1 - u) ** 2

def swing():
    """윗변 각도 θ(t)와 반지름 R(t)를 1/240초로 적분. 반환 [(t, θ, R)]."""
    out = []; th = TOP; w = -2.2; t = OPEN_END; dt = 1 / 240; bounced = False
    while t < DISSOLVE_TO + .02:
        out.append((t, th, R0 + (R1 - R0) * ease_out((t - OPEN_END) / .32)))
        w += -G_ANG * math.cos(th) * dt; th += w * dt
        floor = LOW + math.radians(1.5)               # 적에 걸리지 않고 통과해 땅까지 내려온다(사용자: 몬스터에 안 쌓여도 된다)
        if th < floor:
            th = floor; w = -w * (.32 if not bounced else 0); bounced = True   # 땅에 닿으면 한 번 살짝 튀고 가라앉는다
        t += dt
    return out
SWING = swing()
LANDED = next((t for t, th, R in SWING if th <= LOW + math.radians(1.5) + 1e-6), DISSOLVE_TO)

def state(t):
    if t < START: return None
    if t < OPEN_END:
        u = (t - START) / (OPEN_END - START); e = ease_out(u)
        return math.radians(20) + (TOP - math.radians(20)) * e, 1.5 + (R0 - 1.5) * e
    i = min(len(SWING) - 1, int((t - OPEN_END) * 240)); _, th, R = SWING[i]; return th, R

def ground(p): return np.array([p[0], max(0.0, p[1])])

# 부드럽게(사용자: 그물이 딱딱해 보인다):
#   - 늦게 따라옴: 살의 바깥쪽 점은 뿌리보다 DELAY·f 초 전의 각도를 쓴다 → 넘어올 때 살이 뒤로 휘고, 멈출 때 끝이 한 번 더 넘어간다.
#   - 물결: 살과 직각으로 바깥으로 달려가는 물결(그물이 펄럭임). 땅에 닿은 뒤엔 잦아든다.
DELAY, WAVE, WAVE_LEN, WAVE_SPEED, SAG, SPOKE_LAG = .055, .18, .9, 2.4, .16, .02
# 부드러움 강도(사용자가 비교해 고른다): 늦게 따라옴·물결·그물줄 처짐·윗살이 아랫살보다 늦게 오는 정도를 함께 키운다.
LEVELS = {1: (.035, .10, .10, 0.0), 2: (.055, .18, .16, .02), 3: (.09, .30, .24, .05), 4: (.13, .42, .32, .08)}
def configure(level):
    global DELAY, WAVE, SAG, SPOKE_LAG
    DELAY, WAVE, SAG, SPOKE_LAG = LEVELS[level]
_rng = np.random.default_rng(5); JIT = _rng.normal(0, .045, (SPOKES + 1, 64, 2))
def point(k, f, t):
    s0 = state(t); th_now, R = s0
    td = t - DELAY * f - SPOKE_LAG * k / SPOKES
    s1 = state(max(START, td)); th = s1[0]
    a = LOW + (th - LOW) * k / SPOKES
    d = np.array([math.cos(a), math.sin(a)]); n = np.array([-d[1], d[0]])
    calm = 1.0 if t < LANDED else math.exp(-(t - LANDED) / .12)
    wave = WAVE * calm * f ** 1.5 * math.sin(2 * math.pi * (f / WAVE_LEN - WAVE_SPEED * (t - START)) + .7 * k)
    j = min(63, int(f * 63))
    return ground(PIVOT + R * f * d + n * wave + JIT[k, j] * f)

def fan_points(t):
    if state(t) is None: return None
    pts = np.zeros((SPOKES + 1, len(RINGS) + 1, 2))
    for k in range(SPOKES + 1):
        for j, f in enumerate((0,) + RINGS): pts[k, j] = point(k, f, t)
    return pts

def layer(t, X0, Y1, W, H, NP):
    """월드 범위(X0 왼쪽, Y1 위쪽)를 NP 화소/u 로 그린 장. 채찍(손→뿌리→아랫변) + 부채 그물."""
    if t < START or t >= DISSOLVE_TO: return None
    pts = fan_points(t)
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    px = lambda p: ((p[0] - X0) * NP, (Y1 - p[1]) * NP)
    def curve(ps, width):
        d.line([px(p) for p in ps], fill=INK, width=width, joint='curve')
    # 그물줄(둥근 줄): 살 사이를 처지게 이어 그린다
    for j in range(1, len(RINGS) + 1):
        row = []
        for k in range(SPOKES):
            a, b = pts[k, j], pts[k + 1, j]
            for s in np.linspace(0, 1, 6, endpoint=False):
                m = a + (b - a) * s; sag = SAG * RINGS[j - 1] * math.sin(math.pi * s)
                row.append(ground(m + np.array([0, -sag])))
        row.append(pts[SPOKES, j]); curve(row, LINE)
    for k in range(1, SPOKES + 1):                  # 살: 휘어 보이게 잘게 나눠 그린다
        curve([point(k, f, t) for f in np.linspace(0, 1, 25)], LINE)
    # 테두리 가시
    rim = [pts[k, -1] for k in range(SPOKES + 1)]
    for k in range(SPOKES):
        a, b = rim[k], rim[k + 1]
        for s in (.33, .66):
            m = a + (b - a) * s; tg = (b - a) / (np.linalg.norm(b - a) + 1e-6); nrm = np.array([tg[1], -tg[0]])
            if nrm[1] < 0: nrm = -nrm
            p0 = px(m); p1 = px(m + nrm * .12 + tg * .05); d.line((p0, p1), fill=INK, width=LINE)
    # 채찍: 손 → 뿌리 → 아랫변 끝(땅)
    # 손 → 뿌리 구간은 몸 모션이 끝나는 HAND_UNTIL(0.8초)까지만: 그 뒤엔 캐릭터가 움직일 수 있어 손에서 떨어진다(그물·땅의 채찍만 남는다)
    whip = ([HAND, HAND + (PIVOT - HAND) * .5 + np.array([0, -.08])] if t < HAND_UNTIL else []) + [PIVOT] + [point(0, f, t) for f in np.linspace(.05, 1, 20)]
    curve(whip, 5)
    if t > DISSOLVE_FROM:
        a = np.asarray(im).copy(); q = (t - DISSOLVE_FROM) / (DISSOLVE_TO - DISSOLVE_FROM)
        a[np.random.default_rng(11).random(a.shape[:2]) < q] = 0; im = Image.fromarray(a)
    return im

if __name__ == '__main__':
    for t in (.32, .36, .40, .48, .56, .64, .72, .80, .9, 1.0):
        th, R = state(t); print(t, 'θ', round(math.degrees(th), 1), 'R', round(R, 2), 'tip x', round(PIVOT[0] + R * math.cos(th), 2))
