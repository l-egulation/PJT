# 발밑 맞춤(2026-11-08). 사용자 「장침 발 위치 맞춰」: 에테르 칸마다 발바닥 높이가 달랐다(장침 9px·흘려 11px 뜸, 모아·연침 3~4px 묻힘).
# 6색 정리본에서 칸마다 가장 아래 불투명 줄을 재어, 0번 칸(입력 대기 그림)의 바닥줄(640칸 y=326, 1280칸 y=652)에 오도록 위아래로만 옮긴다.
# 장침의 일부러 띄우는 장(lift)은 이 맞춤 뒤에 위로 올린다. 결과는 ground-shift.json {sheet: {frame: dy}} (dy>0 = 아래로).
import io, json, zipfile
from pathlib import Path
import numpy as np
from PIL import Image
HERE = Path(__file__).resolve().parent; B = HERE.parent
DIRS = {'평타': 'basic', '모아찌르기': 'charge', '장침': 'needle', '흘려찌르기': 'slip', '섬광연침': 'flurry'}
GROUND = {'basic': 652}   # 0번 칸 가장 아래 불투명 줄(1280칸 652, 640칸 326)
def zname(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        try: n = n.encode('cp437').decode('utf-8')
        except Exception: pass
    return n
out = {}
with zipfile.ZipFile(B / '3_보정/레이피어_6색정리_72프레임.zip') as z:
    for i in z.infolist():
        n = zname(i)
        if not n.endswith('.png'): continue
        sheet = DIRS[n.split('/')[0]]; f = int(n.split('/')[-1][:-4].split('-')[-1])
        a = np.array(Image.open(io.BytesIO(z.read(i))))[..., 3] > 0
        bottom = int(np.nonzero(a.any(1))[0].max())
        out.setdefault(sheet, {})[str(f)] = GROUND.get(sheet, 326) - bottom
(HERE / 'ground-shift.json').write_text(json.dumps(out, indent=1), encoding='utf-8')
for k, v in out.items(): print(k, v)
