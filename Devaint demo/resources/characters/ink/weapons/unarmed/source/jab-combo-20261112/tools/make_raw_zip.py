# 고른 칸을 시트에서 자른 그대로(640x360, 손대지 않음) zip 으로 묶는다. 배경 제거는 사용자가 다른 도구로 한다.
# 되받을 이름: 맨손잽_배경제거_외곽색보정_16장.zip (파일 이름은 그대로 두면 된다)
import json, zipfile
from pathlib import Path
HERE = Path(__file__).resolve().parent; ROOT = HERE.parent; sel = json.loads((ROOT / '3_프레임' / 'unarmed-selection.json').read_text(encoding='utf-8'))
out = ROOT / '맨손잽-원본칸-16장.zip'; names = []
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for hit, steps in sel['sequence'].items():
        for n, st in enumerate(steps):
            name = f'{hit}_{n + 1:02d}_칸{st["cell"]:02d}.png'; z.write(ROOT / '3_프레임' / '칸' / f'{st["cell"]:02d}.png', name); names.append(name)
    z.writestr('selection.json', json.dumps(sel, ensure_ascii=False, indent=1))
print(out.name, len(names), names)
