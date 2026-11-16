"""Index the ink-part pixelfx effect sheets; imported ZIP mappings stay immutable.

The sheets are generated in the main repository (전투테스트/<effect>/export.mjs → outputs/pixelfx/<effect>/<name>.png + .json)
and copied unchanged into effects/ink-parts/runtime/<effect>/. Run this after copying to refresh asset.json, catalog.json and
generated-assets.json. Python 3.9+.
"""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
IDENT, FOLDER = 'effects.ink-parts', 'effects/ink-parts'
NAME = '잉크 파츠 pixelfx 이펙트 시트(표식 쌓임·격발, 덧베기, 이동 속도, 회복, 보호막, 속박, 둔화, 끌어당김, 다음 공격 강화, 파열 가시)'


def write(path, value):
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2)+'\n').encode('utf-8'))


def merge_generated_records(existing, updated, prefix):
    """Refreshing this set replaces only its own folder and never erases other generated families."""
    names = [item['path'] for item in updated]
    if len(names) != len(set(names)):
        raise ValueError('Duplicate refreshed generated path')
    # 기존 항목의 순서는 그대로 두고 이 묶음만 뒤에 붙인다(다른 묶음의 diff 를 만들지 않는다)
    return [item for item in existing if not item['path'].startswith(prefix)]+updated


def main():
    base = ROOT/FOLDER
    files = sorted(p.relative_to(base).as_posix() for p in base.rglob('*') if p.is_file() and p.name != 'asset.json')
    sheets = [f for f in files if f.endswith('.png')]
    missing = [f for f in sheets if f[:-4]+'.json' not in files]
    if not sheets or missing:
        raise ValueError('Each sheet PNG needs its Aseprite JSON: ' + ', '.join(missing))
    write(base/'asset.json', {'format': 'deviant-resource-asset-v1', 'id': IDENT, 'path': FOLDER, 'name': NAME,
          'status': 'native-review-exports', 'approval': 'pending-user-art-review',
          'gameIntegration': 'trial-chapter1-ink-part-fx',
          'sources': ['main repository 전투테스트/<effect>/ (pixelfx effect code + export.mjs); sheets copied byte for byte'],
          'sheetFormat': {'sheet': 'transparent PNG + Aseprite-style JSON; meta.pixelfx has frameSize, pivot, columns, fps, loop',
                           'pixelsPerUnit': 32, 'facing': 'right; mirror for left'},
          'files': files})
    catalog = json.loads((ROOT/'catalog.json').read_text(encoding='utf-8'))
    catalog['assets'] = [a for a in catalog['assets'] if a['id'] != IDENT]
    catalog['assets'].append({'id': IDENT, 'path': FOLDER, 'fileCount': len(files)})
    write(ROOT/'catalog.json', catalog)
    records = [{'path': FOLDER+'/'+p, 'bytes': (base/p).stat().st_size, 'sha256': hashlib.sha256((base/p).read_bytes()).hexdigest()} for p in files]
    manifest_path = ROOT/'generated-assets.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files'] = merge_generated_records(manifest['files'], records, FOLDER+'/')
    write(manifest_path, manifest)
    print('Indexed', len(files), 'ink-part effect files (', len(sheets), 'sheets ); imported mapping unchanged.')


if __name__ == '__main__': main()
