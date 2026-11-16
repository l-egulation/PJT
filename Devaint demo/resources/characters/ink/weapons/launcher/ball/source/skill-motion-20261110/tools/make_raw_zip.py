# 발사기 고른 칸 원본 그대로 묶음(2026-11-10, 폭탄 때와 같은 방식): 에테르 시트에서 칸만 잘라 낸다. 옮기기·지우기·색 정리 없음.
# 발 맞춤 세로 옮김값(shift)과 크기 비(scale, 얼굴 기준)는 shift.json 에 적어 두고, 배경 제거본을 받은 뒤 적용한다.
import io, json, sys, zipfile
from pathlib import Path
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parent))
import launcher_pick as L
used = {k: L.used(k) for k in L.SHEETS}
n = sum(map(len, used.values())); zp = L.ROOT / '3_보정·추린프레임' / f'발사기-원본칸-{n}장.zip'; zp.parent.mkdir(exist_ok=True)
info = {L.NAMES[k]: {f'{i:02d}': {'shift': int(L.GROUND - L.sole_bottom(k, i)), 'scale': round(L.scale_of(k, i), 4)} for i in ids} for k, ids in used.items()}
with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
    for k, ids in used.items():
        sh = Image.open(L.SRC / L.SHEETS[k])
        for i in ids:
            buf = io.BytesIO(); sh.crop(((i % 6) * 640, (i // 6) * 360, (i % 6 + 1) * 640, (i // 6 + 1) * 360)).save(buf, 'PNG'); z.writestr(f'{L.NAMES[k]}/{i:02d}.png', buf.getvalue())
    z.writestr('shift.json', json.dumps(info, ensure_ascii=False, indent=1))
    z.writestr('설명.txt', f'발사기 스킬 고른 칸 {n}장, 에테르 시트에서 잘라 낸 원본 그대로(2026-11-10).\n'
               '칸 640x360, 흰 배경, 옮기기·지우기·색 정리 없음. 파일 이름 = 시트 칸 번호(6x6, 왼쪽 위부터 0).\n'
               '에테르가 그려 넣은 연노랑 총구 불꽃, 샷건 착지 먼지가 남아 있는 칸이 있다(지워 주면 좋음). 배경 제거 후 크기와 자리는 바꾸지 말 것.\n'
               'shift.json: 칸마다 발 맞춤 세로 옮김(px, +는 아래)과 크기 비(얼굴 기준, 게임 대기 그림에 맞춤). 받은 뒤 이 값으로 맞추고 정리한다.\n')
json.dump(info, open(Path(__file__).resolve().parent / 'launcher-v1-shift.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(zp.name, n, {L.NAMES[k]: len(v) for k, v in used.items()})
