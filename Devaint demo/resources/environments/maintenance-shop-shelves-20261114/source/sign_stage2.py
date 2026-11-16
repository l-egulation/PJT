"""2챕터 팔레트 팻말: 1챕터 팻말(sign-stage1.png)과 같은 모양, 색만 2챕터(옛 2챕터 팻말의 나무색·물감색, 먹 외곽선, 고리판은 쇠).

    python sign_stage2.py <워크트리 루트>
"""
import sys
from pathlib import Path
from PIL import Image
HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1])
SRC = ROOT / 'unity/Assets/Deviant/Game/Resources/MaintenanceShops/sign-stage1.png'
INK = (6, 9, 11, 255)
WOOD = {(254, 247, 238): (184, 151, 106), (255, 252, 247): (207, 176, 132), (204, 199, 190): (165, 132, 92), (160, 157, 150): (138, 106, 72)}
STEEL = {(254, 247, 238): (111, 129, 124), (255, 252, 247): (143, 168, 154), (204, 199, 190): (74, 92, 90), (160, 157, 150): (74, 92, 90)}
PAINT = {(200, 53, 44): (192, 53, 44), (224, 103, 92): (230, 120, 100), (61, 94, 168): (44, 90, 168), (110, 140, 208): (96, 140, 210),
         (230, 169, 46): (224, 165, 42), (243, 204, 111): (246, 206, 110), (63, 138, 79): (52, 128, 84), (111, 174, 121): (104, 172, 124)}


def make():
    im = Image.open(SRC).convert('RGBA'); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if not a: continue
            k = (r, g, b)
            if k == (63, 62, 60): px[x, y] = INK
            elif k in PAINT: px[x, y] = PAINT[k] + (255,)
            elif k in WOOD: px[x, y] = ((STEEL if y < 8 else WOOD)[k]) + (255,)
    return im


if __name__ == '__main__':
    make().save(HERE / 'sign-stage2-palette.png')
