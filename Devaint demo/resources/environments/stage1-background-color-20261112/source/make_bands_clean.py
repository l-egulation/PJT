"""Background bands by the pixel-art rules (video 「게임 속 픽셀 아트에 숨겨져 있는 규칙」), one drawing for both states:
the colour bands are redrawn by hand in 2x2 dots after the user's design (near_redraw, far_redraw, mountain_redraw),
then the sketch band is that same drawing with each colour turned into the cream sketch's warm grey of matching
brightness, so the two coincide dot for dot when overlapped."""
from pathlib import Path
import numpy as np
from PIL import Image

RES = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112/resources/environments")
COL = RES / "stage1-background-color-20261112/runtime"
CREAM = RES / "stage1-background-cream-20261112/runtime"

BANDS = (("near-village", "03-가까운마을.png", 666, 334, 2, 9), ("far-village", "04-먼마을.png", 444, 222, 1, 6), ("mountains", "05-산.png", 888, 444, 1, 2))
lum = lambda c: .3 * c[..., 0] + .59 * c[..., 1] + .11 * c[..., 2]
(COL / "sketch").mkdir(exist_ok=True)
import near_redraw, far_redraw, mountain_redraw
DRAWN = {'near-village': near_redraw.img, 'far-village': far_redraw.b.img, 'mountains': mountain_redraw.img}
for name, src, w, h, dot, k in BANDS:
    clean = Image.fromarray(DRAWN[name], 'RGBA').resize((w, h), Image.NEAREST)
    clean.save(COL / f"{name}.png", optimize=True)
    # cream tones of the same brightness range as the approved cream band (warm greys r, r-5, r-16)
    a = np.array(clean).astype(float); m = a[..., 3] > 0
    old = np.array(Image.open(CREAM / f"{name}.png").convert('RGBA')).astype(float)
    reds = np.unique(old[old[..., 3] > 0][:, 0], return_counts=True)
    reds = reds[0][reds[1] > reds[1].sum() * .02]
    L = lum(a[m]); lo, hi = L.min(), L.max()
    r = np.round(reds.min() + (L - lo) / max(1e-6, hi - lo) * (reds.max() - reds.min()))
    s = np.zeros_like(a); s[m, 0] = r; s[m, 1] = r - 5; s[m, 2] = r - 16; s[m, 3] = 255
    Image.fromarray(s.astype(np.uint8), 'RGBA').save(COL / "sketch" / f"{name}.png", optimize=True)
    print(name, 'sketch tones', len(np.unique(r)))
