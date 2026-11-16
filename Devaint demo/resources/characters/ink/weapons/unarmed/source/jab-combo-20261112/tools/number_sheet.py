# 맨손 잽 동작 에테르 결과(6x6, 칸 640x360)에 번호를 붙이고 칸별 PNG 로 뽑는다. 2026-11-12
# 사용자: 「이거 우리 기본 동작 이걸로 변경하려고. 프레임 좀 뽑아서 보여줘」
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; SRC = ROOT / '2_에테르결과' / '31dccac8-edf1-4c5f-a153-bcee66cc00db.png'; OUT = ROOT / '3_프레임'
im = Image.open(SRC).convert('RGBA'); w = Image.new('RGBA', im.size, 'white'); w.alpha_composite(im); sheet = w.convert('RGB'); CW, CH = im.width // 6, im.height // 6
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 22)
VIEW = (90, 100, 420, 340); vw, vh = VIEW[2] - VIEW[0], VIEW[3] - VIEW[1]
def cell(i): return sheet.crop(((i % 6) * CW, (i // 6) * CH, (i % 6) * CW + CW, (i // 6) * CH + CH))
(OUT / '칸').mkdir(exist_ok=True)
num = Image.new('RGB', (6 * vw, 6 * (vh + 30)), 'white'); d = ImageDraw.Draw(num)
for i in range(36):
    im.crop(((i % 6) * CW, (i // 6) * CH, (i % 6) * CW + CW, (i // 6) * CH + CH)).save(OUT / '칸' / f'{i:02d}.png')
    x, y = (i % 6) * vw, (i // 6) * (vh + 30); num.paste(cell(i).crop(VIEW), (x, y + 30)); d.text((x + 8, y + 2), f'#{i}', font=F, fill=(200, 0, 0)); d.line((x, y + 30 + 327 - VIEW[1], x + vw, y + 30 + 327 - VIEW[1]), fill=(200, 220, 255))
num.save(OUT / '번호시트.png'); print(im.size, CW, CH)
