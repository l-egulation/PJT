# 맨손 잽 16장 정리(2026-11-12): 사용자 배경 제거본 → 발을 땅(칸 y=327)에 → 얼굴 크기를 게임 맨손 대기 그림에 → 캐릭터 6색.
# 입력: 맨손잽_배경제거_외곽색보정_16장.zip (자르기 전 자리의 투명 PNG, 640x360)
# 기준: 게임 맨손 대기 UnarmedBody/idle/F01 (128x128, PPU32, 피벗 (0.5, 0.0547)) 를 칸 배율(10/3배)로 키운 것. 게임은 이 대기에서 공격 첫 장으로 바로 넘어간다.
# 눈(찡그림)은 고치지 않는다. 그려 넣은 이펙트는 없다.
import io, json, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; OUT = ROOT / '4_정리본'; OUT.mkdir(exist_ok=True)
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Combat/Resources')
SEL = json.loads((ROOT / '3_프레임' / 'unarmed-selection.json').read_text(encoding='utf-8'))
GROUND, FX = 327, 220
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]], np.int32)
def clean(a):
    """알파 128 로 딱 자르고, 작은 조각·구멍을 정리하고, 6색으로 맞춘다."""
    rgb = a[..., :3].astype(np.int32); solid = a[..., 3] >= 128; m = solid.copy()
    lab, n = ndi.label(m, np.ones((3, 3)))
    if n: sz = ndi.sum(m, lab, range(1, n + 1)); m &= np.isin(lab, 1 + np.flatnonzero(sz >= 24))
    hl, n = ndi.label(~m)
    if n: sz = ndi.sum(~m, hl, range(1, n + 1)); m |= np.isin(hl, 1 + np.flatnonzero(sz < 24)) & (hl != hl[0, 0])
    _, idx = ndi.distance_transform_edt(~solid, return_indices=True); rgb = np.where((m & ~solid)[..., None], rgb[tuple(idx)], rgb)
    k = ((rgb[..., None, :] - PAL) ** 2).sum(-1).argmin(-1); r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k[((r - b >= 100) | (g - b >= 55)) & (r > 170) & (g > 110)] = 5
    out = np.zeros_like(a); out[..., :3] = PAL[k]; out[..., 3] = m * 255; return out
def face_size(a):   # 얼굴(먹 타원, 눈 구멍 메움) 넓이의 제곱근
    rgb = a[..., :3].astype(int); al = a[..., 3] >= 128
    yel = al & (rgb[..., 0] > 200) & (rgb[..., 1] > 130) & (rgb[..., 1] < 200) & (rgb[..., 2] < 90); ink = al & (rgb.max(2) < 60)
    lab, n = ndi.label(ink); best = None; bs = 0
    for j in range(1, n + 1):
        part = lab == j; ys, xs = np.nonzero(part)
        if ys.min() > 230 or len(ys) < 400: continue
        h, w = ys.max() - ys.min(), xs.max() - xs.min()
        if not (25 < h < 80 and 25 < w < 80): continue
        filled = ndi.binary_fill_holes(part); sc = (filled & yel).sum()
        if sc > bs: best, bs = filled, sc
    return float(np.sqrt(best.sum())) if best is not None and bs >= 15 else None
def idle():   # 게임 맨손 대기를 칸 배율로: 피벗 (64, 121) → (220, 327)
    im = Image.open(RES / 'UnarmedBody/idle/F01.png').convert('RGBA'); big = im.resize((round(128 * 10 / 3), round(128 * 10 / 3)), Image.NEAREST)
    cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0)); cv.alpha_composite(big, (FX - round(64 * 10 / 3), GROUND - round(121 * 10 / 3))); return cv
IDLE = idle(); REF = face_size(np.array(IDLE))
z = zipfile.ZipFile(ROOT / '맨손잽_배경제거_외곽색보정_16장.zip'); by_cell = {int(i.filename[-6:-4]): i for i in z.infolist() if i.filename.lower().endswith('.png')}
frames = {}; report = {}
for hit, steps in SEL['sequence'].items():
    for n, st in enumerate(steps):
        c = st['cell']; a = np.array(Image.open(io.BytesIO(z.read(by_cell[c]))).convert('RGBA'))
        solid = a[..., 3] >= 128; lab, cnt = ndi.label(solid, np.ones((3, 3))); sz = ndi.sum(solid, lab, range(1, cnt + 1)); body = lab == (1 + int(np.argmax(sz)))
        a[~ndi.binary_dilation(body, iterations=2), 3] = 0                       # 몸에서 떨어진 조각 지우기
        ys = np.nonzero(body.any(1))[0]; dy = GROUND - int(ys.max()); a = np.roll(a, dy, 0)   # 발밑을 땅에 (맨 아래 화소가 신발 밑창)
        if dy > 0: a[:dy] = 0
        elif dy < 0: a[dy:] = 0
        fs = face_size(a); s = REF / fs if fs else None; report[c] = {'dy': dy, 'face': fs, 'scale': s}; frames[(hit, n)] = (c, a)
med = float(np.median([r['scale'] for r in report.values() if r['scale']]))
for (hit, n), (c, a) in frames.items():
    s = report[c]['scale']
    if s is None or abs(s / med - 1) > .04: s = med; report[c]['scaleUsed'] = 'median'    # 얼굴이 가려진 칸은 가운뎃값
    report[c]['scale'] = round(float(s), 4); im = Image.fromarray(a)
    big = im.resize((round(640 * s), round(360 * s)), Image.LANCZOS); cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0)); cv.paste(big, (round(FX - FX * s), round(GROUND - GROUND * s)), big)
    out = clean(np.array(cv)); ys = np.nonzero((out[..., 3] > 0).any(1))[0]; dy = GROUND - int(ys.max())                # 키운 뒤 반올림으로 뜬 1px 을 다시 맞춘다
    if dy: out = np.roll(out, dy, 0)
    frames[(hit, n)] = (c, Image.fromarray(out)); frames[(hit, n)][1].save(OUT / f'{hit}_{n + 1:02d}_칸{c:02d}.png')
(OUT / 'report.json').write_text(json.dumps({'reference': 'UnarmedBody/idle/F01 x10/3', 'referenceFaceSize': REF, 'cells': report}, ensure_ascii=False, indent=1), encoding='utf-8')
with zipfile.ZipFile(ROOT / '맨손잽_정리본_16장.zip', 'w', zipfile.ZIP_DEFLATED) as zo:
    for p in sorted(OUT.glob('*.png')): zo.write(p, p.name)

# 확인 그림: 게임 맨손 대기와 정리본을 같은 땅 줄 위에
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16)
VIEW = (90, 110, 450, 345); vw, vh = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]; BG = (246, 246, 246, 255)
def view(im):
    w = Image.new('RGBA', im.size, BG); w.alpha_composite(im); return w.convert('RGB').crop(VIEW)
items = [('게임 대기', IDLE)] + [(f'{h} {n + 1}  #{c}', im) for (h, n), (c, im) in frames.items()]
cols = 6; rows = -(-len(items) // cols); sheet = Image.new('RGB', (cols * vw, rows * (vh + 30)), 'white'); d = ImageDraw.Draw(sheet)
for k, (t, im) in enumerate(items):
    x, y = (k % cols) * vw, (k // cols) * (vh + 30); sheet.paste(view(im), (x, y + 30)); d.text((x + 6, y + 4), t, font=F, fill=(0, 110, 60) if k == 0 else (20, 80, 160) if t.startswith('1타') else (170, 40, 40))
    d.line((x, y + 30 + GROUND - VIEW[1], x + vw, y + 30 + GROUND - VIEW[1]), fill=(150, 190, 255)); d.line((x, y + 30 + 150 - VIEW[1], x + vw, y + 30 + 150 - VIEW[1]), fill=(255, 200, 200))
sheet.save(OUT / '정리본-한눈에.png')
# 전체 모션 GIF: 대기 → 1타 → 2타 → 대기 (게임 시간)
seq = [('대기', IDLE, .30)] + [(f'{h} #{c}', frames[(h, n)][1], st['seconds']) for h, steps in SEL['sequence'].items() for n, st in enumerate(steps) for c in [st['cell']]] + [('대기', IDLE, .40)]
def gif(path, mul):
    fr = []; ms = []
    for t, im, s in seq:
        p = Image.new('RGB', (vw * 2, vh * 2 + 34), 'white'); p.paste(view(im).resize((vw * 2, vh * 2), Image.NEAREST), (0, 34)); ImageDraw.Draw(p).text((8, 4), t, font=F, fill=(40, 40, 40)); fr.append(p); ms.append(max(20, int(round(s * 1000 * mul / 10)) * 10))
    fr[0].save(path, save_all=True, append_images=fr[1:], duration=ms, loop=0, disposal=1)
gif(OUT / '정리본-전체모션-게임속도.gif', 1); gif(OUT / '정리본-전체모션-0.5배.gif', 2)
print('ref', round(REF, 2), 'median scale', round(med, 4)); print({c: (r['dy'], r['scale'], r.get('scaleUsed', '')) for c, r in report.items()})
