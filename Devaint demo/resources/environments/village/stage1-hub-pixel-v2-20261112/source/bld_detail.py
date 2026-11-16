"""Detail pass on the user's hand-finished buildings (04_테스트/마을 건물 픽셀 규격/사용자다듬음_*.png).
Only adds, on the user's own pixels and palette: voussoir joints in the stone arches, iron hinge straps and lit plank
edges on the doors, cast shadows under sills, a couple of bare-brick patches where the plaster has fallen, lit tops on
stones. One-dot lines only, no scattered dots. Usage: bld_detail.py"""
import math
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage
from px import *
from hub_bld import SK, CO
from bld_user_sketch import material_map

D = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/04_테스트/마을 건물 픽셀 규격")
OUT = Path(__file__).parent.parent / "bld2"


def load(name):
    im = Image.open(D / name).convert('RGBA')
    a = np.asarray(im)
    mm = material_map()
    key = np.full(a.shape[:2], '', dtype=object)
    for y in range(a.shape[0]):
        for x in range(a.shape[1]):
            if a[y, x, 3]: key[y, x] = mm['%02x%02x%02x' % tuple(a[y, x, :3])]
    return key


def paint_keys(key, P):
    H, W = key.shape
    c = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            if key[y, x]: c.px(x, y, P[key[y, x]])
    return c


STONES = ('stone', 'stone_hi', 'stone_lo')


def openings(key, mats, min_size=40, zone=None):
    """boxes (x0, x1, top, bottom) of the openings made of the given materials (glass panes merged with their cross)"""
    m = np.isin(key, mats)
    if zone: z = np.zeros_like(m); z[zone[2]:zone[3] + 1, zone[0]:zone[1] + 1] = True; m &= z
    # close the cross / plank lines so one opening is one blob
    m2 = ndimage.binary_closing(m | (np.isin(key, ('wood', 'wood_lo', 'wood_hi', 'in')) & ndimage.binary_dilation(m, iterations=2) & (zone is None or True)), iterations=1)
    lab, n = ndimage.label(m2)
    out = []
    for i in range(1, n + 1):
        b = (lab == i) & m
        if b.sum() < min_size: continue
        ys, xs = np.nonzero(b)
        out.append((xs.min(), xs.max(), ys.min(), ys.max()))
    return out


def arch_joints(key, box, max_ring=5):
    """radial joints across the stone ring of an arched opening (every ~30°), straight joints down its jambs"""
    x0, x1, top, bottom = box
    H, W = key.shape
    cx = (x0 + x1 + 1) / 2; r = (x1 - x0 + 1) / 2; cy = top + r
    ring = np.isin(key, STONES)
    for deg in (30, 60, 90, 120, 150):
        t = math.radians(deg)
        pts = []
        for k in np.arange(r + .6, r + max_ring + 1.5, .5):
            p = (int(math.floor(cx + k * math.cos(t))), int(math.floor(cy - k * math.sin(t))))
            if p not in pts: pts.append(p)
        run = [p for p in pts if 0 <= p[0] < W and 0 <= p[1] < H and ring[p[1], p[0]]]
        for x, y in run: key[y, x] = 'in'
    # jambs: a joint every 7 rows from the springing line down
    for y in range(int(cy) + 5, bottom - 2, 7):
        for side in (-1, 1):
            x = (x0 - 1) if side < 0 else (x1 + 1)
            run = []
            while 0 <= x < W and key[y, x] in ('in',) + STONES and len(run) < max_ring + 2:
                run.append(x); x += side
            for xx in run:
                if key[y, xx] in STONES: key[y, xx] = 'in'


def arch_e(x0, x1, top, spring, bottom, grow=0):
    """an arched opening: straight jambs from the springing row down, a half-ellipse above it"""
    cx = (x0 + x1 + 1) / 2; rx = (x1 - x0 + 1) / 2 + grow; ry = spring - top + grow
    m = {(x, y) for x in range(x0 - grow, x1 + grow + 1) for y in range(spring, bottom + 1)}
    m |= {(x, y) for x in range(int(cx - rx) - 1, int(cx + rx) + 2) for y in range(top - grow, spring)
          if ((x + .5 - cx) / rx) ** 2 + ((y + .5 - spring) / ry) ** 2 <= 1}
    return smooth_mask(m)


def clean_arch(key, x0, x1, top, spring, bottom, ring=2, step=4.5):
    """redraw an arched stone ring as clean curves of the same size: inner ink edge (x0..x1, top), the ring of
    stones, the outer ink edge; voussoir joints evenly along the curve, jamb joints every seven rows"""
    H, W = key.shape
    inner = arch_e(x0, x1, top, spring, bottom); outer = arch_e(x0, x1, top, spring, bottom, ring + 1)
    e_in = rings(inner)[0]; e_out = rings(outer)[0]
    band = outer - inner
    interior = inner - e_in
    def maj(p, region, avoid):
        nb = [key[j, i] for j in range(p[1] - 3, p[1] + 4) for i in range(p[0] - 3, p[0] + 4)
              if 0 <= j < H and 0 <= i < W and (i, j) in region and key[j, i] not in avoid and key[j, i]]
        return max(set(nb), key=nb.count) if nb else None
    old = ('in',) + STONES
    # around the curve: old ring dots outside the new ring become wall, inside become the opening's own fill
    for y in range(max(0, top - ring - 4), min(H, spring + 2)):
        for x in range(max(0, x0 - ring - 4), min(W, x1 + ring + 5)):
            p = (x, y)
            if p in outer or key[y, x] not in old: continue
            near = any((x + dx, y + dy) in outer for dx in range(-3, 4) for dy in range(-3, 4))
            if near:
                outside = [key[j, i] for j in range(y - 3, y + 4) for i in range(x - 3, x + 4)
                           if 0 <= j < H and 0 <= i < W and (i, j) not in outer and key[j, i] in ('plaster', 'plaster_lo', 'plaster_dk')]
                if outside: key[y, x] = max(set(outside), key=outside.count)
    for x, y in interior:
        if y < spring + 2 and key[y, x] in old:
            v = maj((x, y), interior, old)
            if v: key[y, x] = v
    fill_ = [key[y, x] for x, y in band if key[y, x] in STONES]
    stone = max(set(fill_), key=fill_.count) if fill_ else 'stone'
    for x, y in band:
        if y < bottom: key[y, x] = stone
    for x, y in e_in | e_out:
        if 0 <= x < W and 0 <= y < bottom: key[y, x] = 'in'          # the foot row stays as drawn (ground line)
    # voussoir joints: straight across the ring, spaced evenly along the curve
    cx = (x0 + x1 + 1) / 2; rx = (x1 - x0 + 1) / 2 + ring / 2 + .5; ry = spring - top + ring / 2 + .5
    pts = [(cx + rx * math.cos(math.radians(d)), spring - ry * math.sin(math.radians(d))) for d in np.arange(0, 180.01, .25)]
    acc, last = 0, None
    marks = []
    for p in pts:
        if last is not None: acc += math.dist(p, last)
        last = p
        if acc >= step: marks.append(p); acc = 0
    for mx, my in marks[:-1] if len(marks) > 1 else marks:
        if my > spring - 1: continue
        dx, dy = (mx - cx) / rx ** 2, (my - spring) / ry ** 2          # normal of the ellipse
        n = math.hypot(dx, dy); dx, dy = dx / n, dy / n
        for k in np.arange(-ring, ring + .1, .5):
            q = (int(math.floor(mx + k * dx)), int(math.floor(my + k * dy)))
            if q in band and q not in e_in and q not in e_out: key[q[1], q[0]] = 'in'
    for y in range(spring + 5, bottom - 2, 7):
        for x in list(range(x0 - ring, x0)) + list(range(x1 + 1, x1 + ring + 1)):
            if (x, y) in band: key[y, x] = 'in'


def perfect_outline(mask):
    """the 4-connected edge of a mask with every L-corner dot taken out: a one-dot, 8-connected outline
    (pixel-perfect curve); returns (outline, removed corner dots)"""
    edge = {p for p in mask if any((p[0] + dx, p[1] + dy) not in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
    removed = set()
    changed = True
    while changed:
        changed = False
        for p in sorted(edge):
            x, y = p
            for hx in (-1, 1):
                for vy in (-1, 1):
                    if (x + hx, y) in edge and (x, y + vy) in edge and (x - hx, y) not in edge and (x, y - vy) not in edge:
                        edge.discard(p); removed.add(p); changed = True
                        break
                else: continue
                break
    return edge, removed


def clean_arch_c(key, x0, x1, top, bottom, ring=2, joints=7):
    """an arched stone ring drawn the pixel-art way: two concentric circles (inner edge through columns x0, x1 and
    row top; outer edge ring+1 dots further out), each a one-dot pixel-perfect curve, straight jambs below the
    springing row, the ring filled with stone, an odd number of evenly spaced radial joints (keystone at the crown),
    jamb joints every seven rows. The opening keeps its own fill; old ring dots outside go back to wall."""
    import pixel_curves as PC
    H, W = key.shape
    Wd = x1 - x0 + 1; g = ring + 1
    cx = (x0 + x1) / 2; r = (x1 - x0) / 2; cy = top + (Wd - 1) / 2; R = r + g
    spring = top + (Wd - 1) // 2                      # last row of the round part
    def arch(xa, xb, ya):
        ringp, ins = PC.ellipse_ring(xa, ya, xb, ya + (xb - xa))
        ringp = {p for p in ringp if p[1] <= spring}
        ins = {p for p in ins if p[1] <= spring}
        jamb = {(xa, y) for y in range(spring + 1, bottom)} | {(xb, y) for y in range(spring + 1, bottom)}
        body = {(x, y) for x in range(xa + 1, xb) for y in range(spring + 1, bottom + 1)}
        return ringp | jamb, ins | body
    e_in, inner_in = arch(x0, x1, top)
    e_out, outer_in = arch(x0 - g, x1 + g, top - g)
    inner = e_in | inner_in
    outer = e_out | outer_in
    cut_out = set()
    band = (outer - inner) | cut_out
    band -= e_out
    interior = inner - e_in
    old = ('in',) + STONES
    # old ring dots outside the new ring go back to wall
    for y in range(max(0, int(cy - R) - 4), bottom):
        for x in range(max(0, int(cx - R) - 4), min(W, int(cx + R) + 5)):
            if (x, y) in outer or key[y, x] not in old: continue
            near = any((x + dx, y + dy) in outer for dx in range(-3, 4) for dy in range(-3, 4))
            if near:
                wall = [key[j, i] for j in range(y - 3, y + 4) for i in range(x - 3, x + 4)
                        if 0 <= j < H and 0 <= i < W and (i, j) not in outer and key[j, i] in ('plaster', 'plaster_lo', 'plaster_dk')]
                if wall: key[y, x] = max(set(wall), key=wall.count)
    # inside the opening: old ring / ink dots near the curve take the opening's own fill
    near_in = set()
    for x, y in e_in:
        near_in |= {(x + dx, y + dy) for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2)}
    for x, y in interior & near_in:
        if key[y, x] in old and y <= cy + 1:
            nb = [key[j, i] for j in range(y - 3, y + 4) for i in range(x - 3, x + 4)
                  if 0 <= j < H and 0 <= i < W and (i, j) in interior and key[j, i] not in old and key[j, i]]
            if nb: key[y, x] = max(set(nb), key=nb.count)
    fill_ = [key[y, x] for x, y in band if key[y, x] in STONES]
    stone = max(set(fill_), key=fill_.count) if fill_ else 'stone'
    for x, y in band:
        if y < bottom: key[y, x] = stone
    for x, y in e_in | e_out: key[y, x] = 'in'
    # joints that stay clean on a two-dot ring: a keystone on the crown (two upright joints on the flat top run)
    # and level joints down the jambs at one spacing
    mid = (x0 + x1) // 2
    kx = (mid - 2, mid + 2) if (x0 + x1) % 2 == 0 else (mid - 1, mid + 2)
    for x in kx:
        for y in range(top - g + 1, top):
            if (x, y) in band: key[y, x] = 'in'
    for y in range(spring + 1, bottom - 2, 6):
        for x in list(range(x0 - g + 1, x0)) + list(range(x1 + 1, x1 + g)):
            if (x, y) in band: key[y, x] = 'in'


def door_detail(key, box):
    """iron strap hinges (ink, iron, ink) across the leaf from the hinge side, lit edge on each plank"""
    x0, x1, top, bottom = box
    w = x1 - x0 + 1
    planks = [x for x in range(x0 + 1, x1) if sum(key[y, x] == 'wood_lo' for y in range(top + 8, bottom)) > (bottom - top) * .5]
    for x in planks:
        for y in range(top, bottom + 1):
            if key[y, x + 1] == 'wood' and key[y, x] == 'wood_lo': key[y, x + 1] = 'wood_hi'
    for yy in (top + 9, bottom - 9):
        end = x0 + int(w * .62)
        for x in range(x0, end + 1):
            if key[yy - 1, x] in ('wood', 'wood_hi', 'wood_lo'): key[yy - 1, x] = 'in'
            if key[yy, x] in ('wood', 'wood_hi', 'wood_lo'): key[yy, x] = 'metal_hi' if x < x0 + 3 else 'metal'
            if key[yy + 1, x] in ('wood', 'wood_hi', 'wood_lo'): key[yy + 1, x] = 'in'
        key[yy, end + 1] = 'in'


def sill_shadow(key, box):
    """the wall under a window sill takes a one-row shadow as wide as the sill"""
    x0, x1, top, bottom = box
    H, W = key.shape
    # find the sill's bottom ink row under the opening
    for y in range(bottom + 1, min(H - 1, bottom + 9)):
        row = [x for x in range(x0 - 6, x1 + 7) if 0 <= x < W and key[y, x] == 'in']
        if len(row) > (x1 - x0) and key[y + 1, (x0 + x1) // 2] == 'plaster':
            for x in range(min(row), max(row) + 1):
                if key[y + 1, x] == 'plaster': key[y + 1, x] = 'plaster_lo'
            return


BRICKS = ["...oooooooooo...",
          "...oHHHHoHHHHo..",
          "...oSSSSoSSSSo..",
          "oooooooooooooooo",
          "oHHoHHHHoHHHHo..",
          "oSSoSSSSoSSSSo..",
          "ooooooooooooooo.",
          "..oHHHHoHHHHo...",
          "..oSSSSoSSSSo...",
          "..ooooooooooo..."]


def brick_patch(key, x, y, flip=False):
    """bare bricks where the plaster has fallen, with a shadowed broken edge below and to the right"""
    rows = [r[::-1] for r in BRICKS] if flip else BRICKS
    done = set()
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch == '.' or key[y + j, x + i] not in ('plaster', 'plaster_lo', 'plaster_dk'): continue
            key[y + j, x + i] = {'o': 'in', 'H': 'stone_hi', 'S': 'stone'}[ch]; done.add((x + i, y + j))
    for px_, py in done:
        for dx, dy in ((1, 0), (0, 1), (1, 1)):
            q = (px_ + dx, py + dy)
            if q not in done and key[q[1], q[0]] == 'plaster': key[q[1], q[0]] = 'plaster_lo'


def lit_tops(key, mats=('stone',), hi='stone_hi'):
    """a stone whose top row sits right under ink gets that row lit (only where the row is still the plain colour)"""
    H, W = key.shape
    for y in range(1, H):
        for x in range(W):
            if key[y, x] in mats and key[y - 1, x] == 'in' and (y + 1 < H and key[y + 1, x] in mats):
                key[y, x] = hi


def undo_clumps(key, orig):
    """any 2x2 block of ink that an added dot made goes back to what the user drew there"""
    H, W = key.shape
    for _ in range(3):
        for y in range(H - 1):
            for x in range(W - 1):
                blk = [(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)]
                if all(key[b] == 'in' for b in [(p[1], p[0]) for p in blk]):
                    for px_, py in blk:
                        if orig[py, px_] != 'in': key[py, px_] = orig[py, px_]; break


def house():
    key = load('사용자다듬음_기록농가_130x162.png')
    orig = key.copy()
    wins = openings(key, ('glass',))
    door = openings(key, ('wood', 'wood_lo'), zone=(30, 70, 110, 161), min_size=200)
    print('windows', wins, 'door', door)
    for b in wins: sill_shadow(key, b)
    for b in door: door_detail(key, b)
    for x0, x1, top, bottom in ((33, 58, 54, 76), (86, 102, 58, 80), (86, 102, 118, 139), (32, 59, 118, 161)):
        clean_arch_c(key, x0, x1, top, bottom)
    brick_patch(key, 96, 96); brick_patch(key, 65, 134, flip=True)
    undo_clumps(key, orig)
    return key


def palette_grain(key):
    """wood grain on the giant palette: two broken arcs following its rim, never touching ink or paint"""
    m = key == 'wood_hi'
    lab, n = ndimage.label(m)
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    board = lab == (np.argmax(sizes) + 1)
    ys, xs = np.nonzero(board)
    cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
    rx, ry = (xs.max() - xs.min()) / 2, (ys.max() - ys.min()) / 2
    H, W = key.shape
    for scale, a0, a1 in ((.86, -20, 70), (.86, 110, 160), (.7, 10, 55), (.7, 200, 240)):
        prev = None
        for deg in np.arange(a0, a1, .5):
            t = math.radians(deg)
            p = (int(round(cx + scale * rx * math.cos(t))), int(round(cy + scale * ry * math.sin(t))))
            if p == prev: continue
            prev = p
            x, y = p
            if not (0 < x < W - 1 and 0 < y < H - 1) or not board[y, x]: continue
            if any(key[y + dy, x + dx] not in ('wood_hi', 'wood') for dx in (-1, 0, 1) for dy in (-1, 0, 1)): continue
            key[y, x] = 'wood'


def atelier():
    key = load('사용자다듬음_팔레트공방_158x203.png')
    orig = key.copy()
    wins = openings(key, ('glass',))
    door = openings(key, ('wood', 'wood_lo'), zone=(45, 90, 140, 202), min_size=200)
    print('windows', wins, 'door', door)
    for b in door: door_detail(key, b)
    for x0, x1, top, bottom in ((95, 116, 144, 170), (45, 77, 145, 202)):
        clean_arch_c(key, x0, x1, top, bottom)
    palette_grain(key)
    undo_clumps(key, orig)
    return key


if __name__ == '__main__':
    for name, fn, src in (('house', house, '사용자다듬음_기록농가_130x162.png'), ('atelier', atelier, '사용자다듬음_팔레트공방_158x203.png')):
        key = fn()
        for v, P in (('color', CO), ('sketch', SK)):
            c = paint_keys(key, P); lint(c, colors=[K, P['in']], name=f'{name} {v}')
            c.im.save(OUT / f'd-{name}-{v}.png')
        o = Image.open(D / src).convert('RGBA'); n = Image.open(OUT / f'd-{name}-color.png')
        S = 3; s = Image.new('RGBA', (o.width * S * 2 + 30, o.height * S), (244, 241, 233, 255))
        s.alpha_composite(o.resize((o.width * S, o.height * S), 0), (0, 0)); s.alpha_composite(n.resize((n.width * S, n.height * S), 0), (o.width * S + 30, 0))
        s.save(OUT / f'd-{name}-cmp.png')
