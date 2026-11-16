# 수리검 최종 정리(2026-11-08): 사용자가 가져온 배경 제거본(수리검_배경제거_외곽정리_v3_62장.zip, 발 맞춘 칸 640x360 그대로)에
# 단검과 같은 6색 정리(shuriken_clean.clean)만 입힌다. 결과 3_보정·추린프레임/수리검_6색정리_62프레임.zip + 훑어보기·미리보기.
import io, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import shuriken_pick as P, shuriken_clean as C
HERE = Path(__file__).resolve().parent
SRC = HERE / '../수리검_배경제거_외곽정리_v3_62장.zip'
def zname(i):
    n = i.filename
    if not (i.flag_bits & 0x800):
        try: n = n.encode('cp437').decode('utf-8')
        except Exception: pass
    return n
DIRKEY = {v: k for k, v in C.NAMES.items()}
DONE = {}
with zipfile.ZipFile(SRC) as z:
    for i in z.infolist():
        n = zname(i)
        if not n.lower().endswith('.png'): continue
        d, f = n.split('/')[-2], n.split('/')[-1][:-4]
        DONE[(DIRKEY[d], int(f))] = C.clean(np.array(Image.open(io.BytesIO(z.read(i))).convert('RGBA')))
_c = {}
def cleaned(key, i):
    if (key, i) not in _c:
        a = DONE.get((key, i))
        if a is None: a = C.clean(C.cut(key, i))        # 시간표 밖 칸(대기 #0 등)은 원본에서
        _c[(key, i)] = Image.fromarray(a).resize((round(640 * P.K), round(360 * P.K)), Image.NEAREST)
    return _c[(key, i)]
if __name__ == '__main__':
    out = HERE / f'../3_보정·추린프레임/수리검_6색정리_{len(DONE)}프레임.zip'
    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        for (key, i), a in sorted(DONE.items()):
            buf = io.BytesIO(); Image.fromarray(a).save(buf, 'PNG', optimize=True); z.writestr(f'{C.NAMES[key]}/{i:02d}.png', buf.getvalue())
        z.writestr('설명.txt', f'수리검 {len(DONE)}장 6색 정리(2026-11-08)\n입력: 수리검_배경제거_외곽정리_v3_62장.zip(사용자 배경 제거본)\n'
                   '- 크기·자리 그대로(640x360, 발밑 (220,327), 밑창 땅 y=327)\n- 알파 128 로 딱 자른 외곽, 캐릭터 6색 FDEEDA 1B1616 025673 D2B396 02648C EBA516\n'
                   '- 24화소 미만 조각·구멍, 6화소 미만 색 조각 정리, 외곽·색 경계 3x3 다수결\n시간표: _작업/shuriken-selection.json\n')
    print(out.name)
    F = ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf', 14); rows = []
    for key in C.NAMES:
        fs = [f for _, f in P.SK[key]['tl']]
        row = Image.new('RGB', (len(fs) * 132, 2 * 128 + 22), (150, 150, 150)); d = ImageDraw.Draw(row); d.text((4, 2), C.NAMES[key], font=F, fill=(0, 0, 0))
        for j, f in enumerate(fs):
            im = Image.fromarray(DONE[(key, f)]).crop((100, 90, 360, 340)).resize((130, 125), Image.NEAREST)
            for k2, bg in enumerate(((38, 42, 50), (240, 234, 220))):
                b = Image.new('RGBA', im.size, bg + (255,)); b.alpha_composite(im); row.paste(b.convert('RGB'), (j * 132, 20 + k2 * 128))
        rows.append(row)
    W = max(r.width for r in rows); st = Image.new('RGB', (W, sum(r.height + 4 for r in rows)), (110, 110, 110)); y = 0
    for r in rows: st.paste(r, (0, y)); y += r.height + 4
    st.save(HERE / '../4_미리보기/수리검-6색정리-훑어보기.png')
    import shuriken_clean_preview as V
    P.cell = cleaned; V.P.cell = cleaned
    for step, name in ((.02, '수리검-정리본-실제속도.gif'), (.008, '수리검-정리본-느리게.gif')):
        fr = V.frames(step); fr[0].save(HERE / '../4_미리보기' / name, save_all=True, append_images=fr[1:], duration=20, loop=0, optimize=True); print(name, len(fr))
