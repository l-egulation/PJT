# 잉크 결정 손상 단계 시안. 「귀퉁이 들쭉날쭉 떨어짐」(1안)이 싸 보인다는 말을 듣고 다른 표현 넷.
#  A 깔끔하게 잘린 면 · B 갈라져 벌어짐 · C 속에 잉크 번짐 · D 빛이 꺼져 감
# 모두 확정 대기 그림(crystal_v3.frame(f,'C','none'))을 받아 결정 화소만 고친다. 결정 밖에 떠 있는 것·얇은 선은 넣지 않는다.
import math
import numpy as np
from PIL import Image
import crystal_v3 as c

V, INK, N = c.V, c.INK, c.N
OUT = c.OUT / 'damage-options'

def split(im):
    a = np.array(im); body = a[..., 3] > 200
    glow = a.copy(); glow[body] = 0
    ink = body & (np.abs(a[..., :3].astype(int) - INK).sum(-1) < 6)
    core = a.copy(); core[~(body & ~ink)] = 0          # 먹 테두리를 뗀 결정 속살
    return glow, core

def outlined(core):
    return c.outline(core.copy())

def blend(a, m, col, k):
    a[m, :3] = (a[m, :3] * (1 - k) + np.array(col) * k).astype(np.uint8)

def bob_of(f): return round(2 * math.sin(f / N * 2 * math.pi))

def side(xx, yy, p, q):
    """선분 p→q 의 어느 쪽인지(부호)와 그 선까지 거리."""
    (x0, y0), (x1, y1) = p, q; dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy)
    return ((xx - x0) * dy - (yy - y0) * dx) / L

def compose(glow, layers, glow_k=1.0):
    g = glow.copy(); g[..., 3] = (g[..., 3] * glow_k).astype(np.uint8)
    out = Image.fromarray(g)
    for (arr, (dx, dy)) in layers: out.alpha_composite(Image.fromarray(arr), (dx, dy))
    return out

# ---------------------------------------------------------------- A 깔끔하게 잘린 면
def opt_A(f, stage):
    glow, core = split(c.frame(f, 'C', 'none')); cy = 70 + bob_of(f); cx = 48
    H, W = core.shape[:2]; yy, xx = np.mgrid[0:H, 0:W]
    cuts = [((cx - 1, cy - 50), (cx + 19, cy + 2), V[6])]                      # 오른쪽 위를 깊게 비스듬히
    if stage >= 2: cuts += [((cx - 19, cy - 4), (cx + 3, cy + 50), V[4]),       # 왼쪽 아래
                            ((cx - 18, cy - 33), (cx + 12, cy - 41), V[7])]     # 꼭대기를 평평하게
    alive = core[..., 3] > 0
    def keep(p, q):                                     # 결정 가운데가 있는 쪽을 남긴다
        sc = 1 if side(cx, cy, p, q) > 0 else -1; return side(xx, yy, p, q) * sc
    for (p, q, col) in cuts: alive &= keep(p, q) > 0
    core[~alive] = 0
    for (p, q, col) in cuts:                            # 잘린 면: 선 쪽으로 4px 넓은 밝은 면
        d = keep(p, q); blend(core, alive & (d < 6.5), col, .7)
    if stage >= 2: blend(core, alive, V[1], .15)
    return compose(glow, [(outlined(core), (0, 0))], 1 if stage < 2 else .7)

# ---------------------------------------------------------------- B 갈라져 벌어짐
def opt_B(f, stage):
    glow, core = split(c.frame(f, 'C', 'none')); cy = 70 + bob_of(f); cx = 48
    H, W = core.shape[:2]; yy, xx = np.mgrid[0:H, 0:W]
    alive = core[..., 3] > 0
    breath = round(math.sin(f / N * 2 * math.pi * 2) * .6 + .4)               # 틈이 아주 조금 숨쉬듯
    # 1단계 금은 2단계에도 같은 자리·같은 벌어짐으로 남고, 2단계는 아래 조각에 새 금이 하나 더 난다
    d = side(xx, yy, (cx - 20, cy - 2), (cx + 20, cy + 12))
    upper, lower = alive & (d > 0), alive & (d <= 0)
    if stage == 1:
        parts = [(upper, (-2, -3 - breath)), (lower, (2, 2))]
    else:
        p, q = (cx - 20, cy + 30), (cx + 20, cy + 20)              # 새 금: 반대로 기운 선
        d2 = side(xx, yy, p, q) * (1 if side(cx, cy + 44, p, q) > 0 else -1)   # 아래 끝 쪽이 +
        parts = [(upper, (-2, -3 - breath)), (lower & (d2 <= 0), (2, 2)), (lower & (d2 > 0), (0, 7 + breath))]
    layers = []
    for (m, off) in parts:
        piece = core.copy(); piece[~m] = 0
        # 쪼개진 단면 쪽 2px 를 한 단 밝게(새로 드러난 면)
        layers.append((outlined(piece), off))
    if stage >= 2:
        for (arr, _) in layers: blend(arr, arr[..., 3] > 0, V[1], .12)
    return compose(glow, layers, 1 if stage < 2 else .75)

# ---------------------------------------------------------------- C 속에 잉크 번짐
def opt_C(f, stage):
    glow, core = split(c.frame(f, 'C', 'none')); cy = 70 + bob_of(f); cx = 48
    H, W = core.shape[:2]; yy, xx = np.mgrid[0:H, 0:W]
    ph = f / N * 2 * math.pi
    alive = core[..., 3] > 0
    # 아래 끝에서 차오르는 잉크 + 가운데에서 번지는 얼룩, 가장자리가 천천히 일렁인다
    top = (cy + 26 if stage == 1 else cy - 4) + 2.5 * np.sin(xx * .35 + ph) + 1.5 * np.sin(xx * .9 - ph * 2)
    stain = alive & (yy > top)
    ang = np.arctan2(yy - (cy - 6), xx - (cx + 3)); r0 = 7 if stage == 1 else 13
    rr = r0 * (1 + .25 * np.sin(ang * 3 + ph) + .15 * np.sin(ang * 5 - ph))
    if stage >= 2: stain |= alive & (np.hypot(xx - (cx + 3), (yy - (cy - 6)) * .8) < rr)
    blend(core, stain, V[0], .62); blend(core, stain & (xx < cx - 6), V[2], .18)  # 잉크 속에도 면 구분이 남게
    return compose(glow, [(outlined(core), (0, 0))], 1 if stage < 2 else .7)

# ---------------------------------------------------------------- D 빛이 꺼져 감
def opt_D(f, stage):
    glow, core = split(c.frame(f, 'C' if stage < 2 else 'A', 'none'))
    alive = core[..., 3] > 0
    flick = {1: (5, 9), 2: (2, 4, 8, 9)}[stage]
    k = (.28 if stage == 1 else .5) + (.18 if f in flick else 0)
    blend(core, alive, V[0], k)
    gk = (.6 if stage == 1 else .3) * (.4 if f in flick else 1)
    return compose(glow, [(outlined(core), (0, 0))], gk)

OPTS = [('A', '깔끔하게 잘린 면', opt_A), ('B', '갈라져 벌어짐', opt_B), ('C', '속에 잉크 번짐', opt_C), ('D', '빛이 꺼져 감', opt_D)]

if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for key, _, fn in OPTS:
        for st in (1, 2):
            for i in range(N): fn(i, st).save(OUT / f'{key}-stage{st}-{i:02d}.png')
    print('damage options ok')
