"""Fit the user's colour designs (zip 마을배경-컬러-5종) onto the game's grids: same sizes and dot clusters as the
approved layers, colours reduced to each picture's own few tones, binary alpha, no lone dots."""
import numpy as np
from pathlib import Path
from PIL import Image
from scipy.cluster.vq import kmeans2
from scipy import ndimage
from pxref import downsample

Z = Path(r"C:/Users/SSAFY/AppData/Local/Temp/claude/a222fd4d74955e13/60bab92f-f68c-431c-9999-beba6dfc9717/scratchpad/zip-colour5")
ENV = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112/resources/environments")
SKYSET = ENV / "stage1-atmosphere-sky-ash-20261111/runtime"

def quantize(rgb, al, k, seed=4):
    m = al >= .5
    pts = rgb[m]
    pal, _ = kmeans2(pts, k, minit='++', seed=seed)
    pal = np.clip(pal, 0, 255)
    idx = np.full(al.shape, -1); idx[m] = ((pts[:, None] - pal[None]) ** 2).sum(-1).argmin(1)
    for _ in range(2):                                   # lone dots take their neighbours' colour
        out = idx.copy()
        for y, x in zip(*np.nonzero(m)):
            nb = [idx[yy, xx] for yy in range(max(0, y - 1), min(m.shape[0], y + 2)) for xx in range(max(0, x - 1), min(m.shape[1], x + 2))
                  if (yy, xx) != (y, x) and idx[yy, xx] >= 0]
            if nb and nb.count(idx[y, x]) == 0: out[y, x] = max(set(nb), key=nb.count)
        idx = out
    a = np.zeros(al.shape + (4,), np.uint8)
    a[m, :3] = np.round(pal[idx[m]]).astype(np.uint8); a[m, 3] = 255
    return Image.fromarray(a, "RGBA")

def drop_halo(q):
    """The designs carry a white rim along roofs and trees (left from painting over the sketch); at game scale it
    reads as the sketch showing through. Near-white dots on the silhouette go clear; any inside take their
    neighbours' colour."""
    a = np.array(q); m = a[..., 3] > 0
    halo = m & (a[..., :3].min(-1) >= 205) & (a[..., :3].astype(int).sum(-1) >= 700)
    clear = ndimage.binary_dilation(~m, np.ones((3, 3), bool))
    for _ in range(6):
        edge = halo & clear
        a[edge] = 0; m = a[..., 3] > 0; halo &= m
        clear = ndimage.binary_dilation(~m, np.ones((3, 3), bool))
        if not (halo & clear).any(): break
    for y, x in zip(*np.nonzero(halo)):
        ys, xs = slice(max(0, y - 2), y + 3), slice(max(0, x - 2), x + 3)
        nb = a[ys, xs].reshape(-1, 4); keep = (nb[:, 3] > 0) & ~halo[ys, xs].reshape(-1)
        if keep.any():
            vals, counts = np.unique(nb[keep], axis=0, return_counts=True); a[y, x] = vals[counts.argmax()]
    return Image.fromarray(a, "RGBA")

def layer(src, w, h, cluster, k):
    im = Image.open(Z / src)
    rgb, al = downsample(im, w // cluster, h // cluster)
    q = drop_halo(quantize(rgb, al, k))
    return q.resize((w, h), Image.NEAREST) if cluster > 1 else q

def cells(src, kind, k):
    """six cells (3 x 2) -> six sprites matching the approved ones' canvas and bounding boxes"""
    sheet = Image.open(Z / src).convert("RGBA"); cw, ch = sheet.width // 3, sheet.height // 2
    out = []
    for i in range(6):
        cell = sheet.crop(((i % 3) * cw, (i // 3) * ch, (i % 3 + 1) * cw, (i // 3 + 1) * ch))
        bb = cell.getbbox(); cell = cell.crop(bb)
        ref = Image.open(SKYSET / f"{kind}/{kind}-{i}.png"); rb = ref.getbbox()
        tw = rb[2] - rb[0]; th = max(1, round(cell.height * tw / cell.width))
        rgb, al = downsample(cell, tw, th)
        spr = quantize(rgb, al, k)
        canvas = Image.new("RGBA", ref.size, (0, 0, 0, 0))
        cy = (rb[1] + rb[3]) // 2
        canvas.alpha_composite(spr, (rb[0], max(0, min(ref.height - th, cy - th // 2))))
        out.append(canvas)
    return out

def build(out_dir):
    out = Path(out_dir); (out / "sky/clouds").mkdir(parents=True, exist_ok=True); (out / "sky/birds").mkdir(parents=True, exist_ok=True)
    layer("03-가까운마을.png", 666, 334, 2, 9).save(out / "near-village.png")
    layer("04-먼마을.png", 444, 222, 1, 6).save(out / "far-village.png")
    layer("05-산.png", 888, 444, 1, 2).save(out / "mountains.png")
    for i, c in enumerate(cells("01-구름.png", "clouds", 3)): c.save(out / f"sky/clouds/clouds-{i}.png")
    # birds: the user's slate colour on the approved flocks, three flap frames each (their marks vanish at game scale)
    from colorize import flap_frames
    for i in range(6):
        for f, fr in enumerate(flap_frames(Image.open(SKYSET / f"birds/birds-{i}.png"), (0x6a, 0x7e, 0x89))):
            fr.save(out / f"sky/birds/birds-{i}-{f}.png")

if __name__ == "__main__":
    build("out/color5")
