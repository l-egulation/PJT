"""Preserve committed main audio/fonts/VFX bytes; never edit the consumer checkout."""
import argparse
import json
from pathlib import Path
import subprocess

from assets import json_bytes, safe_path, sha
from verify_resources import LFS_HEADER

ROOT = Path(__file__).resolve().parents[1]
MAPPING = 'imports/main-media/mapping.json'
AUDIO = 'unity/Assets/Deviant/Audio/'
FONTS = ('unity/Assets/Deviant/UI/Fonts/',
         'unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/',
         'unity/Assets/TextMesh Pro/Fonts/')
VFX = 'unity/Assets/ThirdParty/Wallcoeur/'


def owner(name):
    suffix = Path(name).suffix.lower()
    if name.startswith(AUDIO) and suffix in {'.wav', '.ogg', '.mp3', '.flac'}:
        return 'audio.approved-combat', 'audio/approved-combat', name[len(AUDIO):]
    if name.startswith(FONTS) and suffix in {'.ttf', '.otf'}:
        return 'fonts.game', 'fonts/game', name[len('unity/Assets/'):]
    if name.startswith(VFX) and suffix in {'.png', '.tga', '.psd', '.exr', '.dds'}:
        return 'effects.wallcoeur', 'effects/wallcoeur', name[len(VFX):]
    return None


def collect(repo, revision):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(repo), *args])
    commit = git('rev-parse', '--verify', revision + '^{commit}').decode().strip()
    names = git('ls-tree', '-r', '--name-only', '-z', commit).decode().split('\0')
    files, records, groups = {}, [], {}
    for name in sorted(filter(None, names)):
        selected = owner(name)
        if selected is None:
            continue
        asset_id, folder, local = selected
        data = git('show', commit + ':' + name)
        if data.startswith(LFS_HEADER):
            raise ValueError('Source is an LFS pointer, not restored media: ' + name)
        if safe_path(repo, name).read_bytes() != data:
            raise ValueError('Uncommitted source edits preserved: ' + name)
        meta = git('show', commit + ':' + name + '.meta')
        if safe_path(repo, name + '.meta').read_bytes() != meta:
            raise ValueError('Uncommitted Unity metadata edits preserved: ' + name)
        target = folder + '/runtime/' + local
        files[target] = data
        groups.setdefault((asset_id, folder), []).append('runtime/' + local)
        records.append({'package': None, 'path': name, 'target': target,
                        'originalProjectPath': name, 'bytes': len(data), 'sha256': sha(data),
                        'unityMetaSha256': sha(meta)})
    if not records:
        raise ValueError('No committed audio/fonts/VFX found in the selected main revision')
    for (asset_id, folder), names in groups.items():
        files[folder + '/asset.json'] = json_bytes({
            'format': 'deviant-resource-asset-v1', 'id': asset_id, 'path': folder,
            'status': 'preserved-from-main', 'approval': 'not-reassessed-by-migration',
            'gameIntegration': 'requires-consumer-deployment-and-Unity-validation',
            'distribution': 'private-project-only; public-or-third-party-sharing-requires-rights-review',
            'licenseReview': 'not-established-by-private-repository-status',
            'sources': [MAPPING], 'files': sorted(names)})
    files[MAPPING] = json_bytes({
        'format': 'deviant-resource-import-v1', 'sourceType': 'git-source',
        'sourceRepository': 'https://lab.ssafy.com/s15-final/S15P31F109',
        'sourceCommit': commit, 'scope': 'current-audio-fonts-vfx-originals',
        'authorization': 'user-requested-private-project-storage; not-a-license-certification',
        'records': records})
    return files, groups


def import_media(repo, revision='HEAD', root=ROOT, check=False):
    files, groups = collect(repo, revision)
    catalog_path = safe_path(root, 'catalog.json')
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    for (asset_id, folder), names in groups.items():
        entry = {'id': asset_id, 'path': folder, 'fileCount': len(names)}
        matches = [a for a in catalog['assets'] if a['id'] == asset_id or a['path'] == folder]
        if matches and matches != [entry]:
            raise ValueError('Conflicting existing media asset: ' + folder)
        if not matches:
            catalog['assets'].append(entry)
    mappings = catalog.setdefault('additionalImportMappings', [])
    if MAPPING not in mappings:
        mappings.append(MAPPING)
    # Preflight the entire import, including indices, before writing any bytes.
    for name, data in files.items():
        target = safe_path(root, name)
        if target.exists() and (not target.is_file() or target.read_bytes() != data):
            raise ValueError('Refusing to overwrite different resource: ' + name)
    result = {'mediaFiles': sum(len(names) for names in groups.values()),
              'assetGroups': len(groups), 'checkOnly': check}
    if not check:
        for name, data in files.items():
            target = safe_path(root, name)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        catalog_path.write_bytes(json_bytes(catalog))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=Path, required=True)
    parser.add_argument('--revision', default='HEAD')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(import_media(args.source_repo, args.revision, check=args.check), indent=2))
