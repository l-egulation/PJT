# 맨손 잽 이펙트 v3(2026-11-12). 시간 배분은 사용자가 고른 B(끊어치기). 앞 판은 unarmed_fx.py(v1 작은 충격, v2 흰 초승달).
# 사용자: 「좀 더 가운데가 더 두꺼웠으면 좋겠고, 좀 더 완만하다의 반대말… 그런 형태」 = 가운데를 두껍게 + 더 급하게(가파르게) 휜 모양.
#   「급하게」가 어느 쪽인지 그림으로 고르게 둘을 낸다.
#     가 = 가운데만 두껍게(바깥 곡선은 v2 와 같은 둥근 호)
#     나 = 가운데 두껍게 + 앞으로 더 깊이 내민 가파른 곡선(위아래 끝이 뒤로 길게 빠진다)
# 모양: 위아래 끝을 같이 쓰는 반타원 둘 사이. 바깥 반타원(앞으로 rx_out)과 안쪽 반타원(rx_in) → 가운데 두께 = rx_out - rx_in, 끝으로 갈수록 0.
# 5장 x 0.03초, 타격 순간(0.12초)에 시작: 작고 얇게 생김 → 가장 두껍고 큼 → 앞으로 나가며 가늘어짐 → 실처럼 끊김 → 흰 점.
# 게임 이펙트 밀도(PPU32). 흰색 + 오목한 쪽 회색 한 줄(크림 배경에서 흰색이 묻히지 않게). 2타는 한 치수 크게.
import json, math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; SRC = ROOT / '4_정리본'; OUT = ROOT / '5_이펙트'; OUT.mkdir(exist_ok=True)
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Combat/Resources')
WHITE, SHADE = (255, 255, 255, 255), (150, 156, 168, 255)
W, H, OX, OY = 104, 88, 34, 44   # 원점 = 주먹 끝
def crescent(ry, rx_out, rx_in, a, gaps=()):   # 볼록한 가운데가 원점에서 a 만큼 앞
    xt = a - rx_out; pts = set()
    for y in range(-ry, ry + 1):
        if any(g0 <= y <= g1 for g0, g1 in gaps): continue
        k = math.sqrt(max(0.0, 1 - (y / (ry + .5)) ** 2)); x0 = xt + rx_in * k; x1 = xt + rx_out * k
        for x in range(math.floor(x0) - 1, math.ceil(x1) + 2):
            if x0 <= x + .5 < x1: pts.add((x, y))
    return pts
# (세로 반폭, 앞으로 내민 깊이, 가운데 두께) — 가장 클 때 기준
SHAPE = {'가': (22, 22, 13), '나': (22, 31, 15)}
def frames(kind, big):
    ry, out_, th = SHAPE[kind]; k = 3 if big else 0; ry += k; out_ += k; th += k // 2; res = []
    plan = ((.62, .45, 5, ()), (1.0, 1.0, 11, ()), (1.08, .55, 17, ()), (1.14, .2, 21, ((-7, -5), (8, 10))), (1.18, .08, 24, ((-60, -12), (-4, 3), (9, 15), (21, 60))))
    for sc, tk, a, gaps in plan:
        r = round(ry * sc); o = round(out_ * sc); t = max(1, round(th * tk)); body = crescent(r, o, o - t, a, gaps)
        shade = {(x, y) for x, y in body if (x - 1, y) not in body} if t >= 5 else {(x - 1, y) for x, y in body} - body
        res.append((body - shade, shade))
    return res
def draw(pts_white, pts_shade):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0)); px = im.load()
    for pts, col in ((pts_white, WHITE), (pts_shade, SHADE)):
        for x, y in pts:
            if 0 <= OX + x < W and 0 <= OY + y < H: px[OX + x, OY + y] = col
    return im
NF = 5; FX = {(kind, clip): [draw(*f) for f in frames(kind, big)] for kind in SHAPE for clip, big in (('jab', False), ('jab_big', True))}
for (kind, clip), fr in FX.items():
    d_ = OUT / f'{kind}_{clip}'; d_.mkdir(exist_ok=True)
    for old in d_.glob('*.png'): old.unlink()
    for i, im in enumerate(fr): im.save(d_ / f'{i:02d}.png')
(OUT / 'fx.json').write_text(json.dumps({'format': 'deviant-unarmed-jab-fx-draft-v3-crescent', 'pixelsPerUnit': 32, 'size': [W, H], 'originTopLeftPx': [OX, OY], 'fps': 100 / 3, 'frames': NF,
    'variants': {k: {'halfHeight': v[0], 'forwardDepth': v[1], 'middleThickness': v[2]} for k, v in SHAPE.items()}, 'clips': {'jab': '1타', 'jab_big': '2타 (한 치수 크게)'},
    'anchor': '주먹 끝. 타격 순간(0.12초)에 시작, 캐릭터가 보는 쪽으로 뒤집는다'}, ensure_ascii=False, indent=1), encoding='utf-8')
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
# 이펙트 장 한눈에
Z = 4; sheet = Image.new('RGB', (NF * (W * Z + 10) + 10, 2 * (H * Z + 40) + 10), 'white'); d = ImageDraw.Draw(sheet)
for r, (kind, t) in enumerate((('가', '가: 가운데만 두껍게'), ('나', '나: 가운데 두껍게 + 더 가파르게'))):
    for i, im in enumerate(FX[(kind, 'jab')]):
        x, y = 10 + i * (W * Z + 10), 10 + r * (H * Z + 40); bg = Image.new('RGBA', (W, H), (70, 78, 86, 255) if i % 2 else (236, 228, 212, 255)); bg.alpha_composite(im)
        sheet.paste(bg.convert('RGB').resize((W * Z, H * Z), Image.NEAREST), (x, y + 30)); d.text((x + 4, y + 4), f'{t}  {i + 1}/{NF}' if i == 0 else f'{i + 1}/{NF}', font=F, fill=(0, 0, 0))
sheet.save(OUT / '이펙트-장.png')

# B 시간 배분 위에 얹어 게임 속도로: 왼쪽 가 / 오른쪽 나, 위 밝은 배경 / 아래 어두운 배경
cells = {int(p.stem[-2:]): Image.open(p).convert('RGBA') for p in SRC.glob('*타_*_칸*.png')}
def idle():
    im = Image.open(RES / 'UnarmedBody/idle/F01.png').convert('RGBA'); big = im.resize((round(128 * 10 / 3), round(128 * 10 / 3)), Image.NEAREST)
    cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0)); cv.alpha_composite(big, (220 - round(64 * 10 / 3), 327 - round(121 * 10 / 3))); return cv
IDLE = idle()
def fist_tip(im):   # 뻗은 장에서 맨 오른쪽 먹색 덩어리(주먹)의 오른쪽 끝과 가운데 높이
    a = np.asarray(im); m = (a[..., 3] > 0) & (a[..., :3].max(2) < 60); lab, n = ndi.label(m); best = None
    for k in range(1, n + 1):
        yy, xx = np.where(lab == k)
        if len(yy) > 60 and (best is None or xx.max() > best[0]): best = (int(xx.max()), float(yy.mean()))
    return best
B = {'1타': [(9, .10), (10, .02), (11, .06), (12, .06), (14, .05), (15, .07)], '2타': [(20, .10), (22, .02), (23, .07), (26, .07), (32, .06)]}
TIP = {'1타': fist_tip(cells[11]), '2타': fist_tip(cells[23])}
def with_fx(body, key, i, tip):
    cv = body.copy(); fx = FX[key][i]; big = fx.resize((round(W * 10 / 3), round(H * 10 / 3)), Image.NEAREST)
    cv.alpha_composite(big, (round(tip[0] + 2 - OX * 10 / 3), round(tip[1] - OY * 10 / 3))); return cv
VIEW = (90, 60, 600, 345); vw, vh = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]
def view(im, bg):
    w = Image.new('RGBA', im.size, bg); w.alpha_composite(im); return w.convert('RGB').crop(VIEW)
def track(kind):   # 10ms 눈금마다 (그림, 바뀜 표시용 열쇠)
    out = [(IDLE, 'i')] * 30
    for k in ('1타', '2타'):
        t = 0
        for c, s in B[k]:
            for q in range(int(round(s * 100))):
                i = (t + q - 12) // 3; on = 0 <= i < NF
                out.append((with_fx(cells[c], (kind, 'jab' if k == '1타' else 'jab_big'), i, TIP[k]), (k, c, i)) if on else (cells[c], (k, c)))
            t += int(round(s * 100))
    return out + [(IDLE, 'i')] * 40
tr = {kind: track(kind) for kind in SHAPE}; n = len(tr['가'])
def save(path, mul):
    runs = []
    for t in range(n):
        key = tuple(tr[k][t][1] for k in SHAPE)
        if runs and runs[-1][0] == key: runs[-1][2] += 1
        else: runs.append([key, t, 1])
    frs = []
    for _, t, _ in runs:
        p = Image.new('RGB', (2 * vw + 10, 2 * vh + 46), 'white'); dd = ImageDraw.Draw(p)
        for col, (kind, name) in enumerate((('가', '가: 가운데만 두껍게'), ('나', '나: 두껍게 + 더 가파르게'))):
            p.paste(view(tr[kind][t][0], (236, 228, 212, 255)), (col * (vw + 10), 34)); p.paste(view(tr[kind][t][0], (70, 78, 86, 255)), (col * (vw + 10), 44 + vh)); dd.text((col * (vw + 10) + 6, 4), name, font=F, fill=(0, 0, 0))
        frs.append(p)
    ms = [max(20, c * 10 * mul) for _, _, c in runs]; frs[0].save(path, save_all=True, append_images=frs[1:], duration=ms, loop=0, disposal=1)
for old in OUT.glob('맨손-B-이펙트-*.gif'): old.unlink()
for old in ('jab', 'jab_big'):
    for q in (OUT / old).glob('*.png'): q.unlink()
    if (OUT / old).exists(): (OUT / old).rmdir()
save(OUT / '맨손-B-초승달-가나-게임속도.gif', 1); save(OUT / '맨손-B-초승달-가나-0.5배.gif', 2); save(OUT / '맨손-B-초승달-가나-0.25배.gif', 4)
print('ok', TIP)
