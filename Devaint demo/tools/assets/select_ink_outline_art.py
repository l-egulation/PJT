"""Point the selected monster/NPC frames at the resource ink-outline set, matching the player's shader outline.

Reads resources/authoring/ink-outline-1px/source/outline-map.json and, by target path only, rewrites the
deployment contract entries and the frame hashes the Unity importers check. Run after any register script
that reselects these targets, then apply with update_assets.py.
"""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[2]
MAP = ROOT / 'resources/authoring/ink-outline-1px/source/outline-map.json'
ASSET = 'authoring.ink-outline-1px'
FRAME_INDEXES = ['unity/Assets/Deviant/Stage1Monsters/Data/import.json',
                 'unity/Assets/Deviant/Stage1Monsters/Data/effects-import.json',
                 'unity/Assets/ForestCombatV2/Resources/Chapter1NewMonsterArt.json']


def read(p): return json.loads(p.read_text(encoding='utf-8'))
def write(p, v): p.write_bytes((json.dumps(v, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


def main():
    outline = {f['target']: f for f in read(MAP)['files']}
    manifest_path = ROOT / 'docs/assets/manifests/unity-deployment.json'; manifest = read(manifest_path); seen = set()
    for item in manifest['files']:
        f = outline.get(item['target'])
        if not f: continue
        if item['source']['path'] not in (f['sourcePath'], f['path']): raise SystemExit('target reselected since outline export: ' + item['target'])
        item.update(bytes=f['bytes'], sha256=f['sha256'], source={'asset': ASSET, 'path': f['path']}); seen.add(item['target'])
    if seen != set(outline): raise SystemExit('outline targets missing from contract: ' + ', '.join(sorted(set(outline) - seen)[:5]))
    write(manifest_path, manifest)
    by_resource = {t.split('/Resources/', 1)[1][:-4]: t for t in outline if '/Resources/' in t}
    changed = 0
    def walk(node):
        nonlocal changed
        if isinstance(node, dict):
            target = 'unity/' + node['path'] if isinstance(node.get('path'), str) and node['path'].startswith('Assets/') else by_resource.get(node.get('resource'))
            if 'sha256' in node and target in outline:
                if node['sha256'] not in (outline[target]['sourceSha256'], outline[target]['sha256']): raise SystemExit('unexpected frame hash: ' + target)
                node['sha256'] = outline[target]['sha256']; changed += 1
            for v in node.values(): walk(v)
        elif isinstance(node, list):
            for v in node: walk(v)
    for rel in FRAME_INDEXES:
        data = read(ROOT / rel); walk(data); write(ROOT / rel, data)
    print(json.dumps({'contract': len(seen), 'frameHashes': changed}))


if __name__ == '__main__':
    main()
