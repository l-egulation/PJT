"""Build a pixel-sized trial kit from preserved imagegen sources, without painting them."""
from pathlib import Path
import argparse
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / 'environments/waterway-lighting'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    destination = args.destination.resolve()
    if destination.exists() and any(destination.iterdir()):
        raise SystemExit('Use a new empty review directory; existing art is preserved.')
    destination.mkdir(parents=True, exist_ok=True)
    recipe = json.loads((ASSET / 'source/recipe.json').read_text(encoding='utf-8'))
    for part in recipe['parts']:
        source = Image.open(ASSET / part['source']).convert('RGBA')
        w, h = source.size
        box = tuple(round(n * (w if i % 2 == 0 else h)) for i, n in enumerate(part['region']))
        crop = source.crop(box)
        alpha = crop.getchannel('A').point(lambda a: 255 if a >= 128 else 0)
        bounds = alpha.getbbox()
        if bounds is None:
            raise ValueError('No solid pixels in ' + part['name'])
        crop.putalpha(alpha)
        sprite = crop.crop(bounds).resize(tuple(part['size']), Image.Resampling.NEAREST)
        sprite.save(destination / (part['name'] + '.png'))
    print('Built', len(recipe['parts']), 'pixel sprites:', destination)


if __name__ == '__main__':
    main()
