# 단검 다섯 동작 전부 미리보기(2026-11-07): 확정 몸 그림(단검_6색정리_75프레임.zip) + 그림자 난무 e 초승달 폭풍.
# 평타 4연타 → 숨죽이기 → 먹안개 → 삼연 도약 → 그림자 난무 를 차례로. 1유닛 = 64px = 무대 2배(칸 640x360 그대로).
import io, json, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent
ZIP = HERE / '../3_보정·추린프레임/단검_6색정리_75프레임.zip'
ART = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/whip-skill-motion-20261107/unity/Assets/Deviant/Stage1Monsters/Art')
U = 64; SW = 900; H = 300; GY = 262; PX = 150
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 18); FD = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 20)

def zname(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        try: n = n.encode('cp437').decode('cp949')
        except Exception: pass
    return n
FR = {}
with zipfile.ZipFile(ZIP) as z:
    for i in z.infolist():
        n = zname(i)
        if not n.endswith('.png'): continue
        d, f = n.split('/')[-2], n.split('/')[-1][:-4]
        FR[(d[:2], f)] = Image.open(io.BytesIO(z.read(i))).convert('RGBA')
def big(im): return im.resize((im.width * 2, im.height * 2), Image.NEAREST)
d0 = HERE / 'flurry-fx/e'; m = json.load(open(d0 / 'meta.json'))
FXE = ([big(Image.open(q).convert('RGBA')) for q in sorted(d0.glob('*.png'))], (m['origin_px'][0] * 2, m['origin_px'][1] * 2), m['fps'])
MOB = big(Image.open(ART / '01_thorn-root/idle/00.png').convert('RGBA')); MOB_CY = 30 / 32

# 동작마다 (이름, 길이, [(시각, 칸 키)], 타격 시각들, 기준점, 이동(시작, 길이, 유닛), 적 거리(유닛), e 이펙트)
basic = json.load(open(HERE / 'dagger-basic-selection-v2.json', encoding='utf-8'))
tl, hits, t0 = [], [], 0.0
for c in basic['clips']:
    tl += [(t0 + k['t'], ('01', f"{k['part']}부_{k['cell']:02d}")) for k in c['keys']]; hits.append(t0 + c['impact']); t0 += c['length']
tl += [(t0 + k['t'], ('01', f"{k['part']}부_{k['cell']:02d}")) for k in basic['recover']]
SK = [('평타 4연타', t0 + .2 + .25, tl, hits, (220, 304), None, 1.6, False)]
sk = json.load(open(HERE / 'dagger-skills-selection.json', encoding='utf-8'))
for key, nm, dn in (('hold-breath', '숨죽이기', '02'), ('ink-smoke', '먹안개', '03'), ('triple-leap', '삼연 도약', '04')):
    s = sk[key]; SK.append((nm, s['end'] + .25, [(a, (dn, f'{b:02d}')) for a, b in s['timeline']], [s['impact']], tuple(s['pivot_cell']), s['move'], 1.6 if key != 'triple-leap' else 3.2, False))
fl = json.load(open(HERE / 'flurry-v4-selection-B.json', encoding='utf-8'))
SK.append(('그림자 난무 + 초승달 폭풍', fl['end'] + .45, [(a, ('05', f'{b:02d}')) for a, b in fl['timeline']], fl['hits'], (221, 304), None, 1.75, True))
missing = [k for s in SK for _, k in s[2] if k not in FR]; assert not missing, missing

def at(tl, t):
    k = tl[0][1]
    for a, b in tl:
        if t >= a: k = b
    return k
def scene(s, t, dark):
    nm, end, tl, hits, piv, move, ex_u, fx = s
    bg = (38, 42, 50, 255) if dark else (255, 255, 255, 255); fg = (230, 230, 230, 255) if dark else (34, 34, 34, 255)
    c = Image.new('RGBA', (SW, H), bg); d = ImageDraw.Draw(c)
    d.line((10, GY, SW - 10, GY), fill=(200, 90, 60, 255), width=2)
    x = PX
    if move:
        a, ln, dist = move; x += dist * U * min(1, max(0, (t - a) / ln))
    ex = PX + ex_u * U; cy = GY - MOB_CY * U; shake = 0
    for h in hits:
        if 0 <= t - h < .06: shake = 2 if int((t - h) * 50) % 2 == 0 else -2
    c.alpha_composite(MOB, (round(ex - 128 + shake), GY - 240))
    k = at(tl, max(0, t)); c.alpha_composite(FR[k], (round(x - piv[0]), GY - piv[1]))
    if fx:
        fr, (ox, oy), fps = FXE; i = int(t * fps + 1e-6)
        if 0 <= i < len(fr): c.alpha_composite(fr[i], (round(ex - ox), round(cy - oy)))
    for n, h in enumerate(hits):
        a = t - h
        if 0 <= a < .45: d.text((ex - 10 + (n % 2) * 16, cy - 70 - 46 * a - n * 3), '8', font=FD, fill=(224, 72, 47, 255), stroke_width=2, stroke_fill=bg[:3] + (255,))
    d.text((8, 6), f'{nm}   {k[1]}', font=F, fill=fg)
    d.rectangle((0, H - 5, int(SW * min(1, max(0, t) / end)), H), fill=(224, 164, 58, 255))
    return c.convert('RGB')
def frames(step):
    out = []
    for s in SK:
        t = -.1
        while t < s[1]:
            im = Image.new('RGB', (SW, H * 2 + 6), (120, 120, 120)); im.paste(scene(s, t, False), (0, 0)); im.paste(scene(s, t, True), (0, H + 6)); out.append(im); t += step
    return out
if __name__ == '__main__':
    for step, name in ((.02, '단검-전체-실제속도.gif'), (.008, '단검-전체-느리게.gif')):
        fr = frames(step); fr[0].save(HERE / '../4_미리보기' / name, save_all=True, append_images=fr[1:], duration=20, loop=0, optimize=True); print(name, len(fr))
