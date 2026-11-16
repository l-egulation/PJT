# 맨손 기본 공격을 새 잽 두 번으로 바꾸는 칸 고르기(2026-11-12). 사용자: 「이거 우리 기본 동작 이걸로 변경하려고」 → 「맨손 기본 공격」
# 게임 시간(data/combat/unarmed_basic.json, 바꾸지 않음): 1타 0.36초·타격 0.12초, 2타 0.32초·타격 0.12초. 지금 그림은 UnarmedAttack/approved-combo(640x360, PPU 90, 피벗 (340,340)) 10장+9장.
# 새 시트(6x6, 칸 640x360, 1유닛 106.67px, 발밑 (220,327)): #3~9 주먹 올린 자세 / #10 몸을 실음 / #11~13 잽 1 / #14 당김 / #15~20 자세 / #21~22 나감 / #23~35 잽 2
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; OUT = ROOT / '3_프레임'
GAME = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Combat/Resources/UnarmedAttack/approved-combo')
SEQ = {'1타': [(8, .04, '자세'), (9, .04, '노려봄'), (10, .04, '몸을 실음'), (11, .04, '뻗음'), (12, .04, '버팀'), (13, .05, ''), (14, .05, '당김'), (15, .06, '자세로')],
       '2타': [(20, .04, '자세'), (21, .04, '나감'), (22, .04, ''), (23, .04, '뻗음'), (24, .04, '버팀'), (26, .04, ''), (29, .04, ''), (32, .04, '유지')]}
HIT = {'1타': .12, '2타': .12}; DUR = {'1타': .36, '2타': .32}
NOW = {'1타': (['01', '03', '05', '06', '07', '09', '12', '13', '16', '18'], [.04, .04, .04, .03, .04, .03, .04, .04, .03, .03]),
       '2타': (['19', '20', '21', '22', '24', '27', '28', '29', '30'], [.07, .05, .03, .03, .02, .02, .035, .035, .03])}
for k, v in SEQ.items(): assert abs(sum(s for _, s, _ in v) - DUR[k]) < 1e-6, k
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
VIEW = (60, 90, 440, 345); vw, vh = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]; COL = {'1타': (20, 80, 160), '2타': (170, 40, 40)}
def cell(i):
    im = Image.open(OUT / '칸' / f'{i:02d}.png').convert('RGBA'); w = Image.new('RGBA', im.size, 'white'); w.alpha_composite(im); return w.convert('RGB')
def game(name):   # PPU 90 → 106.67 로 키우고 피벗 (340,340) 을 새 칸의 발밑 (220,327) 에
    im = Image.open(GAME / f'frame-{name}.png').convert('RGBA'); k = 106.6667 / 90; im = im.resize((round(im.width * k), round(im.height * k)), Image.NEAREST)
    w = Image.new('RGBA', (640, 360), 'white'); w.alpha_composite(im, (220 - round(340 * k), 327 - round(340 * k))); return w.convert('RGB')
def face_area(im):
    a = np.asarray(im).astype(int); lab, n = ndi.label(a.max(2) < 90); best = 0
    for k in range(1, n + 1):
        yy, xx = np.where(lab == k)
        if 1200 < len(yy) < 5000 and yy.max() < 230 and xx.max() - xx.min() < 90: best = max(best, int(ndi.binary_fill_holes(lab == k).sum()))
    return best
base = face_area(cell(0)); sizes = {i: round((face_area(cell(i)) / base) ** .5, 3) for k in SEQ for i, _, _ in SEQ[k] if face_area(cell(i))}
# 고른 칸 표
allf = [(k, *f) for k, v in SEQ.items() for f in v]; strip = Image.new('RGB', (8 * vw, 2 * (vh + 56)), 'white'); d = ImageDraw.Draw(strip); t = {k: 0.0 for k in SEQ}
for n, (k, i, s, note) in enumerate(allf):
    x, y = (n % 8) * vw, (n // 8) * (vh + 56); strip.paste(cell(i).crop(VIEW), (x, y + 56)); hit = abs(t[k] - HIT[k]) < 1e-6; t[k] += s
    d.text((x + 6, y + 4), f'{k}  #{i}' + ('  ← 타격' if hit else ''), font=F, fill=COL[k]); d.text((x + 6, y + 30), f'{s:.2f}초  {note}', font=FS, fill=(70, 70, 70))
strip.save(OUT / '맨손-새잽-고른칸.png')
# 비교 GIF (10ms 눈금)
def track(items):
    out = []; acc = 0.0; done = 0
    for n, s in enumerate(items):
        acc += s; upto = int(round(acc * 100)); out += [n] * (upto - done); done = upto
    return out
new_im = [cell(i).crop(VIEW) for k in SEQ for i, _, _ in SEQ[k]]; new_lab = [f'{k}  #{i}' for k in SEQ for i, _, _ in SEQ[k]]; new_k = [k for k in SEQ for _ in SEQ[k]]
old_im = [game(n).crop(VIEW) for k in NOW for n in NOW[k][0]]
tn = track([s for k in SEQ for _, s, _ in SEQ[k]]); to = track([s for k in NOW for s in NOW[k][1]]); assert len(tn) == len(to) == 68, (len(tn), len(to))
runs = []
for o, n in zip(to, tn):
    if runs and runs[-1][0] == (o, n): runs[-1][1] += 1
    else: runs.append([(o, n), 1])
def panel(o, n):
    p = Image.new('RGB', (vw, 2 * vh + 60), 'white'); d = ImageDraw.Draw(p); p.paste(old_im[o], (0, 30)); p.paste(new_im[n], (0, vh + 60))
    d.text((8, 2), '지금 게임', font=F, fill=(90, 90, 90)); d.text((8, vh + 32), '새 잽  ' + new_lab[n], font=F, fill=COL[new_k[n]]); d.line((0, vh + 30, vw, vh + 30), fill=(200, 200, 200)); return p
def save(path, mul):
    fr = [panel(*k) for k, _ in runs]; ms = [max(20, int(c * 10 * mul)) for _, c in runs]; ms[-1] += 700
    fr[0].save(path, save_all=True, append_images=fr[1:], duration=ms, loop=0, disposal=1)
save(OUT / '맨손-비교-게임속도.gif', 1); save(OUT / '맨손-비교-0.5배.gif', 2); save(OUT / '맨손-비교-0.25배.gif', 4)
(OUT / 'unarmed-selection.json').write_text(json.dumps({'sheet': '2_에테르결과/31dccac8-edf1-4c5f-a153-bcee66cc00db.png', 'cell': [640, 360],
    'sequence': {k: [{'cell': i, 'seconds': s} for i, s, _ in v] for k, v in SEQ.items()}, 'hit': HIT, 'duration': DUR, 'faceScaleVsCell0': sizes}, ensure_ascii=False, indent=1), encoding='utf-8')
print('ok', len(runs), 'face scale vs #0', sizes)
