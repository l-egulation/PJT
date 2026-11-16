"""Point the village (hub) props and the tutorial-road props at the 2x2-dot pixel set and keep their imports pixel-exact.

The buildings (record farmhouse, palette atelier) are not part of this set."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'environments.village.stage1-hub-pixel-props-20261112'
SET = 'environments/village/stage1-hub-pixel-props-20261112/runtime/'
HUB = 'unity/Assets/Deviant/Hub/Resources/Hub/'

MAP = {}
for name in ('return-well', 'record-desk', 'palette-bench', 'departure-sign', 'low-wall', 'handcart'):
    for state in ('sketch', 'color'):
        MAP[f'{HUB}props/{state}/{name}.png'] = f'props/{state}/{name}.png'
for name in ('bridge-pier', 'crate', 'fence'):
    for suffix in ('', '-sketch'):
        MAP[f'{HUB}opening/course-props/{name}{suffix}.png'] = f'course-props/{name}{suffix}.png'


def pixel_meta(text):
    text = re.sub(r'spritePixelsToUnits: [0-9.]+', 'spritePixelsToUnits: 32', text)
    text = text.replace('enableMipMap: 1', 'enableMipMap: 0')
    text = re.sub(r'filterMode: \d', 'filterMode: 0', text)
    text = re.sub(r'textureCompression: \d', 'textureCompression: 0', text)
    return text


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    old = {row['target']: row for row in manifest['files']}
    for target, src in MAP.items():
        raw = (ROOT / 'resources' / (SET + src)).read_bytes()
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
        if target in old:
            prior = [old[target]['sha256']] + old[target].get('replaceHashes', [])
            row['replaceHashes'] = sorted(set(prior) - {row['sha256']})
        row['source'] = {'asset': ASSET, 'path': SET + src}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None: manifest['files'].append(row)
        else: manifest['files'][at] = row
        meta = ROOT / (target + '.meta')
        meta.write_text(pixel_meta(meta.read_text(encoding='utf-8')), encoding='utf-8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    register()
