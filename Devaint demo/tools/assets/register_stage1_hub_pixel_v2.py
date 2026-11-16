"""Point the village at the second pixel set: the two buildings (the record farmhouse gets its own village file, the
old house_balcony stays for the combat backgrounds and the loading view), the palette atelier's colour-spread frames,
and the nine props with their corner mid tones. Imports stay pixel-exact; the spread frames share the building's
bottom-centre pivot."""
import hashlib
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'environments.village.stage1-hub-pixel-v2-20261112'
SET = 'environments/village/stage1-hub-pixel-v2-20261112/runtime/'
HUB = 'unity/Assets/Deviant/Hub/Resources/Hub/'

MAP = {}
for name in ('return-well', 'record-desk', 'palette-bench', 'departure-sign', 'low-wall', 'handcart'):
    for state in ('sketch', 'color'):
        MAP[f'{HUB}props/{state}/{name}.png'] = f'props/{state}/{name}.png'
for name in ('bridge-pier', 'crate', 'fence'):
    for suffix in ('', '-sketch'):
        MAP[f'{HUB}opening/course-props/{name}{suffix}.png'] = f'course-props/{name}{suffix}.png'
for name in ('record-farmhouse', 'palette-atelier'):
    for state in ('sketch', 'color'):
        MAP[f'{HUB}buildings/{state}/{name}.png'] = f'buildings/{state}/{name}.png'
for k in range(1, 9):
    MAP[f'{HUB}effects/color-spread/palette-atelier/{k:02d}.png'] = f'effects/palette-atelier/{k:02d}.png'


def pixel_meta(text):
    text = re.sub(r'spritePixelsToUnits: [0-9.]+', 'spritePixelsToUnits: 32', text)
    text = text.replace('enableMipMap: 1', 'enableMipMap: 0')
    text = re.sub(r'filterMode: \d', 'filterMode: 0', text)
    text = re.sub(r'textureCompression: \d', 'textureCompression: 0', text)
    text = re.sub(r'spritePivot: \{x: [0-9.]+, y: [0-9.]+\}', 'spritePivot: {x: 0.5, y: 0}', text)
    return text


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    old = {row['target']: row for row in manifest['files']}
    template = (ROOT / (HUB + 'buildings/color/palette-atelier.png.meta')).read_text(encoding='utf-8')
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
        if not meta.exists():                                   # a new village file: the atelier's import, a new guid
            meta.write_text(re.sub(r'guid: [0-9a-f]{32}', 'guid: ' + uuid.uuid4().hex, template, count=1), encoding='utf-8', newline='\n')
        meta.write_text(pixel_meta(meta.read_text(encoding='utf-8')), encoding='utf-8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    register()
