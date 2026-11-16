# 가시 그물: 앞 시안(감싸는 그물, 멈춘 그림) vs 물리 그물(던져 펼쳐지며 쓸고 떨어져 적·땅에 내려앉음). 게임 속도 GIF·정지컷.
import json, sys, random
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, str(Path(__file__).resolve().parent))
import net_scale as ns, net_wrap as nw, net_physics as ph, net_fall as nf, net_fan as fan
OUT = Path(__file__).resolve().parent.parent
S = json.loads((OUT / '_작업' / 'scale.json').read_text(encoding='utf-8')); PAD = (0, 130, 900, 0)
WRAP = ns.build(S['SX'], S['SY'], PAD, nw.make)
# 물리 판의 몸: 그물 장(15~19)은 그물을 지운 몸만(주먹은 남김)
BODY = {}
for n in [f.stem for f in sorted(ns.SRC.glob('*.png'))]:
    if n in ns.NET:
        a, net, grip = ns.split(n); b = a.copy(); b[net] = 0; BODY[n] = Image.fromarray(b)
    else: BODY[n] = Image.open(ns.SRC / f'{n}.png').convert('RGBA')
ORDER = ['01', '03', '04', '05', '06', '08', '10', '12', '13', '14', '15', '16', '17', '18', '19']
SECS = [0.03] * 7 + [0.035] * 4 + [0.06, 0.06, 0.05, 0.28]
MON = Image.open(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-large-projectile-20261112/unity/Assets/Deviant/Stage1Monsters/Art/01_thorn-root/idle/00.png').convert('RGBA')
U = 40; X0, X1, Y0, Y1 = -1.5, 13.2, -.4, 6.0; W, H = int((X1 - X0) * U), int((Y1 - Y0) * U)
NP = 88.22                                  # 그물을 몸 그림과 같은 화소 밀도로 그린 뒤 같이 줄인다
gx = lambda x: (x - X0) * U; gy = lambda y: (Y1 - y) * U
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 17); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 14)
INK = (27, 22, 22, 255)
DISSOLVE_FROM, DISSOLVE_TO = 1.0, 1.25
rng = random.Random(7); NOISE = np.array([[rng.random() for _ in range(2000)] for _ in range(700)])

def spline(pts, k=6):
    """Catmull-Rom 으로 매끄럽게."""
    pts = [pts[0]] + list(pts) + [pts[-1]]; out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = map(np.asarray, pts[i - 1:i + 3])
        for s in range(k):
            t = s / k; t2, t3 = t * t, t * t * t
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(np.asarray(pts[-2])); return out

def net_layer(P, t):
    """노드 격자를 몸 그림 화소 밀도(NP)로 그린 장. 반환: (그림, 왼쪽 위 월드 좌표)."""
    wx0, wy1 = X0, Y1; Wn, Hn = int((X1 - X0) * NP), int((Y1 - Y0) * NP)
    im = Image.new('RGBA', (Wn, Hn)); d = ImageDraw.Draw(im)
    px = lambda p: ((p[0] - wx0) * NP, (wy1 - p[1]) * NP)
    Nn, Mm = P.shape[0] - 1, P.shape[1] - 1
    for i in range(1, Nn + 1):                       # 가로 줄(채찍은 따로 굵게)
        pts = [px(P[i, j]) for j in range(Mm + 1)]; d.line([tuple(q) for q in spline(pts)], fill=INK, width=2)
    for j in range(1, Mm + 1):                       # 세로 줄
        pts = [px(P[i, j]) for i in range(Nn + 1)]; d.line([tuple(q) for q in spline(pts)], fill=INK, width=2)
    whip = [px(P[0, j]) for j in range(Mm + 1)]; d.line([tuple(q) for q in spline(whip)], fill=INK, width=5)
    pts = [px(P[i, 0]) for i in range(Nn + 1)]; d.line([tuple(q) for q in spline(pts)], fill=INK, width=2)
    # 테두리 가시: 테두리 줄 마디마다 바깥으로 짧은 가시 둘
    rim = spline([px(P[Nn, j]) for j in range(Mm + 1)], 3)
    for k in range(1, len(rim) - 1, 2):
        a, b = np.asarray(rim[k - 1]), np.asarray(rim[k + 1]); tng = (b - a) / (np.linalg.norm(b - a) + 1e-6); nrm = np.array([tng[1], -tng[0]])
        c = np.asarray(rim[k]); tip = c + nrm * 7 + tng * 3; d.line((tuple(c), tuple(tip)), fill=INK, width=2)
    if t > DISSOLVE_FROM:                            # 사라짐: 화소가 무작위로 빠진다(반투명 없음)
        a = np.asarray(im).copy(); q = (t - DISSOLVE_FROM) / (DISSOLVE_TO - DISSOLVE_FROM)
        a[NOISE[:a.shape[0], :a.shape[1]] < q] = 0; im = Image.fromarray(a)
    return im

def sim_at(t):
    if t < ph.START: return None
    i = min(len(SIM) - 1, int(round((t - ph.START) * 60))); return SIM[i][1]

def base(t):
    c = Image.new('RGBA', (W, H), (246, 240, 228, 255)); d = ImageDraw.Draw(c)
    d.rectangle((0, gy(0), W, H), fill=(226, 216, 198)); d.line((0, gy(0), W, gy(0)), fill=(90, 80, 70), width=2)
    hot = .35 <= t < .45
    d.rectangle((gx(ph.BOX_L), gy(ph.BOX_T), gx(ph.BOX_R), gy(0)), outline=(220, 60, 50) if hot else (220, 160, 150), width=2)
    for mx in (3.0, 6.2, 9.0):
        m = MON.resize((round(MON.width * U / 32), round(MON.height * U / 32)), Image.NEAREST).transpose(Image.FLIP_LEFT_RIGHT)
        c.alpha_composite(m, (int(gx(mx) - .5 * m.width), int(gy(0) - (1 - .0625) * m.height)))
    return c
def put(c, im, pad):
    s = im.resize((round(im.width * U / ns.PPU), round(im.height * U / ns.PPU)), Image.NEAREST)
    px, py = (ns.PX + pad[0]) * U / ns.PPU, (ns.GROUND + pad[1]) * U / ns.PPU
    c.alpha_composite(s, (int(round(gx(0) - px)), int(round(gy(0) - py))))
def body_at(t):
    acc = 0
    for k, s in zip(ORDER, SECS):
        if t < acc + s: return k
        acc += s
    return ORDER[-1]
def panel(t):
    n = body_at(t)
    out = Image.new('RGB', (2 * W + 12, H + 50), 'white'); d = ImageDraw.Draw(out)
    a = base(t); put(a, WRAP[n], PAD)
    b = base(t); put(b, BODY[n], (0, 0, 0, 0))
    L = fan.layer(t, X0, Y1, int((X1 - X0) * NP), int((Y1 - Y0) * NP), NP)
    if L is not None: b.alpha_composite(L.resize((W, H), Image.NEAREST))
    for i, (img, name, note) in enumerate(((a, '앞 시안 (감싸는 그물)', '그린 그물이 공중에 멈춰 있음'), (b, '부채 그물', '채찍 위 뿌리에서 펴져 위로 넘어오며 커지고 적 너머까지 덮음'))):
        x = i * (W + 12); out.paste(img.convert('RGB'), (x, 50)); d.text((x + 6, 2), name, font=F, fill=(0, 0, 0)); d.text((x + 6, 26), note, font=FS, fill=(80, 80, 80))
        d.text((x + W - 70, 56), f'{t:.2f}초', font=FS, fill=(120, 120, 120))
    return out
if __name__ == '__main__':
    STEP = 1 / 60; total = 1.5
    frames = [panel(i * STEP) for i in range(int(total / STEP))]
    def save(path, mul):
        keep = frames[::2]; q = [f.quantize(colors=64, method=Image.Quantize.MEDIANCUT) for f in keep]
        ms = [30 if (k % 2 == 0) else 40 for k in range(len(q))] if mul == 1 else [int(2000 / 60 * mul)] * len(q); ms[-1] += 500
        q[0].save(path, save_all=True, append_images=q[1:], duration=ms, loop=0, disposal=1)
    save(OUT / '가시그물-부채v5-게임속도.gif', 1); save(OUT / '가시그물-부채v5-0.5배.gif', 2)
    cuts = (.34, .38, .42, .47, .52, .57, .62, .70, .90, 1.25)
    still = Image.new('RGB', (W, len(cuts) * (H + 24)), 'white'); dd = ImageDraw.Draw(still)
    for k, t in enumerate(cuts):
        im = frames[round(t / STEP)].crop((W + 12, 50, 2 * W + 12, H + 50)); still.paste(im, (0, k * (H + 24) + 22)); dd.text((4, k * (H + 24) + 2), f'부채 그물 {t:.2f}초', font=FS, fill=(0, 0, 0))
    still.save(OUT / '가시그물-부채v5-정지컷.png'); print('ok', len(frames))
