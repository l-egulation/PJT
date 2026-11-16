"""Select the user's hammer/rapier basics; preserve gameplay impact and total source times."""
import hashlib
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
GROUP = 'characters/ink/weapons/basic-outline-20261111'
ASSET = 'characters.ink.weapons.basic-outline'
RUNTIME = 'runtime/rapier-blade-revised-20261112'
HAMMER_RUNTIME = 'runtime/hammer-current-motion-20261112'
# First contact pictures: horizontal hammer hit, ground slam, then three thrusts.
CLIPS = [('hammer', 'attack1', 'hammer-a', 0, 9, 7), ('hammer', 'attack2', 'hammer-a', 9, 21, 15),
         ('rapier', 'attack1', 'rapier', 0, 9, 3), ('rapier', 'attack2', 'rapier', 9, 16, 11),
         ('rapier', 'attack3', 'rapier', 16, 24, 17)]


def read(path): return json.loads(path.read_text(encoding='utf-8'))
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
def guid(name): return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant/basic-outline/' + name).hex


def main():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = read(manifest_path)
    registry_path = ROOT / 'data/combat/presentation/action_motion_registry.json'
    registry = read(registry_path)
    heavy_path = ROOT / 'unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json'
    heavy = read(heavy_path)
    entries = []
    for form, clip, folder, start, end, contact in CLIPS:
        refs = [f'CharacterBasicOutline/{folder}/{i:02d}' for i in range(start, end)]
        palette = next(p for p in registry['palettes'] if p['id'] == f'{form}/{clip}')
        motion = next(m for m in registry['motions'] if m['id'] == palette['id'])
        duration = sum(step['seconds'] for step in motion['steps'])
        impact = motion['sourceImpact']
        before = contact - start
        # Subdivide each side of the existing contact; gameplay timing remains owned by the registry.
        times = [impact / before] * before + [(duration - impact) / (end - contact)] * (end - contact)
        if form == 'rapier':
            palette['frames'] = refs
            motion['steps'] = [{'id': f'{form}-{clip}-outline-{i:02d}', 'frame': ref, 'seconds': round(seconds, 9)}
                               for i, (ref, seconds) in enumerate(zip(refs, times), start)]
        if form == 'hammer':
            body = next(c for s in heavy['sets'] if s['form'] == form for c in s['clips'] if c['name'] == clip)
            body.update(frames=refs, seconds=[s['seconds'] for s in motion['steps']])
        template_path = 'CharacterHammerReview/hammer/attack_1_01' if form == 'hammer' else 'CharacterRapierMotion/basic/007'
        template = (ROOT / f'unity/Assets/Deviant/Combat/Resources/{template_path}.png.meta').read_text(encoding='utf-8')
        for i, ref in enumerate(refs, start):
            runtime = HAMMER_RUNTIME if form == 'hammer' else RUNTIME
            source = f'{GROUP}/{runtime}/{folder}/{i:02d}.png'
            raw = (ROOT / 'resources' / source).read_bytes()
            target = f'unity/Assets/Deviant/Combat/Resources/{ref}.png'
            entries.append({'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(), 'source': {'asset': ASSET, 'path': source}})
            meta = ROOT / (target + '.meta'); meta.parent.mkdir(parents=True, exist_ok=True)
            if not meta.exists():
                px = 104 if form == 'hammer' else 72
                text = re.sub(r'(?m)^guid: .*$', 'guid: ' + guid(target), template)
                text = re.sub(r'spritePivot: .*', f'spritePivot: {{x: {px / 256}, y: {16 / 144}}}', text)
                text = re.sub(r'spritePixelsToUnits: .*', 'spritePixelsToUnits: ' + ('32' if form == 'hammer' else '35'), text)
                text = re.sub(r'spriteMeshType: .*', 'spriteMeshType: 1', text)
                text = re.sub(r'filterMode: .*', 'filterMode: 0', text)
                text = re.sub(r'spriteID: .*', 'spriteID: ' + guid(target + '/sprite'), text)
                meta.write_text(text, encoding='utf-8')
            if form == 'rapier' and meta.exists():
                text = meta.read_text(encoding='utf-8')
                text = re.sub(r'spritePivot: .*', 'spritePivot: {x: 0.28125, y: 0.1111111111111111}', text)
                text = re.sub(r'spritePixelsToUnits: .*', 'spritePixelsToUnits: 35', text)
                meta.write_text(text, encoding='utf-8')
    prefix = 'unity/Assets/Deviant/Combat/Resources/CharacterBasicOutline/'
    manifest['files'] = [f for f in manifest['files'] if not f['target'].startswith(prefix)] + entries
    write(manifest_path, manifest)
    write(registry_path, registry)
    write(heavy_path, heavy)
    base = ROOT / prefix
    for directory in [base, base / 'hammer-a', base / 'rapier']:
        meta = Path(str(directory) + '.meta')
        if not meta.exists():
            meta.write_text(f'fileFormatVersion: 2\nguid: {guid(directory.relative_to(ROOT).as_posix())}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n', encoding='utf-8')
    print(f'Selected {len(entries)} original PNGs across five basic attacks')


if __name__ == '__main__': main()
