from px import *

RED, BLUE, YELLOW = h('c8352c'), h('3d5ea8'), h('e6a92e')


def lantern():
    c = Canvas(10, 19)
    rows = [
        "....oo....",
        "...o..o...",
        "....oo....",
        "....o.....",
        "....o.....",
        "....o.....",
        "....o.....",
        "...oooo...",
        "..oCCCCo..",
        ".oCWCCCLo.",
        "oooooooooo",
        ".oLWWWWLo.",
        ".oLWWWWLo.",
        ".oLWWWWLo.",
        ".oLWWWWLo.",
        ".oLWWWWLo.",
        ".oLLLLLLo.",
        "oMMMMMMMMo",
        ".oooooooo.",
    ]
    paint(c, rows, 0, 0, {'o': D, 'C': C, 'W': W, 'L': L, 'M': M})
    c.outline(K)
    lint(c, name='lantern')
    return c


def sign():
    c = Canvas(26, 33)
    hole = ellipse_mask(16, 16, 20, 20) | {(x, y) for x in range(19, 26) for y in range(19, 21)}
    board = smooth_mask(ellipse_mask(0, 10, 25, 32) - hole)
    for x, y in board:
        c.px(x, y, C)
    rr = rings(board)
    for x, y in rr[1]:                                             # rim light/shade just inside the outline
        c.px(x, y, L if x + y > 34 else W)
    for x, y, n in ((15, 24, 3), (6, 29, 3)):
        c.hl(x, x + n - 1, y, L)                                   # grain
    # paint dabs (kept in colour: it is the artisan's sign)
    paint(c, ["..rrr.", ".rrrrr", "rrrrrr", ".rrrr.", "..rr.."], 6, 13, {'r': RED})
    paint(c, ["..bbb.", ".bbbbb", "bbbbb.", ".bbbb.", "..bb.."], 3, 20, {'b': BLUE})
    paint(c, [".yyy..", "yyyyy.", "yyyyyy", ".yyyy.", "...y.."], 9, 25, {'y': YELLOW})
    c.px(13, 15, RED); c.px(2, 25, BLUE); c.px(16, 27, YELLOW)     # flecks
    # chain and mount
    c.rect(9, 0, 16, 2, fill=L); c.hl(10, 15, 0, C)
    for y in range(3, 10):
        c.px(12, y, D) if y % 3 else (c.px(11, y, D), c.px(13, y, D))
    c.px(12, 10, D)
    c.outline(K)
    lint(c, name='sign')
    return c


if __name__ == '__main__':
    lantern().native().save('out/maintenance-lantern.png')
    sign().native().save('out/maintenance-sign.png')
