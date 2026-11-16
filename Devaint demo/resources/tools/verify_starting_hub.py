"""Check native output contracts independently of the source exporter."""
from pathlib import Path
import json

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def main():
    layout = json.loads((ROOT / 'ui/roguelite-hub/source/sprite-layout.json').read_text(encoding='utf-8'))
    sprites = layout['sprites']
    assert len(sprites) == 55
    assert len({s['id'] for s in sprites}) == len(sprites)
    pairs = {}
    icons = frames = backgrounds = 0
    for item in sprites:
        with Image.open(ROOT / item['path']) as image:
            assert list(image.size) == item['size'], item['id']
            image.load()
            if item.get('alpha') == 'binary':
                assert image.mode == 'RGBA', item['id']
                alpha = image.getchannel('A')
                assert set(alpha.getdata()) <= {0, 255}, item['id']
            if item.get('state'):
                key = item['id'].rsplit('.', 1)[0]
                pairs.setdefault(key, {})[item['state']] = (image.size, alpha.tobytes(), item['pivot'])
                assert image.getbbox()[3] <= image.height-8, item['id']
            if item.get('tint'):
                icons += 1
                assert image.size == (32, 32)
                assert all(r == g == b == 255 for r, g, b, a in image.getdata() if a), item['id']
            if 'frameDurationMs' in item:
                frames += 1
                if item['id'].endswith('.08'):
                    assert alpha.getbbox() is None, item['id']
            if item.get('kind') == 'static-story-backdrop':
                backgrounds += 1
                assert image.size == (960, 540)
    assert len(pairs) == 10 and icons == 16 and frames == 16 and backgrounds == 3
    for name, states in pairs.items():
        assert states['color'] == states['sketch'], name
    print('PASS: 55 outputs; 10 identical-geometry state pairs; 16 binary white glyphs; '
          '16 effect frames with transparent endings; 3 story backdrops.')


if __name__ == '__main__': main()
