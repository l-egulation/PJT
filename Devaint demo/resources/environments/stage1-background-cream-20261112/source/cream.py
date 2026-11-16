"""Stage-1 background in the floor's cream with depth (option C):
keep each dot's lightness, give it the floor's warm lean (#fef7ee), then pull farther layers toward the sky."""
import numpy as np
from PIL import Image

SKY = (252, 247, 238)
CREAM = np.array([6.0, 1.0, -10.0])
DEPTH = {"mountains": .30, "far-village": .16, "near-village": .0, "clouds": .20, "birds": .0, "boss-room-rose-window": .22}


def cream(im, depth):
    a = np.asarray(im.convert("RGBA")).astype(float)
    m = a[..., 3] > 0
    L = a[..., :3] @ np.array([.299, .587, .114])
    out = np.clip(L[..., None] + CREAM, 0, 255)
    out = out + (np.array(SKY) - out) * depth
    a[..., :3] = np.where(m[..., None], np.round(out), a[..., :3])
    return Image.fromarray(a.astype(np.uint8), "RGBA")
