"""Colour versions of the lawn tiles, decor, junctions and wood platforms.
Terrain: per 32px tile, per column, everything from the top down to one dot below the lowest grass-only tone
(^ = %) is the grass band; the rest is soil. Ink becomes dark green in the grass and dark brown in the soil."""
from pathlib import Path
import numpy as np
from PIL import Image
LAWN = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112/resources/environments/stage1-pixel-lawn-wood-32/runtime")
FILL, INK, LIGHT, MID, HI, DARK = (254,247,238), (63,62,60), (204,199,190), (160,157,150), (255,252,247), (111,110,105)
GRASS_ONLY = (HI, MID, DARK)

GRASS = {INK:(58,82,52), HI:(178,198,132), MID:(136,162,100), DARK:(92,120,74), LIGHT:(112,140,86), FILL:(136,162,100)}
OPTIONS = {
 'A': dict(name='모래흙', soil={FILL:(230,206,168), LIGHT:(212,184,146), MID:(196,166,128), HI:(242,226,196), DARK:(160,130,100), INK:(86,66,52)}),
 'B': dict(name='갈색흙', soil={FILL:(196,152,112), LIGHT:(176,132,96), MID:(160,118,84), HI:(214,176,136), DARK:(118,86,62), INK:(76,56,44)}),
 'C': dict(name='짙은흙', soil={FILL:(158,116,86), LIGHT:(140,100,74), MID:(126,90,66), HI:(182,140,104), DARK:(96,68,50), INK:(64,46,38)}),
}
OPTIONS['D'] = dict(name='짙은흙+음영', soil=OPTIONS['C']['soil'])
OPTIONS['E'] = dict(name='속 비우기(2챕터식)', soil=OPTIONS['C']['soil'])
WOOD = {FILL:(206,158,108), MID:(170,124,82), HI:(230,192,140), LIGHT:(188,142,96), DARK:(124,86,58), INK:(80,56,42)}

def remap(arr, table, where=None):
    out = arr.copy()
    for src, dst in table.items():
        m = np.all(arr[..., :3] == src, axis=-1) & (arr[..., 3] > 0)
        if where is not None: m &= where
        out[m, :3] = dst
    return out

def grass_zone(arr):
    """a tile with grass tones in its top 10px has its top surface exposed; its top 14px (blades, dark line,
    lit lip and shaded underside) is the grass band. Grass tones lower down are soil hatch marks."""
    h, w = arr.shape[:2]; z = np.zeros((h, w), bool)
    g = np.zeros((h, w), bool)
    for c in GRASS_ONLY: g |= np.all(arr[..., :3] == c, axis=-1) & (arr[..., 3] > 0)
    from scene import SHAPES
    for k in range(len(SHAPES) * 4):
        ty, tx = k // 16 * 32, k % 16 * 32
        if not SHAPES[k // 4] & 1: z[ty:ty + 14, tx:tx + 32] = True   # no neighbour above: grass on top
    return z

def terrain(arr, soil):
    z = grass_zone(arr)
    out = remap(arr, GRASS, z)
    return remap(out, soil, ~z) if True else out

def load(rel): return np.array(Image.open(LAWN / rel).convert("RGBA"))

def build(opt):
    soil = OPTIONS[opt]['soil']; res = {}
    res['terrain-atlas.png'] = terrain(load('terrain-atlas.png'), soil)
    if opt == 'E':
        import tiledetail; res['terrain-atlas.png'] = tiledetail.hollow(load('terrain-atlas.png'), res['terrain-atlas.png'])
    if opt == 'D':
        import tiledetail; res['terrain-atlas.png'] = tiledetail.detail(load('terrain-atlas.png'), res['terrain-atlas.png'])
    for n in ('inside-left-down', 'inside-left-up', 'inside-right-down', 'inside-right-up'):
        res[f'junctions/{n}.png'] = remap(load(f'junctions/{n}.png'), soil)
    for n in ('left', 'middle-a', 'middle-b', 'right', 'single'):
        res[f'wood/{n}.png'] = remap(load(f'wood/{n}.png'), WOOD)
    for i in range(4): res[f'decor/pencil-patch-{i}.png'] = remap(load(f'decor/pencil-patch-{i}.png'), soil)
    for i in range(3):
        peb = {DARK:(132,120,108), HI:(216,206,192), MID:(170,156,140), FILL:soil[FILL], INK:soil[INK]}
        res[f'decor/embedded-pebble-{i}.png'] = remap(load(f'decor/embedded-pebble-{i}.png'), peb)
    for s in ('left', 'right'): res[f'decor/wall-blade-{s}.png'] = remap(load(f'decor/wall-blade-{s}.png'), GRASS)
    res['decor/daisy.png'] = remap(load('decor/daisy.png'), {INK:GRASS[INK], HI:(255,253,244), DARK:(236,190,76)})
    out = {k: Image.fromarray(v) for k, v in res.items()}
    if opt in ('D', 'E'):
        import tiledetail
        for i in range(3): out[f'decor/embedded-pebble-{i}.png'] = tiledetail.stone_sprite(i) if opt == 'D' else tiledetail.dim(tiledetail.stone_sprite(i))
    if opt == 'E':
        for i in range(4): out[f'decor/pencil-patch-{i}.png'] = Image.fromarray(remap(load(f'decor/pencil-patch-{i}.png'), {MID: tiledetail.VOID_LT, LIGHT: tiledetail.VOID_LT}))
    return out

if __name__ == '__main__':
    a = load('terrain-atlas.png'); z = grass_zone(a)
    # sanity: grass-only tones below 16px inside a tile?
    g = np.zeros(a.shape[:2], bool)
    for c in GRASS_ONLY: g |= np.all(a[..., :3] == c, axis=-1)
    ys = np.nonzero(g)[0] % 32; print('grass tones max tile-y', ys.max(), 'count >=16:', (ys >= 16).sum())
