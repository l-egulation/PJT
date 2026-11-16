# 맨손 잽 시간 배분 비교(2026-11-12). 사용자: 「너무 부드러운 거 같아」
# A(지금 판)는 0.04~0.06초 장을 고르게 이어서 미끄러지듯 보인다. 주먹은 「참았다가 한 번에 뻗고, 뻗은 채 멈췄다가, 빨리 거두는」 대비가 있어야 친 것처럼 보인다.
# 전체 시간과 타격 순간은 그대로(1타 0.36·2타 0.32초, 타격 0.12초). 장 수를 줄이고 시간을 몰아 준다.
#   B 끊어치기: 자세로 0.10초 참음 → 중간 장 0.02초만 → 타격 순간에 다 뻗은 장, 그대로 길게 멈춤 → 빨리 거둠
#   C 끊어치기 + 앞으로 툭: B 에 더해 타격 첫 0.03초 동안 몸 전체를 앞으로 8px(0.075유닛) 밀었다가 제자리로
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; SRC = ROOT / '4_정리본'; OUT = ROOT / '4_정리본' / '끊어치기'; OUT.mkdir(exist_ok=True)
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Combat/Resources')
cells = {int(p.stem[-2:]): Image.open(p).convert('RGBA') for p in SRC.glob('*타_*_칸*.png')}
def idle():
    im = Image.open(RES / 'UnarmedBody/idle/F01.png').convert('RGBA'); big = im.resize((round(128 * 10 / 3), round(128 * 10 / 3)), Image.NEAREST)
    cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0)); cv.alpha_composite(big, (220 - round(64 * 10 / 3), 327 - round(121 * 10 / 3))); return cv
IDLE = idle()
def fr(c, dx=0):
    if c == 'idle': return IDLE
    if not dx: return cells[c]
    cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0)); cv.alpha_composite(cells[c], (dx, 0)); return cv
A = {'1타': [(8, .04), (9, .04), (10, .04), (11, .04), (12, .04), (13, .05), (14, .05), (15, .06)], '2타': [(20, .04), (21, .04), (22, .04), (23, .04), (24, .04), (26, .04), (29, .04), (32, .04)]}
B = {'1타': [(9, .10), (10, .02), (11, .06), (12, .06), (14, .05), (15, .07)], '2타': [(20, .10), (22, .02), (23, .07), (26, .07), (32, .06)]}
C = {'1타': [(9, .10), (10, .02), ((11, 8), .03), (11, .05), (12, .04), (14, .05), (15, .07)], '2타': [(20, .10), (22, .02), ((23, 8), .03), (23, .06), (26, .05), (32, .06)]}
VERS = [('A 지금 판 (고르게)', A), ('B 끊어치기', B), ('C 끊어치기 + 앞으로 툭', C)]
for _, v in VERS:
    assert abs(sum(s for _, s in v['1타']) - .36) < 1e-6 and abs(sum(s for _, s in v['2타']) - .32) < 1e-6
    for k in v:   # 타격 0.12초에 다 뻗은 장이 시작하는지
        acc = 0; ok = False
        for c, s in v[k]:
            if abs(acc - .12) < 1e-6: ok = (c[0] if isinstance(c, tuple) else c) in (11, 23)
            acc += s
        assert ok, k
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
VIEW = (90, 110, 450, 345); vw, vh = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]
def view(im):
    w = Image.new('RGBA', im.size, (246, 246, 246, 255)); w.alpha_composite(im); return w.convert('RGB').crop(VIEW)
def track(v):   # 10ms 눈금마다 (그림, 꼬리표)
    seq = [('idle', .30, '대기')] + [(c, s, k) for k in ('1타', '2타') for c, s in v[k]] + [('idle', .40, '대기')]; out = []; acc = 0.0; done = 0
    for c, s, k in seq:
        acc += s; upto = int(round(acc * 100)); im = view(fr(*c) if isinstance(c, tuple) else fr(c)); out += [(im, k)] * (upto - done); done = upto
    return out
tracks = [track(v) for _, v in VERS]; n = len(tracks[0]); assert all(len(t) == n for t in tracks)
def panel(t):
    p = Image.new('RGB', (3 * vw + 20, vh + 62), 'white'); d = ImageDraw.Draw(p)
    for k, (name, _) in enumerate(VERS):
        im, tag = tracks[k][t]; p.paste(im, (k * (vw + 10), 34)); d.text((k * (vw + 10) + 6, 4), name, font=F, fill=(0, 0, 0)); d.text((k * (vw + 10) + 6, vh + 38), tag, font=FS, fill=(200, 30, 30) if tag != '대기' else (120, 120, 120))
    return p
def save(path, mul):
    runs = []
    for t in range(n):
        key = tuple(id(tracks[k][t][0]) for k in range(3))
        if runs and runs[-1][0] == key: runs[-1][2] += 1
        else: runs.append([key, t, 1])
    frs = [panel(t) for _, t, _ in runs]; ms = [max(20, c * 10 * mul) for _, _, c in runs]
    frs[0].save(path, save_all=True, append_images=frs[1:], duration=ms, loop=0, disposal=1)
save(OUT / '맨손-끊어치기-비교-게임속도.gif', 1); save(OUT / '맨손-끊어치기-비교-0.5배.gif', 2)
# 시간표
PX = 2400; X0 = 250; im = Image.new('RGB', (X0 + int(.68 * PX) + 40, 60 + 3 * 74), 'white'); d = ImageDraw.Draw(im); d.text((10, 8), '시간 배분 (빨간 선 = 타격 0.12초)', font=F, fill=(0, 0, 0))
for r, (name, v) in enumerate(VERS):
    y = 46 + r * 74; d.text((10, y + 16), name, font=FS, fill=(0, 0, 0)); t0 = 0
    for k in ('1타', '2타'):
        acc = 0
        for c, s in v[k]:
            x1 = X0 + int((t0 + acc) * PX); x2 = X0 + int((t0 + acc + s) * PX); cc = c[0] if isinstance(c, tuple) else c
            d.rectangle((x1, y, x2 - 2, y + 56), fill=(255, 215, 200) if cc in (11, 12, 13, 23, 24, 26, 29, 32) else (205, 225, 245), outline=(120, 120, 120)); d.text((x1 + 3, y + 4), f'#{cc}', font=FS, fill=(40, 40, 40)); d.text((x1 + 3, y + 32), f'{s:.2f}', font=FS, fill=(90, 90, 90)); acc += s
        xi = X0 + int((t0 + .12) * PX); d.line((xi, y - 4, xi, y + 60), fill=(220, 30, 30), width=3); t0 += acc
im.save(OUT / '맨손-끊어치기-시간표.png')
(OUT / 'variants.json').write_text(json.dumps({name: {k: [{'cell': (c[0] if isinstance(c, tuple) else c), 'shiftX': (c[1] if isinstance(c, tuple) else 0), 'seconds': s} for c, s in v[k]] for k in v} for name, v in VERS}, ensure_ascii=False, indent=1), encoding='utf-8')
print('ok', n)
