"""Copy the clock boss HTML preview art (parts, attack sprites, FX, collapse pieces) into Unity Resources.

The Unity test room assembles these at runtime exactly like the preview (app.mjs). They are preview
parts, not approved boss motion clips. The parts' cut edges are filled first (mend_clock_boss_parts.py),
because Unity bobs each part on its own delay; the preview originals are left untouched.
"""
import hashlib
import json
import shutil
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from mend_clock_boss_parts import mend
from pixelate_clock_boss_needle import pixelate, HEIGHT as NEEDLE_HEIGHT
from authoring_output import ROOT as RESOURCE_ROOT, WORKBENCH, destination

SOURCE = WORKBENCH / 'MonsterReference/local-assets/hanging-clock-boss-battle-v1'
SOURCE_MANIFEST = RESOURCE_ROOT / 'monsters/stage-01/bosses/clock-executioner/source/battle-preview/asset-manifest.json'
# 2026-11-10 redesign that fills the needle's 96 x 384 px hit column (playtest: art and hit box differed).
NEEDLE_REDESIGN = RESOURCE_ROOT / 'monsters/stage-01/bosses/clock-executioner/source/falling-needle-redesign-20261110/falling-needle-v3.webp'
PART_PIVOT = [192, 245]  # dial centre; hands rotate here and every part shares one origin
# The boss has no white hit flash and no ink splashes (user decisions), so those masks and the ink FX are not exported.
FX = ['dust', 'hit', 'needleImpact', 'dissolve', 'collapsePlume']


def rgb(colour):
    return tuple(bytes.fromhex(colour[1:]))


def main():
    ROOT = destination(__doc__)
    DEST = ROOT / 'unity/Assets/Deviant/Stage1Monsters/Resources/ClockExecutionerPreview'
    UNITY = ROOT / 'unity'
    source_bytes = SOURCE_MANIFEST.read_bytes()
    manifest = json.loads(source_bytes)
    boss = manifest['boss']
    palette = boss['palette']
    output = dict(sourceSHA256=hashlib.sha256(source_bytes).hexdigest(), ppu=32, clips=[], pieces=[])

    def add(clip_id, role, images, pivot, colours, fps=0, fade=None):
        folder = DEST / clip_id
        folder.mkdir(parents=True, exist_ok=True)
        allowed = {rgb(c) for c in colours}
        frames, size, opacity = [], None, []
        for i, image in enumerate(images):
            pixels = np.array(image.convert('RGBA'))
            alphas = set(np.unique(pixels[:, :, 3])) - {0}
            # FX may fade by one uniform frame opacity (dust); every other pixel stays opaque.
            assert len(alphas) <= 1 and (role == 'fx' or alphas <= {255}), (clip_id, i)
            opacity.append(int(next(iter(alphas), 255)))
            assert set(map(tuple, pixels[pixels[:, :, 3] > 0][:, :3])).issubset(allowed), (clip_id, i)
            size = size or image.size
            assert image.size == size, (clip_id, i)
            target = folder / f'{i:02}.png'
            Image.fromarray(pixels).save(target)
            frames.append(dict(path=target.relative_to(UNITY).as_posix(),
                               resource=target.relative_to(DEST.parent).with_suffix('').as_posix(),
                               sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
        width, height = size
        output['clips'].append(dict(id=clip_id, role=role, width=width, height=height, fps=fps, loopFrom=-1, startAt=0,
                                    pivotPx=list(pivot), pivot=[pivot[0] / width, 1 - pivot[1] / height],
                                    fadeAfter=(fade or [0, 0])[0], fadeDuration=(fade or [0, 0])[1],
                                    palette=list(colours), opacity=opacity, frames=frames))

    load = lambda path: Image.open(SOURCE / path).convert('RGBA')
    parts = mend({k: load(v) for k, v in boss['parts'].items()})
    for part_id, image in parts.items():
        add('part_' + part_id, 'part', [image], PART_PIVOT, palette)
    singles = {
        'giantFist': (boss['giantFist'], boss['giantFistPivot']),
        'armTile': (boss['armTile'], [0, 74]),
        'needle': (boss['needle'], [48, NEEDLE_HEIGHT]),
        'foregroundBody': (boss['foregroundBody'], boss['foregroundPivot']),
        'blade_minute': (boss['blade_minute'], boss['bladePivot']),
        'blade_hour': (boss['blade_hour'], boss['bladePivot']),
        'bladeHub': (boss['bladeHub'], [16, 16]),
    }
    for clip_id, (path, pivot) in singles.items():
        image = load(path)
        if clip_id == 'needle':
            image = pixelate(Image.open(NEEDLE_REDESIGN), palette)
        if clip_id == 'armTile':
            pivot = [0, image.size[1] // 2]
        add(clip_id, 'part', [image], pivot, palette)
    # Tether links: app.mjs drawTether() strokes each 13 px link with pixel lines; bake both gauges with
    # the path running down the image (+y), so Unity only rotates them along the chain.
    def stamp(draw, x0, y0, x1, y1, colour, width):
        x0, y0, x1, y1 = (round(v) for v in (x0, y0, x1, y1))
        dx, sx, dy, sy = abs(x1 - x0), 1 if x0 < x1 else -1, -abs(y1 - y0), 1 if y0 < y1 else -1
        err = dx + dy
        while True:
            draw.rectangle([x0, y0, x0 + width - 1, y0 + width - 1], fill=colour)
            if x0 == x1 and y0 == y1:
                break
            e = 2 * err
            if e >= dy:
                err += dy; x0 += sx
            if e <= dx:
                err += dx; y0 += sy
    for name, wide in (('chainLink_wide', 6), ('chainLink_narrow', 3)):
        link = Image.new('RGBA', (2 * wide + 8, 28))
        draw = ImageDraw.Draw(link)
        cx, cy = wide + 3, 13
        pts = [(-wide + 2, -10), (wide - 2, -10), (wide, -7), (wide, 7), (wide - 2, 10), (-wide + 2, 10), (-wide, 7), (-wide, -7)]
        to_px = lambda p: (cx - p[0], cy + p[1])  # normal = (-1,0) when the path runs along +y
        for k in range(len(pts)):
            (x0, y0), (x1, y1) = to_px(pts[k]), to_px(pts[(k + 1) % len(pts)])
            stamp(draw, x0 - 1, y0 - 1, x1 - 1, y1 - 1, palette[0], 5)
            stamp(draw, x0, y0, x1, y1, palette[3] if k < 4 else palette[1], 3)
        add(name, 'part', [link], [cx, cy], palette)
    for fx_id in FX:
        clip = manifest['fx'][fx_id]
        images = [load(frame) for frame in clip['frames']]
        fade = [clip['fadeAfter'], clip['fadeDuration']] if 'fadeAfter' in clip else None
        add('fx_' + fx_id, 'fx', images, clip.get('pivot') or [images[0].size[0] // 2, images[0].size[1]],
            clip.get('palette'), clip.get('fps', 20), fade)

    # Collapse pieces: the same polygons app.mjs prepareCollapse() cuts at runtime.
    body = Image.new('RGBA', (384, 416))
    for part_id in ['body_merged', 'dial', 'hand_minute', 'hand_hour', 'hand_hub']:
        body.alpha_composite(parts[part_id])
    seam = [(0, 184), (112, 184), (124, 190), (146, 186), (160, 193), (194, 189), (214, 195), (240, 186), (268, 190), (384, 190)]
    lower = [(0, 328), (144, 333), (164, 325), (191, 334), (213, 325), (235, 332), (250, 324), (384, 324)]
    full = [(0, 0), (384, 0), (384, 416), (0, 416)]
    cuts = [('collapse_roof', body, [(0, 0), (384, 0)] + seam[::-1], 'body', -20, -.4, .66),
            ('collapse_dial', body, seam + lower[::-1], 'body', 32, .25, .66),
            ('collapse_ink', body, lower + [(384, 416), (0, 416)], 'body', -12, -.1, .66),
            ('collapse_fist_left', parts['fist_left'], full, 'fist', -55, -.35, .57),
            ('collapse_fist_right', parts['fist_right'], full, 'fist', 55, .35, .57),
            ('collapse_chain_left', parts['chain_left'], full, 'chain', -30, .8, .7),
            ('collapse_chain_right', parts['chain_right'], full, 'chain', 30, .8, .7),
            ('collapse_chain_center', parts['chain_center'], full, 'chain', 30, .8, .7)]
    for piece_id, image, polygon, kind, vx, spin, delay in cuts:
        mask = Image.new('L', (384, 416), 0)
        ImageDraw.Draw(mask).polygon(polygon, fill=255)
        cut = Image.new('RGBA', (384, 416))
        cut.paste(image, (0, 0), mask)
        box = cut.getbbox()
        if not box:
            continue
        piece = cut.crop(box)
        add(piece_id, 'piece', [piece], [piece.size[0] / 2, piece.size[1] / 2], palette)
        output['pieces'].append(dict(id=piece_id, x=box[0], y=box[1], w=piece.size[0], h=piece.size[1],
                                     kind=kind, vx=vx, spin=spin, delay=delay))

    (DEST / 'preview.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Exported {len(output["clips"])} clips / {sum(len(c["frames"]) for c in output["clips"])} frames, '
          f'{len(output["pieces"])} collapse pieces.')


if __name__ == '__main__':
    main()
