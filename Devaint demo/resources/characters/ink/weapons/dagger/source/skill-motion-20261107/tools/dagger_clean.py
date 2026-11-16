# 단검 75장 정리(2026-11-07): 다른 AI 배경 제거본(외곽선명도보정 v3)을 채찍과 같은 방식으로 정리한다.
# 해상도·자리는 그대로. 알파는 반쯤 투명한 테두리를 없애 딱 잘린 외곽으로, 색은 캐릭터 6색으로,
# 그 뒤 홀로 떨어진 잔점(작은 색 조각·떠 있는 알갱이·작은 구멍)을 둘레 색으로 메운다.
import sys, zipfile, io, json
import numpy as np
from pathlib import Path
from PIL import Image
from scipy import ndimage as ndi

HERE = Path(__file__).resolve().parent
SRC = HERE / '../단검_외곽선명도보정_v3_75프레임.zip'
OUT = HERE / '../3_보정·추린프레임/단검_6색정리_75프레임.zip'
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]], np.int32)
A_CUT, ISLAND, HOLE, SPECK = 128, 24, 24, 6
# 자리 바로잡기(11-07 사용자 「먹안개가 발이 떠」): 먹안개는 #00(대기)만 제자리이고 #02~#31 이 통째로 위 9·앞 12화소 어긋나 있다
# (크기는 같다: 두건 끝~발 129 vs 132). 대기 발바닥 줄 303·발 x 208~253 에 맞춰 평행이동만 한다.
SHIFT = {('03_', f): (-12, 9) for f in ('02', '04', '06', '07', '08', '10', '12', '20', '23', '25', '27', '29', '31')}
def shift(a, dx, dy):
    o = np.zeros_like(a); H, W = a.shape[:2]
    o[max(0, dy):H + min(0, dy), max(0, dx):W + min(0, dx)] = a[max(0, -dy):H - max(0, dy), max(0, -dx):W - max(0, dx)]
    return o

def name(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        for enc in ('cp437',):
            try: n = n.encode(enc).decode('cp949')
            except Exception: pass
    return n

def clean(a):
    rgb = a[..., :3].astype(np.int32); m = a[..., 3] >= A_CUT
    lab, n = ndi.label(m, np.ones((3, 3)))                       # 떠 있는 알갱이 지우기
    if n: sz = ndi.sum(m, lab, range(1, n + 1)); m &= np.isin(lab, 1 + np.flatnonzero(sz >= ISLAND))
    hl, n = ndi.label(~m)                                         # 몸 안 작은 구멍 메우기
    if n: sz = ndi.sum(~m, hl, range(1, n + 1)); m |= np.isin(hl, 1 + np.flatnonzero(sz < HOLE)) & (hl != hl[0, 0])
    k = ((rgb[..., None, :] - PAL) ** 2).sum(-1).argmin(-1)       # 6색
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k[((r - b >= 100) | (g - b >= 55)) & (r > 170) & (g > 110)] = 5   # 노랑(손잡이 테·버클·장화)이 배경에 섞여 연해진 것도 노랑으로. 베이지·크림은 g-b 30 아래
    # 구멍을 메운 자리·반투명이던 테두리는 가까운 확실한 안쪽 색을 쓴다
    sure = a[..., 3] >= 250
    _, idx = ndi.distance_transform_edt(~sure, return_indices=True)
    k = np.where(m & ~sure, k[tuple(idx)], k)
    for _ in range(2):                                            # 작은 색 조각을 둘레 다수 색으로
        changed = 0
        for c in range(len(PAL)):
            cl, n = ndi.label((k == c) & m)
            if not n: continue
            sz = ndi.sum(np.ones_like(cl), cl, range(1, n + 1))
            for j in np.flatnonzero(sz < SPECK) + 1:
                piece = cl == j; ring = ndi.binary_dilation(piece, np.ones((3, 3))) & ~piece & m
                if not ring.any(): continue
                k[piece] = np.bincount(k[ring], minlength=len(PAL)).argmax(); changed += 1
        if not changed: break
    # 외곽 계단 다듬기: 알파는 3x3 다수결(1화소 돌기·홈만 바뀐다), 색 경계도 3x3 다수결 한 번
    cnt = ndi.uniform_filter(m.astype(float), 3) * 9
    m2 = np.where(cnt >= 6, True, np.where(cnt <= 3, False, m))
    _, idx = ndi.distance_transform_edt(~m, return_indices=True); k = np.where(m2 & ~m, k[tuple(idx)], k); m = m2
    votes = np.stack([ndi.uniform_filter(((k == c) & m).astype(float), 3) for c in range(len(PAL))])
    best = votes.argmax(0); keep = votes.max(0) * 9 >= 5
    thin = np.isin(k, [5])                                          # 노랑 작은 점(눈·버클)은 지킨다
    k = np.where(m & keep & ~thin & (best != 5), best, k)
    out = np.zeros_like(a); out[..., :3] = PAL[k]; out[..., 3] = m * 255
    return out

def main():
    zin = zipfile.ZipFile(SRC); rows = []
    with zipfile.ZipFile(OUT, 'w', zipfile.ZIP_DEFLATED) as zo:
        for i in zin.infolist():
            n = name(i)
            if n.endswith('/'): continue
            data = zin.read(i)
            if n.lower().endswith('.png'):
                a = np.array(Image.open(io.BytesIO(data)).convert('RGBA'))
                b = clean(a)
                key = (n.split('/')[-2][:3], n.split('/')[-1][:-4])
                if key in SHIFT: b = shift(b, *SHIFT[key])
                buf = io.BytesIO(); Image.fromarray(b).save(buf, 'PNG', optimize=True)
                zo.writestr(n, buf.getvalue())
                rows.append((n, int(((a[..., 3] > 0) & (a[..., 3] < 255)).sum()), len(np.unique(a[a[..., 3] > 0][:, :3], axis=0)), int(b[..., 3].sum() // 255 - (a[..., 3] >= A_CUT).sum())))
            elif not n.lower().endswith('.txt'): zo.writestr(n, data)
        zo.writestr('설명.txt', '단검 75장 6색 정리(2026-11-07)\n'
            '입력: 단검_외곽선명도보정_v3_75프레임.zip(다른 AI 배경 제거·외곽 보정본)\n'
            '- 크기·자리 그대로(640x360, 발밑 같은 자리). 다시 그리거나 키우지 않음\n'
            '- 알파 128 기준으로 딱 자른 외곽(반투명 테두리 없음)\n'
            '- 색은 캐릭터 6색 FDEEDA 1B1616 025673 D2B396 02648C EBA516\n'
            '- 먹안개 #02~#31 은 위 9·앞 12화소 어긋나 있어 대기 발 자리로 옮김(평행이동만)\n'
            f'- {ISLAND}화소 미만 떠 있는 조각 지움, {HOLE}화소 미만 구멍 메움, {SPECK}화소 미만 색 조각은 둘레 색으로\n')
    print(len(rows), '장 ->', OUT.name)
    print('반투명 화소 평균', np.mean([r[1] for r in rows]).round(), '색 개수 평균', np.mean([r[2] for r in rows]).round())
main()
