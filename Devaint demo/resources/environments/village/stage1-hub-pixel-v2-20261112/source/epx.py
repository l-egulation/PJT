"""Scale2x (EPX) on our dot art: each logical dot (2x2 screen px) splits into four screen px, and only the corner
of a diagonal step takes its neighbour's colour; no new colours. The colour and the sketch version are scaled as one
image of (colour, sketch) pairs, so both get the very same shape."""
import numpy as np
from pathlib import Path
from PIL import Image

PIX = Path(__file__).parent
OUT = PIX.parent / 'bld2' / 'epx'; OUT.mkdir(exist_ok=True)


def scale2x_ids(ids):
    H, W = ids.shape
    P = np.pad(ids, 1, mode='edge')
    A, B, C, D, E = P[:-2, 1:-1], P[1:-1, 2:], P[1:-1, :-2], P[2:, 1:-1], P[1:-1, 1:-1]   # up, right, left, down
    out = np.empty((H * 2, W * 2), ids.dtype)
    out[0::2, 0::2] = np.where((C == A) & (C != D) & (A != B), A, E)
    out[0::2, 1::2] = np.where((A == B) & (A != C) & (B != D), B, E)
    out[1::2, 0::2] = np.where((D == C) & (D != B) & (C != A), C, E)
    out[1::2, 1::2] = np.where((B == D) & (B != A) & (D != C), D, E)
    return out


def scale2x_pair(color, sketch):
    """color, sketch: RGBA arrays at one px per dot -> both at two px per dot, smoothed alike"""
    c = color.reshape(-1, 4); s = sketch.reshape(-1, 4)
    pairs = np.concatenate([c, s], 1)
    uniq, ids = np.unique(pairs, axis=0, return_inverse=True)
    ids = ids.reshape(color.shape[:2])
    big = scale2x_ids(ids)
    return uniq[big][..., :4].astype(np.uint8), uniq[big][..., 4:].astype(np.uint8)


def dot(path, two_x):
    a = np.array(Image.open(path).convert('RGBA'))
    a = a[0::2, 0::2] if two_x else a
    a[a[..., 3] == 0] = 0                    # one 'empty'
    return a


ASSETS = [(n, PIX / f'out/hub/{n}-color.png', PIX / f'out/hub/{n}-sketch.png', True) for n in
          ('return-well', 'record-desk', 'palette-bench', 'departure-sign', 'low-wall', 'handcart', 'bridge-pier', 'crate', 'fence')] + \
         [(n, PIX.parent / f'bld2/v80-{n}-color.png', PIX.parent / f'bld2/v80-{n}-sketch.png', False) for n in ('house', 'atelier')]

if __name__ == '__main__':
    for n, pc, ps, two in ASSETS:
        c, s = dot(pc, two), dot(ps, two)
        c2, s2 = scale2x_pair(c, s)
        Image.fromarray(c2).save(OUT / f'{n}-color.png'); Image.fromarray(s2).save(OUT / f'{n}-sketch.png')
        Image.fromarray(np.repeat(np.repeat(c, 2, 0), 2, 1)).save(OUT / f'{n}-color-blocky.png')
        Image.fromarray(np.repeat(np.repeat(s, 2, 0), 2, 1)).save(OUT / f'{n}-sketch-blocky.png')
        print(n, c2.shape[1::-1])
