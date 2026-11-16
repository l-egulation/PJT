import numpy as np
from PIL import Image, ImageDraw, ImageFont
import scene, tilecolour as TC
from color_preview import frame
C = "out/color5/"
def shot(opt):
    res = TC.build(opt) if opt else None
    scene._atlas = res['terrain-atlas.png'] if res else None
    import color_preview as CP
    # ground with a step and a pit wall so side/corner tiles show
    cells = ['.' * 31] * 9 + ['.' * 22 + '#' * 9] * 2 + ['.' * 18 + '#' * 13] * 2 + ['#' * 12 + '.' * 3 + '#' * 16] * 4
    sc = scene.Screen(cells); sc.im = Image.new("RGBA", (960, 540), CP.SKYC + (255,))
    for name, x, y in (("mountains", -200, 400), ("far-village", -60, 420), ("near-village", -120, 448)):
        im = Image.open(C + f"{name}.png"); w = im.width; xx = x - w
        while xx < 960:
            idx = round((xx - x) / w); piece = im.transpose(Image.FLIP_LEFT_RIGHT) if idx % 2 else im
            sc.im.alpha_composite(piece, (xx, y - im.height))
            row = piece.crop((0, piece.height - 1, w, piece.height)).resize((w, 540 - y)); sc.im.alpha_composite(row, (xx, y)); xx += w
    for i, (x, y) in enumerate(((60, 60), (420, 110), (760, 40))):
        sc.im.alpha_composite(Image.open(C + f"sky/clouds/clouds-{i}.png"), (x, y))
    sc.terrain()
    src = (lambda k: res[k]) if res else (lambda k: Image.open(TC.LAWN / k).convert("RGBA"))
    # wood platform at row 8
    for i, n in enumerate(['left', 'middle-a', 'middle-b', 'middle-a', 'right']):
        sc.im.alpha_composite(src(f'wood/{n}.png'), (160 + i * 32, 8 * 32 - 8))
    sc.im.alpha_composite(src('wood/single.png'), (560, 6 * 32 - 8))
    # decor on the ground
    for k, x, y in (('decor/daisy.png', 96, 13 * 32 - 30), ('decor/daisy.png', 780, 9 * 32 - 30),
                    ('decor/embedded-pebble-1.png', 64, 15 * 32), ('decor/embedded-pebble-2.png', 600, 15 * 32 + 8), ('decor/embedded-pebble-0.png', 840, 12 * 32),
                    ('decor/pencil-patch-0.png', 300, 15 * 32), ('decor/pencil-patch-2.png', 860, 14 * 32)):
        sc.im.alpha_composite(src(k), (x, y))
    sc.im.alpha_composite(src('decor/wall-blade-right.png'), (12 * 32 - 8, 13 * 32 + 4))
    sc.hero(500, 448 - 32 * 4 + 32 * 4 - 0)
    return sc.im.convert("RGB"), res

if __name__ == '__main__':
    import sys
    OPTS = (None, 'A', 'B', 'C') if len(sys.argv) < 2 else [None if a == '-' else a for a in sys.argv[1:]]
    font = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 22)
    shots = []
    for opt in OPTS:
        im, res = shot(opt); d = ImageDraw.Draw(im)
        label = '지금(스케치)' if not opt else f"{opt} {TC.OPTIONS[opt]['name']}"
        d.rectangle((8, 8, 260, 40), fill='white'); d.text((16, 10), label, font=font, fill='black')
        shots.append(im)
        if opt:
            import os; os.makedirs(f'out/tilecolour/{opt}', exist_ok=True)
            for k, v in res.items():
                os.makedirs(os.path.dirname(f'out/tilecolour/{opt}/{k}'), exist_ok=True); v.save(f'out/tilecolour/{opt}/{k}')
    s = Image.new("RGB", (960 * 2 + 8, 540 * 2 + 8), "white")
    for i, im in enumerate(shots): s.paste(im, (i % 2 * 968, i // 2 * 548))
    s.save('out/tilecolour-options.png')
    # zoom of option crops
    z = Image.new("RGB", (480 * 3 + 16, 240), "white")
    z = Image.new("RGB", (4 * 368 + 24, 2 * 200 + 8), "white")
    for i, im in enumerate(shots):
        z.paste(im.crop((300, 360, 484, 460)).resize((368, 200), Image.NEAREST), (i * 376, 0))
        z.paste(im.crop((60, 236, 244, 336)).resize((368, 200), Image.NEAREST), (i * 376, 208))
    z.save('out/tilecolour-zoom.png')
