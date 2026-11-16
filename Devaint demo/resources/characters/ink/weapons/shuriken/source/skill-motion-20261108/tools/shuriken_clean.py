# 수리검 고른 프레임 배경 제거 + 6색 정리(2026-11-08). 에테르 원본 칸(640x360, 흰 배경)을 shuriken_pick 의 발 맞춤만큼 옮긴 뒤
#  1) 테두리에서 흰색(세 채널 모두 238 이상)을 따라 채워 배경을 지운다(두건 크림은 파랑 채널 218 이라 남는다)
#  2) 몸에 붙지 않은 조각(날아가는 별)을 지운다, 폭발 #11~14 는 휘두름 호(베이지 228~247 / 212~236 / 196~226)도 지운다
#  3) 단검과 같은 정리(dagger_clean.clean): 알파 128 로 딱 자르기, 6색, 작은 조각·구멍·색 조각 정리, 3x3 다수결
# 크기·배율은 그대로(640x360, 발밑 (220,327)). 결과: 3_보정·추린프레임/수리검_6색정리_N프레임.zip
import io, json, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
import shuriken_pick as P

HERE = Path(__file__).resolve().parent
OUT = HERE / '../3_보정·추린프레임'
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]], np.int32)
A_CUT, ISLAND, HOLE, SPECK = 128, 24, 24, 6
NAMES = {'00': '00_평타', '01': '01_큰관통', '02': '02_폭발', '03': '03_적뒤던지기', '03b': '03b_적뒤도착', '04': '04_전진회전'}
ARC = {('02', i) for i in (11, 12, 13, 14)}

def aligned(key, i):
    c = P.raw(key, i).astype(np.int16).copy(); dx, dy = P.shift_of(key, i)
    c = np.roll(np.roll(c, dy, 0), dx, 1)
    if dy > 0: c[:dy] = 255
    elif dy < 0: c[dy:] = 255
    if dx > 0: c[:, :dx] = 255
    elif dx < 0: c[:, dx:] = 255
    return c
def cut(key, i):
    c = aligned(key, i); white = c.min(2) >= 238
    if (key, i) in ARC:
        r, g, b = c[..., 0], c[..., 1], c[..., 2]
        white |= (r >= 228) & (r <= 247) & (g >= 212) & (g <= 236) & (b >= 196) & (b <= 226) & (r - b >= 12) & (r - b <= 36)
    lab, n = ndi.label(white); edge = set(np.unique(np.r_[lab[0], lab[-1], lab[:, 0], lab[:, -1]])) - {0}
    bg = np.isin(lab, list(edge))
    fg = ~bg
    lab2, n2 = ndi.label(fg); sz = ndi.sum(fg, lab2, range(1, n2 + 1)); body = lab2 == (1 + int(np.argmax(sz)))
    near = ndi.binary_dilation(body, iterations=6); keep = np.zeros_like(fg)
    for j in range(1, n2 + 1):
        part = lab2 == j
        if (part & near).any(): keep |= part
    return np.dstack([c.astype(np.uint8), (keep * 255).astype(np.uint8)])

def clean(a):     # dagger_clean.clean 과 같은 규칙
    rgb = a[..., :3].astype(np.int32); m = a[..., 3] >= A_CUT
    lab, n = ndi.label(m, np.ones((3, 3)))
    if n: sz = ndi.sum(m, lab, range(1, n + 1)); m &= np.isin(lab, 1 + np.flatnonzero(sz >= ISLAND))
    hl, n = ndi.label(~m)
    if n: sz = ndi.sum(~m, hl, range(1, n + 1)); m |= np.isin(hl, 1 + np.flatnonzero(sz < HOLE)) & (hl != hl[0, 0])
    k = ((rgb[..., None, :] - PAL) ** 2).sum(-1).argmin(-1)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k[((r - b >= 100) | (g - b >= 55)) & (r > 170) & (g > 110)] = 5
    sure = a[..., 3] >= 250
    _, idx = ndi.distance_transform_edt(~sure, return_indices=True)
    k = np.where(m & ~sure, k[tuple(idx)], k)
    for _ in range(2):
        changed = 0
        for cc in range(len(PAL)):
            cl, n = ndi.label((k == cc) & m)
            if not n: continue
            sz = ndi.sum(np.ones_like(cl), cl, range(1, n + 1))
            for j in np.flatnonzero(sz < SPECK) + 1:
                piece = cl == j; ring = ndi.binary_dilation(piece, np.ones((3, 3))) & ~piece & m
                if not ring.any(): continue
                k[piece] = np.bincount(k[ring], minlength=len(PAL)).argmax(); changed += 1
        if not changed: break
    cnt = ndi.uniform_filter(m.astype(float), 3) * 9
    m2 = np.where(cnt >= 6, True, np.where(cnt <= 3, False, m))
    _, idx = ndi.distance_transform_edt(~m, return_indices=True); k = np.where(m2 & ~m, k[tuple(idx)], k); m = m2
    votes = np.stack([ndi.uniform_filter(((k == cc) & m).astype(float), 3) for cc in range(len(PAL))])
    best = votes.argmax(0); keep = votes.max(0) * 9 >= 5
    k = np.where(m & keep & (k != 5) & (best != 5), best, k)
    out = np.zeros_like(a); out[..., :3] = PAL[k]; out[..., 3] = m * 255
    return out

def frames():
    for key in NAMES:
        for i in sorted({f for _, f in P.SK[key]['tl']}): yield key, i

if __name__ == '__main__':
    done = {}
    for key, i in frames(): done[(key, i)] = clean(cut(key, i))
    n = len(done); zp = OUT / f'수리검_6색정리_{n}프레임.zip'
    with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
        for (key, i), a in done.items():
            buf = io.BytesIO(); Image.fromarray(a).save(buf, 'PNG', optimize=True); z.writestr(f'{NAMES[key]}/{i:02d}.png', buf.getvalue())
        z.writestr('설명.txt', f'수리검 고른 프레임 {n}장 6색 정리(2026-11-08)\n입력: 에테르 원본 칸 640x360(2_에테르결과/aether-history/shuriken-v4·v5), 발 맞춤 적용(땅 327, 발밑 (220,327)).\n'
                   '배경 제거는 테두리부터 흰색 채우기, 날아가는 별(몸에 안 붙은 조각)·폭발 #11~14 휘두름 호 제거.\n색은 6색 FDEEDA 1B1616 025673 D2B396 02648C EBA516, 알파 128 로 딱 자름.\n시간표는 _작업/shuriken-selection.json.\n')
    print(zp.name, n)
    # 훑어보기: 동작마다 고른 칸 순서대로, 어두운·밝은 바탕
    F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 14); rows = []
    for key in NAMES:
        fs = [f for _, f in P.SK[key]['tl']]
        row = Image.new('RGB', (len(fs) * 132, 2 * 128 + 22), (150, 150, 150)); d = ImageDraw.Draw(row); d.text((4, 2), NAMES[key], font=F, fill=(0, 0, 0))
        for j, f in enumerate(fs):
            im = Image.fromarray(done[(key, f)]).crop((100, 90, 360, 340)).resize((130, 125), Image.NEAREST)
            for k2, bg in enumerate(((38, 42, 50), (240, 234, 220))):
                b = Image.new('RGBA', im.size, bg + (255,)); b.alpha_composite(im); row.paste(b.convert('RGB'), (j * 132, 20 + k2 * 128))
        rows.append(row)
    W = max(r.width for r in rows); st = Image.new('RGB', (W, sum(r.height + 4 for r in rows)), (110, 110, 110)); y = 0
    for r in rows: st.paste(r, (0, y)); y += r.height + 4
    st.save(HERE / '../4_미리보기/수리검-6색정리-훑어보기.png'); print(st.size)
