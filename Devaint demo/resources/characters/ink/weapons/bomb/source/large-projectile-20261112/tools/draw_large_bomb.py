# 대형 폭탄(bomb_range_special) 전용 투사체 그림. 2026-11-12
# 사용자: 「폭탄 투사체 만들어줘. 완전 동글동글하게, 울퉁불퉁하지 않게. 모션 참고해」
#   - 지금 게임은 기본 폭탄 그림(16x19, PPU32)을 그대로 날린다 → 모션 속 큰 폭탄보다 작다. 2배로 키우면 픽셀이 굵어 울퉁불퉁하다.
#   - 모션(CharacterBombSkills/big_heave, PPU 106.6667) 속 폭탄: 반지름 38~41px(지름 약 0.75유닛), 먹색 몸통, 위에 노란 띠, 띠에서 올라가 휘는 심지, 몸통 위쪽 파란 반짝임.
# 그리는 법: 모션과 같은 밀도(PPU 106.6667)로, 8배 크게 그린 뒤 줄여 반으로 가른다(반투명 없음) → 계단이 한 화소씩인 고른 원.
#   색은 게임 팔레트 그대로: 먹 #1B1616, 노랑 #EBA516, 파랑 #025673.
# 그림 가운데 = 몸통 가운데(피벗 0.5,0.5) → 날아가며 돌 때 몸통이 제자리에서 돈다.
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent
RES = Path(r'C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/bomb-skill-motion-20261110/unity/Assets/Deviant/Combat/Resources')
SIZE, C, R, SS = 144, 72, 40, 8
INK, GOLD, BLUE = (27, 22, 22, 255), (235, 165, 22, 255), (2, 86, 115, 255)
def layer(draw):   # 8배 판에 그려 1배로 줄이고 반으로 가른 마스크
    m = Image.new('L', (SIZE * SS, SIZE * SS), 0); draw(ImageDraw.Draw(m), lambda v: v * SS)
    return m.resize((SIZE, SIZE), Image.BOX).point(lambda v: 255 if v >= 128 else 0)
def body(d, s): d.ellipse((s(C - R), s(C - R), s(C + R), s(C + R)), fill=255)
def fuse(d, s):   # 띠 가운데에서 올라가 오른쪽으로 휜다. 굵기 6
    pts = [(C, C - R - 6), (C, C - R - 15), (C + 3, C - R - 21), (C + 9, C - R - 25), (C + 17, C - R - 26), (C + 25, C - R - 24)]
    d.line([(s(x), s(y)) for x, y in pts], fill=255, width=s(6), joint='curve')
    for x, y in (pts[0], pts[-1]): d.ellipse((s(x - 3), s(y - 3), s(x + 3), s(y + 3)), fill=255)
def band(d, s): d.rounded_rectangle((s(C - 16), s(C - R - 9), s(C + 16), s(C - R + 5)), radius=s(4), fill=255)
def shine(d, s):   # 몸통 오른쪽 위의 반짝임: 작은 덩어리와 점 하나
    d.ellipse((s(C + 6), s(C - 27), s(C + 19), s(C - 17)), fill=255); d.ellipse((s(C + 22), s(C - 14), s(C + 27), s(C - 10)), fill=255)
img = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
for draw, col in ((fuse, INK), (body, INK), (band, GOLD), (shine, BLUE)):
    img.paste(Image.new('RGBA', (SIZE, SIZE), col), (0, 0), layer(draw))
out = ROOT / '대형폭탄-투사체-v1.png'; img.save(out)

# 확인 그림 1: 크게
F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 22); FS = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 17)
big = Image.new('RGBA', (SIZE * 4, SIZE * 4), (246, 246, 246, 255)); big.alpha_composite(img.resize((SIZE * 4, SIZE * 4), Image.NEAREST)); big.convert('RGB').save(ROOT / '대형폭탄-투사체-v1-4배.png')

# 확인 그림 2: 손에 든 폭탄 / 지금 게임 / 2배 확대 / 새 그림 (모두 같은 배율, 1유닛 = 106.67px)
held = Image.open(RES / 'CharacterBombSkills/big_heave/09.png').convert('RGBA'); after = Image.open(RES / 'CharacterBombSkills/big_heave/12.png').convert('RGBA'); t = Image.open(RES / 'EffectsApproved13/bomb/F00.png').convert('RGBA')
def small(scale): k = scale * 106.6667 / 32; return t.resize((round(t.width * k), round(t.height * k)), Image.NEAREST)
W, H = 470, 330; sheet = Image.new('RGB', (4 * W + 50, H + 90), 'white'); d = ImageDraw.Draw(sheet)
def panel(n, title, sub, base, b=None, pos=None):
    x = 10 + n * (W + 10); p = Image.new('RGBA', (W, H), (246, 246, 246, 255)); p.alpha_composite(base.crop((50, 30, 50 + W, 30 + H)))
    if b is not None: p.alpha_composite(b, (pos[0] - b.width // 2, pos[1] - b.height // 2))
    sheet.paste(p.convert('RGB'), (x, 50)); d.text((x + 4, 8), title, font=F, fill=(0, 0, 0)); d.text((x + 4, 54 + H), sub, font=FS, fill=(70, 70, 70))
panel(0, '던지기 직전 (모션 그림)', '손에 든 폭탄 지름 약 0.75유닛', held)
panel(1, '지금 게임', '기본 폭탄 그림 그대로, 0.45유닛', after, small(1), (380, 120))
panel(2, '2배로 키우기만', '크기는 맞지만 픽셀이 굵다', after, small(2), (380, 120))
panel(3, '새 그림 v1', '지름 0.75유닛, 모션과 같은 밀도', after, img, (380, 120))
sheet.save(ROOT / '대형폭탄-투사체-v1-비교.png'); print('ok', out)
