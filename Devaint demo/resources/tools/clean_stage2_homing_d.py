"""Deterministic D extraction: local white unmatting and approved crossbow reuse.

The supplied sheet and approved pixels are immutable inputs. No generative redraw,
per-frame fitting, body warping, or animation timing changes.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import binary_dilation, binary_erosion, label
from scipy.spatial import cKDTree

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
OUT = GROUP / 'runtime/homing-d'
PREVIEW = GROUP / 'preview/homing-cleanup'


def dump(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def components(mask):
    ids, _ = label(mask, np.ones((3, 3)))
    counts = np.bincount(ids.ravel())
    counts[0] = 0
    return ids, counts


def matte_body(raw):
    rgb = np.array(raw.convert('RGB'))
    lo, hi = rgb.min(2), rgb.max(2)
    core = (lo < 225) & ~((lo >= 170) & (hi.astype(int) - lo < 30))
    ids, counts = components(core)
    body = ids == counts.argmax()
    # Body and floating bow are disconnected in the actual source. Extract the
    # body before fitting any colors, so white is never a palette entry.
    interior = binary_erosion(body, iterations=2)
    candidates = binary_dilation(body, iterations=2) & ~interior
    # Include confidently colored thin fingers as local samples too.
    seeds = interior | (body & (hi.astype(int) - lo > 45) & (lo < 150))
    points = np.column_stack(np.nonzero(seeds))
    query = np.column_stack(np.nonzero(candidates))
    _, near = cKDTree(points).query(query, k=24)
    colors = rgb[tuple(points[near].transpose(2, 0, 1))].astype(np.float32)
    observed = rgb[candidates].astype(np.float32)
    rays = 255 - colors
    coverage = np.clip(((255 - observed[:, None]) * rays).sum(2) / np.maximum((rays * rays).sum(2), 1), 0, 1)
    fitted = 255 - coverage[:, :, None] * rays
    error = ((observed[:, None] - fitted) ** 2).sum(2) + 20 * (1 - coverage) ** 2
    choice = error.argmin(1)
    index = np.arange(len(query))
    alpha = coverage[index, choice]
    arr = np.zeros((*rgb.shape[:2], 4), np.uint8)
    arr[interior, :3] = rgb[interior]
    arr[interior, 3] = 255
    arr[candidates, :3] = colors[index, choice].astype(np.uint8)
    arr[candidates, 3] = np.where(alpha >= .55, 255, 0)
    # Discard isolated matte specks, never close real negative spaces or fingers.
    final_ids, final_counts = components(arr[:, :, 3] > 0)
    arr[final_ids != final_counts.argmax()] = 0
    arr[arr[:, :, 3] == 0] = 0
    # Measure source bow separately, solely for integer translation of the
    # already approved bow. Its own uncertain matte is not used in the output.
    bow_id = max((i for i in range(1, len(counts)) if i != counts.argmax()), key=lambda i: counts[i])
    yy, xx = np.nonzero(ids == bow_id)
    bow_bounds = [int(xx.min()), int(yy.min()), int(xx.max()+1), int(yy.max()+1)]
    return Image.fromarray(arr), bow_bounds, int(np.sum(np.any(arr[interior, :3] != rgb[interior], axis=1)))


def main():
    (OUT / 'frames').mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    approved_path = GROUP / 'source/approved-fused-idle-original.png'
    source_path = GROUP / 'source/homing-aether-01/aether-original.png'
    before = {str(p): sha(p) for p in [approved_path, source_path]}
    approved = np.array(Image.open(approved_path).convert('RGBA'))
    labels, sizes = components(approved[:, :, 3] > 0)
    bow = approved.copy()
    bow[labels == sizes.argmax()] = 0
    bow[bow[:, :, 3] == 0] = 0
    bow_im = Image.fromarray(bow)
    bbox = bow_im.getbbox()
    bow_crop = bow_im.crop(bbox)
    bow_crop.save(OUT / 'approved-crossbow.png')
    d = json.loads((GROUP / 'preview/homing-candidates/candidates.json').read_text(encoding='utf-8'))['candidates'][0]
    assert d['id'] == 'D' and d['sourceFrames'] == list(range(4, 22))
    frames, records = [], []
    for n, source in enumerate(d['sourceFrames']):
        raw_path = GROUP / f'source/homing-aether-01/frames/{source:02}.png'
        raw = Image.open(raw_path).convert('RGBA')
        body, source_bow, interior_changes = matte_body(raw)
        assert interior_changes == 0
        frame = Image.new('RGBA', (384, 256))
        frame.paste(body.resize((480, 270), Image.Resampling.NEAREST), (-48, -21))
        center = ((source_bow[0] + source_bow[2]) * .375 - 48, (source_bow[1] + source_bow[3]) * .375 - 21)
        pos = (round(center[0] - bow_crop.width/2), round(center[1] - bow_crop.height/2))
        frame.alpha_composite(bow_crop, pos)
        file = OUT / f'frames/{n:02}.png'
        frame.save(file)
        pixels = np.array(frame)
        assert set(np.unique(pixels[:, :, 3])).issubset({0, 255})
        assert not np.any(pixels[pixels[:, :, 3] == 0])
        # The reused component must be pixel-identical, with no resampling.
        actual = np.array(frame.crop((*pos, pos[0]+bow_crop.width, pos[1]+bow_crop.height)))
        expected = np.array(bow_crop)
        assert np.array_equal(actual[expected[:, :, 3] > 0], expected[expected[:, :, 3] > 0])
        frames.append(frame)
        records.append({'file':f'frames/{n:02}.png','sourceFrame':source,'durationMs':d['durationsMs'][n], 'bounds':frame.getbbox(),'sourceCellSha256':sha(raw_path),'sha256':sha(file),'crossbowPosition':pos,'sourceCrossbowBounds':source_bow})
    sheet = Image.new('RGBA', (384*6, 256*3))
    for n, frame in enumerate(frames):
        sheet.paste(frame, (n%6*384, n//6*256))
    sheet.save(OUT / 'sheet.png')
    frames[0].save(PREVIEW / 'homing-d-clean.webp', save_all=True, append_images=frames[1:], duration=d['durationsMs'], loop=0, lossless=True, background=(0,0,0,0))
    # All eighteen contours on black: no masking a white fringe with a light mat.
    board = Image.new('RGB', (384*6, 280*3), '#111111')
    draw = ImageDraw.Draw(board)
    for n, frame in enumerate(frames):
        x,y = n%6*384,n//6*280
        board.paste(frame,(x,y+24),frame)
        draw.text((x+12,y+6),f'D {n:02} / source {n+4:02}',fill='#dddddd')
    board.save(PREVIEW / 'all-frames-black.png')
    # Compact 2x inspection of full bodies with aspect ratio intact.
    board = Image.new('RGB', (768*3,512*3))
    for row,bg in enumerate(['#ffffff','#808080','#000000']):
        for col,n in enumerate([0,8,9]):
            tile=Image.new('RGBA',(384,256),bg);tile.alpha_composite(frames[n])
            board.paste(tile.resize((768,512),Image.Resampling.NEAREST),(col*768,row*512))
    board.save(PREVIEW / 'edges-three-backgrounds.png')
    manifest = {'format':'stage2-homing-d-clean-v1','status':'D-timing-user-selected-cleanup-pending-visual-review-not-unity-integrated','canvas':[384,256],'pivot':[192,232],'repeat':False,'holdLastFrame':True,'totalMs':sum(d['durationsMs']),'projectileCueMs':1000,'sourceFrames':d['sourceFrames'],'durationsMs':d['durationsMs'],'sourceSheet':'../../source/homing-aether-01/aether-original.png','sourceSheetSha256':before[str(source_path)],'frames':records,'matteProcessing':{'method':'source-resolution local white-compositing inversion before nearest-neighbor resize','whiteMinimum':225,'neutralMinimum':170,'neutralChromaMaximum':30,'localSeedCount':24,'coverageThreshold':.55,'alphaValues':[0,255],'transparentRgb':[0,0,0],'interiorSourceColorsChanged':False,'perFrameAutoFit':False,'scale':.75,'rasterOffset':[-48,-21],'paletteQuantization':False},'crossbow':{'method':'exact approved RGBA crop, translation only','file':'approved-crossbow.png','approvedSource':'../../source/approved-fused-idle-original.png','approvedSourceSha256':before[str(approved_path)],'cropBounds':bbox,'scale':1,'placement':'source bow bounding-box center transformed by shared affine; rounded to integer pixel','pixelIdentityCheckedEveryFrame':True},'identity':{'newLimbs':False,'maskRedrawn':False,'generativeRedraw':False},'unityIntegrated':False}
    dump(OUT / 'motion.json',manifest)
    assert all(sha(Path(p)) == digest for p,digest in before.items())
    handoff_path=GROUP/'source/handoff.json'
    handoff=json.loads(handoff_path.read_text(encoding='utf-8'))
    handoff.update(status=manifest['status'],selectedCandidate='D',selectedSourceFrames=d['sourceFrames'],playbackSequence=list(range(18)),perFrameDurationsMs=d['durationsMs'],cleanedMotion='runtime/homing-d/motion.json',cleanupPreview='preview/homing-cleanup/index.html')
    handoff['futureMatteProcessing']={'status':'applied-to-D-see-runtime-manifest','manifest':'runtime/homing-d/motion.json'}
    dump(handoff_path,handoff)
    print(json.dumps({'frames':len(frames),'durationMs':manifest['totalMs'],'crossbowCrop':bbox,'positions':[r['crossbowPosition'] for r in records],'sourceHashesUnchanged':True,'alpha':'binary / zero transparent RGB','interiorPixelsUnchanged':True}))


if __name__ == '__main__':
    main()
