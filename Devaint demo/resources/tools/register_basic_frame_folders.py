"""Index user-selected hammer/rapier PNGs without changing their bytes."""
import argparse
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'characters/ink/weapons/basic-outline-20261111'
SET = 'background-removed-v1-20261112'


def read(path): return json.loads(path.read_text(encoding='utf-8'))
def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rapier', type=Path)
    parser.add_argument('--hammer', type=Path)
    parser.add_argument('--set', dest='set_name', default=SET)
    args = parser.parse_args()
    if args.rapier is None and args.hammer is None:
        parser.error('Select at least one weapon folder')
    set_name = args.set_name
    if not set_name or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for c in set_name):
        raise ValueError('Set name must contain lowercase letters, numbers and hyphens')
    target = ROOT / GROUP
    if (target / 'runtime' / set_name).exists():
        raise ValueError('Set already exists; preserve registered source bytes')
    files = {}
    selected = [(folder, source, count) for folder, source, count in
                [('hammer-a', args.hammer, 21), ('rapier', args.rapier, 24)] if source is not None]
    for folder, source, count in selected:
        expected = [f'{i:02d}.png' for i in range(count)]
        if sorted(p.name for p in source.iterdir() if p.is_file()) != expected:
            raise ValueError('Unexpected frame list: ' + str(source))
        for name in expected:
            raw = (source / name).read_bytes()
            if raw[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', raw[16:24]) != (256, 144):
                raise ValueError('Expected original 256x144 PNG: ' + name)
            files[f'runtime/{set_name}/{folder}/{name}'] = raw
    for name, raw in files.items():
        path = target / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
    asset_path = target / 'asset.json'; asset = read(asset_path)
    asset['files'] += sorted(files)
    asset.setdefault('generatedSets', {})[set_name] = {
        'runtime': 'runtime/' + set_name, 'approval': 'user-requested-game-integration-20261112',
        'source': 'User-selected background-removed-v1/png weapon folder',
        'frameCounts': {folder: count for folder, _, count in selected}, 'gameIntegration': 'selected-for-main-deployment'}
    write(asset_path, asset)
    generated_path = ROOT / 'generated-assets.json'; generated = read(generated_path)
    generated['files'] += [{'path': GROUP + '/' + name, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
                           for name, raw in sorted(files.items())]
    write(generated_path, generated)
    catalog_path = ROOT / 'catalog.json'; catalog = read(catalog_path)
    next(a for a in catalog['assets'] if a['path'] == GROUP)['fileCount'] = len(asset['files'])
    write(catalog_path, catalog)
    print(f'Preserved and indexed {len(files)} original PNG frames')


if __name__ == '__main__': main()
