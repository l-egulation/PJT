"""Transfer the accepted outline-free look to native normal-monster motion pixels.

Run once with --apply. The immutable local backup makes repeat runs deterministic.
Only near-black rim pixels adjacent to existing material colors are recolored;
alpha, inner detail, timing, disconnected droplets and separate FX are preserved.
"""
import argparse
import base64
import hashlib
import io
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'approved'
BACKUP = ROOT / 'local-assets/99_archive/before-normal-outline-free-motions-v2'
OUT = ROOT / 'local-assets/normal-outline-free-motions-v3'
IDS = ['01_thorn-root', '02_broom-stalker', '03_gutter-spitter']
STYLE = 'normal-outline-free-motions-v3'
POLICY = {'version': STYLE, 'method': 'extend existing material into near-black rim',
          'maxRimDepth': 2, 'maxMaterialDistance': 5, 'alphaUnchanged': True,
          'internalDetailUnchanged': True, 'separateEffectsUnchanged': True,
          'broomRimMaximumTone': 2, 'removeIsolatedBlackRimPixels': True}


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def encode(im):
    b = io.BytesIO()
    im.save(b, format='PNG')
    return 'data:image/png;base64,' + base64.b64encode(b.getvalue()).decode()


def rgba(path):
    return Image.open(path).convert('RGBA')


def digest(im):
    return hashlib.sha256(im.tobytes()).hexdigest()


def clean(im, palette, monster_id):
    a = np.array(im)
    mask = a[:, :, 3] > 0
    idx = np.argmin(((a[:, :, None, :3].astype(float) - palette) ** 2).sum(axis=3), axis=2)
    labels, count = ndi.label(mask, np.ones((3, 3)))
    sizes = np.bincount(labels.ravel())
    depth = ndi.distance_transform_cdt(mask, metric='chessboard')
    # Work within each component: material on another disconnected part cannot bleed in.
    for component in range(1, count + 1):
        if sizes[component] <= 24:
            continue
        body = labels == component
        seeds = body & (idx > 0)
        if not seeds.any():
            continue
        distance, loc = ndi.distance_transform_edt(~seeds, return_indices=True)
        edit = body & (idx == 0) & (depth <= 2) & (distance <= 5)
        nearest = idx[loc[0], loc[1]]
        if monster_id == '02_broom-stalker':
            # Thin wood/ink limbs must retain contrast: metal/highlight seeds must
            # never turn the surrounding arm and finger pixels nearly white.
            nearest = np.minimum(nearest, 2)
        a[edit, :3] = palette[nearest[edit]]
        remaining = body & (idx == 0) & (depth <= 2) & ~edit
        fragments, total = ndi.label(remaining, np.ones((3, 3)))
        sizes_rim = np.bincount(fragments.ravel())
        filled_neighbor = ndi.binary_dilation(edit, np.ones((3, 3)))
        # A short residual rim between material-colored areas reads as a black
        # flicker. Large continuous ink/shadow masses are deliberately retained.
        for fragment in range(1, total + 1):
            region = fragments == fragment
            if sizes_rim[fragment] <= 3 and np.any(region & filled_neighbor):
                a[region, :3] = palette[np.minimum(nearest[region], 2)]
    assert np.array_equal(a[:, :, 3], np.array(im)[:, :, 3])
    return Image.fromarray(a)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', required=True)
    parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    current = read(DEST / 'manifest.json')
    if not BACKUP.exists():
        BACKUP.mkdir(parents=True)
        for mid in IDS:
            shutil.copytree(DEST / mid, BACKUP / mid)
        shutil.copy2(DEST / 'manifest.json', BACKUP / 'manifest.json')
    source = read(BACKUP / 'manifest.json')
    palette = np.array([list(bytes.fromhex(c[1:])) for c in current['palette']], dtype=np.uint8)
    cache, preview, checks = {}, [], []

    def converted(im):
        key = mid + ':' + digest(im)
        if key not in cache:
            cache[key] = clean(im, palette, mid)
        return cache[key].copy()

    def save_motion(motion, images):
        data = read(BACKUP / motion['editorJSON'])
        ids = data.get('sourceFrames', [0])
        for key, im, path in zip(ids, images, motion['frames']):
            im.save(DEST / path)
            if 'images' in data:
                data['images'][str(key)] = encode(im)
            else:
                data['image'] = encode(im)
        data['outlineFree'] = POLICY
        data['paletteLock'] = True
        write(DEST / motion['editorJSON'], data)
        if motion.get('sheet'):
            sheet = Image.new('RGBA', (images[0].width * len(images), images[0].height))
            for i, im in enumerate(images):
                sheet.paste(im, (i * im.width, 0))
            sheet.save(DEST / motion['sheet'])
        motion['outlineFree'] = POLICY

    for mid in IDS:
        old = next(m for m in source['monsters'] if m['id'] == mid)
        new = next(m for m in current['monsters'] if m['id'] == mid)
        item = {'id': mid, 'name': new['name'], 'motions': {}}
        primary_jsons = set()
        for key, motion in new['motions'].items():
            original = [rgba(BACKUP / f) for f in old['motions'][key]['frames']]
            images = [converted(im) for im in original]
            if motion.get('layers'):
                body = motion['layers']['body']
                effects = motion['layers']['effects']
                old_body = [rgba(BACKUP / f) for f in body['frames']]
                new_body = [converted(im) for im in old_body]
                # Composite authoring can include an extra rim. Restrict recoloring to
                # body coverage and preserve all pixels owned by the original FX layer.
                for i, im in enumerate(original):
                    a = np.array(images[i]); o = np.array(im)
                    b = np.array(old_body[i]); nb = np.array(new_body[i])
                    fx = np.array(rgba(BACKUP / effects['frames'][i]))
                    editable = (b[:, :, 3] > 0) & (fx[:, :, 3] == 0)
                    a[~editable] = o[~editable]
                    same = editable & np.all(o == b, axis=2)
                    a[same] = nb[same]
                    images[i] = Image.fromarray(a)
                save_motion(body, new_body)
                primary_jsons.update([body['editorJSON'], effects['editorJSON']])
            save_motion(motion, images)
            primary_jsons.add(motion['editorJSON'])
            for before, after in zip(original, images):
                a, b = np.array(before), np.array(after)
                assert before.size == after.size
                assert np.array_equal(a[:, :, 3], b[:, :, 3])
                changed = np.any(a != b, axis=2)
                assert np.all(a[changed, :3] == palette[0])
                assert set(map(tuple, b[b[:, :, 3] > 0, :3])) <= set(map(tuple, palette))
            checks.append({'monster': mid, 'motion': key, 'frames': len(images),
                           'changedPixels': sum(int(np.any(np.array(a) != np.array(b), axis=2).sum()) for a,b in zip(original, images)),
                           'dimensionsAlphaTimingPreserved': True})
            item['motions'][key] = {'before': [encode(rgba(ROOT / 'local-assets/99_archive/before-normal-outline-free-motions-v3' / f)) for f in old['motions'][key]['frames']],
                                    'after': [encode(i) for i in images], 'ms': motion['ms'], 'loop': motion['loop']}
        # Auxiliary preparation/attack JSONs contain the same native poses.
        for path in (BACKUP / mid).rglob('*.json'):
            rel = path.relative_to(BACKUP).as_posix()
            if rel in primary_jsons:
                continue
            data = read(path)
            if not ('images' in data or 'image' in data):
                continue
            for k, value in list(data.get('images', {}).items()):
                im = rgba(io.BytesIO(base64.b64decode(value.split(',')[1])))
                data['images'][k] = encode(converted(im))
            if 'image' in data:
                data['image'] = encode(converted(rgba(io.BytesIO(base64.b64decode(data['image'].split(',')[1])))))
            data['outlineFree'] = POLICY
            write(DEST / rel, data)
        # The broom's held anticipation pose also has a standalone PNG export.
        for path in (BACKUP / mid).rglob('preparation.png'):
            converted(rgba(path)).save(DEST / path.relative_to(BACKUP))
        preview.append(item)
    current['normalOutlineFree'] = {'monsterIds': IDS, **POLICY}
    write(DEST / 'manifest.json', current)
    write(OUT / 'data.json', preview)
    write(OUT / 'verification.json', checks)
    shutil.copy2(Path(__file__).with_name('normal-outline-free-preview.html'), OUT / 'index.html')
    # Fixed-palette, nearest-neighbor GIFs for all five motions, including frame timings.
    rgb_palette = [255, 255, 255] + palette.ravel().tolist()
    rgb_palette += [0] * (768 - len(rgb_palette))
    lookup = {tuple(c): i + 1 for i,c in enumerate(palette)}
    for mon in preview:
        frames, times = [], []
        for key in ['idle', 'walk', 'attack', 'hit', 'death']:
            motion = mon['motions'][key]
            ims = []
            for uri in motion['after']:
                im = rgba(io.BytesIO(base64.b64decode(uri.split(',')[1])))
                indexed = Image.new('P', im.size); indexed.putpalette(rgb_palette)
                indexed.putdata([lookup[tuple(p[:3])] if p[3] else 0 for p in np.array(im).reshape(-1,4)])
                ims.append(indexed.resize((512,512), Image.Resampling.NEAREST))
            for _ in range(2 if key in ['idle','walk'] else 1):
                frames.extend(ims); times.extend(motion['ms'])
            frames.append(ims[-1]); times.append(300 if key != 'death' else 650)
        frames[0].save(OUT / (mon['id'] + '.gif'), save_all=True, append_images=frames[1:], duration=times, loop=0, disposal=1, optimize=False)
    print(json.dumps({'mainFrames': sum(c['frames'] for c in checks), 'motions': len(checks), 'output': str(OUT)}))


if __name__ == '__main__':
    main()
