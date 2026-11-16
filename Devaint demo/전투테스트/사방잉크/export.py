# 처치 폭발(ink3_red_05) 처치 폭발 이펙트를 스프라이트 시트(투명 PNG + Aseprite 형식 JSON)로 뽑는다.
#   python 전투테스트/사방잉크/export.py [--out outputs/pixelfx/ink-splash] [--preview]
# 다른 잉크 파츠 시트와 같은 규격(PPU32, 오른쪽 기준, meta.pixelfx)이다. 같은 모양을 독폭탄용 보라 판으로도 뽑는다.
# 판마다 실제 반경 크기로 그려 Unity 에서 키우거나 줄이지 않는다(화소 크기가 다른 이펙트와 같다).
# 난수는 고정 시드라 다시 뽑아도 같은 바이트가 나온다.
import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
PALETTES = {
    # 어두운 테 → 빨강 → 밝은 빨강 → 흰 심(표식 격발 red 와 같은 값)
    "red": ["#5a1d16", "#9e3424", "#d8664c", "#f09a7c", "#fff3e6"],
    # 표식 격발 purple 과 같은 값
    "purple": ["#3d1f57", "#64348c", "#9a5cc8", "#cfa2ec", "#fbf2ff"],
}

# 판마다 반경(게임 unit)이 다르다. 처치 폭발 = 피해 반경 6, 독폭탄 = 몬스터 한 마리에 터지는 반경 3.2
RADII = {"red": 6.0, "purple": 3.2}
PPU, FLOOR = 32, 28       # 바닥 = 피벗(몬스터 몸통 가운데) 아래 28px(다른 잉크 시트와 같다)
FPS, COLUMNS = 25, 4


def clamp(v, a=0.0, b=1.0):
    return max(a, min(b, v))


def out_cubic(u):
    u = clamp(u)
    return 1 - (1 - u) ** 3


def smooth(a, b, v):
    u = clamp((v - a) / (b - a))
    return u * u * (3 - 2 * u)


class Splash:
    """반경 RADIUS(px)에 맞춘 한 판. 반경 2(64px)를 기준 모양으로 두고 크기·개수·시간을 늘린다."""

    def __init__(self, radius_units):
        self.RADIUS = R = radius_units * PPU
        k = R / 64                       # 기준 대비 거리 배율
        self.s = s = math.sqrt(k)        # 덩어리·방울 굵기 배율(거리보다 덜 키워 잘게 튀어 보이게)
        self.ts = 1 + 0.1 * (k - 1)      # 넓을수록 조금 더 오래 퍼진다
        self.k = k
        margin = round(0.14 * R) + 16
        self.PX = self.PY = margin + round(R)
        self.W, self.H = 2 * self.PX, self.PY + FLOOR + 12
        self.FRAMES = math.ceil(12 * self.ts)
        self.rng = rng = np.random.default_rng(20261109)
        ys, xs = np.mgrid[0:self.H, 0:self.W]
        self.ys, self.xs = ys, xs
        self.X, self.Y = xs - self.PX + 0.5, ys - self.PY + 0.5
        self.D = np.hypot(self.X, self.Y)
        self.A = np.arctan2(self.Y, self.X)
        # 덩어리가 흩어질 때 구멍이 뚫리는 순서(큰 얼룩 + 작은 얼룩)
        self.EROSION = 0.65 * self.value_noise(max(2, round(9 * s))) + 0.35 * self.value_noise(max(2, round(4 * s)))
        self.LOBE = rng.uniform(0, 2 * math.pi, 4)
        self.RING_PHASE = rng.uniform(0, 2 * math.pi, 2)
        # 튀는 방울: 고르게 돌린 각도에 흔들림을 주고, 반경 언저리까지 날아간다
        n = round(26 * s)
        self.DROPS = []
        for i in range(n):
            self.DROPS.append({
                "ang": 2 * math.pi * i / n + rng.uniform(-0.12, 0.12),
                "reach": R * rng.uniform(0.72, 1.08),
                "size": s * (rng.uniform(2.2, 4.2) if i % 4 else rng.uniform(4.5, 6.0)),
                "delay": rng.uniform(0.0, 0.04),
                "fly": rng.uniform(0.22, 0.3),
            })
        self.SPECKS = [(rng.uniform(0, 2 * math.pi), R * rng.uniform(0.35, 1.0), rng.uniform(0.02, 0.08)) for _ in range(round(40 * k))]

    def value_noise(self, cell):
        g = self.rng.random((self.H // cell + 3, self.W // cell + 3))
        gy, gx = self.ys / cell, self.xs / cell
        iy, ix = gy.astype(int), gx.astype(int)
        fy, fx = gy - iy, gx - ix
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        a, b = g[iy, ix], g[iy, ix + 1]
        c, d = g[iy + 1, ix], g[iy + 1, ix + 1]
        return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def draw_frame(sp, t, pal):
    # 0 투명, 1..5 팔레트(어두운 테 → 흰 심). t 는 판 크기에 맞춰 늘린 시간을 기준 시간으로 되돌려 쓴다
    W, H, PX, PY, X, Y, D, A = sp.W, sp.H, sp.PX, sp.PY, sp.X, sp.Y, sp.D, sp.A
    RADIUS, s, k = sp.RADIUS, sp.s, sp.k
    t = t / sp.ts
    idx = np.zeros((H, W), np.uint8)
    above_floor = Y <= FLOOR

    # 1) 퍼지는 고리: 0.2초 만에 반경 끝까지 퍼지며 가늘어지고 끊어진다
    rr = 12 * s + (RADIUS - 12 * s) * out_cubic(t / 0.2)
    wobble = 1 + 0.025 * np.sin(5 * A + sp.RING_PHASE[0])
    width = (7 - 5 * smooth(0.0, 0.22, t)) * s
    gaps = np.sin(7 * A + sp.RING_PHASE[0]) + 0.6 * np.sin(11 * A + sp.RING_PHASE[1])
    if t < 0.32:
        cut = -1.6 + 3.0 * smooth(0.1, 0.32, t)
        ring = (D <= rr * wobble) & (D >= rr * wobble - width) & (gaps > cut) & above_floor
        idx[ring] = 3
        idx[ring & (D <= rr * wobble - width + 1.5 * s)] = 4
        idx[ring & (D >= rr * wobble - s)] = 2

    # 2) 가운데 잉크 덩어리: 0.08초 만에 부풀고, 0.12초부터 구멍이 뚫리며 흩어진다
    LOBE = sp.LOBE
    R = (6 + 22 * out_cubic(t / 0.08)) * s
    shape = R * (1 + 0.12 * np.sin(3 * A + LOBE[0]) + 0.1 * np.sin(5 * A + LOBE[1]) + 0.08 * np.sin(8 * A + LOBE[2]) + 0.05 * np.sin(13 * A + LOBE[3]))
    q = D / np.maximum(shape, 1e-3)
    erode = smooth(0.1, 0.36, t)
    blob = (q <= 1) & (sp.EROSION > erode * 1.05) & above_floor
    if t < 0.38:
        core = 0.45 * (1 - smooth(0.0, 0.14, t))       # 흰 심은 처음에만
        mid = 0.5 - 0.2 * smooth(0.08, 0.3, t)
        idx[blob] = 2
        idx[blob & (q <= 0.88)] = 3
        idx[blob & (q <= mid)] = 4
        idx[blob & (q <= core)] = 5

    # 3) 튀는 방울: 바깥으로 날아가며 살짝 떨어지고, 바닥에 닿으면 납작한 얼룩이 된다
    for dr in sp.DROPS:
        u = t - dr["delay"]
        if u <= 0:
            continue
        p = out_cubic(u / dr["fly"])
        dx, dy = math.cos(dr["ang"]), math.sin(dr["ang"])
        x = dx * dr["reach"] * p
        y = dy * dr["reach"] * p + 160 * k * max(0.0, u - 0.08) ** 2
        size = dr["size"] * (1 - 0.55 * smooth(0.12, 0.4, u))
        fade = smooth(0.3, 0.42, u)
        if y >= FLOOR - 1:
            # 바닥 얼룩: 떨어진 자리에 납작하게 남았다 사라진다
            if fade >= 1:
                continue
            sw, sh = size * 1.9, max(1.0, size * 0.55)
            m = ((X - x) / sw) ** 2 + ((Y - FLOOR + sh * 0.5) / sh) ** 2 <= 1
            m &= Y <= FLOOR
            idx[m & (idx == 0)] = 2
            idx[m & (((X - x) / sw) ** 2 + ((Y - FLOOR + sh * 0.5) / sh) ** 2 <= 0.35)] = 3
            continue
        if fade >= 1 or size < 0.8 * s:
            continue
        # 꼬리: 날아가는 반대쪽으로 늘어진 눈물방울(빠를수록 길다)
        speed = 1 - p
        tail = size * (1 + 5 * speed)
        vx, vy = dx, dy + 0.004 * 160 * max(0.0, u - 0.08)  # 꼬리 기울기는 크기와 관계없이 같게
        n = math.hypot(vx, vy) or 1
        vx, vy = vx / n, vy / n
        along = (X - x) * vx + (Y - y) * vy
        across = -(X - x) * vy + (Y - y) * vx
        rad = np.where(along >= 0, size, size * np.clip(1 + along / tail, 0, 1))
        m = (along >= -tail) & (np.abs(across) <= rad) & (along <= size) & (np.hypot(np.maximum(along, 0), across) <= size)
        m &= above_floor
        idx[m] = np.maximum(idx[m], 2)
        idx[m & (np.abs(across) <= rad * 0.55) & (along > -tail * 0.6)] = 3
        idx[m & (np.hypot(along - size * 0.2, across + size * 0.25) <= size * 0.4)] = 4

    # 4) 잔 물방울: 한 화소짜리 점이 튀어 나갔다가 사라진다
    for ang, reach, delay in sp.SPECKS:
        u = t - delay
        if u <= 0 or u > 0.26:
            continue
        x = math.cos(ang) * reach * out_cubic(u / 0.26)
        y = math.sin(ang) * reach * out_cubic(u / 0.26) + 120 * k * max(0.0, u - 0.1) ** 2
        ix, iy = int(round(PX + x)), int(round(PY + y))
        if 0 <= ix < W and 0 <= iy < H and y <= FLOOR:
            idx[iy, ix] = max(idx[iy, ix], 3 if u < 0.16 else 2)

    lut = np.zeros((6, 4), np.uint8)
    for i, c in enumerate(pal, start=1):
        lut[i] = [int(c[1:3], 16), int(c[3:5], 16), int(c[5:7], 16), 255]
    return Image.fromarray(lut[idx], "RGBA")


def export(color, out, preview):
    pal = PALETTES[color]
    name = f"ink-splash_{color}"
    sp = Splash(RADII[color])
    W, H, PX, PY, FRAMES, RADIUS = sp.W, sp.H, sp.PX, sp.PY, sp.FRAMES, sp.RADIUS
    rows = math.ceil(FRAMES / COLUMNS)
    sheet = Image.new("RGBA", (W * COLUMNS, H * rows), (0, 0, 0, 0))
    frames = []
    for i in range(FRAMES):
        img = draw_frame(sp, i / FPS, pal)
        col, row = i % COLUMNS, i // COLUMNS
        sheet.paste(img, (col * W, row * H))
        frames.append({
            "filename": f"{name} {i}",
            "frame": {"x": col * W, "y": row * H, "w": W, "h": H},
            "rotated": False, "trimmed": False,
            "spriteSourceSize": {"x": 0, "y": 0, "w": W, "h": H},
            "sourceSize": {"w": W, "h": H},
            "duration": round(1000 / FPS),
        })
    meta = {
        "app": "전투테스트/사방잉크/export.py",
        "version": "1",
        "image": f"{name}.png",
        "format": "RGBA8888",
        "size": {"w": sheet.width, "h": sheet.height},
        "scale": "1",
        "frameTags": [{"name": name, "from": 0, "to": FRAMES - 1, "direction": "forward"}],
        "pixelfx": {
            "id": "ink/ink-splash", "name": "Ink splash (ink)", "fps": FPS, "loop": False,
            "frames": FRAMES, "frameSize": [W, H], "pivot": [PX, PY], "columns": COLUMNS,
            "color": color, "facing": "right", "impactFrame": 0, "floor": FLOOR, "fieldRadius": round(RADIUS, 1), "radiusUnits": RADII[color],
        },
    }
    out.mkdir(parents=True, exist_ok=True)
    sheet.save(out / f"{name}.png", optimize=True)
    (out / f"{name}.json").write_text(json.dumps({"frames": frames, "meta": meta}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    if preview:
        # 어두운 바탕·반경 원·바닥선을 깐 확인용 그림(커밋하지 않는다)
        bg = Image.new("RGBA", sheet.size, (52, 62, 54, 255))
        px = bg.load()
        for i in range(FRAMES):
            ox, oy = (i % COLUMNS) * W, (i // COLUMNS) * H
            for deg in range(720):
                a = math.radians(deg / 2)
                x, y = int(ox + PX + RADIUS * math.cos(a)), int(oy + PY + RADIUS * math.sin(a))
                if 0 <= x - ox < W and 0 <= y - oy < H:
                    px[x, y] = (90, 110, 96, 255)
            for x in range(W):
                px[ox + x, oy + PY + FLOOR + 1] = (110, 90, 70, 255)
        bg.alpha_composite(sheet)
        bg.save(out / f"{name}.preview.png")
    print(name, FRAMES, "frames", f"{W}x{H}", "->", out)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(HERE.parent.parent / "outputs" / "pixelfx" / "ink-splash"))
    ap.add_argument("--preview", action="store_true")
    args = ap.parse_args()
    for color in PALETTES:
        export(color, Path(args.out), args.preview)
