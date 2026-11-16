"""Animate only the approved pre-thrust robe and scarf with integer pixel shifts.
No generated pose, body repaint, rescaling, interpolation, or palette changes.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSET = Path('characters/ink/weapons/rapier')
ORIGINAL = ASSET / 'runtime/motion-20261108/charge'
RUNTIME = ASSET / 'runtime/charge-hold-direct-20261112'
PREVIEW = ASSET / 'preview/charge-hold-direct-20261112'


def retouch(image, source_frame):
    pixels = np.array(image.convert('RGBA'))
    rgb = pixels[:, :, :3].astype(np.int32)
    yy, xx = np.indices(pixels.shape[:2])
    # The raised empty LEFT hand is a plain round mitten. The right weapon
    # hand, rapier and cuffs keep their original pixels.
    boxes = {2: ((289, 237, 309, 256), (290, 238, 308, 256)),
             4: ((302, 173, 329, 208), (303, 182, 327, 206)),
             9: ((298, 164, 325, 201), (300, 175, 323, 198)),
             15: ((299, 163, 326, 201), (301, 176, 323, 198))}
    erase, circle = boxes[source_frame]
    x0, y0, x1, y1 = erase
    black = (rgb == (27, 22, 22)).all(axis=2) & (pixels[:, :, 3] > 0)
    region = (xx >= x0) & (xx < x1) & (yy >= y0) & (yy < y1)
    if source_frame == 2:
        # This early source drawing has exposed fingers below the blade.
        exposed = (rgb[:, :, 0] > 180) & (yy >= 244)
        pixels[region & (black | exposed)] = 0
    else:
        pixels[region & black] = 0
    # Loose black edge pixels on the trailing scarf/cape tips are not arms.
    # Turn only those fabric-edge pixels into the original dark teal.
    fabric_edge = ((xx < 190) & (yy >= 175) & (yy < 218)) | ((xx < 150) & (yy >= 240) & (yy < 270)) | ((xx < 190) & (yy >= 255) & (yy < 270))
    edge = black & fabric_edge
    pixels[edge] = (2, 86, 115, 255)
    result = Image.fromarray(pixels)
    ImageDraw.Draw(result).ellipse(circle, fill=(27, 22, 22, 255))
    return result


def create(root=ROOT):
    original = root / ORIGINAL
    output = root / RUNTIME
    for kind in ('prepare', 'hold'):
        (output / kind).mkdir(parents=True, exist_ok=True)
    # Four original preparation drawings stop before thrust contact. Only the
    # requested left mitten and loose fabric outlines receive a direct retouch.
    for i, frame in enumerate((2, 4, 9, 15)):
        retouch(Image.open(original / f'{frame:03d}.png'), frame).save(output / 'prepare' / f'{i:02d}.png')
    base = np.array(retouch(Image.open(original / '015.png'), 15))
    yy, xx = np.indices(base.shape[:2])
    rgb = base[:, :, :3].astype(np.int32)
    opaque = base[:, :, 3] > 0
    blue = (rgb[:, :, 2] > rgb[:, :, 0] + 30) & (rgb[:, :, 1] > rgb[:, :, 0] + 20)
    scarf = opaque & blue & (xx < 200) & (yy >= 175) & (yy < 265)
    cream = (rgb[:, :, 0] > 180) & (rgb[:, :, 1] > 150) & (rgb[:, :, 2] > 120)
    robe = opaque & cream & (yy >= 270) & (yy < 316)
    cloth = scarf | robe
    fixed = opaque & ~cloth
    palette = set(map(tuple, base[opaque, :3]))
    for index in range(8):
        phase = index * math.tau / 8
        pixels = base.copy()
        pixels[cloth] = 0
        layer = np.zeros_like(base)
        sy, sx = np.where(scarf)
        for y, x in zip(sy, sx):
            u = min(1, max(0, (200 - int(x)) / 73))
            wave = (math.sin(phase + u * 1.5) - math.sin(u * 1.5))
            dy = round(5 * u * wave)
            layer[y + dy, x] = base[y, x]
        ry, rx = np.where(robe)
        for y, x in zip(ry, rx):
            u = (int(y) - 270) / 46
            wave = math.sin(phase + u * 1.2) - math.sin(u * 1.2)
            dx = round(3 * u * wave)
            layer[y, x + dx] = base[y, x]
        # Fixed opaque pixels draw in front of moving cloth: head, face, arms,
        # both mitten hands, complete sword and planted feet are byte-identical.
        moving = (layer[:, :, 3] > 0) & ~fixed
        pixels[moving] = layer[moving]
        assert np.array_equal(pixels[fixed], base[fixed])
        assert set(map(tuple, pixels[pixels[:, :, 3] > 0, :3])) <= palette
        Image.fromarray(pixels).save(output / 'hold' / f'{index:02d}.png')
    preview = root / PREVIEW
    preview.mkdir(parents=True, exist_ok=True)
    frames = [Image.open(output / 'hold' / f'{i:02d}.png').convert('RGBA') for i in range(8)]
    display = []
    for frame in frames:
        bg = Image.new('RGBA', frame.size, (78, 80, 90, 255)); bg.alpha_composite(frame)
        display.append(bg.convert('RGB'))
    display[0].save(preview / 'charge-hold.gif', save_all=True, append_images=display[1:], duration=60, loop=0, disposal=2)
    # Comparison shown at a shared 32 PPU; preview resizing never affects assets.
    idle = Image.open(root / ASSET / 'runtime/approved-eleven/character/F030.png').convert('RGBA')
    sheet = Image.new('RGB', (1050, 430), (78, 80, 90)); draw = ImageDraw.Draw(sheet)
    for col, (name, image, ppu) in enumerate((('Original idle', idle, 32), ('Original pre-thrust 015', Image.open(original / '015.png').convert('RGBA'), 106.6667), ('Round left fist + cloth loop', frames[2], 106.6667))):
        image = image.crop(image.getbbox()); ratio = 128 / ppu
        image = image.resize((round(image.width * ratio), round(image.height * ratio)), Image.Resampling.NEAREST)
        sheet.paste(image, (col * 350 + 15, 380 - image.height), image)
        draw.text((col * 350 + 10, 15), name, fill='white')
    sheet.save(preview / 'comparison.png')
    contact = Image.new('RGB', (640 * 4, 384 * 2), (78, 80, 90)); draw = ImageDraw.Draw(contact)
    for i, frame in enumerate(frames):
        x, y = i % 4 * 640, i // 4 * 384
        contact.paste(frame, (x, y), frame); draw.text((x + 10, y + 360), 'hold ' + str(i), fill='white')
    contact.save(preview / 'frames.png')
    print('Created original 640x360 preparation and cloth-only hold frames; fixed body RGBA and six-colour palette verified')


if __name__ == '__main__':
    create()
