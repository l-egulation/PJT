# 발사기 스킬 v1 프레임 고르기 + 게임 이펙트 합친 미리보기(2026-11-10). 폭탄 bomb_pick.py 와 같은 틀.
# 에테르 6x6 시트(3840x2160, 칸 640x360), 1유닛 = 106.67 칸px, 발밑 (220,327). 시간은 data/combat/launcher_skills.json 그대로.
# 이펙트는 게임 Resources/LauncherSkillFx(PPU32) 그대로. 배경은 미리보기용으로만 흰색 채우기로 지운다(게임 적용본은 사용자 배경 제거본).
# 총구 자리는 칸마다 몸 덩어리에서 총 높이 띠의 가장 앞(오른쪽) 먹 화소로 잰다(게임 muzzleX/Y 다시 재기의 초안).
import io, json, math, re, sys, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent
SRC = ROOT / '2_에테르결과/aether-history'
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-skill-motion-20261110/unity/Assets/Deviant/Combat/Resources')
SHEETS = {'01': 'v1-20261110/6549e40e-dd4d-46c6-bbe0-5290acb45e3d.png', '02': 'v3-laser-20261110/32ccdbe9-5d17-4b1a-9445-07e6d7650eb9.png',
          '03': 'v1-20261110/7f07d476-839e-4828-93b6-7b422d7f9360.png', '04': 'v1-20261110/20f96eb6-09aa-488c-95c8-01b3b6e9739b.png'}
NAMES = {'01': '01_모아쏘기', '02': '02_레이저', '03': '03_샷건', '04': '04_유탄'}
GUNFORM = {'01': 'launcher', '02': 'launcher_laser', '03': 'launcher_shotgun', '04': 'launcher_grenade'}
PIV, GROUND, CU = (220, 327), 327, 320 / 3
# 시간표 (초, 칸). 모아쏘기는 준비 0~0.2 → 모으는 동안 HOLD 칸 → 뗀 순간부터 REL 시간표(뗀 시각 기준).
TL = {
 '01': [(0, 0), (.05, 2), (.1, 3), (.15, 5)],
 # 02 레이저 v3(총을 허리에 고정): 0.15 발사 → 빔에 밀려 버팀 → 0.6 끝
 '02': [(0, 0), (.05, 3), (.1, 5), (.15, 6), (.2, 8), (.25, 11), (.3, 13), (.35, 16), (.4, 19), (.45, 22), (.5, 26), (.55, 28)],
 '03': [(0, 0), (.05, 3), (.1, 5), (.15, 6), (.2, 9), (.26, 12), (.32, 14), (.38, 16), (.46, 18), (.52, 20), (.58, 22), (.64, 24), (.70, 26), (.75, 28)],
 '04': [(0, 0), (.06, 2), (.13, 4), (.2, 6), (.26, 8), (.32, 10), (.4, 13), (.47, 16), (.54, 18), (.6, 20), (.66, 22), (.72, 24), (.8, 26), (.86, 28), (.93, 30)],
}
HOLD01 = [8, 10, 12, 14]            # 모으는 동안 0.06초씩 돌려 쓰는 칸(거의 같은 버티기 칸)
REL01 = [(0, 18), (.05, 20), (.1, 22), (.15, 24), (.2, 26), (.25, 28)]   # 뗀 뒤 0.3초
END = {'02': .6, '03': .8, '04': 1.0}
AIRGROUND = {'03'}                  # 샷건은 게임이 몸을 띄우므로 공중 칸도 땅에 맞춘다
_sheet = {}
def raw(key, i):
    if key not in _sheet: _sheet[key] = np.array(Image.open(SRC / SHEETS[key]).convert('RGB')).astype(np.int16)
    a = _sheet[key]; return a[(i // 6) * 360:(i // 6 + 1) * 360, (i % 6) * 640:(i % 6 + 1) * 640]
def body_mask(c):
    m = c.min(2) < 236; lab, n = ndi.label(m); sz = ndi.sum(m, lab, range(1, n + 1)); return lab == (1 + int(np.argmax(sz)))
def sole_bottom(key, i):
    c = raw(key, i); body = body_mask(c); dark = c.max(2) < 90
    ys, xs = np.nonzero((dark & body)[200:]); return int(ys.max() + 200) if len(ys) else GROUND
def cut(key, i):
    c = raw(key, i).copy(); dy = GROUND - sole_bottom(key, i)
    c = np.roll(c, dy, 0)
    if dy > 0: c[:dy] = 255
    elif dy < 0: c[dy:] = 255
    white = c.min(2) >= 238
    lab, n = ndi.label(white); edge = set(np.unique(np.r_[lab[0], lab[-1], lab[:, 0], lab[:, -1]])) - {0}
    fg = ~np.isin(lab, list(edge))
    lab2, n2 = ndi.label(fg); sz = ndi.sum(fg, lab2, range(1, n2 + 1)); body = lab2 == (1 + int(np.argmax(sz)))
    near = ndi.binary_dilation(body, iterations=3); keep = np.zeros_like(fg)
    for j in range(1, n2 + 1):
        part = lab2 == j
        if (part & near).any() and part.sum() > 30: keep |= part
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    keep &= ~((r > 230) & (g > 200) & (b < 170) & (r - b > 60))        # 그려 넣은 연노랑 총구 불꽃 지우기
    return np.dstack([c.astype(np.uint8), (keep * 255).astype(np.uint8)])
# 크기: 얼굴(먹 타원, 눈 구멍 메움) 넓이 제곱근을 게임 입력 칸(#0)에 맞춘다(폭탄 때 교훈)
def face_size(a):
    rgb = a[..., :3].astype(int); al = a[..., 3] > 0
    yel = al & (rgb[..., 0] > 200) & (rgb[..., 1] > 130) & (rgb[..., 1] < 200) & (rgb[..., 2] < 90)
    ink = al & (rgb.max(2) < 60)
    lab, n = ndi.label(ink); best = None; bs = 0
    for j in range(1, n + 1):
        part = lab == j; ys, xs = np.nonzero(part)
        if ys.min() > 200 or len(ys) < 400: continue
        h, w = ys.max() - ys.min(), xs.max() - xs.min()
        if not (25 < h < 75 and 25 < w < 75): continue
        filled = ndi.binary_fill_holes(part); sc = (filled & yel).sum()
        if sc > bs: best, bs = filled, sc
    return float(np.sqrt(best.sum())) if best is not None and bs >= 15 else None
_scale = {}
def used(key):
    ids = {i for _, i in TL[key]} | {35}
    if key == '01': ids |= set(HOLD01) | {i for _, i in REL01}
    return sorted(ids)
def scale_of(key, i):
    if key not in _scale:
        ref = face_size(cut(key, 0)); vals = {j: face_size(cut(key, j)) for j in used(key)}
        med = float(np.median([v for v in vals.values() if v]))
        for j in vals:
            if j and vals[j] and abs(vals[j] / med - 1) > .025: vals[j] = None
        known = [j for j in vals if vals[j]]; out = {}
        for j in vals:
            if vals[j]: out[j] = ref / vals[j]
            else:
                lo = max([q for q in known if q < j], default=None); hi = min([q for q in known if q > j], default=None)
                vs = [ref / vals[q] for q in (lo, hi) if q is not None]; out[j] = sum(vs) / len(vs)
        out[0] = 1.0; _scale[key] = out
    return _scale[key].get(i, 1.0)
# 배경 제거본(11-10 사용자가 다른 도구로 해 옴): 옮기기 전 원본 자리의 투명 PNG. 있으면 이걸 쓴다.
BG_ZIP = ROOT / '3_보정·추린프레임' / '발사기_배경제거_외곽색보정_59장.zip'
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]], np.int32)
def clean_l(a):
    """발사기 정리: 알파 128 로 딱 자르고, 작은 조각·구멍을 정리하고, 캐릭터 색만 6색으로 맞춘다.
    총의 무채색 음영(게임 총 그림도 수백 가지 회색)은 그대로 둔다(6색으로 모으면 총이 뭉개진다)."""
    rgb = a[..., :3].astype(np.int32); m = a[..., 3] >= 128
    lab, n = ndi.label(m, np.ones((3, 3)))
    if n: sz = ndi.sum(m, lab, range(1, n + 1)); m &= np.isin(lab, 1 + np.flatnonzero(sz >= 24))
    hl, n = ndi.label(~m)
    if n: sz = ndi.sum(~m, hl, range(1, n + 1)); m |= np.isin(hl, 1 + np.flatnonzero(sz < 24)) & (hl != hl[0, 0])
    _, idx = ndi.distance_transform_edt(~(a[..., 3] >= 128), return_indices=True); rgb = np.where((m & ~(a[..., 3] >= 128))[..., None], rgb[tuple(idx)], rgb)
    grey = (np.abs(rgb[..., 0] - rgb[..., 1]) < 14) & (np.abs(rgb[..., 1] - rgb[..., 2]) < 14) & (rgb.max(-1) < 150)
    k = ((rgb[..., None, :] - PAL) ** 2).sum(-1).argmin(-1)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k[((r - b >= 100) | (g - b >= 55)) & (r > 170) & (g > 110)] = 5
    out = np.zeros_like(a); out[..., :3] = np.where(grey[..., None], rgb, PAL[k]); out[..., 3] = m * 255
    return out
_bgz = None
def cut_ext(key, i):
    global _bgz
    if _bgz is None: _bgz = zipfile.ZipFile(BG_ZIP)
    a = np.array(Image.open(io.BytesIO(_bgz.read(f'{NAMES[key]}/{i:02d}.png'))).convert('RGBA'))
    dy = GROUND - sole_bottom(key, i); a = np.roll(a, dy, 0)
    if dy > 0: a[:dy] = 0
    elif dy < 0: a[dy:] = 0
    fg = a[..., 3] >= 128; lab, n = ndi.label(fg, np.ones((3, 3)))
    if n:
        sz = ndi.sum(fg, lab, range(1, n + 1)); body = lab == (1 + int(np.argmax(sz)))
        near = ndi.binary_dilation(body, iterations=3); keep = np.zeros_like(fg)
        for j in range(1, n + 1):
            part = lab == j
            if (part & near).any() and part.sum() > 30: keep |= part
        a[~ndi.binary_dilation(keep, iterations=1), 3] = 0
    r, g, b = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    a[(r > 230) & (g > 200) & (b < 170) & (r - b > 60), 3] = 0          # 남은 연노랑 총구 불꽃
    return clean_l(a)
_done = {}
def frame(key, i):
    if (key, i) not in _done:
        a = cut_ext(key, i) if BG_ZIP.exists() else cut(key, i); s = scale_of(key, i); im = Image.fromarray(a)
        if abs(s - 1) > .004:
            big = im.resize((round(640 * s), round(360 * s)), Image.LANCZOS); cv = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
            cv.paste(big, (round(220 - 220 * s), round(GROUND - GROUND * s)), big); im = cv
        _done[(key, i)] = im
    return _done[(key, i)]
def muzzle(key, i):
    """총구: 몸 덩어리 중 먹·회색(총) 화소 가운데 가장 앞(오른쪽)인 점. 칸 px."""
    a = np.array(frame(key, i)); al = a[..., 3] > 0; rgb = a[..., :3].astype(int)
    gun = al & (rgb.max(2) < 110) & (np.abs(rgb[..., 0] - rgb[..., 2]) < 30)
    gun[:, :200] = False; gun[:120] = False; gun[300:] = False
    ys, xs = np.nonzero(gun)
    if not len(xs): return (330, 210)
    x = xs.max(); col = ys[xs >= x - 3]; return (int(x), int(np.median(col)))

# ── 미리보기 무대: 1유닛 = 64px
U, W, H, GY = 64, 1100, 300, 270
K = U / CU
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 16)
_clips = {}
def clip(name):
    if name not in _clips:
        c = next(c for c in json.loads((RES / 'LauncherSkillFx/clips.json').read_text(encoding='utf-8'))['clips'] if c['name'] == name); fr = []
        for p in c['frames']:
            im = Image.open(RES / (p + '.png')).convert('RGBA'); m = re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}', (RES / (p + '.png.meta')).read_text(encoding='utf-8'))
            fr.append((im.resize((im.width * 2, im.height * 2), Image.NEAREST), (float(m.group(1)), float(m.group(2)))))
        _clips[name] = dict(c, fr=fr)
    return _clips[name]
def put(c, name, t, x, y, px, rot=0):
    cl = clip(name); secs = cl['seconds']; n = len(secs)
    if t < 0: return False
    i, acc = 0, 0
    total = sum(secs)
    if cl['loop']: t = t % total
    while i < n and t >= acc + secs[i]: acc += secs[i]; i += 1
    if i >= n: return False
    im, (pvx, pvy) = cl['fr'][i]
    if rot:
        im = im.rotate(rot, expand=True, resample=Image.NEAREST); pvx, pvy = .5, .5
    c.alpha_composite(im, (round(px + x * U - pvx * im.width), round(GY - y * U - (1 - pvy) * im.height))); return True
NOW = {}
def now_body(form, t, release):
    cat = json.loads((RES / 'CharacterV2/AnimationCatalog.json').read_text(encoding='utf-8')) if not NOW else None
    if not NOW:
        for s in cat['sets']:
            if s['form'].startswith('launcher'):
                cl = {c['name']: c for c in s['clips']}
                NOW[s['form']] = (cl['idle']['frames'][0], cl['attack_single'])
    idle, att = NOW[form]; imp = att.get('sourceImpact', .1); u = t - (release - imp); name = idle
    if form == 'launcher' and 0 <= t < release: name = att['frames'][1]
    elif u >= 0:
        acc = 0
        for f, s in zip(att['frames'], att['seconds']):
            acc += s
            if u < acc: name = f; break
    im = Image.open(RES / (name + '.png')).convert('RGBA'); im = im.resize((im.width * 2, im.height * 2), Image.NEAREST)
    return im, (round(192 * .4375 * 2), round(160 * (1 - .2375) * 2))
EX = {'01': 6.0, '02': 5.0, '03': 4.5, '04': 6.0}
def body_new(key, t, release):
    if key == '01':
        if t < .2: i = [b for a, b in TL['01'] if t >= a][-1] if t >= 0 else 0
        elif t < release: i = HOLD01[int((t - .2) / .06) % len(HOLD01)]
        elif t < release + .3: i = [b for a, b in REL01 if t - release >= a][-1]
        else: i = 35
    else:
        if t < 0: i = 0
        elif t >= END[key]: i = 35
        else: i = [b for a, b in TL[key] if t >= a][-1]
    return i
def scene(key, t, new, release=None):
    s = Image.new('RGBA', (W, H), (255, 255, 255, 255)); d = ImageDraw.Draw(s); d.line((0, GY, W, GY), fill=(200, 90, 60), width=2)
    PX = 700 if key == '03' else 200
    rel = release if key == '01' else {'02': .15, '03': .15, '04': .2}[key]
    ex = EX[key]
    # 적(상자)
    d.rectangle((PX + (ex - .55) * U, GY - 1.6 * U, PX + (ex + .55) * U, GY), fill=(150, 154, 162))
    # 샷건 반동: 뒤로 6칸, 높이 1칸, 0.312초
    rx = ry = 0
    if key == '03' and t >= .15:
        u = min(1, (t - .15) / .312); rx = -6 * u; ry = 4 * u * (1 - u)
    if new:
        i = body_new(key, t, rel); im = frame(key, i); im2 = im.resize((round(640 * K), round(360 * K)), Image.LANCZOS)
        s.alpha_composite(im2, (round(PX + rx * U - PIV[0] * K), round(GY - ry * U - PIV[1] * K)))
        mz = muzzle(key, i); mzu = ((mz[0] - PIV[0]) / CU + rx, (PIV[1] - mz[1]) / CU + ry)
    else:
        im, piv = now_body(GUNFORM[key], t, rel)
        s.alpha_composite(im, (round(PX + rx * U - piv[0]), round(GY - ry * U - piv[1])))
        mzu = (1.15 + rx, .9 + ry)
    u = t - rel
    if key == '01':
        if .2 <= t < rel:
            step = min(3, 1 + int((t - .2) / .2)); put(s, f'charge_orb{step}', t - .2, mzu[0] + .2, mzu[1], PX); put(s, 'charge_suck', t - .2, mzu[0] + .2, mzu[1], PX)
        if u >= 0:
            step = min(3, 1 + int((rel - .2) / .2)); put(s, 'charge_pop_big', u, mzu[0], mzu[1], PX)
            x0 = .85625; hit_t = (ex - .55 - x0) / 20
            if u < hit_t: put(s, f'charge_shot{step}', u, x0 + 20 * u, 1.0, PX)
            else: put(s, f'charge_burst{step}', u - hit_t, ex - .55, 1.0, PX)
    if key == '02' and 0 <= u < .35:
        if u < .1: put(s, 'laser_pop', u, mzu[0], mzu[1], PX)
        x = mzu[0] + .3; reach = min(W / U, mzu[0] + 60 * max(u, .001))
        while x < reach: put(s, 'laser_beam', u, x, mzu[1], PX); x += .5
        put(s, 'laser_hold', u, mzu[0], mzu[1], PX)
        if reach > ex - .55: put(s, 'laser_hit', u - (ex - .55 - mzu[0]) / 60, ex - .55, mzu[1], PX)
    if key == '03' and u >= 0:
        put(s, 'shotgun_pop', u, mzu[0] if new else 1.03 - 0, mzu[1] if new else 1.12, PX) if u < .2 and new else (put(s, 'shotgun_pop', u, 1.03, 1.12, PX) if u < .2 else None)
        put(s, 'shotgun_dust', u, 0, 0, PX)
        if .05 < u < .312: put(s, 'shotgun_wind', u, rx + .2, ry + 1.0, PX)
        if t >= .462: put(s, 'shotgun_dust', t - .462, rx, 0, PX)
        for a in (-20, -10, 0, 10, 20):
            dd = 20 * u
            hx = 1.0 + math.cos(math.radians(a)) * dd
            if hx >= ex - .55 and abs(1 + math.sin(math.radians(a)) * (ex - .55 - 1) ) < 1.6:
                th = (ex - .55 - 1.0) / (20 * math.cos(math.radians(a)))
                put(s, 'shotgun_hit', u - th, ex - .55, 1 + math.sin(math.radians(a)) * 20 * th, PX)
            elif dd <= 6: put(s, 'shotgun_pellet', u, hx, 1 + math.sin(math.radians(a)) * dd, PX, rot=a)
    if key == '04' and u >= 0:
        if u < .2: put(s, 'grenade_pop', u, mzu[0], mzu[1], PX)
        sx, sy = (mzu[0], mzu[1]) if new else (1.0, 1.16)
        vx, vy = 10 * math.cos(math.radians(30)), 10 * math.sin(math.radians(30)); T = 0
        while T < 3:
            T += 1 / 600; x, y = sx + vx * T, sy + vy * T - 10 * T * T
            if (x > ex - .55 and y < 1.6) or y <= .1: break
        if u < T:
            put(s, 'grenade_shell', u, sx + vx * u, sy + vy * u - 10 * u * u, PX)
            for k in range(1, 6):
                q = u - k * .03
                if q > 0: put(s, 'grenade_smoke', k * .03, sx + vx * q, sy + vy * q - 10 * q * q, PX)
        else:
            w = u - T; put(s, 'grenade_main_air' if y > .3 else 'grenade_main_ground', w, x, y, PX)
            for a in (30, 60, 120, 150):
                fw = min(w, .2); fx = x - .0 + math.cos(math.radians(a)) * 6 * fw; fy = y + math.sin(math.radians(a)) * 6 * fw - 10 * fw * fw
                if w < .2: put(s, 'grenade_frag', w, fx, fy, PX)
                else: put(s, 'grenade_frag_air', w - .2, fx, fy, PX)
    d = ImageDraw.Draw(s)
    d.text((8, 6), ('새 모션' if new else '지금 게임 (평타와 같은 동작)') + f'   {max(0, t):.2f}초', font=F, fill=(30, 30, 30) if new else (120, 120, 120))
    return s
TOTAL = {'01': 1.6, '02': 1.2, '03': 1.4, '04': 2.0}
TITLE = {'01': '모아쏘기 (0.7초까지 모았다 뗌)', '02': '레이저 (빔에 밀려 버팀)', '03': '샷건 (뒤로 6칸 반동 비행)', '04': '유탄 (무릎 꿇고 쏘고 장전)'}
def pair(key, t):
    a, b = scene(key, t, False, .7), scene(key, t, True, .7)
    out = Image.new('RGB', (W, H * 2 + 34), (255, 255, 255)); d = ImageDraw.Draw(out)
    d.text((8, 6), TITLE[key], font=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20), fill=(20, 20, 20))
    out.paste(a.convert('RGB'), (0, 34)); out.paste(b.convert('RGB'), (0, 34 + H)); d.line((0, 34 + H, W, 34 + H), fill=(210, 210, 210), width=2)
    return out
if __name__ == '__main__':
    OUT = ROOT / '4_미리보기'; OUT.mkdir(exist_ok=True)
    for key in (sys.argv[1:] or ['01', '02', '03', '04']):
        for name, step, ms in (('실제속도', .02, 20), ('느리게', .01, 40)):
            fr, t = [], -.2
            while t < TOTAL[key]: fr.append(pair(key, t)); t += step
            fr[0].save(OUT / f'발사기v1-{NAMES[key]}-{name}.gif', save_all=True, append_images=fr[1:], duration=ms, loop=0, optimize=True)
        print(key, len(fr))
    zp = ROOT / '3_보정·추린프레임' / '발사기_정리본.zip'
    with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
        for k in SHEETS:
            for i in used(k):
                buf = io.BytesIO(); frame(k, i).save(buf, 'PNG', optimize=True); z.writestr(f'{NAMES[k]}/{i:02d}.png', buf.getvalue())
    print(zp.name)
    sel = {k: {'sheet': SHEETS[k], 'timeline': TL[k], 'end': END.get(k), 'frames_used': used(k), 'scale': {str(j): round(scale_of(k, j), 4) for j in used(k)},
               'muzzle_cell_px': {str(j): muzzle(k, j) for j in used(k)}} for k in SHEETS}
    sel['01'].update({'hold': HOLD01, 'release': REL01})
    json.dump(sel, open(HERE / 'launcher-v1-selection.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
