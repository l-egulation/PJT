# 수리검 여섯 동작 프레임 고르기(2026-11-08). 에테르 6x6 시트(3840x2160, 칸 640x360). 입력은 무대 192x108 의 20배를 6분의 1 로 받은 칸
# → 칸 1px = 무대 0.3px, 1unit(32 무대px) = 106.67 칸px, 발밑 (220, 327). 미리보기 1unit = 64px 이라 칸을 0.6배.
# 시간은 data/combat/ranged_basic_design.json 그대로. 흰 배경은 문턱으로만 빼고(배경 제거 전), 몸에서 떨어진 날아가는 별 조각은 지운다.
import json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
HERE = Path(__file__).resolve().parent
H4 = HERE / '../2_에테르결과/aether-history/shuriken-v4-20261107'; H5 = HERE / '../2_에테르결과/aether-history/shuriken-v5-20261107'
SHEETS = {'00': H4 / 'ffa82e08-7b98-4d94-a698-f6b47f74d644.png', '01': H4 / '76847070-2836-40b9-b4d0-501d03c5bcae.png',
          '02': H5 / 'add7de09-1311-41d1-a15f-78ddf266828f.png', '03': H4 / 'e0584b51-f43b-4064-8a08-cc7a9c2cdb67.png',
          '03b': H4 / '0dc869f3-154e-44f4-850b-2d4cbebc569b.png', '04': H5 / '8ea8c658-b4c6-4bcb-a882-9371df43e7c4.png'}
PIV = (220, 327); K = 0.6; U = 64
ART = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/whip-skill-motion-20261107/unity/Assets/Deviant/Stage1Monsters/Art')
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 16)

# 동작마다: 이름, [(시각, 칸)], 발사 시각, 끝, 탄(지름·속도·생성 위치), 이동
SK = {
 '00': dict(name='평타 2타(밀고 들어가며)', tl=[(0, 3), (.04, 4), (.08, 7), (.10, 8), (.15, 9), (.20, 11),
            (.25, 14), (.30, 15), (.37, 17), (.44, 18), (.52, 20), (.60, 26), (.66, 28), (.72, 29), (.78, 30)],
            fire=[.10, .37], end=.60, tail=.84, shot=(.4, 22, (.85625, 1)), move=5),
 '01': dict(name='큰 관통 수리검', tl=[(0, 3), (.05, 5), (.10, 7), (.15, 9), (.20, 10), (.27, 11), (.35, 13), (.45, 24), (.50, 26), (.55, 28)],
            fire=[.20], end=.60, tail=.70, shot=(.8, 18, (1.10625, 1)), move=0),
 # 02: 휘두름 호가 몸을 가로지른 #11~14 는 정리로 못 지워 뺐다(11-08). 감기 #10 → 던진 뒤 #17, 휘두름은 이펙트로 그린다.
 '02': dict(name='폭발 수리검', tl=[(0, 3), (.05, 5), (.10, 8), (.15, 10), (.20, 17), (.30, 22), (.43, 29), (.50, 32), (.56, 33)],
            fire=[.20], end=.60, tail=.70, shot=(.8, 18, (1.10625, 1)), move=0),
 '03': dict(name='적 뒤 이동 던지기', tl=[(0, 2), (.07, 4), (.13, 5), (.20, 6), (.26, 8), (.32, 12), (.38, 14), (.46, 17), (.54, 21), (.58, 28)],
            fire=[.20], end=.60, tail=.70, shot=(.8, 18, (1.10625, 1)), move=0, arrive='03b'),
 '03b': dict(name='적 뒤 도착', tl=[(0, 8), (.05, 10), (.10, 13), (.16, 16), (.21, 17), (.26, 19), (.31, 21), (.36, 24)], end=.40),
 '04': dict(name='전진 회전 수리검', tl=[(0, 4), (.05, 6), (.10, 9), (.15, 11), (.20, 12), (.25, 14), (.32, 18), (.40, 26), (.46, 28), (.52, 30)],
            fire=[.20], end=.20, tail=.56, shot=(3, 3, (2.20625, 1.6)), move=0),
}
_cells = {}
GROUND, IDLE_REAR = 327, 217     # 대기 칸(#0)의 밑창 줄·뒷발 왼끝
def raw(key, i):
    if key not in _cells: _cells[key] = np.array(Image.open(SHEETS[key]).convert('RGB')).astype(np.int16)
    a = _cells[key]; return a[(i // 6) * 360:(i // 6 + 1) * 360, (i % 6) * 640:(i % 6 + 1) * 640]
def feet(key, i):
    """장화(노랑)가 붙은 발 덩어리들의 (왼끝, 오른끝, 밑) — 칸 아래쪽 것만."""
    c = raw(key, i); r, g, b = c[..., 0], c[..., 1], c[..., 2]
    boot = (r > 200) & (g > 130) & (g < 200) & (b < 80); m = (c.max(2) < 70) | boot; m[:270] = False
    lab, n = ndi.label(m); out = []
    for j in range(1, n + 1):
        part = lab == j; ys, xs = np.nonzero(part)
        w, h = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
        if len(xs) >= 40 and boot[part].any() and ys.max() >= 285 and 8 <= w <= 40 and h <= 32: out.append((xs.min(), xs.max(), ys.max()))   # 쥔 별(넓다)은 뺀다
    return sorted(out)
# 발 맞춤(2026-11-08 사용자 「발 위치만 맞춰서」): 세로는 장마다 가장 낮은 발을 땅(327)에, 가로는 동작마다 하나의 값 —
# 마지막(대기로 돌아온) 장의 뒷발 왼끝을 대기 칸과 같게(장마다 따로 옮기면 디딘 발이 미끄러진다).
def sole_bottom(key, i):
    """밑창(검정) 맨 아래 줄. 쥔 별(노란 속이 검정으로 둘러싸인 덩어리)은 반경 30px 를 빼고 잰다.
    (장화 노랑과 밑창 검정이 따로 떨어진 장이 있어 장화 덩어리 밑으로 재면 10px 씩 흔들렸다 — 11-08 2차)"""
    c = raw(key, i); r, g, b = c[..., 0], c[..., 1], c[..., 2]
    yel = (r > 200) & (g > 130) & (g < 200) & (b < 80); black = c.max(2) < 70
    m = c.min(2) < 236; lab, n = ndi.label(m); sz = ndi.sum(m, lab, range(1, n + 1)); body = ndi.binary_dilation(lab == (1 + int(np.argmax(sz))), iterations=6)
    keep = black & body
    yl, yn = ndi.label(yel)
    for j in range(1, yn + 1):
        part = yl == j; ring = ndi.binary_dilation(part, iterations=4) & ~part
        if ring.sum() and (black & ring).sum() / ring.sum() > .7:          # 별 속(사방이 검정) → 별 몸 지우기
            cy, cx = [v.mean() for v in np.nonzero(part)]; yy, xx = np.ogrid[:360, :640]
            keep &= (yy - cy) ** 2 + (xx - cx) ** 2 > 30 ** 2
    ys, xs = np.nonzero(keep[230:]); return int(ys.max() + 230) if len(ys) else GROUND
def shift_of(key, i):
    dy = GROUND - sole_bottom(key, i)
    last = SK[key]['tl'][-1][1]; fl = feet(key, last); g = max(y for _, _, y in fl) if fl else 0
    fl = [f for f in fl if f[2] >= g - 15]; dx = IDLE_REAR - fl[0][0] if fl else 0
    return dx, dy
def cell(key, i):
    k = (key, i)
    if k not in _cells:
        c = raw(key, i); dx, dy = shift_of(key, i)
        c = np.roll(np.roll(c, dy, 0), dx, 1)
        if dy > 0: c[:dy] = 255
        elif dy < 0: c[dy:] = 255
        if dx > 0: c[:, :dx] = 255
        elif dx < 0: c[:, dx:] = 255
        m = c.min(2) < 236
        lab, n = ndi.label(m); sz = ndi.sum(m, lab, range(1, n + 1)); body = lab == (1 + int(np.argmax(sz)))
        near = ndi.binary_dilation(body, iterations=6)
        keep = np.zeros_like(m)
        for j in range(1, n + 1):
            part = lab == j
            if (part & near).any(): keep |= part      # 몸에 붙은 것(쥔 별·목도리)만 남기고 떨어진 날아가는 별은 뺀다
        rgba = np.dstack([c.astype(np.uint8), (keep * 255).astype(np.uint8)])
        im = Image.fromarray(rgba, 'RGBA'); _cells[k] = im.resize((round(640 * K), round(360 * K)), Image.LANCZOS)
    return _cells[k]
def big(im): return im.resize((im.width * 2, im.height * 2), Image.NEAREST)
MOB = big(Image.open(ART / '01_thorn-root/idle/00.png').convert('RGBA')); MOB_CY = 30 / 32
def at(tl, t):
    k = tl[0][1]
    for a, b in tl:
        if t >= a: k = b
    return k
def star(d, x, y, r, ang):
    pts = []
    for i in range(8):
        rr = r if i % 2 == 0 else r * .35; a = ang + i * math.pi / 4; pts.append((x + rr * math.cos(a), y + rr * math.sin(a)))
    d.polygon(pts, fill=(27, 22, 22)); d.ellipse((x - r * .18, y - r * .18, x + r * .18, y + r * .18), fill=(235, 165, 22))
W, H, GY = 720, 300, 262
def scene(key, t):
    s = SK[key]; c = Image.new('RGBA', (W, H), (255, 255, 255, 255)); d = ImageDraw.Draw(c)
    d.line((0, GY, W, GY), fill=(200, 90, 60, 255), width=2)
    px0 = 120; ex = px0 + 6.5 * U if key != '04' else px0 + 5 * U
    x = px0 + s.get('move', 0) * U * min(max(t, 0), s['end'])
    hit_t = None
    if s.get('fire'):
        dia, speed, (sx, sy) = s['shot']
        for f in s['fire']:
            if t < f: continue
            q = t - f; bx = x if not s.get('move') else px0 + s['move'] * U * f
            cx = bx + sx * U + speed * U * q; cy = GY - sy * U
            if key == '04': cx = min(cx, bx + sx * U + 6 * U)
            front = ex - .5 * U
            if key in ('01', '02', '00', '03') and cx >= front:
                hit_t = f + (front - bx - sx * U) / (speed * U)
                if q - (hit_t - f) < .08: star(d, front, cy, dia / 2 * U, q * 30)
                continue
            star(d, cx, cy, dia / 2 * U, q * (30 if key != '04' else 6))
    shake = 2 if hit_t is not None and 0 <= t - hit_t < .08 and int(t * 50) % 2 == 0 else 0
    c.alpha_composite(MOB, (round(ex - 128 + shake), GY - 240))
    if key == '03' and hit_t is not None and t >= hit_t:          # 적중 → 적 뒤(몸 간격 0.25)로 순간이동, 왼쪽 보며 도착 동작
        q = t - hit_t; im = cell('03b', at(SK['03b']['tl'], q)).transpose(Image.FLIP_LEFT_RIGHT)
        bx = ex + .5 * U + .25 * U + .47 * U
        c.alpha_composite(im, (round(bx - (im.width - PIV[0] * K)), round(GY - PIV[1] * K)))
        label = SK['03b']['name']
    else:
        span = s.get('tail', s['end'])
        k = at(s['tl'], max(0, min(t, span))) if t < span else 0
        im = cell(key, k)
        c.alpha_composite(im, (round(x - PIV[0] * K), round(GY - PIV[1] * K))); label = f"{s['name']}  #{k}"
    d.text((8, 6), label, font=F, fill=(30, 30, 30))
    total = s.get('tail', s['end']) + (.5 if key == '03' else .3)
    d.rectangle((0, H - 5, int(W * min(1, max(0, t) / total)), H), fill=(224, 164, 58, 255))
    return c.convert('RGB')
ORDER = ['00', '01', '02', '03', '04']
def frames(step):
    out = []
    for key in ORDER:
        s = SK[key]; total = s.get('tail', s['end']) + (.5 if key == '03' else .3)
        if key == '00': total = 1.2
        t = -.1
        while t < total: out.append(scene(key, t)); t += step
    return out
if __name__ == '__main__':
    for step, name in ((.02, '수리검-고른프레임-실제속도.gif'), (.008, '수리검-고른프레임-느리게.gif')):
        fr = frames(step); fr[0].save(HERE / '../4_미리보기' / name, save_all=True, append_images=fr[1:], duration=20, loop=0, optimize=True); print(name, len(fr))
    sel = {k: {'sheet': str(SHEETS[k].relative_to(HERE.parent)).replace('\\', '/') if False else SHEETS[k].name, 'grid': [6, 6], 'cell': [640, 360], 'pivot_cell': list(PIV),
               'timeline': v['tl'], 'fire': v.get('fire'), 'end': v['end'], 'tail': v.get('tail'), 'frames_used': sorted({f for _, f in v['tl']})} for k, v in SK.items()}
    json.dump(sel, open(HERE / 'shuriken-selection.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    # 정지 컷: 동작마다 고른 칸을 차례로
    rows = []
    for key in ['00', '01', '02', '03', '03b', '04']:
        fs = [f for _, f in SK[key]['tl']]
        row = Image.new("RGB", (len(fs) * 196, 200), (255, 255, 255)); dd = ImageDraw.Draw(row)
        for j, f in enumerate(fs):
            im = cell(key, f).crop((40, 40, 300, 216)).resize((196, 133)); row.paste(im, (j * 196, 30), im); dd.text((j * 196 + 4, 8), f"#{f}", font=F, fill=(0, 0, 0))
        dd.text((4, 176), SK[key]["name"], font=F, fill=(200, 60, 40)); rows.append(row)
    Wm = max(r.width for r in rows); st = Image.new('RGB', (Wm, sum(r.height + 4 for r in rows)), (150, 150, 150)); y = 0
    for r in rows: st.paste(r, (0, y)); y += r.height + 4
    st.save(HERE / '../4_미리보기/수리검-고른프레임-정지컷.png'); print(st.size)
