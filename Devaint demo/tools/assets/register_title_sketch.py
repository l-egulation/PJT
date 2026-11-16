"""Register the approved title's PNGs in the main-owned Unity deployment contract."""
import hashlib
import importlib.util
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[2]
SOURCE = 'ui/title-sketch-20261115'
TARGET = 'unity/Assets/Deviant/Game/Resources/TitleSketch'


def register():
    spec = importlib.util.spec_from_file_location('village_metadata', Path(__file__).with_name('register_stage1_village_colour.py'))
    village = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(village)
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf8'))
    files = sorted((ROOT / 'resources' / SOURCE / 'runtime').rglob('*.png'))
    for source in files:
        relative = source.relative_to(ROOT / 'resources' / SOURCE / 'runtime').as_posix()
        target = TARGET + '/' + relative
        raw = source.read_bytes()
        row = {'target': target, 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest(),
               'source': {'asset': 'ui.title-sketch-20261115', 'path': source.relative_to(ROOT/'resources').as_posix()}}
        old = next((r for r in manifest['files'] if r['target'] == target), None)
        if old:
            previous = set([old['sha256']] + old.get('replaceHashes', [])) - {row['sha256']}
            if previous: row['replaceHashes'] = sorted(previous)
            manifest['files'][manifest['files'].index(old)] = row
        else: manifest['files'].append(row)
        path = ROOT / target
        path.parent.mkdir(parents=True, exist_ok=True)
        parent = path.parent
        while parent != ROOT/'unity/Assets/Deviant/Game/Resources':
            village.folder_meta(parent)
            parent = parent.parent
        meta = Path(str(path)+'.meta')
        if not meta.exists():
            text = village.META.format(guid=uuid.uuid4().hex, wrap=1, px='.5', py='.5', readable=0)
            if relative in ('logo.png', 'marker.png'): text = text.replace('filterMode: 0', 'filterMode: 1')
            meta.write_text(text, encoding='utf8', newline='\n')
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf8', newline='\n')
    print('Registered', len(files), 'title PNG destinations; native bytes are applied only by update_assets.py.')


if __name__ == '__main__': register()
