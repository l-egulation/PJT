"""Assemble resources/environments/village/stage1-hub-pixel-props-20261112 from the scratch work."""
import hashlib, json, shutil
from pathlib import Path
from PIL import Image
from apply_preview import PROPS, COURSE

PIX = Path(__file__).parent
RES = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-hub-pixel-20261112/resources")
REL = "environments/village/stage1-hub-pixel-props-20261112"
OUT = RES / REL
CAP = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-hub-pixel-20261112/outputs/hub-pixel")
def wtext(p, t): p.write_bytes(t.encode("utf-8"))
if OUT.exists(): shutil.rmtree(OUT)
for d in ("runtime/props/sketch", "runtime/props/color", "runtime/course-props", "source", "preview"): (OUT / d).mkdir(parents=True)

rows = []
for n in PROPS:
    for v in ("sketch", "color"):
        dst = OUT / f"runtime/props/{v}/{n}.png"; shutil.copy2(PIX / f"out/hub/{n}-{v}.png", dst)
        w, h = Image.open(dst).size; rows.append({"file": dst.relative_to(OUT).as_posix(), "width": w, "height": h, "pivot": "bottom"})
for n in COURSE:
    for v, suffix in (("color", ""), ("sketch", "-sketch")):
        dst = OUT / f"runtime/course-props/{n}{suffix}.png"; shutil.copy2(PIX / f"out/hub/{n}-{v}.png", dst)
        w, h = Image.open(dst).size; rows.append({"file": dst.relative_to(OUT).as_posix(), "width": w, "height": h, "pivot": "bottom"})
contract = {"format": "stage1-hub-pixel-props-v1", "pixelsPerUnit": 32, "dot": "2x2 px", "filterMode": "Point", "compression": "None",
            "mipmaps": False, "alpha": "binary", "states": "sketch = the colour-drained village (terrain six colours), color = the colour returned",
            "contact": "bottom row is the ground contact line, centred pivot (even widths keep the 2 px grid)", "sprites": rows}
wtext(OUT / "runtime/props.json", json.dumps(contract, ensure_ascii=False, indent=2) + "\n")

for f in ("px.py", "hub_art.py", "hub_well2.py", "hub_small.py", "hub_small2.py", "hub_redo.py", "pixelize.py", "sk_from_co.py", "apply_preview.py", "make_hub_set.py"):
    shutil.copy2(PIX / f, OUT / "source" / f)
shutil.copy2(PIX / "hub-props-sheet.png", OUT / "preview/props-sketch-color.png")
for p in sorted(CAP.glob("review-*.png")): shutil.copy2(p, OUT / "preview" / ("unity-" + p.name[len("review-"):]))

readme = """# 1스테이지 마을·튜토리얼 구조물 · 2×2 도트 픽셀판

우물 마을(허브)과 튜토리얼 길의 구조물을 전투 구조물 세트(`environments/stage1-pixel-structures-20261112`)와 같은 격자로 다시 그린 세트다. 32px = 1유닛(PPU 32), 기본 도트 2×2px, 선은 1점 굵기. 건물(기록 농가·팔레트 공방)은 이 세트에 없다.

한 그림을 두 벌로 뽑는다. `sketch`는 색이 빠진 마을(지형 6색 #fffcf7 #fef7ee #ccc7be #a09d96 #6f6e69, 먹 #3f3e3c), `color`는 색이 돌아온 마을이다.

| 대상 | 파일 | 크기 |
|---|---|---|
| 우물 | runtime/props/*/return-well.png | 126×138 |
| 기록 책상 | runtime/props/*/record-desk.png | 92×70 |
| 장인 작업대 | runtime/props/*/palette-bench.png | 106×88 |
| 출발 이정표(튜토리얼 팻말 겸용) | runtime/props/*/departure-sign.png | 62×84 |
| 낮은 담 | runtime/props/*/low-wall.png | 90×48 |
| 손수레 | runtime/props/*/handcart.png | 96×62 |
| 튜토리얼 다리 기둥·상자·울타리 | runtime/course-props/{bridge-pier,crate,fence}(-sketch).png | 54×90 · 52×64 · 48×52 |

`runtime/props.json`에 크기·기준점이 있다. 밑줄이 바닥 접점이고 기준점은 아래 가운데다.

## 제작

`source/`의 스크립트가 격자 위에 재질 이름(나무·돌·밧줄·잎·물감…)으로 그리고 `SKETCH`·`COLOR` 팔레트로 두 번 칠한다(`px.py`의 lint로 2×2 뭉침·ㄱ자 꺾임 검사). 손수레·장인 작업대는 승인 그림의 모양을 따라 `hub_redo.py`로 다시 그렸고, 기록 책상은 승인 그림을 10색으로 줄여 픽셀화한 색 판(`pixelize.py`)에서 점을 밝기 순서대로 지형 6색에 옮겨 스케치 판을 만들었다(`sk_from_co.py`). `preview/unity-*.png`는 Unity 6000.6.5f1에서 실제 마을·튜토리얼 길을 960×540으로 찍은 지금/새 그림 비교다.
"""
wtext(OUT / "README.md", readme)

files = sorted(p.relative_to(OUT).as_posix() for p in OUT.rglob("*") if p.is_file())
asset = {"format": "deviant-resource-asset-v1", "id": "environments.village.stage1-hub-pixel-props-20261112", "path": REL,
         "status": "user-approved-art", "gameIntegration": "main feat/stage1-hub-pixel-20261112", "selected": "runtime/props.json",
         "generationTool": "hand-authored on the 2x2 grid with Python (source/); record desk reduced from the approved hub art",
         "layersContract": "runtime/props.json", "files": files,
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
