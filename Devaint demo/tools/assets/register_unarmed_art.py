"""Register approved unarmed PNG bytes and main-owned import settings; transport separately.

The current two-hit basic is the straight-jab set (resources source/jab-combo.json, 2026-11-12):
body frames at 640x360, feet (220,327), 1 unit = 106.6667 px, plus the white crescent impact
effect at PPU32 (runtime/jab-fx/fx.json). The previous approved-combo set stays in the resource
repository and is no longer delivered.
"""
from pathlib import Path
import hashlib
import json
import re
import struct
import uuid

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'characters.ink.weapons.unarmed'
GROUP = 'characters/ink/weapons/unarmed'
TARGET = 'unity/Assets/Deviant/Combat/Resources/UnarmedAttack'


def _unity_float(value):
    """Shortest text that reads back as the same 32-bit float, as Unity serializes it (11/120 -> 0.09166667).

    Writing Python's full double repr makes Unity rewrite the meta on its first import."""
    single = struct.unpack('f', struct.pack('f', value))[0]
    for digits in range(1, 10):
        text = f'{single:.{digits}g}'
        if struct.unpack('f', struct.pack('f', float(text)))[0] == single:
            return text
    return f'{single:.9g}'


def _meta(template, target, ppu, pivot):
    value = re.sub(r'(?m)^guid: .*$', 'guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'deviant-unarmed/'+target).hex, template)
    value = re.sub(r'spritePivot: .*', f'spritePivot: {{x: {_unity_float(pivot[0])}, y: {_unity_float(pivot[1])}}}', value)
    value = re.sub(r'spritePixelsToUnits: .*', f'spritePixelsToUnits: {ppu}', value)
    value = re.sub(r'textureCompression: \d+', 'textureCompression: 0', value)
    value = re.sub(r'textureFormat: 25', 'textureFormat: -1', value)
    return re.sub(r'overridden: 1', 'overridden: 0', value)


def _folder(root, folder):
    meta = root/(folder+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'deviant-unarmed/'+folder).hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', encoding='utf-8')


def selection(root=ROOT):
    """(name, source, target, ppu, pivot) for every delivered unarmed sprite."""
    root = Path(root)
    body = json.loads((root/'resources'/GROUP/'source/jab-combo.json').read_text(encoding='utf-8'))
    fx = json.loads((root/'resources'/GROUP/'runtime/jab-fx/fx.json').read_text(encoding='utf-8'))
    width, height = body['canvas']; px, py = body['pivotFromTop']
    items = [(f'jab-combo/frame-{number:02}', body['pixelsPerUnit'], (px/width, 1-py/height)) for number in sum(body['frames'], [])]
    items += [(f'jab-fx/{clip}/{i:02}', fx['pixelsPerUnit'], tuple(fx['pivot'])) for clip in fx['clips'] for i in range(fx['frames'])]
    return [(name, GROUP+'/runtime/'+name+'.png', TARGET+'/'+name+'.png', ppu, pivot) for name, ppu, pivot in items]


def register(root=ROOT):
    root = Path(root)
    template = (root/'unity/Assets/Deviant/Combat/Resources/UnarmedBody/idle/F01.png.meta').read_text(encoding='utf-8')
    entries = []
    for name, source, target, ppu, pivot in selection(root):
        raw = (root/'resources'/source).read_bytes()
        if not raw.startswith(b'\x89PNG\r\n\x1a\n'):
            raise ValueError('Restore selected PNG from Git LFS: '+source)
        entries.append(dict(target=target,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest(),source=dict(asset=ASSET,path=source)))
        meta = root/(target+'.meta')
        meta.parent.mkdir(parents=True, exist_ok=True)
        if not meta.exists():
            meta.write_text(_meta(template, target, ppu, pivot), encoding='utf-8')
    for folder in sorted({str(Path(e['target']).parent).replace('\\', '/') for e in entries} | {TARGET, TARGET+'/jab-fx'}):
        _folder(root, folder)
    path = root/'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(path.read_text(encoding='utf-8'))
    manifest['files'] = [e for e in manifest['files'] if not e['target'].startswith(TARGET+'/')]+entries
    path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n', encoding='utf-8')
    return len(entries)


if __name__=='__main__':
    print(f'Registered {register()} native-resolution unarmed sprites; deploy with tools/assets/update_assets.py.')
