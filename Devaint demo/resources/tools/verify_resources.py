"""Verify the organized assets and, optionally, every source ZIP entry. Python 3.9+."""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
from html.parser import HTMLParser
import io
import json
from pathlib import Path, PurePosixPath
import posixpath
import sys
from urllib.parse import unquote, urlsplit
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LFS_HEADER = b'version https://git-lfs.github.com/spec/v1\n'


def relative(value):
    parts = PurePosixPath(value).parts
    if (not value or value.startswith('/') or '\\' in value or ':' in value
            or '/'.join(parts) != value or any(p in ('.', '..', '.git') for p in parts)):
        raise ValueError('Invalid repository path: ' + value)
    return value


def digest(data):
    return hashlib.sha256(data).hexdigest()


class References(HTMLParser):
    def __init__(self):
        super().__init__()
        self.paths = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ('src', 'href') and value:
                url = urlsplit(value)
                if not url.scheme and not url.netloc and url.path:
                    self.paths.append(unquote(url.path))


def selected_input(selection, package, path):
    if selection is None:
        return True
    name = package or path
    return (name in selection.get('files', [])
            or any(name.startswith(folder) for folder in selection.get('directories', [])))


def import_selection(mapping):
    selection = mapping.get('selection')
    if selection is None:
        return None
    if (not isinstance(selection, dict) or set(selection) - {'files', 'directories'}
            or not any(selection.get(key) for key in ('files', 'directories'))):
        raise ValueError('Invalid import selection')
    for key in ('files', 'directories'):
        names = selection.get(key, [])
        if not isinstance(names, list) or len(names) != len(set(names)):
            raise ValueError('Invalid import selection: ' + key)
        for name in names:
            relative(name[:-1] if key == 'directories' and name.endswith('/') else name)
            if key == 'directories' and not name.endswith('/'):
                raise ValueError('Selected directory must end with /: ' + name)
    return selection


def input_entries(archive, selection=None):
    with zipfile.ZipFile(archive) as outer:
        for item in outer.infolist():
            if item.is_dir():
                continue
            if not selected_input(selection, None, item.filename):
                continue
            data = outer.read(item)
            yield (None, item.filename), data
            if item.filename.endswith('.zip'):
                with zipfile.ZipFile(io.BytesIO(data)) as nested:
                    for child in nested.infolist():
                        if not child.is_dir():
                            yield (item.filename, child.filename), nested.read(child)


def verify(root, archive=None, check_links=False, additional_archives=()):
    catalog = json.loads((root / 'catalog.json').read_text(encoding='utf-8'))
    if catalog.get('format') != 'deviant-resource-catalog-v1':
        raise ValueError('Unsupported catalog')
    mapping_paths = [relative(catalog['importMapping'])]
    mapping_paths.extend(relative(name) for name in catalog.get('additionalImportMappings', []))
    if len(mapping_paths) != len(set(mapping_paths)):
        raise ValueError('Duplicate import mapping')
    imports = []
    expected = {}
    for mapping_path in mapping_paths:
        mapping = json.loads((root / mapping_path).read_text(encoding='utf-8'))
        if mapping.get('format') != 'deviant-resource-import-v1':
            raise ValueError('Unsupported import mapping')
        selection = import_selection(mapping)
        by_input = {}
        for record in mapping['records']:
            key = (record['package'], record['path'])
            if key in by_input:
                raise ValueError('Duplicate input record: ' + str(key))
            if not selected_input(selection, *key):
                raise ValueError('Record outside import selection: ' + str(key))
            by_input[key] = record
            for name in (record['target'], *record.get('aliases', [])):
                relative(name)
                value = (record['sha256'], record['bytes'])
                if name in expected and expected[name] != value:
                    raise ValueError('Conflicting target record: ' + name)
                expected[name] = value
            if 'originalProjectPath' in record:
                relative(record['originalProjectPath'])
        imports.append({'path': mapping_path, 'mapping': mapping, 'selection': selection,
                        'by_input': by_input, 'archiveVerified': False})
    if len({p.casefold() for p in expected}) != len(expected):
        raise ValueError('Case-insensitive path collision')
    generated = []
    if catalog.get('generatedManifest'):
        generated_manifest = relative(catalog['generatedManifest'])
        document = json.loads((root / generated_manifest).read_text(encoding='utf-8'))
        if document.get('format') != 'deviant-generated-resources-v1':
            raise ValueError('Unsupported generated-resource manifest')
        seen_generated = set()
        for item in document['files']:
            name = relative(item['path'])
            if name in expected or name in seen_generated:
                raise ValueError('Generated file conflicts with an imported or generated path: ' + name)
            if (not isinstance(item['bytes'], int) or item['bytes'] < 0
                    or len(item['sha256']) != 64
                    or any(c not in '0123456789abcdef' for c in item['sha256'])):
                raise ValueError('Invalid generated file size/hash: ' + name)
            seen_generated.add(name)
            expected[name] = (item['sha256'], item['bytes'])
            generated.append(name)
    if len({p.casefold() for p in expected}) != len(expected):
        raise ValueError('Case-insensitive path collision')
    for name, (sha, size) in expected.items():
        data = (root / name).read_bytes()
        if data.startswith(LFS_HEADER):
            raise ValueError('LFS object not downloaded: ' + name)
        if len(data) != size or digest(data) != sha:
            raise ValueError('Bytes/hash mismatch: ' + name)
    owners = set()
    for item in catalog['assets']:
        path = relative(item['path'])
        if path in owners:
            raise ValueError('Duplicate asset group: ' + path)
        owners.add(path)
        asset = json.loads((root / path / 'asset.json').read_text(encoding='utf-8'))
        if asset['id'] != item['id'] or asset['path'] != path:
            raise ValueError('Asset identity mismatch: ' + path)
        names = asset['files']
        if len(names) != len(set(names)) or len(names) != item['fileCount']:
            raise ValueError('Asset file count/duplicates: ' + path)
        for name in names:
            relative(name)
            if path + '/' + name not in expected:
                raise ValueError('Unmapped asset file: ' + path + '/' + name)
    indexed = {item['path'] + '/' + name
               for item in catalog['assets']
               for name in json.loads((root / item['path'] / 'asset.json').read_text(encoding='utf-8'))['files']}
    missing_owner = [p for p in expected if not p.startswith('imports/') and p not in indexed]
    if missing_owner:
        raise ValueError('Asset files missing from catalog: ' + repr(missing_owner[:5]))
    payloads = json.loads((root / '.lfs-payloads.json').read_text(encoding='utf-8'))
    for name in payloads:
        if relative(name) not in expected:
            raise ValueError('Unmapped embedded payload: ' + name)
    result = {'inputRecords': sum(len(item['by_input']) for item in imports), 'verifiedFiles': len(expected),
              'generatedFiles': len(generated),
              'assetGroups': len(owners), 'embeddedLfsFiles': len(payloads),
              'unclassifiedFiles': sum('/unclassified/' in p for p in expected),
              'originalArchiveVerified': False}
    archives = ([archive] if archive is not None else []) + list(additional_archives)
    for source_archive in archives:
        data = source_archive.read_bytes()
        identity = (len(data), digest(data))
        matches = [item for item in imports
                   if identity == (item['mapping'].get('archive', {}).get('bytes'),
                                   item['mapping'].get('archive', {}).get('sha256'))]
        if not matches:
            raise ValueError('Unexpected original archive')
        del data
        for item in matches:
            by_input = item['by_input']
            seen = set()
            for key, source in input_entries(source_archive, item['selection']):
                if key in seen or key not in by_input:
                    raise ValueError('Unmapped/duplicate ZIP entry: ' + str(key))
                seen.add(key)
                record = by_input[key]
                if len(source) != record['bytes'] or digest(source) != record['sha256']:
                    raise ValueError('Original ZIP entry differs: ' + str(key))
            if seen != set(by_input):
                raise ValueError('Mapping contains nonexistent ZIP entries')
            item['archiveVerified'] = True
    result['originalArchiveVerified'] = imports[0]['archiveVerified']
    result['importMappings'] = [
        {'path': item['path'], 'inputRecords': len(item['by_input']),
         'scope': (item['mapping'].get('sourceType') or
                   ('git-source' if item['mapping'].get('sourceCommit') else
                    'selected-inputs' if item['selection'] is not None else 'complete-archive')),
         'archiveVerified': item['archiveVerified']} for item in imports]
    if check_links:
        broken, absent_in_input, checked = [], [], 0
        for item, record in ((item, record) for item in imports for record in item['mapping']['records']):
            if not record['target'].endswith('.html'):
                continue
            parser = References()
            parser.feed((root / record['target']).read_text(encoding='utf-8-sig'))
            for href in parser.paths:
                original = posixpath.normpath(posixpath.join(posixpath.dirname(record['path']), href))
                target = posixpath.normpath(posixpath.join(posixpath.dirname(record['target']), href))
                if (root / target).exists():
                    checked += 1
                elif (record['package'], original) in item['by_input']:
                    broken.append({'page': record['target'], 'href': href,
                                   'expectedTarget': item['by_input'][(record['package'], original)]['target']})
                else:
                    absent_in_input.append({'page': record['target'], 'href': href})
        result['htmlLinks'] = {'resolved': checked, 'brokenByReorganization': broken,
                               'missingFromOriginalPackage': absent_in_input}
        if broken:
            print(json.dumps(result, ensure_ascii=False, indent=2))
            raise ValueError('Reorganization broke existing HTML references')
        generated_links = 0
        for name in generated:
            if not name.endswith('.html'):
                continue
            parser = References()
            parser.feed((root / name).read_text(encoding='utf-8-sig'))
            for href in parser.paths:
                target = (root / name).parent.joinpath(href).resolve()
                if not target.is_relative_to(root.resolve()) or not target.exists():
                    raise ValueError('Broken/unsafe generated HTML reference: ' + name + ' -> ' + href)
                generated_links += 1
        result['generatedHtmlLinks'] = generated_links
    return result


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, action='append', default=[],
                        help='Original Drive ZIP; repeat for each import mapping (respects explicit selections)')
    parser.add_argument('--check-links', action='store_true', help='Check HTML references that existed in the original package')
    args = parser.parse_args()
    result = verify(ROOT, check_links=args.check_links, additional_archives=args.archive)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
