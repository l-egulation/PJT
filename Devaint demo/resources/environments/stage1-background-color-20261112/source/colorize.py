"""Colour versions of the stage-1 combat background, clouds and birds (village colour world):
each grey step of the approved art becomes one material colour; birds get three flap frames."""
import numpy as np
from PIL import Image
from pathlib import Path
from scipy import ndimage

ENV = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112/resources/environments")
NATIVE = ENV / "stage1-pixel-background-native-20261111/runtime"
SKYSET = ENV / "stage1-atmosphere-sky-ash-20261111/runtime"
SKY = (188, 214, 230)

def h(s): s = s.lstrip('#'); return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))
MAPS = {
    "near-village": {"c4bcb3": "cf9277", "cbc4bb": "a3b086", "d3ccc3": "97a98c", "dbd5cb": "e2d2ba", "e3ddd3": "efe3ce", "eae5db": "f7eedd"},
    "far-village": {"dbd4c9": "d4b4a3", "e2dbd0": "bdc9b3", "e8e1d6": "dce2da"},
    "mountains": {"ece6dc": "b9ccd8", "f1ece3": "cfdde5"},
    "clouds": {"e6e2d8": "fbf7ee", "ddd8cd": "d3dde5"},
    "birds": {"b7afa0": "3e4954"},
}

def recolour(im, mapping):
    a = np.asarray(im.convert("RGBA")).copy()
    for k, v in mapping.items():
        kk, vv = h(k), h(v)
        m = (a[..., 0] == kk[0]) & (a[..., 1] == kk[1]) & (a[..., 2] == kk[2]) & (a[..., 3] > 0)
        a[m, :3] = vv
    return Image.fromarray(a, "RGBA")

GLYPHS = [["#.#", ".#."], ["###", ".#."], [".#.", "#.#"]]     # wings up, level, down

def flap_frames(im, colour):
    """three frames of the same flock: every little bird redrawn as a 3x2 glyph at its own centre"""
    a = np.asarray(im.convert("RGBA"))[..., 3] > 0
    lab, n = ndimage.label(a, structure=np.ones((3, 3)))
    centres = [tuple(int(round(v)) for v in ndimage.center_of_mass(lab == i)) for i in range(1, n + 1)]
    frames = []
    for f in range(3):
        out = Image.new("RGBA", im.size, (0, 0, 0, 0))
        for k, (cy, cx) in enumerate(centres):
            g = GLYPHS[(f + k) % 3]
            for dy, row in enumerate(g):
                for dx, ch in enumerate(row):
                    if ch == '#':
                        x, y = cx - 1 + dx, cy - 1 + dy
                        if 0 <= x < im.width and 0 <= y < im.height: out.putpixel((x, y), colour + (255,))
        frames.append(out)
    return frames

def build(out_dir):
    out = Path(out_dir); (out / "sky/clouds").mkdir(parents=True, exist_ok=True); (out / "sky/birds").mkdir(parents=True, exist_ok=True)
    for name in ("mountains", "far-village", "near-village"):
        recolour(Image.open(NATIVE / f"{name}.png"), MAPS[name]).save(out / f"{name}.png")
    for i in range(6):
        recolour(Image.open(SKYSET / f"clouds/clouds-{i}.png"), MAPS["clouds"]).save(out / f"sky/clouds/clouds-{i}.png")
        for f, fr in enumerate(flap_frames(Image.open(SKYSET / f"birds/birds-{i}.png"), h("3e4954"))):
            fr.save(out / f"sky/birds/birds-{i}-{f}.png")

if __name__ == "__main__":
    build("out/color")
