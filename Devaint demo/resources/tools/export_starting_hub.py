"""Export reviewed hub sources at native game scale; never rewrite imported art."""
from pathlib import Path
import hashlib
import json
import math

from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[1]
VILLAGE = ROOT / 'environments/village'
UI = ROOT / 'ui/roguelite-hub'
FX = ROOT / 'effects/hub-unlock'
INTRO = ROOT / 'sequences/starting-village-intro'
BUILDINGS = ('palette-atelier', 'supply-office', 'reclaim-workshop')
PROPS = ('palette-bench', 'supply-counter', 'reclaim-bench', 'record-desk',
         'return-well', 'departure-gate', 'departure-table')
ICONS = ('palette_slots_4', 'palette_slots_5', 'palette_wildcard_1',
         'palette_wildcard_2', 'starting_gold_100', 'destroyed_ink_gold_20',
         'magic_pigment', 'palette_atelier', 'supply_office', 'reclaim_workshop',
         'record_farmhouse', 'departure_lane', 'locked', 'unlocked',
         'story_record', 'destroy_ink')


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


def save(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def native_sprite(source, size, fit):
    image = Image.open(source).convert('RGBA')
    # Some generated sources already have real alpha. Remove only their faint
    # outer halo; keep every original source unchanged for later retouching.
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            keyed = r > 180 and b > 180 and g < 100
            pixels[x, y] = (r, g, b, 255) if a >= 230 and not keyed else (0, 0, 0, 0)
    box = image.getbbox()
    if not box:
        raise ValueError('Source has no opaque subject: ' + str(source))
    image = image.crop(box)
    scale = min(fit[0] / image.width, fit[1] / image.height)
    image = image.resize((max(1, round(image.width * scale)),
                          max(1, round(image.height * scale))), Image.Resampling.NEAREST)
    canvas = Image.new('RGBA', size)
    canvas.alpha_composite(image, ((size[0] - image.width) // 2, size[1] - 8 - image.height))
    return canvas


def sketch(image):
    """Remove color using source marks; geometry/alpha stay bit-identical."""
    out = Image.new('RGBA', image.size)
    src, dst = image.load(), out.load()
    paper, graphite = (246, 240, 224), (66, 57, 46)
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = src[x, y]
            if not a:
                continue
            value = (r * .25 + g * .65 + b * .1) / 255
            strength = (1 - value) ** 2.1
            neighbors = [src[nx, ny] for nx, ny in
                         ((x-1, y), (x+1, y), (x, y-1), (x, y+1))
                         if 0 <= nx < image.width and 0 <= ny < image.height]
            outer = any(p[3] == 0 for p in neighbors)
            lighter = max((p[0]*.25+p[1]*.65+p[2]*.1 for p in neighbors if p[3]),
                          default=value*255)
            # Reinforce only existing contours and the dark side of source
            # joints. Large faces stay paper-light; no invented hatch pattern.
            if outer:
                strength = max(strength, .76)
            elif value < .74 and lighter - value*255 > 32:
                strength = max(strength, .67)
            dst[x, y] = (*[round(p - (p - k) * strength)
                           for p, k in zip(paper, graphite)], a)
    return out


def glyph(name):
    image = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(image)
    white = (255, 255, 255, 255)

    def line(points, width=2): d.line(points, fill=white, width=width)
    def rect(box, width=2): d.rectangle(box, outline=white, width=width)
    def drop(cx=16, cy=15):
        d.polygon([(cx, cy-10), (cx-3, cy-4), (cx-6, cy+1), (cx-6, cy+6),
                   (cx-3, cy+9), (cx+3, cy+9), (cx+6, cy+6),
                   (cx+6, cy+1), (cx+3, cy-4)], fill=white)

    if name.startswith('palette_slots_'):
        rect((3, 4, 28, 27))
        slots = [(7, 8), (19, 8), (7, 19), (19, 19)]
        if name.endswith('5'):
            slots = [(6, 8), (14, 8), (22, 8), (10, 19), (18, 19)]
        for x, y in slots: rect((x-2, y-2, x+3, y+3), 1)
    elif name.startswith('palette_wildcard_'):
        for row in (7, 20):
            for x in (4, 14, 24): rect((x, row, x+5, row+5), 1)
        y = 9 if name.endswith('1') else 22
        line([(2, y), (11, y)])
        line([(7, y-5), (7, y+5)])
        line([(3, y-4), (11, y+4)], 1)
        line([(3, y+4), (11, y-4)], 1)
    elif name in ('starting_gold_100', 'supply_office'):
        d.polygon([(10, 6), (22, 6), (19, 12), (25, 19), (24, 26),
                   (8, 26), (7, 19), (13, 12)], fill=white)
        line([(9, 10), (23, 10)])
        d.rectangle((15, 16, 17, 22), fill=(0, 0, 0, 0))
    elif name in ('destroyed_ink_gold_20', 'reclaim_workshop'):
        drop(13, 13)
        d.ellipse((18, 19, 29, 29), outline=white, width=2)
        line([(19, 18), (19, 24), (24, 24)], 1)
    elif name == 'magic_pigment':
        drop(16, 10)
        d.rectangle((9, 11, 12, 17), fill=(0, 0, 0, 0))
        line([(4, 23), (7, 28), (25, 28), (28, 23)])
        line([(5, 22), (27, 22)])
    elif name == 'palette_atelier':
        rect((3, 9, 28, 25))
        for x in (7, 13, 19, 25): d.rectangle((x-1, 13, x+1, 16), fill=white)
        line([(5, 5), (10, 5), (10, 9)])
    elif name in ('record_farmhouse', 'story_record'):
        rect((5, 4, 26, 28));line([(10, 4), (10, 28)])
        for y in (10, 15, 20): line([(14, y), (22, y)], 1)
    elif name == 'departure_lane':
        line([(4, 28), (4, 4), (27, 4), (27, 28)])
        line([(9, 17), (22, 17), (18, 12)])
        line([(22, 17), (18, 22)])
    elif name == 'locked':
        rect((6, 13, 25, 28));d.arc((9, 3, 22, 23), 180, 360, fill=white, width=3)
        line([(16, 18), (16, 23)])
    elif name == 'unlocked':
        line([(5, 16), (12, 24), (27, 7)], 4)
    elif name == 'destroy_ink':
        drop(13, 11)
        line([(17, 17), (28, 28)], 3);line([(28, 17), (17, 28)], 3)
    return image


def export():
    records = []
    for name in BUILDINGS + PROPS:
        building = name in BUILDINGS
        size, fit = ((256, 256), (232, 232)) if building else ((128, 80), (112, 48))
        if name == 'return-well': size, fit = (128, 144), (112, 128)
        if name == 'departure-gate': size, fit = (192, 208), (176, 192)
        image = native_sprite(VILLAGE / 'source/starting-hub/generated' / (name + '.png'), size, fit)
        category = 'buildings' if building else 'props'
        for state, output in [('color', image), ('sketch', sketch(image))]:
            path = VILLAGE / 'runtime' / category / 'starting-hub' / state / (name + '.png')
            save(output, path)
            records.append({'id': name.replace('-', '_') + '.' + state,
                            'path': path.relative_to(ROOT).as_posix(), 'size': list(size),
                            'ppu': 32, 'pivot': [.5, 8 / size[1]],
                            'contactLineFromTop': size[1] - 8,
                            'state': state, 'alpha': 'binary',
                            'geometry': 'shared-with-paired-state'})
    for name in ICONS:
        image = glyph(name)
        path = UI / 'runtime/icons' / (name + '.png')
        save(image, path)
        records.append({'id': name, 'path': path.relative_to(ROOT).as_posix(),
                        'size': [32, 32], 'ppu': 32, 'pivot': [.5, .5],
                        'alpha': 'binary', 'tint': 'uGUI-color', 'source': 'native-pixel-glyph'})
    for kind in ('unlock', 'pigment-pickup'):
        frames = []
        for i in range(8):
            image = Image.new('RGBA', (64, 64));d = ImageDraw.Draw(image)
            if i < 7:
                radius = 4 + i * 3
                d.rectangle((32-radius, 32-radius//2, 32+radius, 32+radius//2),
                            outline=(237, 224, 181, 255), width=1)
                for j in range(6):
                    angle = math.tau * j / 6
                    x, y = round(32+math.cos(angle)*radius), round(32+math.sin(angle)*radius)
                    d.rectangle((x, y, x+1, y+1), fill=(74, 117, 104, 255))
                if kind == 'pigment-pickup' and i < 4:
                    small = glyph('magic_pigment').resize((16, 16), Image.Resampling.NEAREST)
                    image.alpha_composite(small, (24, 24-i))
            path = FX / 'runtime' / kind / f'{i+1:02}.png'
            save(image, path);frames.append(image)
            records.append({'id': f'{kind}.{i+1:02}', 'path': path.relative_to(ROOT).as_posix(),
                            'size': [64, 64], 'ppu': 32, 'pivot': [.5, .5],
                            'alpha': 'binary', 'frameDurationMs': 80})
        sheet = Image.new('RGBA', (512, 64))
        for i, frame in enumerate(frames): sheet.alpha_composite(frame, (i*64, 0))
        save(sheet, FX / 'preview' / (kind + '-sheet.png'))
    for name in ('record-hall', 'erased-farm', 'awakening-courtyard'):
        image = Image.open(INTRO / 'source/generated' / (name + '.png')).convert('RGB')
        image = ImageOps.fit(image, (960, 540), Image.Resampling.NEAREST)
        path = INTRO / 'runtime' / (name + '.png');save(image, path)
        records.append({'id': name, 'path': path.relative_to(ROOT).as_posix(),
                        'size': [960, 540], 'kind': 'static-story-backdrop',
                        'charactersBakedIn': False})
    write_json(UI / 'source/sprite-layout.json', {
        'format': 'deviant-starting-hub-art-v1', 'status': 'review-output-not-unity-integrated',
        'approval': 'pending-user-art-review', 'sprites': records,
        'reuse': [
            {'id': 'farmhouse', 'path': 'environments/village/runtime/buildings/reference-map-phase2/color/house_balcony.png'},
            {'id': 'farmhouse-sketch', 'path': 'environments/village/runtime/buildings/reference-map-phase2/sketch/house_balcony.png'},
            {'id': 'terrain-and-vegetation', 'path': 'environments/village/asset.json'},
            {'id': 'player-and-weapon-motions', 'path': 'characters/ink/common/asset.json'},
            {'id': 'portal-sequence', 'path': 'sequences/portal/asset.json'}],
        'import': {'filter': 'Point', 'compression': 'None', 'mipmaps': False,
                   'spriteMesh': 'FullRect', 'preserveTransparentCanvas': True},
        'notes': ['No new NPC portraits or locomotion are required for unattended workstations.',
                  'Existing main Galmuri and PaperGuiSkin frames are reused; no baked UI text.',
                  'Story backdrops support static narration; they do not replace the full missing StoryReference production package.']})
    return records


if __name__ == '__main__':
    result = export()
    print('Exported', len(result), 'native sprites/frames and story backdrops.')
