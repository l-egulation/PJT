"""Tiny logical-grid drawing kit. One logical dot = 2x2 screen px in the output."""
from PIL import Image, ImageDraw

def h(c):
    c = c.lstrip('#'); return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4)) + (255,)

# adopted terrain palette (stage1-pixel-lawn-wood-32)
W = h('fffcf7'); C = h('fef7ee'); L = h('ccc7be'); M = h('a09d96'); D = h('6f6e69'); K = h('3f3e3c')
INK = h('1e1d1c')          # liquid ink, darker than the outline
CLEAR = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w, h_):
        self.w, self.h = w, h_
        self.im = Image.new('RGBA', (w, h_), CLEAR)
        self.d = ImageDraw.Draw(self.im)

    def px(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.im.putpixel((x, y), c)

    def get(self, x, y):
        return self.im.getpixel((x, y)) if 0 <= x < self.w and 0 <= y < self.h else CLEAR

    def rect(self, x0, y0, x1, y1, fill=None, line=None):
        self.d.rectangle([x0, y0, x1, y1], fill=fill, outline=line)

    def box(self, x0, y0, x1, y1, fill, line=K, round_=1):
        """filled box with 1-dot outline; round_ cuts the corner dots"""
        self.rect(x0, y0, x1, y1, fill=fill, line=line)
        if round_:
            for x, y in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
                self.px(x, y, CLEAR)
            for x, y in ((x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)):
                self.px(x, y, line)

    def hl(self, x0, x1, y, c):
        for x in range(min(x0, x1), max(x0, x1) + 1): self.px(x, y, c)

    def vl(self, x, y0, y1, c):
        for y in range(min(y0, y1), max(y0, y1) + 1): self.px(x, y, c)

    def line(self, pts, c):
        self.d.line(pts, fill=c, width=1)

    def poly(self, pts, fill=None, line=None):
        self.d.polygon(pts, fill=fill, outline=line)

    def mask_fill(self, mask_fn, c, only=None):
        for y in range(self.h):
            for x in range(self.w):
                if mask_fn(x, y) and (only is None or self.get(x, y) in only):
                    self.px(x, y, c)

    def outline(self, c=K, where=None):
        """paint the outer contour (4-neighbour edge of the opaque area) in c"""
        a = [[self.get(x, y)[3] > 0 for x in range(self.w)] for y in range(self.h)]
        edge = []
        for y in range(self.h):
            for x in range(self.w):
                if not a[y][x]: continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < self.w and 0 <= ny < self.h) or not a[ny][nx]:
                        edge.append((x, y)); break
        for x, y in edge:
            if where is None or where(x, y): self.px(x, y, c)

    def blit(self, other, ox, oy):
        self.im.alpha_composite(other.im, (ox, oy))

    def native(self):
        return self.im.resize((self.w * 2, self.h * 2), Image.NEAREST)


def ascii_mask(rows):
    return {(x, y) for y, r in enumerate(rows) for x, ch in enumerate(r) if ch != '.'}


def rings(mask):
    """peel a mask into 4-neighbour rings: [edge, next edge, ...]"""
    out = []; cur = set(mask)
    while cur:
        edge = {(x, y) for x, y in cur if any((x + dx, y + dy) not in cur for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
        out.append(edge); cur -= edge
    return out


def paint(c, rows, ox, oy, legend):
    """draw an ASCII bitmap; '.' keeps what is underneath"""
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in legend: c.px(ox + x, oy + y, legend[ch])


def lint(c, colors=None, name=''):
    """report clumps: 2x2 blocks of one line colour, and L-elbows (a dot whose same-colour
    neighbours are exactly one horizontal + one vertical with the diagonal between them empty)."""
    colors = colors or [K, D]
    g = lambda x, y: c.get(x, y)
    blocks, elbows = [], []
    for y in range(c.h):
        for x in range(c.w):
            p = g(x, y)
            if p not in colors: continue
            if g(x + 1, y) == p and g(x, y + 1) == p and g(x + 1, y + 1) == p:
                blocks.append((x, y))
            hs = [dx for dx in (-1, 1) if g(x + dx, y) == p]
            vs = [dy for dy in (-1, 1) if g(x, y + dy) == p]
            if len(hs) == 1 and len(vs) == 1:
                dx, dy = hs[0], vs[0]
                # a real corner continues straight on both arms; an elbow on a curve has a diagonal step next
                if g(x + dx, y + dy) != p and g(x + 2 * dx, y) != p and g(x, y + 2 * dy) != p:
                    elbows.append((x, y))
    print(f'lint {name}: {len(blocks)} 2x2 clumps {blocks[:8]} | {len(elbows)} elbows {elbows[:8]}')
    return blocks, elbows


def mirror_rows(left):
    """left half including the centre column -> full symmetric rows"""
    return [r + r[-2::-1] for r in left]


def smooth_mask(mask):
    """shave outer-corner L-elbows off a filled mask so its 4-edge is a clean 1-dot line"""
    mask = set(mask)
    while True:
        edge = {(x, y) for x, y in mask if any((x + dx, y + dy) not in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
        drop = set()
        for x, y in edge:
            hs = [dx for dx in (-1, 1) if (x + dx, y) in edge]
            vs = [dy for dy in (-1, 1) if (x, y + dy) in edge]
            if len(hs) == 1 and len(vs) == 1 and (x + hs[0], y + vs[0]) not in edge \
                    and (x + 2 * hs[0], y) not in edge and (x, y + 2 * vs[0]) not in edge:
                drop.add((x, y))
        if not drop:
            return mask
        mask -= drop


def ellipse_mask(x0, y0, x1, y1):
    cx, cy, rx, ry = (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2, (x1 - x0 + 1) / 2, (y1 - y0 + 1) / 2
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)
            if ((x + .5 - cx) / rx) ** 2 + ((y + .5 - cy) / ry) ** 2 <= 1.0}


def declump(c, line, fill):
    """thin doubled line spots: in every 2x2 block of `line`, the most interior dot becomes `fill`;
    interior L-elbows (fully surrounded by opaque dots) are filled too"""
    for _ in range(6):
        changed = False
        for y in range(c.h - 1):
            for x in range(c.w - 1):
                blk = [(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)]
                if all(c.get(*p) == line for p in blk):
                    def opaque_nb(p):
                        return sum(c.get(p[0] + dx, p[1] + dy)[3] > 0 for dx in (-1, 0, 1) for dy in (-1, 0, 1))
                    p = max(blk, key=opaque_nb)
                    c.px(*p, fill); changed = True
        for y in range(1, c.h - 1):
            for x in range(1, c.w - 1):
                if c.get(x, y) != line: continue
                if not all(c.get(x + dx, y + dy)[3] for dx in (-1, 0, 1) for dy in (-1, 0, 1)): continue
                hs = [dx for dx in (-1, 1) if c.get(x + dx, y) == line]
                vs = [dy for dy in (-1, 1) if c.get(x, y + dy) == line]
                if len(hs) == 1 and len(vs) == 1 and c.get(x + hs[0], y + vs[0]) != line \
                        and c.get(x + 2 * hs[0], y) != line and c.get(x, y + 2 * vs[0]) != line:
                    c.px(x, y, fill); changed = True
        if not changed: break


def de_elbow(c, color, passes=4):
    """replace L-elbow dots of `color` (curve stair-steps) with their most common other neighbour colour"""
    from collections import Counter
    for _ in range(passes):
        _, elbows = lint(c, colors=[color], name='')
        if not elbows: break
        for x, y in elbows:
            nb = Counter(c.get(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                         if (dx or dy) and c.get(x + dx, y + dy) != color)
            if nb: c.px(x, y, nb.most_common(1)[0][0])
