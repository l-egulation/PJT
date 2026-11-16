"""Derive reusable UI art from the approved reference; never bake item/text state into a screen."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid
import zipfile
from authoring_output import prepare_output

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = None
ART = None


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'deviant/' + path.relative_to(ROOT).as_posix()).hex


def sprite_meta(path, border=0):
    meta = f'''fileFormatVersion: 2
guid: {guid(path)}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteMeshType: 0
  spritePixelsToUnits: 100
  spritePivot: {{x: 0.5, y: 0.5}}
  spriteBorder: {{x: {border}, y: {border}, z: {border}, w: {border}}}
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
'''
    Path(str(path) + '.meta').write_text(meta, encoding='utf-8')


def save(image, name, border=0):
    path = ART / (name + '.png')
    image.save(path)
    sprite_meta(path, border)


def half(image):
    return image.resize((max(1, image.width // 2), max(1, image.height // 2)), Image.Resampling.NEAREST)


def blank_inside(image, left, top, right, bottom):
    a = np.array(image.convert('RGBA'))
    for y in range(top, bottom):
        color = a[y, max(0, left - 5)].copy()
        a[y, left:right] = color
    return Image.fromarray(a)


def isolate(image, size=40, magenta=False, fill_holes=True):
    a = np.array(image.convert('RGBA'))
    rgb = a[:, :, :3].astype(float)
    if magenta:
        mask = ~((rgb[:, :, 0] > 180) & (rgb[:, :, 2] > 180) & (rgb[:, :, 1] < 80))
    else:
        bg = np.median(np.concatenate([rgb[:2].reshape(-1, 3), rgb[-2:].reshape(-1, 3), rgb[:, :2].reshape(-1, 3), rgb[:, -2:].reshape(-1, 3)]), axis=0)
        mask = np.linalg.norm(rgb - bg, axis=2) > 23
    labels, count = ndimage.label(mask)
    if count:
        areas = np.bincount(labels.ravel()); areas[0] = 0
        mask = labels == int(areas.argmax())
    if fill_holes and not magenta: mask = ndimage.binary_fill_holes(mask)
    a[:, :, 3] = mask.astype('uint8') * 255
    result = Image.fromarray(a)
    box = result.getbbox()
    if box: result = result.crop(box)
    result.thumbnail((size - 4, size - 4), Image.Resampling.NEAREST)
    out = Image.new('RGBA', (size, size))
    out.alpha_composite(result, ((size - result.width) // 2, (size - result.height) // 2))
    return out


def frame(ref, corner_box, top_box, side_box, corner=32, fill=(43, 29, 51, 255)):
    # 3x3 tiles retain the actual painted pixel border/corner, with no source labels/icons.
    size = corner * 3
    image = Image.new('RGBA', (size, size), fill)
    tl = ref.crop(corner_box).resize((corner, corner), Image.Resampling.NEAREST).convert('RGBA')
    mask = Image.new('L', tl.size); d = ImageDraw.Draw(mask)
    cut = max(3, corner // 4)
    d.polygon([(cut, 0), (corner, 0), (corner, corner), (0, corner), (0, cut), (cut, cut)], fill=255)
    tl.putalpha(mask)
    top = ref.crop(top_box).resize((corner, min(12, corner)), Image.Resampling.NEAREST)
    side = ref.crop(side_box).resize((min(12, corner), corner), Image.Resampling.NEAREST)
    image.paste(top, (corner, 0)); image.paste(top.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (corner, size - top.height))
    image.paste(side, (0, corner)); image.paste(side.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (size - side.width, corner))
    image.alpha_composite(tl, (0, 0)); image.alpha_composite(tl.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (size - corner, 0))
    image.alpha_composite(tl.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (0, size - corner))
    image.alpha_composite(tl.transpose(Image.Transpose.ROTATE_180), (size - corner, size - corner))
    return half(image)


def prepare(reference, weapon_sheet, gameplay, output):
    global ROOT, ART
    ROOT = prepare_output(output)
    ART = ROOT / 'unity/Assets/Deviant/Inventory/Resources/PixelInventory/Art'
    ART.mkdir(parents=True, exist_ok=True)
    ref = Image.open(reference).convert('RGBA')
    assert ref.size == (1672, 941), 'Use the final 1672x941 inventory reference.'
    title = ref.crop((164, 85, 420, 149)).convert('RGBA')
    t = np.array(title); ink = (t[:, :, 0] > 175) & (t[:, :, 1] > 145) & (t[:, :, 2] > 95)
    t[:, :, 3] = ndimage.binary_dilation(ink, iterations=1).astype('uint8') * 255
    save(half(Image.fromarray(t)), 'title-logo')
    save(half(ref.crop((1470, 85, 1525, 138))), 'close', 6)
    save(frame(ref, (112, 56, 156, 100), (210, 56, 254, 68), (112, 210, 124, 254), 44), 'window', 22)
    save(frame(ref, (147, 466, 179, 498), (600, 465, 632, 477), (148, 545, 160, 577), 32), 'card', 16)
    save(frame(ref, (147, 190, 179, 222), (600, 190, 632, 202), (147, 330, 159, 362), 32), 'card-selected', 16)
    slot = ref.crop((1416, 480, 1512, 572))
    slot = blank_inside(slot, 27, 25, 71, 69)
    save(half(slot), 'slot', 6)
    selected = ref.crop((975, 264, 1077, 366))
    a = np.array(selected)
    background = np.array(slot)
    for y in range(12, 93): a[y, 12:91] = background[min(background.shape[0] - 12, int(y * background.shape[0] / a.shape[0])), 20]
    selected = Image.fromarray(a)
    save(half(selected), 'slot-selected', 8)
    for name, box in [('button', (1346, 756, 1524, 810)), ('button-primary', (1088, 756, 1326, 810))]:
        image = ref.crop(box); image = blank_inside(image, 13, 7, image.width - 13, image.height - 7)
        save(half(image), name, 6)
    # Footer completes the source window's L-shaped silhouette; no text is embedded.
    foot = Image.new('RGBA', (488, 122), (43, 29, 51, 255)); d = ImageDraw.Draw(foot)
    d.line([(0, 76), (0, 115), (8, 121), (480, 121), (487, 114), (487, 0)], fill=(182, 119, 165, 255), width=3)
    d.rectangle((477, 110, 487, 121), fill=(249, 212, 155, 255)); d.rectangle((480, 113, 484, 118), fill=(225, 145, 182, 255))
    save(half(foot), 'footer')
    icons = {
        'ink-power': (998, 284, 1058, 354), 'ink-special': (1103, 283, 1170, 354),
        'ink-range': (1215, 283, 1285, 354), 'ink-cooldown': (1325, 283, 1393, 354),
        'ink-speed': (1434, 280, 1494, 353), 'ink-move': (339, 608, 397, 674),
        'ink-follow': (437, 607, 484, 676),
    }
    for name, box in icons.items(): save(isolate(ref.crop(box)), name)
    save(isolate(ref.crop((189, 291, 295, 395)), 48, fill_holes=False), 'weapon-base_sword')
    save(isolate(ref.crop((188, 567, 299, 677)), 48, fill_holes=False), 'weapon-whip')
    sheet = Image.open(weapon_sheet).convert('RGBA')
    names = ['rapier', 'dagger', 'hammer', 'shield', 'greatsword', 'launcher', 'bomb', 'shuriken']
    for i, name in enumerate(names):
        x0 = int((i % 4) * sheet.width / 4); x1 = int((i % 4 + 1) * sheet.width / 4)
        y0 = int((i // 4) * sheet.height / 2); y1 = int((i // 4 + 1) * sheet.height / 2)
        save(isolate(sheet.crop((x0, y0, x1, y1)), 48, True), 'weapon-' + name)
    hud = Image.open(gameplay).convert('RGBA')
    save(isolate(hud.crop((49, 42, 148, 127)), 48), 'portrait')
    save(isolate(hud.crop((1301, 823, 1385, 903)), 40), 'bag')
    # Review images and draft metadata only. Font/game binding and final GUIDs belong to the consumer.
    print('Derived review UI sprites:', len(list(ART.glob('*.png'))))


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--reference', type=Path, required=True)
    p.add_argument('--weapon-sheet', type=Path, required=True)
    p.add_argument('--gameplay', type=Path, required=True)
    p.add_argument('--destination', type=Path, required=True)
    args = p.parse_args(); prepare(args.reference, args.weapon_sheet, args.gameplay, args.destination)
