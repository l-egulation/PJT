import hashlib, json, shutil
from pathlib import Path
from PIL import Image
from cream import cream, DEPTH, SKY
PIX = Path(__file__).parent
RES = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112/resources")
ENV = RES / "environments"
REL = "environments/stage1-background-cream-20261112"; OUT = RES / REL
if OUT.exists(): shutil.rmtree(OUT)
for d in ("runtime/sky/clouds", "runtime/sky/birds", "source", "preview"): (OUT / d).mkdir(parents=True)
src = {"mountains": ENV / "stage1-pixel-background-native-20261111/runtime/mountains.png",
       "far-village": ENV / "stage1-pixel-background-native-20261111/runtime/far-village.png",
       "near-village": ENV / "stage1-pixel-background-native-20261111/runtime/near-village.png",
       "boss-room-rose-window": ENV / "stage1-boss-approach-pixel-20261112/runtime/boss-room-rose-window.png"}
rows = []
for k, p in src.items():
    cream(Image.open(p), DEPTH[k]).save(OUT / f"runtime/{k}.png"); rows.append({"file": f"runtime/{k}.png", "from": p.relative_to(RES).as_posix(), "depth": DEPTH[k]})
for kind in ("clouds", "birds"):
    for i in range(6):
        p = ENV / f"stage1-atmosphere-sky-ash-20261111/runtime/{kind}/{kind}-{i}.png"
        cream(Image.open(p), DEPTH[kind]).save(OUT / f"runtime/sky/{kind}/{kind}-{i}.png")
        rows.append({"file": f"runtime/sky/{kind}/{kind}-{i}.png", "from": p.relative_to(RES).as_posix(), "depth": DEPTH[kind]})
contract = {"format": "stage1-background-cream-v1", "sky": "#%02X%02X%02X" % SKY, "floorLean": "#fef7ee (R+6 G+1 B-10 over lightness)",
            "rule": "lightness kept per dot; floor cream lean added; farther layers blended toward the sky by depth", "files": rows}
(OUT / "runtime/layers.json").write_text(json.dumps(contract, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
shutil.copy2(PIX / "cream.py", OUT / "source/cream.py"); shutil.copy2(PIX / "make_cream.py", OUT / "source/make_cream.py"); shutil.copy2(PIX / "bgtone.py", OUT / "source/bgtone.py")
shutil.copy2(PIX / "out/bgtone-options.png", OUT / "preview/options-now-A-B-C.png")
(OUT / "README.md").write_bytes("""# 1스테이지 배경 · 바닥 크림색과 원근 (C안)

채택된 분리 배경(산·먼 마을·가까운 마을), 구름·새, 보스방 둥근 창틀을 바닥 타일의 크림색에 맞춘 판이다. 원본 세트(`stage1-pixel-background-native-20261111`, `stage1-atmosphere-sky-ash-20261111`, `stage1-boss-approach-pixel-20261112`)는 그대로 두고 여기서 새로 뽑는다.

- 점마다 밝기는 그대로 두고, 바닥(#fef7ee)과 같은 노란 기운을 더한다.
- 먼 층일수록 하늘색(#FCF7EE)에 섞는다: 산 30%, 둥근 창틀 22%, 구름 20%, 먼 마을 16%, 가까운 마을·새 0%.
- 카메라 바탕색도 #FCF7EE로 맞춘다.

`runtime/layers.json`에 파일별 원본과 섞는 정도가 있다. `source/make_cream.py`가 다시 뽑는다. `preview/options-now-A-B-C.png`는 지금·A·B·C 시안 비교다(채택 C = 오른쪽 아래).
""".encode("utf-8"))
files = sorted(p.relative_to(OUT).as_posix() for p in OUT.rglob("*") if p.is_file())
asset = {"format": "deviant-resource-asset-v1", "id": "environments.stage1-background-cream-20261112", "path": REL, "status": "user-approved-art",
         "gameIntegration": "main feat/stage1-background-cream-20261112", "selected": "runtime/layers.json", "generationTool": "Python recolour of approved sets (source/)",
         "layersContract": "runtime/layers.json", "files": files, "fileHashes": {f: hashlib.sha256((OUT / f).read_bytes()).hexdigest() for f in files}, "approvedScope": ["runtime"]}
(OUT / "asset.json").write_text(json.dumps(asset, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
cat = json.loads((RES / "catalog.json").read_text(encoding="utf-8"))
cat["assets"] = [a for a in cat["assets"] if a["id"] != asset["id"]] + [{"id": asset["id"], "path": REL, "fileCount": len(files)}]
(RES / "catalog.json").write_text(json.dumps(cat, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
gen = json.loads((RES / "generated-assets.json").read_text(encoding="utf-8"))
gen["files"] = [e for e in gen["files"] if not e["path"].startswith(REL + "/")]
for f in files:
    d = (OUT / f).read_bytes(); gen["files"].append({"path": REL + "/" + f, "bytes": len(d), "sha256": hashlib.sha256(d).hexdigest()})
(RES / "generated-assets.json").write_bytes((json.dumps(gen, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
print(len(files), "files")
