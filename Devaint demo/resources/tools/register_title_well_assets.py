"""Register the additive temporary well-title set without touching existing sets."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSET = 'ui/title-well-temp'
ID = 'ui.title-well-temp'


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def main():
    target = ROOT / ASSET
    if not (target / 'runtime/layout.json').is_file():
        raise ValueError('Build the asset set first')
    files = sorted(p.relative_to(target).as_posix() for p in target.rglob('*')
                   if p.is_file() and p.name != 'asset.json')
    build_path = target / 'source/build-manifest.json'
    if build_path.is_file():
        build = json.loads(build_path.read_text(encoding='utf-8'))
        build['files'] = []
        for name in files:
            if name == 'source/build-manifest.json':
                continue
            data = (target / name).read_bytes()
            build['files'].append({'path': name, 'bytes': len(data),
                                   'sha256': hashlib.sha256(data).hexdigest()})
        save(build_path, build)
    layout = json.loads((target / 'runtime/layout.json').read_text(encoding='utf-8'))
    independent_menu = 'menu' in layout
    previous_asset = target / 'asset.json'
    integration = json.loads(previous_asset.read_text(encoding='utf-8')).get('gameIntegration', 'assets-prepared-not-applied') if previous_asset.is_file() else 'assets-prepared-not-applied'
    for name in files:
        if (target / name).stat().st_size == 0:
            raise ValueError('Empty asset: ' + name)
    save(target / 'asset.json', {
        'format': 'deviant-resource-asset-v1', 'id': ID, 'path': ASSET,
        'name': '임시 타이틀: 평온한 광장과 우물의 둥근 외눈',
        'approval': 'user-selected-for-temporary-title',
        'gameIntegration': integration,
        'sources': ['built-in image_gen', 'deterministic procedural glitch audio'],
        'selectedEye': 'B-round-lidless-burgundy-eye',
        'preferredTemporaryRuntime': 'runtime/backgrounds + runtime/ui/menu' if independent_menu else 'runtime/flat',
        'referenceResolution': [1672, 941],
        'separatedLayerStatus': 'shared-selection-and-tintable-labels' if independent_menu else 'extracted-from-approved-composition',
        'files': files,
    })
    catalog_path = ROOT / 'catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    matching = [item for item in catalog['assets'] if item['id'] == ID]
    if len(matching) > 1:
        raise ValueError('Duplicate asset ID')
    entry = {'id': ID, 'path': ASSET, 'fileCount': len(files)}
    if matching:
        if matching[0]['path'] != ASSET:
            raise ValueError('Existing asset has a different owner path')
        matching[0].update(entry)
    else:
        catalog['assets'].append(entry)
    manifest_path = ROOT / catalog['generatedManifest']
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files'] = [item for item in manifest['files']
                         if not item['path'].startswith(ASSET + '/')]
    for name in files:
        data = (target / name).read_bytes()
        manifest['files'].append({'path': ASSET + '/' + name, 'bytes': len(data),
                                  'sha256': hashlib.sha256(data).hexdigest()})
    save(catalog_path, catalog)
    save(manifest_path, manifest)
    print(json.dumps({'asset': ID, 'files': len(files),
                      'integration': integration}, ensure_ascii=False))


if __name__ == '__main__':
    main()
