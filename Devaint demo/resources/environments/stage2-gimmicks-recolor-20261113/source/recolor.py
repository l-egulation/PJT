"""Recolour the chapter-1 seal, gate, chest, key and fall-shadow sprites to the stage-2 v26 sewer palette.

Shapes stay pixel for pixel; only the 7-step neutral ramp of the chapter-1 set is swapped for ramps taken from
environments/stage2-sewer-modular-20261111/stage2-sewer-modern-v26/source/render_modern.py (COOL for stone and iron,
WARM for wood). The yellow seal lights and the key keep their colour: they carry meaning.

    python source/recolor.py        (run from this asset folder; writes runtime/)
"""
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parents[1]
ENV = HERE.parent
PIXEL = ENV / 'stage1-pixel-structures-20261112' / 'runtime'
VILLAGE = ENV / 'village' / 'runtime' / 'stage-gimmicks-20261105'


def hexrgb(c):
    return tuple(int(c[i:i + 2], 16) for i in (1, 3, 5))


# chapter-1 ramp:  highlight  face       shade      mid        dark       outline    void
CH1 = ['#fffcf7', '#fef7ee', '#ccc7be', '#a09d96', '#6f6e69', '#3f3e3c', '#1e1d1c']
STONE = ['#75967d', '#5a7b69', '#46655a', '#36534e', '#2a4443', '#142228', '#070b0e']  # COOL 10..3, 0
IRON = ['#5a7b69', '#46655a', '#36534e', '#2a4443', '#1a2c31', '#0b1216', '#070b0e']   # COOL 9..1, 0
WOOD = ['#b77d3a', '#94632f', '#764e28', '#5b3d22', '#46301e', '#1c1513', '#0b0b0d']   # WARM 9..2, 0

SPRITES = {
    'seal-dormant': (PIXEL, STONE), 'seal-active': (PIXEL, STONE), 'seal-broken': (PIXEL, STONE),
    'seal-wave2': (PIXEL, STONE), 'seal-wave3': (PIXEL, STONE),
    'iron-gate-body': (PIXEL, IRON), 'iron-gate-tip': (PIXEL, IRON),
    'locked-chest-closed': (PIXEL, WOOD), 'locked-chest-open': (PIXEL, WOOD),
    'reward-chest-closed': (PIXEL, WOOD), 'reward-chest-open': (PIXEL, WOOD),
    'fall-shadow-decal': (VILLAGE, None), 'key-item': (VILLAGE, 'keep'),
}


def recolour(image, ramp):
    out = image.convert('RGBA')
    px = out.load()
    if ramp is None:  # the brown ink shadow becomes the sewer's blue-black, keeping its alpha and grain
        for y in range(out.height):
            for x in range(out.width):
                r, g, b, a = px[x, y]
                if a:
                    lum = (r + g + b) / 3 / 255
                    px[x, y] = (int(7 + lum * 40), int(11 + lum * 60), int(14 + lum * 66), a)
        return out
    table = {hexrgb(a): hexrgb(b) for a, b in zip(CH1, ramp)}
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in table:
                px[x, y] = table[(r, g, b)] + (a,)
    return out


def main():
    target = HERE / 'runtime'
    target.mkdir(exist_ok=True)
    for name, (folder, ramp) in SPRITES.items():
        source = Image.open(folder / (name + '.png'))
        result = source.convert('RGBA') if ramp == 'keep' else recolour(source, ramp)
        result.save(target / (name + '.png'), optimize=True)
        print(name)


if __name__ == '__main__':
    main()
