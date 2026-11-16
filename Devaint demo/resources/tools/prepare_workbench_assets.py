"""Resolve unique recorded authoring paths inside resources; never recreate old main folders."""
import json
from pathlib import Path

from assets import ROOT, Store, json_bytes, safe_path, sha

MAPPING = 'imports/workbench-assets/mapping.json'
PREFIX = 'authoring/workbench/source/'
ROOTS = ('WeaponReference/', 'MonsterReference/', 'ForestReference/', 'StoryReference/', 'StoryPreview/', '아트/')


def prepare(root=ROOT):
    catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
    store = Store(root)
    candidates = {}
    for name in [catalog['importMapping'], *catalog.get('additionalImportMappings', [])]:
        if name == MAPPING:
            continue
        for item in json.loads((root / name).read_text(encoding='utf-8'))['records']:
            original = item.get('originalProjectPath', '')
            if original.startswith(ROOTS) and item['target'] in store.owners:
                candidates.setdefault(original, {}).setdefault(item['sha256'], item['target'])
    payloads, records, ambiguous = {}, [], []
    for original, versions in sorted(candidates.items()):
        target = PREFIX + original
        path = safe_path(root, target)
        if path.exists():
            continue  # A preserved main source takes precedence; never overwrite it with another set.
        if len(versions) != 1:
            ambiguous.append(original)
            continue
        digest, source = next(iter(versions.items()))
        data = store.read({'asset': store.owners[source], 'path': source})
        records.append({'package': None, 'path': original, 'target': target, 'bytes': len(data),
                        'sha256': digest, 'sourceResource': source})
        payloads[target] = data
    # Keep previous aliases if run again; they remain independently indexed and hash-checked.
    existing = root / MAPPING
    if existing.is_file():
        old = json.loads(existing.read_text(encoding='utf-8'))
        records.extend(old['records'])
    by_target = {item['target']: item for item in records}
    mapping = {'format': 'deviant-resource-import-v1', 'sourceType': 'resource-alias',
               'records': [by_target[name] for name in sorted(by_target)]}
    asset_path = root / 'authoring/workbench/asset.json'
    asset = json.loads(asset_path.read_text(encoding='utf-8'))
    asset['files'] = sorted(set(asset['files']) | {name[len('authoring/workbench/'):] for name in by_target})
    next(item for item in catalog['assets'] if item['id'] == asset['id'])['fileCount'] = len(asset['files'])
    if MAPPING not in catalog.setdefault('additionalImportMappings', []):
        catalog['additionalImportMappings'].append(MAPPING)
    for name, data in payloads.items():
        path = safe_path(root, name)
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open('xb') as stream:
            stream.write(data)
    for name, data in ((MAPPING, json_bytes(mapping)), ('authoring/workbench/asset.json', json_bytes(asset)),
                       ('catalog.json', json_bytes(catalog))):
        path = root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    print('Indexed authoring image aliases:', len(by_target), 'unselected ambiguous paths:', len(ambiguous))


if __name__ == '__main__':
    prepare()
