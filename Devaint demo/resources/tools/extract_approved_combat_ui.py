"""Extract the two approved flattened mockups without generative redrawing.

The source pixels are preserved in opaque foreground layers. Hidden backing
pixels are explicitly marked as restored and never presented as original art.
"""
from pathlib import Path
import argparse
import hashlib
import io
import json
import shutil
import zipfile
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'ui/combat-hud-approved-20261106'
W, H = 1670, 942
RECORDS = []
COMPOSITES = []
SOURCE_IMAGES = {}
LAYER_IMAGES = {}
PROOFS = []


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rect_mask(size, box):
    out = np.zeros((size[1], size[0]), bool)
    x0, y0, x1, y1 = box
    out[max(y0, 0):min(y1, size[1]), max(x0, 0):min(x1, size[0])] = True
    return out


def polygon_mask(size, points):
    out = Image.new('1', size)
    ImageDraw.Draw(out).polygon(points, fill=1)
    return np.asarray(out, bool)


def circular_mask(size, cx, cy, radius):
    yy, xx = np.ogrid[:size[1], :size[0]]
    return (xx-cx)**2 + (yy-cy)**2 <= radius**2


def silhouette(rgb, threshold=125):
    dark = np.max(rgb, axis=2) < threshold
    labels, n = ndi.label(dark)
    counts = np.bincount(labels.ravel())
    keep = counts >= 4
    keep[0] = False
    dark = keep[labels]
    # Preserve enclosed ivory regions and one original antialiased edge pixel.
    return ndi.binary_dilation(ndi.binary_fill_holes(dark), iterations=1)


def largest(mask):
    labels, _ = ndi.label(mask)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    return labels == int(sizes.argmax())


def anchor_for(group):
    if group.startswith('run_'):
        return 'center'
    if group in ('sword', 'hammer'):
        return 'bottom-left'
    if group == 'health':
        return 'top-left'
    return 'top-right'


def export_layer(name, group, rgb, mask, origin, kind, source, note='', folder='layers'):
    yy, xx = np.where(mask)
    if len(xx) == 0:
        raise ValueError(f'Empty asset: {name}')
    box = (int(xx.min()), int(yy.min()), int(xx.max()+1), int(yy.max()+1))
    x0, y0, x1, y1 = box
    rgba = np.zeros((y1-y0, x1-x0, 4), np.uint8)
    rgba[:, :, :3] = rgb[y0:y1, x0:x1]
    rgba[:, :, 3] = mask[y0:y1, x0:x1] * 255
    rgba[rgba[:, :, 3] == 0, :3] = 0
    im = Image.fromarray(rgba)
    rel = f'runtime/{folder}/{name}.png'
    dest = GROUP / rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest)
    gx, gy = origin[0]+x0, origin[1]+y0
    anchor = anchor_for(group)
    # RectTransform pivot (0,1), same unit scale as the 1670 x 942 reference.
    pos = {'top-left': [gx, -gy], 'top-right': [gx-W, -gy],
           'bottom-left': [gx, H-gy], 'center': [gx-W/2, H/2-gy]}[anchor]
    record = dict(id=name, group=group, file=rel, rect=[gx, gy, im.width, im.height],
                  source=source, fidelity=kind, anchor=anchor, unityPivot=[0, 1],
                  unityAnchoredPosition=pos, note=note)
    if folder == 'composites':
        COMPOSITES.append(record)
    else:
        record['z'] = len(RECORDS)
        RECORDS.append(record)
    LAYER_IMAGES[name] = im
    return record


class Cut:
    def __init__(self, name, source, box, custom_mask=None, only_largest=False):
        self.name, self.source, self.box = name, source, box
        self.rgb = np.asarray(SOURCE_IMAGES[source].crop(box).convert('RGB')).copy()
        self.size = (box[2]-box[0], box[3]-box[1])
        self.mask = silhouette(self.rgb) if custom_mask is None else custom_mask
        if only_largest:
            self.mask = largest(self.mask)
        self.fronts = []
        self.used = np.zeros(self.mask.shape, bool)
        self.no_backing = np.zeros(self.mask.shape, bool)
        self.base_sections = []
        export_layer(name+'_complete', name, self.rgb, self.mask, box[:2],
                     'source-pixels', source, 'Static approved sample, not a dynamic widget.', 'composites')

    def region(self, box):
        x, y = self.box[:2]
        return rect_mask(self.size, (box[0]-x, box[1]-y, box[2]-x, box[3]-y))

    def part(self, name, box=None, predicate=None, mask=None, expand=0, note='', only_largest=False, no_backing=False):
        selected = self.mask.copy()
        if box:
            selected &= self.region(box)
        if mask is not None:
            selected &= mask
        if predicate:
            selected &= predicate(self.rgb)
        if only_largest:
            selected = largest(selected)
        if expand:
            selected = ndi.binary_dilation(selected, iterations=expand) & self.mask
            if box:
                selected &= self.region(box)
        selected &= ~self.used
        self.used |= selected
        if no_backing:
            self.no_backing |= selected
        self.fronts.append((name, selected, note))
        return selected

    def base_section(self, name, mask):
        self.base_sections.append((name, mask))

    def finish(self, backing_name=None):
        # Nearest visible material + harmonic fill is only used under removed
        # foreground. Every pixel of the approved foreground is overlaid again.
        # This preserves the selected sample exactly while giving editable bases.
        base = self.rgb.copy().astype(np.float64)
        repair = self.used & ~self.no_backing
        if repair.any():
            # Sample beyond the source glyph fringe, otherwise residual antialias
            # pixels would become a dark/light ghost on the clean backing.
            avoid = ndi.binary_dilation(self.used, iterations=5) | ~self.mask
            nearest = ndi.distance_transform_edt(avoid, return_distances=False, return_indices=True)
            base[repair] = base[nearest[0][repair], nearest[1][repair]]
            # Smooth hidden material; never alter any visible source pixel.
            for _ in range(140):
                avg = ndi.uniform_filter(base, size=(3, 3, 1), mode='nearest')
                base[repair] = avg[repair]
        base = np.clip(np.rint(base), 0, 255).astype(np.uint8)
        start = len(RECORDS)
        base_mask = self.mask & ~self.no_backing
        sections = self.base_sections or [(backing_name or self.name+'_base', base_mask)]
        remaining = base_mask.copy()
        for name, m in sections:
            selected = remaining & m
            remaining &= ~selected
            export_layer(name, self.name, base, selected,
                         self.box[:2], 'source-visible+restored-underlays' if (repair & selected).any() else 'source-pixels',
                         self.source, 'Only hidden pixels beneath movable foreground were restored.' if repair.any() else '')
        if remaining.any():
            export_layer(self.name+'_joining_details', self.name, base, remaining,
                         self.box[:2], 'source-pixels', self.source,
                         'Visible connecting contour / overlaps; place beneath independent foreground parts.')
        for name, mask, note in self.fronts:
            export_layer(name, self.name, self.rgb, mask, self.box[:2], 'source-pixels', self.source, note)
        # Save exact transparent current-state assembly and verify every RGB/A byte.
        rebuilt = Image.new('RGBA', self.size)
        for r in RECORDS[start:]:
            x, y = r['rect'][:2]
            rebuilt.alpha_composite(LAYER_IMAGES[r['id']], (x-self.box[0], y-self.box[1]))
        target = np.dstack([self.rgb, self.mask.astype(np.uint8)*255])
        target[~self.mask, :3] = 0
        delta = np.abs(np.asarray(rebuilt).astype(int)-target.astype(int))
        assert delta.max() == 0, (self.name, delta.max())
        PROOFS.append(dict(group=self.name, differentPixels=int(np.any(delta != 0, axis=2).sum()),
                           maxChannelDifference=int(delta.max()), coveredPixels=int(self.mask.sum())))


def bright(rgb):
    return np.min(rgb, axis=2) > 125


def dark(rgb):
    return np.max(rgb, axis=2) < 140


def teal(rgb):
    r, g, b = rgb.transpose(2, 0, 1).astype(float)
    return (g > 100) & (g > r*1.25) & (b > r*1.2)


def red(rgb):
    r, g, b = rgb.transpose(2, 0, 1).astype(float)
    return (r > 150) & (r > g*1.35) & (r > b*1.35)


def enclosed_in(cut, box):
    region = cut.region(box)
    # Only fill holes within this isolated local cut so the tray/neighbor cannot
    # flood through a bounding edge into a different component.
    x0, y0, x1, y1 = box
    local = cut.rgb[y0-cut.box[1]:y1-cut.box[1], x0-cut.box[0]:x1-cut.box[0]]
    out = np.zeros(cut.mask.shape, bool)
    out[y0-cut.box[1]:y1-cut.box[1], x0-cut.box[0]:x1-cut.box[0]] = silhouette(local)
    return out & region


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--battle', type=Path, required=True)
    ap.add_argument('--palette', type=Path, required=True)
    ap.add_argument('--pigment', type=Path)
    args = ap.parse_args()
    GROUP.mkdir(parents=True, exist_ok=True)
    (GROUP/'source').mkdir(exist_ok=True)
    (GROUP/'preview').mkdir(exist_ok=True)
    provenance = []
    for key, src in [('battle', args.battle), ('palette', args.palette)]:
        dest = GROUP/'source'/f'approved-{key}.png'
        if src.resolve() != dest.resolve():
            shutil.copy2(src, dest)
        assert sha(src) == sha(dest)
        SOURCE_IMAGES[key] = Image.open(dest).convert('RGB')
        provenance.append(dict(id=key, originalFileName=src.name, file=f'source/{dest.name}',
                               sha256=sha(dest), size=list(SOURCE_IMAGES[key].size),
                               selectedFor='battle HUD' if key == 'battle' else 'run-start palette only'))
    if args.pigment:
        shutil.copy2(args.pigment, GROUP/'source/pigment-existing-game.png')
        provenance.append(dict(id='existing-game-pigment', originalFileName=args.pigment.name,
                               file='source/pigment-existing-game.png', sha256=sha(args.pigment),
                               selectedFor='Preserved existing game icon; screenshot extraction is used in exact assembly'))
    write_json(GROUP/'source/provenance.json', provenance)

    c = Cut('health', 'battle', (28, 28, 524, 149))
    # Portrait is kept with its overlap into the health plaque.
    portrait_points = [(29,29),(146,29),(158,55),(165,66),(170,79),(174,91),
                       (171,105),(165,117),(157,125),(154,134),(149,143),(142,148),(29,148)]
    portrait_mask = polygon_mask(c.size, [(x-c.box[0],y-c.box[1]) for x,y in portrait_points])
    c.part('health_portrait', mask=portrait_mask, no_backing=True,
           note='Original visible portrait; plaque underneath the portrait is not present in the flattened source.')
    c.part('health_value_82_100', (185, 63, 322, 103), bright, expand=1,
           note='Approved raster sample text; dynamic numbers require a matched runtime font.')
    c.part('health_fill_82', (164, 105, 456, 133), red, expand=1,
           note='Visible 82% sample only, not a full-width 100% fill.')
    c.finish('health_shell')

    for name, box in [('gold_icon', (1253, 26, 1304, 79)), ('gold_value_240', (1305, 32, 1367, 74)),
                      ('pigment_icon', (1386, 24, 1439, 79)), ('pigment_value_18', (1439, 32, 1482, 74))]:
        Cut(name, 'battle', box).finish(name)
    for name, box, inner in [('inventory', (1507, 21, 1572, 85), (1523, 35, 1555, 69)),
                             ('pause', (1579, 21, 1647, 85), (1597, 36, 1626, 69))]:
        c = Cut(name, 'battle', box, only_largest=True)
        c.part(name+'_glyph', inner, dark, expand=1)
        c.finish(name+'_button_frame')
    c = Cut('minimap', 'battle', (1249, 87, 1648, 235))
    c.part('minimap_player_marker', (1349, 159, 1375, 184), teal, expand=1)
    c.part('minimap_route_sample', (1266, 120, 1465, 188), bright, expand=1,
           note='Sample route; real map geometry must be drawn from game data.')
    c.finish('minimap_frame_and_backing')

    for name, box, tray_box, drops, keys, icons, ring in [
        ('sword', (49, 778, 351, 932), (125, 878, 297, 931),
         [(153, 885, 181, 920), (194, 885, 223, 920), (236, 885, 265, 920)],
         [('q', (202, 789, 249, 828)), ('e', (282, 789, 329, 828))],
         [('weapon', (81, 809, 158, 886), bright), ('basic', (202, 829, 252, 879), bright),
          ('skill', (279, 827, 336, 883), teal)], (52, 780, 192, 922)),
        ('hammer', (402, 778, 726, 932), (482, 878, 653, 931),
         [(510, 885, 538, 920), (551, 885, 579, 920), (592, 885, 621, 920)],
         [('w', (560, 789, 607, 828)), ('r', (648, 789, 695, 828))],
         [('weapon', (435, 807, 511, 884), bright), ('basic', (556, 830, 610, 880), bright),
          ('cooldown_3_2', (650, 838, 702, 869), bright)], (406, 780, 547, 922)),
    ]:
        c = Cut(name, 'battle', box, only_largest=True)
        for i, drop in enumerate(drops):
            c.part(f'{name}_palette_drop_{i+1}', mask=enclosed_in(c, drop))
        # The tray is flat artwork with the individual drops removed. Its visible
        # overlap with the large circle is retained by the base layer.
        for key, keybox in keys:
            tighter = (keybox[0], keybox[1], keybox[2], 825)
            c.part(f'keycap_{key}', mask=largest(enclosed_in(c, tighter)), no_backing=True)
        for kind, iconbox, predicate in icons:
            if kind == 'skill':
                predicate = lambda rgb: teal(rgb) | bright(rgb)
            c.part(f'{name}_{kind}_icon' if kind != 'cooldown_3_2' else 'hammer_cooldown_3_2',
                   iconbox, predicate, expand=1, only_largest=kind != 'cooldown_3_2')
        if name == 'sword':
            c.part('sword_active_ring', ring, teal, expand=1)
        else:
            xx, yy = 475-box[0], 850-box[1]
            annulus = circular_mask(c.size, xx, yy, 71) & ~circular_mask(c.size, xx, yy, 57)
            c.part('hammer_inactive_ring', mask=annulus & ~c.region(tray_box), predicate=bright, expand=1)
            cx, cy = 675-box[0], 855-box[1]
            annulus = circular_mask(c.size, cx, cy, 47) & ~circular_mask(c.size, cx, cy, 34)
            c.part('hammer_skill_cooldown_ring', mask=annulus & ~c.region(tray_box), predicate=bright, expand=1,
                   note='Approved visible radial sample. Use UI radial fill for changing cooldown.')
        # Split the three discs and palette tray into independently placeable
        # backplates. Their occluded portions remain occluded, never invented.
        tx0,ty0,tx1,ty1 = tray_box
        tray = polygon_mask(c.size, [(x-box[0],y-box[1]) for x,y in [
            (tx0+23,ty0),(tx1-23,ty0),(tx1-9,ty0+4),(tx1-2,ty0+14),
            (tx1,ty0+28),(tx1-5,ty1-11),(tx1-18,ty1-3),(tx0+19,ty1-3),
            (tx0+5,ty1-10),(tx0,ty0+27),(tx0+4,ty0+12),(tx0+12,ty0+4)]])
        c.base_section(name+'_palette_tray', tray)
        cx = 122 if name == 'sword' else 475
        basic_x = 225 if name == 'sword' else 585
        special_x = 307 if name == 'sword' else 676
        c.base_section(name+'_weapon_disc', circular_mask(c.size, cx-box[0], 850-box[1], 72))
        c.base_section(name+'_basic_disc', circular_mask(c.size, basic_x-box[0], 855-box[1], 44))
        c.base_section(name+'_special_disc', circular_mask(c.size, special_x-box[0], 855-box[1], 47))
        c.finish()

    c = Cut('run_heading', 'palette', (687, 247, 982, 318), only_largest=True)
    c.part('run_heading_label', (746, 260, 924, 301), bright, expand=1)
    c.part('run_heading_chevrons', (709, 264, 957, 295), teal, expand=1)
    c.finish('run_heading_plate')
    for name, box, labelbox, dividerbox, badgebox, iconbox, drops in [
        ('run_sword', (512, 289, 831, 536), (623, 380, 731, 420), (554, 422, 792, 432),
         (607, 290, 700, 380), (630, 307, 681, 364),
         [(576, 437, 634, 507), (646, 437, 704, 507), (716, 437, 775, 507)]),
        ('run_hammer', (837, 289, 1158, 536), (932, 380, 1070, 420), (880, 422, 1127, 432),
         (967, 290, 1060, 380), (987, 307, 1039, 364),
         [(895, 437, 953, 507), (966, 437, 1025, 507), (1039, 437, 1100, 507)]),
    ]:
        c = Cut(name, 'palette', box, only_largest=True)
        c.part(name+'_name', labelbox, dark, expand=1)
        c.part(name+'_divider', dividerbox, dark, expand=1)
        c.part(name+'_weapon_icon', iconbox, bright, expand=1, only_largest=True)
        for i, drop in enumerate(drops):
            c.part(f'{name}_drop_{i+1}', mask=enclosed_in(c, drop))
        c.part(name+'_accent', (box[0]+5, 485, box[2]-5, 527), teal, expand=1)
        c.finish(name+'_card_and_badge')

    layout = dict(format='deviant-approved-ui-layout-v1', referenceResolution=[W, H],
                  coordinateSystem='top-left pixels; no source resizing',
                  sourceSizes={k:list(v.size) for k,v in SOURCE_IMAGES.items()},
                  unity=dict(canvasScaler='ScaleWithScreenSize', referenceResolution=[W, H],
                             screenMatchMode='MatchWidthOrHeight', matchWidthOrHeight=0.5,
                             textureType='Sprite', spriteMode='Single', pixelsPerUnit=100,
                             alphaIsTransparency=True, mipmaps=False, filterMode='Bilinear',
                             compression='None', meshType='FullRect', maxTextureSize=2048,
                             pixelPerfect=True, preserveAspect=True),
                  compositionOrder='layers in array order; composites are alternatives, not additional layers',
                  runReveal=dict(durationSeconds=3.5, inputRequired=False, placement='center'),
                  layers=RECORDS, composites=COMPOSITES,
                  limitations=[
                      'These are extracted flattened raster layers, not the original vector/PSD sources.',
                      '1 px antialias fringe retains original background RGB. Inspect on contrasting backgrounds.',
                      'Only pixels hidden beneath extracted overlays are restored. No claim of recovered hidden art.',
                      'HP 82/100, gold 240, pigment 18 and cooldown 3.2 are raster sample labels, not a complete font.',
                      'Full 100% HP fill, full radial cooldown ring and occluded circle sections are not present in source.',
                      'Source images differ by 1 pixel in each dimension; native popup pixels are not rescaled.',
                      'Reference reconstruction is an art preview, not an actual Unity runtime screenshot.'
                  ])
    write_json(GROUP/'runtime/layout.json', layout)
    build_previews(layout)
    write_json(GROUP/'source/extraction-contract.json', dict(
        method='deterministic source-pixel crop, silhouette segmentation and foreground partition',
        newImageGeneration=False, sourceOriginalBytesPreserved=True,
        layerAlpha='binary matte preserving source fringe', reconstructionProof=PROOFS,
        acceptanceScope='Each approved component reconstructed from its runtime layers at native pixels.'))
    write_readme()
    register_files()
    print(json.dumps(dict(folder=str(GROUP), layers=len(RECORDS), composites=len(COMPOSITES),
                          checks=PROOFS), ensure_ascii=False))


def checker(size, square=16):
    y, x = np.indices((size[1], size[0]))
    v = np.where(((x//square+y//square)%2)[:, :, None], 76, 88).astype(np.uint8)
    return Image.fromarray(np.repeat(v, 3, axis=2)).convert('RGBA')


def assemble(records, size=(W, H)):
    out = Image.new('RGBA', size)
    for r in records:
        out.alpha_composite(LAYER_IMAGES[r['id']], tuple(r['rect'][:2]))
    return out


def build_previews(layout):
    all_ui = assemble(RECORDS)
    battle_ui = assemble([r for r in RECORDS if not r['group'].startswith('run_')])
    popup_ui = assemble([r for r in RECORDS if r['group'].startswith('run_')])
    for name, im in [('all-ui-transparent', all_ui), ('battle-ui-transparent', battle_ui), ('palette-ui-transparent', popup_ui)]:
        im.save(GROUP/f'runtime/{name}.png')
    check = checker((W, H))
    check.alpha_composite(all_ui)
    check.save(GROUP/'preview/transparent-assembly.png')
    # Presentation-only stage: source1 + native center crop from source2.
    # This is explicitly a combined reference, not an invented clean backdrop.
    reference = SOURCE_IMAGES['battle'].copy()
    reference.paste(SOURCE_IMAGES['palette'].crop((500, 239, 1170, 562)), (500, 239))
    reference.save(GROUP/'preview/selected-combination-reference.png')
    # Compare source foreground to runtime layers, using reference around them.
    proof = reference.convert('RGBA')
    proof.alpha_composite(all_ui)
    arr = np.asarray(proof.convert('RGB')).astype(int)-np.asarray(reference).astype(int)
    assert not np.any(arr), f'Reconstruction changed {np.any(arr,axis=2).sum()} pixels'
    proof.save(GROUP/'preview/reassembled-approved-screen.png')
    # Flattened source lacks the stage hidden behind UI. Make that absence
    # transparent in the editable backdrop instead of fabricating a clean plate.
    bg = reference.convert('RGBA')
    alpha = np.asarray(all_ui)[:, :, 3]
    bgarr = np.array(bg)
    bgarr[alpha > 0] = 0
    Image.fromarray(bgarr).save(GROUP/'preview/reference-background-with-ui-holes.png')
    write_ora(RECORDS, reference)
    draw_contact_sheet()
    write_preview_html(layout)


def write_ora(records, reference):
    root = ET.Element('image', {'w':str(W), 'h':str(H), 'name':'Approved battle + run palette'})
    stack = ET.SubElement(root, 'stack')
    dest = GROUP/'source/approved-ui-layers.ora'
    with zipfile.ZipFile(dest, 'w') as z:
        z.writestr('mimetype', 'image/openraster', compress_type=zipfile.ZIP_STORED)
        for i, r in reversed(list(enumerate(records))):
            src = f'data/layer{i:03}.png'
            ET.SubElement(stack, 'layer', {'name':r['id'], 'src':src, 'x':str(r['rect'][0]),
                                          'y':str(r['rect'][1]), 'opacity':'1.0', 'visibility':'visible',
                                          'composite-op':'svg:src-over'})
            z.write(GROUP/r['file'], src)
        # Background is hidden by default; foreground remains truly transparent.
        ET.SubElement(stack, 'layer', {'name':'REFERENCE ONLY (hidden)', 'src':'data/reference.png',
                                      'x':'0', 'y':'0', 'opacity':'1.0', 'visibility':'hidden',
                                      'composite-op':'svg:src-over'})
        buff = io.BytesIO(); reference.save(buff, format='PNG'); z.writestr('data/reference.png', buff.getvalue())
        z.writestr('stack.xml', ET.tostring(root, encoding='utf-8', xml_declaration=True))
        buff = io.BytesIO(); assemble(records).save(buff, format='PNG'); z.writestr('mergedimage.png', buff.getvalue())
        thumb = assemble(records); thumb.thumbnail((256, 256))
        buff = io.BytesIO(); thumb.save(buff, format='PNG'); z.writestr('Thumbnails/thumbnail.png', buff.getvalue())


def font(size, bold=False):
    path = Path('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf')
    return ImageFont.truetype(str(path), size) if path.exists() else ImageFont.load_default(size=size)


def draw_contact_sheet():
    cw, ch, cols = 430, 225, 4
    rows = (len(RECORDS)+cols-1)//cols
    sheet = Image.new('RGB', (cw*cols, 115+rows*ch), '#ede8df')
    d = ImageDraw.Draw(sheet)
    d.text((28, 20), 'DEVIANT / 채택 UI 분리 에셋', font=font(34, True), fill='#222622')
    d.text((30, 69), '전투 HUD: 첫 번째 원본  ·  출발 팔레트: 두 번째 원본  /  원본 픽셀 추출', font=font(19), fill='#424642')
    for i, r in enumerate(RECORDS):
        x, y = (i%cols)*cw, 115+(i//cols)*ch
        panel = checker((cw-18, ch-59), 12)
        im = LAYER_IMAGES[r['id']].copy()
        ratio = min((panel.width-24)/im.width, (panel.height-20)/im.height, 2)
        im = im.resize((int(im.width*ratio), int(im.height*ratio)), Image.Resampling.NEAREST)
        panel.alpha_composite(im, ((panel.width-im.width)//2, (panel.height-im.height)//2))
        sheet.paste(panel.convert('RGB'), (x+9, y))
        d.text((x+14, y+ch-54), r['id'], font=font(15, True), fill='#222622')
        info = f"{r['rect'][2]} × {r['rect'][3]} px"
        if 'restored' in r['fidelity']:
            info += ' · 가려진 바탕 복원 포함'
        d.text((x+14, y+ch-30), info, font=font(13), fill='#555b54')
    sheet.save(GROUP/'preview/asset-contact-sheet.png')


def write_preview_html(layout):
    # Embedded JSON contains only paths/coordinates; no base64 image payload.
    data = json.dumps(layout, ensure_ascii=False).replace('</', '<\\/')
    html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>DEVIANT · 채택 UI 에셋</title><style>
*{box-sizing:border-box}body{margin:0;background:#181d1c;color:#f5f0e5;font:15px "Malgun Gothic",sans-serif}main{max-width:1726px;margin:auto;padding:28px}h1{font-size:26px;margin:0 0 10px}p{color:#b9c4bd;line-height:1.7}button,a{font:inherit}nav{display:flex;flex-wrap:wrap;gap:8px;margin:20px 0}button{border:1px solid #60756a;background:#26352e;color:#f5f0e5;padding:10px 17px;cursor:pointer;border-radius:7px}button[aria-pressed=true]{background:#89e1ce;color:#11271f}#stage{position:relative;aspect-ratio:1670/942;background:repeating-conic-gradient(#4c4c4c 0% 25%,#585858 0% 50%) 0/32px 32px;border:1px solid #58665d}#stage.light{background:#f3eee1}#stage.dark{background:#111}#stage img{position:absolute;pointer-events:none}#back{inset:0;width:100%;height:100%}#stage.hide-run .run{display:none}.meta{font-size:13px}a{color:#8fe3d0}#grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:14px;margin-top:28px}.asset{background:#25312b;border:1px solid #4a5b50;border-radius:8px;overflow:hidden}.thumb{height:160px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#4c4c4c 0% 25%,#585858 0% 50%) 0/24px 24px}.thumb img{max-width:95%;max-height:145px}.body{padding:12px;word-break:break-all}.body small{display:block;color:#c1cec5;margin-top:7px}code{font-size:13px}</style>
<main><h1>채택 UI · 원본 픽셀 분리본</h1><p>전투 HUD는 첫 번째 이미지, 출발 팔레트는 두 번째 이미지에서 추출했습니다. 원본 크기 1670 × 942 기준입니다.</p>
<nav><button data-bg="reference" aria-pressed="true">선택 조합 확인</button><button data-bg="checker">투명 배경</button><button data-bg="light">밝은 배경</button><button data-bg="dark">어두운 배경</button><button id="popup" aria-pressed="true">출발 팔레트 표시</button></nav>
<div id="stage"><img id="back" src="reference-background-with-ui-holes.png"></div>
<p class="meta">원본 위 재조립 시 픽셀 차이 0. 원본에 없는 숨은 그림은 복구할 수 없으므로 UI를 끄면 참조 배경의 해당 부분은 투명하게 남습니다.<br>체력·재화·쿨타임 숫자는 현재 시안의 표본 이미지입니다. 가변 숫자 폰트, 100% 체력 채움과 완전한 쿨타임 원은 게임 연결 시 보완해야 합니다. 테두리 1px에는 원래 배경의 안티앨리어싱이 포함됩니다.</p>
<p><a href="../runtime/layout.json">좌표·레이어·Unity 가져오기 설정</a> · <a href="../source/approved-ui-layers.ora">레이어 원본 (.ora)</a> · <a href="asset-contact-sheet.png">전체 에셋 시트</a> · <a href="../README.md">사용 안내</a></p><div id="grid"></div></main>
<script>const data=__DATA__;const stage=document.getElementById('stage');const grid=document.getElementById('grid');
for(const a of data.layers){let im=new Image();im.src='../'+a.file;im.alt=a.id;let[x,y,w,h]=a.rect;Object.assign(im.style,{left:x/1670*100+'%',top:y/942*100+'%',width:w/1670*100+'%',height:h/942*100+'%'});if(a.group.startsWith('run_'))im.classList.add('run');stage.append(im);const card=document.createElement('div');card.className='asset';const thumb=document.createElement('a');thumb.className='thumb';thumb.href=im.src;const t=new Image();t.src=im.src;t.alt=a.id;thumb.append(t);const b=document.createElement('div');b.className='body';const label=document.createElement('code');label.textContent=a.id;b.append(label);const small=document.createElement('small');small.textContent=w+' × '+h+' px · '+(a.fidelity.includes('restored')?'가려진 바탕 복원 포함':'원본 픽셀');b.append(small);card.append(thumb,b);grid.append(card)}
document.querySelectorAll('[data-bg]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-bg]').forEach(x=>x.setAttribute('aria-pressed',x===b));stage.classList.remove('light','dark');if(['light','dark'].includes(b.dataset.bg))stage.classList.add(b.dataset.bg);document.getElementById('back').hidden=b.dataset.bg!=='reference'});
document.getElementById('popup').onclick=function(){const hidden=stage.classList.toggle('hide-run');this.setAttribute('aria-pressed',!hidden)};
</script></html>'''
    (GROUP/'preview/index.html').write_text(html.replace('__DATA__', data), encoding='utf-8')


def write_readme():
    text = '''# 채택 전투 HUD + 출발 팔레트

첫 번째 채택 이미지의 전투 HUD와 두 번째 채택 이미지의 출발 팔레트를 원본 픽셀로 분리한 에셋입니다. 새로운 그림 생성이나 원본 리사이즈 없이 추출했습니다. 디자인 선택은 완료되었으며, 이 분리본의 게임 통합과 새 상태의 미술 검수는 별도입니다.

- `preview/index.html`: 재조립 화면, 투명·밝은·어두운 배경, 개별 PNG 링크.
- `preview/reassembled-approved-screen.png`: 두 채택 디자인을 합친 참조 화면. 실제 Unity 실행 화면은 아닙니다.
- `runtime/layers/`: 조립에 사용하는 투명 PNG. `runtime/layout.json`의 배열 순서로 배치합니다.
- `runtime/composites/`: 시안의 완성 상태를 그대로 담은 대체 묶음. layers와 동시에 겹쳐 쓰지 않습니다.
- `runtime/battle-ui-transparent.png`, `runtime/palette-ui-transparent.png`: 배경 없는 전체 위치 확인본.
- `source/approved-ui-layers.ora`: Krita/GIMP에서 열 수 있는 위치 보존 레이어 문서. 참조 배경은 기본 숨김입니다.
- `source/approved-battle.png`, `source/approved-palette.png`: 원본 바이트 보존본.
- `source/pigment-existing-game.png`: 기존 게임 안료 아이콘 원본. 정확한 시안 재조립에는 시안에서 추출한 `pigment_icon.png`를 사용합니다.

## 조립 규격

기준 캔버스는 1670 × 942입니다. 두 번째 원본은 1671 × 941이므로 팔레트 부분만 원래 좌표·크기로 옮깁니다. 늘이거나 정규화해 재샘플링하지 않습니다. 각 PNG의 `rect`는 왼쪽 위 기준 `[x,y,width,height]`입니다. UI를 화면 크기에 맞춰 통째로 비례 확대할 수 있지만, 원본 해상도보다 크게 표시하면 새 디테일이 생기는 것은 아닙니다.

Unity에서는 Sprite (2D and UI), Single, Full Rect, Alpha Is Transparency, 압축 없음, Mipmap 끔, Bilinear를 사용합니다. Canvas Scaler는 Scale With Screen Size, reference 1670 × 942입니다. 각 RectTransform은 pivot (0,1), `anchor`와 `unityAnchoredPosition`을 사용합니다. 선택 시안처럼 체력은 왼쪽 위, 무기·스킬은 왼쪽 아래, 재화·가방·일시정지는 오른쪽 위, 지도는 그 아래입니다. 출발 팔레트는 중앙입니다. 비율이 다른 해상도에서는 게임 카메라의 실제 safe area와 함께 별도로 확인해야 합니다.

## 정확히 보존된 범위와 보완할 범위

모든 원본 노출 픽셀과 현재 샘플 상태는 보존했습니다. 각 부품의 재조립과 선택 조합 화면을 비교하면 픽셀 차이가 0입니다. 외곽 1px에는 원래 배경과 섞인 안티앨리어싱 픽셀이 있어 다른 배경에서 약한 색 테두리가 나타날 수 있습니다. `source/extraction-contract.json`의 수치는 이 원본 크기에서의 픽셀 비교입니다.

바탕에서 숫자·아이콘·물방울을 분리한 뒤 그 아래 숨은 영역만 인접 색으로 보간했습니다. 이 부분은 원본 복원이 아니라 조립용 바탕 복원이며 `fidelity: source-visible+restored-underlays`로 표시합니다. 바뀐 내용 뒤가 드러나는 동적 상태는 미술 검수가 필요합니다. 원본에 가려진 무기 원형의 아래쪽과 무기 초상 뒤 판은 완전한 독립 도형으로 복구하지 않았습니다. 시안의 겹침 순서를 유지하세요.

82/100, 240, 18, 3.2는 시안 그대로인 래스터 표본이며 가변 숫자 전체 폰트가 아닙니다. 82% HP 채움과 쿨타임 원도 시안에 보이는 부분만 존재합니다. 100% 채움 텍스처·완전한 radial fill·다른 숫자·hover/pressed/disabled 상태는 추가 제작 또는 게임 드로잉으로 연결해야 합니다. 지도 경로도 표본이며 실제 맵 데이터에서 그려야 합니다. 이 패키지에 없는 상태를 완료된 것으로 보지 않습니다.

## 재생성

리소스 저장소의 현재 작업 브랜치와 미커밋 변경을 확인한 뒤 실행합니다. 지정 출력 그룹만 갱신합니다.

```powershell
python tools/extract_approved_combat_ui.py --battle ui/combat-hud-approved-20261106/source/approved-battle.png --palette ui/combat-hud-approved-20261106/source/approved-palette.png
```

원본을 바꾸지 않으며 catalog.json과 generated-assets.json에 이 그룹의 파일·해시를 갱신합니다. Unity 프로젝트 코드·메타데이터·배포 계약에는 아직 연결하지 않았습니다.
'''
    (GROUP/'README.md').write_text(text, encoding='utf-8')


def register_files():
    files = sorted(p.relative_to(GROUP).as_posix() for p in GROUP.rglob('*') if p.is_file() and p.name != 'asset.json')
    write_json(GROUP/'asset.json', dict(format='deviant-resource-asset-v1', id='ui.combat-hud-approved-20261106',
               path='ui/combat-hud-approved-20261106', status='design-selected-extraction-review',
               approval='User selected battle from first image and run palette from second; derived extraction awaits visual review',
               gameIntegration='not-integrated', sources=['source/provenance.json'], files=files))
    cat_path = ROOT/'catalog.json'
    catalog = json.loads(cat_path.read_text(encoding='utf-8-sig'))
    catalog['assets'] = [a for a in catalog['assets'] if a['id'] != 'ui.combat-hud-approved-20261106']
    catalog['assets'].append(dict(id='ui.combat-hud-approved-20261106', path='ui/combat-hud-approved-20261106', fileCount=len(files)))
    write_json(cat_path, catalog)
    manifest_path = ROOT/'generated-assets.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    prefix = GROUP.relative_to(ROOT).as_posix()+'/'
    manifest['files'] = [r for r in manifest['files'] if not r['path'].startswith(prefix)]
    for name in files:
        p = GROUP/name
        manifest['files'].append(dict(path=p.relative_to(ROOT).as_posix(), bytes=p.stat().st_size, sha256=sha(p)))
    manifest['files'].sort(key=lambda x:x['path'])
    write_json(manifest_path, manifest)


if __name__ == '__main__':
    raise SystemExit('Rejected extraction approach. Use build_rebuilt_combat_ui.py for complete standalone assets.')
