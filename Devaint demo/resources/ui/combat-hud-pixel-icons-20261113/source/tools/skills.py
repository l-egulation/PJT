# 스킬 아이콘 2차: 크게·단순하게. 작은 원 안쪽(반지름 24)에 맞춰 44칸, 32칸 설계를 1.375배로.
# 그림 하나 + 강조 하나. 색 약속: 크림 = 무기·몸, 금색 = 힘(베기·폭발), 청록 = 이동, 회색 동그라미 = 적.
# 재기 규칙: 선은 0·1:3·1:2·1:1·2:1·3:1·90° 만, 원은 다듬은 원(pixicon.circle_rows).
import math
from pixicon import *

def new(): return Icon(44, 44 / 32)

def foe(ic, c, r=4.5):
    ic.disc(c, r, SHADE); x, y = ic.S(c); x, y = int(round(x / ic.k)), int(round(y / ic.k))
    e = max(1, int(ic.R(r) / ic.k / 4)); s = 1 if e < 2 else 2
    ic.rect(x - e - s, y - e, s, 2, INK); ic.rect(x + e, y - e, s, 2, INK)
def head(ic, b, ang_deg, h, col):
    """b 에 뾰족한 화살촉. 변은 진행 방향에 45°."""
    a = math.radians(round(ang_deg / 45) * 45); d = (math.cos(a), math.sin(a)); n = (-d[1], d[0])
    tip = (b[0] + d[0] * h, b[1] + d[1] * h)
    ic.poly([tip, (b[0] + n[0] * h, b[1] + n[1] * h), (b[0] - n[0] * h, b[1] - n[1] * h)], col)
def arrow(ic, a, b, w, col, h=3.5):
    ic.line(a, b, w, col); head(ic, b, math.degrees(math.atan2(b[1] - a[1], b[0] - a[0])), h, col)
def speed(ic, x1, ys, length, col=TEAL, w=2):
    for k, y in enumerate(ys): ic.line((x1 - length + (k % 2) * 3, y), (x1, y), w, col)
def moon(ic, c1, r1, c2, r2, col):
    """초승달: c1 원에서 c2 원을 뺀 자리."""
    (ax, ay), ra = ic.S(c1), ic.R(r1); (bx, by), rb = ic.S(c2), ic.R(r2)
    for y in range(int(ay - ra) - 1, int(ay + ra) + 2):
        for x in range(int(ax - ra) - 1, int(ax + ra) + 2):
            if (x + .5 - ax) ** 2 + (y + .5 - ay) ** 2 > ra * ra: continue
            if (x + .5 - bx) ** 2 + (y + .5 - by) ** 2 <= rb * rb: continue
            ic.put(x, y, col, True)
def star4(ic, c, T, col, core=None):
    """네 갈래 별(수리검·반짝임). 변 기울기 2:1."""
    I = T * math.sqrt(2) / 3
    for k in range(4):
        ang = math.radians(k * 90 - 90)
        tip = (c[0] + T * math.cos(ang), c[1] + T * math.sin(ang))
        l = (c[0] + I * math.cos(ang - math.pi / 4), c[1] + I * math.sin(ang - math.pi / 4))
        r = (c[0] + I * math.cos(ang + math.pi / 4), c[1] + I * math.sin(ang + math.pi / 4))
        ic.poly([c, l, tip, r], col)
    if core: ic.disc(c, T * .22, core)
def burst(ic, c, r, col, w=2):
    for k in range(8):
        a = math.radians(k * 45); L = r if k % 2 == 0 else r * .7
        ic.line((c[0] + math.cos(a) * r * .35, c[1] + math.sin(a) * r * .35), (c[0] + math.cos(a) * L, c[1] + math.sin(a) * L), w, col)
def hsword(ic, x0, x1, y, half=2, col=CREAM):
    """오른쪽을 향한 검(가로). 끝 변은 1:2."""
    tip = x1 + 2 * half
    ic.poly([(x0 + 4, y - half), (x1, y - half), (tip, y), (x1, y + half), (x0 + 4, y + half)], col)
    ic.line((x0 + 3.5, y - half - 3), (x0 + 3.5, y + half + 3), 2, col); ic.line((x0, y), (x0 + 3, y), 2.4, SHADE)
def vsword(ic, x, y0, y1, half=3, col=CREAM):
    """아래를 향한 검(세로). y0 손잡이 끝, y1 날 끝 시작."""
    tip = y1 + 2 * half
    ic.poly([(x - half, y0 + 6), (x + half, y0 + 6), (x + half, y1), (x, tip), (x - half, y1)], col)
    ic.poly([(x, y0 + 6), (x + half, y0 + 6), (x + half, y1), (x, tip)], SHADE)
    ic.line((x - half - 3.5, y0 + 5), (x + half + 3.5, y0 + 5), 2.4, col); ic.line((x, y0), (x, y0 + 4), 2.4, SHADE)
def shield_icon(ic, c, s, col=CREAM):
    """작은 기사 방패(무기 그림과 같은 모양). 위 평평, 아래는 2:1→1:1 로 모인다."""
    x, y = c; u = s / 6.5
    outer = [(x - 6.5 * u, y - 6.5 * u), (x + 6.5 * u, y - 6.5 * u), (x + 6.5 * u, y + .5 * u), (x + 4.5 * u, y + 4.5 * u), (x, y + 6.75 * u), (x - 4.5 * u, y + 4.5 * u), (x - 6.5 * u, y + .5 * u)]
    ic.poly(outer, SHADE); ic.poly([outer[0], (x, y - 6.5 * u), (x, y + 6.75 * u), outer[5], outer[6]], col)
    ic.poly(offset_poly(outer, 1.6 * u), DARK)
    ic.poly([(x - .9 * u, y - 4.2 * u), (x + .9 * u, y - 4.2 * u), (x + .9 * u, y + 3.8 * u), (x - .9 * u, y + 3.8 * u)], RED)
    ic.poly([(x - 3.8 * u, y - 1.9 * u), (x + 3.8 * u, y - 1.9 * u), (x + 3.8 * u, y - .1 * u), (x - 3.8 * u, y - .1 * u)], RED)

def bomb_at(ic, c, r, col=CREAM):
    ic.disc(c, r, col); ic.disc((c[0] - r * .35, c[1] - r * .35), max(.9, r * .22), LIGHT)
    f = (c[0] + r * .72, c[1] - r * .72); ic.line(f, (f[0] + 2.5, f[1] - 2.5), 1.6, SHADE); ic.disc((f[0] + 3, f[1] - 3), max(1.2, r * .2), GOLD)
def ground(ic, y, col=SHADE): ic.line((5, y), (27, y), 2, col)
def hammer_head(ic, x0, y0, w, h, face="v"):
    ic.poly([(x0, y0), (x0 + w, y0), (x0 + w, y0 + h), (x0, y0 + h)], CREAM)
    if face == "v": ic.poly([(x0 + w * .55, y0), (x0 + w, y0), (x0 + w, y0 + h), (x0 + w * .55, y0 + h)], SHADE)
    else: ic.poly([(x0, y0 + h * .55), (x0 + w, y0 + h * .55), (x0 + w, y0 + h), (x0, y0 + h)], SHADE)

# ---------------- 검
def sword_default():      # 앞쪽 넓은 범위를 한 번 크게 벤다
    ic = new(); moon(ic, (13, 16), 14, (7, 16), 13, GOLD); return ic
def sword_range():        # 꿰뚫는 검기
    ic = new(); moon(ic, (17, 16), 12, (12, 16), 11, GOLD); speed(ic, 12, [10, 16, 22], 8, SHADE); return ic
def sword_movement():     # 돌진하며 찌른다
    ic = new(); hsword(ic, 10, 24, 16, 2.4); speed(ic, 9, [9, 16, 23], 7); return ic
def sword_follow_up():    # 벤 자리에 잔상이 남아 한 번 더 벤다: 흐린 청록 잔상 베기 뒤에 금빛 베기
    ic = new(); ghost = new()
    moon(ghost, (11, 17), 12, (6, 17), 11.5, TEAL)
    for y in range(ic.n):
        for x in range(ic.n):
            if ghost.px[y][x] and (x // ic.k + y // ic.k) % 2 == 0: ic.put(x, y, TEAL)
    moon(ic, (17, 15), 12, (12, 15), 11.5, GOLD); return ic

# ---------------- 채찍
def whip_default():       # 휘감아 묶는다(보스도)
    ic = new(); foe(ic, (16, 16), 10)
    for y in (11, 20): ic.line((4, y), (28, y), 2.6, CREAM)
    return ic
def whip_range():         # 넓은 범위를 모두 묶는다
    ic = new()
    for x in (6, 16, 26): foe(ic, (x, 14), 4.5)
    ic.line((2, 19), (30, 19), 2.6, CREAM); return ic
def whip_movement():      # 매듭 → 다시 누르면 그 적에게 날아간다
    ic = new(); foe(ic, (22, 10), 5); ic.ring((22, 10), 8, 6, CREAM)
    arrow(ic, (5, 27), (13, 19), 2.6, TEAL, 3.5); return ic
def whip_follow_up():     # 매듭이 조여 추가 피해
    ic = new(); foe(ic, (16, 16), 6); ic.ring((16, 16), 9.5, 7.5, CREAM)
    arrow(ic, (1, 16), (3.5, 16), 2, GOLD, 2.5); arrow(ic, (31, 16), (28.5, 16), 2, GOLD, 2.5); return ic

# ---------------- 레이피어
def rapier_default():     # 모아서 세게 찌른다
    ic = new()
    for r in (5, 9): ic.ring((9, 16), r, r - 2, GOLD, 135, 225)
    ic.line((11, 16), (28, 16), 2, CREAM); head(ic, (28, 16), 0, 2, CREAM); ic.line((11, 11), (11, 21), 2.4, CREAM); return ic
def rapier_range():       # 한 줄을 꿰뚫는다
    ic = new()
    for x in (11, 22): foe(ic, (x, 16), 5)
    ic.line((2, 16), (27, 16), 2, CREAM); head(ic, (27, 16), 0, 3, CREAM); return ic
def rapier_movement():    # 순간이동해 찌른다
    ic = new(); star4(ic, (6, 16), 5, TEAL); foe(ic, (25, 16), 5)
    ic.line((11, 16), (18, 16), 2, CREAM); head(ic, (18, 16), 0, 2, CREAM); return ic
def rapier_follow_up():   # 다섯 번 연달아 찌른다
    ic = new()
    for k, y in enumerate((6, 11, 16, 21, 26)):
        x0 = 4 + abs(k - 2) * 3; c = GOLD if k % 2 else CREAM
        ic.line((x0, y), (x0 + 16, y), 2, c); head(ic, (x0 + 16, y), 0, 2, c)
    return ic

# ---------------- 단검
def dagger_default():     # 몸을 숨긴다
    ic = new(); hood = new()
    hood.poly([(16, 3), (27, 14), (27, 29), (5, 29), (5, 14)], CREAM); hood.disc((16, 17), 6, DARK)
    for y in range(ic.n):
        for x in range(ic.n):
            c = hood.px[y][x]
            if c and (c == DARK or (x // ic.k + y // ic.k) % 2 == 0): ic.put(x, y, c)
    ic.disc((13.5, 17), 1.3, GOLD); ic.disc((18.5, 17), 1.3, GOLD); return ic
def dagger_range():       # 먹안개
    ic = new()
    for c, r in [((10, 18), 7), ((19, 13), 8), ((23, 20), 6.5), ((15, 22), 6)]: ic.disc(c, r, SHADE)
    for c, r in [((10, 18), 4.5), ((19, 13), 5.5), ((23, 20), 4), ((15, 22), 3.5)]: ic.disc(c, r, DARK)
    return ic
def dagger_movement():    # 짧게 도약, 세 번까지
    ic = new()
    for k in range(3): ic.ring((7 + k * 9, 22), 4, 2.4, TEAL, 180, 360)
    ground(ic, 25); return ic
def dagger_follow_up():   # 앞을 여섯 번 빠르게 벤다: 나란한 베기 자국(발톱 자국처럼)
    ic = new()
    for (a, b), col in [(((5, 19), (17, 7)), GOLD), (((9, 23), (23, 9)), CREAM), (((15, 27), (27, 15)), GOLD)]:
        ic.line(a, b, 2.8, col); ic.line((a[0] - 1.5, a[1] + 1.5), a, 1.2, col); ic.line(b, (b[0] + 1.5, b[1] - 1.5), 1.2, col)
    return ic

# ---------------- 망치
def hammer_default():     # 크게 휘둘러 멀리 밀쳐 낸다
    ic = new(); ic.line((4, 28), (13, 19), 2.6, SHADE)
    u, v = (.7071, .7071), (.7071, -.7071); c = (15, 17)
    def P(a, b): return (c[0] + u[0] * a + v[0] * b, c[1] + u[1] * a + v[1] * b)
    ic.poly([P(-6.5, -3.5), P(6.5, -3.5), P(6.5, 3.5), P(-6.5, 3.5)], CREAM); ic.poly([P(-6.5, 1), P(6.5, 1), P(6.5, 3.5), P(-6.5, 3.5)], SHADE)
    arrow(ic, (20, 12), (25, 7), 3, GOLD, 4.5); return ic
def hammer_range():       # 땅을 내리쳐 모두 기절
    ic = new(); ground(ic, 27); ic.line((16, 3), (16, 15), 2.6, SHADE); hammer_head(ic, 9, 14, 14, 11, "h")
    star4(ic, (5, 9), 4.5, GOLD); star4(ic, (27, 9), 4.5, GOLD); return ic
def hammer_movement():    # 돌진하며 밀어낸다
    ic = new(); ic.line((8, 16), (20, 16), 2.6, SHADE); hammer_head(ic, 19, 6, 9, 20); speed(ic, 9, [9, 23], 7); return ic
def hammer_follow_up():   # 여진 세 번
    ic = new(); ground(ic, 27)
    for k, (x, w) in enumerate([(6, 2), (15, 3), (25, 4)]):
        ic.poly([(x - w, 26), (x, 26 - 3 * w), (x + w, 26)], GOLD if k == 2 else CREAM)
    return ic

# ---------------- 방패
def shield_default():     # 막고 되받는다, 투사체는 되돌린다
    ic = new(); shield_icon(ic, (9, 13), 6.5)
    ic.line((29, 5), (19, 15), 2, SHADE); arrow(ic, (19, 17), (25, 23), 2.6, GOLD, 3.5); return ic
def shield_range():       # 보호막을 둘러 받는 피해를 줄인다
    ic = new(); ic.ring((16, 16), 15, 13, TEAL); ic.ring((16, 16), 12.5, 11.5, TEAL, 200, 250)   # 막 + 윗쪽 반사광
    shield_icon(ic, (16, 16), 6.5); return ic
def shield_movement():    # 돌진하며 모아 밀어 낸다
    ic = new(); ic.poly([(15, 4), (20, 4), (20, 28), (15, 28)], CREAM); ic.poly([(18, 4), (20, 4), (20, 28), (18, 28)], SHADE)
    speed(ic, 12, [9, 16, 23], 8); foe(ic, (26, 11), 3.5); foe(ic, (26, 21), 3.5); return ic
def shield_follow_up():   # 미끼 방패로 끌어들인다
    ic = new(); shield_icon(ic, (16, 13), 6.5); foe(ic, (4, 22), 3.5); foe(ic, (28, 22), 3.5)
    arrow(ic, (3, 13), (6, 13), 2, GOLD, 2.5); arrow(ic, (29, 13), (26, 13), 2, GOLD, 2.5); return ic

# ---------------- 대검
def greatsword_default(): # 모았다가 내려찍는다
    ic = new(); vsword(ic, 16, 1, 19, 3.4); ground(ic, 28)
    for s in (-1, 1): ic.line((16 + s * 4, 26), (16 + s * 8, 22), 2, GOLD); ic.line((16 + s * 5, 28), (16 + s * 10, 28), 2, GOLD)
    return ic
def greatsword_range():   # 꽂아 양옆을 벤다
    ic = new(); vsword(ic, 16, 2, 20, 3.2); ground(ic, 28)
    moon(ic, (8, 19), 8, (11, 19), 7.5, GOLD); moon(ic, (24, 19), 8, (21, 19), 7.5, GOLD); return ic
def greatsword_movement():# 돌리며 나아간다
    ic = new(); ic.ring((16, 16), 14, 11.6, GOLD, 315, 225); head(ic, (7.6, 7.6), -45, 4, GOLD)
    ic.line((10, 22), (22, 10), 3, CREAM); ic.line((8, 20), (12, 24), 2.4, CREAM); ic.line((7, 25), (10, 22), 2.4, SHADE); return ic
def greatsword_follow_up():# 던져 꽂은 검이 돌며 벤다
    ic = new(); vsword(ic, 16, 3, 18, 3); ground(ic, 28)
    ic.ring((16, 16), 15, 13, GOLD, 135, 225); ic.ring((16, 16), 15, 13, GOLD, 315, 45); return ic

# ---------------- 발사기
def launcher_ball():      # 세 단계 모아쏘기
    ic = new(); ic.disc((16, 16), 6, GOLD); ic.disc((14, 14), 2, GOLD_HI)
    ic.ring((16, 16), 10.5, 9, GOLD); ic.ring((16, 16), 15, 13.5, SHADE); return ic
def launcher_laser():     # 레이저
    ic = new(); ic.poly([(1, 10), (8, 10), (8, 22), (1, 22)], CREAM)
    ic.line((8, 16), (31, 16), 7, TEAL); ic.line((8, 16), (31, 16), 2.4, LIGHT); return ic
def launcher_shotgun():   # 부채꼴 다섯 발, 반동으로 뒤로
    ic = new(); c = (12, 16)
    for dx, dy in [(2, -2), (2, -1), (1, 0), (2, 1), (2, 2)]:
        L = 15 / math.hypot(dx, dy); ic.line(c, (c[0] + dx * L * .9, c[1] + dy * L * .9), 2, GOLD)
    arrow(ic, (10, 16), (5, 16), 2.6, TEAL, 3); return ic
def launcher_grenade():   # 포물선 유탄 → 파편 넷
    ic = new()
    for k in range(5):
        t = k / 5; ic.disc((5 + t * 14, 23 - math.sin(t * math.pi) * 14), 1.1, SHADE)
    ic.disc((21, 20), 4.5, GOLD)
    for dx, dy in [(-1, -1), (1, -1), (-1, 1), (1, 1)]: ic.disc((21 + dx * 6, 20 + dy * 5.5), 1.5, GOLD)
    return ic

# ---------------- 폭탄
def bomb_basic():         # 폭탄 셋을 연달아
    ic = new()
    for c in [(7, 22), (16, 13), (25, 22)]: bomb_at(ic, c, 5)
    return ic
def bomb_range():         # 대형 폭탄
    ic = new(); bomb_at(ic, (15, 18), 12); return ic
def bomb_move():          # 미사일
    ic = new(); ic.poly([(9, 12), (23, 12), (27, 16), (23, 20), (9, 20)], CREAM); ic.poly([(9, 16), (27, 16), (23, 20), (9, 20)], SHADE)
    ic.poly([(9, 12), (6, 9), (13, 9), (16, 12)], SHADE); ic.poly([(9, 20), (6, 23), (13, 23), (16, 20)], SHADE)
    ic.poly([(1, 14), (8, 14), (8, 18), (1, 18)], GOLD); return ic
def bomb_followup():      # 1.5초 뒤 크게 터진다
    ic = new(); bomb_at(ic, (15, 18), 11); ic.disc((15, 18), 6.5, LIGHT)
    ic.line((15, 18), (15, 13), 1.6, INK); ic.line((15, 18), (19, 18), 1.6, INK); return ic

# ---------------- 수리검
def shuriken_basic():     # 닿는 적을 모두 꿰뚫는 큰 수리검
    ic = new(); star4(ic, (11, 16), 10, CREAM, DARK); foe(ic, (26, 16), 4.5); ic.line((21, 16), (31, 16), 2, GOLD); return ic
def shuriken_range():     # 터지는 수리검
    ic = new(); star4(ic, (9, 22), 7, CREAM, DARK); burst(ic, (21, 11), 10, GOLD); ic.disc((21, 11), 2.8, GOLD_HI); return ic
def shuriken_move():      # 맞힌 적의 등 뒤로
    ic = new(); foe(ic, (16, 20), 5.5); star4(ic, (4.5, 20), 4, CREAM)
    ic.ring((16, 20), 12, 10, TEAL, 180, 360); head(ic, (27, 21), 90, 3, TEAL); return ic
def shuriken_followup():  # 천천히 도는 큰 수리검
    ic = new(); star4(ic, (16, 16), 10, CREAM, DARK)
    ic.ring((16, 16), 15, 13, GOLD, 300, 30); ic.ring((16, 16), 15, 13, GOLD, 120, 210); return ic

_RAW = [
    ("검", [("크게 베기", sword_default), ("검기", sword_range), ("돌진 찌르기", sword_movement), ("잔상 베기", sword_follow_up)]),
    ("채찍", [("휘감아 묶기", whip_default), ("넓게 묶기", whip_range), ("매듭 날기", whip_movement), ("매듭 조이기", whip_follow_up)]),
    ("레이피어", [("모아 찌르기", rapier_default), ("한 줄 꿰뚫기", rapier_range), ("순간이동 찌르기", rapier_movement), ("다섯 번 찌르기", rapier_follow_up)]),
    ("단검", [("숨기", dagger_default), ("먹안개", dagger_range), ("세 번 도약", dagger_movement), ("여섯 번 베기", dagger_follow_up)]),
    ("망치", [("밀쳐내기", hammer_default), ("땅 내리쳐 기절", hammer_range), ("돌진", hammer_movement), ("여진 세 번", hammer_follow_up)]),
    ("방패", [("막고 되받기", shield_default), ("보호막", shield_range), ("돌진 밀기", shield_movement), ("미끼 방패", shield_follow_up)]),
    ("대검", [("모아 내려찍기", greatsword_default), ("꽂아 양옆 베기", greatsword_range), ("돌며 나아가기", greatsword_movement), ("던져 꽂고 돌기", greatsword_follow_up)]),
    ("발사기", [("모아쏘기", launcher_ball), ("레이저", launcher_laser), ("샷건", launcher_shotgun), ("유탄", launcher_grenade)]),
    ("폭탄", [("셋 던지기", bomb_basic), ("대형 폭탄", bomb_range), ("미사일", bomb_move), ("시한 폭탄", bomb_followup)]),
    ("수리검", [("꿰뚫는 큰 수리검", shuriken_basic), ("터지는 수리검", shuriken_range), ("등 뒤로", shuriken_move), ("도는 큰 수리검", shuriken_followup)]),
]

def _lit(f):
    def g(): return f().bevel()
    return g
SKILLS = [(w, [(n, _lit(f)) for n, f in lst]) for w, lst in _RAW]
