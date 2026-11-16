"""Prepare the pinned Unity assets before the Game Sandbox enters Play Mode."""
import hashlib
import json
import os
import subprocess
import sys

from update_assets import ROOT, backend, contract, resource_revision


def git(*args, env=None):
    subprocess.run(['git', '-C', str(ROOT), *args], env=env, check=True)


def matches(item):
    path = ROOT / item['target']
    if not path.is_file() or path.stat().st_size != item['bytes']:
        return False
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest() == item['sha256']


def restore_missing_metadata(manifest):
    missing = []
    for item in manifest['files']:
        target = item['target']
        if not target.startswith('unity/'):
            continue
        name = target + '.meta'
        path = ROOT / name
        if path.is_file():
            continue
        if (ROOT / target).exists():
            raise ValueError('Existing Unity asset is missing its metadata: ' + target)
        # A first editor import can remove metadata for assets not yet deployed.
        # Only recover files recorded in the index, without overwriting local files.
        missing.append(name)
    if missing:
        subprocess.run(
            ['git', '-C', str(ROOT), 'checkout-index', '-z', '--stdin'],
            input=('\0'.join(missing) + '\0').encode('utf-8'),
            check=True,
        )


def main():
    manifest = contract(ROOT, require_metadata=False)
    if all(matches(item) for item in manifest['files']):
        contract(ROOT)
        print('Unity assets already match the checkout.')
        return

    resources = ROOT / 'resources'
    if (resources / '.git').exists():
        status = subprocess.check_output(
            ['git', '-C', str(resources), 'status', '--porcelain', '--untracked-files=all'],
            text=True,
        )
        if status.strip():
            raise ValueError('resources contains local changes. Preserve them before starting Game Sandbox.')

    git('submodule', 'update', '--init', '--recursive', '--depth', '1', 'resources',
        env={**os.environ, 'GIT_LFS_SKIP_SMUDGE': '1', 'GIT_TERMINAL_PROMPT': '0'})
    resource_revision(ROOT)
    git('-C', str(resources), '-c', 'lfs.fetchrecentalways=false', 'lfs', 'pull',
        env={**os.environ, 'GIT_TERMINAL_PROMPT': '0'})
    # The editor caller suspends asset refresh until the shared exporter finishes.
    restore_missing_metadata(manifest)
    contract(ROOT)
    result = backend(ROOT).export(resources, manifest, ROOT, check=False)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
        print(f'Game Sandbox asset preparation failed: {error}', file=sys.stderr)
        sys.exit(1)
