# 고른 칸 원본 그대로 묶음(2026-11-10 사용자 「원본 그대로 zip」): 에테르 시트에서 칸만 잘라 낸다. 옮기기·지우기·색 정리 없음.
# 발 맞춤 세로 옮김값은 설명.txt 와 shift.json 에 따로 적어 두고, 배경 제거본을 받은 뒤 적용한다.
import io, json, sys, zipfile
from pathlib import Path
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bomb_pick as B
used = {k: sorted({i for _, i in B.TL[k]} | ({35} if k != '03' else set())) for k in B.TL}
n = sum(map(len, used.values())); zp = B.ROOT / '3_보정·추린프레임' / f'폭탄v2-원본칸-{n}장.zip'
shift = {B.NAMES[k]: {f'{i:02d}': int(B.shift_y(k, i)) for i in ids} for k, ids in used.items()}
sheets = {}
with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
    for k, ids in used.items():
        sh = sheets.setdefault(k, Image.open(B.SRC / B.SHEETS[k]))
        for i in ids:
            c = sh.crop(((i % 6) * 640, (i // 6) * 360, (i % 6 + 1) * 640, (i // 6 + 1) * 360))
            buf = io.BytesIO(); c.save(buf, 'PNG'); z.writestr(f'{B.NAMES[k]}/{i:02d}.png', buf.getvalue())
    z.writestr('shift.json', json.dumps(shift, ensure_ascii=False, indent=1))
    z.writestr('설명.txt', f'폭탄 스킬 v2 고른 칸 {n}장, 에테르 시트에서 잘라 낸 원본 그대로(2026-11-10).\n'
               '칸 640x360, 흰 배경, 옮기기·지우기·색 정리 없음. 파일 이름 = 시트 칸 번호(6x6, 왼쪽 위부터 0).\n'
               '날아가는 폭탄도 그대로 남아 있다(지워 주면 좋음). 배경 제거 후 크기와 자리는 바꾸지 말 것.\n'
               '발 맞춤(땅 줄 y=327)용 세로 옮김값은 shift.json(칸마다 px, + 는 아래로). 받은 뒤 이 값으로 옮기고 6색 정리한다.\n')
json.dump(shift, open(Path(__file__).resolve().parent / 'bomb-v2-shift.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(zp.name, n)
