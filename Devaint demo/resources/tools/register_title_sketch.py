"""Preserve the approved title prototype and prepare its native Unity artwork."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

import numpy as np
from PIL import Image
from scipy.ndimage import maximum_filter1d

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / 'ui/title-sketch-20261115'


def index_generated(files):
    path = ROOT / 'generated-assets.json'
    manifest = json.loads(path.read_text(encoding='utf8'))
    prefix = ASSET.relative_to(ROOT).as_posix() + '/'
    manifest['files'] = [item for item in manifest['files'] if not item['path'].startswith(prefix)]
    for name in files:
        raw = (ASSET / name).read_bytes()
        manifest['files'].append({'path': prefix+name, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()})
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf8')


def register(prototype, character):
    if (ASSET / 'asset.json').exists():
        raise ValueError('The approved title package already exists; preserve it and create a new revision for changes.')
    def copy(source, relative):
        target = ASSET / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    copy(prototype / 'index.html', 'preview/title.html')
    copy(prototype / 'assets/ui/logo-reference-original.png', 'source/logo-original.png')
    copy(prototype / 'assets/ui/yellow-marker/source-clean.png', 'source/marker-original.png')
    copy(character / '후드끝_대기.gif', 'source/idle-approved.gif')
    copy(prototype / 'assets/particles/leaf-ash-4x4-atlas.png', 'runtime/leaf-ash.png')
    for state in ('human', 'ink'):
        for frame in range(1, 49):
            copy(character / state / f'F{frame:02d}.png', f'runtime/hero/{state}/F{frame:02d}.png')
    # Match the title's SVG paper cutout; keep the full original above.
    logo = np.array(Image.open(ASSET / 'source/logo-original.png').convert('RGBA')).copy()
    alpha = np.clip(5.45 * 255 - 2 * logo[:, :, :3].astype(float).sum(axis=2), 0, 255)
    logo[:, :, 3] = np.rint(alpha).astype(np.uint8)
    Image.fromarray(logo).crop((100, 55, 1660, 455)).save(ASSET / 'runtime/logo.png')
    # Match applyMarker(): blue-channel paper removal and 18px vertical ink expansion.
    selection = json.loads((prototype / 'assets/ui/yellow-marker/selection.json').read_text(encoding='utf8'))
    x, y, width, height = selection['viewBox']
    marker = np.array(Image.open(ASSET / 'source/marker-original.png').convert('RGBA').crop((x, y, x+width, y+height))).copy()
    alpha = np.clip(1785 - marker[:, :, 2].astype(int) * 8, 0, 255).astype(np.uint8)
    marker[:, :, 3] = alpha
    marker[alpha == 0] = 0
    expanded = maximum_filter1d((alpha >= 128).astype(np.uint8), size=37, axis=0, mode='constant') > 0
    marker[expanded & (alpha < 255)] = (254, 206, 20, 255)
    Image.fromarray(marker).save(ASSET / 'runtime/marker.png')
    gif = Image.open(ASSET / 'source/idle-approved.gif')
    durations = []
    for frame in range(gif.n_frames):
        gif.seek(frame)
        durations.append(gif.info['duration'])
    assert len(durations) == 48 and sum(durations) == 3600
    files = sorted(str(p.relative_to(ASSET)).replace('\\', '/') for p in ASSET.rglob('*') if p.is_file())
    metadata = {'format': 'deviant-resource-asset-v1', 'id': 'ui.title-sketch-20261115',
                'path': 'ui/title-sketch-20261115', 'approval': 'user-approved', 'files': files,
                'motion': {'frames': 48, 'frameSize': [128,128], 'frameDurationsMs': durations,
                           'loopMs': 3600, 'footAnchorY': 121, 'sourceFaces': 'right'},
                'source': 'Approved local title prototype and user-provided new-design idle; hero PNG bytes preserved.',
                'sha256': {n: hashlib.sha256((ASSET/n).read_bytes()).hexdigest() for n in files}}
    (ASSET / 'asset.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    catalog_path = ROOT / 'catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf8'))
    catalog['assets'].append({'id': metadata['id'], 'path': metadata['path'], 'fileCount': len(files)})
    catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    index_generated(files)
    payload_path = ROOT / '.lfs-payloads.json'
    payload = json.loads(payload_path.read_text(encoding='utf8'))
    payload.append('ui/title-sketch-20261115/preview/title.html')
    payload_path.write_text(json.dumps(sorted(set(payload)), ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(f'Registered {len(files)} files; 96 unmodified character PNGs; 3600ms idle loop.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prototype', required=True, type=Path)
    parser.add_argument('--character', required=True, type=Path)
    args = parser.parse_args()
    register(args.prototype.resolve(), args.character.resolve())
