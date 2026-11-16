"""Clean the two phase-1 Aether sheets (Rose mid-air triple shot, Blue ink burst) into game cells.

The user only has the chat-size sheet (2000x1125 WebP, about 120 px characters), smaller than the game size, so:
1. the whole sheet is scaled once by the ratio of the input cell (frame 00) to the approved pose height and every cell
   uses the same affine (no per-frame fitting); cell bounds use the exact 1/6 grid of the sheet;
2. white background -> transparent (binary alpha);
3. drawn effects are removed: yellow muzzle flash pixels, pieces detached from the body except dark/saturated
   pieces of real size (Blue's orb), light-grey dust pieces;
4. every pixel is recoloured to the nearest colour of the approved pose palette (CIELAB) and lone pixels are cleaned,
   so the motion uses exactly the approved colours (user: 기본 자세 색만 쓰도록).
Sources are read only. Output: boss-phase1-attacks-20261107/preview/<clip>/clean/NN.png + clean.json + contact sheets.
"""
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase1-attacks-20261107'
CLIPS = {
    'rose-triple-shot': {'sheet': 'source/rose-triple-shot-aether-01/aether-original.webp', 'approved': 'source/rose-triple-shot/approved-rose-shot.png',
                         'extraPalette': ['../boss-siblings-lineup-20261103/runtime/pixel-poses/rose-idle.png'], 'anchor': 'centre',
                         'rimProtect': [(0, 0, 165, 110)]},  # crossbow and front hand: always left of x 165 above y 110
    'blue-ink-burst': {'sheet': 'source/blue-ink-burst-aether-01/aether-original.webp', 'approved': 'source/blue-ink-burst/approved-blue-idle.png',
                       'extraPalette': [], 'anchor': 'feet'},
}
BG_DISTANCE = 110     # sum(255 - rgb) above this is ink, below is white paper (higher cuts the white-blended rim)
MIN_PIECE = 40        # detached pieces smaller than this are noise
BOLT_TIP_X = 117      # Rose's crossbow tip never reaches left of this column in the non-release frames
M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]); WP = np.array([0.95047, 1.0, 1.08883])


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def lab(rgb):
    c = np.asarray(rgb, float) / 255; c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92); xyz = c @ M.T / WP
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def hsv(rgb):
    r, g, b = [rgb[..., i] / 255 for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    d = mx - mn + 1e-9
    h = np.where(mx == r, (g - b) / d % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return h, np.where(mx > 0, (mx - mn) / (mx + 1e-9), 0), mx


def ink_box(rgb):
    m = (765 - rgb.astype(int).sum(-1)) > BG_DISTANCE
    ys, xs = np.nonzero(m)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def lone_fix(idx, op, n):
    for _ in range(2):
        pad = np.pad(idx, 1, constant_values=-1); H, W = idx.shape
        nb = np.stack([pad[1 + dy:1 + dy + H, 1 + dx:1 + dx + W] for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx], 0)
        same = (nb == idx[None]).sum(0); best = np.full(idx.shape, -1); bc = np.zeros(idx.shape, int)
        for v in range(n):
            c = (nb == v).sum(0); m = c > bc; best[m] = v; bc[m] = c[m]
        fix = op & (((same <= 1) & (bc >= 5)) | ((same <= 2) & (bc >= 6))) & (best >= 0); idx = idx.copy(); idx[fix] = best[fix]
    return idx


RIM_LIGHTER = 8       # CIELAB L above the inner median that marks a white-blended rim pixel
RIM_PASSES = 3        # rings inward (the upscaled blend can be up to 2-3 px thick)
LIGHT_L = 62          # palette colours at or above this CIELAB L are light (mask, skin, bone, leg wraps)


def fill_rim(idx, op, Lp, protect=()):
    idx = idx.copy(); filled = 0; done = np.zeros(op.shape, bool)
    H, W = op.shape
    for _ in range(RIM_PASSES):
        edge = op & ~done & (ndimage.binary_dilation(~op | done, structure=np.ones((3, 3))))
        inner = op & ~edge & ~done
        # Thin light structures (Rose's crossbow limbs) have empty space on both sides; a blended rim always lies on
        # darker body colour, so require at least two non-light opaque 8-neighbours.
        bodyish = op & (Lp[np.maximum(idx, 0)] < LIGHT_L)
        body_nb = ndimage.convolve(bodyish.astype(int), np.ones((3, 3), int), mode='constant') - bodyish
        allowed = edge & (body_nb >= 2)
        for x0p, y0p, x1p, y1p in protect:
            allowed[y0p:y1p, x0p:x1p] = False
        ys, xs = np.nonzero(allowed); changed = 0
        for y, x in zip(ys, xs):
            y0, y1, x0, x1 = max(0, y - 2), min(H, y + 3), max(0, x - 2), min(W, x + 3)
            sub = idx[y0:y1, x0:x1][inner[y0:y1, x0:x1]]
            if sub.size < 3:
                continue
            if Lp[idx[y, x]] > np.median(Lp[sub]) + RIM_LIGHTER:
                idx[y, x] = np.bincount(sub).argmax(); changed += 1
        filled += changed; done |= edge
    return idx, filled


def clean_clip(name, cfg):
    sheet_path = GROUP / cfg['sheet']; approved_path = GROUP / cfg['approved']
    sheet = np.array(Image.open(sheet_path).convert('RGB')); H, W = sheet.shape[:2]
    approved = np.array(Image.open(approved_path).convert('RGBA'))
    pal_imgs = [approved] + [np.array(Image.open(GROUP / p).convert('RGBA')) for p in cfg['extraPalette']]
    palette = np.unique(np.concatenate([a[a[:, :, 3] > 0][:, :3] for a in pal_imgs]), axis=0).astype(float)
    PL = lab(palette)
    xs = [round(i * W / 6) for i in range(7)]; ys = [round(i * H / 6) for i in range(7)]
    cell = lambda i: sheet[ys[i // 6]:ys[i // 6 + 1], xs[i % 6]:xs[i % 6 + 1]]
    fx0, fy0, fx1, fy1 = ink_box(cell(0))
    ax0, ay0, ax1, ay1 = Image.fromarray(approved).getbbox()
    s = (ay1 - ay0) / (fy1 - fy0)
    if cfg['anchor'] == 'feet':
        off = ((ax0 + ax1) / 2 - (fx0 + fx1) / 2 * s, ay1 - fy1 * s)
    else:
        off = ((ax0 + ax1) / 2 - (fx0 + fx1) / 2 * s, (ay0 + ay1) / 2 - (fy0 + fy1) / 2 * s)
    out_dir = GROUP / 'preview' / name / 'clean'; out_dir.mkdir(parents=True, exist_ok=True)
    records, frames = [], []
    for i in range(36):
        c = Image.fromarray(cell(i)); cw, ch = c.size
        big = np.array(c.resize((round(cw * s), round(ch * s)), Image.Resampling.LANCZOS)).astype(float)
        canvas = np.full((256, 384, 3), 255.0)
        ox, oy = round(off[0]), round(off[1])
        y0, x0 = max(0, oy), max(0, ox); y1, x1 = min(256, oy + big.shape[0]), min(384, ox + big.shape[1])
        canvas[y0:y1, x0:x1] = big[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
        rgb = np.clip(canvas, 0, 255)
        op = (765 - rgb.sum(-1)) > BG_DISTANCE * 1.0
        h, sat, val = hsv(rgb)
        flash = op & (h > 35) & (h < 70) & (sat > .35) & (val > .65)
        op &= ~flash
        lab_, n = ndimage.label(op, structure=np.ones((3, 3)))
        sizes = ndimage.sum(op, lab_, range(1, n + 1))
        keep = np.zeros(op.shape, bool); removed = 0
        main = int(np.argmax(sizes)) + 1 if n else 0
        for k in range(1, n + 1):
            m = lab_ == k; area = sizes[k - 1]
            if k == main:
                keep |= m; continue
            mean = rgb[m].mean(0); _, ms, mv = hsv(mean[None])
            dark_or_sat = (mv[0] < .55) or (ms[0] > .25)
            if area >= MIN_PIECE and dark_or_sat and name == 'blue-ink-burst':
                keep |= m          # Blue's floating orb
            else:
                removed += int(area)
        op = keep
        # A bolt drawn leaving the crossbow (Rose, release frames): thin horizontal pixels in front of the crossbow tip.
        trimmed = 0
        if name == 'rose-triple-shot':
            cols = op.sum(0)
            front = np.zeros(op.shape, bool); front[:, :BOLT_TIP_X] = True
            thin = front & op & (cols[None, :] <= 4)
            op = op & ~thin; trimmed = int(thin.sum())
        L = lab(rgb[op]); idx = np.full(op.shape, -1)
        idx[op] = ((L[:, None, :] - PL[None]) ** 2).sum(-1).argmin(1)
        idx = lone_fix(idx, op, len(palette))
        # White-blended rim: edge pixels that snapped to a light palette colour while most of their opaque neighbours are not light.
        light = palette.min(1) >= 150; rim = 0
        for _ in range(2):
            lm = np.zeros(op.shape, bool); lm[op] = light[idx[op]]
            edge = op & ndimage.binary_dilation(~op, structure=np.ones((3, 3)))
            nb_op = ndimage.convolve(op.astype(int), np.ones((3, 3)), mode='constant') - op
            nb_light = ndimage.convolve(lm.astype(int), np.ones((3, 3)), mode='constant') - lm
            drop = edge & lm & (nb_light * 2 < nb_op)
            op = op & ~drop; idx[drop] = -1; rim += int(drop.sum())
        # Remaining white-blended rim (user: 어두운 색으로 하니 아직 가장자리 … 안쪽 색으로 채워): an outline pixel clearly
        # lighter than the body just inside it takes the most common inner colour; light parts whose inside is light stay.
        idx, filled = fill_rim(idx, op, PL[:, 0], cfg.get('rimProtect', ()))
        out = np.zeros((256, 384, 4), np.uint8); out[op, :3] = palette[idx[op]].astype(np.uint8); out[op, 3] = 255
        p = out_dir / f'{i:02}.png'; Image.fromarray(out).save(p); frames.append(Image.fromarray(out))
        records.append({'file': f'clean/{i:02}.png', 'sourceCell': i, 'flashPixels': int(flash.sum()), 'lightRimPixels': rim, 'boltTrimPixels': trimmed, 'rimFilledPixels': filled, 'removedPiecePixels': removed,
                        'bounds': Image.fromarray(out).getbbox(), 'sha256': sha(p)})
    board = Image.new('RGBA', (384 * 6, 256 * 6), (255, 255, 255, 255))
    for i, f in enumerate(frames):
        board.alpha_composite(f, (i % 6 * 384, i // 6 * 256))
    board.save(GROUP / 'preview' / name / 'clean-white.png')
    dark = Image.new('RGBA', board.size, (40, 46, 56, 255)); [dark.alpha_composite(f, (i % 6 * 384, i // 6 * 256)) for i, f in enumerate(frames)]
    dark.save(GROUP / 'preview' / name / 'clean-dark.png')
    manifest = {'format': 'stage2-phase1-clean-v1', 'clip': name, 'status': 'cleaned-candidate-not-approved', 'canvas': [384, 256], 'pivot': [192, 232],
                'sourceSheet': '../../' + cfg['sheet'], 'sourceSheetSha256': sha(sheet_path), 'sourceSize': [W, H],
                'approved': '../../' + cfg['approved'], 'approvedSha256': sha(approved_path), 'paletteColours': len(palette),
                'transform': {'scale': s, 'offset': off, 'anchor': cfg['anchor'], 'frame00InkBox': [int(fx0), int(fy0), int(fx1), int(fy1)], 'resample': 'Lanczos then palette snap'},
                'frames': records}
    (GROUP / 'preview' / name / 'clean.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(name, 'scale %.3f' % s, 'palette', len(palette), 'flash', sum(r['flashPixels'] for r in records), 'removed', sum(r['removedPiecePixels'] for r in records))


def main():
    for name, cfg in CLIPS.items():
        if len(sys.argv) > 1 and name not in sys.argv[1:]:
            continue
        clean_clip(name, cfg)


if __name__ == '__main__':
    main()
