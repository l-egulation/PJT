"""Build the self-contained boss room preview from main template and original PNG bytes.
Run from the main Deviant checkout: python resources/monsters/stage-02/elite-boss-pattern-drafts/source/build_boss_room.py
"""
from pathlib import Path
import json, base64, hashlib
main = Path.cwd()
root = Path(__file__).resolve().parent.parent
assets = json.loads((root / 'source/boss-room-assets.json').read_text(encoding='utf-8'))
for a in assets.values():
    a['src'] = 'data:image/png;base64,' + base64.b64encode((main / a['source']).read_bytes()).decode()
text = (main / 'tools/chapter2-boss-room-editor/template.html').read_text(encoding='utf-8').replace('__ASSET_DATA__', json.dumps(assets, ensure_ascii=False))
room = json.loads((main / 'tools/chapter2-boss-room-editor/recommended-room.json').read_text(encoding='utf-8'))
text = text.replace('__ROOM_DATA__', json.dumps(room, ensure_ascii=False).replace('<', r'\u003c'))
out = root / 'preview/boss-room/index.html'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(text, encoding='utf-8')
meta = root / 'asset.json'
a = json.loads(meta.read_text(encoding='utf-8'))
for rel in ['preview/boss-room/index.html', 'source/boss-room-assets.json', 'source/build_boss_room.py']:
    if rel not in a['files']: a['files'].append(rel)
    a['sha256'][rel] = hashlib.sha256((root / rel).read_bytes()).hexdigest()
a['files'].sort()
meta.write_text(json.dumps(a, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
catalog = main / 'resources/catalog.json'
c = json.loads(catalog.read_text(encoding='utf-8'))
for group in c.values():
    if isinstance(group, list):
        for entry in group:
            if isinstance(entry, dict) and entry.get('id') == a['id']: entry['fileCount'] = len(a['files'])
catalog.write_text(json.dumps(c, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(out)
