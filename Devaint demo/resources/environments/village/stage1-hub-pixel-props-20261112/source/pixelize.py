"""Pixelize an existing painted prop onto the 2x2 grid: keep its own colours but reduce them, clean stray dots,
give it the shared one-dot ink silhouette. Usage: pixelize.py <unity-relative src> <out name> <colours>"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage

U = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/unarmed-approved-motion-20261111/unity/Assets")
K = np.array([0x3f, 0x3e, 0x3c])


def kmeans(px, k, iters=25, seed=3):
    rng = np.random.default_rng(seed)
    # start from luminance-spread samples so dark lines and highlights both get a centre
    lum = px @ [.299, .587, .114]
    order = np.argsort(lum)
    c = px[order[np.linspace(0, len(px) - 1, k).astype(int)]].astype(float)
    for _ in range(iters):
        d = ((px[:, None, :] - c[None]) ** 2).sum(-1)
        lab = d.argmin(1)
        for j in range(k):
            if (lab == j).any(): c[j] = px[lab == j].mean(0)
    return np.round(c).astype(int), lab


def pixelize(src, colours):
    im = np.asarray(Image.open(U / src).convert("RGBA")).astype(float) / 255
    H, W = im.shape[0] // 2, im.shape[1] // 2
    im = im[:H * 2, :W * 2]
    a = im[..., 3:4]
    prem = np.concatenate([im[..., :3] * a, a], -1).reshape(H, 2, W, 2, 4).mean((1, 3))
    alpha = prem[..., 3]
    rgb = np.where(alpha[..., None] > 1e-6, prem[..., :3] / np.maximum(alpha[..., None], 1e-6), 0) * 255
    mask = alpha >= .5
    mask = ndimage.binary_opening(mask, structure=np.ones((1, 1)))
    px = rgb[mask]
    cent, lab = kmeans(px, colours)
    idx = np.full((H, W), -1); idx[mask] = lab
    # stray dots: a dot whose colour none of its 8 neighbours share takes the neighbours' majority
    for _ in range(2):
        out = idx.copy()
        for y in range(H):
            for x in range(W):
                if idx[y, x] < 0: continue
                nb = idx[max(0, y - 1):y + 2, max(0, x - 1):x + 2].ravel().tolist()
                nb.remove(idx[y, x])
                nb = [v for v in nb if v >= 0]
                if nb and idx[y, x] not in nb:
                    out[y, x] = max(set(nb), key=nb.count)
        idx = out
    img = np.zeros((H, W, 4), np.uint8)
    img[mask, :3] = cent[idx[mask]]; img[mask, 3] = 255
    # shared ink silhouette, one dot
    er = ndimage.binary_erosion(mask, structure=[[0, 1, 0], [1, 1, 1], [0, 1, 0]], border_value=0)
    edge = mask & ~er
    img[edge, :3] = K
    return Image.fromarray(img).resize((W * 2, H * 2), Image.NEAREST), len(np.unique(img[mask][:, :3], axis=0))


if __name__ == "__main__":
    src, name, n = sys.argv[1], sys.argv[2], int(sys.argv[3])
    out, used = pixelize(src, n)
    Path("out/hub/px").mkdir(parents=True, exist_ok=True)
    out.save(f"out/hub/px/{name}.png"); print(name, out.size, used, "colours")
