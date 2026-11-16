# 보스 길목·보스방 시안(render_boss.py 결과 background/foreground, 32px=1u, 세계 x -14..84 · y -5..20)을 게임에 넣을 조각으로 자른다.
# 시안 좌표: 통로 안쪽 x 3..51 · 바닥 y 0, 보스방 안쪽 x 52..82 (전장 왼쪽 아래 = (52,-3), 바닥 윗면 y 0 = 전장 y 3).
#   boss-hall.png         통로 뒷배경 48×6u (등·배관·수문 그림, 빛 구워 넣음)
#   boss-hall-grates.png  통로 바닥 격자 둘(바닥 윗면 1px 아래부터 9px), 48u 폭 투명 바탕
#   boss-room.png         보스방 뒷배경 30×15u (바닥 윗면 위)
#   boss-floor.png        보스방 바닥 30u × (이끼 8px + 3u) — 입장·2·3페이즈의 바닥
#   boss-room-air.png     1페이즈 공중전 판 보스방 30×18u(전장 전체). render_boss.py --air 결과에서 자른다: 바닥 3u 가 비고
#                         양옆 벽·수문·물줄기가 전장 아래 끝까지 이어진다(사용자 2026-11-13 「배경을 밑까지 이어야 끊겨 보이지 않는다」)
import sys
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parent))
from render_boss import FLOOR_GRATES   # noqa: E402

src, out, air = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]); out.mkdir(parents=True, exist_ok=True)   # 3번째 = render_boss.py --air 출력
bg = Image.open(src / 'background.png').convert('RGBA'); fg = Image.open(src / 'foreground.png').convert('RGBA')
assert bg.size == (98 * 32, 25 * 32)
X0, Y1 = -14, 20
px = lambda wx: round((wx - X0) * 32); py = lambda wy: round((Y1 - wy) * 32)
bg.crop((px(3), py(6), px(51), py(0))).save(out / 'boss-hall.png')
bg.crop((px(52), py(15), px(82), py(0))).save(out / 'boss-room.png')
fg.crop((px(52), py(0.25), px(82), py(-3))).save(out / 'boss-floor.png')
# 바닥 격자: render_boss.post_fg 와 같은 무늬를 통로 기준 투명 바탕에 다시 찍는다
g = np.zeros((9, 48 * 32, 4), np.uint8)
for (wx, w) in FLOOR_GRATES:
    x0 = round((wx - 3) * 32); x1 = x0 + round(w * 32)
    g[0, x0:x1] = (110, 140, 128, 255)
    for x in range(x0, x1): g[1:8, x] = (12, 20, 24, 255) if (x - x0) % 5 < 2 else (58, 84, 82, 255)
    g[8, x0:x1] = (8, 12, 14, 255)
Image.fromarray(g).save(out / 'boss-hall-grates.png')
Image.open(air / 'background.png').convert('RGBA').crop((px(52), py(15), px(82), py(-3))).save(out / 'boss-room-air.png')
for f in sorted(out.glob('*.png')): print(f.name, Image.open(f).size)
