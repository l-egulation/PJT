"""Ink recovery station as an ink fuel pump: globe sign, heart gauge, meter, nozzle on a hose.
Native 96x112 canvas (3.5u tall; the current art is 3.0u)."""
from px import *

HEART = mirror_rows([       # R rim, b band, w glass, s ink surface, i ink   (left half incl. centre column)
    "...RRRRR...",
    "..RbbbbbR..",
    ".RbwwwwwbR.",
    "Rbwwwwwwwbw",
    "Rbwwwwwwwww",
    "Rbsssssssss",
    "Rbiiiiiiiii",
    "Rbiiiiiiiii",
    "Rbiiiiiiiii",
    ".Rbiiiiiiii",
    "..Rbiiiiiii",
    "...Rbiiiiii",
    "....Rbiiiii",
    ".....Rbiiii",
    "......Rbiii",
    ".......Rbii",
    "........Rbi",
    ".........Rb",
    "..........R",
])
HEART = [list(r) for r in HEART]
for _row, _ch in ((0, '.'), (1, '.'), (2, '.'), (3, 'R'), (4, 'b')):
    HEART[_row][10] = _ch
HEART = [''.join(r) for r in HEART]

GLOBE = [                    # o rim, c face, C shade  (octagon: no elbows)
    "...ooooo...",
    "..occcccо..",
    ".occcccccо.",
    "occcccccccо",
    "occcccccccо",
    "occcccccccо",
    "occcccccccо",
    "occcccccCCо",
    ".occcccCCо.",
    "..oCCCCCо..",
    "...ooooo...",
]
GLOBE = [r.replace('о', 'o') for r in GLOBE]
DROP = [                     # ink drop icon inside the globe
    "..i..",
    "..i..",
    ".iii.",
    "iiiii",
    "iWiii",
    ".iii.",
]


def tube(c, path, wall=D, core=L):
    """3-dot hose around an 8-connected centre path"""
    P = set(path)
    T = {(x + dx, y + dy) for x, y in P for dx in (-1, 0, 1) for dy in (-1, 0, 1)}
    for x, y in T:
        edge = any((x + dx, y + dy) not in T for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        c.px(x, y, wall if edge else core)


def draw():
    c = Canvas(48, 56)
    # ---- base plinth
    c.rect(6, 50, 36, 55, fill=C)
    c.hl(7, 35, 51, W); c.hl(6, 36, 53, L); c.hl(6, 36, 50, D)
    for x in (13, 29): c.vl(x, 51, 53, M)
    # ---- cabinet
    c.rect(8, 17, 34, 49, fill=C)
    c.vl(9, 17, 49, W); c.vl(33, 17, 49, L)
    for x, y in ((10, 19), (32, 19), (10, 47), (32, 47)): c.px(x, y, M)       # rivets
    # ---- cornice
    c.rect(6, 13, 36, 16, fill=C)
    c.hl(7, 35, 14, W); c.hl(6, 36, 16, D); c.hl(8, 34, 17, L)
    # ---- neck and globe sign
    c.rect(18, 11, 24, 12, fill=L); c.hl(18, 24, 12, D)
    paint(c, GLOBE, 16, 0, {'o': D, 'c': W, 'C': L})
    paint(c, DROP, 19, 2, {'i': INK, 'W': M})
    # ---- heart gauge
    ox, oy = 11, 19
    for y, r in enumerate(HEART):
        for x, ch in enumerate(r):
            X, Y = ox + x, oy + y
            if ch == 'R': c.px(X, Y, D)
            elif ch == 'b': c.px(X, Y, C if (x < 8 and y < 4) else L)
            elif ch == 'w': c.px(X, Y, W)
            elif ch == 's': c.px(X, Y, M)
            elif ch == 'i': c.px(X, Y, INK)
    c.px(ox + 3, oy + 2, L); c.px(ox + 4, oy + 2, L)
    # ---- meter (price dial)
    c.rect(13, 40, 29, 45, fill=W, line=D)
    for x in (16, 21, 26): c.vl(x, 42, 43, M)
    c.hl(14, 28, 44, L)
    # ---- hose from the cabinet foot up to the nozzle
    path = [(34, 45), (35, 45), (36, 45), (37, 45), (38, 44), (39, 43)] + [(39, y) for y in range(42, 28, -1)]
    tube(c, path)
    # ---- nozzle holster on the right side + hanging nozzle
    c.vl(35, 24, 27, D); c.px(36, 24, D)                                     # bracket
    c.rect(37, 21, 40, 28, fill=M, line=D); c.vl(38, 22, 27, L)              # nozzle grip
    c.hl(35, 36, 22, D)                                                      # spout into the holster
    c.outline(K)
    lint(c, name='recovery-station')
    return c


if __name__ == '__main__':
    draw().native().save('out/recovery-station.png')
