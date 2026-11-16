"""Detail pass 4 (both buildings): the wall stops being one flat face.
- deeper shadows under every overhang (eaves, awning): three rows of plaster_lo, then three of a new mid tone
- the wall darkens towards its right edge and its foot (two tones)
- window recesses: the top rows and left column of the glass in shadow (new glass_lo)
- worn plaster: a few soft patches in the mid tone, long one-dot cracks
New colours: plaster_mid, glass_lo (+ pass 3's wood_mid and paint highlights/shades)."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from px import *
from hub_bld import SK, CO
import bld_detail as B
import bld_detail2 as E

OUT = Path(__file__).parent.parent / "bld2"
CO4 = dict(E.CO3, plaster_mid=h('eee0c6'), plaster_sh=h('dcc8a6'), glass_lo=h('394049'))
SK4 = dict(E.SK3, plaster_mid=C, plaster_sh=L, glass_lo=SK['glass'])
ROOF = ('roof', 'roof_hi', 'roof_lo')
WALL = ('plaster', 'plaster_lo', 'plaster_mid', 'plaster_sh')


def overhang_shadow(key, near=3, far=3):
    """per column: plaster within `near` rows under a roof/awning is plaster_lo, the next `far` rows plaster_mid"""
    H, W = key.shape
    for x in range(W):
        last = None
        for y in range(H):
            v = key[y, x]
            if v in ROOF or (v == 'in' and y > 0 and key[y - 1, x] in ROOF): last = y; continue
            if last is None: continue
            if v in WALL:
                d = y - last
                if d <= near: key[y, x] = 'plaster_lo'
                elif d <= near + far and v == 'plaster': key[y, x] = 'plaster_mid'
            elif v in ('in', 'out', 'glass', 'stone', 'stone_hi', 'stone_lo') and y - last > 2:
                last = None if v != 'in' else last


def cast_shadow(key, long_=10, short=3, tones=((2, 'plaster_sh'), (6, 'plaster_lo'), (10, 'plaster_mid'))):
    """light from the upper left: every roof / awning dot casts its shadow down-right `long_` dots, every wooden or
    stone part sticking out of the wall `short` dots; the first `near` dots are plaster_lo, the rest plaster_mid"""
    H, W = key.shape
    src = key.copy()
    STUCK = ('wood', 'wood_hi', 'wood_lo', 'wood_mid', 'stone', 'stone_hi', 'stone_lo')
    for y in range(H):
        for x in range(W):
            v = src[y, x]
            if v in ROOF: L = long_
            elif v in STUCK: L = short
            elif v == 'in' and any(src[y + dy, x + dx] in ROOF for dx, dy in ((0, -1), (-1, 0), (-1, -1)) if 0 <= y + dy < H and 0 <= x + dx < W): L = long_
            else: continue
            for k in range(1, L + 1):
                yy, xx = y + k, x + k
                if not (yy < H and xx < W): break
                w = src[yy, xx]
                if w in WALL:
                    kk = k if L == long_ else k + 3              # short casters start in the lighter tones
                    tone = next((t for lim, t in tones if kk <= lim), None)
                    if tone is None: break
                    order = ['plaster', 'plaster_mid', 'plaster_lo', 'plaster_sh']
                    if order.index(tone) > order.index(key[yy, xx]): key[yy, xx] = tone
                elif w not in ROOF + STUCK + ('in',): break


def edge_shade(key, right=(4, 9), foot=(2, 5)):
    """the wall darkens towards its right edge (plaster_lo, then plaster_mid) and towards its foot"""
    H, W = key.shape
    for y in range(H):
        xs = [x for x in range(W) if key[y, x] == 'out']
        if not xs: continue
        r = max(xs)
        for x in range(r - right[1], r):
            v = key[y, x]
            if v == 'plaster': key[y, x] = 'plaster_lo' if r - x <= right[0] else 'plaster_mid'
    # foot: rows above the plinth
    for x in range(W):
        col = [y for y in range(H - 1, H // 2, -1) if key[y, x] == 'in' and y + 1 < H and key[y + 1, x] in ('stone', 'stone_hi', 'stone_lo')]
        if not col: continue
        t = col[0]
        for y in range(t - foot[1], t):
            if key[y, x] == 'plaster': key[y, x] = 'plaster_lo' if t - y <= foot[0] else 'plaster_mid'


def window_recess(key, rows=2):
    """the glass under the arch and along its left jamb lies in the recess's shadow"""
    H, W = key.shape
    g = key == 'glass'
    out = key.copy()
    for y in range(H):
        for x in range(W):
            if not g[y, x]: continue
            above = any(key[y - k, x] == 'in' for k in range(1, rows + 1) if y - k >= 0)
            left = key[y, x - 1] == 'in'
            if above or left: out[y, x] = 'glass_lo'
    key[:] = out


def patch(key, cx, cy, rx, ry):
    """a soft worn patch of plaster (mid tone), only over plain plaster"""
    m = smooth_mask(ellipse_mask(cx - rx, cy - ry, cx + rx, cy + ry))
    for x, y in m:
        if key[y, x] == 'plaster': key[y, x] = 'plaster_mid'


CRACK = ["k.......",
         ".k......",
         ".k......",
         "..k.....",
         "..kk....",
         "....k...",
         "....k.k.",
         ".....k.k",
         "......k."]


def crack(key, x, y, flip=False):
    rows = [r[::-1] for r in CRACK] if flip else CRACK
    for j, r in enumerate(rows):
        for i, ch in enumerate(r):
            if ch == 'k' and key[y + j, x + i] in WALL: key[y + j, x + i] = 'plaster_dk'


def finish(key):
    cast_shadow(key)
    edge_shade(key)
    window_recess(key)


def house4():
    key = E.house()
    finish(key)
    crack(key, 64, 64); crack(key, 108, 145, flip=True); crack(key, 23, 128)
    return key


def atelier4():
    key = E.atelier3()
    finish(key)
    crack(key, 116, 150); crack(key, 82, 180, flip=True)
    return key


if __name__ == '__main__':
    f = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 18)
    for name, fn, prev in (('house', house4, 'e-house-color.png'), ('atelier', atelier4, 'f-atelier-color.png')):
        key = fn()
        for v, P in (('color', CO4), ('sketch', SK4)):
            c = B.paint_keys(key, P); lint(c, colors=[K, P['in']], name=f'{name}4 {v}')
            c.im.save(OUT / f'g-{name}-{v}.png')
        o = Image.open(OUT / prev); d = Image.open(OUT / f'g-{name}-color.png'); k = Image.open(OUT / f'g-{name}-sketch.png')
        S = 3; s = Image.new('RGBA', (o.width * S * 3 + 60, o.height * S + 30), (244, 241, 233, 255)); dr = ImageDraw.Draw(s)
        for i, (im, t) in enumerate(((o, '이전'), (d, '수정 · 컬러'), (k, '수정 · 스케치'))):
            s.alpha_composite(im.resize((im.width * S, im.height * S), 0), (i * (o.width * S + 30), 30)); dr.text((i * (o.width * S + 30), 4), t, fill=(150, 0, 0), font=f)
        s.save(OUT / f'g-{name}-3.png')
