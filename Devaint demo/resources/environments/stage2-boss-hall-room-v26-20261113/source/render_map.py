# v26 렌더러로 Unity 가 실제로 만든 2챕터 맵(seed 17) 전체를 그리고 Unity 용 조각으로 자른다.
# 맵이 커서(약 9700×8000px) 두 단계로 나눈다.
#   1) 맵 전체 마스크로 등·빛줄기·문·물줄기·가로 관·웅덩이 자리를 한 번만 정한다(이음매에서 판단이 갈리지 않게).
#   2) 2048px 조각마다 넉넉한 여백(PAD)을 두고 화소를 그린 뒤 여백을 잘라 붙인다.
# 화면 기준 처리는 방·바닥 기준으로 바꾼다: 천장 쪽 어둠·안개·젖은 얼룩은 위/아래 땅까지 거리, 가로 관은 방마다.
# python render_map.py <layout-visible.json> <out dir>
from pathlib import Path
import json, math, sys, types, time
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
src = (HERE / 'render_v26.py').read_text(encoding='utf-8')
REPL = [
    ("    depth = np.clip((yy - PAD) / H, 0, 1)\n", "    depth = DEPTH if DEPTH is not None else np.clip((yy - PAD) / H, 0, 1)\n"),
    ("    t = np.clip((yy - (PAD + H * .68)) / (H * .32), 0, 1) * .22\n", "    t = FOG_T if FOG_T is not None else np.clip((yy - (PAD + H * .68)) / (H * .32), 0, 1) * .22\n"),
    ("    for i in range(170):\n", "    for i in range(DUST_N):\n"),
    ("    for (cx, fy, r, h) in find_tunnels(left, m, top):\n", "    for (cx, fy, r, h) in (DOORS_OVERRIDE if DOORS_OVERRIDE is not None else find_tunnels(left, m, top)):\n"),
    ("    BASINS.append([((a_ + left)", "    if SPOTS_OVERRIDE is not None: spots = SPOTS_OVERRIDE\n    BASINS.append([((a_ + left)"),
]
for a, b in REPL:
    assert src.count(a) == 1, a
    src = src.replace(a, b)
src = src.replace("    wall = 3\n    for (y, a, b, fall) in spots:", "    if SELECT_ONLY: return spots\n    wall = 3\n    for (y, a, b, fall) in spots:")
assert 'if SELECT_ONLY' in src
r = types.ModuleType('render_map_core'); r.__file__ = str(HERE / 'render_v26.py')
r.__dict__.update(DEPTH=None, FOG_T=None, DUST_N=170, DOORS_OVERRIDE=None, SPOTS_OVERRIDE=None, SELECT_ONLY=False)
exec(compile(src, str(HERE / 'render_v26.py'), 'exec'), r.__dict__)
PAD = 352; r.PAD = PAD

def light_maps_windowed(lamps, shafts, HH, WW):
    L = np.zeros((HH, WW), np.float32); Wm = np.zeros((HH, WW), np.float32)
    for (lx, ly, _) in lamps:
        x0, x1, y0, y1 = max(0, lx - 240), min(WW, lx + 240), max(0, ly - 240), min(HH, ly + 240)
        if x1 <= x0 or y1 <= y0: continue
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32); d = np.hypot(xx - lx, (yy - ly) * 1.15)
        L[y0:y1, x0:x1] += np.clip(1 - d / 210, 0, 1) ** 1.9 * 3.8
        Wm[y0:y1, x0:x1] = np.maximum(Wm[y0:y1, x0:x1], np.clip(1 - d / 150, 0, 1) ** 1.6)
    for (sx, sy) in shafts:
        x0, x1, y0, y1 = max(0, sx - 80), min(WW, sx + 200), max(0, sy - 70), min(HH, sy + 430)
        if x1 <= x0 or y1 <= y0: continue
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32); dy = yy - sy; cxl = sx + dy * .22; half = 14 + dy * .09
        inside = (dy > 0) & (np.abs(xx - cxl) < half)
        L[y0:y1, x0:x1] += inside * np.clip(1 - dy / 420, 0, 1) * np.clip((half - np.abs(xx - cxl)) / 6, 0, 1) * 2.3
        L[y0:y1, x0:x1] += np.clip(1 - np.hypot(xx - sx, (yy - sy) * 1.6) / 60, 0, 1) * 1.5
    return L, Wm

def dist_to_solid(m, upward):
    rows = np.arange(m.shape[0], dtype=np.int32)[:, None]
    if upward:
        return rows - np.maximum.accumulate(np.where(m, rows, -10 ** 6).astype(np.int32), axis=0)
    return np.minimum.accumulate(np.where(m, rows, 10 ** 6).astype(np.int32)[::-1], axis=0)[::-1] - rows

def set_canvas(Wi, Hi):
    r.W, r.H = Wi, Hi; r.WW, r.HH = Wi + 2 * PAD, Hi + 2 * PAD

# ---------------------------------------------------------------- 1) 맵 전체 배치
def plan(layout, x0u, y0u, x1u, y1u):
    r.layout = layout
    set_canvas((x1u - x0u) * 32, (y1u - y0u) * 32)
    left = x0u * 32 - PAD; top = y1u * 32 + PAD; WW, HH = r.WW, r.HH
    r.pipe_y = lambda view_i, top_: -10 ** 6
    m = r.solid_mask(left, top)
    rooms = layout['rooms']
    def room_of(cx, cy):
        wx, wy = (cx + left) / 32, (top - cy) / 32
        for rm in rooms:
            if rm['x'] <= wx < rm['x'] + rm['w'] and rm['y'] <= wy < rm['y'] + rm['h']: return rm['id']
        return None
    pipes = []
    for rm in rooms:
        if rm['h'] < 9: continue
        hv = r.hsh(int(rm['x'] * 32), int(rm['y'] * 32), 91)
        pipes.append((top - round((rm['y'] + 4.5 + (hv % 4) * .5) * 32), round(rm['x'] * 32) - left, round((rm['x'] + rm['w']) * 32) - left))
    def hits_pipe(x0, y0, x1, y1): return any(y1 >= py - 6 and y0 <= py + 28 and x1 >= a and x0 <= b for (py, a, b) in pipes)
    lamps, shafts = r.find_lights(left, top, m)
    doors = r.find_tunnels(left, m, top)
    lamps = [l for l in lamps if not r.hits_door(doors, l[0] - 8, (l[2] if l[2] is not None else l[1] - 26), l[0] + 8, l[1] + 8)]
    for (dcx, dfy, dr, dh) in doors:
        for side in ((1, -1) if r.hsh(dcx + left, 5) % 2 else (-1, 1)):
            lx = dcx + side * (dr + 48); ly = dfy - round(dh * .56)
            if not (8 < lx < WW - 8) or m[ly - 30:ly + 30, lx - 10:lx + 10].any(): continue
            if r.hits_door(doors, lx - 8, ly - 26, lx + 8, ly + 8) or hits_pipe(lx - 8, ly - 26, lx + 8, ly + 8): continue
            if any(abs(lx - a) < 150 and abs(ly - b) < 140 for a, b, _ in lamps): continue
            lamps.append((lx, ly, None)); break
    ows = r.oneways(left, top, m)
    def hits_deck(x0, y0, x1, y1, mg=16): return any(x1 >= x - mg and x0 <= x + Lw + mg and y1 >= y - mg and y0 <= y + 12 + mg for (x, y, Lw, _a, _b) in ows)
    lamps = [l for l in lamps if not hits_deck(l[0] - 8, (l[2] if l[2] is not None else l[1] - 26), l[0] + 8, l[1] + 8, 8)]
    lamps = [l for l in lamps if not hits_pipe(l[0] - 8, l[1] - 26, l[0] + 8, l[1] + 8)]
    waters, used = [], set()
    for w in r.find_water(left, top, m, lamps, 0, hero_x=None) if False else find_water_all(left, top, m, lamps):
        if r.hits_door(doors, w[0] - 20, w[1] - 24, w[0] + 20, w[2]) or hits_deck(w[0] - 20, w[1] - 24, w[0] + 20, w[2]) or hits_pipe(w[0] - 20, w[1] - 24, w[0] + 20, w[1] + 2): continue
        rid = room_of(w[0], w[2] - 4)
        if rid is None or rid in used or any(abs(w[0] - o[0]) < 300 and abs(w[2] - o[2]) < 200 for o in waters): continue
        used.add(rid); waters.append(w)
    # 방 밖(맵 테두리 벽 바깥, 보스 전투장 잘림 영역)에는 소품을 두지 않는다
    bx, by = layout['boss']['x'], layout['boss']['y'] - 3
    in_boss = lambda cx, cy: bx <= (cx + left) / 32 < bx + 30 and by <= (top - cy) / 32 < by + 18
    hall = layout.get('bossHall') or {'x': 0, 'y': 0, 'w': 0, 'h': 0}
    in_hall = lambda cx, cy: hall['x'] - 2 <= (cx + left) / 32 < hall['x'] + hall['w'] + 2 and hall['y'] - 1 <= (top - cy) / 32 < hall['y'] + hall['h'] + 2
    ok_at = lambda cx, cy: room_of(cx, cy) not in (None, 'B') and not in_boss(cx, cy) and not in_hall(cx, cy)
    lamps = [l for l in lamps if ok_at(l[0], l[1])]
    shafts = [s_ for s_ in shafts if ok_at(s_[0], s_[1] + 8)]
    doors = [d for d in doors if ok_at(d[0], d[1] - 8)]
    waters = [w for w in waters if ok_at(w[0], w[2] - 8)]
    keep = list(doors)
    for p in [layout['spawn']] + list(layout.get('actors', [])) + list(layout.get('ports', [])):
        keep.append((round(p['x'] * 32) - left, top - round(p['y'] * 32), 24, 0))
    up = np.where(m & ~np.roll(m, 1, axis=0), 0, 1).astype(np.int8)
    r.SELECT_ONLY = True; r.SPOTS_OVERRIDE = None
    spots = r.basins(None, None, m, up, left, top, None, None, waters, None, keep, None)
    r.SELECT_ONLY = False
    spots = [sp for sp in spots if ok_at((sp[1] + sp[2]) // 2, sp[0] - 8)]
    print(f'plan: lamps {len(lamps)} shafts {len(shafts)} doors {len(doors)} waters {len(waters)} pipes {len(pipes)} basins {len(spots)}')
    return dict(left=left, top=top, lamps=lamps, shafts=shafts, doors=doors, waters=waters, pipes=pipes, spots=spots,
                basins_world=[{'x0': round((a + left) / 32, 4), 'x1': round((b + left) / 32, 4), 'floorTop': round((top - y) / 32, 4)} for (y, a, b, f) in spots])

def find_water_all(left, top, m, lamps):
    """render_v26.find_water 의 후보 판정을 맵 전체에 쓴다(화면 PAD 가장자리 조건 대신 맵 안 전체)."""
    tops = m & ~np.roll(m, 1, axis=0); lab, _ = r.ndimage.label(tops); cands = []
    for sl in r.ndimage.find_objects(lab):
        ys, xs = sl; y = ys.start; x0, x1 = xs.start + 24, xs.stop - 24
        if x1 - x0 < 96 or y < 160: continue
        for sx in range(x0 + 24, x1 - 24, 16):
            oy = y - 118
            if oy < 8 or m[oy - 20:y - 1, max(0, sx - 30):sx + 30].any(): continue
            dl = min([abs(sx - a) + abs(oy - b) * .3 for a, b, _ in lamps] or [999])
            if dl < 90: continue
            cands.append((min(dl, 260) + (r.hsh(sx + left, 13) % 40), (sx, oy, y)))
    return [c[1] for c in sorted(cands, reverse=True)]

# ---------------------------------------------------------------- 2) 조각 그리기
def render_tile(P, gx, gy, tw, th):
    """맵 캔버스 안쪽 좌표 (gx, gy)부터 tw×th 를 그린다. 반환: 배경 RGB, 전경 RGBA, 물 앞면 RGBA."""
    set_canvas(tw, th); WW, HH = r.WW, r.HH
    left = P['left'] + gx; top = P['top'] - gy      # 조각 캔버스(여백 포함) 왼쪽·위 월드 화소
    ox, oy = gx, gy                                   # 전체 캔버스 → 조각 캔버스 이동량
    tr = lambda x, y: (x - ox, y - oy)
    near = lambda x, y, mg: -mg <= x - ox <= WW + mg and -mg <= y - oy <= HH + mg
    m = r.solid_mask(left, top)
    to_floor = dist_to_solid(m, False); to_ceil = dist_to_solid(m, True)
    r.DEPTH = np.clip(to_ceil / 220.0, 0, 1); r.FOG_T = np.clip(1 - to_floor / 140.0, 0, 1) * .22
    r.DUST_N = int(170 * (WW * HH) / (1152 * 732))
    lamps = [(*tr(x, y), (h - oy) if h is not None else None) for (x, y, h) in P['lamps'] if near(x, y, 300)]
    shafts = [tr(x, y) for (x, y) in P['shafts'] if near(x, y, 500)]
    doors = [(cx - ox, fy - oy, rr, hh) for (cx, fy, rr, hh) in P['doors'] if near(cx, fy, 400)]
    waters = [(sx - ox, wy - oy, fy - oy) for (sx, wy, fy) in P['waters'] if near(sx, fy, 300)]
    pipes = [(py - oy, a - ox, b - ox) for (py, a, b) in P['pipes']]
    spots = [(y - oy, a - ox, b - ox, (f - ox) if f is not None else None) for (y, a, b, f) in P['spots']
             if 8 <= a - ox and b - ox + 8 <= WW and 0 <= y - oy and y - oy + 40 <= HH]
    L, Wm = light_maps_windowed(lamps, shafts, HH, WW)
    for (wx_, wy0, fy) in waters:
        y0, y1 = max(0, wy0 - 50), min(HH, fy + 50); x0, x1 = max(0, wx_ - 50), min(WW, wx_ + 50)
        if y1 <= y0 or x1 <= x0: continue
        yy0, xx0 = np.mgrid[y0:y1, x0:x1]
        L[y0:y1, x0:x1] += np.clip(1 - np.hypot((xx0 - wx_) * 1.0, np.clip(np.abs(yy0 - (wy0 + fy) / 2) - (fy - wy0) / 2, 0, None)) / 46, 0, 1) * 1.1
    _, lvl = r.back_wall(left, top, L, Wm, m, 0)
    r.DOORS_OVERRIDE = doors
    lvl, _, _ = r.draw_props(lvl, left, top, m, 0, lamps, shafts, L, Wm)
    r.DOORS_OVERRIDE = None
    dmask = np.zeros(m.shape, bool)
    for (cx, fy, rr, hh) in doors: dmask[max(0, fy - hh - 10):max(0, fy), max(0, cx - rr - 10):max(0, cx + rr + 11)] = True
    for (py, a, b) in pipes:
        if not (0 < py < HH - 30): continue
        a, b = max(0, a), min(WW, b)
        if b <= a: continue
        band = slice(py, py + 18); rel = np.arange(18)[:, None]; xs = np.arange(a, b)[None, :] + left
        prof = np.where(rel < 2, 4.6, np.where(rel < 5, 4.0, np.where(rel > 15, 2.2, 3.3))) + (xs % 160 < 8) * .7 + ((xs % 160 == 4) & ((rel == 4) | (rel == 13))) * 1.4
        open_ = ~m[band, a:b] & ~dmask[band, a:b]
        lvl[band, a:b] = np.where(open_, prof + np.minimum(L[band, a:b], 6), lvl[band, a:b])
        sh = slice(py + 18, py + 22); lvl[sh, a:b] = np.where(~m[sh, a:b] & ~dmask[sh, a:b], lvl[sh, a:b] - 1.0, lvl[sh, a:b])
    dair = r.ndimage.distance_transform_cdt(~m, metric='chessboard').astype(np.float32)
    lvl = lvl - np.clip(1 - dair / 10, 0, 1) * 1.3; del dair
    yy, xx = np.mgrid[0:HH, 0:WW]
    wl = 26 + (r.hshv((xx + left) // 8) % 4) + (r.hshv((xx + left) // 40) % 6)
    wet = (~m) & (to_floor < wl)
    lvl = np.where(wet, lvl - .75, lvl)
    drip = wet & (r.hshv((xx + left) // 2) % 13 == 0) & (to_floor > wl - 3 - r.hshv((xx + left) // 2 + 7) % 14)
    lvl = np.where(drip, lvl - .5, lvl); del wet, drip, wl
    down = np.minimum(to_floor, 99); upd = np.minimum(to_ceil, 99)
    for pxw in range(((left - 400) // 288) * 288, left + WW + 288, 288):
        sx = pxw - left; a_, b_ = max(0, sx - 4), min(WW, sx + 32)
        if b_ <= a_: continue
        sub_m = ~m[:, a_:b_]; rel = np.arange(a_, b_)[None, :] - sx
        for msk, topline in ((sub_m & (down[:, a_:b_] <= 14), down[:, a_:b_] == 14), (sub_m & (upd[:, a_:b_] <= 10), upd[:, a_:b_] == 10)):
            v = 3.3 + (rel < 0) * .4 - (rel > 27) * .6 + topline * 1.0 + np.minimum(L[:, a_:b_], 6)
            lvl[:, a_:b_] = np.where(msk, v, lvl[:, a_:b_])
    bg = r.ramp(lvl, Wm, xx + left, yy - top); del lvl
    bg = r.fog(bg)
    bgim = Image.fromarray(bg.clip(0, 255).astype(np.uint8)).convert('RGBA'); del bg, yy, xx
    for (sx, sy) in shafts: r.draw_grate(bgim, sx, sy)
    ows = r.oneways(left, top, m)
    for (x, y, Lw, lw, rw) in ows:
        if not (lw or rw): r.chains_for(bgim, x, y, Lw, m)
        if lw: r.wall_bracket(bgim, x, y, 1)
        if rw: r.wall_bracket(bgim, x + Lw - 1, y, -1)
    a = np.array(bgim)
    for (x, y, Lw, lw, rw) in ows:
        ys, ye = max(0, y + 10), min(HH, y + 16); xs_, xe = max(0, x + 2), min(WW, x + Lw + 2)
        if ye > ys and xe > xs_: a[ys:ye, xs_:xe, :3] = (a[ys:ye, xs_:xe, :3] * .6).astype(np.uint8)
    bgim = Image.fromarray(a); del a
    for (lx, ly, hg) in lamps: r.draw_lamp(bgim, lx, ly, hg)
    for w in waters: r.draw_water(bgim, *w, left=left)
    bgim = r.dust(bgim, left, top, L)
    col, alpha, dist, up = r.terrain_B(left, top, m, L, Wm)
    r.drips(col, alpha, m, left, top, L); r.moss_tufts(col, alpha, m, left, top)
    front = np.zeros((HH, WW, 4), np.uint8)
    r.SPOTS_OVERRIDE = spots
    r.basins(col, alpha, m, up, left, top, L, Wm, waters, None, doors, front)
    r.SPOTS_OVERRIDE = None
    fg = Image.fromarray(np.dstack([col.clip(0, 255).astype(np.uint8), alpha])); del col, alpha, dist, up
    for (x, y, Lw, lw, rw) in ows:
        dk, T = r.stone_deck(x, y, Lw, lw, rw, L, Wm, left, top); fg.alpha_composite(dk, (x, y - T))
    box = (PAD, PAD, PAD + tw, PAD + th)
    return bgim.crop(box), fg.crop(box), Image.fromarray(front).crop(box)

def render_map(layout_path, out_dir, chunk=2048, margin_u=0):
    layout = json.loads(Path(layout_path).read_text(encoding='utf-8'))
    S = layout['surfaces']
    x0u = math.floor(min(s['x'] for s in S)) - margin_u; x1u = math.ceil(max(s['x'] + s['w'] for s in S)) + margin_u
    y0u = math.floor(min(s['y'] for s in S)) - margin_u; y1u = math.ceil(max(s['y'] + s['h'] for s in S)) + margin_u
    P = plan(layout, x0u, y0u, x1u, y1u)
    W, H = (x1u - x0u) * 32, (y1u - y0u) * 32
    out = Path(out_dir); out.mkdir(parents=True, exist_ok=True)
    small = Image.new('RGB', (W // 4, H // 4)); chunks = []
    for gy in range(0, H, chunk):
        for gx in range(0, W, chunk):
            t0 = time.time(); tw, th = min(chunk, W - gx), min(chunk, H - gy)
            bg, fg, fr = render_tile(P, gx, gy, tw, th)
            i, j = gx // chunk, gy // chunk
            wx, wy = (P['left'] + PAD + gx) / 32, (P['top'] - PAD - gy) / 32
            for layer, im in (('background', bg), ('foreground', fg), ('water-front', fr)):
                if layer != 'background' and im.getchannel('A').getbbox() is None: continue
                name = f'{layer}-{i:02d}-{j:02d}'; im.save(out / (name + '.png'))
                chunks.append({'name': name, 'layer': layer, 'x': wx, 'y': wy})
            comp = bg.copy(); comp.alpha_composite(fg); comp.alpha_composite(fr)
            small.paste(comp.convert('RGB').resize((tw // 4, th // 4), Image.NEAREST), (gx // 4, gy // 4))
            print(f'tile {i},{j} {tw}x{th} {time.time() - t0:.0f}s', flush=True)
    small.save(out / 'map-quarter.png')
    (out / 'manifest.json').write_text(json.dumps({'seed': 17, 'chunks': chunks, 'basins': P['basins_world']}, indent=1), encoding='utf-8')
    print('chunks', len(chunks), 'basins', len(P['basins_world']))

if __name__ == '__main__':
    render_map(sys.argv[1], sys.argv[2])
