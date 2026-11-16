"""Boss room rear from the reference painting (monumental a-arena): the right tower with its top beam and
dripping corbels is pixelized with colours sampled from the painting and mirrored for the left tower;
the beam is continued across the top. The far clock wheel is a separate faint layer (s_bossring)."""
import numpy as np
from PIL import Image
from px import *
from pxref import pixelize, tidy_lines, flatten, bevel, smooth_silhouette, flat_stones

SRC = r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/map-redesign-research-20261110/resources/environments/stage1-clockroom-monumental-20261111/preview/a-arena.png"
S = 0.5742 / 2            # reference px -> dots (the painting is 1672 px for a 960 px screen)
GROUND = 848              # reference row of the floor contact
CX0 = 1250                # right tower crop starts here


def tower_source():
    im = Image.open(SRC).convert("RGBA")
    a = np.asarray(im).copy()
    H, W = a.shape[:2]
    lum = a[..., :3].astype(float) @ [.299, .587, .114]
    yy, xx = np.mgrid[0:H, 0:W]
    body = (xx >= CX0 + 148) & (yy < GROUND)
    beam = yy < 62
    corbel = (xx >= CX0 + 40) & (yy < 320) & (lum < 200)
    keep = (body | beam | corbel) & (xx >= CX0) & (yy < GROUND)
    a[..., 3] = np.where(keep, 255, 0)
    return Image.fromarray(a).crop((CX0, 0, W, GROUND))


def draw(width_dots=480):
    import s_tower as T
    t = T.draw()
    w, h = t.w, t.h
    c = Canvas(width_dots, h)
    right = t.im
    left = right.transpose(Image.FLIP_LEFT_RIGHT)
    # the top beam continues across the room: repeat the beam's own stones (x 0..47 of the tower)
    import random
    rng = random.Random(5)
    beam = Canvas(width_dots, 48)
    cuts, x = [], 0
    while x < width_dots - 80:
        x += rng.choice((56, 64, 72)); cuts.append(x)
    T.course(beam, 0, 23, 0, width_dots - 1, cuts, T.MID)
    beam.hl(0, width_dots - 1, 24, T.JD)
    # underside shadow: deep where it meets each tower's bracket (15 dots), easing to a thin band with soft ink bumps
    import math
    depths = []
    for x in range(width_dots):
        d = min(x - (w - 1), (width_dots - w) - x)               # distance from the nearer tower
        depths.append(4 + 11 * math.exp(-max(0, d) / 10.0) + 1.0 * math.sin(x * .16))
    depths = [round(sorted(depths[max(0, i - 3):i + 4])[len(depths[max(0, i - 3):i + 4]) // 2]) for i in range(width_dots)]
    for x in range(width_dots):                                 # steps at least 3 dots wide: no comb teeth
        if 0 < x < width_dots - 2 and depths[x] != depths[x - 1] and depths[x + 1] != depths[x]: depths[x] = depths[x - 1]
    for x in range(width_dots):
        for y in range(25, 25 + max(3, depths[x])): beam.px(x, y, T.DARK)
    for x, n in ((w + 14, 3), (w + 31, 5), (w + 52, 2), (width_dots // 2 - 20, 4), (width_dots // 2 + 9, 6),
                 (width_dots - w - 47, 3), (width_dots - w - 26, 5), (width_dots - w - 12, 2)):
        y0 = 25
        while y0 < 47 and beam.get(x, y0 + 1) == T.DARK: y0 += 1
        for k in range(1, n + 1): beam.px(x, y0 + k, T.DARK)
    from pxref import add_shading
    strip = Image.open(SRC).convert("RGBA").crop((0, 0, 1672, 75))
    add_shading(beam, [T.MID], strip, step=9, levels=2, gain=2.2, clean=3, smooth=5)
    from pxref import light_temperature
    light_temperature(beam)
    c.im.alpha_composite(beam.im.crop((w - 1, 0, width_dots - w + 1, 48)), (w - 1, 0))
    c.im.alpha_composite(left, (0, 0))
    c.im.alpha_composite(right, (width_dots - w, 0))
    return c


if __name__ == '__main__':
    c = draw(); c.native().save('out/bossroom2-towers.png')
