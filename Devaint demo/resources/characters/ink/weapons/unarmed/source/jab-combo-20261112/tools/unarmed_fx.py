# 맨손 잽 이펙트(2026-11-12). 사용자: 「B 로 갈게. 그리고 이펙트를 만들어줘. 그냥 간단하게 하면 돼」
# 간단한 것 하나: 주먹 앞에서 터지는 작은 충격. 4장, 장당 0.03초(합 0.12초), 타격 순간(0.12초)에 시작.
#   0 주먹 끝에서 짧은 살 세 가닥 + 가운데 밝은 점 / 1 주먹 앞에 굵은 호가 생기고 살이 떨어져 나감 / 2 호가 넓고 가늘어지며 끊김 / 3 점 몇 개만 남음
# 그리는 법: 게임 이펙트 밀도(PPU32)로 화소를 직접 찍는다. 색은 기본검 타격 이펙트처럼 먹 #1B1616 에 크림 #FDEEDA 속살 → 밝은 배경과 어두운 배경 모두에서 보인다.
#   호는 원을 화소로 딴 것이라 계단이 고르다. 테두리 선, 큰 도형, 번쩍이는 판은 없다.
# 2타(정면 주먹, 피해 10)는 같은 모양을 한 치수 크게(호 반지름 +2, 살 한 가닥 더).
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; SRC = ROOT / '4_정리본'; OUT = ROOT / '5_이펙트'; OUT.mkdir(exist_ok=True)
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Combat/Resources')
# v2(같은 날): 사용자 「너무 평범해. 범위 넓게 해서 **초승달 모양으로 흰색** 이펙트를 만들고 싶어」 → 작은 충격(v1)은 버리고 주먹 앞에 큰 흰 초승달.
#   초승달 = 원에서 왼쪽으로 조금 밀린 같은 크기 원을 뺀 모양(가운데가 가장 두껍고 위아래 끝이 뾰족). 볼록한 쪽이 앞.
#   5장 x 0.03초: 얇게 생김 → 가장 두껍고 큼 → 앞으로 나가며 가늘어짐 → 실처럼 가늘고 끊김 → 흰 점
#   크기는 판정 상자(1타 높이 1.5유닛, 2타 폭 1.6유닛)에 맞춰 키 1.4~1.6유닛. 색은 흰색, 오목한 쪽에 연회색 한 줄(밝은 배경에서도 모양이 보이게).
WHITE, SHADE = (255, 255, 255, 255), (150, 156, 168, 255)
W, H, OX, OY = 84, 80, 22, 40   # 원점 = 주먹 끝
def lune(R, d, a, clip=.92, gaps=()):   # 볼록한 가운데가 원점에서 a 만큼 앞. R = 반지름, d = 가운데 두께
    cx = a - R; pts = set()
    for y in range(-int(R * clip), int(R * clip) + 1):
        if any(g0 <= y <= g1 for g0, g1 in gaps): continue
        for x in range(int(cx - R) - 1, int(cx + R) + 2):
            if (x - cx) ** 2 + y * y <= (R + .5) ** 2 and (x - (cx - d)) ** 2 + y * y > (R + .5) ** 2: pts.add((x, y))   # 반 화소 더해 가운데에 한 점만 튀어나오지 않게
    return pts
def frames(big):
    k = 3 if big else 0; out = []
    for R, d, a, gaps in ((15 + k, 3, 5, ()), (22 + k, 8 + k // 3, 11, ()), (25 + k, 5, 17, ()), (27 + k, 2, 21, ((-6, -4), (7, 9))), (28 + k, 1, 24, ((-20, -9), (-3, 2), (8, 14), (20, 40)))):
        body = lune(R, d, a); body = {(x, y) for x, y in body if not any(g0 <= y <= g1 for g0, g1 in gaps)}
        shade = {(x, y) for x, y in body if (x - 1, y) not in body} if d >= 5 else {(x - 1, y) for x, y in body} - body   # 오목한 쪽에 회색 한 줄: 두꺼운 장은 안쪽 가장자리, 가는 장은 바로 뒤. 크림 배경에서 흰색이 묻히지 않게
        out.append((body - shade, shade))
    return out
def draw(pts_white, pts_shade):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); px = im.load()
    for pts, col in ((pts_white, WHITE), (pts_shade, SHADE)):
        for x, y in pts:
            if 0 <= OX + x < W and 0 <= OY + y < H: px[OX + x, OY + y] = col
    return im
FX = {name: [draw(*f) for f in frames(big)] for name, big in (('jab', False), ('jab_big', True))}
NF = 5
for name, fr in FX.items():
    (OUT / name).mkdir(exist_ok=True)
    for old in (OUT / name).glob('*.png'): old.unlink()
    for i, im in enumerate(fr): im.save(OUT / name / f'{i:02d}.png')
(OUT / 'fx.json').write_text(json.dumps({'format': 'deviant-unarmed-jab-fx-draft-v2-crescent', 'pixelsPerUnit': 32, 'size': [W, H], 'originTopLeftPx': [OX, OY], 'fps': 100 / 3, 'frames': NF,
    'clips': {'jab': '1타', 'jab_big': '2타'}, 'anchor': '주먹 끝. 타격 순간(0.12초)에 시작, 캐릭터가 보는 쪽으로 뒤집는다'}, ensure_ascii=False, indent=1), encoding='utf-8')
# 이펙트 장 한눈에 (10배)
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
Z = 5; sheet = Image.new('RGB', (NF * (W * Z + 10) + 10, 2 * (H * Z + 40) + 10), 'white'); d = ImageDraw.Draw(sheet)
for r, (name, t) in enumerate((('jab', '1타 이펙트'), ('jab_big', '2타 이펙트 (한 치수 크게)'))):
    for i, im in enumerate(FX[name]):
        x, y = 10 + i * (W * Z + 10), 10 + r * (H * Z + 40); bg = Image.new('RGBA', (W, H), (236, 228, 212, 255) if i % 2 == 0 else (70, 78, 86, 255)); bg.alpha_composite(im)
        sheet.paste(bg.convert('RGB').resize((W * Z, H * Z), Image.NEAREST), (x, y + 30)); d.text((x + 4, y + 4), f'{t}  {i + 1}/{NF}' if i == 0 else f'{i + 1}/{NF}', font=F, fill=(0, 0, 0))
sheet.save(OUT / '이펙트-장.png')

# B 시간 배분 위에 얹어 게임 속도로
cells = {int(p.stem[-2:]): Image.open(p).convert('RGBA') for p in SRC.glob('*타_*_칸*.png')}
def idle():
    im = Image.open(RES / 'UnarmedBody/idle/F01.png').convert('RGBA'); big = im.resize((round(128 * 10 / 3), round(128 * 10 / 3)), Image.NEAREST)
    cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0)); cv.alpha_composite(big, (220 - round(64 * 10 / 3), 327 - round(121 * 10 / 3))); return cv
IDLE = idle()
def fist_tip(im):   # 뻗은 장에서 맨 오른쪽 먹색 덩어리(주먹)의 오른쪽 끝과 가운데 높이
    import numpy as np; from scipy import ndimage as ndi
    a = np.asarray(im); m = (a[..., 3] > 0) & (a[..., :3].max(2) < 60); lab, n = ndi.label(m); best = None
    for k in range(1, n + 1):
        yy, xx = np.where(lab == k)
        if len(yy) > 60 and (best is None or xx.max() > best[0]): best = (int(xx.max()), float(yy.mean()))
    return best
B = {'1타': [(9, .10), (10, .02), (11, .06), (12, .06), (14, .05), (15, .07)], '2타': [(20, .10), (22, .02), (23, .07), (26, .07), (32, .06)]}
TIP = {'1타': fist_tip(cells[11]), '2타': fist_tip(cells[23])}
def with_fx(body, clip, i, tip):
    cv = body.copy(); fx = FX[clip][i]; big = fx.resize((round(W * 10 / 3), round(H * 10 / 3)), Image.NEAREST)
    cv.alpha_composite(big, (round(tip[0] + 2 - OX * 10 / 3), round(tip[1] - OY * 10 / 3))); return cv
VIEW = (90, 70, 560, 345); vw, vh = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]
def view(im, bg):
    w = Image.new('RGBA', im.size, bg); w.alpha_composite(im); return w.convert('RGB').crop(VIEW)
def track(fxon):   # 10ms 눈금마다 (640x360 RGBA)
    out = [IDLE] * 30
    for k in ('1타', '2타'):
        t = 0
        for c, s in B[k]:
            for q in range(int(round(s * 100))):
                now = t + q; body = cells[c]; i = (now - 12) // 3
                out.append(with_fx(body, 'jab' if k == '1타' else 'jab_big', i, TIP[k]) if fxon and 0 <= i < NF else body)
            t += int(round(s * 100))
    return out + [IDLE] * 40
ta, tb = track(False), track(True); n = len(tb)
def save(path, mul):
    runs = []
    for t in range(n):
        key = (id(ta[t]), id(tb[t]) if tb[t] in (IDLE,) or tb[t] is ta[t] else (t - 0) // 1 * 0 + hash(tb[t].tobytes()))
        if runs and runs[-1][0] == key: runs[-1][2] += 1
        else: runs.append([key, t, 1])
    frs = []
    for _, t, _ in runs:
        p = Image.new('RGB', (2 * vw + 10, 2 * vh + 46), 'white'); d = ImageDraw.Draw(p)
        for col, (tr, name) in enumerate(((ta, '이펙트 없음'), (tb, '이펙트 있음'))):
            p.paste(view(tr[t], (236, 228, 212, 255)), (col * (vw + 10), 34)); p.paste(view(tr[t], (70, 78, 86, 255)), (col * (vw + 10), 44 + vh)); d.text((col * (vw + 10) + 6, 4), name, font=F, fill=(0, 0, 0))
        frs.append(p)
    ms = [max(20, c * 10 * mul) for _, _, c in runs]; frs[0].save(path, save_all=True, append_images=frs[1:], duration=ms, loop=0, disposal=1)
save(OUT / '맨손-B-이펙트-게임속도.gif', 1); save(OUT / '맨손-B-이펙트-0.5배.gif', 2); save(OUT / '맨손-B-이펙트-0.25배.gif', 4)
print('ok', TIP)
