"""Human-hero wall climb frames for the opening road (wall contact and wall kick).

The approved wall-kick master draws the ink hero: a black face with yellow eyes and black hands. The opening road
is walked by the human hero, who differs from the ink body only in the head (brown hair, skin, black eyes) and the
hands (skin). For each wall frame this takes the unarmed ink frame whose face silhouette matches it, and pastes the
human hero's head at the same place (the human frame minus its ink twin, so hood and scarf stay the wall frame's);
black blobs that do not touch the boots' yellow bands are hands and turn skin. Every wall frame used by the game
(F08-F23 contact, F24-F29 kick) searches all human/ink pairs of the same pose sets (walk, run, jump, idle, fall_land,
mirrored too) for the best face match; frames sharing a face shape land on the same pair, so the loop does not
flicker. Sources are left untouched; output is characters/story/human-hero/runtime/wall/FNN.png (128x128, no outline:
the game draws it).

    python3 tools/export_human_wall_climb.py [--check]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
WALL = ROOT / 'characters/ink/movement/wall-kick/runtime/master'
INK = ROOT / 'characters/ink/unarmed-body/runtime'
HUMAN = ROOT / 'characters/story/human-hero/runtime'
OUT = HUMAN / 'wall'
CLIPS = {'walk': '걷기', 'jump': '점프', 'run': '달리기', 'idle': '대기', 'fall_land': '낙하착지'}
FRAMES = [f'F{n:02d}' for n in range(8, 30)]
BLACK, YELLOW, SKIN = (27, 22, 22), (235, 165, 22), (254, 176, 133)
MIN_IOU = .9


def load(path):
    return np.array(Image.open(path).convert('RGBA'))


def colour(a, c):
    return (a[..., 3] > 0) & (a[..., 0] == c[0]) & (a[..., 1] == c[1]) & (a[..., 2] == c[2])


def face(a):
    """The highest black blob holding yellow eyes."""
    lab, n = ndimage.label(colour(a, BLACK) | colour(a, YELLOW))
    best = None
    for i in range(1, n + 1):
        m = lab == i
        if (m & colour(a, YELLOW)).any() and (m & colour(a, BLACK)).any():
            y = np.nonzero(m)[0].mean()
            if best is None or y < best[0]:
                best = (y, m)
    if best is None:
        raise ValueError('no face')
    return best[1]


def shift(a, dy, dx):
    return np.roll(np.roll(a, dy, 0), dx, 1)


def align(wall_face, ink_face):
    (y0, x0), (y1, x1) = [tuple(int(v.min()) for v in np.nonzero(m)) for m in (wall_face, ink_face)]
    best = (-1.0, 0, 0)
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            s = shift(ink_face, y0 - y1 + dy, x0 - x1 + dx)
            iou = (s & wall_face).sum() / (s | wall_face).sum()
            if iou > best[0]:
                best = (iou, y0 - y1 + dy, x0 - x1 + dx)
    return best


PAIRS_CACHE = []


def pairs():
    if not PAIRS_CACHE:
        for clip, ink_dir in CLIPS.items():
            for path in sorted((HUMAN / clip).glob('*.png')):
                ink_path = INK / ink_dir / path.name
                if not ink_path.is_file():
                    continue
                ink, human = load(ink_path), load(path)
                for mirrored in (False, True):
                    i, h = (ink[:, ::-1].copy(), human[:, ::-1].copy()) if mirrored else (ink, human)
                    try:
                        PAIRS_CACHE.append(((clip, path.stem, mirrored), i, h, face(i)))
                    except ValueError:
                        pass
    return PAIRS_CACHE


def human_wall(name, report):
    wall = load(WALL / f'{name}.png')
    wall_face = face(wall)
    best = None
    for key, ink, human, ink_face in pairs():
        iou, dy, dx = align(wall_face, ink_face)
        if best is None or iou > best[0] + 1e-9:
            best = (iou, dy, dx, key, ink, human, ink_face)
    iou, dy, dx, key, ink, human, ink_face = best
    report.append(f'{name} <- {key} iou {iou:.3f}')
    if iou < MIN_IOU:
        raise ValueError(f'{name}: no human pose matches the face ({iou:.3f})')
    ink, human, ink_face = shift(ink, dy, dx), shift(human, dy, dx), shift(ink_face, dy, dx)
    head = ndimage.binary_dilation(ink_face, iterations=3)
    head[np.nonzero(ink_face)[0].max() + 2:] = False
    # Inside the wall's face everything is the human head; around it only where the wall still shows the ink twin.
    take = head & (wall_face | ((human != ink).any(-1) & (wall == ink).all(-1)))
    out = wall.copy()
    out[take] = human[take]
    lab, n = ndimage.label(colour(out, BLACK))
    boots = ndimage.binary_dilation(colour(out, YELLOW))
    for i in range(1, n + 1):
        m = lab == i
        if m.sum() >= 15 and not (m & boots).any():
            out[m] = SKIN + (255,)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--check', action='store_true', help='fail if a written frame differs')
    args = parser.parse_args()
    OUT.mkdir(exist_ok=True)
    report = []
    for name in FRAMES:
        image = Image.fromarray(human_wall(name, report))
        path = OUT / f'{name}.png'
        if args.check:
            if not path.is_file() or not np.array_equal(load(path), np.array(image)):
                raise SystemExit('stale: ' + str(path.relative_to(ROOT)))
        else:
            image.save(path, optimize=True)
    print(chr(10).join(report))
    print(('checked ' if args.check else 'wrote ') + str(len(FRAMES)) + ' frames')


if __name__ == '__main__':
    main()
