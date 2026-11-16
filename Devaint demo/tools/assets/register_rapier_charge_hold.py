"""Select held rapier art in the main-owned Unity deployment contract."""
import hashlib
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'characters.ink.weapons.rapier'
SOURCE = 'characters/ink/weapons/rapier/runtime/charge-hold-direct-20261112'
TARGET = 'unity/Assets/Deviant/Combat/Resources/CharacterRapierChargeHold'


def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant/rapier-charge-hold/' + name).hex


def main():
    path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(path.read_text(encoding='utf-8'))
    template = (ROOT / 'unity/Assets/Deviant/Combat/Resources/CharacterRapierMotion/charge/002.png.meta').read_text(encoding='utf-8')
    entries = []
    for kind, count in [('prepare', 4), ('hold', 8)]:
        for index in range(count):
            source = f'{SOURCE}/{kind}/{index:02d}.png'
            target = f'{TARGET}/{kind}/{index:02d}.png'
            raw = (ROOT / 'resources' / source).read_bytes()
            entries.append(dict(target=target, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest(), source=dict(asset=ASSET, path=source)))
            text = re.sub(r'(?m)^guid: .*$', 'guid: ' + guid(target), template)
            # Preserve the approved 640x360 special's PPU and foot anchor.
            text = re.sub(r'spritePivot: .*', 'spritePivot: {x: 0.34375, y: 0.092593}', text)
            text = re.sub(r'spritePixelsToUnits: .*', 'spritePixelsToUnits: 106.6667', text)
            text = re.sub(r'spriteMeshType: .*', 'spriteMeshType: 1', text)
            text = re.sub(r'filterMode: .*', 'filterMode: 0', text)
            text = re.sub(r'textureCompression: .*', 'textureCompression: 0', text)
            text = re.sub(r'crunchedCompression: .*', 'crunchedCompression: 0', text)
            text = re.sub(r'spriteID: .*', 'spriteID: ' + guid(target + '/sprite'), text)
            meta = ROOT / (target + '.meta'); meta.parent.mkdir(parents=True, exist_ok=True)
            meta.write_text(text, encoding='utf-8')
    manifest['files'] = [item for item in manifest['files'] if not item['target'].startswith(TARGET + '/')] + entries
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for name in [TARGET, TARGET + '/prepare', TARGET + '/hold']:
        meta = ROOT / (name + '.meta')
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid(name)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n', encoding='utf-8')
    print('Selected 12 charge preparation/hold frames; existing attack frames preserved')


if __name__ == '__main__':
    main()
