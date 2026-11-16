"""Synthesised in-between frames for the ExpandingFan clench (user: "주먹쪽이 또 끊기네 추가혀").

Frames 18->24 change much more per frame than the rest of the clip (silhouette/colour change 4.5k-5.3k vs 3.6k-3.9k)
and the source has no drawing between them. Each in-between is built from the two cleaned neighbours only:
the silhouette is the zero level of the averaged signed distance fields; colours come from the earlier frame where
it is opaque, else the later frame, else the nearest opaque pixel of either. No blending, alpha stays 0/255. The
crossbow of both neighbours is removed and the exact approved crossbow is placed at the averaged position.
24->25 (the release snap) is left as a cut on purpose.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, label

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
CAND = GROUP / 'preview/fan-candidates'
PAIRS = [(18, 19), (19, 20), (20, 21), (21, 22), (22, 23), (23, 24)]
APPROVED_RUNTIME = GROUP / 'runtime/fan-final/motion.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bow_crop():
    a = np.array(Image.open(GROUP / 'source/approved-fused-idle-original.png').convert('RGBA'))
    lab, _ = label(a[:, :, 3] > 0, np.ones((3, 3)))
    sz = np.bincount(lab.ravel()); sz[0] = 0
    a[lab == sz.argmax()] = 0; a[a[:, :, 3] == 0] = 0
    im = Image.fromarray(a)
    return im.crop(im.getbbox())


def sdf(m):
    return distance_transform_edt(~m) - distance_transform_edt(m)


def main():
    if APPROVED_RUNTIME.exists() and json.loads(APPROVED_RUNTIME.read_text(encoding='utf-8')).get('approval'):
        raise SystemExit('ExpandingFan is user-approved. Author further edits as a new version.')
    clean = json.loads((CAND / 'clean-all.json').read_text(encoding='utf-8'))
    bow = bow_crop(); bow_a = np.array(bow); bh, bw = bow_a.shape[:2]

    def without_bow(f):
        a = np.array(Image.open(CAND / f'clean/{f:02}.png'))
        x, y = clean['frames'][f - 1]['crossbowPosition']
        region = np.zeros(a.shape[:2], bool); region[y:y + bh, x:x + bw] = bow_a[:, :, 3] > 0
        a[region] = 0
        return a, (x, y)

    records = []
    for fa, fb in PAIRS:
        A, pa = without_bow(fa); B, pb = without_bow(fb)
        ma, mb = A[:, :, 3] > 0, B[:, :, 3] > 0
        mid = (sdf(ma) + sdf(mb)) / 2 < 0
        out = np.zeros_like(A)
        src_a = mid & ma; src_b = mid & ~ma & mb; rest = mid & ~ma & ~mb
        out[src_a] = A[src_a]; out[src_b] = B[src_b]
        if rest.any():
            comb = np.where(ma[..., None], A, B)
            _, idx = distance_transform_edt(~(ma | mb), return_indices=True)
            out[rest] = comb[idx[0][rest], idx[1][rest]]
        out[:, :, 3] = np.where(mid, 255, 0); out[out[:, :, 3] == 0] = 0
        im = Image.fromarray(out)
        pos = (round((pa[0] + pb[0]) / 2), round((pa[1] + pb[1]) / 2))
        im.alpha_composite(bow, pos)
        px = np.array(im)
        assert set(np.unique(px[:, :, 3])) <= {0, 255}
        path = CAND / f'clean/t{fa:02}-{fb:02}.png'
        im.save(path)
        records.append({'file': f'clean/t{fa:02}-{fb:02}.png', 'between': [fa, fb], 'crossbowPosition': list(pos),
                        'pixelsFromEarlier': int(src_a.sum()), 'pixelsFromLater': int(src_b.sum()), 'pixelsNearest': int(rest.sum()),
                        'sha256': sha(path)})
    (CAND / 'tweens.json').write_text(json.dumps({
        'format': 'stage2-fan-tweens-v1', 'userRequest': '주먹쪽이 또 끊기네 추가혀',
        'method': 'averaged signed distance field silhouette; colours from the earlier frame, else the later, else nearest; no blending; approved crossbow at the averaged position',
        'reason': 'frames 18-24 change 4.5k-5.3k px per step vs 3.6k-3.9k elsewhere; no source drawing exists in between',
        'releaseCutKept': '24->25', 'frames': records}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps([(r['file'], r['pixelsFromEarlier'], r['pixelsFromLater'], r['pixelsNearest']) for r in records]))


if __name__ == '__main__':
    main()
