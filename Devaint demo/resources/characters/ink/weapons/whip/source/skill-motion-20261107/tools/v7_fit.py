# 다른 AI 가 배경을 지운 v7 판(640x360, 원래 칸을 1.92배 키운 것)을 게임 적용본으로 만든다(2026-11-07).
#   배경 제거·가장자리 알파는 손대지 않는다. 하는 일은 셋뿐이다.
#   1) 색: 보이는 화소의 RGB 를 확정 6색 중 가장 가까운 색으로(알파는 그대로).
#   2) 크기·기준점: asis/meta.json(원래 칸 기준, 얼굴 크기로 잰 PPU·발밑 기준점)을 1.92배로 옮긴다. 그림은 늘이거나 줄이지 않는다.
#   3) 판정 맞춤(asis_fit 과 같은 규칙): 휘감기는 판정 앞끝(4.66칸) 너머 채찍 먹을 지우고,
#      매듭 접근은 곧게 뻗은 장에서 끝 매듭을 판정 앞끝(5.66칸)까지 옮기고 빈 곳은 바로 앞 줄 단면(알파 포함)으로 잇는다.
# 결과: v7_fit/<판>/NN.png (NN = 원래 시트 칸 번호), v7_fit/meta.json
import json, shutil
from pathlib import Path
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
SRC = HERE / 'bgremoved-v7' / '채찍_스킬_640x360'
OUT = HERE / 'v7_fit'
NAME = {'1': '1_휘감기', '2': '2_끌려가기', '3': '3_매듭접근', '4': '4_가시그물', '5': '5_매듭조이기'}
PAL = np.array([[0xFD, 0xEE, 0xDA], [0x1B, 0x16, 0x16], [0x02, 0x56, 0x73], [0xD2, 0xB3, 0x96], [0x02, 0x64, 0x8C], [0xEB, 0xA5, 0x16]])
INK = PAL[1]
SX, SY = 640 / (2000 / 6), 360 / (1125 / 6)   # 원래 칸 → v7 판
REACH = {'1': 4.65625, '3': 5.65625}
KNOT = 16 * SX                                 # 끝 매듭으로 볼 폭(원래 칸 16화소)

def snap(a):
    a = a.copy(); op = a[..., 3] > 0
    rgb = a[..., :3].astype(int)
    d = ((rgb[..., None, :] - PAL[None, None]) ** 2).sum(-1)
    a[..., :3][op] = PAL[d.argmin(-1)][op]
    return a

def ink_of(a):
    return (a[..., 3] > 0) & np.all(a[..., :3] == INK, -1)

def crop_tail(a, x_end):
    a = a.copy(); ink = ink_of(a); ink[:, :x_end] = False; a[ink] = 0; return a

def extend_knot(a, x_target, width):
    ink = ink_of(a); xs = np.where(ink.any(0))[0]; xr = int(xs.max()); d = x_target - (xr + 1)
    out = np.zeros((a.shape[0], width, 4), np.uint8); out[:, :a.shape[1]] = a
    if d <= 0:
        return out
    k0 = int(round(xr + 1 - KNOT))
    knot = np.zeros_like(ink); knot[:, k0:] = ink[:, k0:]
    ky, kx = knot.nonzero(); vals = out[ky, kx].copy(); out[ky, kx] = 0; out[ky, kx + d] = vals
    # 줄 단면: 매듭에서 떨어진 곧은 구간(k0-80..k0-20) 가운데 먹 높이가 가장 흔한 열을 골라 그 열의 RGBA 를 그대로 복사
    cand = []
    for x in range(max(0, k0 - 80), k0 - 20):
        rows = np.where(ink[:, x])[0]
        if 0 < len(rows) <= 8:
            cand.append((tuple(rows), x))
    if cand:
        keys = [c[0] for c in cand]; best = max(set(keys), key=keys.count); col = [x for r, x in cand if r == best][0]
    else:
        col = k0 - 24
    rows = np.where(ink[:, col])[0]; colvals = a[rows, col].copy()
    lo, hi = k0 - 12, k0 + d
    band = out[:, lo:hi]; band[np.all(band[..., :3] == INK, -1) & (band[..., 3] > 0)] = 0
    for x in range(lo, hi):
        out[rows, x] = colvals
    return out

if __name__ == '__main__':
    meta = json.loads((HERE / 'asis' / 'meta.json').read_text(encoding='utf-8'))
    if OUT.exists():
        shutil.rmtree(OUT)
    newmeta = {}
    for sheet, folder in NAME.items():
        m = meta[sheet]
        # PPU: 얼굴 크기로 잰 값은 대기 키가 확정 F006(2.0칸)보다 5~10% 작게 나왔다. 대기로 돌아온 35번 칸의 키(두건 끝~발밑)가 2.0칸이 되게 잡는다.
        a35 = np.array(Image.open(HERE / 'asis' / sheet / '35.png'))[..., 3] > 0
        cpx, cpy = m['pivot_px_topleft']; s_old = m['ppu'] / 32
        top = np.where(a35[:, int(cpx) - 25:int(cpx) + 35].any(1))[0].min()
        ppu_cell = (cpy - top) / 2.0; s_new = ppu_cell / 32
        cpx = cpx + (66 - 76.5) * (s_new - s_old)      # 기준점 x = 얼굴 가운데 + (F006 기준점 - F006 얼굴 가운데) x 배율
        px = cpx * SX; ppu = ppu_cell * SX
        # 기준점 y: F006 처럼 검은 밑창 바닥(발 쪽 맨 아래 불투명 줄). 예전 값은 노란 장화 아래끝이라 밑창이 0.1칸 바닥 아래로 내려갔다.
        #   판의 고른 장들에서 발 둘레 맨 아래 줄의 최빈값을 쓴다(공중에 뜬 끌려가기 장은 자연히 빠진다).
        bots = []
        for f in sorted((SRC / folder).glob('*.png')):
            al = np.array(Image.open(f))[..., 3] > 128
            band = al[:, int(px - 0.9 * ppu):int(px + 0.7 * ppu)]; ys = np.where(band.sum(1) >= 3)[0]
            if len(ys):
                bots.append(int(ys.max()) + 1)
        py = float(max(set(bots), key=bots.count))
        # 키(두건 끝~밑창 바닥)가 F006 과 같은 2.0칸이 되게 PPU 를 다시 잡고, 기준점 x 도 그 배율로 옮긴다.
        ppu_v7 = (py - top * SY) / 2.0; s_fix = ppu_v7 / SX / 32
        cpx = cpx + (66 - 76.5) * (s_fix - s_new); px = cpx * SX; ppu = ppu_v7
        W, H = 640, 360
        if sheet == '3':
            W = max(W, int(np.ceil(px + REACH['3'] * ppu)) + 4)
        d = OUT / sheet; d.mkdir(parents=True)
        for f in sorted((SRC / folder).glob('*.png')):
            a = snap(np.array(Image.open(f).convert('RGBA')))
            if sheet == '1':
                a = crop_tail(a, int(round(px + REACH['1'] * ppu)))
            if sheet == '3':
                xs = np.where(ink_of(a).any(0))[0]
                reach = (xs.max() + 1 - px) / ppu if len(xs) else 0
                a = extend_knot(a, int(round(px + REACH['3'] * ppu)), W) if reach >= 4.5 else np.pad(a, ((0, 0), (0, W - a.shape[1]), (0, 0)))
            Image.fromarray(a).save(d / f.name)
        newmeta[sheet] = {'size': [W, H], 'ppu': round(ppu, 2), 'pivot_px_topleft': [round(px, 1), round(py, 1)],
                          'pivot_unity': [round(px / W, 5), round(1 - py / H, 5)], 'from': f'asis/meta.json x{SX:.4f}'}
    json.dump(newmeta, open(OUT / 'meta.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(json.dumps(newmeta, ensure_ascii=False))
