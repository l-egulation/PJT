"""Draw loot icons on their native 16px grid; no generated bitmap or downsampling input."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / 'ui/monster-loot-flat'


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def initial_source():
    empty = '.' * 16
    bottle = [empty, empty, '.....oooooo.....', '.....oooooo.....', '.....occcco.....', '....occcccco....',
              *(['...oxxxxxxxxo...'] * 7), '....oooooooo....', empty, empty]
    stamp = [empty, '......oooo......', '.....oxxxxo.....', '.....oxxxxo.....',
             *(['......oxxo......'] * 4), '....oooooooo....', *(['...oxxxxxxxxo...'] * 4), '....oooooooo....', empty, empty]
    colors = {'red': '#E76754', 'yellow': '#EFC34F', 'green': '#76AA70', 'blue': '#68AACA', 'purple': '#A482BA'}
    icons = [{'id': 'ink-flat-' + name, 'color': color, 'pixels': bottle} for name, color in colors.items()]
    for name, color, mark in [('weapon', '#D6A052', [(7, 10), (6, 11), (7, 11), (8, 11), (7, 12)]),
                              ('skill', '#63AAA5', [(7, 10), (6, 11), (8, 11), (7, 12)])]:
        rows = [list(row) for row in stamp]
        for x, y in mark:
            rows[y][x] = 'c'
        icons.append({'id': name + '-stamp-flat', 'color': color, 'pixels': [''.join(row) for row in rows]})
    return {'canvas': [16, 16], 'palette': {'.': '#00000000', 'o': '#40383A', 'c': '#E8DEBF'},
            'pixelsPerUnit': 16, 'method': 'native pixel-grid drawing', 'icons': icons}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--authoring-output', type=Path)
    args = parser.parse_args()
    source = ASSET / 'source/icons.json'
    if not source.exists():
        save(source, initial_source())
    spec = json.loads(source.read_text(encoding='utf-8'))
    runtime = ASSET / 'runtime'; runtime.mkdir(parents=True, exist_ok=True)
    preview_ids = spec.get('previewOrder', [item['id'] for item in spec['icons']])
    preview_slots = {name: i for i, name in enumerate(preview_ids)}
    preview = Image.new('RGB', (max(900, 16 + len(preview_ids) * 126), 230), '#F4EEDD')
    draw = ImageDraw.Draw(preview)
    draw.text((16, 12), 'NATIVE PIXEL GRID / UNIFORM OUTLINE / REFINED LOOT ICONS', fill='#40383A')
    currencies = Image.new('RGB', (360, 230), '#F4EEDD')
    currency_draw = ImageDraw.Draw(currencies)
    currency_draw.text((16, 12), 'COIN / PIGMENT PALETTE', fill='#40383A')
    currency_count = 0
    records = []
    rendered = {}
    for i, item in enumerate(spec['icons']):
        rows = item['pixels']
        width, height = item.get('canvas', spec['canvas'])
        assert len(rows) == height and all(len(row) == width for row in rows)
        palette = {**spec['palette'], 'x': item['color'], **item.get('palette', {})}
        sprite = Image.new('RGBA', (width, height))
        sprite.putdata([tuple(bytes.fromhex(palette[ch].lstrip('#'))) if ch == '.' else tuple(bytes.fromhex(palette[ch].lstrip('#'))) + (255,)
                        for row in rows for ch in row])
        colors = {p for p in sprite.get_flattened_data() if p[3]}
        assert len(colors) <= item.get('maximumOpaqueColors', 3)
        assert {p[3] for p in sprite.get_flattened_data()} <= {0, 255}
        output = runtime / (item['id'] + '.png'); sprite.save(output)
        rendered[item['id']] = sprite
        actual = sprite.resize((width * 2, height * 2), Image.Resampling.NEAREST)
        if item['id'] in preview_slots:
            x = 16 + preview_slots[item['id']] * 126
            large = sprite.resize((width * 6, height * 6), Image.Resampling.NEAREST)
            preview.paste(large, (x + 10 + (96 - large.width) // 2, 44 + (96 - large.height) // 2), large)
            preview.paste(actual, (x + 10 + (96 - actual.width) // 2, 158 + (32 - actual.height) // 2), actual)
            draw.text((x, 207), item.get('label', item['id'].replace('-flat', '')), fill='#40383A')
        if item['id'] in ['gold-flat-small', 'pigment-palette-flat'] and item['id'] in preview_slots:
            x = 30 + currency_count * 170
            large = sprite.resize((width * 8, height * 8), Image.Resampling.NEAREST)
            currencies.paste(large, (x + (128 - large.width) // 2, 35 + (128 - large.height) // 2), large)
            currencies.paste(actual, (x + (128 - actual.width) // 2, 174 + (32 - actual.height) // 2), actual)
            currency_draw.text((x + 27, 214), item['id'].replace('-flat', ''), fill='#40383A')
            currency_count += 1
        records.append({'id': item['id'], 'canvas': [width, height], 'opaqueColors': len(colors), 'bounds': sprite.getbbox(), 'sha256': hashlib.sha256(output.read_bytes()).hexdigest()})
    (ASSET / 'preview').mkdir(exist_ok=True)
    preview.save(ASSET / 'preview/native-grid.png')
    if currency_count: currencies.save(ASSET / 'preview/currencies.png')
    refined = ['ink-refined-' + color for color in ['red', 'yellow', 'green', 'blue', 'purple']] + ['weapon-stamp-refined', 'skill-stamp-refined']
    original = ['ink-flat-' + color for color in ['red', 'yellow', 'green', 'blue', 'purple']] + ['weapon-stamp-flat', 'skill-stamp-flat']
    if all(name in rendered for name in [*original, *refined]):
        comparison = Image.new('RGB', (900, 310), '#F4EEDD')
        comparison_draw = ImageDraw.Draw(comparison)
        for label, names, top in [('BEFORE', original, 34), ('AFTER', refined, 183)]:
            comparison_draw.text((16, top - 20), label, fill='#40383A')
            for i, name in enumerate(names):
                large = rendered[name].resize((96, 96), Image.Resampling.NEAREST)
                comparison.paste(large, (26 + i * 126, top), large)
        comparison.save(ASSET / 'preview/refined-comparison.png')
    save(ASSET / 'source/pixel-spec.json', {'method': spec['method'], 'canvas': [16, 16], 'maximumOpaqueColors': max(item.get('maximumOpaqueColors', 3) for item in spec['icons']),
        'alpha': 'binary', 'nativeRendering': 'integer 2x at a 32 PPU camera', 'records': records})
    files = sorted(p.relative_to(ASSET).as_posix() for p in ASSET.rglob('*') if p.is_file() and p.name != 'asset.json')
    save(ASSET / 'asset.json', {'format': 'deviant-resource-asset-v1', 'id': 'ui.monster-loot-flat',
        'path': ASSET.relative_to(ROOT).as_posix(), 'name': '16px 단순 드롭 잉크·스탬프·재화',
        'approval': spec.get('approval', 'local-preview-requested'), 'selectedIcons': preview_ids,
        'gameIntegration': 'main-owned', 'sources': ['native editable pixel grid'], 'files': files})
    catalog_path = ROOT / 'catalog.json'; catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    entry = {'id': 'ui.monster-loot-flat', 'path': ASSET.relative_to(ROOT).as_posix(), 'fileCount': len(files)}
    existing = next((a for a in catalog['assets'] if a['id'] == entry['id']), None)
    if existing: existing.update(entry)
    else: catalog['assets'].append(entry)
    save(catalog_path, catalog)
    index_path = ROOT / catalog['generatedManifest']; index = json.loads(index_path.read_text(encoding='utf-8'))
    index['files'] = [f for f in index['files'] if not f['path'].startswith(entry['path'] + '/')]
    for name in files:
        data = (ASSET / name).read_bytes()
        index['files'].append({'path': entry['path'] + '/' + name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
    save(index_path, index)
    if args.authoring_output:
        args.authoring_output.mkdir(parents=True, exist_ok=False)
        for name in [*files, 'asset.json']:
            target = args.authoring_output / name; target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ASSET / name, target)
    print(json.dumps({'asset': entry['id'], 'icons': len(records), 'files': len(files), 'nativeCanvas': [16, 16]}))


if __name__ == '__main__':
    main()
