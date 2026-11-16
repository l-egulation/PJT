# 무기·스킬 아이콘 도트 그리기 도구. 칸 중심 판정으로 다각형·선·원·호를 찍는다(계단이 고르게 나온다).
import math
import numpy as np
from collections import Counter
from PIL import Image

# 팔레트: 어두운 원 위에 크림 그림. 보조 명암 하나, 강조 금색·청록 하나씩.
CREAM = (239, 230, 212, 255)
LIGHT = (253, 246, 232, 255)
SHADE = (176, 166, 148, 255)
DARK = (96, 88, 76, 255)
GOLD = (247, 195, 76, 255)
GOLD_HI = (255, 224, 140, 255)
TEAL = (44, 199, 195, 255)
TEAL_HI = (150, 236, 230, 255)
RED = (224, 72, 60, 255)
RED_D = (160, 44, 38, 255)
INK = (11, 11, 10, 255)
CLEAR = None

# 재기 규칙: 직선은 같은 마디만 나오는 기울기(0, 1:3, 1:2, 1:1, 2:1, 3:1, 수직)만 쓴다.
ALLOWED = sorted({math.atan2(dy, dx) % math.pi for dx, dy in [(1, 0), (3, 1), (2, 1), (1, 1), (1, 2), (1, 3), (0, 1), (-1, 3), (-1, 2), (-1, 1), (-2, 1), (-3, 1)]})
LINT, CTX = [], [""]
def snap_angle(ang):
    a = ang % math.pi; best = min(ALLOWED + [math.pi], key=lambda b: abs(a - b)); return ang - a + best
def ok_slope(dx, dy):
    a = math.atan2(dy, dx) % math.pi; return min(abs(a - b) for b in ALLOWED + [math.pi]) < .012

def clean_quadrant(widths):
    """위→가운데 줄마다 반폭(늘어나기만 함). 가로 마디는 위에서 아래로 줄고, 세로 마디는 늘게 다시 늘어놓는다."""
    groups = []
    for w in widths:
        if groups and abs(groups[-1][0] - w) < 1e-6: groups[-1][1] += 1
        else: groups.append([w, 1])
    if len(groups) < 2: return widths
    h = sorted([groups[i][0] - groups[i - 1][0] for i in range(1, len(groups))], reverse=True)
    v = sorted(g[1] for g in groups)
    out, w = [], groups[0][0]
    for i, cnt in enumerate(v):
        if i: w += h[i - 1]
        out += [w] * cnt
    return out

def circle_rows(cx, cy, r):
    """중심을 0.5칸 격자에 맞추고 재기 규칙대로 다듬은 원. {y: (x0, x1)} (x1 포함)."""
    cx, cy = round(cx * 2) / 2, round(cy * 2) / 2
    ys = [y for y in range(int(cy - r) - 1, int(cy + r) + 2) if abs(y + .5 - cy) <= r]
    top = [y for y in ys if y + .5 <= cy + 1e-6]
    def half(y):
        d = r * r - (y + .5 - cy) ** 2
        if d < 0: return None
        # 오른쪽 끝 칸 x1: x+.5-cx <= sqrt(d)
        return math.floor(math.sqrt(d) + cx - .5) + 1 - cx      # 가운데선에서 끝 칸 오른쪽 경계까지
    ws = [half(y) for y in top]
    keep = [(y, w) for y, w in zip(top, ws) if w is not None and w > 0]
    if not keep: return {}
    ys_t = [y for y, _ in keep]; ws = clean_quadrant([w for _, w in keep])
    rows = {}
    for y, w in zip(ys_t, ws):
        x0, x1 = int(round(cx - w)), int(round(cx + w)) - 1
        rows[y] = (x0, x1); rows[int(round(2 * cy)) - 1 - y] = (x0, x1)
    return rows

K = [1]   # 서브픽셀 렌더 때 4 로 바꾼다(render 참고)
M = [1.0] # 설계 중심(16,16) 기준 축소 배율. 테두리 여백을 넘는 그림만 render 가 줄인다

class Icon:
    def __init__(self, n, sc=1.0):
        # sc: 좌표·굵기·반지름에 곱하는 배율(32칸으로 잡은 설계를 40칸에 그릴 때 1.25)
        # k: 한 칸을 k×k 로 쪼개 그리는 배수. 내부 격자 n·k, 배율 sc·k. 칸 좌표로 받는 rect·dots·stroke 는 k 를 곱한다.
        self.k = K[0]; self.out = n; n *= self.k; sc *= self.k
        self.n = n; self.sc = sc; self.px = [[None] * n for _ in range(n)]
        self.rd = [[False] * n for _ in range(n)]   # 원(디스크·띠·초승달)에서 온 칸 → render 가 HUD 원판과 같은 서브픽셀로 그린다
    def S(self, p): return (((p[0] - 16) * M[0] + 16) * self.sc, ((p[1] - 16) * M[0] + 16) * self.sc)
    def R(self, r): return r * self.sc * M[0]
    def put(self, x, y, c, rd=False):
        if 0 <= x < self.n and 0 <= y < self.n: self.px[y][x] = c; self.rd[y][x] = rd
    def get(self, x, y): return self.px[y][x] if 0 <= x < self.n and 0 <= y < self.n else None
    # ---- 모양
    def poly(self, pts, c, lint=True):
        pts = [self.S(p) for p in pts]
        if lint and self.k == 1:
            for i in range(len(pts)):
                (ax, ay), (bx, by) = pts[i], pts[(i + 1) % len(pts)]
                if math.hypot(bx - ax, by - ay) > 2.2 and not ok_slope(bx - ax, by - ay):
                    LINT.append("%s: 변 (%.1f,%.1f)-(%.1f,%.1f) 기울기 %.2f" % (CTX[0], ax / self.sc, ay / self.sc, bx / self.sc, by / self.sc, math.degrees(math.atan2(by - ay, bx - ax))))
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        for y in range(max(0, int(min(ys)) - 1), min(self.n, int(max(ys)) + 2)):
            for x in range(max(0, int(min(xs)) - 1), min(self.n, int(max(xs)) + 2)):
                if inside(pts, x + .5, y + .5): self.put(x, y, c)
        return self
    def line(self, a, b, w, c, cap=False):
        (x0, y0), (x1, y1) = a, b; dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy) or 1
        ang = snap_angle(math.atan2(dy, dx)); x1, y1 = x0 + math.cos(ang) * L, y0 + math.sin(ang) * L; dx, dy = x1 - x0, y1 - y0
        nx, ny = -dy / L * w / 2, dx / L * w / 2
        self.poly([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], c, lint=False)
        if cap: self.disc(a, w / 2, c); self.disc(b, w / 2, c)
        return self
    def disc(self, cxy, r, c):
        cx, cy = self.S(cxy); r = self.R(r)
        for y in range(int(cy - r) - 1, int(cy + r) + 2):          # 진짜 원(칸 중심 거리)
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x + .5 - cx) ** 2 + (y + .5 - cy) ** 2 <= r * r: self.put(x, y, c, True)
        return self
    def ring(self, cxy, r_out, r_in, c, a0=None, a1=None):
        """반지름 두 개 사이의 띠(HUD 원판과 같은 방식). 4×4 서브픽셀로 그리면 어느 방향이든 같은 굵기로 보인다."""
        cx, cy = self.S(cxy); ro, ri = self.R(r_out), self.R(r_in)
        for y in range(int(cy - ro) - 1, int(cy + ro) + 2):
            for x in range(int(cx - ro) - 1, int(cx + ro) + 2):
                d = math.hypot(x + .5 - cx, y + .5 - cy)
                if not (ri < d <= ro): continue
                if a0 is not None:
                    a = (math.degrees(math.atan2(y + .5 - cy, x + .5 - cx)) - a0) % 360
                    if a > (a1 - a0) % 360: continue
                self.put(x, y, c, True)
        return self
    def stroke(self, start, steps, c, brush=2):
        """칸 좌표(배율 없음)로 긋는 선. steps: [(dx, dy, 횟수)], 단위는 (1,0)(1,1)(2,1)(1,2)(3,1) 꼴만 → 마디가 늘 같다.
        곡선은 단위 기울기가 차례로 바뀌게 늘어놓는다(재기 규칙)."""
        if self.k > 1:
            k = self.k; x, y = start; verts = [(x, y)]
            for dx, dy, n in steps:
                for _ in range(n): x += dx; y += dy; verts.append((x, y))
            b = brush if not isinstance(brush, list) else brush[0]
            for (ax, ay), (bx, by) in zip(verts, verts[1:]):
                a = ((ax + b / 2) / self.sc * k, (ay + b / 2) / self.sc * k); e = ((bx + b / 2) / self.sc * k, (by + b / 2) / self.sc * k)
                self.line(a, e, b / self.sc * k, c); self.disc(e, b / 2 / self.sc * k, c)
            return verts
        x, y = start; path = [(x, y)]
        for dx, dy, n in steps:
            ax, ay = abs(dx), abs(dy); sx, sy = (dx > 0) - (dx < 0), (dy > 0) - (dy < 0)
            for _ in range(n):
                if ax >= ay: moves = [(sx, 0)] * (ax - ay) + [(sx, sy)] * ay
                else: moves = [(0, sy)] * (ay - ax) + [(sx, sy)] * ax
                for mx, my in moves: x += mx; y += my; path.append((x, y))
        bs = brush if isinstance(brush, list) else [brush] * len(path)
        for (px, py), b in zip(path, bs + [bs[-1]] * (len(path) - len(bs))):
            for i in range(b):
                for j in range(b): self.put(px + i, py + j, c)
        return path
    def rect(self, x, y, w, h, c):
        k = self.k
        for j in range(h * k):
            for i in range(w * k): self.put(x * k + i, y * k + j, c)
        return self
    def dots(self, pts, c):
        for x, y in pts: self.rect(x, y, 1, 1, c)
        return self
    def rows(self, ox, oy, rows, cmap):
        for j, r in enumerate(rows):
            for i, ch in enumerate(r):
                if ch in cmap: self.put(ox + i, oy + j, cmap[ch])
        return self
    def outline(self, c=INK, of=None):
        """그림 둘레 바깥 1칸에 테(겹친 그림 사이 경계를 또렷하게)."""
        src = [row[:] for row in self.px]
        for y in range(self.n):
            for x in range(self.n):
                if src[y][x] is not None: continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < self.n and 0 <= yy < self.n and src[yy][xx] is not None and (of is None or src[yy][xx] in of):
                        self.px[y][x] = c; break
        return self
    def bevel(self):
        """위가 비어 있는 칸은 한 단 밝게(빛이 위에서 온다)."""
        hi = {GOLD: GOLD_HI, TEAL: TEAL_HI, CREAM: LIGHT}
        src = [row[:] for row in self.px]
        for y in range(self.n):
            for x in range(self.n):
                c = src[y][x]
                if c in hi and (y == 0 or src[y - 1][x] is None): self.px[y][x] = hi[c]
        return self
    def image(self, scale=1):
        im = Image.new("RGBA", (self.n, self.n), (0, 0, 0, 0))
        for y in range(self.n):
            for x in range(self.n):
                if self.px[y][x]: im.putpixel((x, y), self.px[y][x])
        return im.resize((self.n * scale, self.n * scale), Image.NEAREST) if scale > 1 else im

def inside(pts, x, y):
    c = False; n = len(pts)
    for i in range(n):
        x0, y0 = pts[i]; x1, y1 = pts[(i + 1) % n]
        if (y0 > y) != (y1 > y) and x < (x1 - x0) * (y - y0) / (y1 - y0) + x0: c = not c
    return c

def diag(t, o=0.0, n=32):
    """왼쪽 아래(손잡이)→오른쪽 위(끝) 45° 축 위의 점. t: 축 위 거리, o: 축에 수직 거리(+는 오른쪽 아래)."""
    s = 1 / math.sqrt(2)
    return (4 + t * s + o * s, n - 4 - t * s + o * s)

SAFE_HITS, SHRUNK = [], []
def shift(px, sx, sy, empty=None):
    n = len(px); out = [[empty] * n for _ in range(n)]
    for y in range(n):
        for x in range(n):
            if 0 <= x + sx < n and 0 <= y + sy < n: out[y + sy][x + sx] = px[y][x]
    return out

def mix(a, b, w):
    return tuple(int(round(a[i] + (b[i] - a[i]) * w)) for i in range(3)) + (255,)

def offset_poly(pts, d):
    """볼록 다각형의 변을 안쪽으로 d 만큼 평행 이동(변 기울기 그대로 → 재기 규칙 유지)."""
    n = len(pts); area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
    sgn = 1 if area > 0 else -1; lines = []
    for i in range(n):
        (ax, ay), (bx, by) = pts[i], pts[(i + 1) % n]; L = math.hypot(bx - ax, by - ay)
        nx, ny = -(by - ay) / L * sgn, (bx - ax) / L * sgn
        lines.append(((ax + nx * d, ay + ny * d), (bx - ax, by - ay)))
    out = []
    for i in range(n):
        (p1, d1), (p2, d2) = lines[i - 1], lines[i]
        den = d1[0] * d2[1] - d1[1] * d2[0]
        t = ((p2[0] - p1[0]) * d2[1] - (p2[1] - p1[1]) * d2[0]) / den
        out.append((p1[0] + d1[0] * t, p1[1] + d1[1] * t))
    return out

def render(fn, safe=None, name="", center=True):
    """선명한 1배 그림 + 4×4 서브픽셀 가장자리. 1배에서 빈 칸인데 4배 그림이 걸친 칸은 덮인 비율을 25·50·75% 로 반올림해 반투명으로 채운다.
    safe: 그림 중심에서 이 반지름(칸 중심 기준)을 넘는 칸은 지우고 SAFE_HITS 에 남긴다."""
    M[0] = 1.0
    try:
        for _ in range(16):
            K[0] = 1; crisp = fn(); n = crisp.out
            if center:   # 그림 테두리 상자를 원 가운데로(정수 칸만큼 옮긴다)
                pts = [(x, y) for y in range(n) for x in range(n) if crisp.px[y][x] is not None]
                xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
                sx, sy = int(round(n / 2 - (min(xs) + max(xs) + 1) / 2)), int(round(n / 2 - (min(ys) + max(ys) + 1) / 2))
                nx, ny = getattr(fn, 'nudge', (0, 0)); sx += nx; sy += ny   # 눈으로 본 무게중심 보정(칸)
                crisp.px = shift(crisp.px, sx, sy); crisp.rd = shift(crisp.rd, sx, sy, False)
            far = max([math.hypot(x + .5 - n / 2, y + .5 - n / 2) for y in range(n) for x in range(n) if crisp.px[y][x] is not None] or [0])
            if safe is None or far <= safe: break
            M[0] *= .97
        K[0] = 4; fine = fn()
        if center: fine.px = shift(fine.px, sx * 4, sy * 4); fine.rd = shift(fine.rd, sx * 4, sy * 4, False)
    finally: K[0] = 1; shrink = M[0]; M[0] = 1.0
    if shrink < 1: SHRUNK.append("%s ×%.2f" % (name, shrink))
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0)); hit = 0
    for y in range(n):
        for x in range(n):
            if safe is not None and math.hypot(x + .5 - n / 2, y + .5 - n / 2) > safe:
                if crisp.px[y][x] is not None: hit += 1
                continue
            c = crisp.px[y][x]
            cell = [fine.px[y * 4 + j][x * 4 + i] for j in range(4) for i in range(4)]
            roundish = crisp.rd[y][x] or any(fine.rd[y * 4 + j][x * 4 + i] for j in range(4) for i in range(4))
            if roundish:   # 원 근처: HUD Supersample 과 같은 규칙(가장 많은 색에 두 번째 색을 25·50·75% 로만 섞는다)
                cnt = Counter(cell).most_common(); A = cnt[0][0]
                if len(cnt) == 1:
                    if A is not None: im.putpixel((x, y), A)
                    continue
                B, nb = cnt[1]; w = round(nb / 16 * 4) / 4
                if A is None: col = B[:3] + (int(255 * w),) if w > 0 else None
                elif B is None: col = A[:3] + (int(255 * (1 - w)),)
                else: col = mix(A, B, w)
                if col: im.putpixel((x, y), col)
                continue
            if c is not None: im.putpixel((x, y), c); continue
            got = [v for v in cell if v is not None]
            q = round(len(got) / 16 * 4) / 4
            if q <= 0: continue
            q = min(q, .75); col = Counter(got).most_common(1)[0][0]
            im.putpixel((x, y), col[:3] + (int(255 * q),))
    if hit: SAFE_HITS.append("%s: 테두리 여백을 넘은 칸 %d" % (name, hit))
    return im
