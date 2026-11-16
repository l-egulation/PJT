"""Apply the main checkout's resource selection; never select a shared folder or switch Git refs."""
import argparse
import importlib.util
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
TERRAIN = {'tools/chapter1-rounded-terrain/art/Terrain32.png',
           'tools/chapter1-rounded-terrain/art/SolidPlatform.png',
           'tools/chapter1-rounded-terrain/art/DropPlatform.png'}
FONT_ROOTS = ('unity/Assets/Deviant/UI/Fonts/',
              'unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/',
              'unity/Assets/Deviant/Tutorial/Resources/FirstRunTutorial/Fonts/',
              'unity/Assets/TextMesh Pro/Fonts/')
BOSS_BYTES = 'unity/Assets/ForestCombatV2/Resources/ClockExecutioner/StationaryBytes.bytes'
BOSS_REFERENCE_BYTES = {BOSS_BYTES,
                        'unity/Assets/Chapter2/Resources/RoseBlue/rose.bytes',
                        'unity/Assets/Chapter2/Resources/RoseBlue/blue.bytes'}
MAP_EDITORS = {'tools/chapter1-map-editor/맵 편집기.html',
               'tools/chapter1-map-editor/이전_지형용_편집기.html'}


def resource_revision(repo):
    resources = repo / 'resources'
    if not (resources / '.git').exists():
        raise ValueError('resources is not initialized. Run git submodule update --init --recursive resources.')
    def git(*args):
        return subprocess.check_output(['git', '-C', str(repo), *args], text=True).strip()
    entry = git('ls-files', '--stage', '--', 'resources').split()
    if len(entry) != 4 or entry[0] != '160000' or entry[2] != '0':
        raise ValueError('Main checkout has no unambiguous resources gitlink')
    revision = git('-C', str(resources), 'rev-parse', 'HEAD')
    if revision != entry[1]:
        raise ValueError('resources differs from the main gitlink. Preserve resource edits, then update the submodule.')
    if git('-C', str(resources), 'status', '--porcelain', '--untracked-files=all'):
        raise ValueError('resources has uncommitted changes. Commit them and select that revision in the main gitlink first.')
    return revision


def contract(repo, require_metadata=True, asset=None):
    manifest = json.loads((repo / 'docs/assets/manifests/unity-deployment.json').read_text(encoding='utf-8'))
    if manifest.get('format') != 'deviant-asset-deployment-v1':
        raise ValueError('Unsupported Unity asset contract')
    if asset is not None:
        manifest = {**manifest, 'files': [item for item in manifest['files'] if item['source']['asset'] == asset]}
        if not manifest['files']:
            raise ValueError('No deployed files for resource asset: ' + asset)
    for item in manifest['files']:
        name = item['target']
        parts = PurePosixPath(name).parts
        if (not name or name.startswith('/') or '\\' in name or ':' in name
                or '/'.join(parts) != name or any(p in ('.', '..', '.git') for p in parts)):
            raise ValueError('Not an approved asset destination: ' + name)
        image_bytes = name.startswith('unity/Assets/ForestCombatV2/Resources/ClockBossEnvironment/') and name.endswith('.bytes')
        compressed_audio = name.startswith('unity/Assets/Deviant/Media/Resources/CompressedAudio/Payloads/') and name.endswith('.bytes')
        suffix = PurePosixPath(name).suffix.lower()
        audio = name.startswith('unity/Assets/Deviant/Audio/') and suffix in {'.wav', '.ogg', '.mp3', '.flac'}
        font = name.startswith(FONT_ROOTS) and suffix in {'.ttf', '.otf'}
        if not ((name.startswith('unity/Assets/') and name.endswith('.png')) or image_bytes or compressed_audio or audio or font
                or name in TERRAIN or name in BOSS_REFERENCE_BYTES or name in MAP_EDITORS):
            raise ValueError('Not an approved Unity/media destination: ' + name)
        if require_metadata and name.startswith('unity/') and not (repo / (name + '.meta')).is_file():
            raise ValueError('Main-owned Unity metadata is missing: ' + name + '.meta')
    return manifest


def backend(repo):
    tools = repo / 'resources/tools'
    path = tools / 'assets.py'
    if not path.is_file():
        raise ValueError('Pinned resources has no asset exporter. Update this checkout and its submodule together.')
    sys.path.insert(0, str(tools))
    try:
        spec = importlib.util.spec_from_file_location('deviant_resource_assets', path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module
    finally:
        sys.path.remove(str(tools))


def update(repo=ROOT, check=False, asset=None, local_preview=False):
    repo = repo.resolve()
    if local_preview:
        if not asset:
            raise ValueError('Local preview requires one explicit --asset selection')
        # Explicit local authoring review only. Production's pinned/clean path stays unchanged.
        # The same indexed source hashes, metadata, target checks and local-edit protection apply.
        revision = None
    else:
        revision = resource_revision(repo)
    manifest = contract(repo, asset=asset)
    if not check and (repo / 'unity/Temp/UnityLockfile').exists():
        raise ValueError('Close Unity before applying assets (unity/Temp/UnityLockfile exists).')
    result = backend(repo).export(repo / 'resources', manifest, repo, check=check)
    result['resourceCommit'] = revision
    if local_preview: result['localPreview'] = True
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Verify resources and local conflicts without changing files')
    parser.add_argument('--asset', help='Apply one explicitly selected asset ID from the same pinned contract')
    parser.add_argument('--local-preview', action='store_true', help='Review one explicitly selected local draft asset; not a publishable resource revision')
    args = parser.parse_args()
    try:
        print(json.dumps(update(check=args.check, asset=args.asset, local_preview=args.local_preview), ensure_ascii=False, indent=2))
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
        print('에셋 적용 실패: ' + str(error), file=sys.stderr)
        print('메인의 gitlink와 resources 상태 및 Git LFS 복원을 확인하세요. 로컬 수정은 덮어쓰지 않습니다.', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
