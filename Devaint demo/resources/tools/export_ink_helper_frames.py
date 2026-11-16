"""Split the preserved ink-helper sheet into native-size animation frames."""
import hashlib
import json
from pathlib import Path

from PIL import Image
from authoring_output import ROOT, destination


def main():
    output = destination(__doc__)
    source = ROOT / 'characters/story/ink-helper/runtime/pixel/sheet.png'
    layout = source.with_suffix('.json')
    spec = json.loads(layout.read_text(encoding='utf-8'))
    width, height = spec['frame']
    sheet = Image.open(source).convert('RGBA')
    assert sheet.size == (28 * width, 3 * height), sheet.size
    frames = []
    for clip, row in spec['rows'].items():
        for i in range(row['count']):
            rect = (i * width, row['row'] * height, (i + 1) * width, (row['row'] + 1) * height)
            image = sheet.crop(rect)
            target = output / clip / f'{i:02d}.png'
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target)
            assert Image.open(target).convert('RGBA').tobytes() == image.tobytes()
            frames.append({'path': target.relative_to(output).as_posix(), 'sourceRect': list(rect),
                           'rgbaSha256': hashlib.sha256(image.tobytes()).hexdigest()})
    manifest = {'source': source.relative_to(ROOT).as_posix(),
                'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                'layoutSha256': hashlib.sha256(layout.read_bytes()).hexdigest(),
                'frame': spec['frame'], 'anchor': spec['anchor'], 'frames': frames}
    (output / 'frames.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'Exported and pixel-verified {len(frames)} frames at {width}x{height}; source sheet unchanged.')


if __name__ == '__main__':
    main()
