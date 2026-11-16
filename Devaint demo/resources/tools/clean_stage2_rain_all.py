"""Clean every usable ArrowRain Aether cell (01-35) for timing review and later export.

Same processing as the approved Bounce A (clean_stage2_bounce_a): source-resolution
white unmatting with binary alpha and its preservation fixes, cloth-rim refinement,
foot residue cleanup and the exact approved crossbow placed by translation only.
This sheet is drawn larger (crossbow 157 px, body 256 px tall), so the shared
affine is 445x250 at (-29,-9): frame 01 body maps to about [142,54,241,232]
against the approved fused-idle [145,54,240,232].

Mask: the generation drew a mouth (one dark row on the canonical frame). The user
asked to remove only that. One canonical mouthless face is made from frame 12 by
filling that row from the pale rows above and below, and the same face is pasted
into every frame at the hood registration offset, so the mask stays rigid and does
not flicker. Where the hand passes in front of the face (frame 04) hand-coloured
pixels are protected. Source cells and approved inputs are hash-checked.
"""
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import binary_dilation, binary_fill_holes, label

sys.path.insert(0, str(Path(__file__).resolve().parent))
import clean_stage2_bounce_a as bounce  # noqa: E402
from clean_stage2_homing_d import components  # noqa: E402
from refine_stage2_homing_d_edges import refine  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
SOURCE = GROUP / 'source/rain-aether-01'
OUT = GROUP / 'preview/rain-candidates/clean'
FRAMES = list(range(1, 36))
DEST = (445, 250)
OFFSET = (-29, -9)
SX, SY = DEST[0] / 640, DEST[1] / 360
SHIN_SOURCE_ROW = 294  # canvas row 195 on this sheet; keeps the lowered fist on the homing matte
ENCLOSED_RESTORE_BELOW = 228  # this sheet's background noise inside arm gaps reaches 236-247

CANONICAL_FRAME = 12
FACE_BOX = (174, 62, 200, 90)
MOUTH_ROW, MOUTH_X0, MOUTH_X1 = 81, 184, 191
SHIFT = 3
HAND_OVERLAP_MINIMUM = 20


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def lum(a):
    return .3 * a[..., 0] + .59 * a[..., 1] + .11 * a[..., 2]


def on_white(a):
    rgb = a[:, :, :3].astype(int).copy()
    rgb[a[:, :, 3] == 0] = 255
    return rgb


def face_mask(rgb):
    x0, y0, x1, y1 = FACE_BOX
    sub = rgb[y0:y1, x0:x1].astype(float)
    pale = (lum(sub) >= 110) & ~(sub[..., 2] > sub[..., 0] + 25)
    lab, n = label(pale, np.ones((3, 3)))
    out = np.zeros(rgb.shape[:2], bool)
    if n:
        sizes = np.bincount(lab.ravel()); sizes[0] = 0
        out[y0:y1, x0:x1] = binary_fill_holes(lab == sizes.argmax())
    return out


def hood(rgb):
    return (rgb.min(2) < 200) & (rgb[..., 2] > rgb[..., 0] + 20)


def register(rgb, ref_rgb):
    x0, y0, x1, y1 = FACE_BOX
    region = (slice(y0 - 8, y1 + 4), slice(x0 - 8, x1 + 8))
    ref = hood(ref_rgb)[region]
    best = None
    for dy in range(-SHIFT, SHIFT + 1):
        for dx in range(-SHIFT, SHIFT + 1):
            moved = np.roll(np.roll(hood(rgb), -dy, 0), -dx, 1)[region]
            iou = (moved & ref).sum() / max((moved | ref).sum(), 1)
            if best is None or iou > best[0] + 1e-9:
                best = (float(iou), dx, dy)
    return best


def shift(a, dx, dy):
    return np.roll(np.roll(a, dy, 0), dx, 1)


def matte_frame(source):
    raw = Image.open(SOURCE / f'frames/{source:02}.png').convert('RGBA')
    body, bow, interior_changed, restored, holes = bounce.matte_body(raw)
    assert interior_changed == 0 and holes == 0, (source, interior_changed, holes)
    frame = Image.new('RGBA', (384, 256))
    frame.paste(body.resize(DEST, Image.Resampling.NEAREST), OFFSET)
    return np.array(frame), bow, restored


def main():
    if (GROUP / 'runtime/rain-a-x2/motion.json').exists() and json.loads((GROUP / 'runtime/rain-a-x2/motion.json').read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('ArrowRain A-x2 is user-approved from these cleaned frames. Author further edits as a new version.')
    OUT.mkdir(parents=True, exist_ok=True)
    approved_path = GROUP / 'source/approved-fused-idle-original.png'
    inputs = [approved_path, SOURCE / 'aether-original.png'] + [SOURCE / f'frames/{f:02}.png' for f in FRAMES]
    before = {str(p): sha(p) for p in inputs}
    bounce.SKIN_GROW_MIN_SOURCE_Y = SHIN_SOURCE_ROW
    bounce.ENCLOSED_WHITE_MINIMUM = ENCLOSED_RESTORE_BELOW

    approved = np.array(Image.open(approved_path).convert('RGBA'))
    palette = np.unique(approved[approved[:, :, 3] > 0, :3], axis=0).astype(float)
    labels, sizes = components(approved[:, :, 3] > 0)
    bow = approved.copy(); bow[labels == sizes.argmax()] = 0; bow[bow[:, :, 3] == 0] = 0
    bow_im = Image.fromarray(bow); bbox = bow_im.getbbox(); bow_crop = bow_im.crop(bbox)
    expected_bow = np.array(bow_crop)

    mattes = {f: matte_frame(f) for f in FRAMES}
    ref_rgb = on_white(mattes[CANONICAL_FRAME][0])
    canon = ref_rgb.copy()
    for x in range(MOUTH_X0, MOUTH_X1):
        up, dn = canon[MOUTH_ROW - 1, x], canon[MOUTH_ROW + 1, x]
        canon[MOUTH_ROW, x] = (up + dn) // 2 if lum(dn) > 150 else up
    canon_face = face_mask(ref_rgb)
    assert canon_face[MOUTH_ROW, MOUTH_X0:MOUTH_X1].all()

    records, frames = [], []
    for f in FRAMES:
        a, source_bow, restored = mattes[f]
        rgb = on_white(a)
        iou, dx, dy = register(rgb, ref_rgb)
        region = binary_dilation(shift(canon_face, dx, dy), iterations=1)
        own = shift(face_mask(shift(rgb, -dx, -dy)), dx, dy)
        overlap = int((own & ~region & (a[:, :, 3] > 0)).sum())
        write = region & (a[:, :, 3] > 0)
        protected = 0
        if overlap > HAND_OVERLAP_MINIMUM:
            hand = (lum(rgb) > 130) & ((rgb[..., 1] - rgb[..., 2]) < 14) & ~(rgb[..., 2] > rgb[..., 0] + 25)
            protected = int((write & hand).sum())
            write &= ~hand
        src = shift(canon, dx, dy)
        a[write, :3] = src[write]
        frame, stat = refine(Image.fromarray(a), palette)
        foot, foot_deleted, foot_recolored = bounce.clean_feet(np.array(frame))
        assert not (foot_deleted | foot_recolored)[:bounce.LEG_TOP].any()
        frame = Image.fromarray(foot)
        center = ((source_bow[0] + source_bow[2]) / 2 * SX + OFFSET[0], (source_bow[1] + source_bow[3]) / 2 * SY + OFFSET[1])
        pos = (round(center[0] - bow_crop.width / 2), round(center[1] - bow_crop.height / 2))
        frame.alpha_composite(bow_crop, pos)
        px = np.array(frame)
        assert set(np.unique(px[:, :, 3])) <= {0, 255}
        assert not np.any(px[px[:, :, 3] == 0])
        actual = np.array(frame.crop((*pos, pos[0] + bow_crop.width, pos[1] + bow_crop.height)))
        assert np.array_equal(actual[expected_bow[:, :, 3] > 0], expected_bow[expected_bow[:, :, 3] > 0])
        path = OUT / f'{f:02}.png'
        frame.save(path)
        frames.append(frame)
        records.append({'file': f'clean/{f:02}.png', 'sourceFrame': f, 'sourceCellSha256': before[str(SOURCE / f'frames/{f:02}.png')],
                        'sha256': sha(path), 'bounds': frame.getbbox(), 'crossbowPosition': pos, 'sourceCrossbowBounds': source_bow,
                        'sourcePixelsRestored': restored,
                        'face': {'registrationShift': [dx, dy], 'hoodIoU': round(iou, 3), 'pixelsWritten': int(write.sum()),
                                 'handPixelsProtected': protected, 'handOverlap': overlap},
                        'edgeRefinement': stat, 'footResidueDeleted': int(foot_deleted.sum()), 'footResidueRecolored': int(foot_recolored.sum())})

    board = Image.new('RGB', (384 * 6, 280 * 6), '#111111'); draw = ImageDraw.Draw(board)
    for n, frame in enumerate(frames):
        x, y = n % 6 * 384, n // 6 * 280
        board.paste(frame, (x, y + 24), frame); draw.text((x + 12, y + 6), f'source {FRAMES[n]:02}', fill='#dddddd')
    board.save(OUT.parent / 'all-clean-black.png')
    manifest = {
        'format': 'stage2-rain-clean-all-v1', 'status': 'all-cells-cleaned-for-timing-review-not-approved',
        'canvas': [384, 256], 'pivot': [192, 232], 'sourceSheet': '../../../source/rain-aether-01/aether-original.png',
        'sourceSheetSha256': before[str(SOURCE / 'aether-original.png')], 'frames': records,
        'transform': {'destinationSize': list(DEST), 'scale': [SX, SY], 'rasterOffset': list(OFFSET), 'resampling': 'nearest-neighbor',
                      'perFrameAutoFit': False, 'basis': 'body height 256 -> 178 px of approved fused-idle; crossbow centre aligned'},
        'matteProcessing': 'clean_stage2_bounce_a.matte_body (homing maths + preservation fixes), shin restoration from source row 294, enclosed restoration only below min channel 228 (background noise in arm gaps reaches 236-247)',
        'face': {'method': 'canonical mouthless face from source frame 12 (mouth row 81 x184-190 filled from rows 80/82), pasted at hood registration offset in every frame',
                 'userRequest': '입이 생겨버렸어.. 이거만 따로 제거', 'handProtectionWhenOverlap': 'pixels with g-b < 14 kept (hand colour) when the face region overlaps the hand',
                 'maskStyle': 'generated mask style kept to match approved homing D V2 and bounce A; approved fused-idle dotted eye line not pasted'},
        'crossbow': {'method': 'exact approved RGBA crop, translation only', 'cropBounds': list(bbox), 'scale': 1},
        'footCleanup': 'clean_stage2_bounce_a.clean_feet', 'edgeRefinement': 'refine_stage2_homing_d_edges.refine',
        'excluded': {'00': 'input frame drawn at input scale'},
    }
    (OUT.parent / 'clean-all.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    assert all(sha(Path(p)) == d for p, d in before.items())
    print(json.dumps([(r['sourceFrame'], r['face']['registrationShift'], r['face']['handPixelsProtected']) for r in records]))


if __name__ == '__main__':
    main()
