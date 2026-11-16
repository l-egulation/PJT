"""Summit record board (꼭대기 기록판): a stone lectern holding an open record book, in the bell tower's style —
lit front faces, narrow shaded right faces, a pedestal that widens toward its foot; the book's ribbon in gold.
Logical 40x42 (80x84 px); the bottom row is the foot on the goal stone, the centre column is 20."""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from px import *
from s_tower import X1, X2, X3, GOLD_L, GOLD, GOLD_M, GOLD_D, face, ledge

OUT = os.path.join(os.path.dirname(__file__), 'sky_out')
WD, HT = 40, 42
FRONT = [(0, W), (.12, C), (.7, X1), (.9, L)]
SIDE = [(0, X2), (.45, M), (.85, X3)]


def board():
    c = Canvas(WD, HT)
    # -- pedestal: widens toward the foot (front + side faces), on a two-step plinth
    for y in range(20, 34):
        d = (y - 20) // 5
        fx0, split, sx1 = 13 - d, 22 + (d + 1) // 2, 26 + d
        face(c, fx0, split, y, y, FRONT); face(c, split + 1, sx1, y, y, SIDE)
    for x in range(10, 30):                       # shadow under the desk
        p = c.get(x, 20)
        if p[3]: c.px(x, 20, X2 if p in (W, C, X1) else X3)
    ledge(c, 8, 26, 31, 34, [(W, X2), (C, M), (X1, M), (L, X3)])
    ledge(c, 6, 27, 33, 38, [(W, X2), (C, M), (X1, X3), (L, X3)])
    # -- desk: a slanted slab, its lit top edge and shaded front lip
    ledge(c, 4, 29, 35, 16, [(C, X2), (X1, M), (L, X3), (X2, D)])
    # -- open book on the desk: dark cover peeking out, two pages bowing toward the spine
    for x in range(3, 37):
        c.px(x, 15, D)  # cover: 3..36, centred on the spine (19|20)
    pages = {}
    for x in range(4, 36):
        dip = 1 if x in (19, 20) else 0
        top = 6 + dip + (1 if x in (4, 35) else 0)
        for y in range(top, 15): pages[(x, y)] = None
    for (x, y) in pages:
        right = x >= 20
        col = W if not right else C
        if right and x >= 32: col = X1
        if x in (19, 20): col = X1 if not right else L          # gutter shade
        c.px(x, y, col)
    for i, y in enumerate((8, 10, 12)):                      # writing, as tone lines
        c.hl(7, 16 - (i % 2) * 2, y + (1 if i == 0 else 0), L)
        c.hl(23, 32 - (i == 1) * 3, y + (1 if i == 0 else 0), X2)
    # -- gold ribbon from the spine, hanging over the desk's lip
    for y in range(13, 22): c.px(21, y, GOLD if y < 20 else GOLD_M)
    c.px(22, 13, GOLD_L); c.px(20, 21, GOLD_M); c.px(22, 21, GOLD_M)
    c.outline(K)
    for y in range(6, 15): c.px(20, y, K) if (20, y) in pages and y > 7 else None   # spine line
    return c


if __name__ == '__main__':
    b = board()
    lint(b, [K], 'board')
    print('bbox', b.im.getbbox())
    b.native().save(os.path.join(OUT, 'summit-record-board.png'))
    big = b.native().resize((WD * 2 * 5, HT * 2 * 5), Image.NEAREST)
    bg = Image.new('RGBA', big.size, (80, 70, 90, 255)); bg.alpha_composite(big); bg.save(os.path.join(OUT, 'sheet-board.png'))
