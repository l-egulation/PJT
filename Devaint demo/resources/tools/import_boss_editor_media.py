"""Preserve the three remaining committed boss/editor media payloads without editing main."""
import argparse
import json
from pathlib import Path
import subprocess

from assets import json_bytes, safe_path, sha
from verify_resources import LFS_HEADER

ROOT = Path(__file__).resolve().parents[1]
MAPPING = 'imports/main-boss-map-editor/mapping.json'
BOSS = 'monsters/stage-01/bosses/clock-executioner/runtime/stationary'
EDITOR = 'authoring/map-editor'
SELECTIONS = (
    ('unity/Assets/ForestCombatV2/Resources/ClockExecutioner/StationaryBytes.bytes',
     'monsters.stage-01.bosses.clock-executioner.stationary', BOSS, 'StationaryBytes.bytes'),
    ('tools/chapter1-map-editor/맵 편집기.html',
     'authoring.map-editor', EDITOR, 'runtime/맵 편집기.html'),
    ('tools/chapter1-map-editor/이전_지형용_편집기.html',
     'authoring.map-editor', EDITOR, 'runtime/이전_지형용_편집기.html'),
)


def import_media(repo, revision='HEAD', root=ROOT, check=False):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(repo), *args])
    commit = git('rev-parse', '--verify', revision + '^{commit}').decode().strip()
    files, records, groups = {}, [], {}
    for name, asset_id, folder, local in SELECTIONS:
        data = git('show', commit + ':' + name)
        if data.startswith(LFS_HEADER):
            raise ValueError('Source is an unrestored LFS pointer: ' + name)
        if safe_path(repo, name).read_bytes() != data:
            raise ValueError('Uncommitted source edits preserved: ' + name)
        target = folder + '/' + local
        record = {'package': None, 'path': name, 'target': target,
                  'originalProjectPath': name, 'bytes': len(data), 'sha256': sha(data)}
        if name.startswith('unity/'):
            if not data.startswith(b'\x89PNG\r\n\x1a\n'):
                raise ValueError('Boss TextAsset must preserve its original PNG bytes')
            meta = git('show', commit + ':' + name + '.meta')
            if safe_path(repo, name + '.meta').read_bytes() != meta:
                raise ValueError('Uncommitted Unity metadata edits preserved: ' + name)
            record['unityMetaSha256'] = sha(meta)
        files[target] = data
        records.append(record)
        groups.setdefault((asset_id, folder), []).append(local)
    files[MAPPING] = json_bytes({
        'format': 'deviant-resource-import-v1', 'sourceType': 'git-source',
        'sourceRepository': 'https://lab.ssafy.com/s15-final/S15P31F109',
        'sourceCommit': commit, 'scope': 'stationary-boss-png-bytes-and-offline-map-editors',
        'records': records})
    catalog = json.loads(safe_path(root, 'catalog.json').read_text(encoding='utf-8'))
    payloads = json.loads(safe_path(root, '.lfs-payloads.json').read_text(encoding='utf-8'))
    for (asset_id, folder), names in groups.items():
        entry = {'id': asset_id, 'path': folder, 'fileCount': len(names)}
        matches = [a for a in catalog['assets'] if a['id'] == asset_id or a['path'] == folder]
        if matches and matches != [entry]:
            raise ValueError('Conflicting resource group: ' + folder)
        if not matches:
            catalog['assets'].append(entry)
        files[folder + '/asset.json'] = json_bytes({
            'format': 'deviant-resource-asset-v1', 'id': asset_id, 'path': folder,
            'status': 'preserved-from-main', 'approval': 'not-reassessed-by-migration',
            'gameIntegration': 'requires-consumer-deployment-validation',
            'sources': [MAPPING], 'files': sorted(names)})
        for name in names:
            target = folder + '/' + name
            if name.endswith('.html') and target not in payloads:
                payloads.append(target)
    mappings = catalog.setdefault('additionalImportMappings', [])
    if MAPPING not in mappings:
        mappings.append(MAPPING)
    for name, data in files.items():
        target = safe_path(root, name)
        if target.exists() and (not target.is_file() or target.read_bytes() != data):
            raise ValueError('Refusing to overwrite different resource: ' + name)
    if not check:
        for name, data in files.items():
            target = safe_path(root, name)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        safe_path(root, 'catalog.json').write_bytes(json_bytes(catalog))
        safe_path(root, '.lfs-payloads.json').write_bytes(json_bytes(sorted(payloads)))
    return {'mediaFiles': len(records), 'assetGroups': len(groups), 'checkOnly': check}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=Path, required=True)
    parser.add_argument('--revision', default='HEAD')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(import_media(args.source_repo, args.revision, check=args.check), indent=2))
