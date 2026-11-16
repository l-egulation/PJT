# 단검 이펙트 시안 미리보기(2026-11-07): 평타·숨죽이기·먹안개·삼연 도약, 시안 a·b. 확정 몸 그림(dagger_all_preview 의 FR) 위에 dagger-fx 를 얹는다.
# 스킬마다 GIF 하나: 위 줄 a, 아래 줄 b / 왼쪽 흰 바탕, 오른쪽 어두운 바탕. 은신 중(숨죽이기 0.10초~, 먹안개 0.15초~)은 게임처럼 몸 35%.
# 삼연 도약 a 의 먹빛 몸 잔상 셋은 게임 코드가 그릴 것이라 여기서 그림으로 흉내 낸다.
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import dagger_all_preview as A
HERE = A.HERE; FXD = HERE / 'dagger-fx'; U = A.U; GY = 232; H = 270
_fx = {}
def fx(name, tag):
    k = (name, tag)
    if k not in _fx:
        d = FXD / name / tag; m = json.load(open(d / 'meta.json'))
        _fx[k] = ([A.big(Image.open(q).convert('RGBA')) for q in sorted(d.glob('*.png'))], (m['origin_px'][0] * 2, m['origin_px'][1] * 2), m['fps'])
    return _fx[k]
def put_fx(c, name, tag, t, x, y):
    fr, (ox, oy), fps = fx(name, tag); i = int(t * fps + 1e-6)
    if 0 <= i < len(fr): c.alpha_composite(fr[i], (round(x - ox), round(y - oy)))
def fade(im, a):
    im = im.copy(); al = np.array(im)[..., 3]; im.putalpha(Image.fromarray((al * a).astype(np.uint8))); return im
def inked(im, a):
    arr = np.array(im).copy(); arr[..., :3] = (27, 22, 22); arr[..., 3] = (arr[..., 3] * a).astype(np.uint8); return Image.fromarray(arr)
SK = {s[0]: s for s in A.SK}
CFG = {
    '평타':     dict(s=SK['평타 4연타'], W=460, PX=130, mobs=[1.6], end=None, hide=None),
    '숨죽이기': dict(s=SK['숨죽이기'], W=460, PX=150, mobs=[2.6], end=.95, hide=.10),
    '먹안개':   dict(s=SK['먹안개'], W=560, PX=280, mobs=[2.2], end=4.55, hide=.15),
    '삼연도약': dict(s=SK['삼연 도약'], W=560, PX=80, mobs=[2.4, 4.6], end=.7, hide=None),
}
NAMES = {'평타': {'a': 'a 작은 초승달', 'b': 'b 먹 튐'}, '숨죽이기': {'a': 'a 먹 물결', 'b': 'b 그림자 덮기'},
         '먹안개': {'a': 'a 뭉게 먹구름', 'b': 'b 먹 띠 장막'}, '삼연도약': {'a': 'a 먹 잔상줄', 'b': 'b 일섬'}}
def body_x(cfg, t):
    mv = cfg['s'][5]; x = cfg['PX']
    if mv: a, ln, dist = mv; x += dist * U * min(1, max(0, (t - a) / ln))
    return x
def scene(key, v, t, dark):
    cfg = CFG[key]; nm, end, tl, hits, piv, mv, _, _ = cfg['s']; W = cfg['W']; PX = cfg['PX']
    bg = (38, 42, 50, 255) if dark else (255, 255, 255, 255); fg = (230, 230, 230, 255) if dark else (34, 34, 34, 255)
    c = Image.new('RGBA', (W, H), bg); d = ImageDraw.Draw(c); d.line((8, GY, W - 8, GY), fill=(200, 90, 60, 255), width=2)
    cy = GY - A.MOB_CY * U
    for mu in cfg['mobs']:
        ex = PX + mu * U; shake = 0
        hit_ts = hits if key != '삼연도약' else [mv[0] + mv[1] * ((mu * U - 20) / (mv[2] * U))]
        for h in hit_ts:
            if 0 <= t - h < .06: shake = 2 if int((t - h) * 50) % 2 == 0 else -2
        c.alpha_composite(A.MOB, (round(ex - 128 + shake), GY - 240))
    if key == '먹안개': put_fx(c, 'dagger-ink-smoke', v, t - .15, PX, GY)        # 연막은 몸 뒤·적 앞… 게임 정렬은 적용 때 정함, 여기선 몸 뒤
    x = body_x(cfg, t); k = A.at(tl, max(0, min(t, tl[-1][0] + 1)))
    body = A.FR[k]
    if key == '삼연도약' and v == 'a':
        for lag, a in ((.03, .55), (.06, .38), (.09, .22)):
            if mv[0] < t - lag + .03 and t - lag < mv[0] + mv[1] + .06:
                c.alpha_composite(inked(A.FR[A.at(tl, max(0, t - lag))], a), (round(body_x(cfg, t - lag) - piv[0]), GY - piv[1]))
    hidden = cfg['hide'] is not None and t >= cfg['hide']
    c.alpha_composite(fade(body, .35) if hidden else body, (round(x - piv[0]), GY - piv[1]))
    if key == '숨죽이기' and v == 'b' and .08 <= t < .26:   # 몸이 먹빛 실루엣이 됐다가 걷힌다(게임 코드 몫)
        c.alpha_composite(inked(body, 1 if t < .12 else max(0, 1 - (t - .12) / .14)), (round(x - piv[0]), GY - piv[1]))
    if key == '숨죽이기': put_fx(c, 'dagger-hold-breath', v, t, PX, GY)
    if key == '삼연도약': put_fx(c, 'dagger-triple-leap', v, t, PX, GY)
    if key == '평타':
        ex = PX + cfg['mobs'][0] * U
        for n, h in enumerate(hits): put_fx(c, 'dagger-basic-hit', f'{v}-{n + 1}', t - h, ex, cy)
    d.text((8, 6), f'{key}  {NAMES[key][v]}', font=A.F, fill=fg)
    span = cfg['end'] or end
    d.rectangle((0, H - 5, int(W * min(1, max(0, t) / span)), H), fill=(224, 164, 58, 255))
    return c.convert('RGB')
def frame(key, t):
    W = CFG[key]['W']; out = Image.new('RGB', (W * 2 + 6, H * 2 + 6), (120, 120, 120))
    for r, v in enumerate('ab'):
        out.paste(scene(key, v, t, False), (0, r * (H + 6))); out.paste(scene(key, v, t, True), (W + 6, r * (H + 6)))
    return out
if __name__ == '__main__':
    keys = sys.argv[1:] or list(CFG)
    for key in keys:
        span = CFG[key]['end'] or CFG[key]['s'][1]
        for step, suf in ((.02, '실제속도'), (.008, '느리게')):
            if key == '먹안개' and suf == '느리게': span_s = 1.0
            else: span_s = span
            fr = []; t = -.08
            while t < span_s: fr.append(frame(key, t)); t += step
            fr[0].save(HERE / '../4_미리보기' / f'단검이펙트-{key}-{suf}.gif', save_all=True, append_images=fr[1:], duration=20, loop=0, optimize=True)
            print(key, suf, len(fr))
