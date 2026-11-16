"""Clean the user-selected Bounce attempt 2 / timing A.

Reuses the approved homing D V2 processing: source-resolution white
unmatting with binary alpha (clean_stage2_homing_d.matte_body maths), cloth-rim-only
refinement (refine_stage2_homing_d_edges.refine) and the exact approved crossbow
crop placed by integer translation. Only the shared affine differs, because this
sheet is drawn at about 96% of the approved size. No generative redraw, no
per-frame fitting, no timing change. The matte adds two preservation fixes
(see matte_body) needed because this sheet is drawn smaller than homing. Source cells and approved inputs are
hash-checked before and after.
"""
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from scipy.ndimage import binary_dilation, binary_erosion, binary_fill_holes, distance_transform_edt, label
from scipy.spatial import cKDTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
from clean_stage2_homing_d import components  # noqa: E402
from refine_stage2_homing_d_edges import refine  # noqa: E402

ENCLOSED_WHITE_MINIMUM = 240
BODY_PIECE_MINIMUM = 30
SKIN_WHITE_MINIMUM = 245
SKIN_GROW_STEPS = 6
SKIN_GROW_MIN_SOURCE_Y = 235  # shins only; hands keep the homing matte
DARK_EDGE_MAXIMUM = 110
# Motion streak lines in the two sleeve-smear in-betweens (source 11 and 22).
# Only this canvas region left of the body is touched; skin is protected.
STREAK_FRAMES = {11, 22}
STREAK_REGION = (0, 85, 165, 160)  # x0, y0, x1, y1 on the 384x256 canvas
STREAK_OPENING = 4


FOOT_TOP = 214
LOWER_BOOT_TOP = 224
SOLE_MAXIMUM = 40
BOOT_ZONE = 3
LEG_TOP = 195
PALE_EDGE_MINIMUM = 200
PALE_INNER_MINIMUM = 205
BLUISH_REACH = 6
SPECK_MAXIMUM = 8
SHIN_OVERLAP = 3

def clean_feet(a):
    """Remove matte residue around boots and pale blends on leg edges (canvas rows >= 195 only).

    Boots = brown pieces below row 214. Within 3 px of them, grey, light grey and
    (in the lower boot half) bluish pixels are residue: deleted on the silhouette
    edge, otherwise recoloured from the nearest boot pixel. Brown, the near-black
    sole (max < 40), the shin and the robe hem above row 224 are kept. Near-white
    blends on leg edges (min >= 200) are deleted, inner ones (min >= 205) take the
    nearest normal leg colour. Specks of <= 8 px left in the leg area are removed.
    """
    a = a.copy()
    rgb = a[:, :, :3].astype(int); r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    m = a[:, :, 3] > 0
    mx = rgb.max(2)
    rows = np.arange(a.shape[0])[:, None]
    skin = (r > b * 1.08) & (g > b * 1.03) & (mx > 140)
    brown = m & (r >= g) & (g >= b - 4) & (r - b >= 12) & (mx < 150) & ~skin
    lab, k = label(brown & (rows >= FOOT_TOP), np.ones((3, 3)))
    sizes = np.bincount(lab.ravel()); sizes[0] = 0
    boots = np.isin(lab, np.nonzero(sizes >= 40)[0])
    zone = binary_dilation(boots, iterations=BOOT_ZONE) & (rows >= FOOT_TOP) & m
    # Shin skin is protected only down to just below each boot's top edge.
    shin = np.zeros_like(m)
    for i in np.nonzero(sizes >= 40)[0]:
        ys, xs = np.nonzero(lab == i)
        shin[:ys.min() + SHIN_OVERLAP, xs.min() - BOOT_ZONE:xs.max() + BOOT_ZONE + 1] = True
    skin = skin & shin
    bluish = (b > r + 8) & (rows >= LOWER_BOOT_TOP)
    sole = (mx < SOLE_MAXIMUM) & ~bluish
    robe = (b > r + 20) & (b > 60) & (rows < LOWER_BOOT_TOP)
    residue = zone & ~brown & ~sole & ~skin & ~robe
    residue |= bluish & m & binary_dilation(boots, iterations=BLUISH_REACH)
    edge = m & ~binary_erosion(m, np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]]))
    delete = residue & edge
    recolor = residue & ~edge
    if recolor.any():
        _, idx = distance_transform_edt(~boots, return_indices=True)
        a[recolor, :3] = a[idx[0][recolor], idx[1][recolor], :3]
    # Near-white blends on shin and boot-top edges (legs only, never hands).
    lo = rgb.min(2)
    leg = m & (rows >= LEG_TOP)
    pale_delete = leg & edge & (lo >= PALE_EDGE_MINIMUM)
    pale_recolor = leg & ~edge & (lo >= PALE_INNER_MINIMUM)
    if pale_recolor.any():
        good = m & (lo < PALE_INNER_MINIMUM) & ~pale_delete
        _, idx = distance_transform_edt(~good, return_indices=True)
        a[pale_recolor, :3] = a[idx[0][pale_recolor], idx[1][pale_recolor], :3]
    delete = delete | pale_delete
    recolor = recolor | pale_recolor
    a[delete] = 0
    # Bluish heel residue exposed by the first pass, then leftover specks.
    for _ in range(3):
        mm = a[:, :, 3] > 0
        edge2 = mm & ~binary_erosion(mm, np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]]))
        more = residue & edge2
        if not more.any():
            break
        a[more] = 0
        delete |= more
    mm = a[:, :, 3] > 0
    lab2, _ = label(mm, np.ones((3, 3)))
    sz = np.bincount(lab2.ravel()); sz[0] = 0
    speck = mm & (rows >= LEG_TOP) & np.isin(lab2, np.nonzero((sz > 0) & (sz <= SPECK_MAXIMUM))[0])
    a[speck] = 0
    delete |= speck
    return a, delete, recolor


def remove_streaks(frame):
    from scipy.ndimage import binary_opening, label
    a = np.array(frame)
    m = a[:, :, 3] > 0
    rgb = a[:, :, :3].astype(float)
    skin = (rgb[:, :, 0] > rgb[:, :, 2] * 1.08) & (rgb[:, :, 1] > rgb[:, :, 2] * 1.03) & m
    region = np.zeros_like(m)
    x0, y0, x1, y1 = STREAK_REGION
    region[y0:y1, x0:x1] = True
    thin = m & ~binary_opening(m, np.ones((STREAK_OPENING, STREAK_OPENING))) & ~binary_dilation(skin, iterations=2) & region
    lab, k = label(thin, np.ones((3, 3)))
    remove = np.zeros_like(m)
    for i in range(1, k + 1):
        ys, xs = np.nonzero(lab == i)
        if len(xs) >= 4 and (xs.max() - xs.min() >= 4 or ys.max() - ys.min() >= 4):
            remove |= lab == i
    keep = m & ~remove
    lab, _ = components(keep)
    body_id = np.bincount(lab[keep & ~region].ravel()).argmax()
    orphan = keep & (lab != body_id) & region
    remove |= orphan
    a[remove] = 0
    assert not np.any(remove & skin)
    return Image.fromarray(a), int(remove.sum())


def matte_body(raw):
    """clean_stage2_homing_d.matte_body with two preservation fixes for this smaller sheet.

    1. Body = every core piece of >=30 px except the topmost (the floating bow),
       and keep every final component containing that body's interior. The
       homing version kept only the largest piece, which dropped whole boots
       (source 01, 04, 32) when the pale shin broke the connection.
    1b. Skin-hued, non-white source pixels (min channel < 245) touching the body
       below source row 235 are restored to exact source colour, up to 6 px: lower shins were cut
       from the boots otherwise.
    1c. Opaque edge pixels darker than max channel 110 keep their exact source
       colour (a white blend cannot be that dark): the 1 px boot sole line was
       repainted brown otherwise.
    2. Pixels enclosed by the body whose source colour is not near-white (min
       channel < 240) are restored to their exact source colour, opaque. The
       coverage threshold otherwise punched holes in the mask eye area and
       between the shins. Enclosed near-white gaps stay transparent.
    Matte maths, thresholds and seeds are otherwise unchanged.
    """
    rgb = np.array(raw.convert('RGB'))
    lo, hi = rgb.min(2), rgb.max(2)
    core = (lo < 225) & ~((lo >= 170) & (hi.astype(int) - lo < 30))
    ids, counts = components(core)
    large = [i for i in range(1, len(counts)) if counts[i] >= BODY_PIECE_MINIMUM]
    tops = {i: np.nonzero(ids == i)[0].min() for i in large}
    bow_id = min(large, key=lambda i: tops[i])
    # Every sizeable piece except the floating bow is body: the pale shin can
    # split a boot from the robe in the core mask.
    body = np.isin(ids, [i for i in large if i != bow_id])
    interior = binary_erosion(body, iterations=2)
    candidates = binary_dilation(body, iterations=2) & ~interior
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
    dark = candidates & (hi < DARK_EDGE_MAXIMUM) & (arr[:, :, 3] > 0)
    arr[dark, :3] = rgb[dark]
    dark_kept = int(dark.sum())
    skin_like = (rgb[:, :, 0] > rgb[:, :, 2] * 1.08) & (rgb[:, :, 1] > rgb[:, :, 2] * 1.03) & (lo < SKIN_WHITE_MINIMUM)
    skin_like[:SKIN_GROW_MIN_SOURCE_Y] = False
    grown = 0
    for _ in range(SKIN_GROW_STEPS):
        add = skin_like & (arr[:, :, 3] == 0) & binary_dilation(arr[:, :, 3] > 0, np.ones((3, 3)))
        if not add.any():
            break
        arr[add, :3] = rgb[add]
        arr[add, 3] = 255
        grown += int(add.sum())
    enclosed = binary_fill_holes(arr[:, :, 3] > 0) & (arr[:, :, 3] == 0) & (lo < ENCLOSED_WHITE_MINIMUM)
    arr[enclosed, :3] = rgb[enclosed]
    arr[enclosed, 3] = 255
    final_ids, _ = components(arr[:, :, 3] > 0)
    keep = np.isin(final_ids, np.unique(final_ids[interior & (final_ids > 0)]))
    arr[~keep] = 0
    arr[arr[:, :, 3] == 0] = 0
    yy, xx = np.nonzero(ids == bow_id)
    bow_bounds = [int(xx.min()), int(yy.min()), int(xx.max() + 1), int(yy.max() + 1)]
    interior_changed = int(np.sum(np.any(arr[interior, :3] != rgb[interior], axis=1)))
    holes_left = int((binary_fill_holes(arr[:, :, 3] > 0) & (arr[:, :, 3] == 0) & (lo < ENCLOSED_WHITE_MINIMUM)).sum())
    return Image.fromarray(arr), bow_bounds, interior_changed, int(enclosed.sum()) + grown + dark_kept, holes_left

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
SOURCE = GROUP / 'source/bounce-aether-02'
CANDIDATES = GROUP / 'preview/bounce-v3-candidates/candidates.json'
OUT = GROUP / 'runtime/bounce-a'
PREVIEW = GROUP / 'preview/bounce-cleanup'
DEST = (666, 374)
OFFSET = (-141, -59)
SX, SY = DEST[0] / 640, DEST[1] / 360


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dump(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    manifest_path = OUT / 'motion.json'
    if manifest_path.exists() and json.loads(manifest_path.read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('This cleanup is user-approved. Author further edits as a new version.')
    (OUT / 'frames').mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    approved_path = GROUP / 'source/approved-fused-idle-original.png'
    config = json.loads(CANDIDATES.read_text(encoding='utf-8'))
    a = next(c for c in config['candidates'] if c['id'] == 'A')
    inputs = [approved_path, SOURCE / 'aether-original.png'] + [SOURCE / f'frames/{f:02}.png' for f in a['sourceFrames']]
    before = {str(p): sha(p) for p in inputs}

    approved = np.array(Image.open(approved_path).convert('RGBA'))
    palette = np.unique(approved[approved[:, :, 3] > 0, :3], axis=0).astype(float)
    labels, sizes = components(approved[:, :, 3] > 0)
    bow = approved.copy()
    bow[labels == sizes.argmax()] = 0
    bow[bow[:, :, 3] == 0] = 0
    bow_im = Image.fromarray(bow)
    bbox = bow_im.getbbox()
    bow_crop = bow_im.crop(bbox)
    bow_crop.save(OUT / 'approved-crossbow.png')
    expected_bow = np.array(bow_crop)

    frames, records, stats = [], [], []
    for n, source in enumerate(a['sourceFrames']):
        raw_path = SOURCE / f'frames/{source:02}.png'
        body, source_bow, interior_changes, restored, holes_left = matte_body(Image.open(raw_path).convert('RGBA'))
        assert interior_changes == 0 and holes_left == 0, (source, interior_changes, holes_left)
        frame = Image.new('RGBA', (384, 256))
        frame.paste(body.resize(DEST, Image.Resampling.NEAREST), OFFSET)
        frame, stat = refine(frame, palette)
        streak_pixels = 0
        if source in STREAK_FRAMES:
            frame, streak_pixels = remove_streaks(frame)
        stat['removedStreakPixels'] = streak_pixels
        foot, foot_deleted, foot_recolored = clean_feet(np.array(frame))
        assert not (foot_deleted | foot_recolored)[:LEG_TOP].any()
        frame = Image.fromarray(foot)
        stat['footResidueDeleted'] = int(foot_deleted.sum())
        stat['footResidueRecolored'] = int(foot_recolored.sum())
        center = ((source_bow[0] + source_bow[2]) / 2 * SX + OFFSET[0], (source_bow[1] + source_bow[3]) / 2 * SY + OFFSET[1])
        pos = (round(center[0] - bow_crop.width / 2), round(center[1] - bow_crop.height / 2))
        frame.alpha_composite(bow_crop, pos)
        pixels = np.array(frame)
        assert set(np.unique(pixels[:, :, 3])).issubset({0, 255})
        assert not np.any(pixels[pixels[:, :, 3] == 0])
        actual = np.array(frame.crop((*pos, pos[0] + bow_crop.width, pos[1] + bow_crop.height)))
        assert np.array_equal(actual[expected_bow[:, :, 3] > 0], expected_bow[expected_bow[:, :, 3] > 0])
        file = OUT / f'frames/{n:02}.png'
        frame.save(file)
        frames.append(frame)
        stats.append(stat)
        records.append({'file': f'frames/{n:02}.png', 'sourceFrame': source, 'role': a['roles'][n], 'durationMs': a['durationsMs'][n],
                        'bounds': frame.getbbox(), 'sourceCellSha256': before[str(raw_path)], 'sha256': sha(file),
                        'crossbowPosition': pos, 'sourceCrossbowBounds': source_bow, 'sourcePixelsRestored': restored, 'edgeRefinement': stat})

    sheet = Image.new('RGBA', (384 * 6, 256 * 6))
    for n, frame in enumerate(frames):
        sheet.paste(frame, (n % 6 * 384, n // 6 * 256))
    sheet.save(OUT / 'sheet.png')
    frames[0].save(PREVIEW / 'bounce-a-clean.webp', save_all=True, append_images=frames[1:], duration=a['durationsMs'], loop=0, lossless=True, background=(0, 0, 0, 0))
    board = Image.new('RGB', (384 * 6, 280 * 6), '#111111')
    draw = ImageDraw.Draw(board)
    for n, frame in enumerate(frames):
        x, y = n % 6 * 384, n // 6 * 280
        board.paste(frame, (x, y + 24), frame)
        draw.text((x + 12, y + 6), f'A {n:02} / source {a["sourceFrames"][n]:02} / {a["roles"][n]}', fill='#dddddd')
    board.save(PREVIEW / 'all-frames-black.png')
    picks = [a['sourceFrames'].index(f) for f in (4, 6, 10, 12, 23, 28)]
    board = Image.new('RGB', (768 * 6, 512 * 3))
    for row, bg in enumerate(['#ffffff', '#808080', '#000000']):
        for col, n in enumerate(picks):
            tile = Image.new('RGBA', (384, 256), bg)
            tile.alpha_composite(frames[n])
            board.paste(tile.resize((768, 512), Image.Resampling.NEAREST), (col * 768, row * 512))
    board.save(PREVIEW / 'edges-three-backgrounds.png')

    manifest = {
        'format': 'stage2-bounce-a-clean-v1', 'status': 'A-timing-user-selected-cleanup-pending-visual-review-not-unity-integrated',
        'pattern': 'Bounce', 'canvas': [384, 256], 'pivot': [192, 232], 'repeat': False, 'holdLastFrame': True,
        'totalMs': sum(a['durationsMs']), 'releaseCuesMs': a['releaseCuesMs'], 'peakFrames': config['peakFrames'],
        'visualDirections': config['visualDirections'], 'sourceFrames': a['sourceFrames'], 'durationsMs': a['durationsMs'],
        'sourceSheet': '../../source/bounce-aether-02/aether-original.png', 'sourceSheetSha256': before[str(SOURCE / 'aether-original.png')],
        'prompt': '../../source/bounce-aether/prompt.txt', 'timingCandidates': '../../preview/bounce-v3-candidates/candidates.json',
        'frames': records,
        'matteProcessing': {'method': 'homing D matte (source-resolution local white-compositing inversion, binary alpha, same thresholds) before nearest-neighbor resize, plus preservation fixes: body = all core pieces >=30 px except the bow, keeping every component containing its interior (boots in source 01/04/32 were dropped otherwise); skin-hued non-white source pixels touching the body below source row 235 restored to exact source colour up to 6 px (lower shins only; hands keep the homing matte); opaque edge pixels with max channel < 110 keep exact source colour (boot sole line); and restore enclosed non-white pixels (min channel < 240) to exact source colour (mask eye area, between shins)',
                            'alphaValues': [0, 255], 'transparentRgb': [0, 0, 0], 'interiorSourceColorsChanged': False, 'perFrameAutoFit': False,
                            'destinationSize': list(DEST), 'scale': [SX, SY], 'rasterOffset': list(OFFSET), 'paletteQuantization': False},
        'edgeRefinement': {'method': 'refine_stage2_homing_d_edges.refine unchanged: cloth rim 1-2 px to local approved palette, isolated cloth spurs removed',
                           'maskHandShinsBootsProtected': True, 'crossbowPixelsUnchanged': True, 'timingUnchanged': True,
                           'streakRemoval': {'sourceFrames': sorted(STREAK_FRAMES), 'canvasRegion': list(STREAK_REGION),
                                             'method': f'remove pixels lost to a {STREAK_OPENING}x{STREAK_OPENING} opening (components >=4 px spanning >=4 px, skin protected) plus pieces left disconnected from the body inside the region',
                                             'reason': 'Aether motion-streak lines beside the swinging sleeve; the sleeve swoosh itself is kept as a 50 ms in-between'},
                           'footCleanup': {'scope': 'canvas rows >= 195 only', 'method': 'clean_feet: grey / light-grey / lower-boot bluish residue within 3 px of the boots deleted on the edge or recoloured from the nearest boot pixel; near-white leg edge blends deleted; leg specks <= 8 px removed. Brown, near-black sole, shins and robe hem kept.', 'reason': 'user review: 발 부분 약간 덜 제거 됐다'}},
        'crossbow': {'method': 'exact approved RGBA crop, translation only', 'file': 'approved-crossbow.png', 'approvedSource': '../../source/approved-fused-idle-original.png',
                     'approvedSourceSha256': before[str(approved_path)], 'cropBounds': list(bbox), 'scale': 1,
                     'placement': 'source bow bounding-box center through the shared affine; rounded to integer pixel', 'pixelIdentityCheckedEveryFrame': True},
        'identity': {'newLimbs': False, 'maskRedrawn': False, 'generativeRedraw': False},
        'unityIntegrated': False,
    }
    dump(manifest_path, manifest)
    assert all(sha(Path(p)) == digest for p, digest in before.items())
    print(json.dumps({'frames': len(frames), 'totalMs': manifest['totalMs'], 'crossbowY': sorted({r['crossbowPosition'][1] for r in records}),
                      'changedRim': [s['changedPixels'] for s in stats], 'spurs': sum(s['removedSpurPixels'] for s in stats)}))


if __name__ == '__main__':
    main()
