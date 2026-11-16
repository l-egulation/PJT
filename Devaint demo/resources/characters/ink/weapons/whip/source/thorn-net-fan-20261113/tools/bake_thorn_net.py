# 가시 그물 게임용 굽기(2026-11-13, 사용자 확정: 부채 그물 · 부드러움 4 아주 강하게).
#   thorn_net/NN.png : 부채 그물 + 채찍(손→뿌리는 0.8초까지), 시전 0.315초부터 초당 30장, 1.4초에 끝(마지막 장들은 화소가 빠지며 사라짐).
#                      PPU 88.22(몸 모션과 같은 화소 밀도), 피벗 = 발(왼쪽 끝 x 0, 아래에서 9화소). 오른쪽을 본 그림, 왼쪽은 좌우 뒤집기.
#   body/15~19.png   : 몸 모션(CharacterWhipSkills/thorn-net) 그물이 든 장에서 그물·채찍만 지운 판(주먹은 남김). 크기·피벗 그대로.
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parent))
import net_fan as fan, net_scale as ns
OUT = Path(__file__).resolve().parent.parent / '게임용'; (OUT / 'thorn_net').mkdir(parents=True, exist_ok=True); (OUT / 'body').mkdir(exist_ok=True)
fan.configure(4)
NP = 88.22; BELOW = 9; X0, X1, TOP = 0.0, 11.0, 5.4
W = int(round((X1 - X0) * NP)); H = int(round(TOP * NP)) + BELOW; Y1 = TOP
FPS = 30; n = int(round((fan.DISSOLVE_TO - fan.START) * FPS))
sizes = []
for i in range(n):
    t = fan.START + i / FPS + 1e-6
    im = fan.layer(t, X0, Y1, W, H, NP) or Image.new('RGBA', (W, H))
    im.save(OUT / 'thorn_net' / f'{i:02d}.png', optimize=True); sizes.append(int((np.asarray(im)[..., 3] > 0).sum()))
for k in ns.NET:
    a, net, grip = ns.split(k); b = a.copy(); b[net] = 0; Image.fromarray(b).save(OUT / 'body' / f'{k}.png', optimize=True)
info = {'format': 'deviant-whip-thorn-net-fan-v1', 'pixelsPerUnit': NP, 'size': [W, H], 'pivot': [0.0, round(BELOW / H, 6)], 'pivotPx': [0, H - BELOW],
        'framesPerSecond': FPS, 'frames': n, 'startsAt': fan.START, 'endsAt': fan.START + n / FPS, 'facing': 'right; mirror for left',
        'level': 4, 'params': {'delay': fan.DELAY, 'wave': fan.WAVE, 'sag': fan.SAG, 'spokeLag': fan.SPOKE_LAG, 'pivot': list(fan.PIVOT), 'radius': [fan.R0, fan.R1], 'openDeg': 54},
        'approval': 'user-approved-20261113 (부채 그물, 부드러움 4 아주 강하게)',
        'note': '채찍 위 낮은 뿌리(발 앞 2.2u)에서 부채처럼 펴져 서고(0.315~0.40초), 위로 넘어오며 커져 적 너머 약 10.4u 까지 덮은 뒤 땅에 내려앉는다(적은 통과). 1.15~1.4초에 화소가 빠지며 사라진다. 손→뿌리 채찍은 0.8초(몸 모션 끝)까지만.'}
(OUT / 'thorn_net' / 'fx.json').write_text(json.dumps(info, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
print('frames', n, 'size', W, H, 'pixels', sizes[:3], sizes[-3:])
