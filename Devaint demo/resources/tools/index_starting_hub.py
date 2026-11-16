"""Index only the new hub production set; imported ZIP mappings stay immutable."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
GROUPS = [('ui.roguelite-hub', 'ui/roguelite-hub', '시작 마을의 영구 해금 문양·레이아웃·미리보기'),
          ('effects.hub-unlock', 'effects/hub-unlock', '시작 마을 해금·안료 획득 표현'),
          ('sequences.starting-village-intro', 'sequences/starting-village-intro', '시작 마을 이야기의 정지 배경')]


def write(path, value):
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode('utf-8'))


def merge_generated_records(existing, updated):
    """Refreshing this set must not erase other generated asset families."""
    names=[item['path'] for item in updated]
    if len(names)!=len(set(names)):
        raise ValueError('Duplicate refreshed generated path')
    replaced=set(names)
    return sorted([item for item in existing if item['path'] not in replaced]+updated,
                  key=lambda item:item['path'])


def main():
    catalog = json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
    new_paths = []
    for ident, folder, title in GROUPS:
        base = ROOT/folder
        files = sorted(p.relative_to(base).as_posix() for p in base.rglob('*')
                       if p.is_file() and p.name != 'asset.json')
        write(base/'asset.json', {'format': 'deviant-resource-asset-v1', 'id': ident,
              'path': folder, 'name': title, 'status': 'native-review-exports',
              'approval': 'pending-user-art-review', 'gameIntegration': 'not-integrated',
              'sources': ['new production; see source metadata and prompts'], 'files': files})
        catalog['assets'] = [a for a in catalog['assets'] if a['id'] != ident]
        catalog['assets'].append({'id': ident, 'path': folder, 'fileCount': len(files)})
        new_paths.extend(folder+'/'+p for p in files)
    village_path = ROOT/'environments/village/asset.json'
    village = json.loads(village_path.read_text(encoding='utf-8'))
    folders = ('source/starting-hub', 'runtime/buildings/starting-hub',
               'runtime/props/starting-hub', 'preview/starting-hub')
    files = sorted(p.relative_to(village_path.parent).as_posix()
                   for folder in folders for p in (village_path.parent/folder).rglob('*') if p.is_file())
    imported = [p for p in village['files'] if not any(p.startswith(x+'/') for x in folders)]
    village['files'] = sorted(imported+files)
    village['generatedSets'] = {'starting-hub': {'approval': 'pending-user-art-review',
        'gameIntegration': 'not-integrated', 'source': 'source/starting-hub/generation.json',
        'preview': 'preview/starting-hub/color-assembly.png',
        'pairedGeometry': 'identical alpha, canvas and contact line'}}
    write(village_path, village)
    for item in catalog['assets']:
        if item['id'] == 'environments.village': item['fileCount'] = len(village['files'])
    new_paths.extend('environments/village/'+p for p in files)
    catalog['generatedManifest'] = 'generated-assets.json'
    write(ROOT/'catalog.json', catalog)
    records = [{'path': p, 'bytes': (ROOT/p).stat().st_size,
                'sha256': hashlib.sha256((ROOT/p).read_bytes()).hexdigest()} for p in sorted(new_paths)]
    manifest_path=ROOT/'generated-assets.json'
    manifest=json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
    manifest.update({'format': 'deviant-generated-resources-v1',
        'scope': 'generated production sets', 'approval': 'pending-user-art-review',
        'sourceMappingUnchanged': True,
        'files':merge_generated_records(manifest.get('files',[]),records)})
    write(manifest_path,manifest)
    print('Indexed', len(records), 'new production files; imported mapping unchanged.')


if __name__ == '__main__': main()
