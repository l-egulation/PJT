"""Export explicitly selected, indexed resources without a shared folder or ZIP input."""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

from verify_resources import LFS_HEADER, relative, verify

ROOT = Path(__file__).resolve().parents[1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def safe_path(root, name):
    relative(name)
    root = root.resolve()
    target = root / name
    if not target.resolve().is_relative_to(root):
        raise ValueError('Path escapes root: ' + name)
    if any(path.is_symlink() for path in (target, *target.parents) if path != root):
        raise ValueError('Symlink asset path: ' + name)
    return target


class Store:
    """The resource catalog owns source bytes; a consumer owns its selections/destinations."""
    def __init__(self, root):
        self.root = root.resolve()
        catalog = json.loads((self.root / 'catalog.json').read_text(encoding='utf-8'))
        if catalog.get('format') != 'deviant-resource-catalog-v1':
            raise ValueError('Unsupported resource catalog')
        self.files = {}
        self.owners = {}
        self.cache = {}
        for name in [catalog['importMapping'], *catalog.get('additionalImportMappings', [])]:
            mapping = json.loads(safe_path(self.root, name).read_text(encoding='utf-8'))
            for item in mapping['records']:
                for path in [item['target'], *item.get('aliases', [])]:
                    self.add(path, item)
        if catalog.get('generatedManifest'):
            data = json.loads(safe_path(self.root, catalog['generatedManifest']).read_text(encoding='utf-8'))
            for item in data['files']:
                self.add(item['path'], item)
        for asset in catalog['assets']:
            data = json.loads(safe_path(self.root, asset['path'] + '/asset.json').read_text(encoding='utf-8'))
            if data['id'] != asset['id'] or len(data['files']) != asset['fileCount']:
                raise ValueError('Resource asset identity/count mismatch: ' + asset['id'])
            for name in data['files']:
                path = relative(asset['path'] + '/' + relative(name))
                if path not in self.files or path in self.owners:
                    raise ValueError('Unindexed/ambiguous resource owner: ' + path)
                self.owners[path] = asset['id']

    def add(self, path, item):
        relative(path)
        value = (item['sha256'], item['bytes'])
        if path in self.files and self.files[path] != value:
            raise ValueError('Conflicting resource bytes: ' + path)
        self.files[path] = value

    def read(self, source):
        path = relative(source['path'])
        if path not in self.files or self.owners.get(path) != source['asset']:
            raise ValueError('Unindexed resource selection: ' + path)
        if path not in self.cache:
            data = safe_path(self.root, path).read_bytes()
            if data.startswith(LFS_HEADER):
                raise ValueError('LFS object missing; run git lfs pull in resources: ' + path)
            digest, size = self.files[path]
            if len(data) != size or sha(data) != digest:
                raise ValueError('Resource hash mismatch: ' + path)
            self.cache[path] = data
        data = self.cache[path]
        if source.get('pointer') is not None:
            value = json.loads(data.decode('utf-8-sig'))
            pointer = source['pointer']
            if not isinstance(pointer, str) or not pointer.startswith('/'):
                raise ValueError('Invalid embedded-image JSON pointer: ' + path)
            for key in pointer[1:].split('/'):
                key = key.replace('~1', '/').replace('~0', '~')
                value = value[int(key)] if isinstance(value, list) else value[key]
            if not isinstance(value, str) or not value.startswith('data:image/png;base64,'):
                raise ValueError('Selected JSON value is not a PNG: ' + path)
            data = base64.b64decode(value.split(',', 1)[1], validate=True)
        return data


def export(root, manifest, destination, check=False, state_dir=None):
    """Preflight the whole selection, stage bytes, then atomically replace owned files."""
    destination = destination.resolve()
    if manifest.get('format') != 'deviant-asset-deployment-v1':
        raise ValueError('Unsupported asset deployment contract')
    state_dir = state_dir or destination / 'outputs/assets'
    if not state_dir.resolve().is_relative_to(destination):
        raise ValueError('Asset state must stay inside the destination checkout')
    state_dir = safe_path(destination, state_dir.relative_to(destination).as_posix())
    state_path = safe_path(destination, (state_dir / 'state.json').relative_to(destination).as_posix())
    previous = {}
    if state_path.is_file():
        state = json.loads(state_path.read_text(encoding='utf-8'))
        if state.get('destination') != str(destination):
            raise ValueError('Asset state belongs to another checkout')
        previous = state['files']
    store = Store(root)
    changes, seen = [], set()
    for item in manifest['files']:
        name = relative(item['target'])
        if name.casefold() in seen:
            raise ValueError('Duplicate asset destination: ' + name)
        seen.add(name.casefold())
        if (type(item['bytes']) is not int or item['bytes'] < 0
                or not re.fullmatch('[0-9a-f]{64}', item['sha256'])
                or any(not isinstance(h, str) or not re.fullmatch('[0-9a-f]{64}', h)
                       for h in item.get('replaceHashes', []))):
            raise ValueError('Invalid deployment size/hash: ' + name)
        target = safe_path(destination, name)
        if any(parent.exists() and not parent.is_dir() for parent in target.parents):
            raise ValueError('Asset parent is not a directory: ' + name)
        data = store.read(item['source'])
        if len(data) != item['bytes'] or sha(data) != item['sha256']:
            raise ValueError('Selected resource differs from consumer contract: ' + name)
        local = None
        if target.exists():
            if not target.is_file():
                raise ValueError('Asset destination is not a file: ' + name)
            local = sha(target.read_bytes())
            if local == item['sha256']:
                continue
            if local not in {previous.get(name), *item.get('replaceHashes', [])}:
                raise ValueError('Local edits preserved; no assets changed: ' + name)
        if local is not None:
            backup = safe_path(destination, (state_dir / 'backups' / local).relative_to(destination).as_posix())
            if backup.exists() and (not backup.is_file() or sha(backup.read_bytes()) != local):
                raise ValueError('Existing asset backup differs; no assets changed: ' + name)
        changes.append((item, target, local, data))
    names = {item['target'] for item in manifest['files']}
    if any(parent.as_posix() in names for name in names for parent in Path(name).parents):
        raise ValueError('Overlapping asset destinations')
    result = {'verified': len(manifest['files']), 'changed': len(changes), 'checkOnly': check}
    if check:
        return result
    state_dir.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='stage-', dir=state_dir) as temporary:
        staged = []
        for i, (item, target, old, data) in enumerate(changes):
            path = Path(temporary) / str(i)
            path.write_bytes(data)
            if sha(path.read_bytes()) != item['sha256']:
                raise ValueError('Staged asset hash mismatch: ' + item['target'])
            staged.append((item, target, old, path))
        for item, target, old, path in staged:
            safe_path(destination, item['target'])
            current = sha(target.read_bytes()) if target.is_file() else None
            if current != old or (target.exists() and not target.is_file()):
                raise ValueError('Local asset changed during preflight: ' + item['target'])
        for item, target, old, path in staged:
            safe_path(destination, item['target'])
            current = sha(target.read_bytes()) if target.is_file() else None
            if current != old or (target.exists() and not target.is_file()):
                raise ValueError('Local asset changed during copy: ' + item['target'])
            if old is not None:
                backup = safe_path(destination, (state_dir / 'backups' / old).relative_to(destination).as_posix())
                if not backup.exists():
                    backup.parent.mkdir(parents=True, exist_ok=True)
                    backup.write_bytes(target.read_bytes())
            target.parent.mkdir(parents=True, exist_ok=True)
            os.replace(path, target)
    state = {'destination': str(destination),
             'files': {**previous, **{item['target']: item['sha256'] for item in manifest['files']}}}
    with tempfile.NamedTemporaryFile(prefix='state-', suffix='.tmp', dir=state_dir, delete=False) as stream:
        pending = Path(stream.name)
        stream.write(json_bytes(state))
    try:
        os.replace(pending, state_path)
    finally:
        if pending.exists():
            pending.unlink()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    validate = commands.add_parser('verify', help='Verify all indexed source bytes and links')
    validate.add_argument('--check-links', action='store_true')
    deploy = commands.add_parser('export', help='Export only an explicit consumer contract')
    deploy.add_argument('--manifest', type=Path, required=True)
    deploy.add_argument('--destination', type=Path, required=True)
    deploy.add_argument('--check', action='store_true')
    args = parser.parse_args()
    try:
        if args.command == 'verify':
            result = verify(ROOT, check_links=args.check_links)
        else:
            result = export(ROOT, json.loads(args.manifest.read_text(encoding='utf-8')),
                            args.destination, check=args.check)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (ValueError, OSError, KeyError, TypeError) as error:
        raise SystemExit(str(error)) from error


if __name__ == '__main__':
    main()
