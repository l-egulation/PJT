# v26 화풍을 Unity 타일 부품으로 뽑는다. 2챕터 맵은 판마다 시드가 달라서 통그림 대신 부품을 Unity 가 맵에 맞춰 깐다.
#   python export_tiles.py <출력 폴더>
# 조명은 굽지 않는다(L=0). 뒷벽은 Unity 2D 조명을 받고, 등 둘레 밝음은 halo 부품으로 얹는다.
#
# terrain-atlas.png : 땅 칸 32×48(위아래 8px 는 공기 칸으로 넘치는 이끼·물때). 열 32 × 행 32.
#                     index = mask*4 + variant. mask 비트(y 위가 +): 0(-1,+1) 1(0,+1) 2(+1,+1) 3(-1,0) 4(+1,0) 5(-1,-1) 6(0,-1) 7(+1,-1). 비트가 1 이면 땅.
# wall.png          : 뒷벽 576×512(18u×16u) 이음매 없음. 월드 x·y 가 576·512 의 배수인 자리에 깐다. 기둥은 타일 안 x 0~28·288~316.
# pillar-base/cap   : 기둥이 바닥·천장에 닿는 받침(36×14)·머리돌(36×10). 기둥 x-4 에 둔다.
# contact-shadow    : 땅에 닿는 뒷벽 그늘 32×10(땅 쪽이 아래), 알파만.
# deck-*.png        : 통과 발판 32×18(판 11px, 위 3·아래 4 장식). 판 윗면 = 칸 위에서 3px.
# bracket-left/right, chain : 벽 받침, 사슬(8×6 반복).
# door-0..2.png     : 터널 문(큰·중·작) 테 포함. 바닥 가운데가 기준.
# lamp-wall, lamp-hang, lamp-hang-chain, halo, grate, beam : 등·채광.
# basin-2..4.png, basin-front-2..4.png : 물웅덩이 판 칸(폭 2·3·4u × 1u) 물, 물 앞면.
import json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
import render_v26 as r

OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / 'build'   # runtime 은 이 출력의 사본
OUT.mkdir(parents=True, exist_ok=True)
R = r.rgb

def canvas(w, h):
    r.WW, r.HH = w, h

def save(name, arr_or_img):
    im = arr_or_img if isinstance(arr_or_img, Image.Image) else Image.fromarray(arr_or_img)
    im.save(OUT / name, optimize=True); return im

# ---------------------------------------------------------------- 땅
NB = [(-1, 1), (0, 1), (1, 1), (-1, 0), (1, 0), (-1, -1), (0, -1), (1, -1)]

def var_of(cx, cy):
    mark = (cx * 73856093) ^ (cy * 19349663); return (mark ^ (mark >> 13)) & 7

def terrain_tile(mask, variant):
    # 5×5 칸(160px). 가운데 3×3 은 mask, 바깥 고리는 가장 가까운 3×3 칸을 따른다.
    g = np.zeros((5, 5), bool)
    for j in range(5):
        for i in range(5):
            ci, cj = min(max(i, 1), 3) - 2, min(max(j, 1), 3) - 2   # -1..1 (x 오른쪽, y 아래)
            if ci == 0 and cj == 0: g[j, i] = True; continue
            k = NB.index((ci, -cj)); g[j, i] = bool(mask >> k & 1)
    # 바깥에 공기 고리 2칸(64px)을 둔다: 사방이 땅인 칸도 거리 계산이 바위로 떨어지게. 가운데 칸까지 64px 이상 떨어져 영향 없다.
    g7 = np.zeros((9, 9), bool); g7[2:7, 2:7] = g
    m = np.kron(g7, np.ones((32, 32), bool))
    canvas(288, 288)
    want = [0, 2, 5, 7][variant]
    cx = next(c for c in range(4096) if var_of(c, 9) == want)
    left = (cx - 4) * 32; top = (9 + 5) * 32               # 칸 (cx, 9) 가 가운데(캔버스 128..160)
    L = np.zeros((288, 288), np.float32); Wm = L.copy()
    col, alpha, dist, up = r.terrain_B(left, top, m, L, Wm)
    r.drips(col, alpha, m, left, top, L)
    r.moss_tufts(col, alpha, m, left, top)
    rgba = np.dstack([col.clip(0, 255).astype(np.uint8), alpha])
    tile = rgba[120:168, 128:160].copy()                    # 위 8px · 칸 32px · 아래 8px
    keep = np.zeros((48, 32), bool); keep[8:40] = True
    keep[:8] = ~m[120:128, 128:160]; keep[40:] = ~m[160:168, 128:160]
    rock = np.zeros((48, 32), bool)                          # 바위 화소는 비운다: Unity 가 월드에 고정한 rock.png 를 밑에 깐다
    for c in r.ROCK: rock |= (np.abs(tile[..., :3].astype(int) - c.astype(int)).sum(-1) == 0)
    tile[~keep | (rock & (tile[..., 3] > 0))] = 0
    tile[tile[..., 3] == 0] = 0
    return tile

def build_terrain():
    atlas = np.zeros((32 * 48, 32 * 32, 4), np.uint8)
    for mask in range(256):
        for v in range(4):
            i = mask * 4 + v; c, rr = i % 32, i // 32
            atlas[rr * 48:(rr + 1) * 48, c * 32:(c + 1) * 32] = terrain_tile(mask, v)
    save('terrain-atlas.png', atlas)

# ---------------------------------------------------------------- 땅 속 바위(월드 고정, 이음매 없음 896×448)
ROCK_W, ROCK_H = 896, 448

def build_rock():
    yy, xx = np.mgrid[0:ROCK_H, 0:ROCK_W]; sh = 56
    brow = yy // sh; byp = yy % sh
    sw = np.where(r.hshv(brow + 5) % 2 == 0, 112, 128)
    boff = r.hshv(brow + 31) % 56
    bxp = (xx + boff) % sw; nb = ROCK_W // sw
    bxx = ((xx + boff) // sw) % nb
    bv = r.hshv(bxx * 7919 + brow * 104729) % 4
    idx = 1 + (bv == 0).astype(int)
    idx = np.where(((bxp == 1) | (byp == 1)) & (bv < 2), idx + 1, idx)
    idx = np.where((bxp == sw - 1) | (byp == sh - 1), idx - 1, idx)
    idx = np.where((bxp == 0) | (byp == 0), 0, idx)
    crack = (r.hshv(bxx * 31 + brow * 17) % 7 == 0) & (np.abs((bxp - 10) - (byp - 6) * 1.4) < .7) & (byp > 6) & (byp < 30)
    idx = np.clip(np.where(crack, 0, idx), 0, 3) + 1                 # 사용자 「맵이 너무 어둡다」(11-12): 한 단 밝게
    save('rock.png', r.ROCK[idx].clip(0, 255).astype(np.uint8))

# ---------------------------------------------------------------- 뒷벽·기둥
WALL_W, WALL_H = 576, 512

def wall_levels():
    yy, xx = np.mgrid[0:WALL_H, 0:WALL_W]; wx = xx; wy = -yy        # 타일 왼쪽 위 = 월드 (576k, 512j) 위쪽 끝
    row = np.floor_divide(-wy, 32); off = (row % 2) * 32
    bx = np.floor_divide(wx + off, 64) % 9; bxp = (wx + off) % 64; byp = (-wy) % 32
    mortar = (bxp < 2) | (byp < 2)
    bvar = ((bx * 73856093 ^ (row % 16) * 19349663) & 0xff) % 7
    face = 3.2 + (bvar - 3) * .14
    lit_edge = (byp == 2) & ~mortar; shade_edge = ((byp == 31) | (bxp == 63)) & ~mortar
    lvl = np.where(mortar, 2.1, face) + lit_edge * .7 - shade_edge * .5
    broken = (((bx * 2654435761 ^ (row % 16) * 40503) >> 5) & 31) == 0
    lvl = np.where(broken & ~mortar, 1.5, lvl)
    streak = ((wx // 3) * 2246822519 & 0xffff) % 23 == 0
    lvl = lvl - streak * .45                                         # 천장 쪽 어둠은 ceiling-shade 띠로 따로 얹는다
    for sx in (0, 288):                                              # 기둥 폭 28
        rel = np.arange(sx, sx + 28) - sx
        lvl[:, sx:sx + 28] = 3.0 + (rel < 3) * .9 - (rel > 24) * .7 + ((np.arange(WALL_H)[:, None]) % 64 == 0) * -.6
        lvl[:, sx + 28:sx + 34] -= .8
    return lvl, xx, yy

def build_wall():
    lvl, xx, yy = wall_levels()
    img = r.ramp(lvl, np.zeros_like(lvl), xx, yy)
    save('wall.png', img.clip(0, 255).astype(np.uint8))
    # 셰이더용: 밝기 단계 지도(R = 단계×20)와 팔레트(위 줄 차가운 11색, 아래 줄 따뜻한 11색). Unity 가 등빛을 단계에 더해 다시 칠한다.
    lv = np.clip(np.round(lvl * 20), 0, 255).astype(np.uint8)
    save('wall-level.png', np.dstack([lv, lv, lv, np.full_like(lv, 255)]))
    save('ramp.png', np.stack([r.COOL, r.WARM]).clip(0, 255).astype(np.uint8))
    # 받침(바닥 위 14px)·머리돌(천장 아래 10px): 기둥보다 양쪽 4px 넓고 윗변 1px 밝게
    for name, h, topline in (('pillar-base', 14, 0), ('pillar-cap', 10, 9)):
        rel = np.arange(-4, 32)[None, :]; rows = np.arange(h)[:, None]
        v = 3.3 + (rel < 0) * .4 - (rel > 27) * .6 + (rows == topline) * 1.0
        v = np.broadcast_to(v, (h, 36)).copy()
        yy2, xx2 = np.mgrid[0:h, 0:36]
        save(f'{name}.png', np.dstack([r.ramp(v, np.zeros_like(v), xx2, yy2).clip(0, 255).astype(np.uint8), np.full((h, 36), 255, np.uint8)]))
    # 땅에 닿는 뒷벽 그늘: v26 는 레벨 -1.3*(1-d/10). 검정 반투명으로 근사(4단계 디더).
    a = np.zeros((10, 32, 4), np.uint8)
    for d in range(10):
        k = (1 - d / 10) * .42
        for x in range(32):
            t = r.BAYER[d % 4, x % 4]; q = np.floor(k * 4 + t * .9) / 4
            a[9 - d, x] = (4, 8, 10, int(q * 255))
    save('contact-shadow.png', a)

# ---------------------------------------------------------------- 통과 발판·받침·사슬
DECK_KINDS = ['left-free', 'left-wall', 'right-free', 'right-wall', 'middle-a', 'middle-b', 'middle-c', 'single', 'single-left-wall', 'single-right-wall', 'single-both-wall']

def build_decks():
    canvas(512, 64)
    L = np.zeros((64, 512), np.float32)
    for kind in DECK_KINDS:
        lw = 'left-wall' in kind or 'both' in kind; rw = 'right-wall' in kind or 'both' in kind
        if kind.startswith('single'): x0 = 160; n = 1
        elif kind.startswith('left'): x0 = 160; n = 3
        elif kind.startswith('right'): x0 = 96; n = 3
        else: x0 = 32 + 'abc'.index(kind[-1]) * 32 + 64; n = 5
        im, T = r.stone_deck(x0 - 64 if not kind.startswith('single') and kind.startswith('middle') else x0, 20, 32 * n, lw, rw, L, L, 0, 64)
        a = np.array(im)
        piece = a[:, 0:32] if kind.startswith(('left', 'single')) else a[:, -32:] if kind.startswith('right') else a[:, 64:96]
        save(f'deck-{kind}.png', piece)
    for side, name in ((1, 'bracket-left'), (-1, 'bracket-right')):
        im = Image.new('RGBA', (13, 23)); x = 0 if side == 1 else 12
        r.wall_bracket(im, x, 0, side); save(f'{name}.png', im)
    im = Image.new('RGBA', (8, 6)); d = ImageDraw.Draw(im)
    d.rectangle((3, 0, 5, 3), outline=(44, 58, 58)); d.point((3, 0), fill=(96, 120, 108)); save('chain.png', im)

# ---------------------------------------------------------------- 문
def build_doors():
    out = []
    for i, (rad, h) in enumerate(r.DOOR_SIZES):
        Wd, Hd = 2 * (rad + 10) + 2, h + 10 + 2
        cx, fy = rad + 11, Hd
        yy, xx = np.mgrid[0:Hd, 0:Wd]; arch_top = fy - h
        dx = xx - cx
        inside = (np.abs(dx) < rad) & (yy < fy) & ((yy > arch_top + rad) | (np.hypot(dx, yy - (arch_top + rad)) < rad))
        ring = (np.abs(dx) < rad + 10) & (yy < fy) & ((yy > arch_top + rad) | (np.hypot(dx, yy - (arch_top + rad)) < rad + 10)) & ~inside
        lvl = np.zeros((Hd, Wd), np.float32)
        ang = np.degrees(np.arctan2(yy - (arch_top + rad), dx)); key = ((np.floor(ang / 15)) % 2 == 0) & (yy < arch_top + rad)
        lvl[ring] = 3.6 + key[ring] * .3
        fx = xx[inside]; fyv = yy[inside]
        far = np.full(inside.sum(), .35, np.float32)
        far = far + (((fx % 72) < 6) & (fyv > arch_top + rad - 6)) * .6 + np.clip((fyv - (fy - 40)) / 40, 0, 1) * .5
        far = far + ((fyv >= fy - 10) & (fyv < fy - 7) & ((fx + fyv * 3) % 9 < 6)) * 2.6
        lvl[inside] = far
        col = r.ramp(lvl, np.zeros_like(lvl), xx, yy)
        alpha = ((inside | ring) * 255).astype(np.uint8)
        save(f'door-{i}.png', np.dstack([col.clip(0, 255).astype(np.uint8), alpha]))
        out.append({'index': i, 'radius': rad, 'height': h, 'width': Wd, 'pixelHeight': Hd, 'pivotX': cx})
    return out

# ---------------------------------------------------------------- 등·채광
def build_lights():
    im = Image.new('RGBA', (16, 34)); r.draw_lamp(im, 8, 25, None); save('lamp-wall.png', im)       # 등 가운데 (8,25)
    im = Image.new('RGBA', (16, 20)); r.draw_lamp(im, 8, 11, 1000); save('lamp-hang.png', im)      # 매단 등 가운데 (8,11)
    im = Image.new('RGBA', (8, 5)); d = ImageDraw.Draw(im); d.rectangle((3, 0, 5, 3), outline=(30, 40, 42)); d.point((4, 1), fill=(90, 80, 60)); save('lamp-hang-chain.png', im)
    im = Image.new('RGBA', (37, 4)); r.draw_grate(im, 18, 0); save('grate.png', im)
    # 채광 빛줄기(살짝 기울어 아래로 넓어짐) 160×420, 위 가운데(37,0)가 격자
    Hb, Wb = 420, 160; yy, xx = np.mgrid[0:Hb, 0:Wb].astype(np.float32); sx = 37
    dy = yy; cxl = sx + dy * .22; half = 14 + dy * .09
    inside = (dy > 0) & (np.abs(xx - cxl) < half)
    v = inside * np.clip(1 - dy / 420, 0, 1) * np.clip((half - np.abs(xx - cxl)) / 6, 0, 1)
    th = r.BAYER[yy.astype(int) % 4, xx.astype(int) % 4]; q = np.floor(v * 4 + th * .9) / 4 * .30
    beam = np.zeros((Hb, Wb, 4), np.uint8); beam[..., :3] = (160, 210, 196); beam[..., 3] = (q * 255).astype(np.uint8)
    save('beam.png', beam)

# ---------------------------------------------------------------- 깊이감(맵 렌더러 render_map 의 방·바닥 기준 값)
# ceiling-shade 32×224 : 천장에서 220px 안 뒷벽을 어둡게(v26 은 1.1단, 맵이 어둡다는 말에 덜어 검정 20%)(위가 천장). 검정 반투명 근사, 4단계 디더.
# floor-fog     32×144 : 바닥에서 140px 안 안개 #41605a 최대 22%(아래가 바닥).
# wet-band     256×40  : 바닥 위 26~35px 물때(0.75단 어둡게)와 흘러내린 자국. 월드 x 256px 마다 반복, 아래가 바닥.
def dither_alpha(v, yy, xx):
    th = r.BAYER[yy % 4, xx % 4]; return (np.floor(v * 4 + th * .9) / 4 * 255).astype(np.uint8)

def build_depth():
    yy, xx = np.mgrid[0:224, 0:32]; a = np.zeros((224, 32, 4), np.uint8); a[..., :3] = (4, 8, 10)
    a[..., 3] = dither_alpha(np.clip(1 - yy / 220, 0, 1) * .20, yy, xx); save('ceiling-shade.png', a)
    yy, xx = np.mgrid[0:144, 0:32]; d = 143 - yy; a = np.zeros((144, 32, 4), np.uint8); a[..., :3] = R('#41605a')
    a[..., 3] = dither_alpha(np.clip(1 - d / 140, 0, 1) * .22, yy, xx); save('floor-fog.png', a)
    yy, xx = np.mgrid[0:40, 0:256]; d = 39 - yy
    wl = 26 + (r.hshv(xx // 8) % 4) + (r.hshv(xx // 40) % 6)
    wet = d < wl
    drip = wet & (r.hshv(xx // 2) % 13 == 0) & (d > wl - 3 - r.hshv(xx // 2 + 7) % 14)
    a = np.zeros((40, 256, 4), np.uint8); a[..., :3] = (4, 8, 10); a[..., 3] = (wet * .20 + drip * .12) * 255
    save('wet-band.png', a)

# ---------------------------------------------------------------- 가로 배관·물줄기
# pipe-level.png 160×22 : 배관 밝기 단계 지도(R = 단계×20). 위 18줄 관(띠쇠 160px 마다, 리벳), 아래 4줄은 0 = 「뒷벽 단계 -1」(관 그늘).
#                         월드 x 160px 배수에 맞춰 반복한다. 뒷벽 셰이더로 칠해 등빛을 받는다.
# waterfall.png  40×142 : 벽 배수구(바닥 위 118px)에서 떨어지는 물줄기와 물보라. 아래 가운데(20,0)가 물 떨어지는 바닥 윗면.
# basin-splash.png 25×3 : 물줄기를 받는 웅덩이 수면의 물보라. 위 가운데(12,3)가 바닥 윗면.
def build_water_pipe():
    rel = np.arange(18)[:, None]; xs = np.arange(160)[None, :]
    prof = np.where(rel < 2, 4.6, np.where(rel < 5, 4.0, np.where(rel > 15, 2.2, 3.3))) + (xs % 160 < 8) * .7 + ((xs % 160 == 4) & ((rel == 4) | (rel == 13))) * 1.4
    lv = np.zeros((22, 160), np.uint8); lv[:18] = np.clip(np.round(prof * 20), 1, 255)
    save('pipe-level.png', np.dstack([lv, lv, lv, np.full_like(lv, 255)]))
    im = Image.new('RGBA', (40, 142)); r.draw_water(im, 20, 24, 142); save('waterfall.png', im)
    a = np.zeros((3, 25, 4), np.uint8); fall = 12
    for yy in range(3):
        for xq in range(fall - 5, fall + 6): a[yy, xq, :3] = R('#62a9a0') if abs(xq - fall) > 3 else R('#a6dccd'); a[yy, xq, 3] = 255
    for dx in range(-12, 13):
        if abs(dx) > 5 and r.hsh(fall + dx, 83) % 2 == 0: a[2, fall + dx, :3] = R('#eefaf5'); a[2, fall + dx, 3] = 255
    for dx in (-10, -9, 9, 10): a[1, fall + dx, :3] = R('#a6dccd'); a[1, fall + dx, 3] = 255
    save('basin-splash.png', a)

# ---------------------------------------------------------------- 물웅덩이
def build_basins():
    D = 32
    for wu in (2, 3, 4):
        w = wu * 32
        back = np.zeros((D, w, 4), np.uint8); front = np.zeros((D, w, 4), np.uint8)
        for xq in range(w):
            wx = xq + 1000 * wu
            for d in range(D):
                if d < 2: c = R('#0e1c21')
                elif d == 2: c = R('#c6eedf') if r.hsh(wx, 71) % 7 else R('#86c2b4')
                elif d == 3: c = R('#79bdb0')
                else:
                    f = (d - 4) / max(1, D - 5); t = r.BAYER[d % 4, wx % 4]
                    band = min(4, int(f * 5 + (t - .5) * .6))
                    c = [R('#3c8580'), R('#2e6c6c'), R('#225459'), R('#193f47'), R('#122f38')][band]
                    if d < 14 and r.hsh((wx + d * 3) // 5, d, 73) % (6 + d) == 0: c = c * .45 + R('#7cc7b8') * .55
                back[d, xq, :3] = c.clip(0, 255); back[d, xq, 3] = 255
                if d >= 2: front[d, xq, :3] = c.clip(0, 255); front[d, xq, 3] = 220 if d <= 3 else 140
        for d in range(2, D): back[d, 0, :3] = (back[d, 0, :3] * .55).astype(np.uint8); back[d, -1, :3] = (back[d, -1, :3] * .55).astype(np.uint8)
        for k in range(3):
            bx = 2 + r.hsh(wu, 0, 77 + k) % (w - 4); by = 6 + r.hsh(wu, 0, 81 + k) % (D - 9); back[by, bx, :3] = R('#a8dccf')
        save(f'basin-{wu}.png', back); save(f'basin-front-{wu}.png', front)

if __name__ == '__main__':
    build_terrain(); build_rock(); build_wall(); build_decks(); doors = build_doors(); build_lights(); build_depth(); build_water_pipe(); build_basins()
    (OUT / 'tiles.json').write_text(json.dumps({'format': 'deviant-stage2-sewer-v26-tiles-1', 'pixelsPerUnit': 32,
        'terrain': {'cellWidth': 32, 'cellHeight': 48, 'columns': 32, 'overhang': 8, 'variants': 4,
                    'maskBits': ['-1,+1', '0,+1', '+1,+1', '-1,0', '+1,0', '-1,-1', '0,-1', '+1,-1']},
        'rock': {'width': ROCK_W, 'height': ROCK_H}, 'wall': {'width': WALL_W, 'height': WALL_H, 'pillarPeriod': 288, 'pillarWidth': 28},
        'doors': doors, 'deckKinds': DECK_KINDS}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('tiles ->', OUT)
