# 보스방 가는 길목(48u × 6u)과 보스방(30u × 18u, 바닥 3u)을 v26 화풍으로 그린다.
# 좌표는 길목 시안 좌표(보스방이 오른쪽): 앞 방 벽 바깥면 x=0, 길목 0~51, 보스방 벽 51~52, 전투장 52~82.
# 길목 바닥 = 전투장 바닥 윗면 = y 0. 보스방 안쪽은 y 0~15(전투장 18u 중 바닥 3u 아래).
# python render_boss.py <out dir>
from pathlib import Path
import json, math, sys
import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import render_map as rm   # noqa: E402  (v26 렌더러 코어 r, 조명 창, 거리 함수)
r = rm.r; PAD = rm.PAD

# ---------------------------------------------------------------- 지형
X0, X1, Y0, Y1 = -14, 84, -5, 20          # 그리는 범위(월드 u)
OPEN = [(-14, 0, 0, 12),                   # 앞 방(키 큰 방)
        (0, 51, 0, 6),                     # 길목 48u + 입구 3u
        (51, 52, 0, 6),                    # 보스방 벽의 통로 구멍
        (52, 82, 0, 15)]                   # 보스 전투장 안쪽(바닥 위 15u)
# --air: 1페이즈 공중전 판(사용자 2026-11-13 「바닥이 없으니 배경을 밑까지 이어야 끊겨 보이지 않는다」). 전투장 바닥 3u 를 비우고
# 양옆 돌기둥 벽·수문·물줄기를 전장 아래 끝(시안 y -3)까지 내린다. 수문 뒤 물의 수면 높이는 바닥이 있을 때와 같고(사용자 2026-11-13),
# 바닥이 빠진 만큼 그 아래를 물로 채운다(깊을수록 어둡다).
AIR = '--air' in sys.argv
args = [a for a in sys.argv[1:] if a != '--air']
if AIR: OPEN[-1] = (52, 82, -3, 15)
ARENA_X = 52                               # 전투장 왼쪽 = 시안 x 0
PLATFORMS = [(6.5, 4.5), (17.5, 4.5), (2, 9), (12, 9), (22, 9), (6.5, 13.5), (17.5, 13.5)]   # 전투장 좌표, 폭 6
def arena(x, y): return ARENA_X + x, y - 3            # 전투장 좌표(바닥 아래 끝 0) → 월드

def build_layout():
    step = .5; cols = int((X1 - X0) / step); rows = int((Y1 - Y0) / step)
    solid = np.ones((rows, cols), bool)
    for (a, b, c, d) in OPEN:
        solid[int((c - Y0) / step):int((d - Y0) / step), int((a - X0) / step):int((b - X0) / step)] = False
    surfaces = []
    for j in range(rows):
        i = 0
        while i < cols:
            if not solid[j, i]: i += 1; continue
            k = i
            while k < cols and solid[j, k]: k += 1
            surfaces.append({'id': f's{j}_{i}', 'x': X0 + i * step, 'y': Y0 + j * step, 'w': (k - i) * step, 'h': step, 'oneWay': False})
            i = k
    return {'surfaces': surfaces, 'rooms': [{'id': 'hall', 'x': 0, 'y': 0, 'w': 51, 'h': 6}, {'id': 'B', 'x': 52, 'y': 0, 'w': 30, 'h': 15}]}

# ---------------------------------------------------------------- 칠하기 도구(레벨 배열에, 땅이 아닌 곳만)
class Painter:
    def __init__(self, lvl, Wm, L, m, left, top):
        self.lvl, self.Wm, self.L, self.m, self.left, self.top = lvl, Wm, L, m, left, top
        self.HH, self.WW = lvl.shape
    def cx(self, wx): return round(wx * 32) - self.left
    def cy(self, wy): return self.top - round(wy * 32)
    def box(self, x0, y0, x1, y1):          # 월드 사각형 → 캔버스 조각 (위가 큰 y)
        c0, c1 = sorted((self.cx(x0), self.cx(x1))); r0, r1 = sorted((self.cy(y1), self.cy(y0)))
        return r0, r1, c0, c1
    def put(self, r0, r1, c0, c1, val, lit=.7, mask=None):
        r0, r1, c0, c1 = int(r0), int(r1), int(c0), int(c1)
        if r1 <= r0 or c1 <= c0: return
        R0, R1, C0, C1 = max(0, r0), min(self.HH, r1), max(0, c0), min(self.WW, c1)
        if R1 <= R0 or C1 <= C0: return
        sl = (slice(R0 - r0, R1 - r0), slice(C0 - c0, C1 - c0))
        v = np.broadcast_to(np.asarray(val, dtype=np.float32), (r1 - r0, c1 - c0))[sl] if np.ndim(val) else np.full((R1 - R0, C1 - C0), float(val))
        sub = self.lvl[R0:R1, C0:C1]; air = ~self.m[R0:R1, C0:C1]
        if mask is not None: air = air & np.broadcast_to(mask, (r1 - r0, c1 - c0))[sl]
        sub[air] = (v + np.minimum(self.L[R0:R1, C0:C1], 6) * lit)[air]
    def rect(self, x0, y0, x1, y1, val, lit=.7): self.put(*self.box(x0, y0, x1, y1), val, lit)
    def rivets(self, x0, x1, wy, every=.75, val=4.6):
        y = self.cy(wy)
        for wx in np.arange(x0, x1 + 1e-6, every):
            c = self.cx(wx); self.put(y - 1, y + 1, c - 1, c + 1, val)
    def hpipe(self, x0, x1, wy_top, thick=22, flange_every=4.0):
        r0 = self.cy(wy_top); c0, c1 = self.cx(x0), self.cx(x1)
        base = np.array([5.6, 5.0, 4.5, 4.2, 4.0, 3.9, 3.8, 3.7, 3.6, 3.5, 3.4, 3.3, 3.2, 3.1, 3.0, 2.9, 2.7, 2.5, 2.2, 1.9, 1.5, 1.0])
        prof = np.interp(np.linspace(0, len(base) - 1, thick), np.arange(len(base)), base)
        self.put(r0, r0 + thick, c0, c1, np.repeat(prof[:, None], c1 - c0, 1))
        self.put(r0 + thick, r0 + thick + 4, c0, c1, 1.0, lit=.3)           # 관 아래 그늘
        for wx in np.arange(x0 + flange_every / 2, x1, flange_every):
            c = self.cx(wx); self.put(r0 - 3, r0 + thick + 3, c - 4, c + 4, 3.6); self.put(r0 - 3, r0 - 2, c - 4, c + 4, 5.0)
            for dy in (2, thick - 3): self.put(r0 + dy, r0 + dy + 2, c - 1, c + 1, 5.2)
    def vpipe(self, wx, y0, y1, thick=18):
        c0 = self.cx(wx) - thick // 2; r0, r1 = self.cy(y1), self.cy(y0)
        base = np.array([4.6, 4.2, 3.8, 3.6, 3.4, 3.3, 3.2, 3.1, 3.0, 2.9, 2.8, 2.7, 2.5, 2.3, 2.0, 1.7, 1.3, .9])
        prof = np.interp(np.linspace(0, len(base) - 1, thick), np.arange(len(base)), base)
        self.put(r0, r1, c0, c0 + thick, np.repeat(prof[None, :], max(0, r1 - r0), 0))
    def elbow(self, cwx, cwy, R, quad, thick=22):
        """관 꺾임(사분원). quad: 'ur','ul','dr','dl' = 중심에서 본 관이 지나는 사분면."""
        cc, cr = self.cx(cwx), self.cy(cwy); Rp = round(R * 32)
        r0, r1 = max(0, cr - Rp - thick), min(self.HH, cr + Rp + thick); c0, c1 = max(0, cc - Rp - thick), min(self.WW, cc + Rp + thick)
        yy, xx = np.mgrid[r0:r1, c0:c1]; dx, dy = xx - cc, yy - cr; d = np.hypot(dx, dy)
        sel = (np.abs(d - Rp) < thick / 2)
        sel &= (dx >= 0) if 'r' in quad else (dx <= 0)
        sel &= (dy <= 0) if 'u' in quad else (dy >= 0)
        t = (d - Rp + thick / 2) / thick                  # 0 바깥쪽 → 1 안쪽
        val = 4.6 - t * 3.2 if 'u' in quad else 1.6 + t * 2.8
        self.put(r0, r1, c0, c1, val, mask=sel)
    def buttress(self, wx, y_top, w=1.15, base_w=1.75, base_h=1.6):
        """A1 의 계단 받침 기둥: 바닥에서 관 아래까지, 아래쪽은 넓고 비스듬한 어깨."""
        x0, x1 = wx - w / 2, wx + w / 2
        r0, r1, c0, c1 = self.box(x0, 0, x1, y_top)
        rel = np.arange(c0, c1)[None, :] - c0; seam = ((np.arange(r0, r1)[:, None] + self.top) % 32 == 0)
        self.put(r0, r1, c0, c1, 3.6 + (rel < 3) * 1.2 - (rel > (c1 - c0) - 5) * 1.2 - seam * .8)
        self.put(r0, r1, c1, c1 + 8, 1.2, lit=.2)                                        # 기둥 그림자
        b0, b1 = wx - base_w / 2, wx + base_w / 2
        r0, r1, c0, c1 = self.box(b0, 0, b1, base_h)
        yy, xx = np.mgrid[r0:r1, c0:c1]; hgt = r1 - r0; slope = (yy - r0) < 16
        inside = ~slope | (np.abs(xx - (c0 + c1) / 2) < (c1 - c0) / 2 * ((yy - r0) / 16) + (c1 - c0) / 2 * .55)
        rel = xx - c0
        self.put(r0, r1, c0, c1, 3.8 + (rel < 3) * 1.2 - (rel > (c1 - c0) - 5) * 1.2 + ((yy - r0) <= 1) * 1.4, mask=inside)
    def band(self, x0, x1, wy, h=10):
        r0, r1, c0, c1 = self.box(x0, wy - h / 32, x1, wy)
        rel = np.arange(r0, r1)[:, None] - r0
        self.put(r0, r1, c0, c1, np.where(rel == 0, 4.2, np.where(rel == (r1 - r0) - 1, 1.6, 2.7)) + np.zeros((1, c1 - c0)))
        self.rivets(x0 + .2, x1 - .2, wy - h / 64, .75)
    def plate(self, x0, y0, x1, y1):
        r0, r1, c0, c1 = self.box(x0, y0, x1, y1)
        yy, xx = np.mgrid[r0:r1, c0:c1]; edge = (yy - r0 < 2) | (r1 - yy <= 2) | (xx - c0 < 2) | (c1 - xx <= 2)
        self.put(r0, r1, c0, c1, 2.7 + edge * .8 + ((yy - r0) == 0) * .8)
        for (cx_, cy_) in ((c0 + 4, r0 + 4), (c1 - 5, r0 + 4), (c0 + 4, r1 - 5), (c1 - 5, r1 - 5)): self.put(cy_, cy_ + 2, cx_, cx_ + 2, 5.0)
    def wheel(self, wx, wy, R=14):
        cc, cr = self.cx(wx), self.cy(wy); r0, r1, c0, c1 = cr - R - 3, cr + R + 4, cc - R - 3, cc + R + 4
        yy, xx = np.mgrid[r0:r1, c0:c1]; dx, dy = xx - cc, yy - cr; d = np.hypot(dx, dy); ang = np.arctan2(dy, dx)
        ring = np.abs(d - R) < 2.2; spoke = (d < R) & (np.abs(np.sin(3 * ang)) * d < 1.3); hub = d < 4.2
        val = np.where(dy < 0, 6.0, 4.4) * ring + 4.8 * spoke + 6.2 * hub
        self.put(r0, r1, c0, c1, val, mask=ring | spoke | hub)
    def window(self, x0, y0, x1, y1, warm=1.0):
        """리벳 테 관측창. 안은 따뜻한 빛과 먼 아치 실루엣(보스방이 비친다)."""
        self.plate(x0 - .2, y0 - .2, x1 + .2, y1 + .2)
        r0, r1, c0, c1 = self.box(x0, y0, x1, y1)
        yy, xx = np.mgrid[r0:r1, c0:c1]; t = (yy - r0) / max(1, r1 - r0)
        glow = 7.8 - t * 2.0
        gx = (xx - c0) / max(1, c1 - c0)
        frame = (np.abs(gx - .5) < .34) & (t > .18)                                  # 멀리 보이는 수문 틀
        bars = frame & (((xx - c0) % 5) < 2) & (t > .26)
        beam = frame & (np.abs(t - .26) < .05)
        floor_ = t > .86
        sil = bars | beam | floor_ | (frame & (np.abs(gx - .5) > .3))
        self.put(r0, r1, c0, c1, np.where(sil, 3.0, glow), lit=0)
        self.Wm[r0:r1, c0:c1] = np.maximum(self.Wm[r0:r1, c0:c1], warm)
        for k in (1, 2):
            c = c0 + (c1 - c0) * k // 3; self.put(r0, r1, c - 1, c + 2, 2.2, lit=0)      # 창살
        # 창에서 새는 빛
        gy0, gy1, gx0, gx1 = max(0, r0 - 40), min(self.HH, r1 + 50), max(0, c0 - 60), min(self.WW, c1 + 60)
        yy, xx = np.mgrid[gy0:gy1, gx0:gx1]
        d = np.hypot(np.clip(np.abs(xx - (c0 + c1) / 2) - (c1 - c0) / 2, 0, None), np.clip(np.abs(yy - (r0 + r1) / 2) - (r1 - r0) / 2, 0, None))
        self.L[gy0:gy1, gx0:gx1] += np.clip(1 - d / 70, 0, 1) * 1.8 * warm
        self.Wm[gy0:gy1, gx0:gx1] = np.maximum(self.Wm[gy0:gy1, gx0:gx1], np.clip(1 - d / 60, 0, 1) * warm * .8)
    def gate(self, x0, x1, y_bot, y_top, raised=0.0, post=.55, beam=.6, bar_every=.6, depth_dark=True):
        """수문: 양옆 리벳 기둥 + 위 가로대 + 세로 창살 + 가로 띠. raised>0 이면 창살이 그만큼 올라가 아래가 트인다."""
        if depth_dark: self.rect(x0 + post, y_bot, x1 - post, y_top - beam, 1.0, lit=.15)          # 문 안쪽 어둠
        bar_bot = y_bot + raised
        for wx in np.arange(x0 + post + bar_every / 2, x1 - post, bar_every):
            r0, r1, c0, c1 = self.box(wx - .09, bar_bot, wx + .09, y_top - beam)
            rel = np.arange(c0, c1)[None, :] - c0
            self.put(r0, r1, c0, c1, np.where(rel == 0, 4.0, np.where(rel >= (c1 - c0) - 1, 1.8, 2.9)) + np.zeros((r1 - r0, 1)))
            if raised > 0: self.put(r1 - 3, r1, c0, c1, 4.4)                                      # 창살 끝 뾰족
        for wy in np.arange(bar_bot + .8, y_top - beam - .3, 1.6):
            self.band(x0 + post, x1 - post, wy, h=8)
        for (a, b) in ((x0, x0 + post), (x1 - post, x1)):
            r0, r1, c0, c1 = self.box(a, y_bot, b, y_top)
            rel = np.arange(c0, c1)[None, :] - c0
            self.put(r0, r1, c0, c1, 3.1 + (rel < 3) * 1.0 - (rel > (c1 - c0) - 4) * .8 + np.zeros((r1 - r0, 1)))
            for wy in np.arange(y_bot + .4, y_top, .6):
                c = (c0 + c1) // 2; rr = self.cy(wy); self.put(rr, rr + 2, c - 1, c + 1, 5.0)
        self.plate(x0 - .15, y_top - beam, x1 + .15, y_top)
        self.rivets(x0, x1, y_top - beam / 2, .5)
    def catwalk(self, x0, x1, wy, rail=1.4, post_every=1.5, dark=0.0):
        """뒤쪽 캣워크(배경): 밟는 발판으로 안 보이게 한 단계 어둡게."""
        r0, r1, c0, c1 = self.box(x0, wy - .4, x1, wy)
        rel = np.arange(r0, r1)[:, None] - r0
        self.put(r0, r1, c0, c1, (np.where(rel < 2, 3.8, np.where(rel > (r1 - r0) - 3, 1.4, 2.6)) - dark) + np.zeros((1, c1 - c0)))
        self.rivets(x0 + .2, x1 - .2, wy - .2, 1.0, 3.8 - dark)
        for wx in np.arange(x0 + .2, x1, post_every):
            self.rect(wx - .06, wy, wx + .06, wy + rail, 2.8 - dark)
        self.rect(x0, wy + rail - .1, x1, wy + rail, 3.4 - dark)
        self.rect(x0, wy + rail * .5 - .05, x1, wy + rail * .5, 2.6 - dark)
        for wx in np.arange(x0 + 1, x1, 3.0):                                               # 아래 받침 까치발
            for k in range(14):
                c = self.cx(wx) + k; rr = self.cy(wy - .4) + k; self.put(rr, rr + 2, c, c + 2, 2.0 - dark)
    def plaster(self, wx, wy, w, h, seed):
        """회반죽이 남은 자리: 판판한 밝은 면 + 아래·오른쪽 1px 그늘 테 + 금 몇 줄. 가장자리는 벽돌 단위로 들쭉날쭉."""
        r0, r1, c0, c1 = self.box(wx - w / 2, wy - h / 2, wx + w / 2, wy + h / 2)
        yy, xx = np.mgrid[r0:r1, c0:c1]
        nx = (xx - (c0 + c1) / 2) / ((c1 - c0) / 2); ny = (yy - (r0 + r1) / 2) / ((r1 - r0) / 2)
        jag = (r.hshv(((xx + self.left) // 16) * 31 + ((yy - self.top) // 8) * 7 + seed) % 100) / 100 * .45
        inside = (np.abs(nx) ** 1.6 + np.abs(ny) ** 1.6) < .62 + jag
        er = np.zeros_like(inside); er[:-1] = inside[:-1] & ~inside[1:]; ec = np.zeros_like(inside); ec[:, :-1] = inside[:, :-1] & ~inside[:, 1:]
        crack = inside & (np.abs((xx - c0) - (yy - r0) * 1.7 - (seed % 5) * 9) < .7) & (((yy - r0) % 23) < 15)
        val = np.where(er | ec, 2.4, np.where(crack, 2.9, 4.3 + ((xx + yy * 3 + seed) % 29 == 0) * .3))
        self.put(r0, r1, c0, c1, val, lit=.6, mask=inside)
    def outlet(self, wx, wy_bot, R=.85, bars=3):
        """아치 배수구(돌 테 + 창살 + 어두운 속). 입 아래 끝 = wy_bot."""
        cc, cb = self.cx(wx), self.cy(wy_bot); Rp = round(R * 32); ring = 10
        r0, r1, c0, c1 = cb - Rp * 2 - ring, cb + 2, cc - Rp - ring, cc + Rp + ring
        yy, xx = np.mgrid[r0:r1, c0:c1]; dx = xx - cc; cyc = cb - Rp
        inner = (np.abs(dx) < Rp) & ((yy > cyc) | (np.hypot(dx, yy - cyc) < Rp)) & (yy < cb)
        outer = (np.abs(dx) < Rp + ring) & ((yy > cyc) | (np.hypot(dx, yy - cyc) < Rp + ring)) & (yy < cb + 2) & ~inner
        ang = np.degrees(np.arctan2(yy - cyc, dx)); key = ((np.floor(ang / 20)) % 2 == 0) & (yy < cyc)
        self.put(r0, r1, c0, c1, 3.6 + key * .4 + ((yy - r0) < 2) * .6, mask=outer)
        self.put(r0, r1, c0, c1, .6, lit=.1, mask=inner)
        for k in range(1, bars + 1):
            c = cc - Rp + (2 * Rp) * k // (bars + 1); self.put(cyc - Rp // 2, cb, c - 1, c + 2, 2.6, mask=None)
    def waterfall(self, wx, wy_top, wy_bot, half=14):
        r0, r1 = self.cy(wy_top), self.cy(wy_bot); c = self.cx(wx)
        yy, xx = np.mgrid[r0:r1, c - half:c + half + 1]; rel = np.abs(xx - c)
        streak = (r.hshv((xx * 7 + (yy // 5) * 3)) % 9 < 2)
        val = np.where(rel > half - 2, 5.2, 7.0) + streak * 1.6 + (rel < 3) * .8
        self.put(r0, r1, c - half, c + half + 1, val, lit=.3)
        self.L[r0:r1 + 20, c - half - 30:c + half + 30] += 1.1
        # 물보라
        for k in range(-half - 10, half + 11, 3):
            rr = r1 - 2 - (abs(k) * 7 // (half + 10)); self.put(rr, rr + 2, c + k, c + k + 2, 7.4, lit=0)

# ---------------------------------------------------------------- 장면 소품
LAMPS = [(4.0, 3.3), (12.5, 3.3), (24.0, 3.3),          # 길목: 1구간 둘, 2구간 하나, 3구간 없음
         (-8.0, 5.0), (-3.0, 5.0),                      # 앞 방
         (*arena(3.6, 12.0),), (*arena(26.4, 12.0),)]   # 보스방 양옆 랜턴(2층·3층 발판 사이 벽)
SHAFTS = [(42.5, 6.0), (*arena(9.3, 18.0),)]          # 길목 끝 채광, 보스방 천장 격자
def paint_scene(P):
    # ── 길목 1구간(0~17): 평범한 수로. v26 그대로 + 가는 세로관
    P.vpipe(9.0, 0.3, 6.0, thick=12)
    # ── 2구간(17~34): 배관 통로. 천장 굵은 관이 시작되고 받침 기둥이 선다
    P.elbow(16.0, 4.0, .9, 'ul', thick=30); P.vpipe(15.1, 4.0, 6.0, thick=30)        # 천장에서 내려와 꺾이는 관
    P.hpipe(16.0, 47.0, 5.85, thick=30, flange_every=3.0)
    for bx in (21.0, 29.0, 37.0): P.buttress(bx, 4.85, w=1.3, base_w=2.0, base_h=1.8)
    for (x, s) in ((19.5, 5), (33.0, 9)): P.plaster(x, 3.6, 3.0, 1.6, s)
    # ── 3구간(34~51): 보스 예고. 철띠·철판·밸브·관측창·수문
    P.band(34.0, 47.0, 4.4, h=10)
    P.plate(39.4, 2.2, 41.6, 4.0); P.wheel(40.5, 3.1, R=20)
    P.window(43.0, 2.0, 45.4, 3.6, warm=1.0)
    P.elbow(46.6, 4.5, .9, 'dl', thick=30); P.vpipe(47.5, 0.4, 4.5, thick=30)      # 관이 수문 기둥으로 내려간다
    P.gate(48.2, 51.0, 0.0, 6.0, raised=4.4, post=.5, beam=.55, bar_every=.45, depth_dark=False)
    # ── 보스방(이미지 2)
    ax = lambda x: ARENA_X + x; ay = lambda y: y - 3
    for (a0, a1, lit_left) in ((0, 1.6, False), (28.4, 30, True)):                        # 양옆 두꺼운 돌기둥 벽
        r0_, r1_, c0_, c1_ = P.box(ax(a0), ay(0 if AIR else 3), ax(a1), ay(18))
        yy_, xx_ = np.mgrid[r0_:r1_, c0_:c1_]; rel = xx_ - c0_; w_ = c1_ - c0_
        seam = ((yy_ + P.top) % 24 == 0) | (((xx_ + P.left) + ((yy_ + P.top) // 24) * 16) % 32 == 0)
        edge = (rel < 3) if lit_left else (rel > w_ - 4)
        P.put(r0_, r1_, c0_, c1_, 3.3 - seam * .9 + edge * 1.3 - ((rel > w_ - 4) if lit_left else (rel < 3)) * .9)
    for (x, y, w, h, s) in ((ax(7.5), ay(15.6), 3.2, 2.2, 11), (ax(23.0), ay(15.8), 3.6, 2.4, 12)): P.plaster(x, y, w, h, s)
    P.catwalk(ax(1.6), ax(11.0), ay(15.2)); P.catwalk(ax(19.0), ax(28.4), ay(15.2))      # 수문 양옆 뒤쪽 캣워크
    P.catwalk(ax(11.0), ax(19.0), ay(15.2), dark=0.0)
    fl = 0 if AIR else 3                                                                     # 수문 아래 끝(공중전 판은 전장 아래 끝까지)
    P.rect(ax(11.2), ay(fl), ax(18.8), ay(14.5), 0.9, lit=.1)                                 # 수문 안쪽 깊은 어둠
    r0_, r1_, c0_, c1_ = P.box(ax(11.2), ay(fl), ax(18.8), ay(4.6))                         # 수문 뒤에 찬 물(수문이라는 표시)
    yy_, xx_ = np.mgrid[r0_:r1_, c0_:c1_]; d_ = yy_ - r0_
    rip = (r.hshv((xx_ + d_ * 3) // 5 + d_ * 101) % (5 + d_)) == 0
    P.put(r0_, r1_, c0_, c1_, np.where(d_ <= 1, 8.8, np.where(d_ < 4, 7.2, 6.0 - d_ * .03)) + rip * 1.4, lit=.2)
    P.L[r0_ - 60:r1_, c0_:c1_] += np.clip(1 - (r0_ - np.arange(r0_ - 60, r1_)[:, None]).clip(0) / 60, 0, 1) * .9   # 물빛이 수문 안을 위로 비춘다
    P.gate(ax(10.6), ax(19.4), ay(fl), ay(15.2), raised=0.0, post=.6, beam=.7, bar_every=.55, depth_dark=False)  # 큰 수문(닫힘)
    P.rect(ax(13.6), ay(15.2), ax(16.4), ay(17.4), 3.0); P.plate(ax(13.6), ay(15.2), ax(16.4), ay(17.4)); P.wheel(ax(15.0), ay(16.3), R=24)   # 밸브 상자
    for x in (ax(8.6), ax(21.4)): P.vpipe(x, ay(15.2), ay(18), thick=10)                 # 캣워크 매다는 막대
    P.elbow(ax(4.2), ay(16.0), 1.0, 'dl', thick=22); P.hpipe(ax(1.6), ax(3.4), ay(15.65) + 0.0, thick=22, flange_every=9)   # 위 모서리 관
    P.elbow(ax(25.8), ay(16.0), 1.0, 'dr', thick=22); P.hpipe(ax(26.6), ax(28.4), ay(15.65), thick=22, flange_every=9)
    P.vpipe(ax(5.2), ay(16.0), ay(18), thick=22); P.vpipe(ax(24.8), ay(16.0), ay(18), thick=22)
    for x in (ax(4.6), ax(25.4)):
        P.outlet(x, ay(6.6), R=.95, bars=3); P.waterfall(x, ay(6.6), ay(-1.5) if AIR else ay(3.0), half=14)

MOSS = [(30.5, 4.4, 26), (36.2, 4.1, 30), (44.0, 4.3, 22), (49.3, 5.5, 34), (60.0, 11.6, 40), (75.8, 11.4, 46), (66.0, 11.8, 28), (64.5, 2.6, 20)]
def post_bg(bgim, left, top):
    d = ImageDraw.Draw(bgim)
    for (wx, wy, ln) in MOSS:                       # 이끼 흘러내림(초록 1~2px 줄)
        x = round(wx * 32) - left; y = top - round(wy * 32)
        for k in range(5):
            xx = x + k * 3 - 6; l2 = ln - (k * 7) % 13
            d.line((xx, y, xx, y + l2), fill=(58, 82, 46)); d.point((xx, y + l2), fill=(96, 124, 70))

FLOOR_GRATES = [(38.5, 1.5), (43.5, 2.0)]   # 3구간 바닥 배수 격자(월드 x, 폭)
def post_fg(fg, left, top):
    a = np.array(fg)
    for (wx, w) in FLOOR_GRATES:
        x0 = round(wx * 32) - left; y = top - round(0 * 32)
        for x in range(x0, x0 + round(w * 32)):
            if (x - x0) % 5 < 2: a[y + 2:y + 9, x, :3] = (12, 20, 24)
            else: a[y + 2:y + 9, x, :3] = (58, 84, 82)
        a[y + 1, x0:x0 + round(w * 32), :3] = (110, 140, 128); a[y + 9, x0:x0 + round(w * 32), :3] = (8, 12, 14)
    return Image.fromarray(a)

def render(out_dir):
    out = Path(out_dir); out.mkdir(parents=True, exist_ok=True)
    layout = build_layout(); r.layout = layout
    W, H = (X1 - X0) * 32, (Y1 - Y0) * 32; rm.set_canvas(W, H)
    left = X0 * 32 - PAD; top = Y1 * 32 + PAD
    cx = lambda wx: round(wx * 32) - left; cy = lambda wy: top - round(wy * 32)
    P = {'left': left, 'top': top, 'doors': [], 'waters': [], 'pipes': [], 'spots': [],
         'lamps': [(cx(x), cy(y), None) for (x, y) in LAMPS], 'shafts': [(cx(x), cy(y)) for (x, y) in SHAFTS]}
    # draw_props(기둥·관) 뒤, 바닥 쪽 처리 뒤에 이번 소품을 칠하도록 back_wall/draw_props 를 감싼다
    orig_dp = r.draw_props
    def dp(lvl, left_, top_, m, vi, lamps, shafts, L, Wm):
        lvl, tun, py = orig_dp(lvl, left_, top_, m, vi, lamps, shafts, L, Wm)
        # 보스방 안쪽은 기둥 대신 맨 벽돌로 되돌리고(이미지 2 구성), 이번 소품을 칠한다
        base = BASE['lvl']; r0, r1 = top_ - round(15 * 32), top_ - 0; c0, c1 = round(53.6 * 32) - left_, round(80.4 * 32) - left_
        lvl[r0:r1, c0:c1] = base[r0:r1, c0:c1] + np.minimum(L[r0:r1, c0:c1], 6.5)
        h0, h1 = top_ - round(6 * 32), top_; k0, k1 = round(17 * 32) - left_, round(51 * 32) - left_
        lvl[h0:h1, k0:k1] = base[h0:h1, k0:k1] + np.minimum(L[h0:h1, k0:k1], 6.5)
        xs = (np.arange(k0, k1) + left_) / 32.0
        dim = np.clip((xs - 30) / 21, 0, 1) * 1.1                      # 30u 부터 보스방 문 앞까지 서서히 어둡게
        lvl[h0:h1, k0:k1] -= dim[None, :]
        paint_scene(Painter(lvl, Wm, L, m, left_, top_))
        return lvl, tun, py
    orig_bw = r.back_wall
    BASE = {}
    def bw(left_, top_, L, Wm, m, vi):
        img, lvl = orig_bw(left_, top_, L, Wm, m, vi)
        BASE['lvl'] = lvl - np.minimum(L, 6.5); return img, lvl
    r.draw_props, r.back_wall = dp, bw
    try:
        bg, fg, fr = rm.render_tile(P, 0, 0, W, H)
    finally:
        r.draw_props, r.back_wall = orig_dp, orig_bw
    post_bg(bg, left + PAD, top - PAD)
    fg = post_fg(fg, left + PAD, top - PAD)
    bg.save(out / 'background.png'); fg.save(out / 'foreground.png')
    comp = bg.copy(); comp.alpha_composite(fg); comp.alpha_composite(fr)
    comp.convert('RGB').save(out / 'full.png')
    # 미리보기: Unity 가 따로 그리는 1페이즈 발판(승인 타일)과 가운데 기계, 캐릭터를 겹친다.
    # 메인 저장소 unity/Assets/Chapter2/Resources/RoseBluePhase1 경로를 인자 두 번째로 주면 그린다(없으면 건너뛴다).
    U = Path(args[1]) if len(args) > 1 else None
    if U and (U / 'tile.png').exists():
        prev = comp.copy()
        tile = Image.open(U / 'tile.png').convert('RGBA'); mach = Image.open(U / 'machine.png').convert('RGBA')
        px = lambda wx: round((wx - X0) * 32); py = lambda wy: round((Y1 - wy) * 32)
        for (ax_, ay_) in PLATFORMS:
            wx, wy = arena(ax_, ay_); y = py(wy + .5)
            for k in range(0, 6 * 32, tile.width): prev.alpha_composite(tile.crop((0, 0, min(tile.width, 6 * 32 - k), tile.height)), (px(wx) + k, y))
        mw, mh = 2 * 32, 3 * 32; m2 = mach.resize((mw, round(mach.height * mw / mach.width)), Image.NEAREST)
        mx, my = arena(14, 9.5); prev.alpha_composite(m2, (px(mx), py(my) - m2.height))
        for (hx, hy) in ((5.0, 0.0), (26.0, 0.0), (41.0, 0.0), (49.6, 0.0), arena(9.5, 5.0)):
            prev.alpha_composite(r.hero, (px(hx) - r.hero.width // 2, py(hy) - r.hero.height))
        prev.convert('RGB').save(out / 'preview.png')
    meta = {'world': [X0, Y0, X1, Y1], 'ppu': 32, 'hallInner': [3, 51], 'arenaOrigin': [ARENA_X, -3]}
    (out / 'meta.json').write_text(json.dumps(meta), encoding='utf-8')
    return comp

if __name__ == '__main__':
    render(args[0] if args else str(HERE.parent / 'build'))
