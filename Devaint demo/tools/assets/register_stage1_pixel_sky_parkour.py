"""Point the village sky path's parkour props (Hub/sky/props) at the 2x2-dot pixel set.

The pictures keep their names, so StartingVillageWorld.SkyParkour loads them as before; the single-tile crumbling stone
frames (crumbling-1tile-00..05), the bell tower and the summit record board are new and get new .meta files from the
crumbling stone's (top-left pivot, 32 PPU). Hub/ art is imported point-filtered and uncompressed by HubArtImporter, so
existing .meta files are left as they are. Walls are not pictures: they use the combat lawn atlas (Stage1Combat)."""
import hashlib
import json
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'environments.stage1-pixel-sky-parkour-20261112'
SET = 'environments/stage1-pixel-sky-parkour-20261112/runtime/'
PROPS = 'unity/Assets/Deviant/Hub/Resources/Hub/sky/props/'

NAMES = ([f'floating-{kind}-{k}tile' for kind in ('bare', 'green') for k in (1, 2, 3)]
         + ['moving-2tile', 'moving-3tile', 'arrow-left', 'arrow-right', 'bell-tower', 'summit-record-board']
         + [f'launcher-{side}-0{f}' for side in ('left', 'right') for f in range(4)]
         + [f'crumbling-0{f}' for f in range(6)] + [f'crumbling-1tile-0{f}' for f in range(6)])


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    old = {row['target']: row for row in manifest['files']}
    for name in NAMES:
        target = PROPS + name + '.png'
        raw = (ROOT / 'resources' / (SET + name + '.png')).read_bytes()
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
        if target in old:
            prior = [old[target]['sha256']] + old[target].get('replaceHashes', [])
            replaced = sorted(set(prior) - {row['sha256']})
            if replaced: row['replaceHashes'] = replaced
        row['source'] = {'asset': ASSET, 'path': SET + name + '.png'}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None: manifest['files'].append(row)
        else: manifest['files'][at] = row
        meta = ROOT / (target + '.meta')
        if not meta.exists():   # new pictures: the crumbling stone's settings (top-left pivot) under a new GUID
            lines = (ROOT / (PROPS + 'crumbling-00.png.meta')).read_text(encoding='utf-8').splitlines()
            lines[1] = 'guid: ' + uuid.uuid4().hex
            meta.write_text('\n'.join(lines) + '\n', encoding='utf-8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    register()
