"""Validate the three explicitly approved stage-1 art groups without loading unrelated imports."""
from pathlib import Path
import hashlib
import json
import re

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUPS = (
    "stage1-pixel-lawn-wood-32",
    "stage1-pixel-background-native-20261111",
    "stage1-atmosphere-sky-ash-20261111",
)


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    catalog = {x["id"]: x for x in read(ROOT / "catalog.json")["assets"]}
    generated = {x["path"]: x for x in read(ROOT / "generated-assets.json")["files"]}
    count = 0
    for name in GROUPS:
        group = ROOT / "environments" / name
        asset = read(group / "asset.json")
        expected = set(asset["files"])
        actual = {p.relative_to(group).as_posix() for p in group.rglob("*") if p.is_file()}
        assert expected | {"asset.json"} == actual, f"Unindexed or missing file: {name}"
        assert asset["status"] == "user-approved-art", name
        assert catalog[asset["id"]]["fileCount"] == len(expected), name
        for relative in actual:
            path = group / relative
            assert path.resolve().is_relative_to(group.resolve()), relative
            key = path.relative_to(ROOT).as_posix()
            record = generated[key]
            assert digest(path) == record["sha256"] and path.stat().st_size == record["bytes"], key
            if relative != "asset.json":
                assert digest(path) == asset["fileHashes"][relative], key
            if path.suffix == ".html":
                for link in re.findall(r'(?:src|href)="([^"#]+)"', path.read_text(encoding="utf-8")):
                    if "${" not in link and not re.match(r"[a-z]+:", link):
                        assert (path.parent / link).is_file(), (path, link)
        count += len(actual)

    tile = ROOT / "environments" / GROUPS[0] / "runtime"
    spec = read(tile / "tileset.json")
    assert spec["tileSize"] == spec["pixelsPerUnit"] == 32
    assert spec["artPixelSize"] == 2 and spec["surfaceInsetPx"] == 8
    assert len(spec["tiles"]) == 188 and len(spec["canonicalMaskForRawMask"]) == 256
    entries = {(x["mask"], x["variant"]): x for x in spec["tiles"]}
    assert len(entries) == 188
    for mask in spec["canonicalMaskForRawMask"]:
        for variant in range(4):
            assert (mask, variant) in entries
    for file in tile.rglob("*.png"):
        if file.name == "terrain-atlas.png":
            continue
        pixels = np.asarray(Image.open(file).convert("RGBA"))
        assert pixels.shape == (32, 32, 4), file
        assert np.array_equal(pixels, np.repeat(np.repeat(pixels[::2, ::2], 2, 0), 2, 1)), file

    native = ROOT / "environments" / GROUPS[1] / "runtime"
    for layer in read(native / "layers.json")["layers"]:
        pixels = np.asarray(Image.open(native / layer["file"]).convert("RGBA"))
        grid = layer["pixelCluster"]
        assert pixels.shape[:2] == (layer["height"], layer["width"])
        assert set(np.unique(pixels[:, :, 3])) <= {0, 255}
        assert np.array_equal(pixels, np.repeat(np.repeat(pixels[::grid, ::grid], grid, 0), grid, 1))
        assert len(np.unique(pixels[-1], axis=0)) == 1 and pixels[-1, 0, 3] == 255
    weather = ROOT / "environments" / GROUPS[2]
    sky = read(weather / "runtime/sky-weather.json")
    assert len(sky["layers"]) == 12
    assert [x["count"] for x in sky["ash"]["paletteMix"]] == [67, 10, 19]
    for layer in sky["layers"]:
        pixels = np.asarray(Image.open(weather / layer["path"]).convert("RGBA"))
        assert pixels.shape[:2] == (layer["height"], layer["width"])
        assert set(np.unique(pixels[:, :, 3])) <= {0, 255}
        assert len(np.unique(pixels[pixels[:, :, 3] > 0], axis=0)) <= 3
    for room in ("s1-04", "s1-07", "s1-07-upper", "s1-08"):
        assert Image.open(weather / f"preview/{room}-approved.png").size == (960, 540)
    animation = Image.open(weather / "preview/paper-ash-mixed-960.gif")
    assert animation.size == (960, 540) and animation.n_frames == 48
    print(f"PASS: {count} indexed files; 188 tile variants; native grids; 12 sky assets; four game views; 48 animation frames")


if __name__ == "__main__":
    main()
