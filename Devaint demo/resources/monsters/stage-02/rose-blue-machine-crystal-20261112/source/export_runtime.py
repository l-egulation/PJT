# 잉크 결정(1페이즈 중앙 장치) 실행용 시트와 sheets.json 을 뽑는다.
#   python export_runtime.py <출력 폴더>
# 시트 칸 규칙은 Unity RoseBlueEncounter 의 VFX 시트와 같다: 칸은 왼쪽 위부터 행 우선, pivot 은 칸 왼쪽 위에서 잰 화소, 32px = 1u.
# 대기·손상·맞기 칸(96×128)의 pivot (48,76) 은 판정 2×3u 칸의 가운데다(판정 칸은 칸 안 x 16~80, y 28~124).
# 부서지기 칸(256×256)은 결정 칸을 (80,64)에 둔 것이라 pivot 이 (128,140)이다.
import json, sys
from pathlib import Path
from PIL import Image
import crystal_v3 as c
import crystal_states as st

def sheet(frames, cols):
    w, h = frames[0].size; rows = (len(frames) + cols - 1) // cols
    im = Image.new('RGBA', (w * cols, h * rows))
    for i, f in enumerate(frames): im.alpha_composite(f, ((i % cols) * w, (i // cols) * h))
    return im, rows

def main(out):
    out = Path(out); out.mkdir(parents=True, exist_ok=True)
    plan = [('machine-crystal-idle', [st.damaged(i, 0) for i in range(c.N)], 12, (48, 76), [85] * 12, True),
            ('machine-crystal-damage1', [st.damaged(i, 1) for i in range(c.N)], 12, (48, 76), [85] * 12, True),
            ('machine-crystal-damage2', [st.damaged(i, 2) for i in range(c.N)], 12, (48, 76), [85] * 12, True)]
    for d in (0, 1, 2): plan.append((f'machine-crystal-hit{d}', st.hit_frames(d), 3, (48, 76), [50] * 3, False))
    plan.append(('machine-crystal-break', st.break_frames(), 7, (128, 140), [70] * 14, False))
    sheets = []
    for (name, frames, cols, (px, py), ms, loop) in plan:
        im, rows = sheet(frames, cols); im.save(out / f'{name}.png', optimize=True)
        w, h = frames[0].size
        sheets.append(dict(name=name, columns=cols, rows=rows, frames=len(frames), cellWidth=w, cellHeight=h,
                           pivotX=px, pivotY=py, durationsMs=ms, loop=loop))
    (out / 'sheets.json').write_text(json.dumps({'sheets': sheets}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('runtime', len(sheets), 'sheets ->', out)

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / 'runtime')
