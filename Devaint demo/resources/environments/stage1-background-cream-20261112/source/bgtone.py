"""Background tone options: keep each colour's lightness, move its hue/chroma toward the terrain's."""
import numpy as np
from PIL import Image
from scene import Screen, NATIVE, PAPER
import scene

def lum(c): return c[0] * .299 + c[1] * .587 + c[2] * .114

def remap(im, mode):
    a = np.asarray(im.convert("RGBA")).astype(float)
    rgb = a[..., :3]; L = rgb @ np.array([.299, .587, .114])
    if mode == "now": return im
    cream = np.array([6.0, 1.0, -10.0])          # the floor's own lean (#fef7ee vs its grey)
    fade = {"A": 0.0, "B": .20, "C": .10, "D": .0}[mode]
    off = cream if mode != "C" else np.array([-3.0, 0.0, 5.0])
    L = L + (255 - L) * fade
    out = np.clip(L[..., None] + off, 0, 255)
    a[..., :3] = np.where(a[..., 3:] > 0, out, rgb)
    return Image.fromarray(a.astype(np.uint8), "RGBA")

DEPTH = {"mountains": .30, "far-village": .16, "near-village": 0}

def scene_for(mode, sky):
    cells = ['.' * 31] * 10 + ['.' * 6 + '#' * 6 + '.' * 19] * 1 + ['.' * 31] * 3 + ['#' * 31] * 3
    sc = Screen(cells)
    sc.im = Image.new("RGBA", (960, 540), sky)
    for name, x, y in (("mountains", -200, 400), ("far-village", -60, 420), ("near-village", -120, 448)):
        im = remap(Image.open(NATIVE / f"{name}.png"), mode)
        if mode == "D":  # graded depth: the farther, the closer to the sky
            a = np.asarray(im).astype(float); m = a[..., 3] > 0
            a[m, :3] = a[m, :3] + (np.array(sky[:3]) - a[m, :3]) * DEPTH[name]; im = Image.fromarray(a.astype(np.uint8), "RGBA")
        w = im.width; xx = x - w
        while xx < 960:
            idx = round((xx - x) / w); piece = im.transpose(Image.FLIP_LEFT_RIGHT) if idx % 2 else im
            sc.im.alpha_composite(piece, (xx, y - im.height))
            row = piece.crop((0, piece.height - 1, w, piece.height)).resize((w, 540 - y))
            sc.im.alpha_composite(row, (xx, y)); xx += w
    sc.terrain(); sc.hero(420, 448)
    return sc.im.convert("RGB")

if __name__ == "__main__":
    skies = {"now": PAPER, "A": (252, 247, 238, 255), "B": (253, 249, 242, 255), "C": (246, 246, 242, 255), "D": (252, 247, 238, 255)}
    shots = [scene_for(m, skies[m]) for m in ("now", "A", "B", "D")]
    s = Image.new("RGB", (1928, 1088), "white")
    for i, im in enumerate(shots): s.paste(im, ((i % 2) * 968, (i // 2) * 548))
    s.save("out/bgtone-options.png")
