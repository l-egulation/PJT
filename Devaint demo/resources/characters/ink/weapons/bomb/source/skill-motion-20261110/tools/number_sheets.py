# 폭탄 v2 에테르 결과(6x6, 칸 640x360)에 번호를 붙인 훑어보기 그림. 칸 발밑 (220,327) = 무대 (66,98) × 3.333.
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent
SRC = HERE / '../2_에테르결과/aether-history/v2-1794269025197'
SHEETS = {'01-triple-kick': '51f8b09c-1c14-42f1-8933-8fc30f096734.png', '02-big-bomb-heave': 'e362087b-9d86-4101-99b2-6556d8bf92fc.png',
          '03-missile-launch': '8a7b18a9-8765-40a9-9083-63deb87a0d4e.png', '03b-missile-emerge': '1b58dc74-5a2e-4ab6-8f65-04c25710bae4.png',
          '04-timed-bomb-fiddle': 'cd18a26c-3d9e-4992-b32f-04cfe2dbb352.png'}
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
