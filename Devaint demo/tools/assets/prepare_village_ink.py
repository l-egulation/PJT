"""Build main-owned village-ink texture bindings and deployment selection; preserve original PNG bytes."""
from pathlib import Path
import hashlib
import json
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
GROUP = 'ui/village-ink-return-20261107'
ASSET = 'ui.village-ink-return-20261107'
DEST = 'unity/Assets/Deviant/Game/Resources/VillageInk'
NAMESPACE = uuid.UUID('a0c498fe-a651-467e-b0ef-6cc9720c57c6')


def dump(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def meta(path, folder=False):
    file = path.with_name(path.name + '.meta')
    if file.exists():
        return
    guid = uuid.uuid5(NAMESPACE, path.relative_to(ROOT).as_posix()).hex
    if folder:
        text = f'fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    elif path.suffix == '.png':
        text = f'fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n  externalObjects: {{}}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n  textureSettings:\n    filterMode: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  maxTextureSize: 2048\n  textureType: 0\n  alphaIsTransparency: 1\n  textureCompression: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        importer = 'MonoImporter' if path.suffix == '.cs' else 'TextScriptImporter'
        text = f'fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    file.write_text(text, encoding='utf-8')


def main():
    base = ROOT / 'resources' / GROUP
    layout = json.loads((base/'runtime/layout.json').read_text(encoding='utf-8'))
    contract_path = ROOT/'docs/assets/manifests/unity-deployment.json'
    contract = json.loads(contract_path.read_text(encoding='utf-8'))
    contract['files'] = [e for e in contract['files'] if e['source']['asset'] != ASSET]
    images, frames = [], []
    records = []
    for color, item in layout['inks'].items():
        for state in ('closed', 'open', 'cap', 'empty'):
            records.append(item[state])
        records.append(item['drop'])
        frame = item['frame']
        frames.append({'color': color, 'width': frame['width'], 'height': frame['height'],
                       'pour': [frame['pourOriginPixel'][0]/frame['width'], frame['pourOriginPixel'][1]/frame['height']]})
    records.extend(layout['ui'].values())
    for record in records:
        source = record['file']
        relative = source.removeprefix('runtime/')
        destination = ROOT/DEST/relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        raw = (base/source).read_bytes()
        sha = hashlib.sha256(raw).hexdigest()
        if sha != record['sha256']:
            raise ValueError('Source differs from reviewed layout: '+source)
        meta(destination)
        contract['files'].append({'target': f'{DEST}/{relative}', 'bytes': len(raw), 'sha256': sha,
            'source': {'asset': ASSET, 'path': f'{GROUP}/{source}'}})
        images.append({'id': relative[:-4], 'resource': 'VillageInk/'+relative[:-4],
            'width': record['width'], 'height': record['height'], 'bounds': record['contentBounds'],
            'crop': not relative.startswith('acquisition/')})
    dump(contract_path, contract)
    bindings = ROOT/DEST/'Bindings.json'
    dump(bindings, {'schemaVersion': 1, 'images': images, 'frames': frames})
    meta(bindings)
    for path in sorted((ROOT/DEST).rglob('*')):
        if path.is_dir():
            meta(path, True)
    meta(ROOT/DEST, True)
    for relative in ('unity/Assets/Deviant/Game/Runtime/VillageInkAssets.cs',
                     'unity/Assets/Deviant/Game/Runtime/VillageInkRevealView.cs',
                     'unity/Assets/Deviant/Game/Runtime/TemporaryGameGui.VillageInk.cs',
                     'unity/Assets/Deviant/Game/Editor/VillageInkTextureImporter.cs'):
        meta(ROOT/relative)
    # Use the existing compiler's story validation, without running its unrelated pinned-media deployment.
    sys.path.insert(0, str(ROOT/'tools/assets'))
    from build_hub_bindings import visits_text, note_text
    (ROOT/'unity/Assets/Deviant/Hub/Resources/HubVisits.json').write_text(visits_text(), encoding='utf-8')
    (ROOT/'unity/Assets/Deviant/Hub/Resources/HubRecordNote.json').write_text(note_text(), encoding='utf-8')
    print(f'Prepared {len(images)} village ink texture bindings and matching main-owned metadata.')


if __name__ == '__main__':
    main()
