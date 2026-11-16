"""Register and verify this handoff's own files without touching imported hashes."""
import hashlib
import io
import json
import re
import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.request import urlopen

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'monsters/stage-02/boss-phase3-attacks-20261106'
P = ROOT / GROUP


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')


def place(items, entries, key, mine):
    """Replace this group's entries without reordering anyone else's: keep the existing order and insert the
    group's entries where a key-sorted list would put them (append when the existing list is not sorted)."""
    import bisect
    kept = [x for x in items if not mine(x)]
    keys = [x[key] for x in kept]
    if keys == sorted(keys):
        for e in sorted(entries, key=lambda x: x[key]):
            kept.insert(bisect.bisect_left([x[key] for x in kept], e[key]), e)
        return kept
    return kept + sorted(entries, key=lambda x: x[key])


def main():
    asset_id = 'monsters.stage-02.boss-phase3-attacks-20261106'
    files = sorted(x.relative_to(P).as_posix() for x in P.rglob('*') if x.is_file() and x.name != 'asset.json')
    write(P/'asset.json', {'format': 'deviant-resource-asset-v1', 'id': asset_id, 'path': GROUP, 'name': '강화 Blue 3페이즈 공격 모션', 'status': 'homing-D-V2-user-approved-all-four-phase3-motions-user-approved-not-unity-integrated', 'entry': 'preview/fan-sim/index.html', 'files': files})
    catalog = read(ROOT/'catalog.json')
    catalog['assets'] = place(catalog['assets'], [{'id': asset_id, 'path': GROUP, 'fileCount': len(files)}], 'id', lambda x: x['id'] == asset_id)
    write(ROOT/'catalog.json', catalog)
    manifest = read(ROOT/'generated-assets.json')
    entries = []
    for name in files:
        blob = (P/name).read_bytes()
        entries.append({'path': GROUP+'/'+name, 'bytes': len(blob), 'sha256': hashlib.sha256(blob).hexdigest()})
    manifest['files'] = place(manifest['files'], entries, 'path', lambda x: x['path'].startswith(GROUP+'/'))
    write(ROOT/'generated-assets.json', manifest)

    class Refs(HTMLParser):
        def __init__(self):
            super().__init__()
            self.paths=[]
        def handle_starttag(self, tag, attrs):
            for key,value in attrs:
                if key in ('src','href') and value and not value.startswith(('http:','https:','#','data:')):
                    self.paths.append(value)
    page=P/'preview/index.html'
    parser=Refs()
    parser.feed(page.read_text(encoding='utf-8'))
    refs=[page.resolve(), *[(page.parent/x).resolve() for x in parser.paths]]
    # Include the canvas image dependencies and fetched prompt from the JS module.
    js=(P/'preview/review.js').read_text(encoding='utf-8')
    refs += [(page.parent/x).resolve() for x in re.findall(r"'(assets/[^']+\.png)'",js)]
    # Verify every newly registered file, including extracted source cells and candidate dependencies.
    refs += [(P/name).resolve() for name in files]
    for html in P.rglob('*.html'):
        links=Refs()
        links.feed(html.read_text(encoding='utf-8'))
        refs += [(html.parent/name).resolve() for name in links.paths]
    refs=[target/'index.html' if target.is_dir() else target for target in refs]
    base='http://127.0.0.1:8166/'
    for target in set(refs):
        assert target.is_relative_to(ROOT) and target.is_file(), target
        blob=target.read_bytes()
        if '--local-only' not in sys.argv:
            with urlopen(base+target.relative_to(ROOT).as_posix(),timeout=10) as response:
                assert response.status==200 and response.read()==blob,target
        if target.suffix=='.png':
            Image.open(io.BytesIO(blob)).verify()
    for item in manifest['files']:
        if item['path'].startswith(GROUP+'/'):
            blob=(ROOT/item['path']).read_bytes()
            assert len(blob)==item['bytes'] and hashlib.sha256(blob).hexdigest()==item['sha256']
    from assets import Store
    store=Store(ROOT)
    for name in files:
        store.read({'asset':asset_id,'path':GROUP+'/'+name})
    mode='local links (HTTP not checked)' if '--local-only' in sys.argv else 'HTTP dependencies'
    print(f'PASS: {len(files)} indexed files; {len(set(refs))} {mode}; PNG decode, byte hashes, ownership/counts; no browser render claim.')


if __name__ == '__main__':
    main()
