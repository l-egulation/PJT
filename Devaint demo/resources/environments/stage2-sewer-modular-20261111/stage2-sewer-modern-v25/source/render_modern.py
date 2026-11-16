# 2스테이지 지하수로 현대화 시안. 맵 형태·캐릭터·채택 타일 원본은 바꾸지 않고 읽기만 한다.
# B안(배경 교체 + 땅 속은 어두운 바위, 테두리 2~3줄만 채택 v21 벽돌, 통과 발판은 같은 램프의 돌판). build.py 가 호출한다.
from pathlib import Path
import json, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

HERE = Path(__file__).resolve().parent
SET = HERE.parent                      # stage2-sewer-modern-v25
PKG = SET.parent                       # stage2-sewer-modular-20261111
V21 = PKG / 'stage2-unified-stone-v21'
layout = json.loads((PKG / 'source/chapter2-layouts.json').read_text(encoding='utf-8'))['layouts'][0]
views = json.loads((PKG / 'stage2-room-background-v24/source/foreground-manifest.json').read_text(encoding='utf-8'))['views']
hero = Image.open(PKG / 'source/approved-player.png').convert('RGBA'); hero = hero.crop(hero.getbbox())
W, H, PAD = 960, 540, 96
WW, HH = W + 2 * PAD, H + 2 * PAD

BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + .5) / 16

def rgb(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], dtype=np.float32)
# 차가운 쪽은 남색→청록→민트로 색상이 돈다. 따뜻한 쪽은 같은 단계의 호박색.
COOL = np.stack([rgb(c) for c in ['#070b0e', '#0b1216', '#0f191e', '#142228', '#1a2c31', '#21373a', '#2a4443', '#36534e', '#46655a', '#5a7b69', '#75967d']])
WARM = np.stack([rgb(c) for c in ['#0b0b0d', '#130f10', '#1c1513', '#281c16', '#36261a', '#46301e', '#5b3d22', '#764e28', '#94632f', '#b77d3a', '#dba04c']])
INK = rgb('#06090b')
ROCK = np.stack([rgb(c) for c in ['#07080c', '#0e1017', '#141720', '#1b1f2a', '#232837', '#2d3344']])

def hsh(*a):
    v = 2166136261
    for x in a:
        v = ((v ^ (int(x) & 0xffffffff)) * 16777619) & 0xffffffff
    return v

def hshv(a):
    a = (a.astype(np.int64) * 2654435761) & 0xffffffff
    a ^= a >> 15; a = (a * 2246822519) & 0xffffffff; a ^= a >> 13
    return a

def ramp(level, warm, wx, wy):
    """level 0..10 실수, warm 0..1. 베이어 디더로 단계 사이를 1px 점으로 섞는다."""
    t = .5 + (BAYER[wy % 4, wx % 4] - .5) * .3   # 경계 근처에만 점이 생기게 좁힌다
    li = np.clip(np.floor(level + t - .5).astype(int), 0, 10)
    ws = np.clip(np.floor(warm * 3 + BAYER[wy % 4, wx % 4]).astype(int), 0, 3) / 3.0
    return COOL[li] * (1 - ws[..., None]) + WARM[li] * ws[..., None]

def solid_mask(left, top):
    mk = Image.new('1', (WW, HH)); d = ImageDraw.Draw(mk)
    for s in layout['surfaces']:
        if s['oneWay']: continue
        x = round(s['x'] * 32) - left; y = top - round((s['y'] + s['h']) * 32)
        x2 = round((s['x'] + s['w']) * 32) - left; y2 = top - round(s['y'] * 32)
        if x2 > x and y2 > y: d.rectangle((x, y, x2 - 1, y2 - 1), fill=1)
    return np.array(mk, dtype=bool)

def oneways(left, top, m):
    out = []
    for s in layout['surfaces']:
        if not s['oneWay']: continue
        L = round(s['w'] * 32); x = round(s['x'] * 32) - left; y = top - round((s['y'] + s['h']) * 32)
        if x > WW or x + L < 0 or y > HH or y + 12 < 0: continue
        lw = 0 <= x - 1 < WW and 0 <= y + 3 < HH and bool(m[y + 3, x - 1])
        rw = 0 <= x + L < WW and 0 <= y + 3 < HH and bool(m[y + 3, x + L])
        out.append((x, y, L, lw, rw))
    return out

def pick(i, n, lw, rw):
    if n == 1: return 'single-both-wall' if lw and rw else 'single-left-wall' if lw else 'single-right-wall' if rw else 'single'
    if i == 0: return 'left-wall' if lw else 'left-free'
    if i == n - 1: return 'right-wall' if rw else 'right-free'
    return ['middle-a', 'middle-b', 'middle-c'][(i - 1) % 3]

def deck(width, lw, rw):
    n = math.ceil(width / 32); out = Image.new('RGBA', (width, 32))
    for i in range(n):
        im = Image.open(V21 / f'runtime/full/{pick(i, n, lw, rw)}.png').convert('RGBA'); remain = min(32, width - i * 32)
        if remain < 32:
            cut = Image.new('RGBA', (remain, 32)); h = remain // 2
            cut.alpha_composite(im.crop((0, 0, h, 32))); cut.alpha_composite(im.crop((32 - (remain - h), 0, 32, 32)), (h, 0)); im = cut
        out.alpha_composite(im, (i * 32, 0))
    return out

# ---------------------------------------------------------------- 조명
def find_lights(left, top, m):
    """바닥 위 벽등, 천장엔 매단 등과 채광 격자를 번갈아. 월드 좌표에 고정."""
    lamps, shafts = [], []
    tops = m & ~np.roll(m, 1, axis=0)
    bots = m & ~np.roll(m, -1, axis=0)
    for mask_, kind in [(tops, 'top'), (bots, 'bot')]:
        lab, _ = ndimage.label(mask_)
        for sl in ndimage.find_objects(lab):
            ys, xs = sl; y = ys.start; x0, x1 = xs.start, xs.stop
            if x1 - x0 < 96: continue
            step = 256 if kind == 'top' else 176
            base = (y + top) * 7
            for wx in range(((x0 + left) // step + 1) * step, x1 + left - 24, step):
                sx = wx - left
                if kind == 'top':
                    ly = y - 88
                    if ly < 4 or m[max(0, ly - 30):ly + 30, max(0, sx - 10):sx + 10].any(): continue
                    if any(abs(sx - a) < 220 and abs(ly - b) < 140 for a, b, _ in lamps): continue
                    lamps.append((sx, ly, None))
                else:
                    if y + 150 >= HH or m[y + 2:y + 130, max(0, sx - 20):sx + 20].any(): continue
                    slot = (wx // step + base) % 3
                    if slot == 0:
                        shafts.append((sx, y + 1))
                    elif slot == 1:
                        ly = y + 44 + (wx // step) % 3 * 10
                        if any(abs(sx - a) < 220 and abs(ly - b) < 140 for a, b, _ in lamps): continue
                        lamps.append((sx, ly, y + 1))
    return lamps, shafts

def find_water(left, top, m, lamps, view_i, hero_x=None):
    """뒷벽 배수구에서 떨어지는 물줄기 한 개. 등·캐릭터와 떨어진, 가장 넓은 바닥 위."""
    tops = m & ~np.roll(m, 1, axis=0); lab, _ = ndimage.label(tops)
    cands = []
    for sl in ndimage.find_objects(lab):
        ys, xs = sl; y = ys.start; x0, x1 = max(xs.start, PAD + 24), min(xs.stop, PAD + W - 24)
        if x1 - x0 < 96 or y < PAD + 140: continue
        for sx in range(x0 + 24, x1 - 24, 16):
            oy = y - 118
            if oy < PAD or m[oy - 20:y - 1, max(0, sx - 30):sx + 30].any(): continue
            dl = min([abs(sx - a) + abs(oy - b) * .3 for a, b, _ in lamps] or [999])
            dh = abs(sx - hero_x) if hero_x is not None else 999
            if dl < 90 or dh < 90: continue
            score = min(dl, 260) + min(dh, 260) + (hsh(sx + left, 13) % 40)
            cands.append((score, (sx, oy, y)))
    return [c[1] for c in sorted(cands, reverse=True)]

def light_maps(lamps, shafts):
    yy, xx = np.mgrid[0:HH, 0:WW].astype(np.float32)
    L = np.zeros((HH, WW), np.float32); Wm = np.zeros((HH, WW), np.float32)
    for (lx, ly, _) in lamps:
        d = np.hypot(xx - lx, (yy - ly) * 1.15)
        g = np.clip(1 - d / 210, 0, 1) ** 1.9
        L += g * 3.8; Wm = np.maximum(Wm, np.clip(1 - d / 150, 0, 1) ** 1.6)
    for (sx, sy) in shafts:
        dy = yy - sy; cxl = sx + dy * .22  # 살짝 기운 빛줄기
        half = 14 + dy * .09
        inside = (dy > 0) & (np.abs(xx - cxl) < half)
        fall = np.clip(1 - dy / 420, 0, 1)
        edge = np.clip((half - np.abs(xx - cxl)) / 6, 0, 1)
        L += inside * fall * edge * 2.3
        d = np.hypot(xx - sx, (yy - sy) * 1.6); L += np.clip(1 - d / 60, 0, 1) * 1.5
    return L, Wm

# ---------------------------------------------------------------- 뒷벽
def back_wall(left, top, L, Wm, m, view_i):
    yy, xx = np.mgrid[0:HH, 0:WW]; wx = xx + left; wy = top - yy  # wy 위로 증가
    # 큰 마름돌 64x32, 2px 줄눈. 지형 벽돌(32x16)과 일부러 크기를 달리해 땅과 벽이 섞이지 않게.
    row = np.floor_divide(-wy, 32); off = (row % 2) * 32
    bx = np.floor_divide(wx + off, 64); bxp = (wx + off) % 64; byp = (-wy) % 32
    mortar = (bxp < 2) | (byp < 2)
    bvar = np.vectorize(lambda a, b: hsh(a, b, 7) % 7)(bx, row) if False else ((bx * 73856093 ^ row * 19349663) & 0xff) % 7
    face = 3.2 + (bvar - 3) * .14
    lit_edge = (byp == 2) & ~mortar                    # 돌 윗변 1px 밝게
    shade_edge = ((byp == 31) | (bxp == 63)) & ~mortar  # 아랫변·오른변 1px 어둡게
    lvl = np.where(mortar, 2.1, face) + lit_edge * .7 - shade_edge * .5
    # 돌 몇 개는 깨져 빠지고 이끼가 낀다
    broken = (((bx * 2654435761 ^ row * 40503) >> 5) & 31) == 0
    lvl = np.where(broken & ~mortar, 1.5, lvl)
    # 위에서 흘러내린 물때: 세로 줄무늬, 월드 x 마다 고정
    streak_col = ((wx // 3) * 2246822519 & 0xffff) % 23 == 0
    lvl = lvl - streak_col * .45
    # 화면 위쪽은 더 어둡게(천장 쪽 깊이감)
    depth = np.clip((yy - PAD) / H, 0, 1)
    lvl = lvl - (1 - depth) * 1.1
    lvl = lvl + np.minimum(L, 6.5)
    img = ramp(lvl, Wm, wx, -wy)
    return img, lvl

def pipe_y(view_i, top):
    pipe_wy = [5.25, 6.0, 4.75, 7.0, 5.5, 6.25, 4.5, 6.5][view_i]
    return top - round((views[view_i]['heroFeet'][1] + pipe_wy) * 32)

DOOR_SIZES = [(76, 200), (60, 160), (46, 124)]   # (아치 반지름, 문 높이). 테는 반지름+10

def door_supported(m, cx, fy, half):
    """문 테 바깥쪽+4px 까지 전부 같은 높이 바닥 위에 서 있어야 한다."""
    x0, x1 = cx - half, cx + half
    if x0 < 0 or x1 >= m.shape[1] or fy < 1: return False
    return bool(m[fy, x0:x1 + 1].all() and not m[fy - 1, x0:x1 + 1].any())

def find_tunnels(left, m, top=None):
    """문(터널 아치) 자리: 기둥 사이 칸마다(바로 옆 칸 연속 금지). 문 폭 전체가 바닥 위에 있고,
    위·옆이 땅에 잘리지 않고, 통과 발판이 지나가지 않을 때만. 큰 문부터 맞춰 보고 안 되면 작은 문,
    기둥 받침에 닿지 않는 범위에서 좌우로 옮겨 본다. 다 안 되면 두지 않는다."""
    out = []
    decks = oneways(left, top, m) if top is not None else []
    prev = False
    for bay in range(((left - 400) // 288), (left + WW) // 288 + 2):
        if prev: prev = False; continue
        c0 = bay * 288 + 144 + 14 - left
        placed = None
        for (r, h) in DOOR_SIZES:
            lim = 284 - (r + 14) - 158          # 칸 가운데(158)에서 기둥 받침까지 남는 폭
            shifts = [0] + [k * sgn for k in range(8, lim + 1, 8) for sgn in (-1, 1)]
            for shift in shifts:
                cx = c0 + shift
                if not (r + 20 < cx < WW - r - 20): continue
                colm = m[:, cx]
                ys = np.where(colm & ~np.roll(colm, 1))[0]
                ys = ys[(ys > PAD + 100) & (ys < HH - 8)]
                if len(ys) == 0 or ys[-1] - h - 14 < 0: continue
                fy = int(ys[-1])
                if not door_supported(m, cx, fy, r + 14): continue
                if m[fy - h - 14:fy, cx - r - 20:cx + r + 21].any(): continue
                if any(x + Lw >= cx - r - 18 and x <= cx + r + 18 and y + 12 >= fy - h - 18 and y <= fy for (x, y, Lw, _a, _b) in decks): continue
                placed = (cx, fy, r, h); break
            if placed: break
        if placed: out.append(placed); prev = True
    return out

def hits_door(doors, x0, y0, x1, y1, margin=24):
    """사각형이 문 테 바깥 여유 margin(24px) 안으로 들어오면 True."""
    for (cx, fy, r, h) in doors:
        if x1 >= cx - r - 10 - margin and x0 <= cx + r + 10 + margin and y1 >= fy - h - 10 - margin and y0 <= fy:
            return True
    return False

def draw_props(img_lvl, left, top, m, view_i, lamps, shafts, L, Wm):
    """벽 기둥·관·터널 입구. 레벨 배열에 직접 그려 조명과 같은 램프를 쓴다."""
    lvl = img_lvl
    yy, xx = np.mgrid[0:HH, 0:WW]; wx = xx + left
    tunnels = []; tmask = np.zeros(lvl.shape, bool)
    # 기둥: 월드 x 288 간격, 폭 28. 밝은 왼변·어두운 오른변.
    for px in range(((left - 400) // 288) * 288, left + WW + 288, 288):
        sx = px - left
        if sx + 28 < 0 or sx > WW: continue
        a, b = max(0, sx), min(WW, sx + 28)
        col = lvl[:, a:b]
        rel = np.arange(a, b) - sx
        shape = 3.0 + (rel < 3) * .9 - (rel > 24) * .7 + ((np.arange(HH)[:, None] + top) % 64 == 0) * -.6
        lvl[:, a:b] = shape + np.minimum(L[:, a:b], 6.5)
        # 기둥이 벽에 드리우는 그림자 6px
        c, e = min(WW, b), min(WW, b + 6)
        lvl[:, c:e] -= .8
    # 터널 입구: 기둥 사이 칸 중 일부. 안쪽은 깊은 어둠 + 먼 아치 실루엣 + 물빛 한 줄
    for (cx, fy, r, h) in find_tunnels(left, m, top):
        arch_top = fy - h
        dx = xx - cx; inside = (np.abs(dx) < r) & (yy < fy) & ((yy > arch_top + r) | (np.hypot(dx, yy - (arch_top + r)) < r))
        ring = (np.abs(dx) < r + 10) & (yy < fy) & ((yy > arch_top + r) | (np.hypot(dx, yy - (arch_top + r)) < r + 10)) & ~inside
        inside &= ~m; ring &= ~m
        # 아치 테 돌: 쐐기돌 줄눈
        ang = np.degrees(np.arctan2(yy - (arch_top + r), dx))
        key = ((np.floor(ang / 15)) % 2 == 0) & (yy < arch_top + r)
        lvl[ring] = (3.6 + key[ring] * .3 + np.minimum(L[ring], 6))
        # 안쪽: 멀수록 어둡게, 먼 층은 시점 0.45배로 밀림
        far = np.full(inside.sum(), .35, np.float32)
        fx = (xx[inside] + left * .45).astype(int); fyv = yy[inside]
        far_arch = ((fx % 72) < 6) & (fyv > arch_top + r - 6)
        far = far + far_arch * .6 + np.clip((fyv - (fy - 40)) / 40, 0, 1) * .5
        water = (fyv >= fy - 10) & (fyv < fy - 7) & ((fx + fyv * 3) % 9 < 6)
        far = far + water * 2.6
        lvl[inside] = far + np.minimum(L[inside], 5) * .35
        tunnels.append((cx, fy, r)); tmask |= inside | ring
    # 굵은 가로 관: 방마다 하나, 월드 y 고정. 띠쇠와 리벳, 아래로 물방울.
    py = pipe_y(view_i, top)
    if 0 < py < HH - 30:
        band = slice(py, py + 18)
        rel = np.arange(18)[:, None]
        prof = np.where(rel < 2, 4.6, np.where(rel < 5, 4.0, np.where(rel > 15, 2.2, 3.3)))
        clamp = ((xx[band] + left) % 160 < 8)
        prof = prof + clamp * .7
        rivet = clamp & ((rel == 4) | (rel == 13)) & ((xx[band] + left) % 160 == 4)
        prof = prof + rivet * 1.4
        open_ = ~m[band] & ~tmask[band]
        lvl[band] = np.where(open_, prof + np.minimum(L[band], 6), lvl[band])
        # 관 아래 그림자 4px
        lvl[py + 18:py + 22] = np.where(~m[py + 18:py + 22] & ~tmask[py + 18:py + 22], lvl[py + 18:py + 22] - 1.0, lvl[py + 18:py + 22])
    return lvl, tunnels, py

# ---------------------------------------------------------------- 지형
def terrain_B(left, top, m, L, Wm):
    """지형 속은 먹색으로 빼고, 바깥 테두리 2~3줄만 채택 벽돌을 보인다."""
    yy, xx = np.mgrid[0:HH, 0:WW]; wx = xx + left; wy = top - yy
    dist = ndimage.distance_transform_cdt(m, metric='taxicab').astype(np.float32)
    up = np.full(m.shape, 99, np.int16)
    for n in range(1, 40):
        sh = np.roll(m, n, axis=0); sh[:n] = False
        up[(up == 99) & ~sh] = n - 1
    cores = np.stack([np.array(Image.open(V21 / f'terrain/runtime/tiles/stone-FF-v{v}.png').convert('RGB')) for v in range(8)]).astype(np.float32)
    caps = np.stack([np.array(Image.open(V21 / f'terrain/source/normalized/top-{v}.png').convert('RGB')) for v in range(8)]).astype(np.float32)
    mark = (((wx) // 32) * 73856093) ^ (((wy) // 32) * 19349663); var = (mark ^ (mark >> 13)) & 7
    tex = cores[var, (-wy) % 32, wx % 32]
    # 채택 벽돌 밝기를 0..10 램프로 옮긴다(색은 새 팔레트, 모양은 채택본 그대로)
    lum = tex.mean(-1)
    lvl = 3.4 + (lum - 77) / 6.0
    # 테두리에서 멀어질수록 먹으로: 0~10px 그대로, 10~30px 디더로 꺼짐
    # 벽돌 한 장(32x16, 줄마다 반 장 밀림) 단위로 남기거나 바위로 바꾼다. 번짐 없이 부서진 가장자리.
    brow_ = np.floor_divide(-wy, 16); boff_ = (brow_ % 2) * 16
    bcol_ = np.floor_divide(wx + boff_, 32)
    cy_ = brow_ * 16 + 8; cx_ = bcol_ * 32 - boff_ + 16
    # 벽돌 중심의 표면 거리
    cyi = np.clip(top + cy_ - 0, 0, HH - 1).astype(int) if False else np.clip(cy_ + top - top + (yy - (-wy - (-wy) % 16)) * 0 + (yy - ((-wy) % 16)) + 8 - yy + yy, 0, HH - 1)
    cyi = np.clip(yy - ((-wy) % 16) + 8, 0, HH - 1); cxi = np.clip(xx - ((wx + boff_) % 32) + 16, 0, WW - 1)
    dc = dist[cyi, cxi]
    keep = dc < 12 + (hshv(bcol_ * 92821 + brow_ * 68917) % 14)
    fade = (~keep).astype(np.float32)
    lvl = lvl * (1 - fade) + 1.0 * fade
    lvl = lvl + np.minimum(L, 5) * (1 - fade) * .7
    cap_mark = ((wx // 32) * 73856093) ^ (((wy + up) // 32) * 19349663); cap_var = (cap_mark ^ (cap_mark >> 13)) & 7
    capsel = m & (up < 14)
    cap_lum = caps[cap_var[capsel], np.clip(up[capsel], 0, 15), wx[capsel] % 32].mean(-1)
    lvl[capsel] = 4.8 + (cap_lum - 134) / 28.0 + np.minimum(L[capsel], 5) * .8
    lvl[m & (up == 0)] += 4.0         # 윗면 1px 빛(발판과 같은 밝기)
    lvl[m & (up == 1)] += 2.6
    lvl[m & (up == 2)] += 1.0
    col = ramp(lvl, Wm * (1 - fade), wx, -wy)
    # 이끼: 윗면 바로 아래로 1~7px 흘러내림, 월드 x 마다 길이 고정
    moss_len = ((wx * 2654435761 >> 7) & 7) - 1
    moss_len = np.where(hshv(wx // 7) % 5 < 2, moss_len, 0)
    moss = m & (up >= 14) & (up < 14 + moss_len)
    wet = m & (up == 0) & (hshv(wx // 24 + (wy // 32) * 977) % 4 == 0) & ((wx % 24) > 3) & ((wx % 24) < 19)
    # 먹 테두리 1px (캐릭터 테두리와 같은 말투)
    edge = m & ~ndimage.binary_erosion(m, np.ones((3, 3)), border_value=1)
    col[edge & (up > 0)] = INK
    # 속: 뒷벽(청록)과 색상을 달리한 어두운 청보라 바위. 큰 돌마다 윗왼변 1px 빛, 아랫오른변 1px 그늘.
    deep = m & (dist > 8)
    sh = 56; brow = np.floor_divide(-wy, sh); boff = hshv(brow + 31) % 56
    sw = 88 + (hshv(brow) % 3) * 24
    bxp = (wx + boff) % sw; byp = (-wy) % sh
    bxx = np.floor_divide(wx + boff, sw)
    bv = hshv(bxx * 7919 + brow * 104729) % 4
    idx = np.full(m.shape, 1, np.int16) + (bv == 0)
    idx = np.where(((bxp == 1) | (byp == 1)) & (bv < 2), idx + 1, idx)
    idx = np.where((bxp == sw - 1) | (byp == sh - 1), idx - 1, idx)
    idx = np.where((bxp == 0) | (byp == 0), 0, idx)
    # 표면 가까울수록 한 단계 밝게
    # 금 간 자국: 돌 몇 개에 대각선 1px
    crack = (hshv(bxx * 31 + brow * 17) % 7 == 0) & (np.abs((bxp - 10) - (byp - 6) * 1.4) < .7) & (byp > 6) & (byp < 30)
    idx = np.where(crack, 0, idx)
    idx = np.clip(idx, 0, 3)
    rc = ROCK[idx]
    deep = m & ~keep & (dist > 1)
    col[deep] = rc[deep]
    alpha = m.astype(np.uint8) * 255
    return col, alpha, dist, up

def moss_tufts(col, alpha, m, left, top):
    tops = m & ~np.vstack([np.zeros((1, m.shape[1]), bool), m[:-1]])
    ys, xs = np.where(tops)
    for y, x in zip(ys, xs):
        wx = x + left; c = hsh(wx // 9, y + top, 5)
        if c % 4: continue
        k = wx % 9
        if k > 5: continue
        h = [1, 2, 3, 2, 1, 2][(k + c) % 6]
        for j in range(1, h + 1):
            if y - j < 0 or m[y - j, x]: break
            col[y - j, x] = rgb('#86a35a') if j == h else rgb('#4d6e3a'); alpha[y - j, x] = 255

def stone_deck(x, y, Lw, lw, rw, L, Wm, left, top):
    """통과 발판: 땅 윗면과 같은 램프·조명을 받는 얇은 돌판. 판 자체 높이 11px(규격 12px 이하), 위 3px·아래 4px 은 이끼·물방울 장식."""
    T, h, B = 3, 11, 4
    wxs = np.arange(Lw) + x + left
    prof = np.array([10.0, 9.0, 7.2, 7.0, 6.8, 6.6, 6.4, 6.0, 4.6, 3.0, 0.0])
    lv = np.repeat(prof[:, None], Lw, axis=1)
    seam = (wxs % 32 == 0); bvar = ((hshv(wxs // 32) % 3) - 1) * .3
    lv[2:9] += bvar
    lv[2:9, seam] = 4.0
    lv[2:9, wxs % 32 == 1] += .5
    chip = hshv(wxs) % 19 == 0
    lv[1, chip] -= 1.4; lv[2, chip] -= .8
    rows = np.clip(np.arange(h) + y, 0, HH - 1)[:, None]; cols = np.clip(np.arange(Lw) + x, 0, WW - 1)[None, :]
    lv[:9] += np.minimum(L[rows, cols][:9], 5) * .7
    rgbv = ramp(lv, Wm[rows, cols], np.broadcast_to(wxs, (h, Lw)), np.broadcast_to(rows, (h, Lw)) - top)
    rgbv[10] = INK
    out = np.zeros((T + h + B, Lw, 4), np.uint8)
    out[T:T + h, :, :3] = rgbv.clip(0, 255); out[T:T + h, :, 3] = 255
    # 벽에 붙지 않은 끝은 먹 테두리와 둥근 모서리
    if not lw:
        out[T + 1:T + h, 0, :3] = INK; out[T, 0, 3] = 0; out[T + h - 1, 0, 3] = 0
    if not rw:
        out[T + 1:T + h, -1, :3] = INK; out[T, -1, 3] = 0; out[T + h - 1, -1, 3] = 0
    for i, wx in enumerate(wxs):
        c = hsh(wx // 7, y + top, 21)
        # 위: 이끼 덩이(드물게)
        if c % 5 == 0 and (wx % 7) < 4 and 1 < i < Lw - 2:
            hh = [1, 2, 1, 2][(wx + c) % 4]
            for j in range(1, hh + 1):
                out[T - j, i, :3] = rgb('#86a35a') if j == hh else rgb('#4d6e3a'); out[T - j, i, 3] = 255
        # 아래: 물방울·이끼 늘어짐
        d = hsh(wx, y + top, 22)
        if d % 23 == 0 and 1 < i < Lw - 2:
            ln = 1 + d % 3
            for j in range(ln):
                out[T + h + j, i, :3] = rgb('#2d4a40') if j < ln - 1 else rgb('#7fb39a'); out[T + h + j, i, 3] = 255
    return Image.fromarray(out), T

def wall_bracket(img, x, y, side):
    """벽에 붙은 발판 끝 아래 쇠 받침. side=+1 이면 왼쪽 벽, -1 이면 오른쪽 벽."""
    d = ImageDraw.Draw(img)
    pts = [(x, y + 10), (x + 12 * side, y + 10), (x, y + 22)]
    d.polygon(pts, fill=(26, 34, 36)); d.line([(x, y + 10), (x + 12 * side, y + 10)], fill=(84, 104, 98))
    d.line([(x + 12 * side, y + 10), (x, y + 22)], fill=(16, 22, 24))
    d.point((x + 3 * side, y + 13), fill=(110, 128, 116))

def puddles(col, alpha, m, up, left, top, L, Wm, waters, hero_x):
    """바닥 윗면 군데군데 얕은 물웅덩이. 물줄기 떨어지는 자리엔 꼭 하나."""
    surf = m & (up == 0); lab, _ = ndimage.label(surf)
    spots = []
    for sl in ndimage.find_objects(lab):
        ys, xs = sl; y = ys.start; x0, x1 = xs.start, xs.stop
        for (sx, oy, fy) in waters:
            if fy == y and x0 + 8 <= sx - 26 and sx + 26 <= x1 - 8: spots.append((y, sx - 26, sx + 26))
        step = 208
        for wx in range(((x0 + left) // step) * step, x1 + left, step):
            hsv = hsh(wx, y + top, 31)
            if hsv % 4 == 0: continue
            w = 56 + hsv % 60; a = wx - left + (hsv >> 8) % 50
            if a < x0 + 10 or a + w > x1 - 10: continue
            if any(abs((a + w / 2) - (s1 + s2) / 2) < (w + s2 - s1) / 2 + 24 for (yy_, s1, s2) in spots if yy_ == y): continue
            spots.append((y, a, a + w))
    for (y, a, b) in spots:
        for xq in range(a, b):
            t = min(xq - a, b - 1 - xq); depth = 1 if t < 2 else 2 if t < 5 else 3 if t < 10 else 4
            warm = Wm[y, xq]; lit = min(L[y, xq], 4) / 4
            wx = xq + left
            surf_c = rgb('#d2f4e8') if hsh(wx, 41) % 5 else rgb('#8cc4b6')
            if y - 1 >= 0 and not m[y - 1, xq] and t >= 1:
                col[y - 1, xq] = surf_c * (1 - warm * .45) + rgb('#ffd08a') * warm * .45; alpha[y - 1, xq] = 255
            col[y, xq] = rgb('#5fa69c') * (1 - warm * .35) + rgb('#f2bf74') * warm * .35
            body = [None, rgb('#2f6f70'), rgb('#255d62'), rgb('#1c4b52')]
            for k in range(1, depth): col[y + k, xq] = body[k]
            if depth >= 3 and hsh(wx // 3, 49) % 4 == 0: col[y + 2, xq] = rgb('#4f9a94')   # 위 벽이 비친 잔물결
            if depth >= 2 and warm > .2 and hsh(wx // 2, 43) % 3 == 0: col[y + 1, xq] = rgb('#d9a35e')   # 등불 반사
            for k in range(2, 5):
                if y - k >= 0 and not m[y - k, xq]: alpha[y - k, xq] = 0
        # 물결 고리: 수면 위 1px, 웅덩이 안 한 곳
        rx = a + (hsh(a + left, y, 47) % max(1, b - a - 12)) + 6
        for dx in (-4, -3, 3, 4):
            q = rx + dx
            if a < q < b - 1 and y - 2 >= 0 and not m[y - 2, q]:
                col[y - 2, q] = rgb('#9fd0c0'); alpha[y - 2, q] = 255
        for xq in (a - 1, b):
            if 0 <= xq < WW and m[y, xq]: col[y, xq] = col[y, xq] * .55
    return spots

def drips(col, alpha, m, left, top, Lm):
    """천장 아랫면에서 늘어지는 물때·점액. 땅 밖으로 나오므로 지형 위에 그린다."""
    bots = m & ~np.vstack([m[1:], np.zeros((1, m.shape[1]), bool)])
    ys, xs = np.where(bots)
    for y, x in zip(ys, xs):
        wx = x + left
        h = hsh(wx, y + top, 3)
        if h % 7 != 0: continue
        ln = 2 + h % 9
        for k in range(1, ln + 1):
            yy = y + k
            if yy >= HH or m[yy, x]: break
            col[yy, x] = INK if k < ln else rgb('#2d4a40'); alpha[yy, x] = 255
        if h % 5 == 0 and y + ln + 1 < HH and not m[y + ln + 1, x]:
            col[y + ln + 1, x] = rgb('#7fb39a'); alpha[y + ln + 1, x] = 255  # 맺힌 물방울

# ---------------------------------------------------------------- 소품
def draw_lamp(img, lx, ly, hang=None):
    d = ImageDraw.Draw(img)
    if hang is not None:
        for yy in range(hang, ly - 10, 5):
            d.rectangle((lx - 1, yy, lx + 1, yy + 3), outline=(30, 40, 42)); d.point((lx, yy + 1), fill=(90, 80, 60))
    # 벽 고정쇠 → 사슬 → 등갓
    if hang is None:
        d.rectangle((lx - 1, ly - 22, lx + 1, ly - 10), fill=(20, 26, 28))
        d.rectangle((lx - 5, ly - 24, lx + 5, ly - 22), fill=(28, 34, 36))
    d.rectangle((lx - 6, ly - 10, lx + 6, ly - 8), fill=(30, 26, 22))
    d.rectangle((lx - 5, ly - 8, lx + 5, ly + 5), fill=(255, 196, 108))
    d.rectangle((lx - 3, ly - 6, lx + 3, ly + 3), fill=(255, 236, 186))
    for x in (lx - 5, lx, lx + 5): d.line((x, ly - 8, x, ly + 5), fill=(48, 34, 24))
    d.rectangle((lx - 6, ly + 5, lx + 6, ly + 7), fill=(30, 26, 22))

def draw_grate(img, sx, sy):
    d = ImageDraw.Draw(img)
    d.rectangle((sx - 18, sy, sx + 18, sy + 3), fill=(12, 18, 20))
    for x in range(sx - 15, sx + 16, 5): d.line((x, sy, x, sy + 2), fill=(170, 205, 190))

def draw_water(img, sx, oy, fy, left=0):
    d = ImageDraw.Draw(img)
    # 벽에서 튀어나온 짧은 관과 테두리 쇠
    d.rectangle((sx - 11, oy - 22, sx + 11, oy - 4), fill=(34, 48, 50)); d.line((sx - 11, oy - 22, sx + 11, oy - 22), fill=(80, 104, 98))
    d.rectangle((sx - 13, oy - 6, sx + 13, oy - 2), fill=(26, 38, 40)); d.line((sx - 13, oy - 6, sx + 13, oy - 6), fill=(96, 120, 110))
    d.rectangle((sx - 7, oy - 2, sx + 7, oy), fill=(8, 12, 14))
    for x in (sx - 9, sx + 9): d.point((x, oy - 14), fill=(110, 130, 118))
    # 물줄기: 어두운 테 + 밝은 심 + 흘러내리는 반짝임, 아래로 조금 넓어짐
    for y in range(oy, fy):
        k = (y - oy) / max(1, fy - oy); half = 5 + int(k * 2)
        d.line((sx - half, y, sx + half, y), fill=(58, 106, 104))
        d.line((sx - half + 2, y, sx + half - 2, y), fill=(98, 156, 146))
        if (y + sx) % 9 < 4: d.point((sx - 2 + (y // 6) % 4, y), fill=(200, 236, 222))
        if (y * 3 + sx) % 13 < 2: d.point((sx + half - 3, y), fill=(170, 214, 200))
    # 물보라와 고인 물
    for i, (dx, dy) in enumerate([(-11, -3), (-8, -7), (-4, -10), (5, -9), (9, -6), (12, -3), (-14, -1), (14, -1), (0, -12)]):
        d.point((sx + dx, fy + dy), fill=(190, 228, 214) if i % 2 else (120, 170, 158))
    d.line((sx - 18, fy - 1, sx + 18, fy - 1), fill=(120, 172, 160)); d.line((sx - 10, fy - 2, sx + 10, fy - 2), fill=(160, 204, 190))

def chains_for(img, x, y, L, m):
    d = ImageDraw.Draw(img)
    for cx in (x + 10, x + L - 11):
        if not (0 <= cx < WW): continue
        top_y = y
        while top_y > 0 and not m[top_y - 1, cx] and y - top_y < 320: top_y -= 1
        if top_y == 0 or y - top_y >= 320: continue
        for yy in range(top_y, y, 6):
            d.rectangle((cx - 1, yy, cx + 1, yy + 3), outline=(44, 58, 58))
            d.point((cx - 1, yy), fill=(96, 120, 108))

def dust(img, left, top, L):
    a = np.array(img)
    for i in range(170):
        h = hsh(i, left // 960, top // 540, 9)
        x = h % WW; y = (h >> 11) % HH
        if L[y, x] > .8:
            a[y, x] = np.minimum(a[y, x].astype(int) + 70, 255)
    return Image.fromarray(a)

def fog(arr):
    yy = np.arange(HH)[:, None]
    t = np.clip((yy - (PAD + H * .68)) / (H * .32), 0, 1) * .22
    th = BAYER[np.arange(HH)[:, None] % 4, np.arange(WW)[None] % 4]
    step = (np.floor(t * 4 + th * .9) / 4)
    return arr * (1 - step[..., None]) + rgb('#41605a') * step[..., None]

# ---------------------------------------------------------------- 합성
def render(view_i, mode='B', layers=False):
    v = views[view_i]; cx, cy = v['camera']; px, py = v['heroFeet']
    left = round(cx * 32 - W / 2) - PAD; top = round(cy * 32 + H / 2) + PAD
    m = solid_mask(left, top)
    lamps, shafts = find_lights(left, top, m)
    _hx = round(px * 32) - left; _hy = top - round(py * 32)
    lamps = [l for l in lamps if not (abs(l[0] - _hx) < 56 and _hy - 90 < l[1] < _hy + 10)]
    doors = find_tunnels(left, m, top)
    lamps = [l for l in lamps if not hits_door(doors, l[0] - 8, (l[2] if l[2] is not None else l[1] - 26), l[0] + 8, l[1] + 8)]
    # 문 옆 벽등: 문 테 바깥 여유 12px 너머, 문과 같은 높이대
    for (dcx, dfy, dr, dh) in doors:
        for side in ((1, -1) if hsh(dcx + left, 5) % 2 else (-1, 1)):
            lx = dcx + side * (dr + 48); ly = dfy - round(dh * .56)
            _p = pipe_y(view_i, top)
            if ly + 8 >= _p - 6 and ly - 26 <= _p + 28: ly = _p + 28 + 30 if _p + 58 < dfy - 40 else _p - 6 - 12
            if not (8 < lx < WW - 8) or m[ly - 30:ly + 30, lx - 10:lx + 10].any(): continue
            if hits_door(doors, lx - 8, ly - 26, lx + 8, ly + 8): continue
            if abs(lx - _hx) < 56 and _hy - 90 < ly < _hy + 10: continue
            if any(abs(lx - a) < 150 and abs(ly - b) < 140 for a, b, _ in lamps): continue
            lamps.append((lx, ly, None)); break
    waters = find_water(left, top, m, lamps, view_i, hero_x=round(px * 32) - left)
    waters = [w for w in waters if not hits_door(doors, w[0] - 20, w[1] - 24, w[0] + 20, w[2])]
    _ow = oneways(left, top, m)
    def _hits_deck(x0, y0, x1, y1, mg=16):
        return any(x1 >= x - mg and x0 <= x + Lw + mg and y1 >= y - mg and y0 <= y + 12 + mg for (x, y, Lw, _a, _b) in _ow)
    waters = [w for w in waters if not _hits_deck(w[0] - 20, w[1] - 24, w[0] + 20, w[2])]
    lamps = [l for l in lamps if not _hits_deck(l[0] - 8, (l[2] if l[2] is not None else l[1] - 26), l[0] + 8, l[1] + 8, 8)]
    _py = pipe_y(view_i, top)
    def _hits_pipe(y0, y1): return y1 >= _py - 6 and y0 <= _py + 28
    lamps = [l for l in lamps if not _hits_pipe(l[1] - 26, l[1] + 8)]
    waters = [w for w in waters if not _hits_pipe(w[1] - 24, w[1] + 2)]
    waters = waters[:1]
    L, Wm = light_maps(lamps, shafts)
    yy0, xx0 = np.mgrid[0:HH, 0:WW]
    for (wx_, oy, fy) in waters:
        L += np.clip(1 - np.hypot((xx0 - wx_) * 1.0, np.clip(np.abs(yy0 - (oy + fy) / 2) - (fy - oy) / 2, 0, None)) / 46, 0, 1) * 1.1
    _, lvl = back_wall(left, top, L, Wm, m, view_i)
    lvl, tunnels, _pipe_drawn = draw_props(lvl, left, top, m, view_i, lamps, shafts, L, Wm)
    # 땅에 닿는 벽은 어둡게(접촉 그늘): 땅까지 거리 0~10px
    dair = ndimage.distance_transform_cdt(~m, metric='chessboard').astype(np.float32)
    lvl = lvl - np.clip(1 - dair / 10, 0, 1) * 1.3
    # 바닥에서 위로 재는 높이. 34px 아래는 물에 잠겼던 자리라 어둡고, 그 위 2px 은 물때 줄.
    down = np.full(m.shape, 99, np.int16)
    for n in range(1, 60):
        sh = np.roll(m, -n, axis=0); sh[-n:] = False
        down[(down == 99) & sh] = n
    yy, xx = np.mgrid[0:HH, 0:WW]
    # 수위: 화면 안 가장 낮은 바닥 윗면에서 34px 위, 방 전체에 한 높이. 바닥에서 떨어진 공중 덩어리엔 안 생김.
    floor_rows = np.where((m & ~np.roll(m, 1, axis=0))[PAD:PAD + H].any(axis=1))[0]
    low = (floor_rows.max() + PAD) if len(floor_rows) else HH
    wl_y = low - 26 - (hshv((xx + left) // 8) % 4) - (hshv((xx + left) // 40) % 6)   # 8px·40px 단위로 들쭉날쭉
    wet = (~m) & (yy > wl_y) & (yy < low)
    lvl = np.where(wet, lvl - .75, lvl)
    drip = wet & (hshv((xx + left) // 2) % 13 == 0) & (yy < wl_y + 3 + hshv((xx + left) // 2 + 7) % 14)
    lvl = np.where(drip, lvl - .5, lvl)   # 얼룩이 아래로 흘러내린 자국(어둡게)
    # 기둥 받침(바닥 위 14px)과 머리돌(천장 아래 10px): 기둥보다 4px 넓고 윗변 1px 밝게
    upd = np.full(m.shape, 99, np.int16)
    for n in range(1, 40):
        sh2 = np.roll(m, n, axis=0); sh2[:n] = False
        upd[(upd == 99) & sh2] = n
    for pxw in range(((left - 400) // 288) * 288, left + WW + 288, 288):
        sx = pxw - left
        a_, b_ = max(0, sx - 4), min(WW, sx + 32)
        if b_ <= a_: continue
        sub_m = ~m[:, a_:b_]
        base = sub_m & (down[:, a_:b_] <= 14)
        cap = sub_m & (upd[:, a_:b_] <= 10)
        rel = np.arange(a_, b_)[None, :] - sx
        for msk, topline in ((base, down[:, a_:b_] == 14), (cap, upd[:, a_:b_] == 10)):
            v = 3.3 + (rel < 0) * .4 - (rel > 27) * .6 + topline * 1.0 + np.minimum(L[:, a_:b_], 6)
            lvl[:, a_:b_] = np.where(msk, v, lvl[:, a_:b_])
    bg = ramp(lvl, Wm, xx + left, yy - top)
    bg = fog(bg)
    bgim = Image.fromarray(bg.clip(0, 255).astype(np.uint8)).convert('RGBA')
    for (sx, sy) in shafts: draw_grate(bgim, sx, sy)
    ows = oneways(left, top, m)
    for (x, y, Lw, lw, rw) in ows:
        if not (lw or rw): chains_for(bgim, x, y, Lw, m)
        if lw: wall_bracket(bgim, x, y, 1)
        if rw: wall_bracket(bgim, x + Lw - 1, y, -1)
        # 발판이 벽에 드리우는 그림자
        a = np.array(bgim); ys, ye = max(0, y + 10), min(HH, y + 16); xs, xe = max(0, x + 2), min(WW, x + Lw + 2)
        a[ys:ye, xs:xe, :3] = (a[ys:ye, xs:xe, :3] * .6).astype(np.uint8); bgim = Image.fromarray(a)
    for (lx, ly, hg) in lamps: draw_lamp(bgim, lx, ly, hg)
    for w in waters: draw_water(bgim, *w, left=left)
    bg_layer = bgim.copy()
    col, alpha, dist, up = terrain_B(left, top, m, L, Wm)
    drips(col, alpha, m, left, top, L)
    moss_tufts(col, alpha, m, left, top)
    puddles(col, alpha, m, up, left, top, L, Wm, waters, round(px * 32) - left)
    fg_layer = Image.fromarray(np.dstack([col.clip(0, 255).astype(np.uint8), alpha]))
    for (x, y, Lw, lw, rw) in ows:
        dk, T = stone_deck(x, y, Lw, lw, rw, L, Wm, left, top)
        fg_layer.alpha_composite(dk, (x, y - T))
    bgim.alpha_composite(fg_layer)
    bgim = dust(bgim, left, top, L)
    hx = round(px * 32) - left; hy = top - round(py * 32)
    # 캐릭터 발밑 그림자 (승인 캐릭터 그림은 손대지 않음)
    a = np.array(bgim); sh = (np.abs(xx - hx) < 20) & (np.abs(yy - (hy - 1)) < 2) & ~m
    a[sh, :3] = (a[sh, :3] * .55).astype(np.uint8); bgim = Image.fromarray(a)
    bgim.alpha_composite(hero, (hx - hero.width // 2, hy - hero.height))
    box = (PAD, PAD, PAD + W, PAD + H)
    info = {'lamps': len(lamps), 'shafts': len(shafts), 'doors': len(tunnels), 'waters': len(waters)}
    if layers:
        return bgim.crop(box).convert('RGB'), bg_layer.crop(box).convert('RGB'), fg_layer.crop(box), info
    return bgim.crop(box).convert('RGB'), info
