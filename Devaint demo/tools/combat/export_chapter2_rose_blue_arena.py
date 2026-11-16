"""Validate the approved editor layout and copy its text data into Unity Resources."""
import argparse
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[2]
data = json.loads((root / 'data/combat/chapter2_rose_blue_arena.json').read_text(encoding='utf-8'))
assert data['layoutStatus'] == 'user-approved'
assert (data['width'], data['height']) == (30, 18)
platforms = [p for p in data['surfaces'] if p['kind'] == 'platform']
assert [sum(p['layer'] == layer for p in platforms) for layer in (1, 2, 3)] == [2, 3, 2]
for item in data['surfaces'] + data['devices'] + data['markers']:
    assert all(math.isfinite(item[k]) for k in ('x', 'y', 'w', 'h'))
    assert item['w'] > 0 and item['h'] > 0
    x = item['x'] - item['w'] / 2 if 'assetId' in item else item['x']
    assert x >= 0 and x + item['w'] <= 30 and item['y'] >= 0 and item['y'] + item['h'] <= 18
machine, = data['devices']
assert (machine['x'], machine['y'], machine['w'], machine['h']) == (14, 9.5, 2, 3)
out = root / 'unity/Assets/Chapter2/Resources/RoseBlueArena.json'
text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--check', action='store_true')
if parser.parse_args().check:
    assert out.read_text(encoding='utf-8') == text, 'Stale Unity arena data'
else:
    out.write_text(text, encoding='utf-8')
print('Approved Rose/Blue arena synchronized')
