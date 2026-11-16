"""Preserve the user's Bounce Aether sheet/GIF and extract unmodified 640x360 cells."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--sheet', type=Path, required=True)
    parser.add_argument('--gif', type=Path, required=True)
    parser.add_argument('--out', default='bounce-aether-01')
    parser.add_argument('--prompt', default='../bounce-aether/prompt.txt')
    args = parser.parse_args()
    source = GROUP / 'source' / args.out
    if source.exists():
        raise SystemExit('Refusing to overwrite an existing extraction.')
    source.mkdir()
    original = source / 'aether-original.png'
    gif = source / 'aether-original.gif'
    shutil.copy2(args.sheet, original)
    shutil.copy2(args.gif, gif)
    im = Image.open(original).convert('RGBA')
    assert im.size == (3840, 2160), im.size
    g = Image.open(gif)
    gif_durations = []
    for i in range(g.n_frames):
        g.seek(i)
        gif_durations.append(g.info.get('duration'))
    frames = source / 'frames'
    frames.mkdir()
    records = []
    for i in range(36):
        x, y = i % 6 * 640, i // 6 * 360
        cell = im.crop((x, y, x + 640, y + 360))
        cell.save(frames / f'{i:02}.png')
        mask = Image.new('L', cell.size)
        mask.putdata([255 if min(p[:3]) < 200 and p[3] > 0 else 0 for p in cell.get_flattened_data()])
        records.append({'sourceFrame': i, 'cell': [x, y, 640, 360], 'file': f'frames/{i:02}.png', 'foregroundMeasurementBounds': mask.getbbox()})
    (source / 'extraction.json').write_text(json.dumps({
        'sourceName': args.sheet.name, 'sourceBytes': original.stat().st_size, 'sourceSha256': sha(original),
        'sourceSize': list(im.size), 'grid': [6, 6], 'cellSize': [640, 360], 'numbering': 'zero-based, row-major',
        'gifName': args.gif.name, 'gifSha256': sha(gif), 'gifSize': list(g.size), 'gifFrames': g.n_frames,
        'gifDurationsMs': gif_durations, 'gifRole': 'Aether preview only; the PNG sheet is the pixel source.',
        'prompt': args.prompt, 'startImage': '../bounce-aether/bounce-start-white-3x.png',
        'frames': records, 'pixelEdits': False}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    contact = Image.new('RGB', (1920, 1260), '#e5e5e5')
    draw = ImageDraw.Draw(contact)
    for n in range(36):
        x, y = n % 6 * 320, n // 6 * 210
        contact.paste(Image.open(frames / f'{n:02}.png').convert('RGB').resize((320, 180), Image.NEAREST), (x, y + 24))
        draw.text((x + 8, y + 6), f'{n:02}', fill='black')
    contact.save(source / 'inspection-contact.png')
    print(json.dumps([r['foregroundMeasurementBounds'] for r in records]))


if __name__ == '__main__':
    main()
