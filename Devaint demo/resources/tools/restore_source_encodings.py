"""Recover the exact CRLF source identity declared by the existing boss media bindings."""
import json
from pathlib import Path

from assets import ROOT, Store, json_bytes, sha

SOURCE = 'authoring/workbench/source/MonsterReference/local-assets/hanging-clock-boss-battle-v1/assets/asset-manifest.json'
TARGET = 'monsters/stage-01/bosses/clock-executioner/source/battle-preview/asset-manifest.json'
EXPECTED = '42b0f9795dcb53d8231a115f665614537aa11a895e436ea44bfab84e4ffedd87'
MAPPING = 'imports/source-encodings/mapping.json'


def restore(root=ROOT):
    raw = Store(root).read({'asset': 'authoring.workbench', 'path': SOURCE})
    if b'\r' in raw:
        raise ValueError('Expected the identified LF source, not an arbitrary reserialization')
    data = raw.replace(b'\n', b'\r\n')
    if sha(data) != EXPECTED or json.loads(raw) != json.loads(data):
        raise ValueError('Recovered source must exactly match the existing consumer identity and JSON meaning')
    record = {'package': None, 'path': 'clock-preview-crlf', 'target': TARGET,
              'bytes': len(data), 'sha256': EXPECTED}
    mapping = {'format': 'deviant-resource-import-v1', 'sourceType': 'encoding-recovery',
               'recipe': {'source': SOURCE, 'sourceSha256': sha(raw), 'operation': 'LF-to-CRLF',
                          'consumer': 'ClockExecutionerPreview/preview.json and ClockExecutionerTelegraph/telegraphs.json'},
               'records': [record]}
    for name, payload in ((TARGET, data), (MAPPING, json_bytes(mapping))):
        path = root / name
        if path.exists() and path.read_bytes() != payload:
            raise ValueError('Preserve different source encoding output: ' + name)
    catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
    if MAPPING not in catalog.setdefault('additionalImportMappings', []):
        catalog['additionalImportMappings'].append(MAPPING)
    group = 'monsters/stage-01/bosses/clock-executioner'
    asset_path = root / group / 'asset.json'
    asset = json.loads(asset_path.read_text(encoding='utf-8'))
    name = TARGET[len(group) + 1:]
    if name not in asset['files']:
        asset['files'].append(name)
        asset['files'].sort()
    if MAPPING not in asset['sources']:
        asset['sources'].append(MAPPING)
    next(item for item in catalog['assets'] if item['path'] == group)['fileCount'] = len(asset['files'])
    for name, payload in ((TARGET, data), (MAPPING, json_bytes(mapping)),
                          (group + '/asset.json', json_bytes(asset)), ('catalog.json', json_bytes(catalog))):
        path = root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)
    print('Recovered exact existing boss source hash:', EXPECTED, 'bytes:', len(data))


if __name__ == '__main__':
    restore()
