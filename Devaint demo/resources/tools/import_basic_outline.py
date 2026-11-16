"""Preserve the user-supplied hammer/rapier basic frames and index their original bytes."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'characters/ink/weapons/basic-outline-20261111'
ASSET = 'characters.ink.weapons.basic-outline'


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    args = parser.parse_args()
    target = ROOT / GROUP
    if target.exists():
        raise ValueError('Use a new set; never overwrite preserved frames')
    files = {}
    expected = [f'{folder}/{i:02d}.png' for folder, count in [('hammer-a', 21), ('rapier', 22)] for i in range(count)]
    with zipfile.ZipFile(args.archive) as archive:
        if sorted(archive.namelist()) != sorted(expected):
            raise ValueError('Expected exactly 21 hammer and 22 rapier PNGs')
        for name in expected:
            files['runtime/' + name] = archive.read(name)
    files['source/' + args.archive.name] = args.archive.read_bytes()
    for name, raw in files.items():
        path = target / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
    write(target / 'asset.json', {'format': 'deviant-resource-asset-v1', 'id': ASSET, 'path': GROUP,
        'status': 'user-selected', 'approval': 'user-requested-game-integration-20261111',
        'gameIntegration': 'selected-for-main-deployment', 'sources': [args.archive.name], 'files': sorted(files)})
    manifest_path = ROOT / 'generated-assets.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files'] += [{'path': GROUP + '/' + name, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()} for name, raw in files.items()]
    write(manifest_path, manifest)
    catalog_path = ROOT / 'catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    catalog['assets'].append({'id': ASSET, 'path': GROUP, 'fileCount': len(files)})
    write(catalog_path, catalog)
    print(f'Preserved {len(expected)} original PNGs and the input archive')


if __name__ == '__main__':
    main()
