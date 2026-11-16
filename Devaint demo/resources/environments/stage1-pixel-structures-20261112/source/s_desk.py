from px import *


def jar(c, x0, y0, w):
    """jar w wide (odd), 10 tall; lid w-4 wide"""
    lid = w - 4
    rows = [
        ' ' * 2 + 'o' * lid + ' ' * 2,
        ' ' * 2 + 'o' + 'W' * (lid - 2) + 'o' + ' ' * 2,
        ' ' * 2 + 'o' + 'L' * (lid - 2) + 'o' + ' ' * 2,
        ' ' + 'o' + 'C' * (w - 4) + 'o' + ' ',
        'o' + 'C' + 'W' + 'C' * (w - 5) + 'L' + 'o',
        'o' + 'W' + 'C' * (w - 4) + 'L' + 'o',
        'o' + 'W' + 'C' * ((w - 4) // 2 - 1) + 'dd' + 'C' * (w - 4 - (w - 4) // 2 - 1) + 'L' + 'o',
        'o' + 'C' * (w - 3) + 'L' + 'o',
        'o' + 'L' * (w - 2) + 'o',
        ' ' + 'o' * (w - 2) + ' ',
    ]
    paint(c, rows, x0, y0, {'o': D, 'W': W, 'C': C, 'L': L, 'd': D})


def draw():
    c = Canvas(75, 39)
    # ---- legs (drawn first; the top overlaps them)
    for x0, x1, inner in ((3, 9, 10), (63, 70, 62)):
        c.rect(x0, 23, x1, 38, fill=C)
        c.vl(x1 if inner == 10 else x0, 24, 38, L)
        c.vl(inner, 24, 32, D)
    c.vl(10, 33, 38, D); c.vl(62, 33, 38, D)                      # leg inner edges below the apron
    c.px(6, 27, D); c.px(66, 27, D)                               # bolts
    c.px(6, 26, M); c.px(66, 26, M)
    # ---- apron between the legs
    c.rect(11, 24, 61, 32, fill=L)
    c.hl(11, 61, 24, M)
    c.hl(11, 61, 32, D)
    for x, y, n in ((16, 27, 5), (27, 29, 6), (39, 26, 4), (31, 26, 3), (20, 30, 4)):
        c.hl(x, x + n - 1, y, M)                                  # grain dashes
    # ---- table top
    c.rect(0, 17, 74, 22, fill=C)
    c.hl(1, 73, 18, W)
    c.hl(0, 74, 21, L)
    c.hl(0, 74, 22, D)
    c.hl(2, 72, 23, M)                                            # shadow under the top
    # ---- cloth draped over the front
    bottom = [29, 30, 31, 32, 33, 34, 35, 34, 33, 32, 31, 30, 29, 28]
    for i, x in enumerate(range(45, 59)):
        c.vl(x, 17, bottom[i] - 1, W)
        c.px(x, bottom[i], D)
    c.vl(44, 17, 28, D); c.vl(59, 17, 27, D)
    c.hl(45, 58, 22, L)                                           # fold over the table edge
    c.vl(49, 24, 31, L); c.vl(54, 24, 31, L)
    c.vl(57, 23, 29, L)
    # ---- jars on the top
    jar(c, 6, 7, 11)
    jar(c, 19, 8, 9)
    jar(c, 30, 8, 9)
    # ---- brush cup and brushes
    c.rect(60, 9, 68, 16, fill=C, line=D)
    c.hl(61, 67, 10, W); c.vl(67, 11, 15, L); c.hl(61, 67, 12, M)
    c.outline(K)
    # brushes after the silhouette pass: thin handle lines, dark bristle tips (two dots along the handle)
    for (xa, ya), (xb, yb), (tx, ty) in (((62, 8), (61, 3), (60, 1)), ((64, 8), (64, 2), (64, 0)),
                                         ((66, 8), (67, 3), (68, 1)), ((67, 8), (70, 5), (72, 3))):
        c.line([(xa, ya), (xb, yb)], D)
        c.line([(xb + (tx > xb) - (tx < xb), yb - 1), (tx, ty)], K)
    lint(c, name='maintenance-desk')
    return c


if __name__ == '__main__':
    draw().native().save('out/maintenance-desk.png')
