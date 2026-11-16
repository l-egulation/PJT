"""Return-to-idle clips for the four approved phase3 motions (user: 공격을 하고 나서 제자리로 돌아오는 모션이 없어).

Homing / Bounce / ExpandingFan: the unused Aether cells after each approved clip (homing 22-35, bounce 33-35,
fan 32-35) are cleaned with exactly the pipeline of the approved clip (same matte, shared affine, edge refinement,
foot cleanup, approved crossbow by translation). ArrowRain has no return cells, so its last approved frame is joined to
the approved fused idle by signed-distance in-betweens built only from the pixels of those two frames (no blending,
binary alpha), like the ExpandingFan clench in-betweens. Approved runtime clips are read, never written.
Output: preview/returns/<clip>/NN.png and preview/returns/returns.json (candidates, not approved).
"""
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, label

sys.path.insert(0, str(Path(__file__).resolve().parent))
import clean_stage2_bounce_a as bounce  # noqa: E402
import clean_stage2_fan_all as fan  # noqa: E402
import clean_stage2_homing_d as homing  # noqa: E402
from clean_stage2_homing_d import components  # noqa: E402
from refine_stage2_homing_d_edges import refine  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
OUT = GROUP / 'preview/returns'
IDLE = GROUP / 'source/approved-fused-idle-original.png'
FRAME_MS = 80
RAIN_TWEENS = (0.25, 0.5, 0.75)
BRIDGE_ABOVE = 500


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def approved_bow():
    a = np.array(Image.open(IDLE).convert('RGBA'))
    lab, sizes = components(a[:, :, 3] > 0)
    bow = a.copy(); bow[lab == sizes.argmax()] = 0; bow[bow[:, :, 3] == 0] = 0
    im = Image.fromarray(bow); box = im.getbbox()
    return im.crop(box), box, np.unique(a[a[:, :, 3] > 0, :3], axis=0).astype(float)


def place_bow(frame, bow, source_bow, sx, sy, offset):
    center = ((source_bow[0] + source_bow[2]) / 2 * sx + offset[0], (source_bow[1] + source_bow[3]) / 2 * sy + offset[1])
    pos = (round(center[0] - bow.width / 2), round(center[1] - bow.height / 2))
    frame.alpha_composite(bow, pos)
    return pos


def check(frame, bow, pos):
    px = np.array(frame)
    assert set(np.unique(px[:, :, 3])) <= {0, 255}
    assert not np.any(px[px[:, :, 3] == 0])
    exp = np.array(bow); act = np.array(frame.crop((*pos, pos[0] + bow.width, pos[1] + bow.height)))
    assert np.array_equal(act[exp[:, :, 3] > 0], exp[exp[:, :, 3] > 0])


def homing_frame(source, bow, palette):
    raw = Image.open(GROUP / f'source/homing-aether-01/frames/{source:02}.png').convert('RGBA')
    body, source_bow, changed = homing.matte_body(raw)
    assert changed == 0
    frame = Image.new('RGBA', (384, 256)); frame.paste(body.resize((480, 270), Image.Resampling.NEAREST), (-48, -21))
    frame, _ = refine(frame, palette)
    pos = place_bow(frame, bow, source_bow, .75, .75, (-48, -21))
    return frame, pos


def bounce_frame(source, bow, palette):
    raw = Image.open(GROUP / f'source/bounce-aether-02/frames/{source:02}.png').convert('RGBA')
    body, source_bow, changed, _, holes = bounce.matte_body(raw)
    assert changed == 0 and holes == 0
    frame = Image.new('RGBA', (384, 256)); frame.paste(body.resize(bounce.DEST, Image.Resampling.NEAREST), bounce.OFFSET)
    frame, _ = refine(frame, palette)
    foot, _, _ = bounce.clean_feet(np.array(frame))
    frame = Image.fromarray(foot)
    pos = place_bow(frame, bow, source_bow, bounce.SX, bounce.SY, bounce.OFFSET)
    return frame, pos


def fan_frame(source, bow, palette):
    raw = Image.open(GROUP / f'source/fan-aether-03/frames/{source:02}.png').convert('RGBA')
    body, source_bow, changed, _, holes = bounce.matte_body(raw)
    assert changed == 0 and holes == 0
    frame = Image.new('RGBA', (384, 256)); frame.paste(body.resize(fan.DEST, Image.Resampling.NEAREST), fan.OFFSET)
    frame, _ = refine(frame, palette)
    foot, _, _ = bounce.clean_feet(np.array(frame))
    lab, _ = components(foot[:, :, 3] > 0)
    sizes = np.bincount(lab.ravel()); sizes[0] = 0; body_id = sizes.argmax()
    for i in np.nonzero(sizes)[0]:
        if i != body_id and np.nonzero(lab == i)[0].max() < fan.BOW_BAND_BOTTOM:
            foot[lab == i] = 0
    foot, _, _ = fan.polish(foot)
    frame = Image.fromarray(foot)
    pos = place_bow(frame, bow, source_bow, fan.SX, fan.SY, fan.OFFSET)
    return frame, pos


def without_bow(a, bow, pos):
    a = a.copy(); m = np.array(bow)[:, :, 3] > 0
    y, x = pos[1], pos[0]
    sub = a[y:y + m.shape[0], x:x + m.shape[1]]; sub[m] = 0
    return a


def sdf(m):
    return distance_transform_edt(~m) - distance_transform_edt(m)


def tween(a, b, t):
    """Silhouette = zero level of the blended signed distance; colour from the nearer-in-time frame where opaque,
    else the other frame, else the nearest opaque pixel of either."""
    ma, mb = a[:, :, 3] > 0, b[:, :, 3] > 0
    m = (1 - t) * sdf(ma) + t * sdf(mb) < 0
    first, second = (a, b) if t <= .5 else (b, a)
    f1, f2 = first[:, :, 3] > 0, second[:, :, 3] > 0
    out = np.zeros_like(a)
    out[m & f1] = first[m & f1]
    rest = m & ~f1 & f2; out[rest] = second[rest]
    hole = m & ~f1 & ~f2
    if hole.any():
        both = np.where(f1[..., None], first, second); src = f1 | f2
        _, (iy, ix) = distance_transform_edt(~src, return_indices=True)
        out[hole] = both[iy[hole], ix[hole]]
    out[m, 3] = 255; out[~m] = 0
    # keep only the main body piece
    lab, n = label(m, structure=np.ones((3, 3)))
    if n > 1:
        sizes = np.bincount(lab.ravel()); sizes[0] = 0
        out[(lab != sizes.argmax()) & m] = 0
    return out


def split_bow(a, band=64):
    """Body = largest piece; everything else whose lowest pixel is above the bow band is the crossbow layer."""
    lab, _ = components(a[:, :, 3] > 0)
    sizes = np.bincount(lab.ravel()); sizes[0] = 0; body_id = sizes.argmax()
    bow = np.zeros_like(a); body = a.copy()
    for i in np.nonzero(sizes)[0]:
        if i != body_id and np.nonzero(lab == i)[0].max() < band:
            bow[lab == i] = a[lab == i]; body[lab == i] = 0
    return body, bow


def glide():
    """Rest-move pose (user: 떠서 미끄러지기): the approved fused-arrival pose, entered and left through two
    signed-distance in-betweens from the approved idle. The crossbow layer switches at the midpoint."""
    arrival = np.array(Image.open(GROUP / 'preview/assets/fused-arrival.png').convert('RGBA'))
    idle = np.array(Image.open(IDLE).convert('RGBA'))
    (OUT / 'glide').mkdir(exist_ok=True)
    ib, ibow = split_bow(idle); ab, abow = split_bow(arrival)
    recs = []
    for n, t in enumerate((1 / 3, 2 / 3)):
        img = tween(ib, ab, t); layer = ibow if t < .5 else abow
        img[layer[:, :, 3] > 0] = layer[layer[:, :, 3] > 0]
        p = OUT / 'glide' / f'in{n}.png'; Image.fromarray(img).save(p)
        recs.append({'file': f'glide/in{n}.png', 'synthesisedAt': round(t, 3), 'sha256': sha(p)})
    p = OUT / 'glide' / 'arrival.png'; Image.fromarray(arrival).save(p)
    recs.append({'file': 'glide/arrival.png', 'copyOf': 'preview/assets/fused-arrival.png', 'sha256': sha(p)})
    return {'method': 'approved fused-arrival pose with two idle->arrival in-betweens (played in reverse to land)', 'frames': recs}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    bow, box, palette = approved_bow()
    idle = np.array(Image.open(IDLE).convert('RGBA'))
    idle_pos = (box[0], box[1])
    fan.bounce.ENCLOSED_WHITE_MINIMUM = bounce.ENCLOSED_WHITE_MINIMUM
    clips = {}
    plans = [('homing-d-v2', homing_frame, list(range(22, 36))), ('bounce-a', bounce_frame, [33, 34, 35]), ('fan-final', fan_frame, [32, 33, 34, 35])]
    for name, fn, sources in plans:
        if name == 'fan-final':
            bounce.ENCLOSED_WHITE_MINIMUM = fan.ENCLOSED_RESTORE_BELOW; bounce.SPECK_MAXIMUM = fan.LEG_SPECK_MAXIMUM
        else:
            bounce.ENCLOSED_WHITE_MINIMUM = 240; bounce.SPECK_MAXIMUM = 8
        (OUT / name).mkdir(exist_ok=True)
        for old in (OUT / name).glob('*.png'):
            old.unlink()
        made = [(s,) + fn(s, bow, palette) for s in sources]
        # Stop at the cell closest to the idle silhouette (later cells drift or repeat), then bridge the remaining
        # difference with one signed-distance in-between when it is still large.
        idle_mask = idle[:, :, 3] > 0
        diffs = [int(((np.array(f)[:, :, 3] > 0) ^ idle_mask).sum()) for _, f, _ in made]
        made = made[:int(np.argmin(diffs)) + 1]
        recs = []
        for n, (s, frame, pos) in enumerate(made):
            check(frame, bow, pos)
            p = OUT / name / f'{n:02}.png'; frame.save(p)
            recs.append({'file': f'{name}/{n:02}.png', 'sourceFrame': s, 'durationMs': FRAME_MS, 'crossbowPosition': list(pos), 'sha256': sha(p)})
        if min(diffs) > BRIDGE_ABOVE:
            _, frame, pos = made[-1]
            img = Image.fromarray(tween(without_bow(np.array(frame), bow, pos), without_bow(idle, bow, idle_pos), .5))
            bpos = (round((pos[0] + idle_pos[0]) / 2), round((pos[1] + idle_pos[1]) / 2)); img.alpha_composite(bow, bpos); check(img, bow, bpos)
            n = len(recs); p = OUT / name / f'{n:02}.png'; img.save(p)
            recs.append({'file': f'{name}/{n:02}.png', 'synthesisedAt': .5, 'between': 'last return cell and idle', 'durationMs': FRAME_MS, 'crossbowPosition': list(bpos), 'sha256': sha(p)})
        clips[name] = {'method': 'unused Aether return cells, approved clip pipeline, cut at the cell closest to idle', 'idleSilhouetteDiff': diffs, 'frames': recs}
    # ArrowRain: in-betweens from the approved last frame to the approved idle.
    rain = json.loads((GROUP / 'runtime/rain-a-x2/motion.json').read_text(encoding='utf-8'))
    last = rain['frames'][-1]
    la = np.array(Image.open(GROUP / 'runtime/rain-a-x2' / last['file']).convert('RGBA'))
    a = without_bow(la, bow, last['crossbowPosition']); b = without_bow(idle, bow, idle_pos)
    (OUT / 'rain-a-x2').mkdir(exist_ok=True); recs = []
    for old in (OUT / 'rain-a-x2').glob('*.png'):
        old.unlink()
    for n, t in enumerate(RAIN_TWEENS):
        img = Image.fromarray(tween(a, b, t))
        pos = (round((1 - t) * last['crossbowPosition'][0] + t * idle_pos[0]), round((1 - t) * last['crossbowPosition'][1] + t * idle_pos[1]))
        img.alpha_composite(bow, pos); check(img, bow, pos)
        p = OUT / 'rain-a-x2' / f'{n:02}.png'; img.save(p)
        recs.append({'file': f'rain-a-x2/{n:02}.png', 'synthesisedAt': t, 'durationMs': FRAME_MS, 'crossbowPosition': list(pos), 'sha256': sha(p)})
    clips['rain-a-x2'] = {'method': 'signed-distance in-betweens of the approved last frame and the approved idle (source pixels only)', 'frames': recs}
    clips['glide'] = glide()
    manifest = {'format': 'stage2-phase3-returns-v1', 'status': 'candidate-not-approved', 'canvas': [384, 256], 'pivot': [192, 232],
                'idle': '../../source/approved-fused-idle-original.png', 'idleSha256': sha(IDLE), 'frameMs': FRAME_MS, 'clips': clips,
                'userRequest': '이제 각각 공격을 하고 나서 제 자리? 로 돌아오는 모션이 없어'}
    (OUT / 'returns.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({k: len(v['frames']) for k, v in clips.items()}))


if __name__ == '__main__':
    main()
