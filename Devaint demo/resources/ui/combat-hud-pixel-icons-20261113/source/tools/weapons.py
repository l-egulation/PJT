# 무기 아이콘 3차: 48칸(32칸 설계 × 1.5). 길쭉한 비율 되살리기 + 디테일. 발사기 네 종은 모양부터 다르다.
# 손잡이는 왼쪽 아래, 끝은 오른쪽 위(45° 축). 재기 규칙: 변은 축에 평행·수직(화면 45°), 축에 1:2(화면 1:3), 축에 1:1(화면 가로·세로)만.
import math
from pixicon import *

N, SC = 48, 1.5
PALE = (214, 204, 186, 255)    # 크림과 그늘 사이
def D(t, o=0.0): return diag(t, o, 32)
def new(): return Icon(N, SC)
def band(ic, t0, t1, o0, o1, c):
    """축 좌표 직사각형(t0~t1, o0~o1)."""
    ic.poly([D(t0, o0), D(t1, o0), D(t1, o1), D(t0, o1)], c)
def blade(ic, t0, t1, half, c, k=2):
    """곧은 날 + 끝(끝 길이 = k·반폭). k=2 → 화면 1:3 변, k=1 → 화면 가로·세로 변."""
    tip = t1 + k * half
    ic.poly([D(t0, -half), D(t1, -half), D(tip, 0), D(t1, half), D(t0, half)], c)
    return tip
def grip(ic, t0, t1, w, wraps=()):
    ic.line(D(t0), D(t1), w, SHADE)
    for t in wraps: ic.line(D(t, -w / 2), D(t, w / 2), 1, DARK)

def sword():
    ic = new()
    tip = blade(ic, 10, 27, 2.6, CREAM)
    ic.poly([D(10, 0), D(27, 0), D(tip, 0), D(27, 2.6), D(10, 2.6)], SHADE)        # 아래 날 그늘
    ic.line(D(12), D(26), 1, LIGHT)                                                 # 날 가운데 빛
    ic.line(D(9, -6.5), D(9, 6.5), 2.6, CREAM); ic.line(D(9, 2), D(9, 6.5), 2.6, SHADE)
    ic.disc(D(9), 1.4, GOLD)                                                       # 날밑 보석
    grip(ic, 3, 8, 2.8, (4.5, 6.5)); ic.disc(D(2), 2, CREAM); ic.disc(D(2.3, .6), 1, SHADE)
    return ic

def rapier():
    ic = new()
    ic.line(D(10), D(34), 1.8, CREAM); ic.line(D(10, .5), D(34, .5), .9, SHADE)    # 가늘고 긴 날
    ic.line(D(9, -5.5), D(9, 5.5), 2, CREAM); ic.line(D(9, 1.5), D(9, 5.5), 2, SHADE)   # 가로 막대
    ic.disc(D(10.5), 3, CREAM); ic.disc(D(11, .8), 1.8, SHADE)                     # 둥근 손 보호 컵
    grip(ic, 3.5, 8.5, 2.4, (5, 7)); ic.disc(D(2.6), 1.8, GOLD)
    return ic

def whip():
    """사용자 스케치: 굵은 손잡이 → 줄이 위로 솟아 좁게 접혀 내려오고 → 오른쪽 위로 길게 뻗다가 → 작게 꺾여 꼬리."""
    ic = new()
    grip(ic, 1.5, 9.5, 2.6, (3.5, 5.5, 7.5)); ic.disc(D(1.2), 2, CREAM); ic.line(D(9), D(10.6), 3.4, CREAM)
    # 줄(칸 좌표). 구간마다 기울기가 한쪽으로만 돌아간다.
    up = [(1, -2, 2), (1, -1, 9), (2, -1, 2), (3, -1, 1), (1, 0, 1),            # 솟는 줄(시계 방향으로 눕는다)
          (1, 1, 1), (0, 1, 2), (-1, 2, 2), (-1, 1, 6), (-1, 2, 1), (0, 1, 1)]  # 꼭대기에서 접혀 왼쪽 아래로
    path = ic.stroke((16, 28), up, CREAM, 2)
    back = [(1, 1, 1), (1, 0, 1), (2, -1, 5)]                                    # 바닥에서 돌아 오른쪽 위로 길게
    path = ic.stroke(path[-1], back, CREAM, 2)
    tail = [(0, 1, 3), (1, 0, 1), (2, -1, 1), (1, -1, 2)]                       # 작게 꺾인 꼬리(가늘게)
    path = ic.stroke(path[-1], tail, CREAM, 1)
    return ic

def dagger():
    ic = new()
    tip = blade(ic, 11, 20, 3.4, CREAM)
    ic.poly([D(11, 0), D(20, 0), D(tip, 0), D(20, 3.4), D(11, 3.4)], SHADE)
    ic.line(D(12.5), D(22), 1, LIGHT)
    ic.line(D(10, -5), D(10, 5), 2.6, CREAM); ic.line(D(10, 1.5), D(10, 5), 2.6, SHADE)
    grip(ic, 5, 9.5, 3, (6.5, 8)); ic.disc(D(4), 2.2, CREAM); ic.disc(D(4), 1, GOLD)
    return ic

def hammer():
    ic = new()
    grip(ic, 2, 21, 2.8, (4, 6, 8)); ic.disc(D(1.4), 2, CREAM)
    band(ic, 20, 29, -8, 8, CREAM); band(ic, 20, 29, 2.5, 8, SHADE)                 # 머리
    band(ic, 20, 29, -8, -6.6, LIGHT)                                               # 빛 받는 면
    band(ic, 23.6, 25.4, -8.6, 8.6, DARK)                                          # 쇠띠
    band(ic, 18.5, 20, -3, 3, SHADE)                                                # 목
    return ic

def greatsword():
    ic = new()
    tip = blade(ic, 8, 29, 4, CREAM, k=1)
    ic.poly([D(8, 0), D(29, 0), D(tip, 0), D(29, 4), D(8, 4)], SHADE)
    ic.line(D(10), D(27), 1.2, DARK)                                                # 홈
    ic.line(D(10, -1.5), D(27, -1.5), .9, LIGHT)
    ic.line(D(7, -9), D(7, 9), 3, CREAM); ic.line(D(7, 3), D(7, 9), 3, SHADE)
    ic.disc(D(7), 1.6, GOLD)
    grip(ic, 1.5, 6, 3, (3, 4.6)); ic.disc(D(.8), 2.2, CREAM)
    return ic

def shield():
    """기사 방패: 밝은 쇠 테(왼쪽 위는 빛, 오른쪽 아래는 그늘) + 어두운 면 + 붉은 십자.
    아래 곡선은 3:1→1:2→1:1→2:1 로 차례로 눕는다(재기 규칙). 안쪽 면은 같은 기울기로 평행 이동."""
    ic = new()
    outer = [(5, 4), (27, 4), (27, 15), (26, 18), (24, 22), (20, 26), (16, 28), (12, 26), (8, 22), (6, 18), (5, 15)]
    ic.poly(outer, SHADE)                                                          # 테(그늘 쪽)
    ic.poly([(5, 4), (16, 4), (16, 28), (12, 26), (8, 22), (6, 18), (5, 15)], CREAM)   # 테(빛 쪽 왼쪽 반)
    ic.poly([(5, 4), (27, 4), (27, 5.2), (5, 5.2)], LIGHT)                          # 윗변 빛
    inner = offset_poly(outer, 2.6)
    ic.poly(inner, DARK)                                                           # 면
    ic.poly(offset_poly(outer, 2.6)[:1] + [(inner[1][0], inner[1][1]), (inner[1][0], inner[1][1] + 1.2), (inner[0][0], inner[0][1] + 1.2)], (70, 64, 56, 255))
    # 붉은 십자(오른쪽·아래 반은 짙은 빨강)
    ic.poly([(14.6, 8), (17.4, 8), (17.4, 23), (14.6, 23)], RED); ic.poly([(16.2, 8), (17.4, 8), (17.4, 23), (16.2, 23)], RED_D)
    ic.poly([(9, 12.6), (23, 12.6), (23, 15.4), (9, 15.4)], RED); ic.poly([(9, 14.4), (23, 14.4), (23, 15.4), (9, 15.4)], RED_D)
    for p_ in [(7, 6), (25, 6), (6.6, 15), (25.4, 15)]: ic.disc(p_, .7, DARK)      # 못
    return ic

# ---------------- 발사기 네 종: 같은 자세, 몸통 모양이 다르다
def launcher_ball():
    """기본: 어깨에 메는 굵은 대포(포구가 넓게 벌어진 통) + 모은 금빛 탄."""
    ic = new()
    band(ic, 3, 24, -4.6, 4.6, CREAM); band(ic, 3, 24, 1.8, 4.6, SHADE); band(ic, 3, 24, -4.6, -3.4, LIGHT)
    band(ic, 22, 28, -6.4, 6.4, CREAM); band(ic, 22, 28, 2.6, 6.4, SHADE)              # 벌어진 포구
    band(ic, 27, 28, -4.6, 4.6, DARK)                                                   # 구멍
    for t in (8, 16): band(ic, t, t + 1.2, -4.8, 4.8, DARK)                            # 띠
    band(ic, 1.5, 3.2, -3.4, 3.4, SHADE)                                                # 뒤 마개
    band(ic, 12, 15, -7.2, -4.6, CREAM)                                                 # 조준기
    band(ic, 9, 12, 4.6, 10, SHADE); band(ic, 17, 19.5, 4.6, 8, SHADE)                  # 손잡이 둘
    ic.disc(D(33), 3.4, GOLD); ic.disc(D(32.6, -1.2), 1.2, GOLD_HI)
    return ic

def launcher_laser():
    """레이저: 몸통 + 가늘고 긴 총열에 청록 고리, 끝에 렌즈."""
    ic = new()
    band(ic, 2, 15, -4, 3.2, CREAM); band(ic, 2, 15, 1, 3.2, SHADE); band(ic, 2, 15, -4, -3, LIGHT)
    band(ic, 5, 11, -2.4, -.4, TEAL)                                                    # 에너지 칸
    band(ic, 15, 31, -1.6, 1.6, CREAM); band(ic, 15, 31, .4, 1.6, SHADE)               # 긴 총열
    for t in (18, 22, 26): band(ic, t, t + 1.4, -2.8, 2.8, TEAL)                        # 코일
    band(ic, 31, 33.5, -2.6, 2.6, CREAM); band(ic, 33, 33.5, -1.6, 1.6, TEAL)           # 렌즈
    band(ic, 4, 7.5, 3.2, 10, SHADE); band(ic, 6, 9, 3.2, 4.6, DARK)                    # 손잡이
    return ic

def launcher_shotgun():
    """샷건: 짧고 굵은 두 총열(구멍 둘) + 개머리."""
    ic = new()
    band(ic, 12, 30, -4.4, -.2, CREAM); band(ic, 12, 30, .2, 4.4, SHADE)               # 총열 둘
    band(ic, 12, 30, -4.4, -3.4, LIGHT); band(ic, 12, 29, -.5, .5, DARK)                # 두 총열 사이 틈
    band(ic, 29, 30.5, -4.4, 4.4, CREAM); ic.disc(D(30, -2.3), 1.2, DARK); ic.disc(D(30, 2.3), 1.2, DARK)
    band(ic, 22, 23.2, -4.6, 4.6, DARK)                                                 # 띠
    band(ic, 6, 12.5, -4, 4.4, CREAM); band(ic, 6, 12.5, 1.8, 4.4, SHADE)              # 몸통
    band(ic, 0, 6, -3, 6, SHADE)                                                        # 개머리
    band(ic, 0, 6, 1, 2, DARK)
    ic.ring(D(10, 6), 2.4, 1.2, SHADE, 0, 180)                                          # 방아쇠 울
    for o in (-6, 0, 6): ic.disc(D(34.2, o), 1.4, GOLD)                                 # 퍼지는 알
    return ic

def launcher_grenade():
    """유탄: 굵고 짧은 총열 + 둥근 탄창(회전 드럼) + 날아가는 유탄."""
    ic = new()
    band(ic, 15, 28, -4.2, 4.2, CREAM); band(ic, 15, 28, 1.6, 4.2, SHADE); band(ic, 15, 28, -4.2, -3.2, LIGHT)
    band(ic, 27, 28.5, -4.6, 4.6, CREAM); band(ic, 27.6, 28.5, -2.8, 2.8, DARK)
    band(ic, 2, 9, -3, 3, SHADE)                                                        # 개머리
    ic.disc(D(12, 1.5), 6.4, CREAM); ic.ring(D(12, 1.5), 6.4, 5.2, SHADE, 0, 150)       # 드럼
    for k in range(6):
        a = math.radians(k * 60 + 30); p = D(12, 1.5); ic.disc((p[0] + 3.6 * math.cos(a), p[1] + 3.6 * math.sin(a)), 1, DARK)
    ic.disc(D(12, 1.5), .9, GOLD)
    band(ic, 17, 19.5, 4.2, 9, SHADE)                                                   # 앞 손잡이
    ic.disc(D(33.5), 3.2, GOLD); band(ic, 32, 33, -3.2, 3.2, DARK)                      # 유탄(띠)
    return ic

def shuriken():
    ic = new(); c = (16, 16); T = 14.5; I = T * math.sqrt(2) / 3
    for a in range(4):
        ang = math.radians(a * 90 - 90)
        tip = (c[0] + T * math.cos(ang), c[1] + T * math.sin(ang))
        l = (c[0] + I * math.cos(ang - math.pi / 4), c[1] + I * math.sin(ang - math.pi / 4))
        r = (c[0] + I * math.cos(ang + math.pi / 4), c[1] + I * math.sin(ang + math.pi / 4))
        ic.poly([c, l, tip, r], CREAM); ic.poly([c, tip, r], SHADE)
    ic.disc(c, 4, SHADE); ic.disc(c, 3, DARK); ic.disc((15, 15), .8, SHADE)
    return ic

def bomb():
    ic = new(); c = (14, 19)
    ic.disc(c, 11, CREAM); ic.ring(c, 11, 8, SHADE, 0, 120)
    ic.disc((10, 15), 2.6, LIGHT); ic.disc((8.5, 14), 1, (255, 255, 255, 255))
    ic.poly([(19, 7), (24, 7), (24, 11), (19, 11)], SHADE); ic.line((19, 9), (24, 9), 1, DARK)   # 뚜껑
    ic.line((22, 7.5), (25, 4.5), 1.8, CREAM); ic.disc((25.8, 3.8), 2.2, GOLD); ic.disc((25.8, 3.8), 1, GOLD_HI)
    for p in [(28.5, 3.5), (26, 1), (28, 6)]: ic.disc(p, .55, GOLD_HI)                # 불똥
    return ic

def unarmed():
    """맨손: 앞에서 본 주먹. 손가락 넷 + 앞을 가로지르는 엄지 + 손목 띠."""
    ic = new()
    ic.poly([(11, 22), (21, 22), (21, 28), (11, 28)], SHADE); ic.line((11, 24.5), (21, 24.5), 1.2, DARK)     # 손목·소매
    ic.poly([(8, 12), (24, 12), (24, 22), (22, 24), (10, 24), (8, 22)], CREAM)                              # 손등
    for k in range(4):                                                                                    # 손가락 넷(둥근 마디)
        x = 8 + k * 4
        ic.poly([(x, 9), (x + 4, 9), (x + 4, 15), (x, 15)], CREAM); ic.disc((x + 2, 9.2), 2, CREAM)
        ic.disc((x + 2, 8.6), .8, LIGHT)
        if k: ic.line((x, 8.5), (x, 15), 1, SHADE)
    ic.line((8, 15.5), (24, 15.5), 1, SHADE)                                                              # 손가락 접힌 선
    ic.poly([(8, 16), (18, 16), (20, 18), (18, 20), (8, 20)], CREAM)                                      # 엄지
    ic.line((8, 16), (18, 16), 1, SHADE); ic.line((8, 20.5), (18, 20.5), 1, SHADE)
    ic.poly([(18, 16), (20, 18), (18, 20)], SHADE)                                                        # 엄지 끝 그늘
    ic.poly([(20, 12), (24, 12), (24, 22), (22, 24), (20, 24)], PALE)                                     # 오른쪽 옆면 그늘
    for k in range(4): x = 8 + k * 4; ic.poly([(x + 3, 9), (x + 4, 9), (x + 4, 15), (x + 3, 15)], PALE) if k == 3 else None
    return ic

shield.nudge = (0, 2)   # 위가 넓고 아래가 뾰족해 상자 가운데에 두면 떠 보인다 → 2칸 내린다

def launcher(variant="ball"):
    return {"ball": launcher_ball, "laser": launcher_laser, "shotgun": launcher_shotgun, "grenade": launcher_grenade}[variant]()

WEAPONS = {
    "weapon-sword": sword, "weapon-rapier": rapier, "weapon-whip": whip, "weapon-dagger": dagger, "weapon-hammer": hammer,
    "weapon-greatsword": greatsword, "weapon-shield": shield, "weapon-launcher": launcher_ball,
    "weapon-launcher-laser": launcher_laser, "weapon-launcher-shotgun": launcher_shotgun,
    "weapon-launcher-grenade": launcher_grenade, "weapon-shuriken": shuriken, "weapon-bomb": bomb,
    "weapon-unarmed": unarmed,
}
NAMES = {"weapon-sword": "검", "weapon-rapier": "레이피어", "weapon-whip": "채찍", "weapon-dagger": "단검", "weapon-hammer": "망치",
         "weapon-greatsword": "대검", "weapon-shield": "방패", "weapon-launcher": "발사기", "weapon-launcher-laser": "발사기·레이저",
         "weapon-launcher-shotgun": "발사기·샷건", "weapon-launcher-grenade": "발사기·유탄", "weapon-shuriken": "수리검", "weapon-bomb": "폭탄", "weapon-unarmed": "맨손"}
