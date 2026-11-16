"""Existing stage-1 structure art -> native 2x2-dot pixel art (logical grid = display/2).

Keeps the current design: silhouette, inner lines and colour areas come from the
existing picture; only the dot grid and palette are rebuilt.
"""
import json, sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage

SRC = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/unarmed-approved-motion-20261111/resources/environments")
OUT = Path(__file__).parent / "out"

# Adopted terrain palette (stage1-pixel-lawn-wood-32)
INK = (0x3f, 0x3e, 0x3c)
BASE = [(0xff, 0xfc, 0xf7), (0xfe, 0xf7, 0xee), (0xcc, 0xc7, 0xbe), (0xa0, 0x9d, 0x96), (0x6f, 0x6e, 0x69), INK]


def hexrgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def lum(rgb):
    return rgb[..., 0] * .299 + rgb[..., 1] * .587 + rgb[..., 2] * .114


def load(spec):
    im = Image.open(SRC / spec["file"]).convert("RGBA")
    if spec.get("crop"):
        im = im.crop(tuple(spec["crop"]))
    return im


def to_logical(im, lw, lh, dark_t, dark_frac, alpha_t):
    a = np.asarray(im, dtype=np.float64) / 255.0
    rgb, al = a[..., :3], a[..., 3:4]
    prem = np.concatenate([rgb * al, al], axis=2)
    pim = Image.fromarray((prem * 255).round().astype(np.uint8), "RGBA")
    small = np.asarray(pim.resize((lw, lh), Image.BOX), dtype=np.float64) / 255.0
    sa = small[..., 3]
    srgb = np.where(sa[..., None] > 1e-6, small[..., :3] / np.maximum(sa[..., None], 1e-6), 0)
    # dark line coverage per cell: lines thinner than a cell must survive
    L = lum(rgb * 255) * (al[..., 0] > .5)
    dark = ((L < dark_t) & (al[..., 0] > .5)).astype(np.float64)
    dcov = np.asarray(Image.fromarray((dark * 255).astype(np.uint8)).resize((lw, lh), Image.BOX), dtype=np.float64) / 255.0
    mask = sa > alpha_t
    line = (dcov > dark_frac) & mask
    return srgb * 255, mask, line, dcov


def nearest(px, pal):
    pal = np.asarray(pal, dtype=np.float64)
    w = np.array([.3, .59, .11]) * 3
    d = (((px[:, None, :] - pal[None, :, :]) ** 2) * w).sum(-1)
    return d.argmin(1)


def kmeans(px, k, iters=20, seed=1):
    rng = np.random.default_rng(seed)
    c = px[rng.choice(len(px), size=min(k, len(px)), replace=False)].astype(np.float64)
    for _ in range(iters):
        lab = nearest(px, c)
        for j in range(len(c)):
            if (lab == j).any():
                c[j] = px[lab == j].mean(0)
    return [tuple(int(round(v)) for v in cc) for cc in c]


def clean_orphans(idx, mask, protect):
    """Single isolated dots that differ from all 8 neighbours take the neighbour majority."""
    h, w = idx.shape
    out = idx.copy()
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            if not mask[y, x] or protect[y, x]:
                continue
            nb = idx[y - 1:y + 2, x - 1:x + 2].ravel().tolist(); nb.pop(4)
            nm = mask[y - 1:y + 2, x - 1:x + 2].ravel().tolist(); nm.pop(4)
            vals = [v for v, m in zip(nb, nm) if m]
            if vals and idx[y, x] not in vals:
                out[y, x] = max(set(vals), key=vals.count)
    return out


def convert(spec):
    im = load(spec)
    dw, dh = spec["display"]
    lw, lh = dw // 2, dh // 2
    rgb, mask, line, dcov = to_logical(im, lw, lh, spec.get("dark", 95), spec.get("dark_frac", .22), spec.get("alpha", .45))
    if spec.get("fill_holes", True):
        mask = ndimage.binary_fill_holes(mask) | mask
        mask &= ndimage.binary_opening(mask, np.ones((1, 1)))
    # outer contour = 1 logical dot = 2 screen px, like the terrain edge
    er = ndimage.binary_erosion(mask, structure=np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]]), border_value=0)
    edge = mask & ~er
    pal = [hexrgb(h) for h in spec.get("palette", [])] or list(BASE)
    if spec.get("accents"):
        px = rgb[mask & ~line & ~edge]
        sat = px.max(1) - px.min(1)
        acc = px[sat > spec.get("sat", 40)]
        if len(acc) > 3:
            pal += kmeans(acc, spec["accents"])
    pal = [tuple(p) for p in pal]
    if INK not in pal:
        pal.append(INK)
    ink_i = pal.index(INK)
    body = mask & ~edge & ~line
    idx = np.full((lh, lw), -1, dtype=int)
    # body colours: never the outline ink unless the source is that dark
    flat = rgb[body]
    lab = nearest(flat, pal)
    idx[body] = lab
    idx[line] = ink_i if not spec.get("line_color") else pal.index(hexrgb(spec["line_color"]))
    idx[edge] = ink_i
    for keep in spec.get("dark_fill", []):
        pass
    idx = clean_orphans(idx, mask, edge | line)
    out = np.zeros((lh, lw, 4), dtype=np.uint8)
    P = np.asarray(pal, dtype=np.uint8)
    out[mask, :3] = P[idx[mask]]
    out[mask, 3] = 255
    logical = Image.fromarray(out, "RGBA")
    native = logical.resize((lw * 2, lh * 2), Image.NEAREST)
    return logical, native, pal


if __name__ == "__main__":
    specs = json.loads((Path(__file__).parent / "specs.json").read_text(encoding="utf-8"))
    only = set(sys.argv[1:])
    OUT.mkdir(exist_ok=True)
    for s in specs:
        if only and s["id"] not in only:
            continue
        lg, nat, pal = convert(s)
        nat.save(OUT / f"{s['id']}.png")
        used = sorted({tuple(p[:3]) for p in lg.getdata() if p[3]})
        print(s["id"], nat.size, len(used), "colours")
