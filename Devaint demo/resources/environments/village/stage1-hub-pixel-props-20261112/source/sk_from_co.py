"""Sketch twin of a pixelized colour prop: same dots, each colour mapped by luminance rank to the terrain tones."""
import sys
import numpy as np
from PIL import Image
from px import *

def logical(path):
    im = Image.open(path).convert("RGBA"); return im.resize((im.width // 2, im.height // 2), Image.NEAREST)

def sketch_of(co, tones=(D, M, L, C, W)):
    a = np.asarray(co).copy()
    op = a[..., 3] > 0
    cols = sorted({tuple(p[:3]) for p in a[op]} - {tuple(K[:3])}, key=lambda c: c[0] * .299 + c[1] * .587 + c[2] * .114)
    lum = np.array([c[0] * .299 + c[1] * .587 + c[2] * .114 for c in cols])
    # spread by luminance between the darkest and brightest colour (not by rank), so near colours stay together
    t = (lum - lum.min()) / max(1, lum.max() - lum.min())
    m = {c: tones[min(len(tones) - 1, int(v * len(tones)))] for c, v in zip(cols, t)}
    out = a.copy()
    for y, x in zip(*np.nonzero(op)):
        c = tuple(a[y, x, :3])
        if c != tuple(K[:3]): out[y, x] = m[c]
    return Image.fromarray(out), m

if __name__ == "__main__":
    for n in sys.argv[1:]:
        co = logical(f"out/hub/{n}-color.png")
        sk, m = sketch_of(co, (M, L, C, C, W, W))   # the tones used for the record desk
        print(n, {('#%02x%02x%02x' % k): ('#%02x%02x%02x' % v[:3]) for k, v in m.items()})
        sk.resize((sk.width * 2, sk.height * 2), Image.NEAREST).save(f"out/hub/{n}-sketch-from-color.png")
