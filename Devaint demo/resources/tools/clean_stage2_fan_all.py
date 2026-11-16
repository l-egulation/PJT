"""Clean every usable ExpandingFan Aether cell (01-31) of the v4 output for timing review and export.

Same processing as the approved Bounce A: source-resolution white unmatting with binary alpha and its
preservation fixes, cloth-rim refinement, foot residue cleanup and the exact approved crossbow by
translation only. This sheet is drawn at the input size like Bounce (frame 01 body 92x171 at feet row
280), so the shared affine is 666x374 at (-141,-59). The generated mask has no mouth, so it is kept as
drawn. Frames 32-35 return to idle and are excluded. Source cells and approved inputs are hash-checked.
"""
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import convolve, label, binary_closing, binary_dilation, binary_fill_holes, binary_opening, distance_transform_edt

sys.path.insert(0, str(Path(__file__).resolve().parent))
import clean_stage2_bounce_a as bounce  # noqa: E402
from clean_stage2_homing_d import components  # noqa: E402
from refine_stage2_homing_d_edges import refine  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
SOURCE = GROUP / 'source/fan-aether-03'
OUT = GROUP / 'preview/fan-candidates/clean'
FRAMES = list(range(1, 32))
DEST = (666, 374)
OFFSET = (-141, -59)
SX, SY = DEST[0] / 640, DEST[1] / 360
APPROVED_RUNTIME = GROUP / 'runtime/fan-final/motion.json'
BOW_BAND_BOTTOM = 64
LEG_SPECK_MAXIMUM = 12  # a detached 9 px toe-sole fragment on the left boot in 7 frames
ENCLOSED_RESTORE_BELOW = 228  # background noise inside arm gaps reaches 239-240 on this sheet (as on the rain sheet)

HAND_SCALE = 0.7
WRIST_RADIUS = 12
WRIST_CLOSING = 3
HAND_FRAMES = set(range(25, 32))

K8 = np.ones((3, 3), int); K8[1, 1] = 0
K4 = np.array([[0, 1, 0], [1, 0, 1], [0, 1, 0]])
LEG_TOP = 195
LEG_PIECE_MAX = 10


def skin_mask(rgb, m):
    return m & (rgb[..., 0] > rgb[..., 2] * 1.08) & (rgb[..., 1] > rgb[..., 2] * 1.03) & (rgb.max(2) > 150)


def polish(a, hand_rows=180):
    """Edge polish (user review: \"약간 외곽 좀 덜 정리 됐다\"), before the crossbow is composited.

    Removes unsupported 1 px spurs anywhere except the hand, pale bandage bumps and dark (non-bluish) sole
    hooks in the leg zone that a 3x3 opening shaves off in pieces of <= 10 px, then fills 1 px notches from
    the nearest opaque colour. Cloth tips are not touched by the leg rule (bluish colours are excluded).
    """
    a = a.copy()
    removed = np.zeros(a.shape[:2], bool); filled = np.zeros(a.shape[:2], bool)
    rows = np.arange(a.shape[0])[:, None]
    for _ in range(2):
        m = a[:, :, 3] > 0; rgb = a[:, :, :3].astype(int)
        n8 = convolve(m.astype(int), K8, mode='constant')
        hand = skin_mask(rgb, m) & (rows < hand_rows)
        spur = m & (n8 <= 2) & ~hand
        legpale = m & (rows >= LEG_TOP) & (rgb.min(2) >= 165) & (n8 <= 3)
        kill = spur | legpale
        a[kill] = 0; removed |= kill
    # Leg zone: thin pale bandage bumps and dark sole hooks that a 3x3 opening shaves off (small pieces only).
    m = a[:, :, 3] > 0; rgb = a[:, :, :3].astype(int)
    shaved = m & ~binary_opening(m, np.ones((3, 3))) & (rows >= LEG_TOP) & ((rgb.min(2) >= 150) | ((rgb.max(2) < 90) & (rgb[..., 2] <= rgb[..., 0] + 10)))
    lab, k = label(shaved, np.ones((3, 3)))
    if k:
        sz = np.bincount(lab.ravel()); sz[0] = 0
        small = np.isin(lab, np.nonzero((sz > 0) & (sz <= LEG_PIECE_MAX))[0])
        a[small] = 0; removed |= small
    m = a[:, :, 3] > 0
    n4 = convolve(m.astype(int), K4, mode='constant'); n8 = convolve(m.astype(int), K8, mode='constant')
    notch = ~m & ((n4 == 4) | (n8 >= 7))
    if notch.any():
        _, idx = distance_transform_edt(~m, return_indices=True)
        a[notch, :3] = a[idx[0][notch], idx[1][notch], :3]; a[notch, 3] = 255; filled |= notch
    m = a[:, :, 3] > 0
    lone = m & (convolve(m.astype(int), K8, mode='constant') == 0)
    a[lone] = 0; removed |= lone
    return a, removed, filled


def hand_component(a, x_max=176, row_max=175, row_min=60):
    rgb = a[:, :, :3].astype(int); m = a[:, :, 3] > 0
    sk = skin_mask(rgb, m)
    sk[:row_min] = False; sk[row_max:] = False; sk[:, x_max:] = False
    lab, k = label(sk, np.ones((3, 3)))
    if k == 0:
        return None
    sz = np.bincount(lab.ravel()); sz[0] = 0
    hand = binary_fill_holes(lab == sz.argmax())
    # Include the hand's own darker creases and outline (not the blue/violet cloth) so they scale with it.
    cloth = rgb[..., 2] > rgb[..., 0] + 10
    ring = binary_dilation(hand, np.ones((3, 3))) & m & ~cloth & ~hand
    return binary_fill_holes(hand | ring)


def shrink_hand(a, s):
    """Scale the open hand (user: \"손 크기 줄이기 가넝? 마지막쯤에?\") about the wrist, i.e. the mean of the hand
    pixels touching the sleeve. Nearest-neighbour inverse mapping of the original hand pixels; vacated pixels
    inside small concavities of the sleeve/body (3x3 closing x2) take the nearest body colour, the rest
    become transparent. The hand is drawn in front of the sleeve."""
    a = a.copy()
    H = hand_component(a)
    m = a[:, :, 3] > 0
    body = m & ~H
    touch = H & (convolve(body.astype(int), K4, mode='constant') > 0)
    ys, xs = np.nonzero(touch)
    ax, ay = xs.mean(), ys.mean()
    hy, hx = np.nonzero(H)
    src = a.copy()
    # vacate
    closed = binary_closing(body, np.ones((3, 3)), iterations=2)
    vac = H.copy()
    refill = vac & closed
    a[vac] = 0
    if refill.any():
        _, idx = distance_transform_edt(~body, return_indices=True)
        a[refill, :3] = src[idx[0][refill], idx[1][refill], :3]; a[refill, 3] = 255
    # paste scaled hand by inverse mapping (nearest)
    x0, x1, y0, y1 = hx.min(), hx.max(), hy.min(), hy.max()
    nx0 = int(np.floor(ax + (x0 - ax) * s)) - 1; nx1 = int(np.ceil(ax + (x1 - ax) * s)) + 1
    ny0 = int(np.floor(ay + (y0 - ay) * s)) - 1; ny1 = int(np.ceil(ay + (y1 - ay) * s)) + 1
    placed = np.zeros(a.shape[:2], bool)
    for y in range(ny0, ny1 + 1):
        for x in range(nx0, nx1 + 1):
            sx = int(round(ax + (x - ax) / s)); sy = int(round(ay + (y - ay) / s))
            if 0 <= sy < a.shape[0] and 0 <= sx < a.shape[1] and H[sy, sx]:
                a[y, x] = src[sy, sx]; placed[y, x] = True
    # Wrist bridge (user: "손부분이랑 옷 소매 부분 사이가 비어서 그 부분 보강해줘"): near the wrist, close the gap
    # between the shrunk hand and the sleeve and fill it with the nearest hand (wrist skin) colour.
    m2 = a[:, :, 3] > 0
    yy, xx = np.mgrid[:a.shape[0], :a.shape[1]]
    near = (xx - ax) ** 2 + (yy - ay) ** 2 <= WRIST_RADIUS ** 2
    joined = binary_closing(placed | (m2 & ~placed), np.ones((3, 3)), iterations=WRIST_CLOSING)
    bridge = joined & ~m2 & near & binary_dilation(placed, np.ones((3, 3)), iterations=WRIST_CLOSING) & binary_dilation(m2 & ~placed, np.ones((3, 3)), iterations=WRIST_CLOSING)
    if bridge.any():
        _, idx = distance_transform_edt(~placed, return_indices=True)
        a[bridge, :3] = a[idx[0][bridge], idx[1][bridge], :3]; a[bridge, 3] = 255
    return a, dict(anchor=(round(float(ax), 1), round(float(ay), 1)), handPixels=int(H.sum()), placed=int(placed.sum()), refilled=int(refill.sum()), wristBridge=int(bridge.sum()))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    if APPROVED_RUNTIME.exists() and json.loads(APPROVED_RUNTIME.read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('ExpandingFan is user-approved from these cleaned frames. Author further edits as a new version.')
    OUT.mkdir(parents=True, exist_ok=True)
    bounce.ENCLOSED_WHITE_MINIMUM = ENCLOSED_RESTORE_BELOW
    bounce.SPECK_MAXIMUM = LEG_SPECK_MAXIMUM
    approved_path = GROUP / 'source/approved-fused-idle-original.png'
    inputs = [approved_path, SOURCE / 'aether-original.png'] + [SOURCE / f'frames/{f:02}.png' for f in FRAMES]
    before = {str(p): sha(p) for p in inputs}

    approved = np.array(Image.open(approved_path).convert('RGBA'))
    palette = np.unique(approved[approved[:, :, 3] > 0, :3], axis=0).astype(float)
    labels, sizes = components(approved[:, :, 3] > 0)
    bow = approved.copy(); bow[labels == sizes.argmax()] = 0; bow[bow[:, :, 3] == 0] = 0
    bow_im = Image.fromarray(bow); bbox = bow_im.getbbox(); bow_crop = bow_im.crop(bbox)
    expected_bow = np.array(bow_crop)

    records, frames = [], []
    for f in FRAMES:
        raw = Image.open(SOURCE / f'frames/{f:02}.png').convert('RGBA')
        body, source_bow, interior_changed, restored, holes = bounce.matte_body(raw)
        assert interior_changed == 0 and holes == 0, (f, interior_changed, holes)
        frame = Image.new('RGBA', (384, 256))
        frame.paste(body.resize(DEST, Image.Resampling.NEAREST), OFFSET)
        frame, stat = refine(frame, palette)
        foot, foot_deleted, foot_recolored = bounce.clean_feet(np.array(frame))
        assert not (foot_deleted | foot_recolored)[:bounce.LEG_TOP].any()
        # Generated crossbow sparkle dots survive the matte as separate small pieces; the approved bow
        # (with its own dots) replaces them, so drop pieces detached from the body that sit in the bow band.
        lab, _ = components(foot[:, :, 3] > 0)
        sizes_f = np.bincount(lab.ravel()); sizes_f[0] = 0
        body_id = sizes_f.argmax()
        stray = np.zeros(foot.shape[:2], bool)
        for i in np.nonzero(sizes_f)[0]:
            if i == body_id:
                continue
            ys = np.nonzero(lab == i)[0]
            if ys.max() < BOW_BAND_BOTTOM:
                stray |= lab == i
        foot[stray] = 0
        hand_info = None
        if f in HAND_FRAMES:
            foot, hand_info = shrink_hand(foot, HAND_SCALE)
        foot, polished_removed, polished_filled = polish(foot)
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
                        'sourcePixelsRestored': restored, 'edgeRefinement': stat,
                        'strayBowBandPixelsRemoved': int(stray.sum()), 'handShrink': hand_info, 'edgePolish': {'removed': int(polished_removed.sum()), 'filled': int(polished_filled.sum())}, 'footResidueDeleted': int(foot_deleted.sum()), 'footResidueRecolored': int(foot_recolored.sum())})

    board = Image.new('RGB', (384 * 6, 280 * 6), '#111111'); draw = ImageDraw.Draw(board)
    for n, frame in enumerate(frames):
        x, y = n % 6 * 384, n // 6 * 280
        board.paste(frame, (x, y + 24), frame); draw.text((x + 12, y + 6), f'source {FRAMES[n]:02}', fill='#dddddd')
    board.save(OUT.parent / 'all-clean-black.png')
    manifest = {
        'format': 'stage2-fan-clean-all-v1', 'status': 'all-cells-cleaned-for-timing-review-not-approved',
        'canvas': [384, 256], 'pivot': [192, 232], 'sourceSheet': '../../../source/fan-aether-03/aether-original.png',
        'sourceSheetSha256': before[str(SOURCE / 'aether-original.png')], 'prompt': '../../../source/fan-aether/prompt.txt (v4)',
        'frames': records,
        'transform': {'destinationSize': list(DEST), 'scale': [SX, SY], 'rasterOffset': list(OFFSET), 'resampling': 'nearest-neighbor',
                      'perFrameAutoFit': False, 'basis': 'same size as Bounce: frame 01 body 92x171 at feet row 280'},
        'matteProcessing': 'clean_stage2_bounce_a.matte_body (homing maths + Bounce preservation fixes), enclosed restoration only below min channel 228 as on the rain sheet',
        'face': 'generated mask has no mouth; kept as drawn',
        'handShrink': {'frames': sorted(HAND_FRAMES), 'scale': HAND_SCALE, 'method': 'shrink_hand: open hand (skin + its own creases/outline, holes filled) scaled about the wrist contact with the sleeve, nearest-neighbour; wrist bridge closes the hand-to-sleeve gap within 12 px of the wrist with the nearest wrist skin colour (user: 손부분이랑 옷 소매 부분 사이가 비어서 그 부분 보강해줘)', 'userRequest': '손 크기 줄이기 가넝? 마지막쯤에?'},
        'edgePolish': {'method': 'polish: 1 px spurs (except hand), leg-zone pale bumps / dark non-bluish sole hooks shaved by a 3x3 opening (pieces <= 10 px), 1 px notches filled', 'userRequest': '약간 외곽 좀 덜 정리 됐다'},
        'crossbow': {'method': 'exact approved RGBA crop, translation only', 'cropBounds': list(bbox), 'scale': 1},
        'footCleanup': 'clean_stage2_bounce_a.clean_feet with leg specks up to 12 px removed (detached 9 px toe-sole fragment)', 'strayBowDots': 'generated crossbow sparkle dots detached from the body above canvas row 64 are removed before the approved bow is placed', 'edgeRefinement': 'refine_stage2_homing_d_edges.refine',
        'excluded': {'00': 'input frame', '32-35': 'return to idle'},
    }
    (OUT.parent / 'clean-all.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    assert all(sha(Path(p)) == d for p, d in before.items())
    print(json.dumps([(r['sourceFrame'], r['footResidueDeleted'], r['footResidueRecolored']) for r in records]))


if __name__ == '__main__':
    main()
