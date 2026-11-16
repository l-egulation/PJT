"""Village/tutorial colour set: the hollow (chapter-2 style) colour ground, colour wood and decor, flat skies, and the
cloud strip and bird flap atlases in colour and sketch for the hub's paired pieces."""
from pathlib import Path
import numpy as np
from PIL import Image
import tilecolour as TC

RES = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112/resources/environments")
COL = RES / "stage1-background-color-20261112/runtime"
CREAM = RES / "stage1-background-cream-20261112/runtime"
OUT = COL / "village"

def save(im, rel):
    p = OUT / rel; p.parent.mkdir(parents=True, exist_ok=True); im.save(p, optimize=True)

res = TC.build('E')
save(res['terrain-atlas.png'], 'ground/terrain-atlas.png')
for n in ('left', 'middle-a', 'middle-b', 'right', 'single'): save(res[f'wood/{n}.png'], f'ground/wood/{n}.png')
save(res['decor/daisy.png'], 'ground/decor/daisy.png')
for i in range(3): save(res[f'decor/embedded-pebble-{i}.png'], f'ground/decor/embedded-pebble-{i}.png')

save(Image.new('RGB', (960, 540), (188, 214, 230)), 'sky/sky.png')
save(Image.new('RGB', (960, 540), (252, 247, 238)), 'sky/sky-sketch.png')

# one cloud strip, the same layout in both states (x, y top-left, kind)
LAYOUT = [(0, 70, 0), (300, 10, 3), (560, 100, 1), (840, 30, 4), (1100, 90, 2), (1360, 20, 5)]
W, H = 1600, 170
for state, base in (('', COL / 'sky/clouds'), ('-sketch', CREAM / 'sky/clouds')):
    strip = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for x, y, k in LAYOUT: strip.alpha_composite(Image.open(base / f'clouds-{k}.png').convert('RGBA'), (x, y))
    save(strip, f'sky/clouds{state}.png')

# birds: three flap frames (32x16 art centred in 32x32 cells); the sketch is the same frames in the cream sky's bird tone
KIND = 2
frames = [Image.open(COL / f'sky/birds/birds-{KIND}-{f}.png').convert('RGBA') for f in range(3)]
cream_tone = (182, 177, 166)
for state in ('', '-sketch'):
    atlas = Image.new('RGBA', (96, 32), (0, 0, 0, 0))
    for f, fr in enumerate(frames):
        a = np.array(fr)
        if state: a[a[..., 3] > 0, :3] = cream_tone
        atlas.alpha_composite(Image.fromarray(a), (f * 32, 8))
    save(atlas, f'sky/bird{state}.png')
print(sorted(str(p.relative_to(OUT)) for p in OUT.rglob('*.png')))

# far village and mountains: the combat stages stretch each band's bottom row down to the floor; the village draws
# plain sprites, so both states get that continuation baked in (3 units below the band), as the sky must not show
# through the near village's gaps above the ground.
EXTEND = 96
# the sketch is the same cleaned drawing in cream (make_bands_clean.py), so both states coincide when overlapped
for state, base in (('color', COL), ('sketch', COL / 'sketch')):
    for name in ('far-village', 'mountains'):
        band = Image.open(base / f'{name}.png').convert('RGBA')
        tall = Image.new('RGBA', (band.width, band.height + EXTEND), (0, 0, 0, 0))
        tall.paste(band, (0, 0))
        tall.paste(band.crop((0, band.height - 1, band.width, band.height)).resize((band.width, EXTEND), Image.NEAREST), (0, band.height))
        save(tall, f'background/{state}/{name}.png')
