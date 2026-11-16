# 발사기 v1 에테르 결과(6x6, 칸 640x360)에 번호를 붙인 훑어보기 그림. 칸 발밑 (220,327) = 무대 (66,98) × 3.333.
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent
SRC = HERE / '../2_에테르결과/aether-history'
SHEETS = {'02-laser-beam': 'v3-laser-20261110/32ccdbe9-5d17-4b1a-9445-07e6d7650eb9.png', '01-charge-shot': 'v1-20261110/6549e40e-dd4d-46c6-bbe0-5290acb45e3d.png', '03-shotgun-blast-jump': 'v1-20261110/7f07d476-839e-4828-93b6-7b422d7f9360.png', '04-grenade-kneel-reload': 'v1-20261110/20f96eb6-09aa-488c-95c8-01b3b6e9739b.png'}
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 22)
OUT = HERE / '../4_미리보기'; OUT.mkdir(exist_ok=True)
CROP = (60, 60, 520, 360)   # 칸 안에서 캐릭터 주변
for key, name in SHEETS.items():
    sh = Image.open(SRC / name).convert('RGBA')
    cw, ch = (CROP[2] - CROP[0]) // 2, (CROP[3] - CROP[1]) // 2
    out = Image.new('RGB', (cw * 6, ch * 6), 'white'); d = ImageDraw.Draw(out)
    for i in range(36):
        c = sh.crop(((i % 6) * 640, (i // 6) * 360, (i % 6 + 1) * 640, (i // 6 + 1) * 360)).crop(CROP).resize((cw, ch), Image.LANCZOS)
        bg = Image.new('RGBA', c.size, 'white'); bg.alpha_composite(c)
        x, y = (i % 6) * cw, (i // 6) * ch; out.paste(bg.convert('RGB'), (x, y))
        d.line((x + (220 - CROP[0]) // 2 - 6, y + (327 - CROP[1]) // 2, x + (220 - CROP[0]) // 2 + 6, y + (327 - CROP[1]) // 2), fill=(220, 60, 40), width=2)
        d.rectangle((x, y, x + cw - 1, y + ch - 1), outline=(210, 210, 210)); d.text((x + 4, y + 2), f'#{i}', font=F, fill=(200, 40, 40))
    out.save(OUT / f'번호-{key}.png'); print(key, out.size)
