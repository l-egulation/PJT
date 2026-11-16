"""Mountain band redrawn in 2x2 dots (444 x 222 -> 888 x 444): the user's two ridgelines, smoothed, then every
slope's steps reordered so they only shrink toward the steepest point and grow after it (no jaggies)."""
import numpy as np
from PIL import Image
from scipy import ndimage

SRC = "C:/Users/SSAFY/Desktop/자율 프로젝트/outputs/village-colour/old-mountains.png"
BACK, FRONT = (194, 216, 222), (174, 200, 199)

def profile(mask):
    """top row of the mask in each column (rows count down), None where empty"""
    has = mask.any(0); top = mask.argmax(0).astype(float); top[~has] = mask.shape[0]
    return top

def regular(h):
    """h: integer heights per column. Within each rise or fall, reorder the flat runs so their lengths are V-shaped
    (long, shorter ... shortest at the steepest, ... longer, long); steps steeper than one dot stay as they are."""
    h = list(h); n = len(h)
    # runs of equal height
    runs = []; i = 0
    while i < n:
        j = i
        while j + 1 < n and h[j + 1] == h[i]: j += 1
        runs.append([h[i], j - i + 1]); i = j + 1
    out = []; k = 0
    while k < len(runs):
        # a monotone stretch of single-dot steps
        m = k
        while m + 1 < len(runs) and abs(runs[m + 1][0] - runs[m][0]) == 1 and (m == k or np.sign(runs[m + 1][0] - runs[m][0]) == np.sign(runs[m][0] - runs[m - 1][0])):
            m += 1
        seg = runs[k:m + 1]
        if len(seg) >= 4:
            inner = [r[1] for r in seg[1:-1]]        # the end runs are the flats at the top/bottom: keep them
            lo = int(np.argmin(inner))
            inner = sorted(inner[:lo + 1], reverse=True) + sorted(inner[lo + 1:])
            for r, L in zip(seg[1:-1], inner): r[1] = L
        out.extend(seg if not out or out[-1] is not seg[0] else seg[1:])
        k = m + 1 if m > k else k + 1
    res = []
    for v, L in out: res += [v] * L
    return np.array(res[:n])

src = np.array(Image.open(SRC).convert('RGBA'))[::2, ::2]
back = np.all(src[..., :3] == BACK, -1) | np.all(src[..., :3] == FRONT, -1)
front = np.all(src[..., :3] == FRONT, -1)
H, W = back.shape
img = np.zeros((H, W, 4), np.uint8)
for mask, col in ((back, BACK), (front, FRONT)):
    top = ndimage.gaussian_filter1d(profile(mask), 6, mode='reflect')
    top = regular(np.round(top).astype(int))
    for x in range(W):
        img[top[x]:, x, :3] = col; img[top[x]:, x, 3] = 255

if __name__ == '__main__':
    im = Image.fromarray(img, 'RGBA').resize((W * 2, H * 2), Image.NEAREST); im.save('out/mountain-redraw.png')
    o = Image.open(SRC).convert('RGBA')
    v = lambda i: (lambda bg: (bg.alpha_composite(i), bg)[1])(Image.new('RGBA', i.size, (188, 214, 230, 255))).crop((0, 140, 888, 330))
    s = Image.new('RGB', (888, 388), 'white'); s.paste(v(o), (0, 0)); s.paste(v(im), (0, 198)); s.save('out/mountain-cmp.png')
