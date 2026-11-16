# 단검 확정 이펙트 모아 보기(2026-11-07): 평타(이펙트 없음, 기본 맞음 이펙트 사용) → 숨죽이기 b → 먹안개 b(테두리 없음) → 삼연 도약 b 일섬 → 그림자 난무 e.
# 위 흰 바탕, 아래 어두운 바탕. 먹안개는 3초를 다 보여 준다.
from PIL import Image, ImageDraw
import dagger_all_preview as A
import dagger_fx_preview as P
W = 900; H = P.H
def bare(key, t, dark):   # 평타: 이펙트 없는 몸만
    im = P.scene(key, 'a', t, dark) if False else None
    return im
def scene(i, t, dark):
    if i == 0:
        cfg = P.CFG['평타']; old = P.put_fx; P.put_fx = lambda *a, **k: None
        try: im = P.scene('평타', 'a', t, dark)
        finally: P.put_fx = old
        d = ImageDraw.Draw(im); bg = (38, 42, 50) if dark else (255, 255, 255)
        d.rectangle((0, 0, im.width, 28), fill=bg); d.text((8, 6), '평타 4연타 (기본 맞음 이펙트)', font=A.F, fill=(230, 230, 230) if dark else (34, 34, 34))
        return im
    if i == 4:
        im = A.scene(A.SK[4], t, dark).crop((0, 30, A.SW, 30 + H))   # 땅선 맞춤(262 → 232)
        ImageDraw.Draw(im).text((8, 6), '그림자 난무  e 초승달 폭풍', font=A.F, fill=(230, 230, 230) if dark else (34, 34, 34)); return im
    return P.scene(['', '숨죽이기', '먹안개', '삼연도약'][i], 'b', t, dark)
SPANS = [P.CFG['평타']['s'][1], P.CFG['숨죽이기']['end'], P.CFG['먹안개']['end'], P.CFG['삼연도약']['end'], A.SK[4][1]]
def frames(step):
    out = []
    for i, span in enumerate(SPANS):
        t = -.08
        while t < span:
            im = Image.new('RGB', (W, H * 2 + 6), (120, 120, 120))
            for r, dark in enumerate((False, True)):
                s = scene(i, t, dark); im.paste(s.crop((0, 0, min(W, s.width), H)), (0, r * (H + 6)))
            out.append(im); t += step
    return out
if __name__ == '__main__':
    for step, name in ((.02, '단검-확정이펙트-실제속도.gif'), (.008, '단검-확정이펙트-느리게.gif')):
        fr = frames(step); fr[0].save(A.HERE / '../4_미리보기' / name, save_all=True, append_images=fr[1:], duration=20, loop=0, optimize=True); print(name, len(fr))
