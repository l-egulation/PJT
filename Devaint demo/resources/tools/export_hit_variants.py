"""Extract approved impact cores to an empty review directory; never write game .meta files."""
import argparse
import json
from pathlib import Path
from PIL import Image

from authoring_output import ROOT, prepare_output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe', type=Path, required=True, help='Main-owned hit_variants.json')
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    data = json.loads(args.recipe.read_text(encoding='utf-8'))
    source_path = (ROOT / data['source'].removeprefix('resources/')).resolve()
    if not source_path.is_relative_to(ROOT):
        raise ValueError('Art source must belong to resources')
    source = Image.open(source_path).convert('RGB')
    size = data['cellSize']
    if source.size != (size * data['columns'], size * data['rows']):
        raise ValueError('Source image dimensions differ from recipe')
    target = prepare_output(args.destination)
    for i, (px, py) in enumerate(data['pivots']):
        x, y = i % data['columns'] * size, i // data['columns'] * size
        cell = source.crop((x, y, x + size, y + size)).resize(
            (data['outputSize'], data['outputSize']), Image.Resampling.NEAREST)
        output = Image.new('RGBA', cell.size)
        output.putdata([(255, 255, 255, 255 if min(rgb) >= data['whiteThreshold'] else 0)
                        for rgb in cell.getdata()])
        output.save(target / f'{i:02}.png')
    print('Extracted six impact images for review. Unity GUIDs/pivots remain main-owned.')


if __name__ == '__main__':
    main()
