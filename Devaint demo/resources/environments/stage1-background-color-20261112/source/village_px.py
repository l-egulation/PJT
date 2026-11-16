"""Shared drawing for the village bands in 2x2 dots: 1:1 slopes only, clean discs, big light/shade shapes."""
import numpy as np

class Band:
    def __init__(self, w, h, pal):
        self.w, self.h, self.p = w, h, pal
        self.img = np.zeros((h, w, 4), np.uint8)
        self.yy, self.xx = np.mgrid[0:h, 0:w]

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h: self.img[y, x, :3] = c; self.img[y, x, 3] = 255
    def span(self, y, x0, x1, c):
        for x in range(x0, x1 + 1): self.put(x, y, c)
    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1): self.span(y, x0, x1, c)
    def disc(self, cx, cy, r):
        return (self.xx - cx) ** 2 + (self.yy - cy) ** 2 <= r * r + r * .6
    def paint(self, m, c):
        self.img[m, :3] = c; self.img[m, 3] = 255

    def blob(circles, *a, **k): raise NotImplementedError

    def crowns(self, circles, tones=None, trunk=None):
        """round leaf clumps drawn back to front, each with its own light rim (upper left) and shade rim (lower
        right), so a tree reads as separate clumps instead of one lump; optional trunk (x, top, bottom) under them"""
        L, M, D = tones or (self.p['leaf_l'], self.p['leaf'], self.p['leaf_d'])
        if trunk: self.rect(trunk[0], trunk[1], trunk[0] + 1, trunk[2], self.p.get('trunk', D))
        S = lambda a, dx, dy: np.roll(np.roll(a, dy, 0), dx, 1)
        for cx, cy, r in circles:
            m = self.disc(cx, cy, r)
            self.paint(m, M)
            self.paint(m & ~S(m, -2, -2), D)
            self.paint(m & ~S(m, 2, 2) & S(m, -2, -2), L)

    def edge_house(self, side, half, peak_y, bottom, wins=(), roof_rows=None, door=None):
        """half of one wide symmetric house whose centre line is the band's edge: the band repeats mirrored, so
        the next copy completes it. Its roof is a symmetric hip (flat in the middle, 1:1 ends) of roof_rows rows,
        all in roof tiles, the eave shade under it; it fills the whole seam zone so nothing else is mirrored there."""
        p = self.p; rows = roof_rows if roof_rows is not None else half
        eave = peak_y + rows
        xs = range(0, half + 2) if side == 'left' else range(self.w - 2 - half, self.w)
        for x in xs:
            i = x if side == 'left' else self.w - 1 - x               # distance from the centre line
            if i > half + 1: continue
            top = peak_y + max(0, i - (half - rows))
            if i == half + 1: top = eave
            for y in range(top, eave + 1): self.put(x, y, p['roof'])
            if i <= half:
                self.put(x, eave + 1, p['eave'])
                for y in range(eave + 2, bottom + 1): self.put(x, y, p['wall'])
            if i < half: self.put(x, eave, p['roof_sh'])
        for wx, wy in wins:
            x = wx if side == 'left' else self.w - 1 - wx - 1
            self.windows(x, wy, 1)
        if door:                                   # (half width, top): an arched door on the centre line
            dw, dt = door
            for i in range(dw):
                x = i if side == 'left' else self.w - 1 - i
                for y in range(dt + (1 if i == dw - 1 else 0), bottom + 1): self.put(x, y, p['win'])

    def cypress(self, cx, top, length=26, half=3):
        """a cypress: a flame of foliage, 1:2 taper at the tip, full width through the middle, narrowing again at
        the foot where it sinks into the tree row; light edge left, shade edge right, a short trunk below"""
        p = self.p
        for i in range(length + 1):
            w = min(half, i // 2, (length - i) // 3 + 1)
            y = top + i
            self.span(y, cx - w, cx + w, p['leaf'])
            if w >= 1: self.put(cx - w, y, p['leaf_l'])
            if w >= 2: self.put(cx + w, y, p['leaf_d'])
        self.rect(cx, top + length + 1, cx, top + length + 3, p.get('trunk', p['leaf_d']))

    def windows(self, x, y, n=2, h=3):
        for i in range(n): self.rect(x + i * 3, y, x + i * 3 + 1, y + h - 1, self.p['win'])

    def home(self, x0, front_w, gable_w, peak_y, bottom, gable_right=True, chimney=None, wins=(), gwin=True, win_h=3):
        """3/4-view house: lit front face under a roof slab, the gable end in shade; every slope 1:1"""
        p = self.p; h = gable_w // 2; eave = peak_y + h
        if gable_right:
            gx0 = x0 + front_w; gx1 = gx0 + gable_w - 1; fx0, fx1 = x0, gx0 - 1
        else:
            gx0 = x0; gx1 = x0 + gable_w - 1; fx0, fx1 = gx1 + 1, gx1 + front_w
        gc = gx0 + h
        if chimney is not None:
            self.rect(chimney, peak_y - 4, chimney + 3, peak_y + 3, p['wall']); self.span(peak_y - 5, chimney - 1, chimney + 4, p['roof'])
        for i in range(h + 1): self.span(peak_y + i, gc - i, gc + i, p['wall_sh'])
        self.rect(gx0, eave, gx1, bottom, p['wall_sh'])
        for i in range(h + 1):
            y = peak_y + i
            a, b = (fx0 + h - i - 1, gc - i - 1) if gable_right else (gc + i + 1, fx1 - h + i + 1)
            self.span(y, a, b, p['roof'])
            self.put(b if gable_right else a, y, p['roof_sh'])
        for i in range(h + 2):
            self.put(gc - i, peak_y + i - 1, p['roof']); self.put(gc + i, peak_y + i - 1, p['roof'])
        self.rect(fx0, eave + 1, fx1, bottom, p['wall'])
        self.span(eave + 1, fx0, fx1, p['eave'])
        self.span(eave, fx0 - 1 if gable_right else fx0, fx1 if gable_right else fx1 + 1, p['roof'])
        for wx, wy in wins: self.windows(wx, wy, h=win_h)
        if gwin: self.windows(gc - 1 if gable_w > 4 else gc, eave + 3, 1, h=win_h)

    def hip(self, x0, x1, top, eave, bottom, wins=()):
        """a low house seen side-on: hip roof (1:1 both ends), lit wall below"""
        p = self.p; rows = eave - top
        for i in range(rows + 1):
            a, b = x0 + rows - i, x1 - rows + i
            self.span(top + i, a, b, p['roof']); self.put(b, top + i, p['roof_sh'])
        self.span(eave, x0 - 1, x1 + 1, p['roof'])
        self.rect(x0, eave + 1, x1, bottom, p['wall']); self.span(eave + 1, x0, x1, p['eave'])
        for wx, wy in wins: self.windows(wx, wy)

    def fill_bottom(self, y0, c):
        for y in range(y0, self.h):
            gap = self.img[y, :, 3] == 0; self.img[y, gap, :3] = c; self.img[y, gap, 3] = 255

def close_pinholes(band):
    """single sky dots and one-dot slits caught between shapes take a neighbour's colour"""
    img = band.img
    for _ in range(2):
        a = img[..., 3] > 0
        for y in range(1, band.h - 1):
            for x in range(1, band.w - 1):
                if a[y, x]: continue
                nb = [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
                solid = [q for q in nb if a[q]]
                slit = (a[y, x - 1] and a[y, x + 1]) or (a[y - 1, x] and a[y + 1, x])
                if len(solid) >= 3 or slit: img[y, x] = img[solid[-1]]
Band.close_pinholes = close_pinholes

def hedge(band, y, seed, r=(6, 9), gap=(7, 11), jitter=2):
    """a hedge of uneven bushes along the whole bottom, one bush centred on each end (it mirrors into itself), so
    the band ends in bush tops and the flat fill below continues the bushes' shade colour, never a cut line"""
    import random
    rnd = random.Random(seed); x = rnd.randint(4, 8); out = [(-.5, y, r[1]), (band.w - .5, y, r[1])]
    while x < band.w - 4:
        out.append((x, y + rnd.randint(-jitter, jitter), rnd.randint(*r))); x += rnd.randint(*gap)
    out.sort(key=lambda c: c[1])
    band.crowns(out)
    band.rect(0, y + 3, band.w - 1, band.h - 1, band.p['leaf_d'])
Band.hedge = hedge
