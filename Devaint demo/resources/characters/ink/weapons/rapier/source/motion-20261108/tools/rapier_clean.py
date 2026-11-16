# 레이피어 평타 C·특수 4종 6색 정리(2026-11-08). 사용자가 가져온 배경 제거본(레이피어_배경제거_외곽정리_72장.zip, 칸 크기·자리 그대로)에
# 단검·수리검과 같은 6색 정리(dagger_clean.clean)를 입히되 두 가지를 바꾼다.
#   - 평타 칸은 1280x720 이라 특수(640x360)보다 2배 크다 → 알갱이·구멍·색 조각 기준 넓이를 4배로.
#   - 레이피어 칼날은 1~2화소 두께로 가늘어진다 → 외곽 3x3 다수결이 먹색(칼날·손) 화소는 지우지 않게 한다(칼끝이 깎이지 않게).
# 결과: 3_보정/레이피어_6색정리_72프레임.zip (같은 폴더·이름) + 4_미리보기/레이피어-6색정리-전후.png
import io, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
B = Path(__file__).resolve().parent.parent
SRC = B / '레이피어_배경제거_외곽정리_72장.zip'
OUT = B / '3_보정/레이피어_6색정리_72프레임.zip'
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]], np.int32)
BLACK, YELLOW = 1, 5
def zname(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        try: n = n.encode('cp437').decode('utf-8')
        except Exception: pass
    return n
def clean(a, s=1):
    ISLAND, HOLE, SPECK = 24 * s * s, 24 * s * s, 6 * s * s
    rgb = a[..., :3].astype(np.int32); m = a[..., 3] >= 128
    lab, n = ndi.label(m, np.ones((3, 3)))
    if n: sz = ndi.sum(m, lab, range(1, n + 1)); m &= np.isin(lab, 1 + np.flatnonzero(sz >= ISLAND))
    hl, n = ndi.label(~m)
    if n: sz = ndi.sum(~m, hl, range(1, n + 1)); m |= np.isin(hl, 1 + np.flatnonzero(sz < HOLE)) & (hl != hl[0, 0])
    k = ((rgb[..., None, :] - PAL) ** 2).sum(-1).argmin(-1)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k[((r - b >= 100) | (g - b >= 55)) & (r > 170) & (g > 110)] = YELLOW
    sure = a[..., 3] >= 250
    _, idx = ndi.distance_transform_edt(~sure, return_indices=True)
    k = np.where(m & ~sure, k[tuple(idx)], k)
    for _ in range(2):
        changed = 0
        for c in range(len(PAL)):
            cl, n = ndi.label((k == c) & m)
            if not n: continue
            sz = ndi.sum(np.ones_like(cl), cl, range(1, n + 1))
            for j in np.flatnonzero(sz < SPECK) + 1:
                piece = cl == j
                if c == BLACK and piece.sum() >= 2: continue          # 가는 칼날 끊긴 조각도 칼이다
                ring = ndi.binary_dilation(piece, np.ones((3, 3))) & ~piece & m
                if not ring.any(): continue
                k[piece] = np.bincount(k[ring], minlength=len(PAL)).argmax(); changed += 1
        if not changed: break
    cnt = ndi.uniform_filter(m.astype(float), 3) * 9
    m2 = np.where(cnt >= 6, True, np.where((cnt <= 3) & (k != BLACK), False, m))   # 먹색 가는 선은 깎지 않는다
    _, idx = ndi.distance_transform_edt(~m, return_indices=True); k = np.where(m2 & ~m, k[tuple(idx)], k); m = m2
    votes = np.stack([ndi.uniform_filter(((k == c) & m).astype(float), 3) for c in range(len(PAL))])
    best = votes.argmax(0); keep = votes.max(0) * 9 >= 5
    protect = np.isin(k, [YELLOW, BLACK])                             # 노랑 작은 점·먹색 칼날은 지킨다
    k = np.where(m & keep & ~protect & (best != YELLOW), best, k)
    out = np.zeros_like(a); out[..., :3] = PAL[k]; out[..., 3] = m * 255
    return out
if __name__ == '__main__':
    rows = []
    with zipfile.ZipFile(SRC) as zi, zipfile.ZipFile(OUT, 'w', zipfile.ZIP_DEFLATED) as zo:
        for i in zi.infolist():
            n = zname(i)
            if not n.lower().endswith('.png'): continue
            a = np.array(Image.open(io.BytesIO(zi.read(i))).convert('RGBA'))
            b = clean(a, 2 if a.shape[1] >= 1280 else 1)
            buf = io.BytesIO(); Image.fromarray(b).save(buf, 'PNG', optimize=True); zo.writestr(n, buf.getvalue())
            cols = len({tuple(c) for c in b[b[..., 3] > 0][:, :3]})
            rows.append((n, a, b, cols))
    print(len(rows), 'colors max', max(r[3] for r in rows), OUT)
    # 전후 비교: 폴더마다 두 장(타격 근처), 원본 위·정리본 아래, 칼 부분을 확대
    F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 16)
    pick = ['평타/basic-011.png', '평타/basic-048.png', '모아찌르기/charge-021.png', '장침/needle-007.png', '흘려찌르기/slip-007.png', '섬광연침/flurry-018.png']
    W = 420; S = Image.new('RGB', (W * len(pick), 2 * 250 + 20), (60, 64, 72)); d = ImageDraw.Draw(S)
    for k, n in enumerate(pick):
        r = next((x for x in rows if x[0] == n), None)
        if not r: continue
        for j, im in enumerate((r[1], r[2])):
            pil = Image.fromarray(im); bb = pil.getbbox(); pil = pil.crop(bb)
            pil.thumbnail((W - 10, 240), Image.NEAREST); bg = Image.new('RGBA', pil.size, (60, 64, 72, 255)); bg.alpha_composite(pil)
            S.paste(bg.convert('RGB'), (k * W + 5, 20 + j * 250))
        d.text((k * W + 5, 2), n, font=F, fill=(230, 230, 230))
    S.save(B / '4_미리보기/레이피어-6색정리-전후.png')
