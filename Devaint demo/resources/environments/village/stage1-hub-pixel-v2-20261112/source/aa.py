"""Selective anti-aliasing on the Scale2x images (one screen px per cell): a mid tone between the ink line and the
fill only at the corners of the line's steps. Never against the empty background (alpha stays 0/255).
mode 'fill': the fill pixel tucked into a step's inner corner; 'ink': the ink pixel sticking out at a step's outer
corner; 'both'."""
import numpy as np
from pathlib import Path
from PIL import Image
import bld_detail4 as F

EPX = Path(__file__).parent.parent / 'bld2' / 'epx'
INK_C = [np.array(F.CO4[k][:3]) for k in ('in', 'out')]


def ink_mask(c):
    m = np.zeros(c.shape[:2], bool)
    for k in INK_C: m |= (c[..., :3] == k).all(-1) & (c[..., 3] > 0)
    return m


def aa(color, sketch, mode, w=.5):
    ink = ink_mask(color); empty = color[..., 3] == 0
    fill = ~ink & ~empty
    H, W = ink.shape
    outs = [color.copy(), sketch.copy()]
    pad = lambda m: np.pad(m, 1, constant_values=False)
    I, Fm = pad(ink), pad(fill)
    U, Dn, L, Rt = (slice(0, -2), slice(1, -1)), (slice(2, None), slice(1, -1)), (slice(1, -1), slice(0, -2)), (slice(1, -1), slice(2, None))
    corners = []
    for a, b in ((U, L), (U, Rt), (Dn, L), (Dn, Rt)):           # two orthogonal neighbours
        oa = {U: Dn, Dn: U, L: Rt, Rt: L}
        if mode in ('fill', 'both'):                              # fill px with ink on two sides, fill on the others
            m = fill & I[a] & I[b] & Fm[oa[a]] & Fm[oa[b]]
            corners.append((m, a, b, 'ink'))
        if mode in ('ink', 'both'):                               # ink px with fill on two sides, ink on the others
            m = ink & Fm[a] & Fm[b] & I[oa[a]] & I[oa[b]]
            corners.append((m, a, b, 'fill'))
    for img, out in zip((color, sketch), outs):
        P = np.pad(img.astype(float), ((1, 1), (1, 1), (0, 0)), mode='edge')
        for m, a, b, other in corners:
            ys, xs = np.nonzero(m)
            na = P[a][ys, xs]; nb = P[b][ys, xs]
            other_c = (na + nb) / 2
            out[ys, xs, :3] = np.round(img[ys, xs, :3] * (1 - w) + other_c[:, :3] * w).astype(np.uint8)
    return outs


if __name__ == '__main__':
    import epx
    for n, *_ in epx.ASSETS:
        c = np.array(Image.open(EPX / f'{n}-color.png').convert('RGBA')); s = np.array(Image.open(EPX / f'{n}-sketch.png').convert('RGBA'))
        for mode in ('fill', 'ink', 'both'):
            c2, s2 = aa(c, s, mode)
            Image.fromarray(c2).save(EPX / f'{n}-color-aa-{mode}.png'); Image.fromarray(s2).save(EPX / f'{n}-sketch-aa-{mode}.png')
    print('ok')
