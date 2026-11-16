# 폭탄 스킬 v2 프레임 고르기 + 배경 제거·6색 정리 + 게임 이펙트 합친 미리보기(2026-11-10).
# 에테르 6x6 시트(3840x2160, 칸 640x360). 입력 무대 192x108 × 20 을 6분의 1 로 받은 칸 → 1unit(32 무대px) = 106.67 칸px, 발밑 (220,327).
# 시간은 data/combat/ranged_basic_design.json bomb 그대로. 이펙트는 게임 Resources/BombSkillFx(PPU32) 그대로, 미사일은 기존 탄환 몸·발사 펑·꼬리·폭발(사용자 「기존 미사일 써서 합쳐서」).
# 시한폭탄 태엽 = 시계 눈금 고리 A(04_테스트/폭탄 스킬 자세 설명/태엽고리/draw_dial.py fx_clock).
import io, json, math, sys, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent
SRC = ROOT / '2_에테르결과/aether-history/v2-1794269025197'
MAIN = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/main-check-20261109')
RES = MAIN / 'unity/Assets/Deviant/Combat/Resources'
sys.path.insert(0, r'C:/Users/SSAFY/Desktop/자율 프로젝트/04_테스트/폭탄 스킬 자세 설명/태엽고리')
import draw_dial as DIAL
SHEETS = {'01': '51f8b09c-1c14-42f1-8933-8fc30f096734.png', '02': 'e362087b-9d86-4101-99b2-6556d8bf92fc.png',
          '03': '8a7b18a9-8765-40a9-9083-63deb87a0d4e.png', '03b': '1b58dc74-5a2e-4ab6-8f65-04c25710bae4.png', '04': 'cd18a26c-3d9e-4992-b32f-04cfe2dbb352.png'}
NAMES = {'01': '01_3연속차기', '02': '02_대형폭탄', '03': '03_미사일출발', '03b': '03b_미사일복귀', '04': '04_시한폭탄'}
PIV, GROUND, CU = (220, 327), 327, 320 / 3          # 칸 발밑, 땅 줄, 1유닛 칸px
# 시간표 (초, 칸). 끝나면 같은 시트의 대기 칸 #35.
TL = {
 '01': [(0, 0), (.04, 2), (.08, 4), (.12, 6), (.16, 9), (.20, 11), (.27, 12), (.35, 13), (.42, 14), (.50, 15), (.56, 17), (.62, 18), (.68, 19), (.74, 21), (.80, 23), (.86, 27)],
 '02': [(0, 0), (.04, 2), (.08, 4), (.14, 5), (.20, 7), (.26, 9), (.30, 12), (.36, 14), (.44, 16), (.52, 18), (.60, 20), (.68, 22), (.76, 25), (.82, 27), (.87, 29)],
 '03': [(0, 0), (.05, 5), (.10, 9), (.14, 13), (.17, 18)],
 '03b': [(0, 3), (.06, 6), (.12, 10), (.17, 14), (.22, 17), (.30, 19), (.38, 22), (.45, 25), (.50, 28)],
 # 04: 꼼지락 없이 바로 던진다(11-10 사용자). 가슴으로 들어 올려 0.20초에 툭.
 '04': [(0, 0), (.05, 3), (.10, 6), (.15, 14), (.20, 17), (.26, 19), (.34, 21), (.42, 23), (.50, 28), (.56, 30)],
}
END = {'01': .90, '02': .90, '03': .20, '03b': .52, '04': .60}
AIR_KEEP = {'01': set(range(8, 19))}      # 이 칸들은 공중 높이를 살린다(앞뒤 땅 칸의 옮김값을 쓴다). 03b 는 게임이 몸을 띄우므로 모두 땅에 맞춘다.
LOOSE_KEEP = {'03': set(range(0, 36)), '03b': set(range(0, 36))}   # 몸에서 떨어진 조각을 남기는 칸(없으면 다 지운다: 날아가는 폭탄)

_sheet = {}
def raw(key, i):
    if key not in _sheet: _sheet[key] = np.array(Image.open(SRC / SHEETS[key]).convert('RGB')).astype(np.int16)
    a = _sheet[key]; return a[(i // 6) * 360:(i // 6 + 1) * 360, (i % 6) * 640:(i % 6 + 1) * 640]
def body_mask(c):
    m = c.min(2) < 236; lab, n = ndi.label(m); sz = ndi.sum(m, lab, range(1, n + 1)); return lab == (1 + int(np.argmax(sz)))
def sole_bottom(key, i):
    c = raw(key, i); body = body_mask(c); black = c.max(2) < 70
    ys, xs = np.nonzero((black & body)[200:]); return int(ys.max() + 200) if len(ys) else GROUND
def shift_y(key, i):
    air = AIR_KEEP.get(key, set())
    if i not in air: return GROUND - sole_bottom(key, i)
    lo = max([j for j in range(i) if j not in air] or [0]); hi = min([j for j in range(i + 1, 36) if j not in air] or [35])
    return round((shift_y(key, lo) + shift_y(key, hi)) / 2)
def cut(key, i):
    c = raw(key, i).copy(); dy = shift_y(key, i)
    c = np.roll(c, dy, 0)
    if dy > 0: c[:dy] = 255
    elif dy < 0: c[dy:] = 255
    white = c.min(2) >= 238
    lab, n = ndi.label(white); edge = set(np.unique(np.r_[lab[0], lab[-1], lab[:, 0], lab[:, -1]])) - {0}
    fg = ~np.isin(lab, list(edge))
    lab2, n2 = ndi.label(fg); sz = ndi.sum(fg, lab2, range(1, n2 + 1)); body = lab2 == (1 + int(np.argmax(sz)))
    if i in LOOSE_KEEP.get(key, set()): keep = fg
    else:
        near = ndi.binary_dilation(body, iterations=4); keep = np.zeros_like(fg)
        for j in range(1, n2 + 1):
            part = lab2 == j
            if (part & near).any(): keep |= part
    return np.dstack([c.astype(np.uint8), (keep * 255).astype(np.uint8)])

# ── 6색 정리(수리검·단검과 같은 규칙: remaining-skill-motions-20261107/_작업/shuriken_clean.clean)
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]], np.int32)
A_CUT, ISLAND, HOLE, SPECK = 128, 24, 24, 6
def clean(a):
    rgb = a[..., :3].astype(np.int32); m = a[..., 3] >= A_CUT
    lab, n = ndi.label(m, np.ones((3, 3)))
    if n: sz = ndi.sum(m, lab, range(1, n + 1)); m &= np.isin(lab, 1 + np.flatnonzero(sz >= ISLAND))
    hl, n = ndi.label(~m)
    if n: sz = ndi.sum(~m, hl, range(1, n + 1)); m |= np.isin(hl, 1 + np.flatnonzero(sz < HOLE)) & (hl != hl[0, 0])
    k = ((rgb[..., None, :] - PAL) ** 2).sum(-1).argmin(-1)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k[((r - b >= 100) | (g - b >= 55)) & (r > 170) & (g > 110)] = 5
    for _ in range(2):
        changed = 0
        for cc in range(len(PAL)):
            cl, n = ndi.label((k == cc) & m)
            if not n: continue
            sz = ndi.sum(np.ones_like(cl), cl, range(1, n + 1))
            for j in np.flatnonzero(sz < SPECK) + 1:
                piece = cl == j; ring = ndi.binary_dilation(piece, np.ones((3, 3))) & ~piece & m
                if not ring.any(): continue
                k[piece] = np.bincount(k[ring], minlength=len(PAL)).argmax(); changed += 1
        if not changed: break
    cnt = ndi.uniform_filter(m.astype(float), 3) * 9
    m2 = np.where(cnt >= 6, True, np.where(cnt <= 3, False, m))
    _, idx = ndi.distance_transform_edt(~m, return_indices=True); k = np.where(m2 & ~m, k[tuple(idx)], k); m = m2
    votes = np.stack([ndi.uniform_filter(((k == cc) & m).astype(float), 3) for cc in range(len(PAL))])
    best = votes.argmax(0); keep = votes.max(0) * 9 >= 5
    k = np.where(m & keep & (k != 5) & (best != 5), best, k)
    out = np.zeros_like(a); out[..., :3] = PAL[k]; out[..., 3] = m * 255
    return out
# ── 눈 고치기(11-10 사용자 「눈만 고치고 확정」): 찡그리거나 감은 눈을 대기 칸 #0 의 노란 세로 틈 둘로 바꾼다.
# 'auto' = 먹에 둘러싸인 노랑 조각(눈 자국)을 지우고 그 두 무리의 가운데에 본 눈을 찍는다. 좌표 = 얼굴이 가려 자국이 없는 칸의 두 눈 가운데(칸 px).
# 02 #4·#12 는 큰 폭탄·팔이 얼굴을 가려 그대로 둔다.
EYE_FIX = {**{('01', i): 'auto' for i in (4, 6, 18, 19, 21, 23)}, **{('02', i): 'auto' for i in (14, 16, 18, 20, 22, 25)},
           ('02', 5): [(260, 186), (280, 186)], ('03', 9): 'auto', ('03', 13): 'auto',
           **{('03b', i): 'auto' for i in (10, 14, 17, 19, 22)}, ('04', 28): 'auto'}
_YEL = np.array([0xEB, 0xA5, 0x16]); _INK = np.array([0x1B, 0x16, 0x16])
def _eye_template():
    a = clean(cut('01', 0)); rgb = a[..., :3].astype(int)
    yel = (np.abs(rgb - _YEL).sum(-1) < 10) & (a[..., 3] > 0); yel[:150] = False; yel[200:] = False; yel[:, :235] = False; yel[:, 280:] = False
    lab, n = ndi.label(yel); out = []
    for j in range(1, n + 1):
        ys, xs = np.nonzero(lab == j); out.append((ys - int(ys.mean()), xs - int(xs.mean()), xs.mean()))   # 정수 기준으로 옮겨야 칸이 안 겹친다
    out.sort(key=lambda e: e[2]); return [(dy, dx) for dy, dx, _ in out[:2]]
_TPL = None
def fix_eyes(a, spec):
    global _TPL
    if _TPL is None: _TPL = _eye_template()
    a = a.copy(); rgb = a[..., :3].astype(int); al = a[..., 3] > 0
    yel = al & (np.abs(rgb - _YEL).sum(-1) < 10); ink = al & (np.abs(rgb - _INK).sum(-1) < 10)
    if spec == 'auto':
        lab, n = ndi.label(yel); marks = []
        for j in range(1, n + 1):
            part = lab == j; ring = ndi.binary_dilation(part, iterations=2) & ~part
            ys, xs = np.nonzero(part)
            if ring.sum() and (ink & ring).sum() / ring.sum() > .8 and len(ys) < 120 and ys.mean() < 260: marks.append((xs.mean(), ys.mean(), part))
        marks.sort(key=lambda m: m[0])
        for _, _, part in marks: a[part, :3] = _INK
        xs = [m[0] for m in marks]; ys = [m[1] for m in marks]
        gaps = [xs[k + 1] - xs[k] for k in range(len(xs) - 1)]; cut_at = int(np.argmax(gaps)) + 1 if gaps else 1
        L, R = marks[:cut_at], marks[cut_at:]
        if not L or not R: return a
        centers = [(np.mean([m[0] for m in g]), np.mean([m[1] for m in g])) for g in (L, R)]
    else: centers = spec
    (lx, ly), (rx, ry) = centers; mx, my = (lx + rx) / 2, (ly + ry) / 2; half = min(max((rx - lx) / 2, 9), 13)
    centers = [(mx - half, my), (mx + half, my)]
    ink = (np.abs(a[..., :3].astype(int) - _INK).sum(-1) < 10) & (a[..., 3] > 0)
    for (cx, cy), (dy, dx) in zip(centers, _TPL):
        yy, xx = np.round(cy).astype(int) + dy, np.round(cx).astype(int) + dx
        ok = (yy >= 0) & (yy < a.shape[0]) & (xx >= 0) & (xx < a.shape[1])
        yy, xx = yy[ok], xx[ok]; on = ink[yy, xx]
        a[yy[on], xx[on], :3] = _YEL
    return a
# 배경 제거본(11-10 사용자가 다른 도구로 해 옴): 원본 칸(옮기기 전) 그대로의 투명 PNG. 있으면 내 흰색 채우기 대신 이걸 쓴다.
BG_ZIP = ROOT / '3_보정·추린프레임' / '폭탄v2_배경제거_외곽색보정_59장.zip'
_bgz = None
# 크기 맞춤(11-10 사용자 「큰 폭탄 던질 때 커지고 대기로 돌아오면 작아진다, 다른 것도 맞춰」):
# 시트 #0 은 게임 대기 그림을 그대로 키운 입력이고 나머지는 에테르가 그린 칸이라 1~4% 크거나 작다.
# 자세와 상관없이 크기가 같아야 하는 얼굴(먹 타원, 눈 구멍 메움)의 넓이 제곱근을 재서 #0(=게임 크기)에 맞춘다.
# 얼굴이 폭탄·팔에 붙어 못 재는 칸은 앞뒤 칸 값을 시간표 순서로 이어 쓴다. 기준점은 발밑(220, 땅 줄).
FACE_REF = None
_YELc = np.array([0xEB, 0xA5, 0x16]); _INKc = np.array([0x1B, 0x16, 0x16])
def face_size(a):
    rgb = a[..., :3].astype(int); al = a[..., 3] > 0
    yel = al & (np.abs(rgb - _YELc).sum(-1) < 10); ink = al & (np.abs(rgb - _INKc).sum(-1) < 10)
    lab, n = ndi.label(ink); best = None; bs = 0
    for j in range(1, n + 1):
        part = lab == j; ys, xs = np.nonzero(part)
        if ys.min() > 220 or len(ys) < 400: continue
        h, w = ys.max() - ys.min(), xs.max() - xs.min()
        if not (25 < h < 75 and 25 < w < 75): continue
        filled = ndi.binary_fill_holes(part); sc = (filled & yel).sum()
        if sc > bs: best, bs = filled, sc
    return float(np.sqrt(best.sum())) if best is not None and bs >= 20 else None
_scale = {}
def scale_of(key, i):
    global FACE_REF
    if key not in _scale:
        if FACE_REF is None: FACE_REF = face_size(clean(cut_ext('01', 0, use_scale=False)))   # 시트 #0 = 게임 대기 그림 그대로(모든 시트 같다)
        ids = sorted({j for _, j in TL[key]} | ({35} if key != '03' else set())); raw_s = {}
        for j in ids: raw_s[j] = face_size(clean(cut_ext(key, j, use_scale=False)))
        med = float(np.median([v for v in raw_s.values() if v]))
        for j in ids:                                       # 얼굴이 손·폭탄과 붙어 크게 잡힌 칸(시트 중앙값과 2.5% 넘게 다름)은 못 잰 칸으로
            if j != 0 and raw_s[j] and abs(raw_s[j] / med - 1) > .025: raw_s[j] = None
        known = [j for j in ids if raw_s[j]]
        out = {}
        for j in ids:
            if raw_s[j]: out[j] = FACE_REF / raw_s[j]
            else:
                lo = max([q for q in known if q < j], default=None); hi = min([q for q in known if q > j], default=None)
                vs = [FACE_REF / raw_s[q] for q in (lo, hi) if q is not None]; out[j] = sum(vs) / len(vs)
        _scale[key] = {j: (1.0 if j == 0 else min(1.08, max(.92, s))) for j, s in out.items()}   # #0 은 게임 그림 그대로
        # 몸 키 보정: 얼굴로 맞춘 뒤에도 끝 대기 칸(#35)의 키가 시작 대기 칸(#0, 게임 그림)과 다르면 시트 전체(#0 빼고)에 그 비만큼 더 곱한다.
        # (시한폭탄 시트는 얼굴보다 몸이 더 작게 그려져 얼굴만 맞추면 2% 작았다)
        if 35 in _scale[key]:
            def height(a): ys = np.nonzero(a[..., 3] > 0)[0]; return ys.max() - ys.min() + 1
            h0 = height(clean(cut_ext('01', 0, use_scale=False)))
            h35 = height(clean(cut_ext(key, 35, use_scale=True)))
            extra = h0 / h35
            if abs(extra - 1) > .004: _scale[key] = {j: (s if j == 0 else s * extra) for j, s in _scale[key].items()}
    return _scale[key].get(i, 1.0)
def cut_ext(key, i, use_scale=True):
    global _bgz
    if _bgz is None: _bgz = zipfile.ZipFile(BG_ZIP)
    a = np.array(Image.open(io.BytesIO(_bgz.read(f'{NAMES[key]}/{i:02d}.png'))).convert('RGBA'))
    dy = shift_y(key, i)
    s = scale_of(key, i) if use_scale else 1.0
    if abs(s - 1) > .004:                                   # 발밑(220, 이 칸의 땅 줄)을 기준으로 키우거나 줄인다
        im = Image.fromarray(a); W0, H0 = im.size; gy = GROUND - dy
        big = im.resize((round(W0 * s), round(H0 * s)), Image.LANCZOS)
        canvas = Image.new('RGBA', (W0, H0), (0, 0, 0, 0)); canvas.paste(big, (round(220 - 220 * s), round(gy - gy * s)), big)
        a = np.array(canvas)
    a = np.roll(a, dy, 0)
    if dy > 0: a[:dy] = 0
    elif dy < 0: a[dy:] = 0
    fg = a[..., 3] >= A_CUT
    if i not in LOOSE_KEEP.get(key, set()):                       # 몸에서 떨어진 조각(날아가는 폭탄) 지우기
        lab, n = ndi.label(fg, np.ones((3, 3)))
        if n:
            sz = ndi.sum(fg, lab, range(1, n + 1)); body = lab == (1 + int(np.argmax(sz)))
            near = ndi.binary_dilation(body, iterations=4); keep = np.zeros_like(fg)
            for j in range(1, n + 1):
                part = lab == j
                if (part & near).any(): keep |= part
            a[~ndi.binary_dilation(keep, iterations=1), 3] = 0
    return a
USE_EYE_FIX = False   # 11-10 사용자 「고치지 말자, 게임에선 잘 안 보여」 → 눈은 에테르 그대로. 고치는 코드는 남겨 둔다.
_done = {}
def frame(key, i):
    if (key, i) not in _done:
        a = clean(cut_ext(key, i) if BG_ZIP.exists() else cut(key, i))
        if USE_EYE_FIX and (key, i) in EYE_FIX: a = fix_eyes(a, EYE_FIX[(key, i)])
        _done[(key, i)] = Image.fromarray(a)
    return _done[(key, i)]

# ── 미리보기 무대: 1유닛 = 64px
U, W, H = 64, 960, 300; GY = 270; PX = 120
K = U / CU                                            # 칸 → 미리보기 배율 0.6
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 16)
def load_clip(name):
    c = next(c for c in json.loads((RES / 'BombSkillFx/clips.json').read_text(encoding='utf-8'))['clips'] if c['name'] == name)
    fr = []
    for p in c['frames']:
        im = Image.open(RES / (p + '.png')).convert('RGBA'); meta = (RES / (p + '.png.meta')).read_text(encoding='utf-8')
        import re
        mm = re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}', meta); piv = (float(mm.group(1)), float(mm.group(2)))
        fr.append((im.resize((im.width * 2, im.height * 2), Image.NEAREST), piv))
    return dict(c, fr=fr)
CL = {n: load_clip(n) for n in ('triple_0', 'triple_1', 'triple_2', 'range', 'timed_fuse', 'timed', 'missile_launch', 'missile_body', 'missile_trail', 'missile_hit')}
def put_clip(c, name, t, x, y, facing=1, loop=False):
    """x,y 는 유닛(세계), 클립 피벗을 그 자리에."""
    cl = CL[name]; n = len(cl['fr']); i = int(t * cl['fps'])
    if loop: i %= n
    if t < 0 or i >= n: return False
    im, (px, py) = cl['fr'][i]
    if facing < 0: im = im.transpose(Image.FLIP_LEFT_RIGHT); px = 1 - px
    c.alpha_composite(im, (round(PX + x * U - px * im.width), round(GY - y * U - (1 - py) * im.height))); return True
MOB = Image.open(MAIN / 'unity/Assets/Deviant/Stage1Monsters/Art/01_thorn-root/idle/00.png').convert('RGBA')
MOB = MOB.resize((MOB.width * 2, MOB.height * 2), Image.NEAREST)
def draw_mob(c, ex, shake):
    c.alpha_composite(MOB, (round(PX + ex * U - MOB.width / 2 + shake), GY - MOB.height + 8))
def bomb(d, x, y, dia):
    cx, cy, r = PX + x * U, GY - y * U, dia / 2 * U
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(27, 22, 22)); d.rectangle((cx - r * .35, cy - r - 3, cx + r * .35, cy - r + 1), fill=(235, 165, 22))
    d.line((cx, cy - r - 3, cx + r * .5, cy - r - 8), fill=(27, 22, 22), width=2)

SPAWN = (.5, 1.0); G = 20
def flight(v, ang, dia, ex, hw, eh, pass_enemy=False):
    vx, vy = v * math.cos(math.radians(ang)), v * math.sin(math.radians(ang)); t = 0; r = dia / 2
    while t < 5:
        t += 1 / 600; x, y = SPAWN[0] + vx * t, SPAWN[1] + vy * t - G / 2 * t * t
        if not pass_enemy and x + r > ex - hw and y - r < eh: return t, x, y, 'enemy'
        if y - r <= 0: return t, x, r, 'ground'
    return t, x, y, 'air'
def fly_at(v, ang, u): vx, vy = v * math.cos(math.radians(ang)), v * math.sin(math.radians(ang)); return SPAWN[0] + vx * u, SPAWN[1] + vy * u - G / 2 * u * u

NOW = [(f'F{n:03d}', s) for n, s in zip(range(51, 60), (.07, .065, .075, .095, .045, .1, .07, .095, .15))]
_g = {}
def game_frame(name):
    if name not in _g:
        im = Image.open(RES / f'CharacterApproved13/bomb/{name}.png').convert('RGBA'); _g[name] = im.resize((256, 256), Image.NEAREST)
    return _g[name]
def body_now(t):
    if t < 0: return game_frame('F000')
    acc = 0
    for n, s in NOW:
        acc += s
        if t < acc: return game_frame(n)
    return game_frame('F000')
def put_now(c, x, y, t): im = body_now(t); c.alpha_composite(im, (round(PX + x * U - 128), round(GY - y * U - 216)))
def put_new(c, key, x, y, t):
    tl = TL[key]
    if t < 0: i = tl[0][1]
    elif t >= END[key]: i = 35
    else:
        i = tl[0][1]
        for a, b in tl:
            if t >= a: i = b
    im = frame(key, i); im = im.resize((round(im.width * K), round(im.height * K)), Image.LANCZOS)
    c.alpha_composite(im, (round(PX + x * U - PIV[0] * K), round(GY - y * U - PIV[1] * K))); return i

def bomb_clock(c, x, y, w, life=1.5):
    """땅에 멈춘 폭탄 둘레 시계(게임 화소 PPU32 로 그려 2배). 반지름 0.7유닛, 바늘은 0.125초마다 30도 → 1.5초에 한 바퀴 돌면 터진다.
    처음 0.06초는 0.75배로 톡 나타난다. 노란 고리, 큰 눈금 넷은 먹, 바늘은 고리 바깥 테의 먹 쐐기."""
    im = clock_image(w, life); S2 = im.width
    big = im.resize((S2 * 2, S2 * 2), Image.NEAREST)
    c.alpha_composite(big, (round(PX + x * U - S2), round(GY - y * U - S2)))
def clock_image(w, life=1.5):
    """시계 한 장(게임 화소 PPU32, 64x64, 가운데가 원점). 게임 굽기(register_bomb_resources)도 이걸 쓴다."""
    S2 = 64; im = Image.new('RGBA', (S2, S2), (0, 0, 0, 0)); cx = cy = S2 // 2
    r = round(22 * (.75 if w < .06 else 1)); YEL, INK = (235, 165, 22, 255), (27, 22, 22, 255)
    def put(px, py, col):
        if 0 <= px < S2 and 0 <= py < S2: im.putpixel((int(px), int(py)), col)
    def bar(a, r0, r1, half, col):
        for rr in range(r0, r1 + 1):
            for s in range(-half, half + 1): put(round(cx + math.cos(a) * rr - math.sin(a) * s), round(cy + math.sin(a) * rr + math.cos(a) * s), col)
    step = int(w / (life / 12)); sweep = step * math.pi / 6
    for a10 in range(0, 3600, 4):
        a = math.radians(a10 / 10)
        for k in range(2): put(round(cx + math.cos(a) * (r - k)), round(cy + math.sin(a) * (r - k)), YEL)
    for i in range(12):
        a = -math.pi / 2 + i / 12 * 2 * math.pi
        if i % 3 == 0: bar(a, r - 5, r - 2, 1, INK)
        else: bar(a, r - 3, r - 2, 0, YEL)
    h = -math.pi / 2 + sweep
    for k in range(6): bar(h, r + 4 - k, r + 4 - k, min(2, k // 2), INK)
    for x0 in range(cx - 2, cx + 3):
        for y0 in range(cy - r - 4, cy - r - 1): put(x0, y0, YEL if y0 > cy - r - 4 else INK)
    return im
ENEMY = {'01': 4.5, '02': 4.5, '03': 8.0, '04': 4.5}
def scene(key, t, new):
    c = Image.new('RGBA', (W, H), (255, 255, 255, 255)); d = ImageDraw.Draw(c)
    d.line((0, GY, W, GY), fill=(200, 90, 60), width=2)
    ex = ENEMY[key]; hw, eh = .55, 1.6; shake = 0; label = ''
    if key == '01':
        rel = [.20, .35, .50]
        for j, r0 in enumerate(rel):
            u = t - r0
            if u < 0:
                if new and t >= .08:            # 새 모션: 띄운 폭탄 셋이 차일 때까지 떠 있다(코드로 그리는 판)
                    q = (t - .08) / (r0 - .08); P0, P2 = (.42, 1.1), SPAWN; A = (.55 + .05 * j, 2.0 + .35 * j)
                    C1 = (2 * A[0] - (P0[0] + P2[0]) / 2, 2 * A[1] - (P0[1] + P2[1]) / 2)
                    bx = (1 - q) ** 2 * P0[0] + 2 * (1 - q) * q * C1[0] + q * q * P2[0]; by = (1 - q) ** 2 * P0[1] + 2 * (1 - q) * q * C1[1] + q * q * P2[1]
                    bomb(d, bx, by, .4)
                continue
            ft, fx, fy, what = flight(15, 30, .4, ex, hw, eh)
            if u < ft: bomb(d, *fly_at(15, 30, u), .4)
            else:
                if u - ft < .1 and what == 'enemy': shake = 3 if int(t * 50) % 2 else -3
                put_clip(c, f'triple_{j}', u - ft, fx, fy)
    elif key == '02':
        r0 = .30; u = t - r0
        if u >= 0:
            ft, fx, fy, what = flight(8, 30, .8, ex, hw, eh)
            if u < ft: bomb(d, *fly_at(8, 30, u), .8)
            else:
                if u - ft < .12: shake = 4 if int(t * 50) % 2 else -4
                put_clip(c, 'range', u - ft, fx, 0)
    elif key == '04':
        r0 = .20; u = t - r0
        if u >= 0:
            ft, fx, fy, what = flight(10, 30, .4, ex, hw, eh, pass_enemy=True)
            if u < ft: bomb(d, *fly_at(10, 30, u), .4)
            elif u < ft + 1.5:
                bomb(d, fx, .2, .4); put_clip(c, 'timed_fuse', u - ft, fx, 0)
                if new: bomb_clock(c, fx, .72, u - ft)          # 고리 밑이 땅에 닿게(반지름 0.7) 폭탄을 감싼다
            else:
                if u - ft - 1.5 < .15: shake = 4 if int(t * 50) % 2 else -4
                put_clip(c, 'timed', u - ft - 1.5, fx, 0)
    draw_mob(c, ex, shake)
    # 몸
    if key == '03':
        x0, cy = .3, 1.0; xc = ex - hw - .35; te = .2 + (xc - x0) / 60
        if t < .2:
            (put_new(c, '03', 0, 0, t) if new else put_now(c, 0, 0, t))
        elif t < te:
            x = x0 + 60 * (t - .2); put_clip(c, 'missile_launch', t - .2, x0, cy)
            seg = x0
            while x - seg >= 2: seg += 2; put_clip(c, 'missile_trail', t - .2 - (seg - x0) / 60, seg, cy)
            put_clip(c, 'missile_body', t - .2, x, cy, loop=True)
        else:
            u = t - te; seg = x0
            while xc - seg >= 2: seg += 2; put_clip(c, 'missile_trail', t - .2 - (seg - x0) / 60, seg, cy)
            put_clip(c, 'missile_launch', t - .2, x0, cy)
            if u < .12: shake = 0
            dy = .5 * (1 - (1 - u / .122) ** 2) if u < .122 else (.5 * (1 - ((u - .122) / .098) ** 2) if u < .22 else 0)
            dx = -.6 * min(1, u / .22); bx = xc - .3 + dx
            if new: put_new(c, '03b', bx, max(0, dy), u)
            else:   # 지금 게임: 점프 그림 F020·F021·F022·F024 → 착지 F026~28
                seq = [(0, 'F020'), (.06, 'F021'), (.122, 'F022'), (.17, 'F024'), (.22, 'F026'), (.3, 'F027'), (.4, 'F028'), (.52, 'F000')]
                nm = seq[0][1]
                for a, b in seq:
                    if u >= a: nm = b
                im = game_frame(nm); c.alpha_composite(im, (round(PX + bx * U - 128), round(GY - max(0, dy) * U - 216)))
            put_clip(c, 'missile_hit', u, xc + .35, cy)
    else:
        (put_new(c, key, 0, 0, t) if new else put_now(c, 0, 0, t))
    d = ImageDraw.Draw(c)
    d.text((8, 6), ('새 모션' if new else '지금 게임 (평타 던지기)') + f'   {max(0, t):.2f}초', font=F, fill=(30, 30, 30) if new else (120, 120, 120))
    return c
TOTAL = {'01': 1.5, '02': 1.6, '03': 1.2, '04': 2.9}
TITLE = {'01': '폭탄 3연속 차기', '02': '대형 폭탄', '03': '미사일 변신 (기존 미사일 이펙트)', '04': '착지 시한폭탄 (바로 던지고 폭탄 둘레 시계 한 바퀴)'}
def pair(key, t):
    a, b = scene(key, t, False), scene(key, t, True)
    out = Image.new('RGB', (W, H * 2 + 34), (255, 255, 255)); d = ImageDraw.Draw(out)
    d.text((8, 6), TITLE[key], font=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20), fill=(20, 20, 20))
    out.paste(a.convert('RGB'), (0, 34)); out.paste(b.convert('RGB'), (0, 34 + H)); d.line((0, 34 + H, W, 34 + H), fill=(210, 210, 210), width=2)
    return out
if __name__ == '__main__':
    OUT = ROOT / '4_미리보기'; OUT.mkdir(exist_ok=True)
    keys = sys.argv[1:] or ['01', '02', '03', '04']
    for key in keys:
        for name, step, ms in (('실제속도', .02, 20), ('느리게', .01, 40)):
            fr, t = [], -.2
            while t < TOTAL[key]: fr.append(pair(key, t)); t += step
            fr[0].save(OUT / f'폭탄v2-{NAMES[key]}-{name}.gif', save_all=True, append_images=fr[1:], duration=ms, loop=0, optimize=True)
        print(key, len(fr))
    # 정리본 묶음(고른 칸만, 640x360, 발밑 (220,327), 6색)
    used = {k: sorted({i for _, i in TL[k]} | ({35} if k != '03' else set())) for k in TL}
    zp = ROOT / '3_보정·추린프레임' / '폭탄v2_6색정리.zip'; zp.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
        for k, ids in used.items():
            for i in ids:
                buf = io.BytesIO(); frame(k, i).save(buf, 'PNG', optimize=True); z.writestr(f'{NAMES[k]}/{i:02d}.png', buf.getvalue())
    json.dump({k: {'sheet': SHEETS[k], 'timeline': TL[k], 'end': END[k], 'frames_used': used[k]} for k in TL},
              open(HERE / 'bomb-v2-selection.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(zp.name)
