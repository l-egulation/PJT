"""Place the chapter-2 seal, gate, chest, key and fall-shadow sprites (the chapter-1 set recoloured to the v26 sewer palette).

Copies resources/environments/stage2-gimmicks-recolor-20261113/runtime into unity/Assets/Chapter2/Resources/Chapter2Gimmicks,
records each file in docs/assets/manifests/unity-deployment.json and gives new files the chapter-1 sprites' import settings
(point filter, no compression, no mipmaps) under a fresh GUID.

    python tools/assets/register_stage2_gimmicks.py
"""
import hashlib
import json
import shutil
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'environments.stage2-gimmicks-recolor-20261113'
SET = 'environments/stage2-gimmicks-recolor-20261113/runtime/'
TARGET = 'unity/Assets/Chapter2/Resources/Chapter2Gimmicks/'
TEMPLATE = 'unity/Assets/ForestCombatV2/Resources/Chapter1Gimmicks/'
# The pieces the seal challenge and locked chest load (Chapter1SealChallenge, Chapter1KeyChest).
NAMES = ('seal-dormant', 'seal-active', 'seal-broken', 'iron-gate-body', 'iron-gate-tip', 'locked-chest-closed',
         'locked-chest-open', 'reward-chest-closed', 'reward-chest-open', 'fall-shadow-decal', 'key-item')


def folder_meta():
    return 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex


def register():
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    target_dir = ROOT / TARGET
    target_dir.mkdir(parents=True, exist_ok=True)
    folder = ROOT / (TARGET.rstrip('/') + '.meta')
    if not folder.exists():
        folder.write_text(folder_meta(), encoding='utf-8', newline='\n')
    for name in NAMES:
        source = ROOT / 'resources' / (SET + name + '.png')
        raw = source.read_bytes()
        target = TARGET + name + '.png'
        shutil.copyfile(source, ROOT / target)
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
               'source': {'asset': ASSET, 'path': SET + name + '.png'}}
        at = next((i for i, item in enumerate(manifest['files']) if item['target'] == target), None)
        if at is None:
            manifest['files'].append(row)
        else:
            prior = [manifest['files'][at]['sha256']] + manifest['files'][at].get('replaceHashes', [])
            replaced = sorted(set(prior) - {row['sha256']})
            if replaced:
                row = {'target': target, 'bytes': row['bytes'], 'sha256': row['sha256'], 'replaceHashes': replaced, 'source': row['source']}
            manifest['files'][at] = row
        meta = ROOT / (target + '.meta')
        if not meta.exists():
            lines = (ROOT / (TEMPLATE + name + '.png.meta')).read_text(encoding='utf-8').splitlines()
            lines[1] = 'guid: ' + uuid.uuid4().hex
            meta.write_text('\n'.join(lines) + '\n', encoding='utf-8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    register()
