"""Preserve the selected basic-sword and paper-GUI Drive assets. Python 3.9+."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ARCHIVE_SHA256 = '0aea9ef5dd3795c0002f2153a17f0b43fce654bbe22261e8c300597091433b73'
MAPPING = 'imports/drive-20261031/mapping.json'
SWORD = 'characters/ink/weapons/sword'
GUI = 'ui/temporary-gui'
SHARE = '임시GUI_도트_적용본_20261031/'
PACKAGE_NAME = 'GUI_도트_적용패키지_20261031.zip'
PACKAGE = SHARE + PACKAGE_NAME
PREFIX = '01_승인에셋/assets/'
SWORD_UNITY = 'unity/Assets/Deviant/Combat/Resources/CharacterApproved11/base_sword/'
GUI_UNITY = 'unity/Assets/Deviant/TemporaryGUI/Resources/TemporaryGUI/'
SWORD_COMMIT = 'b8eabdb28749d5af31678dc2d326ee7144df2d0b'
GUI_COMMIT = 'f86fcf8f53568259cfbca717b40d12429c7fb63f'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def collect(archive):
    """Build a complete, conflict-checked plan before writing any resource."""
    files, records = {}, []

    def add(package, name, target, data, project_path=None):
        if target in files and files[target] != data:
            raise ValueError('Conflicting source bytes: ' + target)
        files[target] = data
        record = {'package': package, 'path': name, 'target': target,
                  'bytes': len(data), 'sha256': digest(data)}
        if project_path:
            record['originalProjectPath'] = project_path
        records.append(record)

    sword_inputs = [PREFIX + SWORD_UNITY + f'F{frame}.png' for frame in range(100, 110)]
    with zipfile.ZipFile(archive) as outer:
        names = [item.filename for item in outer.infolist() if not item.is_dir()]
        if len(names) != len(set(names)):
            raise ValueError('Duplicate outer ZIP entries')
        for name in sword_inputs:
            data = outer.read(name)
            if data[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', data[16:24]) != (256, 144):
                raise ValueError('Unexpected basic-sword PNG: ' + name)
            target = SWORD + '/runtime/basic-two-hit-256/character/' + PurePosixPath(name).name
            add(None, name, target, data, name[len(PREFIX):])
        loose = {
            PACKAGE: 'imports/drive-20261031/packages/' + PACKAGE_NAME,
            SHARE + '00_적용안내.txt': GUI + '/source/paper-gui/00_적용안내.txt',
            SHARE + '단순도트_문양20종_원본.png': GUI + '/source/paper-gui/단순도트_문양20종_원본.png',
            SHARE + '미리보기_팔레트관리.png': GUI + '/preview/paper-gui/growth.png',
        }
        shared = {name for name in names if name.startswith(SHARE)}
        if shared != set(loose):
            raise ValueError('GUI share contents changed; review the import selection')
        for name, target in loose.items():
            add(None, name, target, outer.read(name))
        with zipfile.ZipFile(io.BytesIO(outer.read(PACKAGE))) as nested:
            entries = [item for item in nested.infolist() if not item.is_dir()]
            if len(entries) != 47 or len({item.filename for item in entries}) != len(entries):
                raise ValueError('Unexpected GUI package entry count/duplicates')
            icons, metas = 0, 0
            for item in entries:
                name = item.filename
                if not name.startswith(PACKAGE_NAME[:-4] + '/'):
                    raise ValueError('Unexpected GUI package root: ' + name)
                local = name.split('/', 1)[1]
                data = nested.read(item)
                project_path = None
                if local.startswith(GUI_UNITY):
                    suffix = local[len(GUI_UNITY):]
                    if suffix != 'Pixel.meta' and not suffix.startswith('Pixel/'):
                        raise ValueError('Unexpected GUI runtime path: ' + name)
                    if '..' in PurePosixPath(suffix).parts:
                        raise ValueError('Unsafe GUI package path: ' + name)
                    if suffix.endswith('.png'):
                        if data[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', data[16:24]) != (32, 32):
                            raise ValueError('Unexpected GUI icon PNG: ' + name)
                        icons += 1
                    elif suffix.endswith('.meta'):
                        metas += 1
                    else:
                        raise ValueError('Unexpected GUI runtime file: ' + name)
                    target = GUI + '/runtime/paper-gui/' + suffix
                    project_path = local
                elif local.startswith('실제화면/') and PurePosixPath(local).name in ('bag.png', 'growth.png', 'hud.png', 'shop.png'):
                    target = GUI + '/preview/paper-gui/' + PurePosixPath(local).name
                elif local == '원본/단순도트_문양20종_원본.png':
                    target = GUI + '/source/paper-gui/단순도트_문양20종_원본.png'
                elif local == '00_적용안내.txt':
                    target = GUI + '/source/paper-gui/00_적용안내.txt'
                else:
                    raise ValueError('Unclassified GUI package entry: ' + name)
                add(PACKAGE, name, target, data, project_path)
            if (icons, metas) != (20, 21):
                raise ValueError('GUI package must contain 20 PNGs and 21 original metadata files')
    return files, records, {'files': sword_inputs, 'directories': [SHARE]}


def import_archive(archive, root=ROOT):
    source = archive.read_bytes()
    if digest(source) != ARCHIVE_SHA256:
        raise ValueError('Unexpected Drive ZIP; source identity must be reviewed before importing')
    identity = {'name': archive.name, 'bytes': len(source), 'sha256': digest(source)}
    del source
    files, records, selection = collect(archive)
    mapping = {'format': 'deviant-resource-import-v1', 'archive': identity,
               'selection': selection,
               'mainRepository': 'https://lab.ssafy.com/s15-final/S15P31F109',
               'mainCommits': {'basicSword': SWORD_COMMIT, 'paperGui': GUI_COMMIT},
               'records': sorted(records, key=lambda item: (item['package'] or '', item['path']))}
    files[MAPPING] = encoded(mapping)
    for name, data in files.items():
        target = root / name
        if target.exists() and target.read_bytes() != data:
            raise ValueError('Refusing to overwrite different resource: ' + name)

    catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
    sword = json.loads((root / SWORD / 'asset.json').read_text(encoding='utf-8'))
    additions = sorted(name[len(SWORD) + 1:] for name in files if name.startswith(SWORD + '/'))
    sword['files'] = sorted(set(sword['files']) | set(additions))
    if MAPPING not in sword['sources']:
        sword['sources'].append(MAPPING)
    sword.setdefault('importedSets', {})['basic-two-hit-256'] = {
        'path': 'runtime/basic-two-hit-256/character', 'sourceMapping': MAPPING,
        'mainCommit': SWORD_COMMIT, 'approval': 'not-reassessed-by-import',
        'gameIntegration': 'not-validated-by-import',
        'bindingSources': ['data/combat/presentation/base_sword_attack1.json',
                           'data/combat/presentation/action_motion_registry.json'],
        'unityMetadata': 'owned-by-main; not included in this Drive input'}
    gui = {'format': 'deviant-resource-asset-v1', 'id': 'ui.temporary-gui', 'path': GUI,
           'name': '종이색 임시 GUI의 잉크 문양·원본·실제 화면',
           'status': 'imported-preserved', 'approval': 'not-reassessed-by-import',
           'gameIntegration': 'not-validated-by-import', 'sources': [MAPPING],
           'mainCommit': GUI_COMMIT,
           'files': sorted(name[len(GUI) + 1:] for name in files if name.startswith(GUI + '/'))}
    gui_path = root / GUI / 'asset.json'
    if gui_path.exists() and gui_path.read_bytes() != encoded(gui):
        raise ValueError('Refusing to replace existing GUI asset metadata')
    imports = catalog.setdefault('additionalImportMappings', [])
    if MAPPING not in imports:
        imports.append(MAPPING)
    for asset in (sword, gui):
        item = next((item for item in catalog['assets'] if item['id'] == asset['id']), None)
        if item is None:
            catalog['assets'].append({'id': asset['id'], 'path': asset['path'], 'fileCount': len(asset['files'])})
        elif item['path'] != asset['path']:
            raise ValueError('Conflicting asset identity: ' + asset['id'])
        else:
            item['fileCount'] = len(asset['files'])
    files[SWORD + '/asset.json'] = encoded(sword)
    files[GUI + '/asset.json'] = encoded(gui)
    files['catalog.json'] = encoded(catalog)
    for name, data in files.items():
        target = root / name
        if not target.exists() or target.read_bytes() != data:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    return {'inputRecords': len(records), 'preservedFiles': len({item['target'] for item in records}),
            'swordPngs': 10, 'guiPngs': 20, 'guiMetadataFiles': 21, 'mapping': MAPPING}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    args = parser.parse_args()
    print(json.dumps(import_archive(args.archive), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
