from io import BytesIO
from pathlib import Path

from django.core.files.base import ContentFile
from PIL import Image, ImageChops


TARGET_WIDTH = 768
TARGET_HEIGHT = 616
FLATLAY_WIDTH = 600
FLATLAY_HEIGHT = 800
PADDING_RATIO = 0.08


def _open_rgba(source):
    if isinstance(source, bytes):
        source = BytesIO(source)
    if hasattr(source, 'seek'):
        source.seek(0)
    image = Image.open(source).convert('RGBA')
    if hasattr(source, 'seek'):
        source.seek(0)
    return image


def _remove_background(source):
    image = _open_rgba(source)
    try:
        from rembg import remove

        return remove(image).convert('RGBA')
    except Exception:
        return image


def _foreground_box(image):
    alpha = image.getchannel('A')
    box = alpha.getbbox()
    if box:
        return box
    background = Image.new('RGBA', image.size, image.getpixel((0, 0)))
    diff = ImageChops.difference(image, background)
    return diff.getbbox() or (0, 0, image.width, image.height)


def _fit_to_box(image, width=TARGET_WIDTH, height=TARGET_HEIGHT):
    cropped = image.crop(_foreground_box(image))
    padding = int(min(width, height) * PADDING_RATIO)
    cropped.thumbnail((width - (padding * 2), height - (padding * 2)), Image.Resampling.LANCZOS)

    canvas = Image.new('RGBA', (width, height), (255, 255, 255, 0))
    x = (width - cropped.width) // 2
    y = (height - cropped.height) // 2
    canvas.alpha_composite(cropped, (x, y))
    return canvas


def process_clothing_image(source, original_name='clothing.png'):
    image = _remove_background(source)
    processed = _fit_to_box(image)
    output = BytesIO()
    processed.save(output, format='PNG', optimize=True)
    stem = Path(original_name or 'clothing').stem or 'clothing'
    return ContentFile(output.getvalue(), name=f'{stem}_processed.png')


def process_flatlay_image(source, original_name='clothing.png'):
    image = _remove_background(source)
    processed = _fit_to_box(image, width=FLATLAY_WIDTH, height=FLATLAY_HEIGHT)
    output = BytesIO()
    processed.save(output, format='PNG', optimize=True)
    stem = Path(original_name or 'clothing').stem or 'clothing'
    return ContentFile(output.getvalue(), name=f'{stem}_flatlay.png')
