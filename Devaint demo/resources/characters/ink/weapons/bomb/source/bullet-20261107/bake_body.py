# 미사일 몸 B(2026-11-07 사용자 「B 불꽃 붙이기」): 확정 혜성 몸(skill-fx-20261105/missile_body 15장) 앞에 사용자 탄환(64x40, PPU32)을 얹는다.
# 탄환 몸 가운데(32,20) = 시전자 몸 가운데(anchor center). 혜성 피벗은 탄환 가운데보다 22px 뒤 → 불꽃이 탄환 꽁무니에서 나온다.
# 화소는 옮기기만(확대·재표본 없음). 프레임마다 같은 캔버스·같은 피벗.
import json, sys, hashlib
from pathlib import Path
from PIL import Image
ROOT = Path(sys.argv[1]); OUT = Path(sys.argv[2]); BULLET = Path(sys.argv[3])
SRC = ROOT / 'characters/ink/weapons/bomb/runtime/skill-fx-20261105/missile_body'
meta = json.loads((SRC / 'meta.json').read_text(encoding='utf-8'))
cw, ch = meta['size']; cpx, cpy = meta['pivot'][0] * cw, (1 - meta['pivot'][1]) * ch     # 혜성 피벗(위에서 잰 y)
bullet = Image.open(BULLET).convert('RGBA'); bw, bh = bullet.size; bpx, bpy = 32, 20
BACK = 22
# 큰 판에 그린 뒤 프레임 전체의 공통 알파 상자로 자른다
S = 256; O = (160, 128)
frames = []
for f in sorted(SRC.glob('*.png')):
    c = Image.new('RGBA', (S, S))
    c.alpha_composite(Image.open(f).convert('RGBA'), (round(O[0] - BACK - cpx), round(O[1] - cpy)))
    c.alpha_composite(bullet, (O[0] - bpx, O[1] - bpy))
    frames.append((f.name, c))
box = None
for _, c in frames:
    b = c.getbbox(); box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
OUT.mkdir(parents=True, exist_ok=True)
w, h = box[2] - box[0], box[3] - box[1]
pivot = ((O[0] - box[0]) / w, 1 - (O[1] - box[1]) / h)
for name, c in frames:
    c.crop(box).save(OUT / name, optimize=True)
info = {"name": "missile_body", "effect": "skill-fx-20261105/missile_body + 사용자 탄환 64x40", "anchor": "center", "facing": True, "loop": True, "segment": 0, "fps": meta['fps'], "frames": len(frames),
        "size": [w, h], "pivot": list(pivot), "comet_back_px": BACK, "bullet_center_px": [bpx, bpy]}
(OUT / 'meta.json').write_bytes((json.dumps(info, ensure_ascii=False, indent=1) + '\n').encode('utf-8'))
print(w, h, pivot)
