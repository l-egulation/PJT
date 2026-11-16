"""Copy the hub pixel props into the worktree's Unity Resources (preview before registering)."""
import shutil, sys
from pathlib import Path
WT = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-hub-pixel-20261112")
HUB = WT / "unity/Assets/Deviant/Hub/Resources/Hub"
OUT = Path(__file__).parent / "out/hub"
PROPS = ("return-well", "record-desk", "palette-bench", "departure-sign", "low-wall", "handcart")
COURSE = ("bridge-pier", "crate", "fence")

def pairs():
    for n in PROPS:
        for v in ("sketch", "color"):
            yield OUT / f"{n}-{v}.png", HUB / "props" / v / f"{n}.png"
    for n in COURSE:
        yield OUT / f"{n}-color.png", HUB / "opening/course-props" / f"{n}.png"
        yield OUT / f"{n}-sketch.png", HUB / "opening/course-props" / f"{n}-sketch.png"

if __name__ == "__main__":
    for src, dst in pairs():
        shutil.copy2(src, dst); print(dst.relative_to(WT))
