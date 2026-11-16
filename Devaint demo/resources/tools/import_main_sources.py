"""Import selected authoring sources from an identified main Git revision, never a Drive folder."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from assets import ROOT, json_bytes, safe_path

GROUP = 'authoring/workbench'
MAPPING = 'imports/main-authoring/mapping.json'
PREFIXES = ('WeaponReference/', 'MonsterReference/', 'ForestReference/',
            'StoryReference/', 'StoryPreview/', '아트/')
RETIRED_TOOLS = ('restore.py', 'restore-assets.py', 'refresh.py', 'bundle.py', 'package.py',
                 'package_preview.py', 'package-approved-assets.py', 'export-approved-motion-gifs.py')


def import_sources(main, root=ROOT):
    main = main.resolve()
    top = subprocess.check_output(['git', '-C', str(main), 'rev-parse', '--show-toplevel'], text=True).strip()
    if Path(top).resolve() != main:
        raise ValueError('--main-repo must be the actual Git checkout root')
    commit = subprocess.check_output(['git', '-C', str(main), 'rev-parse', 'HEAD'], text=True).strip()
    names = subprocess.check_output(['git', '-C', str(main), 'ls-files', '-z']).decode().split('\0')
    records, payloads, embedded = [], {}, []
    gui_prefix = 'unity/Assets/Deviant/TemporaryGUI/Resources/TemporaryGUI/'
    feedback_prefix = 'unity/Assets/Deviant/Combat/Resources/CombatFeelArt/'
    selected = [name for name in names if name.startswith(PREFIXES)
                or name.startswith('docs/assets/manifests/')
                or (name.startswith(gui_prefix) and name.endswith('.png') and '/Pixel/' not in name)
                or (name.startswith(feedback_prefix) and name.endswith('.png'))]
    for name in sorted(selected):
        path = Path(name)
        # Old distribution instructions and transport programs are retired, not archived as new guides.
        if (path.suffix.lower() == '.md' or path.name in RETIRED_TOOLS
                or path.name in ('.gitignore', '.gitattributes')):
            continue
        data = safe_path(main, name).read_bytes()
        target = ('ui/temporary-gui/runtime/shared-frames/' + path.name if name.startswith(gui_prefix)
                  else 'effects/combat-feedback/runtime/' + name[len(feedback_prefix):]
                  if name.startswith(feedback_prefix) else GROUP + '/source/' + name)
        payloads[target] = data
        records.append({'package': None, 'path': name, 'target': target,
                        'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                        'originalProjectPath': name})
        if path.suffix.lower() in ('.html', '.js', '.json') and b'data:image/' in data:
            embedded.append(target)
    mapping = {'format': 'deviant-resource-import-v1', 'sourceRepository':
               'https://lab.ssafy.com/s15-final/S15P31F109', 'sourceCommit': commit, 'records': records}
    payloads[MAPPING] = json_bytes(mapping)
    for name, data in payloads.items():
        target = safe_path(root, name)
        if name == MAPPING and target.exists():
            old = json.loads(target.read_text(encoding='utf-8'))
            new_by_path = {item['path']: item for item in records}
            if old.get('sourceCommit') != commit or any(new_by_path.get(item['path']) != item for item in old['records']):
                raise ValueError('An existing source import cannot be replaced by another revision')
            continue
        if target.exists() and target.read_bytes() != data:
            raise ValueError('Preserve a different existing authoring source: ' + name)
    catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
    asset = {'format': 'deviant-resource-asset-v1', 'id': 'authoring.workbench', 'path': GROUP,
             'name': '공통 제작 도구·편집 데이터·미리보기 원본', 'status': 'imported-preserved',
             'approval': 'not-reassessed-by-import', 'gameIntegration': 'not-validated-by-import',
             'sources': [MAPPING], 'files': [name[len(GROUP) + 1:] for name in sorted(payloads)
                                          if name.startswith(GROUP + '/')]}
    catalog['assets'] = [item for item in catalog['assets'] if item['id'] != asset['id']]
    catalog['assets'].append({'id': asset['id'], 'path': GROUP, 'fileCount': len(asset['files'])})
    if MAPPING not in catalog.setdefault('additionalImportMappings', []):
        catalog['additionalImportMappings'].append(MAPPING)
    gui_path = root / 'ui/temporary-gui/asset.json'
    gui = json.loads(gui_path.read_text(encoding='utf-8'))
    gui['files'] = sorted(set(gui['files']) | {name[len('ui/temporary-gui/'):] for name in payloads
                                           if name.startswith('ui/temporary-gui/')})
    if MAPPING not in gui['sources']:
        gui['sources'].append(MAPPING)
    next(item for item in catalog['assets'] if item['id'] == gui['id'])['fileCount'] = len(gui['files'])
    feedback = {'format': 'deviant-resource-asset-v1', 'id': 'effects.combat-feedback',
                'path': 'effects/combat-feedback', 'status': 'imported-preserved',
                'approval': 'not-reassessed-by-import', 'gameIntegration': 'not-validated-by-import',
                'sources': [MAPPING], 'files': sorted(name[len('effects/combat-feedback/'):] for name in payloads
                                                     if name.startswith('effects/combat-feedback/'))}
    catalog['assets'] = [item for item in catalog['assets'] if item['id'] != feedback['id']]
    catalog['assets'].append({'id': feedback['id'], 'path': feedback['path'], 'fileCount': len(feedback['files'])})
    lfs = json.loads((root / '.lfs-payloads.json').read_text(encoding='utf-8'))
    payloads[GROUP + '/asset.json'] = json_bytes(asset)
    payloads['ui/temporary-gui/asset.json'] = json_bytes(gui)
    payloads['effects/combat-feedback/asset.json'] = json_bytes(feedback)
    payloads['catalog.json'] = json_bytes(catalog)
    payloads['.lfs-payloads.json'] = json_bytes(sorted(set(lfs) | set(embedded)))
    for name, data in payloads.items():
        target = safe_path(root, name)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return {'sourceCommit': commit, 'files': len(records), 'embeddedPayloads': len(embedded)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--main-repo', type=Path, required=True)
    print(json.dumps(import_sources(parser.parse_args().main_repo), ensure_ascii=False, indent=2))
