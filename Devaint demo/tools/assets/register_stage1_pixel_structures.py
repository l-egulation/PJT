"""Point the chapter-1 combat structures at the 2x2-dot pixel set and keep their Unity import settings pixel-exact."""
import hashlib
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'environments.stage1-pixel-structures-20261112'
SET = 'environments/stage1-pixel-structures-20261112/runtime/'
FC = 'unity/Assets/ForestCombatV2/Resources/'
SHOP = 'unity/Assets/Deviant/Game/Resources/MaintenanceShops/'

MAP = {
    FC + 'StageRecoveryArt/stage1.png': 'recovery-station.png',
    SHOP + 'stage1-backdrop.png': 'maintenance-backdrop.png',
    SHOP + 'stage1-desk.png': 'maintenance-desk.png',
    SHOP + 'lantern-stage1.png': 'maintenance-lantern.png',   # chapter 2 keeps the shared lantern / sign
    SHOP + 'sign-stage1.png': 'maintenance-sign.png',
    FC + 'Chapter1Art/Exit/exit-closed.png': 'exit-sealed.png',
    FC + 'Chapter1Art/Exit/exit-open.png': 'exit-open.png',
    FC + 'Chapter1Art/InkAltar/altar-before.png': 'altar-before.png',
    FC + 'Chapter1Art/InkAltar/altar-after.png': 'altar-after.png',
}
for name in ('locked-chest-closed', 'locked-chest-open', 'reward-chest-closed', 'reward-chest-open',
             'seal-dormant', 'seal-active', 'seal-broken', 'iron-gate-body', 'iron-gate-tip'):
    MAP[FC + 'Chapter1Gimmicks/' + name + '.png'] = name + '.png'
for folder, src in (('Exit/approved-open', 'exit-open'), ('InkAltar/approved-appear', 'altar-appear'), ('InkAltar/approved-use', 'altar-use'),
                    ('InkAltar/choice-acquire', 'altar-choice-acquire'), ('InkAltar/choice-discard', 'altar-choice-discard')):
    for i in range(1, 9):
        MAP[f'{FC}Chapter1Art/{folder}/{i:02d}.png'] = f'anim/{src}/{i:02d}.png'
# the old exit opening had 12 frames; the pixel one has 8
REMOVE = [f'{FC}Chapter1Art/Exit/approved-open/{i:02d}.png' for i in range(9, 13)]


def pixel_meta(text, pivot=None):
    text = re.sub(r'spritePixelsToUnits: [0-9.]+', 'spritePixelsToUnits: 32', text)
    text = text.replace('enableMipMap: 1', 'enableMipMap: 0')
    text = re.sub(r'filterMode: \d', 'filterMode: 0', text)
    text = re.sub(r'textureCompression: \d', 'textureCompression: 0', text)
    if pivot:
        text = re.sub(r'spritePivot: \{x: [0-9.]+, y: [0-9.]+\}', 'spritePivot: {x: %s, y: %s}' % pivot, text)
    return text


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    old = {row['target']: row for row in manifest['files']}
    for target, src in MAP.items():
        raw = (ROOT / 'resources' / (SET + src)).read_bytes()
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}
        if target in old:
            prior = [old[target]['sha256']] + old[target].get('replaceHashes', [])
            row['replaceHashes'] = sorted(set(prior) - {row['sha256']})
        row['source'] = {'asset': ASSET, 'path': SET + src}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None: manifest['files'].append(row)
        else: manifest['files'][at] = row
        meta = ROOT / (target + '.meta')
        if not meta.exists():
            template = (ROOT / (SHOP + Path(target).name.replace('-stage1', '') + '.meta')).read_text(encoding='utf-8')
            lines = template.splitlines(); lines[1] = 'guid: ' + uuid.uuid4().hex
            meta.write_text(pixel_meta('\n'.join(lines) + '\n'), encoding='utf-8', newline='\n')
        elif target.endswith('StageRecoveryArt/stage1.png'):
            meta.write_text(pixel_meta(meta.read_text(encoding='utf-8'), ('0.5', '0')), encoding='utf-8', newline='\n')
    manifest['files'] = [item for item in manifest['files'] if item['target'] not in REMOVE]
    for target in REMOVE:
        for p in (ROOT / target, ROOT / (target + '.meta')):
            if p.exists(): p.unlink()
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    register()
