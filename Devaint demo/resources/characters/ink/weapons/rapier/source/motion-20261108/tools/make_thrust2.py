# 레이피어 새 평타 찌르기 이펙트 2차 시안(2026-11-08). 사용자 「다 별로, 처음부터」.
# 1차(pixelfx 화소 막대·금빛)는 게임의 기본검 계열 이펙트(부드러운 흰 붓질 + 은은한 빛, PPU64, 선형 필터)와 화풍이 달랐다.
# 2차는 그 화풍에 맞춰 매끈한 흰·은빛 붓질로 그린다(4배로 그려 줄여 가장자리를 부드럽게, 빛은 흐림 한 겹).
#   A 은빛 일섬: 가늘고 긴 흰 칼빛이 칼끝 너머로 쭉 뻗고, 꼬리가 앞으로 따라가며 걷힌다. 칼끝에 작은 네 갈래 반짝임.
#   B 겹화살 관통: 기본검 돌진 찌르기의 화살촉(>) 모양을 가늘게 세 겹, 앞으로 미끄러지며 꿰뚫는다.
#   C 일섬 + 먹 가시 터짐: A 에 칼끝에서 앞쪽 부채꼴로 먹 가시 조각(대검 칼바람의 면 나눈 먹 조각 화풍)이 팡 터진다.
#   D 나선 꿰뚫기: A 둘레를 가는 흰 선 두 줄이 꼬이며 칼끝으로 모이고, 끝에서 한 번 번쩍.
# (사용자: 「끝에는 점이 아니라 원으로 터지는 효과」 → A 칼빛 + 칼끝 원 터짐)
#   E 원 한 겹: 칼끝에서 흰 원이 확 번쩍(채운 원) 한 뒤 고리로 퍼지며 가늘어진다.
#   F 원 두 겹: E 에 한 박자 늦은 작은 고리가 하나 더.
#   G 납작 충격파: 찌르는 방향에 수직으로 납작한 타원 고리 둘이 앞으로 밀려 나가며 퍼진다(꿰뚫는 충격).
#   H 원 + 먹 가시: E 의 원 터짐에 C 의 먹 가시 조각이 함께 튄다.
# 원점 = 시전자 발밑, PPU64(64px = 1유닛), 칼 높이 1.0유닛, 코등이 1.35, 판정 끝 3.16유닛. 시각 0 = fxStart(타격 0.03초 전), 50fps.
# 출력: thrust2-fx/<A..D>-hit<1..3>/NNN.png + meta.json
import json, math, shutil, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
HERE = Path(__file__).resolve().parent; OUT = HERE / 'thrust2-fx'
PPU, SS, FPS = 64, 4, 50
CW, CH, OX, OY = 320, 176, 20, 128           # 캔버스, 발밑 자리
Y = OY - 1.0 * PPU                            # 칼 높이
WHITE, SILVER, SHADE = (255, 255, 255), (214, 224, 230), (150, 164, 174)
INK, INK2, INK3 = (27, 22, 22), (58, 50, 48), (90, 78, 74)
IMPACT = 0.03
def ease(x): x = min(1, max(0, x)); return 1 - (1 - x) ** 3
class Canvas:
    def __init__(s):
        s.glow = Image.new('L', (CW * SS, CH * SS), 0); s.col = Image.new('RGBA', (CW * SS, CH * SS), (0, 0, 0, 0))
        s.dg = ImageDraw.Draw(s.glow); s.dc = ImageDraw.Draw(s.col)
    def poly(s, pts, fill, glow=0):
        P = [(x * SS, y * SS) for x, y in pts]
        if glow: s.dg.polygon(P, fill=int(255 * glow))
        s.dc.polygon(P, fill=fill + (255,) if len(fill) == 3 else fill)
    def line(s, pts, w, fill, glow=0):
        P = [(x * SS, y * SS) for x, y in pts]
        if glow: s.dg.line(P, fill=int(255 * glow), width=int(w * SS * 3), joint='curve')
        s.dc.line(P, fill=fill + (255,), width=max(1, int(w * SS)), joint='curve')
    def ring(s, x, y, rx, ry, w, col, glow=0, alpha=1.0):
        if rx < .5 or w < .2: return
        b = [(x - rx) * SS, (y - ry) * SS, (x + rx) * SS, (y + ry) * SS]
        if glow: s.dg.ellipse(b, outline=int(255 * glow * alpha), width=max(1, int(w * SS * 2.5)))
        s.dc.ellipse(b, outline=col + (int(255 * alpha),), width=max(1, int(w * SS)))
    def disc(s, x, y, r, col, glow=0, alpha=1.0):
        if r < .3: return
        b = [(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS]
        if glow: s.dg.ellipse(b, fill=int(255 * glow * alpha))
        s.dc.ellipse(b, fill=col + (int(255 * alpha),))
    def done(s, glow_col=(235, 245, 255), glow_r=6):
        g = s.glow.filter(ImageFilter.GaussianBlur(glow_r * SS)); g = g.point(lambda v: int(v * .55))
        base = Image.new('RGBA', s.col.size, glow_col + (0,)); base.putalpha(g)
        base.alpha_composite(s.col)
        return base.resize((CW, CH), Image.LANCZOS)
def stroke(c, x0, x1, y, W, col, glow=0, peak=.72, tilt=0):
    """뒤 x0(가늘게) → 앞 x1(뾰족). 가장 굵은 곳 peak. 위아래 반폭 W(px)."""
    if x1 - x0 < 1 or W < .2: return
    up, dn = [], []
    for k in range(41):
        u = k / 40; x = x0 + (x1 - x0) * u
        w = W * ((u / peak) ** .6 if u < peak else ((1 - u) / (1 - peak)) ** .85)
        up.append((x, y - w + tilt * u)); dn.append((x, y + w * .8 + tilt * u))
    c.poly(up + dn[::-1], col, glow)
def star4(c, x, y, r, col=WHITE, glow=1):
    a, b = r, r * .16
    c.poly([(x - a, y), (x, y - b), (x + a, y), (x, y + b)], col, glow)
    c.poly([(x, y - a * .62), (x + b, y), (x, y + a * .62), (x - b, y)], col, glow)
def shard(c, x, y, ang, L, Wd):
    ca, sa = math.cos(ang), math.sin(ang); nx, ny = -sa, ca
    p = [(x - ca * L * .35, y - sa * L * .35), (x + nx * Wd, y + ny * Wd), (x + ca * L * .65, y + sa * L * .65), (x - nx * Wd * .7, y - ny * Wd * .7)]
    c.poly(p, INK)
    c.poly([p[0], ((p[0][0] + p[2][0]) / 2 + nx * Wd * .3, (p[0][1] + p[2][1]) / 2 + ny * Wd * .3), p[2], (p[1][0] * .5 + p[2][0] * .5, p[1][1] * .5 + p[2][1] * .5)], INK3)
def make(v, hit, rng):
    big = hit == 3; S = 1.25 if big else 1
    ROOT, TIP = 1.35 * PPU, (3.85 if big else 3.5) * PPU
    W = (9.0 if big else 7.0)          # 처음 6.5·5.0 은 가늘어 안 보였다
    GROW, HOLD, FADE = .03, (.07 if big else .05), (.12 if big else .10)
    shards = [dict(a=rng.uniform(-.85, .85), v=rng.uniform(280, 460) * S, L=rng.uniform(18, 32) * S, w=rng.uniform(4, 6.5) * S, spin=rng.uniform(-6, 6)) for _ in range(9)]
    frames = []
    n = int(round(.36 * FPS))
    for i in range(n):
        t = i / FPS; q = t - IMPACT; c = Canvas()
        head = ROOT + (TIP - ROOT) * ease(t / GROW); tk = min(1, max(0, (t - GROW - HOLD) / FADE)); tail = ROOT + (TIP - ROOT) * tk ** 1.3
        thin = 1 - .5 * tk; on = tk < 1
        if v in ('A', 'C', 'D', 'E', 'F', 'G', 'H') and on:
            stroke(c, tail, head, Y, W * thin * 1.25, SHADE, glow=.9)          # 은빛 바깥
            stroke(c, tail + 6, head - 2, Y - 1, W * thin * .62, SILVER)
            stroke(c, tail + 14, head - 6, Y - 1.5, W * thin * .28, WHITE)       # 흰 심
        if v == 'B' and on:   # 화살촉 세 겹: 앞으로 미끄러지며 뒤 것부터 걷힌다
            for k, (off, sc) in enumerate(((0, 1), (-26, .82), (-50, .64))):
                kk = min(1, max(0, (t - .012 * k) / GROW)); hx = ROOT + (TIP - ROOT) * ease(kk) + off * ease(kk)
                al = 1 - min(1, max(0, (t - GROW - HOLD + .015 * k) / FADE))
                if kk <= 0 or al <= 0: continue
                L = (76 if big else 62) * sc * (.5 + .5 * ease(kk)); sp = (15 if big else 12) * sc
                for sgn in (-1, 1):
                    stroke(c, hx - L, hx, Y + sgn * sp * .5, 3.6 * sc * al + .3, SILVER if k else WHITE, glow=.8 * al, tilt=-sgn * sp * .5)
                stroke(c, hx - L * 1.1, hx + 2, Y, 1.6 * sc * al, WHITE, glow=.6 * al)
        if v == 'D' and on:   # 나선: 진폭이 칼끝으로 갈수록 0, 위상은 시간에 따라 돈다
            for ph in (0, math.pi):
                pts = []
                for k in range(60):
                    u = k / 59; x = tail + (head - tail) * u; amp = (11 if big else 9) * (1 - u) ** .8 * thin
                    pts.append((x, Y + amp * math.sin(u * 5.5 * math.pi + ph - t * 60)))
                c.line(pts, 2.4, WHITE, glow=.8)
        if q >= 0 and q < .08 and v in ('A', 'D'):
            r = (22 if big else 16) * (q / .02 if q < .02 else 1 - (q - .02) / .06 * .8)
            star4(c, TIP - 2, Y - 1, r * (1.4 if v == 'D' else 1))
        if v in ('E', 'F', 'H') and q >= 0:   # 원 터짐: 채운 원이 번쩍 → 고리로 퍼지며 가늘어진다
            bx, R = TIP - 6, (40 if big else 30)
            if q < .035: c.disc(bx, Y - 1, (12 if big else 9) * (q / .012 if q < .012 else 1 - (q - .012) / .023 * .7), WHITE, glow=1)
            rings = [(0, 1.0)] + ([(.03, .62)] if v == 'F' else [])
            for d0, sc in rings:
                k = (q - d0) / .16
                if 0 <= k < 1:
                    r = 4 + (R * sc - 4) * ease(k * 1.15); w = (6 if big else 5) * sc * (1 - k) ** 1.2 + .4
                    c.ring(bx, Y - 1, r, r, w, WHITE if k < .4 else SILVER, glow=.9, alpha=1 - k ** 2)
        if v == 'G' and q >= 0:   # 납작 충격파: 세로로 긴 타원 고리, 앞으로 밀려 나가며 커진다
            for d0, sc in ((0, 1.0), (.025, .75)):
                k = (q - d0) / .15
                if 0 <= k < 1:
                    bx = TIP - 14 + (40 if big else 30) * ease(k) * sc
                    ry = (8 + (30 if big else 22) * ease(k)) * sc; rx = ry * .38; w = (5 if big else 4) * (1 - k) ** 1.2 + .4
                    c.ring(bx, Y - 1, rx, ry, w, WHITE if k < .4 else SILVER, glow=.9, alpha=1 - k ** 2)
            if q < .03: c.disc(TIP - 6, Y - 1, (10 if big else 7) * (1 - q / .03 * .6), WHITE, glow=1)
        if v in ('C', 'H') and q >= 0:
            for s_ in shards:
                life = .17
                if q > life: continue
                k = q / life; d = 6 + s_['v'] * q * (1 - .45 * k)
                shard(c, TIP - 10 + math.cos(s_['a']) * d, Y + math.sin(s_['a']) * d + 160 * q * q, s_['a'] + s_['spin'] * q, s_['L'] * (1 - .6 * k), s_['w'] * (1 - .5 * k))
            if q < .05 and v == 'C': star4(c, TIP - 4, Y - 1, (21 if big else 15) * (1 - q / .05 * .6))
        frames.append(c.done())
    return frames
if __name__ == '__main__':
    shutil.rmtree(OUT, ignore_errors=True)
    for v in (sys.argv[1] if len(sys.argv) > 1 else 'A,B,C,D').split(','):
        for hit in (1, 2, 3):
            fr = make(v, hit, np.random.default_rng(11 + hit)); d = OUT / f'{v}-hit{hit}'; d.mkdir(parents=True)
            for i, f in enumerate(fr): f.save(d / f'{i:03d}.png')
            (d / 'meta.json').write_text(json.dumps({'effect': 'rapier-thrust2', 'variant': v, 'hit': hit, 'fps': FPS, 'frames': len(fr), 'size': [CW, CH], 'origin_px': [OX, OY], 'ppu': PPU}, indent=1))
    print('done')
