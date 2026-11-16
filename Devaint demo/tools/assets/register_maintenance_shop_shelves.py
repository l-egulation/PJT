"""Deploy the maintenance room display art (user-approved 2026-11-14) beside the adopted shop art.

The chapter 1 shop building and chapter 2 wall recess (one drawing per row length) replace the old backdrops in the room, the chapter 2 palette sign
takes the chapter 1 shape, and the reroll supply crates stand at the row ends once shared rerolls are unlocked. Import
settings follow the chapter 2 desk (Point, 32 px per unit, no mipmaps). Run after changing
resources/environments/maintenance-shop-shelves-20261114/runtime.
"""
import hashlib
import json
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'environments.maintenance-shop-shelves-20261114'
SET = 'environments/maintenance-shop-shelves-20261114/runtime/'
SHOP = 'unity/Assets/Deviant/Game/Resources/MaintenanceShops/'
# Shop drawings come in one width per row length (3-6 places: goods plus the reroll crate), same spacing throughout.
NAMES = [f'stage{c}-shop-{n}.png' for c in (1, 2) for n in (3, 4, 5, 6)] + ['sign-stage2.png', 'stage1-reroll-crate.png', 'stage2-reroll-crate.png']
RETIRED = ['stage1-shop.png', 'stage2-shop.png']


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    template = (ROOT / (SHOP + 'stage2-desk.png.meta')).read_text(encoding='utf-8')
    for name in NAMES:
        target = SHOP + name; raw = (ROOT / 'resources' / (SET + name)).read_bytes()
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(), 'source': {'asset': ASSET, 'path': SET + name}}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None: manifest['files'].append(row)
        else:
            prior = [manifest['files'][at]['sha256']] + manifest['files'][at].get('replaceHashes', [])
            replaced = sorted(set(prior) - {row['sha256']})
            if replaced: row = {**row, 'replaceHashes': replaced}
            manifest['files'][at] = {k: row[k] for k in ('target', 'bytes', 'sha256', 'replaceHashes', 'source') if k in row}
        meta = ROOT / (target + '.meta')
        if not meta.exists():
            lines = template.splitlines(); lines[1] = 'guid: ' + uuid.uuid4().hex
            meta.write_text('\n'.join(lines) + '\n', encoding='utf-8', newline='\n')
    manifest['files'] = [item for item in manifest['files'] if item['target'] not in {SHOP + name for name in RETIRED}]
    for name in RETIRED:
        for path in (ROOT / (SHOP + name), ROOT / (SHOP + name + '.meta')):
            if path.exists(): path.unlink()
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    register()
