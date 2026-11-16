"""Turn the redesigned falling clock hand (2026-11-10) into the 96 x 480 px needle part.

Playtest feedback: the old needle was about 34 px wide inside its 3 x 12-unit (96 x 384 px at PPU 32)
hit column. The user picked a redesign drawn at about 5x scale on transparent alpha (a first, broader
redesign read too big in the room); its opaque height is mapped onto 480 px (the 384 px column at 66 px
read a little small, so it is 1.25x that) and its width keeps the drawing's proportions (82 px at the
guard), centred on the pivot column. Unity sinks the thin point into the floor on impact. Each game pixel is opaque when most of its source block is, and
takes the block's mean colour; those colours are banded by brightness onto the boss palette, so edges
stay crisp and the importer's palette check holds. The redesign original is kept unchanged under source/.
"""
import numpy as np
from PIL import Image

WIDTH, HEIGHT = 96, 480  # 3-unit hit column wide; 1.25x the column height (user: a little bigger)


def pixelate(image, palette):
    src = np.array(image.convert('RGBA')).astype(np.float64)
    ys, xs = np.where(src[:, :, 3] > 127)
    top, bottom, left, right = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    opaque = src[:, :, 3] > 127
    # The height fills the column; the width keeps the drawing's proportions, centred on the pivot column.
    scale = (bottom - top) / HEIGHT
    width = min(WIDTH, int(round((right - left) / scale)))
    width += (WIDTH - width) % 2
    offset = (WIDTH - width) // 2
    left = (left + right) / 2 - width * scale / 2
    # One mean colour per game pixel (painterly texture inside a block averages out).
    mean = np.zeros((HEIGHT, WIDTH, 3))
    solid = np.zeros((HEIGHT, WIDTH), bool)
    for y in range(HEIGHT):
        y0 = top + (bottom - top) * y // HEIGHT
        y1 = max(y0 + 1, top + (bottom - top) * (y + 1) // HEIGHT)
        for x in range(width):
            x0 = max(0, int(left + x * scale))
            x1 = max(x0 + 1, int(left + (x + 1) * scale))
            block = opaque[y0:y1, x0:x1]
            if block.size and block.mean() >= .5:
                solid[y, offset + x] = True
                mean[y, offset + x] = src[y0:y1, x0:x1, :3][block].mean(0)
    # The redesign's own tones are ranked into as many brightness bands as the boss palette has shades
    # (1-D k-means on luminance), and band k takes the k-th darkest palette colour.
    colours = sorted(([int(c[i:i + 2], 16) for i in (1, 3, 5)] for c in palette), key=sum)
    lum = mean[solid] @ np.array([.299, .587, .114])
    centres = np.percentile(lum, np.linspace(5, 95, len(colours)))
    for _ in range(50):
        band = np.argmin(abs(lum[:, None] - centres[None]), axis=1)
        centres = np.array([lum[band == k].mean() if (band == k).any() else centres[k] for k in range(len(colours))])
        centres.sort()
    band = np.argmin(abs(lum[:, None] - centres[None]), axis=1)
    out = np.zeros((HEIGHT, WIDTH, 4), np.uint8)
    out[solid, :3] = np.array(colours)[band]
    out[solid, 3] = 255
    return Image.fromarray(out)


if __name__ == '__main__':
    import sys
    import json
    palette = json.loads(sys.argv[3]) if len(sys.argv) > 3 else None
    pixelate(Image.open(sys.argv[1]), palette).save(sys.argv[2])
