"""Prepare the approved item PNG selection and Unity sprite import settings.

Run after resources/ui/inventory-symbols-20261106/runtime/items.json is finalized.
Media bytes are deployed only through update_assets.py; this tool never edits PNGs.
"""
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'ui.inventory-symbols-20261106'
GROUP = ROOT / 'resources/ui/inventory-symbols-20261106'
TARGET = ROOT / 'unity/Assets/Deviant/TemporaryGUI/Resources/ApprovedItemIcons'


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant/' + path.relative_to(ROOT).as_posix()).hex


def main():
    catalog = json.loads((GROUP / 'runtime/items.json').read_text(encoding='utf-8'))
    if len(catalog['items']) != 71:
        raise ValueError('Expected 64 items and 7 blank backings')
    records = []
    for item in catalog['items']:
        source = GROUP / item['file']
        data = source.read_bytes()
        if hashlib.sha256(data).hexdigest() != item['sha256']:
            raise ValueError('Source changed: ' + item['id'])
        records.append(dict(target=(TARGET / source.name).relative_to(ROOT).as_posix(),
                            bytes=len(data), sha256=item['sha256'],
                            source=dict(asset=ASSET, path=source.relative_to(ROOT / 'resources').as_posix())))
    TARGET.mkdir(parents=True, exist_ok=True)
    meta = Path(str(TARGET) + '.meta')
    if not meta.exists():
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid(TARGET)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n', encoding='utf-8')
    for record in records:
        target = ROOT / record['target']
        meta = Path(str(target) + '.meta')
        if meta.exists():
            continue
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid(target)}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  maxTextureSize: 512
  textureSettings:
    filterMode: 1
    wrapU: 1
    wrapV: 1
  nPOTScale: 0
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 100
  spritePivot: {{x: 0.5, y: 0.5}}
  alphaIsTransparency: 1
  textureType: 8
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 512
    textureFormat: -1
    textureCompression: 0
''', encoding='utf-8')
    contract_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    contract = json.loads(contract_path.read_text(encoding='utf-8'))
    contract['files'] = [r for r in contract['files'] if r['source']['asset'] != ASSET] + records
    contract_path.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Prepared {len(records)} item sprites; run update_assets.py for media deployment.')


if __name__ == '__main__':
    main()
