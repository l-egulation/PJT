"""The user's hand-finished buildings (colour, palette colours only) -> the sketch twin by material:
each colour is the material it is in the shared palette, painted with the SKETCH colour of that material."""
import sys
from collections import defaultdict
from PIL import Image
from hub_bld import SK, CO

PREFER = {'5c3a22': 'in', '3f3e3c': 'out'}          # colours two materials share: the ink wins


def material_map():
    by = defaultdict(list)
    for k, v in CO.items(): by['%02x%02x%02x' % v[:3]].append(k)
    return {hx: (PREFER.get(hx) or ks[0]) for hx, ks in by.items()}


def sketch_of(path):
    im = Image.open(path).convert('RGBA'); m = material_map(); out = im.copy(); px = out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a: px[x, y] = SK[m['%02x%02x%02x' % (r, g, b)]][:3] + (255,)
    return out


if __name__ == '__main__':
    sketch_of(sys.argv[1]).save(sys.argv[2])
