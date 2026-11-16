"""Finish the generated charge pose on the original six-colour pixel palette.

The model's head/hand/weapon drift is excluded: hold frames animate cloth only.
Combat timing and Unity import metadata belong to the main repository.
"""
from pathlib import Path
import argparse
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSET = Path('characters/ink/weapons/rapier')
SOURCE = ASSET / 'source/charge-hold-20261112'
RUNTIME = ASSET / 'runtime/charge-hold-20261112'
PREVIEW = ASSET / 'preview/charge-hold-20261112'


def finish(image, palette):
    rgba = np.array(image.convert('RGBA'))
    rgb = rgba[:, :, :3].astype(np.int32)
    distance = ((rgb[:, :, None, :] - palette[None, None, :, :]) ** 2).sum(axis=3)
    rgba[:, :, :3] = palette[distance.argmin(axis=2)]
    rgba[:, :, 3] = (rgba[:, :, 3] >= 128) * 255
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return Image.fromarray(rgba)


def create(root=ROOT):
    source = root / SOURCE
    reference = np.array(Image.open(source / 'reference-native.png').convert('RGBA'))
    palette = np.unique(reference[reference[:, :, 3] == 255, :3], axis=0).astype(np.int32)
    base = finish(Image.open(source / 'hold-pose-native.png'), palette)
    # The corrected imagegen hand is a solid mitten, matching the original
    # character. Restrict the correction to the empty guard hand only.
    mitten = finish(Image.open(source / 'no-fingers-native.png'), palette)
    hand_box = (128, 81, 143, 93)
    base.paste(mitten.crop(hand_box), hand_box[:2])
    base_pixels = np.array(base)
    output = root / RUNTIME
    for kind in ('prepare', 'hold'):
        (output / kind).mkdir(parents=True, exist_ok=True)
    frames = []
    for i in range(4):
        frame = finish(Image.open(source / 'generated/prepare' / f'{i:02d}.png'), palette)
        if i > 0:
            frame.paste(mitten.crop(hand_box), hand_box[:2])
        frame.save(output / 'prepare' / f'{i:02d}.png')
        frames.append(frame)
    for i, selected in enumerate((0, 1, 2, 3, 4, 3, 2, 1)):
        generated = np.array(finish(Image.open(source / 'generated/hold' / f'{selected:02d}.png'), palette))
        pixels = base_pixels.copy()
        # Scarf: only the blue fabric is replaced. The hood, glove, shoulder,
        # gold fastening and all black weapon pixels remain the base artwork.
        blue = lambda a: (a[:, :, 2] > a[:, :, 0] + 30) & (a[:, :, 1] > a[:, :, 0] + 20) & (a[:, :, 3] > 0)
        old_blue, new_blue = blue(base_pixels.astype(np.int32)), blue(generated.astype(np.int32))
        pixels[old_blue] = 0
        pixels[new_blue] = generated[new_blue]
        # Trailing split-robe flap, below the weapon and above the planted feet.
        pixels[101:120, 55:103] = generated[101:120, 55:103]
        # The coat-flap rectangle meets the bottom of the rapier guard. Pin the
        # complete grip/blade region after cloth composition so no weapon pixel
        # is replaced by a moving hem or scarf edge.
        pixels[88:105, 78:172] = base_pixels[88:105, 78:172]
        frame = Image.fromarray(pixels)
        frame.save(output / 'hold' / f'{i:02d}.png')
        frames.append(frame)
    preview = root / PREVIEW
    preview.mkdir(parents=True, exist_ok=True)
    contact = Image.new('RGB', (256 * 4, 168 * 3), (78, 80, 90))
    draw = ImageDraw.Draw(contact)
    for i, frame in enumerate(frames):
        x, y = i % 4 * 256, i // 4 * 168
        contact.paste(frame, (x, y), frame)
        draw.text((x + 6, y + 145), ('prepare ' + str(i)) if i < 4 else ('hold ' + str(i - 4)), fill='white')
    contact.save(preview / 'frames.png')
    loop = []
    for frame in frames[4:]:
        canvas = Image.new('RGBA', frame.size, (78, 80, 90, 255)); canvas.alpha_composite(frame)
        loop.append(canvas.convert('RGB').resize((768, 432), Image.Resampling.NEAREST))
    loop[0].save(preview / 'charge-hold.gif', save_all=True, append_images=loop[1:], duration=60, loop=0, disposal=2)
    print('Created 4 preparation frames and 8 cloth-only hold frames:', output)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    create(parser.parse_args().root)
