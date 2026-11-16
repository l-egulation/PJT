"""Register the selected loot artwork without changing the author's original bytes."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSET = 'ui/monster-loot'


def save_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def preserve(source, target):
    data = source.read_bytes()
    if target.exists() and target.read_bytes() != data:
        raise ValueError('Preserving existing artwork: ' + str(target))
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--authoring-root', type=Path, required=True)
    args = parser.parse_args()
    target = ROOT / ASSET
    prompts = json.loads((args.authoring_root / 'v1/source/prompts.json').read_text(encoding='utf-8'))
    selected_prompts = {}
    for name in ['ink-red', 'ink-yellow', 'ink-green', 'ink-blue', 'ink-purple', 'weapon-stamp', 'skill-stamp']:
        preserve(args.authoring_root / 'v1/source/generated' / (name + '.png'), target / 'source/generated' / (name + '.png'))
        preserve(args.authoring_root / 'v1/runtime/32' / (name + '.png'), target / 'runtime' / (name + '.png'))
        selected_prompts[name] = prompts[name]
    preserve(args.authoring_root / 'v2-simple/source/generated/gold-simple.png', target / 'source/generated/gold-simple.png')
    preserve(args.authoring_root / 'v2-simple/runtime/8/gold-simple.png', target / 'runtime/gold-simple.png')
    simple = json.loads((args.authoring_root / 'v2-simple/source/jobs.json').read_text(encoding='utf-8'))
    selected_prompts['gold-simple'] = next(job['prompt'] for job in simple if job['id'] == 'gold-simple')
    single = args.authoring_root / 'v3-single-coin'
    if single.exists():
        preserve(single / 'source/generated.png', target / 'source/generated/gold-coin-single.png')
        preserve(single / 'source/reference.png', target / 'source/references/gold-stack-reference.png')
        preserve(single / 'source/prompt.json', target / 'source/prompt-single-coin.json')
        preserve(single / 'source/pixel-spec.json', target / 'source/pixel-spec-single-coin.json')
        preserve(single / 'runtime/gold-coin-single.png', target / 'runtime/gold-coin-single.png')
        selected_prompts['gold-coin-single'] = json.loads((single / 'source/prompt.json').read_text(encoding='utf-8'))['prompt']
    palette = ROOT / 'ui/journal-preview/preview/assets/refined/pigment.png'
    preserve(palette, target / 'runtime/pigment-palette.png')
    save_json(target / 'source/prompts.json', selected_prompts)
    save_json(target / 'source/pixel-spec.json', {
        'generation': 'built-in image_gen', 'selection': 'user-selected simplified currency; village palette reused',
        'currency': {'gold': [16, 16] if single.exists() else [8, 8], 'maximumOpaqueColors': 6 if single.exists() else 4},
        'items': {'canvas': [32, 32], 'maximumArtwork': [24, 24], 'maximumOpaqueColors': 12},
        'resampling': 'nearest-neighbor', 'alpha': 'binary',
        'pigmentSource': palette.relative_to(ROOT).as_posix(),
        'pigmentSourceSha256': hashlib.sha256(palette.read_bytes()).hexdigest(),
    })
    files = sorted(p.relative_to(target).as_posix() for p in target.rglob('*') if p.is_file() and p.name != 'asset.json')
    save_json(target / 'asset.json', {'format': 'deviant-resource-asset-v1', 'id': 'ui.monster-loot',
        'path': ASSET, 'name': '몬스터 드롭 잉크병·스탬프·간소화 동전과 마을 팔레트',
        'approval': 'selected-for-local-game-preview', 'gameIntegration': 'main-owned',
        'sources': ['built-in image_gen', 'ui.journal-preview'], 'files': files})
    catalog_path = ROOT / 'catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    entry = {'id': 'ui.monster-loot', 'path': ASSET, 'fileCount': len(files)}
    existing = next((item for item in catalog['assets'] if item['id'] == entry['id']), None)
    if existing:
        existing.update(entry)
    else:
        catalog['assets'].append(entry)
    save_json(catalog_path, catalog)
    manifest_path = ROOT / catalog['generatedManifest']
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['files'] = [item for item in manifest['files'] if not item['path'].startswith(ASSET + '/')]
    for name in files:
        data = (target / name).read_bytes()
        manifest['files'].append({'path': ASSET + '/' + name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
    save_json(manifest_path, manifest)
    print(json.dumps({'asset': entry['id'], 'files': len(files)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
