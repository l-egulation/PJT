# 잉크 조력자 픽셀 스프라이트 시트 생성기(코드로 찍는 도트, 1:1 — 주인공 ink-hero 와 같은 화소 밀도).
# 생김새: 위가 뾰족한 잉크 방울 + 발밑 웅덩이 + 노란 눈 둘. 색은 주인공 잉크(#1b1616)와 눈(#eba516)에서 땄다.
# 줄 1 appear: 사방에서 노란 빛줄기가 모여(원기옥처럼) 한 점에 뭉쳤다가 뿅 하고 나타난다.
# 줄 2 idle  : 숨 쉬듯 출렁 + 마지막 칸은 눈 깜박.
# 줄 3 leap  : 웅크렸다 뛰어올라 쓰러진 주인공에게 떨어지고, 몸에 스며들듯 납작해진다(날아가는 길은 게임 코드가 옮긴다).
# 실행: python -I make_sheet.py  →  sheet.png, sheet.json, frames/*.png, preview_*.webp|gif
import json, math, os, random
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
FW, FH = 320, 200          # 한 칸(빛줄기가 멀리서 모이게 넉넉히)
CX, GROUND = 160, 194      # 몸 가운데 x, 바닥(웅덩이 밑) y
FRAME_MS = 70

INK_OUT = (11, 9, 9, 255)
INK = (27, 22, 22, 255)
INK_RIM = (52, 44, 43, 255)
INK_SHINE = (92, 80, 76, 255)
EYE = (235, 165, 22, 255)
EYE_HI = (255, 222, 110, 255)
L_WHITE = (255, 248, 222, 255)
L_PALE = (255, 236, 150, 255)
L_YEL = (255, 206, 64, 255)
L_ORG = (245, 158, 30, 255)


def new():
    return Image.new("RGBA", (FW, FH), (0, 0, 0, 0))


def put(im, x, y, c):
    x, y = int(round(x)), int(round(y))
    if 0 <= x < FW and 0 <= y < FH:
        im.putpixel((x, y), c)


def rect(im, x, y, w, h, c):
    for yy in range(h):
        for xx in range(w):
            put(im, x + xx, y + yy, c)


# ---------------------------------------------------------------- 몸
def body_mask(sx=1.0, sy=1.0, puddle=1.0, sink=0.0):
    """동글한 잉크 방울(꼭지가 살짝 왼쪽으로 휜다) + 웅덩이. sx/sy 는 찌그러짐, sink 는 녹아내림(0~1)."""
    rx, ry = 17 * sx, 16 * sy * (1 - sink)
    base = GROUND - 2
    m = set()
    if ry > 1:
        cy = base - ry * .82                  # 아래가 바닥에 눌려 납작하다
        for y in range(int(cy - ry) - 1, base + 1):
            k = (y - cy) / ry
            if k >= 1:
                continue
            hw = rx * math.sqrt(max(0, 1 - k * k))
            if k > 0:
                hw = max(hw, rx * (1 - .18 * k))          # 아랫배는 퍼진다
            for x in range(int(CX - hw - 1), int(CX + hw + 2)):
                if abs(x + .5 - CX) <= hw:
                    m.add((x, y))
        # 꼭지: 둥근 머리 위로 짧게, 왼쪽으로 휜다
        th = 9 * sy * (1 - sink)
        top0 = cy - ry + 1
        for i in range(int(th) + 1):
            t = i / max(1, th)
            w = 4.2 * (1 - t) ** 1.2
            x0 = CX - 1 - 4.5 * t * t
            for x in range(int(x0 - w), int(x0 + w) + 1):
                if abs(x + .5 - x0) <= w + .2:
                    m.add((x, int(top0 - i)))
    # 웅덩이
    pw = (22 + 6 * sink) * sx * puddle
    for y in (range(base - 1, GROUND + 1) if puddle > 0 else ()):   # puddle=0: 공중(웅덩이 없이 동글게)
        k = (y - (base - 1)) / 3
        hw = pw * (0.82 + .18 * min(1, k))
        for x in range(int(CX - hw), int(CX + hw + 1)):
            m.add((x, y))
    return m


def draw_body(im, m, eyes="open", eye_dx=-3, eye_y=None, eye_alpha=1.0):
    for (x, y) in m:
        out = any((x + dx, y + dy) not in m for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if out:
            put(im, x, y, INK_OUT)
        elif (x - 2, y - 2) not in m and (x + 3, y) in m:
            put(im, x, y, INK_RIM)          # 왼쪽 위 테두리 빛
        else:
            put(im, x, y, INK)
    ys = [y for _, y in m]
    if not ys:
        return
    top = min(ys)
    # 반짝 두 점(왼쪽 위)
    for (dx, dy) in ((-6, 10), (-6, 11), (-5, 9)):
        if (CX + dx, top + dy) in m:
            put(im, CX + dx, top + dy, INK_SHINE)
    if eyes and eye_alpha > 0:
        ey = eye_y if eye_y is not None else GROUND - 2 - 18 if top < GROUND - 24 else top + 4
        for ex in (CX - 6 + eye_dx, CX + 2 + eye_dx):
            if eyes == "open":
                rect(im, ex, ey, 3, 4, EYE)
                put(im, ex, ey, EYE_HI)
                put(im, ex + 1, ey, EYE_HI)
            elif eyes == "half":
                rect(im, ex, ey + 2, 3, 2, EYE)
            elif eyes == "closed":
                rect(im, ex, ey + 3, 3, 1, EYE)


# ---------------------------------------------------------------- 빛
CORE = (CX, GROUND - 16)


def line(im, x0, y0, x1, y1, cols):
    """x0,y0(머리) → x1,y1(꼬리). cols 는 머리에서 꼬리로 갈 색들."""
    n = max(1, int(max(abs(x1 - x0), abs(y1 - y0))))
    for i in range(n + 1):
        t = i / n
        c = cols[min(len(cols) - 1, int(t * len(cols)))]
        if c is None:
            continue
        if t > .75 and i % 2:                # 꼬리는 점선으로 흩어진다
            continue
        px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        put(im, px, py, c)
        if t < .34:                          # 머리 쪽은 두 줄 두께
            if abs(x1 - x0) > abs(y1 - y0): put(im, px, py + 1, c)
            else: put(im, px + 1, py, c)


random.seed(7)
RAYS = []
for i in range(36):
    ang = -math.pi * ((i * 7) % 36 / 35) + random.uniform(-.06, .06)   # 위쪽 반원(바닥 밑에서는 안 온다), 고르게 섞어서
    RAYS.append(dict(ang=ang, r0=random.uniform(105, 175), delay=i / 36 * 11 + random.uniform(-.6, .6), dur=random.uniform(4.2, 6.0), L=random.randint(14, 28)))
# 옆에서 낮게 들어오는 줄기 몇 개
for ang in (.12, math.pi - .12, .25, math.pi - .25):
    RAYS.append(dict(ang=-ang, r0=random.uniform(130, 150), delay=random.uniform(2, 9), dur=5.0, L=20))


def draw_rays(im, f):
    cx, cy = CORE
    for r in RAYS:
        p = (f - r["delay"]) / r["dur"]
        if p <= 0 or p >= 1:
            continue
        e = p ** 1.6                                     # 처음엔 느리게, 가까워질수록 빨리 빨려 든다
        rad = r["r0"] * (1 - e)
        L = r["L"] * (1 - .5 * e) + 4 * e
        dx, dy = math.cos(r["ang"]), math.sin(r["ang"])
        hx, hy = cx + dx * rad, cy + dy * rad
        tx, ty = cx + dx * (rad + L), cy + dy * (rad + L)
        line(im, hx, hy, tx, ty, [L_WHITE, L_PALE, L_YEL, L_YEL, L_ORG, L_ORG])


def draw_core(im, size, glow=0):
    cx, cy = CORE
    if size <= 0:
        return
    for y in range(-size - 2, size + 3):
        for x in range(-size - 2, size + 3):
            d = abs(x) + abs(y) * 1.0                          # 마름모에 가까운 둥근 덩어리
            d2 = math.hypot(x, y)
            if d2 <= size * .55:
                put(im, cx + x, cy + y, L_WHITE)
            elif d2 <= size * .85:
                put(im, cx + x, cy + y, L_PALE)
            elif d2 <= size + .3:
                put(im, cx + x, cy + y, L_YEL)
            elif glow and d2 <= size + 1.6 and (x + y) % 2 == 0:
                put(im, cx + x, cy + y, L_ORG)


def sparkle(im, x, y, n, c=L_PALE, core=L_WHITE):
    for i in range(1, n + 1):
        cc = c if i < n else L_YEL
        for dx, dy in ((i, 0), (-i, 0), (0, i), (0, -i)):
            put(im, x + dx, y + dy, cc)
    put(im, x, y, core)


def ring(im, r, cols, dither=False, below=False):
    cx, cy = CORE
    steps = int(2 * math.pi * r * 1.5) + 8
    for i in range(steps):
        a = 2 * math.pi * i / steps
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r * .8
        if y > GROUND - 1 and not below:
            continue
        if dither and i % 2:
            continue
        put(im, x, y, cols[i % len(cols)])


def sparks(im, r, n=10, size=2, col=L_YEL):
    cx, cy = CORE
    for i in range(n):
        a = -math.pi * (i + .5) / n - .05
        x, y = cx + math.cos(a) * r * (1 + .25 * (i % 3)), cy + math.sin(a) * r * .85 * (1 + .25 * (i % 3))
        rect(im, x, y, size, size, col)


# 주변에 떠도는 작은 별
AMBIENT = [(random.randint(30, 290), random.randint(20, 150), random.uniform(0, 9)) for _ in range(26)]


def draw_ambient(im, f, fade=1.0):
    for (x, y, ph) in AMBIENT:
        k = (f + ph) % 4
        if fade < 1 and (x * 7 + y) % 10 >= fade * 10:
            continue
        if k < 1:
            put(im, x, y, L_YEL)
        elif k < 2:
            sparkle(im, x, y, 1, L_YEL, L_PALE)
        elif k < 3:
            put(im, x, y, L_ORG)


# ---------------------------------------------------------------- 칸 만들기
def appear():
    out = []
    N = 20
    for f in range(N):                       # 모이기(약 1.4초)
        im = new()
        draw_ambient(im, f, fade=1 - max(0, f - 14) / 6)
        draw_rays(im, f)
        draw_core(im, [0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 6][f], glow=f >= 10)
        if f >= 16:
            sparkle(im, CORE[0], CORE[1], 6 + (f - 16) * 2, L_PALE, L_WHITE)
        out.append(im)
    # 응축(작아졌다 커지며 번쩍)
    im = new(); draw_core(im, 3, glow=1); sparkle(im, *CORE, 12); out.append(im)
    im = new(); draw_core(im, 9); ring(im, 12, [L_PALE, L_YEL]); out.append(im)            # 번쩍
    # 뿅: 찌그러진 몸 + 퍼지는 고리 + 튀는 불티
    # 고리·불티는 몸 뒤에 깐다
    im = new(); ring(im, 18, [L_YEL, L_ORG]); sparks(im, 16); draw_body(im, body_mask(1.3, .6), eyes=None); out.append(im)
    im = new(); ring(im, 25, [L_YEL, L_ORG], dither=True); sparks(im, 24, size=2, col=L_ORG); draw_body(im, body_mask(.9, 1.12), eyes=None); out.append(im)
    im = new(); sparks(im, 31, size=1, col=L_ORG); draw_body(im, body_mask(1.08, .92), eyes=None); out.append(im)
    im = new(); draw_body(im, body_mask(.96, 1.04), eyes="closed"); out.append(im)
    im = new(); draw_body(im, body_mask(1.0, 1.0), eyes="half"); out.append(im)
    im = new(); draw_body(im, body_mask(1.0, 1.0), eyes="open"); out.append(im)
    return out


def idle():
    out = []
    for (sx, sy, eyes) in ((1.0, 1.0, "open"), (1.02, .975, "open"), (1.04, .955, "open"), (1.02, .975, "open"), (1.0, 1.0, "open"), (.99, 1.015, "closed")):
        im = new(); draw_body(im, body_mask(sx, sy), eyes=eyes); out.append(im)
    return out


def leap():
    """칸마다 발밑(앵커) 기준 몸만. 1~2 웅크림, 3 박차기, 4~6 공중(오름·꼭대기·떨어짐), 7~9 주인공 몸에 닿아 스며든다."""
    out = []
    for (sx, sy, pud, eyes) in ((1.2, .8, 1, "open"), (1.32, .66, 1, "closed"), (.78, 1.28, .45, "open"),
                                (.84, 1.16, 0, "open"), (1.0, .96, 0, "open"), (.86, 1.14, 0, "open"),
                                (1.45, .5, .7, None), (1.12, .3, .45, None), (.62, .16, .25, None)):
        im = new(); draw_body(im, body_mask(sx, sy, puddle=pud), eyes=eyes); out.append(im)
    return out


rows = [("appear", appear()), ("idle", idle()), ("leap", leap())]
cols = max(len(fr) for _, fr in rows)
sheet = Image.new("RGBA", (FW * cols, FH * len(rows)), (0, 0, 0, 0))
meta = {"frame": [FW, FH], "anchor": [CX, GROUND], "frameMs": FRAME_MS, "rows": {}}
os.makedirs(os.path.join(HERE, "frames"), exist_ok=True)
for r, (name, frames) in enumerate(rows):
    meta["rows"][name] = {"row": r, "count": len(frames)}
    for c, im in enumerate(frames):
        sheet.paste(im, (c * FW, r * FH))
        im.save(os.path.join(HERE, "frames", f"{name}-{c + 1:02d}.png"))
sheet.save(os.path.join(HERE, "sheet.png"))
json.dump(meta, open(os.path.join(HERE, "sheet.json"), "w"), indent=1)

# 미리보기: 어두운 바탕·밝은 바탕, 4배, 나타나기 → 숨쉬기 두 번 → 녹기
seq = rows[0][1] + rows[1][1] * 2 + rows[2][1]
for name, bg in (("dark", (34, 30, 28, 255)), ("light", (226, 220, 206, 255))):
    big = []
    for im in seq:
        b = Image.new("RGBA", (FW, FH), bg); b.alpha_composite(im)
        big.append(b.resize((FW * 2, FH * 2), Image.NEAREST))
    big[0].save(os.path.join(HERE, f"preview_{name}.webp"), save_all=True, append_images=big[1:], duration=FRAME_MS, loop=0, lossless=True)
print(sheet.size, {k: len(v) for k, v in rows})
