# 잉크 결정 확정본(면만 + C 빛 쓸기)의 맞기·손상 단계·부서지기.
#  - 손상: damaged(f, d) — 1·2 는 갈라져 벌어진 결정(B안). 체력 66%·33% 아래에서 바꿔 끼우는 대기 고리 12장씩
#  - 맞기: 3장 50ms — 결정 화소만 하얗게 번쩍, 2px 밀렸다 돌아옴(그 순간의 손상 단계 그림 위에)
#  - 부서지기: 256×256 칸(결정 칸은 (80,64)) 14장 70ms — 부풀며 번쩍 → 귀퉁이 떨어짐 → 결정이 조각(보로노이 9개)으로 터져 떨어지고 잉크가 퍼진다
import math
import numpy as np
from PIL import Image
import crystal_v3 as c
import crystal_damage_options as dmgopt

def damaged(f, d):
    """손상 단계 그림: 0 멀쩡, 1·2 는 B안(갈라져 벌어짐). 11-12 사용자 선택."""
    return c.frame(f, 'C', 'none') if d == 0 else dmgopt.opt_B(f, d)

OUT = c.OUT / 'final-E3'
V, INK = c.V, c.INK
rng = np.random.default_rng(7)

def crystal_only(im):
    """빛 번짐·그림자를 뺀 결정 화소(알파 200 넘는 곳)만."""
    a = np.array(im); keep = a[..., 3] > 200; a[~keep] = 0; return a

def tint(a, k, col=V[7]):
    m = a[..., 3] > 0; out = a.copy()
    out[m, :3] = (a[m, :3] * (1 - k) + np.array(col) * k).astype(np.uint8); return out

def hit_frames(damage):
    base = damaged(6, damage)                           # 빛 쓸기 없는 칸
    glow = np.array(base); glow[np.array(base)[..., 3] > 200] = 0
    body = crystal_only(base); out = []
    for k, dx in ((.85, 0), (.45, 2), (.15, -1)):
        im = Image.fromarray(glow); b = Image.fromarray(tint(body, k)); im.alpha_composite(b, (dx, 0)); out.append(im)
    return out

def break_frames():
    BW, BH, OX, OY = 256, 256, 80, 64                   # 결정 칸(96×128)을 가운데 위에, 아래 64px 는 조각이 떨어질 자리
    base = crystal_only(damaged(6, 2))
    H_, W_ = base.shape[:2]; cx, cy = 48, 70
    mask = base[..., 3] > 0
    # 보로노이로 조각 9개
    seeds = np.array([(cx + rng.integers(-14, 15), cy + rng.integers(-40, 41)) for _ in range(9)], float)
    yy, xx = np.mgrid[0:H_, 0:W_]
    lab = np.argmin(((xx[..., None] - seeds[:, 0]) ** 2 + (yy[..., None] - seeds[:, 1]) ** 2), -1)
    shards = []
    for i in range(len(seeds)):
        m = mask & (lab == i)
        if m.sum() < 12: continue
        ys, xs = np.nonzero(m); x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        piece = np.zeros((y1 - y0, x1 - x0, 4), np.uint8); piece[m[y0:y1, x0:x1]] = base[y0:y1, x0:x1][m[y0:y1, x0:x1]]
        piece = c.outline(piece.copy()) if False else piece
        pc_ = (xs.mean(), ys.mean()); d = np.array(pc_) - (cx, cy); d = d / (np.linalg.norm(d) + 1e-3)
        vel = d * rng.uniform(3, 5) + (0, -rng.uniform(3.5, 5.5)); spin = rng.uniform(-35, 35)
        shards.append(dict(img=Image.fromarray(piece), at=np.array((x0, y0), float), vel=vel, spin=spin))
    frames = []
    for f in range(14):
        im = Image.new('RGBA', (BW, BH))
        if f < 3:                                        # 부풀며 번쩍
            k = (.3, .65, .95)[f]; s = (1.0, 1.04, 1.08)[f]
            b = Image.fromarray(tint(base, k)); b = b.resize((round(W_ * s), round(H_ * s)), Image.NEAREST)
            im.alpha_composite(b, (OX + round(W_ * (1 - s) / 2), OY + round(H_ * (1 - s) / 2)))
        else:
            t = f - 3
            # 잉크 퍼짐: 들쭉날쭉한 덩이가 커졌다 체크 디더로 사라짐
            r = 10 + 34 * (1 - (1 - min(1, t / 5)) ** 2)
            by, bx = np.mgrid[0:BH, 0:BW]; ang = np.arctan2(by - (OY + cy), bx - (OX + cx))
            rr = r * (1 + .16 * np.sin(ang * 3 + 1.3) + .12 * np.sin(ang * 7 + .4) + .08 * np.sin(ang * 13 + 2.1))
            dist = np.hypot(bx - (OX + cx), (by - (OY + cy)) * 1.15)
            fade = max(0, 1 - t / 10)
            ink = (dist < rr) & (((bx + by) % 2 == 0) | (fade > .55))
            inner = dist < rr * .62
            arr = np.zeros((BH, BW, 4), np.uint8)
            if fade > 0:
                arr[ink] = (*V[1], round(200 * fade)); arr[ink & inner] = (*INK, round(220 * fade))
            im.alpha_composite(Image.fromarray(arr))
            for sh in shards:                              # 조각: 바깥으로 튀고 떨어지며 돈다
                pos = sh['at'] + sh['vel'] * t * 2.2 + np.array((0, .55 * (t * 2.2) ** 2 / 2.2))
                p = sh['img'].rotate(sh['spin'] * t, expand=True, resample=Image.NEAREST)
                p = Image.fromarray(c.outline(np.array(p)))
                if t >= 8: pa = np.array(p); pa[..., 3] = (pa[..., 3] * max(0, 1 - (t - 7) / 4)).astype(np.uint8); p = Image.fromarray(pa)
                im.alpha_composite(p, (round(OX + pos[0] - (p.width - sh['img'].width) / 2), round(OY + pos[1] - (p.height - sh['img'].height) / 2)))
            if t == 0:                                     # 터지는 순간: 결정 모양 그대로 하얗게 부푼 섬광
                from scipy.ndimage import binary_dilation
                sil = np.zeros((BH, BW), bool); sil[OY:OY + H_, OX:OX + W_] = mask
                big = binary_dilation(sil, iterations=3)
                fl = np.zeros((BH, BW, 4), np.uint8)
                fl[big] = (*V[7], 255)
                im.alpha_composite(Image.fromarray(fl))
        frames.append(im)
    return frames

def save_gif(frames, path, ms, scale=3, bg=(13, 20, 24, 255)):
    g = []
    for fr in frames:
        b = Image.new('RGBA', (fr.width * scale, fr.height * scale), bg); b.alpha_composite(fr.resize((fr.width * scale, fr.height * scale), Image.NEAREST)); g.append(b.convert('RGB'))
    g[0].save(path, save_all=True, append_images=g[1:], duration=ms, loop=0)

if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    for dmg in (1, 2):
        fr = [damaged(i, dmg) for i in range(c.N)]
        for i, im in enumerate(fr): im.save(OUT / f'machine-crystal-damage{dmg}-{i:02d}.png')
        save_gif(fr, OUT / f'machine-crystal-damage{dmg}-3x.gif', 85)
    for dmg in (0, 1, 2):
        hf = hit_frames(dmg)
        for i, im in enumerate(hf): im.save(OUT / f'machine-crystal-hit{dmg}-{i:02d}.png')
    bf = break_frames()
    for i, im in enumerate(bf): im.save(OUT / f'machine-crystal-break-{i:02d}.png')
    save_gif(bf, OUT / 'machine-crystal-break-3x.gif', 70)
    print('states ok', len(bf))
