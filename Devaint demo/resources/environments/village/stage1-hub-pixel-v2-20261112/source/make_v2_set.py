"""Assemble resources/environments/village/stage1-hub-pixel-v2-20261112: the two village buildings (80 %), the
palette atelier's colour-spread frames, and the nine props again with the corner mid tones."""
import hashlib, json, shutil
import numpy as np
from pathlib import Path
from PIL import Image

PIX = Path(__file__).parent
PICK = PIX.parent / 'bld2/aa1/pick'
RES = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-hub-pixel-20261112/resources")
USER = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/04_테스트/마을 건물 픽셀 규격")
REL = "environments/village/stage1-hub-pixel-v2-20261112"
OUT = RES / REL
def wtext(p, t): p.write_bytes(t.encode("utf-8"))
def x2(src, dst):
    a = np.array(Image.open(src).convert('RGBA')); a[a[..., 3] == 0] = 0
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1)).save(dst)
if OUT.exists(): shutil.rmtree(OUT)
for d in ("runtime/props/sketch", "runtime/props/color", "runtime/course-props", "runtime/buildings/color", "runtime/buildings/sketch",
          "runtime/effects/palette-atelier", "source/input", "preview"): (OUT / d).mkdir(parents=True)
rows = []
def row(p): w, h = Image.open(p).size; rows.append({"file": p.relative_to(OUT).as_posix(), "width": w, "height": h, "pivot": "bottom"})
PROPS = ('return-well', 'record-desk', 'palette-bench', 'departure-sign', 'low-wall', 'handcart')
for n in PROPS:
    for v in ('sketch', 'color'):
        d = OUT / f"runtime/props/{v}/{n}.png"; x2(PICK / f"{n}-{v}.png", d); row(d)
for n in ('bridge-pier', 'crate', 'fence'):
    for v, suf in (('color', ''), ('sketch', '-sketch')):
        d = OUT / f"runtime/course-props/{n}{suf}.png"; x2(PICK / f"{n}-{v}.png", d); row(d)
for n, f in (('house', 'record-farmhouse'), ('atelier', 'palette-atelier')):
    for v in ('sketch', 'color'):
        d = OUT / f"runtime/buildings/{v}/{f}.png"; x2(PICK / f"{n}-{v}.png", d); row(d)
# colour spread: the colour art revealed dot by dot outward from the red brush head, coverage as the old 8 frames
col = np.array(Image.open(PICK / 'atelier-color.png').convert('RGBA'))
vis = col[..., 3] > 0
yy, xx = np.mgrid[:col.shape[0], :col.shape[1]]
dist = np.hypot(xx - 15, yy - 19)
cover = (.002, .087, .244, .453, .694, .879, .987, 1.0)
for k, c in enumerate(cover, 1):
    r = np.quantile(dist[vis], c) if c < 1 else dist.max() + 1
    f = col.copy(); f[(dist > r) | ~vis] = 0
    d = OUT / f"runtime/effects/palette-atelier/{k:02d}.png"
    Image.fromarray(np.repeat(np.repeat(f, 2, 0), 2, 1)).save(d); row(d)
contract = {"format": "stage1-hub-pixel-v2", "pixelsPerUnit": 32, "dot": "2x2 px", "filterMode": "Point", "compression": "None",
            "mipmaps": False, "alpha": "binary", "states": "sketch = the colour-drained village (terrain six colours), color = the colour returned",
            "contact": "bottom row is the ground contact line, centred pivot", "cornerMidTones": {"B (ink corner)": ["record-farmhouse", "palette-atelier", "handcart"], "A (fill corner)": "the other props"},
            "colorSpread": "effects/palette-atelier/01..08: the colour art shown dot by dot outward from the red brush head; 08 equals the colour art", "sprites": rows}
wtext(OUT / "runtime/v2.json", json.dumps(contract, ensure_ascii=False, indent=2) + "\n")
for f in ("px.py", "pixel_curves.py", "bld_user_sketch.py", "bld_detail.py", "bld_detail2.py", "bld_detail4.py", "bld_shrink.py", "bld_refine80.py",
          "bld_final80.py", "bld_polish.py", "bld_roofs.py", "epx.py", "aa.py", "make_v2_set.py"):
    shutil.copy2(PIX / f, OUT / "source" / f)
for f in ("사용자다듬음_기록농가_130x162.png", "사용자다듬음_팔레트공방_158x203.png"):
    shutil.copy2(USER / f, OUT / "source/input" / ("record-farmhouse-user.png" if '농가' in f else "palette-atelier-user.png"))
shutil.copy2(PIX.parent / 'bld2/aa1/pick-overview.png', OUT / 'preview/set-color-sketch.png')
shutil.copy2(PIX.parent / 'bld2/aa1/detail2-full.png', OUT / 'preview/buildings-color.png')
readme = """# 1스테이지 마을 · 픽셀판 2차 (건물 2채 + 구조물 중간톤)

`stage1-hub-pixel-props-20261112`의 다음 판이다. 32px = 1유닛(PPU 32), 기본 도트 2×2px, 선 1점.

- **건물 2채**: 기록 농가(`runtime/buildings/*/record-farmhouse.png`, 210×260)와 팔레트 공방(`palette-atelier.png`, 254×326). 사용자가 다듬어 온 그림(`source/input/`)을 80%로 줄이고 선 규칙(직선은 같은 마디, 곡선은 마디가 한 방향으로만 변함)에 맞춰 정리한 뒤 문·창·차양·난간·항아리·지붕 모서리를 손질했다.
- **공방 색 번짐**: `runtime/effects/palette-atelier/01..08.png`. 색 판을 붉은 붓 머리에서부터 도트 단위로 넓혀 드러낸다. 08은 색 판과 같다.
- **구조물 9종**: 1차 판에 계단 모서리 중간톤을 넣었다. 선이 꺾이는 모서리에만 선 색과 면 색의 중간 색을 쓴다. 건물·손수레는 튀어나온 선 모서리(B), 나머지는 안쪽 면 모서리(A). 바깥 윤곽(투명과 맞닿는 곳)은 그대로라 알파는 0/255다.

`sketch`는 지형 6색(#fffcf7 #fef7ee #ccc7be #a09d96 #6f6e69, 먹 #3f3e3c), `color`는 색이 돌아온 마을이다. 크기·기준점은 `runtime/v2.json`.

## 제작

`source/bld_final80.py`가 사용자 그림을 재질 이름 배열로 읽어 손질(`bld_polish.py`)하고 두 팔레트로 칠한다. `aa.py`가 중간톤을 넣고 `make_v2_set.py`가 2배로 키워 이 폴더를 만든다.
"""
wtext(OUT / "README.md", readme)
files = sorted(p.relative_to(OUT).as_posix() for p in OUT.rglob("*") if p.is_file() and p.name != 'asset.json')
asset = {"format": "deviant-resource-asset-v1", "id": "environments.village.stage1-hub-pixel-v2-20261112", "path": REL,
         "status": "user-approved-art", "gameIntegration": "main feat/stage1-hub-buildings-20261112", "selected": "runtime/v2.json",
         "generationTool": "user drawings tidied on the 2x2 grid with Python (source/)", "layersContract": "runtime/v2.json", "files": files,
         "fileHashes": {f: hashlib.sha256((OUT / f).read_bytes()).hexdigest() for f in files}, "approvedScope": ["runtime"]}
wtext(OUT / "asset.json", json.dumps(asset, ensure_ascii=False, indent=2) + "\n")
cat = json.loads((RES / "catalog.json").read_text(encoding="utf-8"))
cat["assets"] = [a for a in cat["assets"] if a["id"] != asset["id"]] + [{"id": asset["id"], "path": REL, "fileCount": len(files)}]
wtext(RES / "catalog.json", json.dumps(cat, ensure_ascii=False, indent=2) + "\n")
gen = json.loads((RES / "generated-assets.json").read_text(encoding="utf-8"))
gen["files"] = [e for e in gen["files"] if not e["path"].startswith(REL + "/")]
for f in files:
    data = (OUT / f).read_bytes()
    gen["files"].append({"path": REL + "/" + f, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
wtext(RES / "generated-assets.json", json.dumps(gen, ensure_ascii=False, indent=2) + "\n")
print(len(files), "files")
