"""Export the selected single-coin generation to a native 16px runtime sprite."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    (args.output / 'source').mkdir()
    (args.output / 'runtime').mkdir()
    shutil.copy2(args.input, args.output / 'source/generated.png')
    shutil.copy2(args.reference, args.output / 'source/reference.png')
    image = Image.open(args.input).convert('RGBA')
    box = image.getchannel('A').point(lambda a: 255 if a >= 96 else 0).getbbox()
    assert box
    crop = image.crop(box)
    ratio = min(14 / crop.width, 14 / crop.height)
    size = tuple(max(1, round(n * ratio)) for n in crop.size)
    small = crop.resize(size, Image.Resampling.NEAREST)
    alpha = small.getchannel('A').point(lambda a: 255 if a >= 128 else 0)
    small = small.convert('RGB').quantize(colors=6, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert('RGBA')
    small.putalpha(alpha)
    sprite = Image.new('RGBA', (16, 16))
    sprite.paste(small, ((16 - size[0]) // 2, (16 - size[1]) // 2))
    sprite.putdata([rgba if rgba[3] else (0, 0, 0, 0) for rgba in sprite.get_flattened_data()])
    sprite.save(args.output / 'runtime/gold-coin-single.png')
    sprite.resize((256, 256), Image.Resampling.NEAREST).save(args.output / 'single-coin-16x.png')
    pixels = list(sprite.get_flattened_data())
    colors = {p[:3] for p in pixels if p[3]}
    assert len(colors) <= 6 and {p[3] for p in pixels} == {0, 255}
    spec = {'generation': 'built-in image_gen', 'canvas': [16, 16], 'maximumArtwork': [14, 14],
            'opaqueColors': len(colors), 'alpha': 'binary', 'resampling': 'nearest-neighbor',
            'bounds': list(sprite.getbbox()), 'sourceCrop': list(box),
            'sourceSha256': hashlib.sha256(args.input.read_bytes()).hexdigest()}
    (args.output / 'source/pixel-spec.json').write_text(json.dumps(spec, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(spec))


if __name__ == '__main__':
    main()
