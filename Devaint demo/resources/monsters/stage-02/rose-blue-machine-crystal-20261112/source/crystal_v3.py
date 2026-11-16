# 1페이즈 중앙 장치 E3 「잉크 결정」 3차: 1차 크기(그림 96×128, 판정 2×3u)로 돌아가고
# 둘레에 떠다니는 구슬·반짝이 조각과 결정 속 얇은 선(잉크 띠·세로 빛줄)을 뺐다. 면 명암과 속 빛 심지로만 읽히게. 12장.
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent / 'build'   # 미리보기 출력(커밋하지 않는다). 실행용 시트는 export_runtime.py
W, H, N = 96, 128, 12
INK = (14, 8, 21)
V = [(24, 14, 34), (46, 26, 64), (72, 40, 102), (104, 62, 146), (142, 98, 196), (186, 150, 234), (226, 206, 252), (248, 242, 255)]

def outline(a):
    al = a[..., 3] > 200; p = np.pad(al, 1)
    ring = (p[2:, 1:-1] | p[:-2, 1:-1] | p[1:-1, 2:] | p[1:-1, :-2]) & ~al
    a[ring] = (*INK, 255); return a

def poly_mask(pts):
    im = Image.new('L', (W, H)); ImageDraw.Draw(im).polygon(pts, fill=255); return np.array(im) > 0

def frame(f, accent='A', core='none', damage=0):
    # damage: 0 멀쩡 · 1 귀퉁이 둘 떨어짐 · 2 위 끝까지 깨지고 어두워짐
    # core: none 면만 · inner 속 결정 · ink 잉크 고임 · facet 속 굴절면
    # accent: A 없음 · B 꼭대기 차오름 · C 빛 쓸기 · D 심지 솟음 · E 가장자리 빛 · F 면 차례로
    ph = f / N * 2 * math.pi; bob = round(2 * math.sin(ph)); cx, cy = 48, 70 + bob
    a = np.zeros((H, W, 4), np.uint8); yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    T, S0, S1, B = cy - 44, cy - 20, cy + 22, cy + 46
    L, R, l2, r2 = cx - 16, cx + 16, cx - 6, cx + 6
    faces = [  # 왼 바깥(빛 받는 면)·왼 안·오른 안·오른 바깥(그늘). (꼭짓점, 기둥, 윗뿔, 아랫뿔)
        ([(L, S0), (l2, S0), (l2, S1), (L, S1)], V[5], V[6], V[3]),
        ([(l2, S0), (cx, S0 - 2), (cx, S1 + 2), (l2, S1)], V[4], V[5], V[2]),
        ([(cx, S0 - 2), (r2, S0), (r2, S1), (cx, S1 + 2)], V[3], V[4], V[2]),
        ([(r2, S0), (R, S0), (R, S1), (r2, S1)], V[2], V[3], V[1]),
    ]
    whole = poly_mask([(cx, T), (R, S0), (R, S1), (cx, B), (L, S1), (L, S0)])
    for (pts, col, topc, botc) in faces:
        a[poly_mask(pts)] = (*col, 255)
        a[poly_mask([(cx, T), pts[0], pts[1]])] = (*topc, 255)
        a[poly_mask([(cx, B), pts[3], pts[2]])] = (*botc, 255)
    # 속 빛 심지: 맥박치는 타원 두 겹(덮어 섞기, 선 없음)
    pulse = .5 + .5 * math.sin(ph * 2)
    def blend(m, c, k):
        a[m, :3] = (a[m, :3] * (1 - k) + np.array(c) * k).astype(np.uint8)
    if core == 'inner':    # 결정 속에 작은 결정 하나(밝은 쪽 왼반, 맥박)
        ip = [(cx, cy - 24), (cx + 7, cy - 11), (cx + 7, cy + 12), (cx, cy + 25), (cx - 7, cy + 12), (cx - 7, cy - 11)]
        im_ = poly_mask(ip); blend(im_ & (xx < cx), V[7], .30 + .15 * pulse); blend(im_ & (xx >= cx), V[6], .18 + .12 * pulse)
    elif core == 'ink':    # 아래쪽에 잉크가 고여 출렁임(위는 맑은 결정)
        lvl = S1 - 4 - 3 * pulse + 1.5 * np.sin(xx * .45 + ph * 2)
        pool = whole & (yy > lvl); blend(pool, V[1], .55); blend(pool & (yy > lvl + 9), V[0], .35)
    elif core == 'facet':  # 속에 굴절면 둘이 번갈아 빛남
        k1 = .5 + .5 * math.sin(ph); k2 = 1 - k1
        blend(whole & poly_mask([(L + 2, S0 + 5), (cx - 1, cy - 4), (L + 2, cy + 12)]), V[7], .12 + .3 * k1)
        blend(whole & poly_mask([(r2 + 1, S0 + 3), (R - 2, S0 + 16), (cx + 1, cy + 8)]), V[6], .08 + .3 * k2)
        blend(whole & poly_mask([(l2 + 1, cy + 6), (r2 - 1, cy + 2), (cx, S1 + 10)]), V[1], .25)
    # 빛 맺힌 점 몇 개(왼쪽 윗면) — 선이 아니라 점
    for (px_, py_) in ((cx - 4, T + 10), (cx - 5, T + 11), (L + 2, S0 + 4), (L + 2, S0 + 5)):
        a[py_, px_] = (*V[7], 255)
    # ---- 손상 단계: 선(금) 대신 귀퉁이가 떨어져 나간 자리로 보인다
    if damage:
        chips = [[(L - 2, cy - 7), (L + 5, cy - 2), (L - 2, cy + 4)], [(R + 2, cy + 9), (R - 4, cy + 14), (R + 2, cy + 20)]]
        if damage >= 2:
            chips += [[(cx - 12, T - 2), (cx + 12, T - 2), (cx + 12, T + 9), (cx + 4, T + 13), (cx - 1, T + 8), (cx - 12, T + 12)],
                      [(L - 2, S1 + 1), (L + 8, S1 + 7), (L + 4, S1 + 15), (L - 2, S1 + 14)],
                      [(R + 2, S0 - 4), (R - 5, S0 + 1), (R + 2, S0 + 7)]]
            blend(whole, V[1], .22)
        cut = np.zeros_like(whole)
        for c_ in chips: cut |= poly_mask(c_)
        cut &= whole; a[cut] = 0
        pc = np.pad(cut, 1); rim = whole & ~cut & (pc[2:, 1:-1] | pc[:-2, 1:-1] | pc[1:-1, 2:] | pc[1:-1, :-2])
        blend(rim, V[6], .55)                       # 새로 드러난 깨진 면은 밝게
    # ---- 위쪽 강조(시안별). 결정 밖으로 아무것도 띄우지 않는다
    t = f / N
    if accent == 'B':      # 위 뿔이 꼭짓점부터 아래로 빛이 차올랐다 빠짐
        k = max(0., 1 - abs(t - .25) / .25)
        if k > 0: blend(whole & (yy < T + 4 + 20 * k), V[7], .55 * k)
    elif accent == 'C':    # 왼쪽 위에서 오른쪽 아래로 넓은 빛이 면을 한 번 쓸고 감
        if t < .5:
            c0 = -30 + t / .5 * 110; band = np.abs((xx - L) + (yy - T) * .45 - c0)
            blend(whole & (band < 6), V[7], .38)
    elif accent == 'D':    # 심지 빛 한 덩이가 위 끝까지 솟았다 사라짐
        if t < .5:
            yc = cy - (t / .5) * (cy - T - 6); k = 1 - t / .5 * .6
            blend(whole & (((xx - cx) / (5 - 2 * t)) ** 2 + ((yy - yc) / 6) ** 2 < 1), V[7], .6 * k)
    elif accent == 'E':    # 결정 가장자리 안쪽 1px 이 맥박에 맞춰 밝아짐
        pw = np.pad(whole, 1); inner = whole & ~(pw[2:, 1:-1] & pw[:-2, 1:-1] & pw[1:-1, 2:] & pw[1:-1, :-2])
        blend(inner, V[6], .25 + .45 * pulse)
    elif accent == 'F':    # 네 면이 왼쪽부터 차례로 한 단 밝아짐
        i = f // 3
        pts = faces[i][0]; m = poly_mask(pts) | poly_mask([(cx, T), pts[0], pts[1]]) | poly_mask([(cx, B), pts[3], pts[2]])
        blend(whole & m, V[7], .28)
    a = outline(a)
    # 빛 번짐(4단계) — 결정 뒤
    g = np.zeros((H, W, 4), np.float32); dist = np.hypot(xx - cx, (yy - cy) / 1.35)
    g[..., :3] = V[3]; g[..., 3] = np.floor(np.clip(1 - dist / 48, 0, 1) * 4) / 4 * (.28 + .08 * pulse) * 255
    out = Image.fromarray(g.astype(np.uint8)); out.alpha_composite(Image.fromarray(a))
    d = ImageDraw.Draw(out)
    s = 20 - bob * 2; d.ellipse((cx - s, 122, cx + s, 127), fill=(*V[0], 150))   # 그림자
    return out

if __name__ == '__main__':
    OUT.mkdir(parents=True, exist_ok=True)
    frames = [frame(i) for i in range(N)]  # 기본(A)
    sheet = Image.new('RGBA', (W * N, H))
    for i, fr in enumerate(frames): sheet.alpha_composite(fr, (i * W, 0)); fr.save(OUT / f'E3v3-{i:02d}.png')
    sheet.save(OUT / 'E3v3-sheet.png')
    gif = []
    for fr in frames:
        g = Image.new('RGBA', (W * 3, H * 3), (13, 20, 24, 255)); g.alpha_composite(fr.resize((W * 3, H * 3), Image.NEAREST)); gif.append(g.convert('RGB'))
    gif[0].save(OUT / 'E3v3-idle-3x.gif', save_all=True, append_images=gif[1:], duration=85, loop=0)
    print('E3v3 ok')
