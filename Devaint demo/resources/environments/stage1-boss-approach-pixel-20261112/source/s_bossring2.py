"""Far rose window behind the boss room, measured from the reference (a-arena): a thick outer stone ring,
a thin inner ring, radial mullions between the inner ring and an inner circle, and on each side a ledge
on a column where the ring springs. Faint colours sampled from the painting. 480 x 243 dots."""
import math
from px import *

RING = (223, 213, 203, 255); RING_J = (206, 195, 185, 255); RING_HI = (234, 227, 218, 255); RING_SH = (214, 203, 193, 255); RING_ALT = (219, 209, 199, 255)
W_, H_ = 480, 243
CX, CY = 240, 115            # reference centre (836, 399) * 0.287
R_OUT0, R_OUT1 = 129, 142    # thick outer ring
R_IN0, R_IN1 = 117, 122      # thin inner ring
R_HUB = 86                   # mullions run from the inner circle to the inner ring
N_BARS, BAR_W = 16, 3        # half-width in dots


def draw():
    c = Canvas(W_, H_)
    pts = []
    for y in range(H_):
        for x in range(W_):
            d = math.hypot(x - CX, y - CY)
            if R_OUT0 <= d <= R_OUT1 or R_IN0 <= d <= R_IN1 or R_HUB - 4 <= d <= R_HUB: c.px(x, y, RING)
    for k in range(N_BARS):
        a = k * math.tau / N_BARS
        ca, sa = math.cos(a), math.sin(a)
        poly = [(CX + t * ca - o * sa, CY + t * sa + o * ca) for t, o in ((R_HUB, -BAR_W), (R_IN0 + .5, -BAR_W), (R_IN0 + .5, BAR_W), (R_HUB, BAR_W))]
        c.poly(poly, fill=RING)
        poly = [(CX + t * ca - o * sa, CY + t * sa + o * ca) for t, o in ((R_IN1, -2), (R_OUT0 + .5, -2), (R_OUT0 + .5, 2), (R_IN1, 2))]
        c.poly(poly, fill=RING)
    # side ledges on columns where the ring springs (left one measured, right mirrored)
    for x0, x1 in ((86, 135), (W_ - 136, W_ - 87)):
        c.rect(x0, 105, x1, 112, fill=RING); c.rect(x0 + 3, 113, x1 - 3, 120, fill=RING)
        colx = (x0 + 8, x0 + 28) if x0 < W_ // 2 else (x1 - 28, x1 - 8)
        c.rect(colx[0], 121, colx[1], 190, fill=RING)
        c.rect(colx[0] - 3, 121, colx[1] + 3, 125, fill=RING)
    # no outline: shapes read by small face-tone steps, like the village silhouettes
    for k in range(44):                                      # alternate voussoir tones on the outer ring
        a0, a1 = k * math.tau / 44, (k + 1) * math.tau / 44
        if k % 2: continue
        for y in range(H_):
            for x in range(W_):
                if c.get(x, y) != RING: continue
                d = math.hypot(x - CX, y - CY)
                if R_OUT0 <= d <= R_OUT1 and a0 <= math.atan2(y - CY, x - CX) % math.tau < a1: c.px(x, y, RING_ALT)
    for y in range(H_):
        for x in range(W_):
            if c.get(x, y) != RING and c.get(x, y) != RING_ALT: continue
            d = math.hypot(x - CX, y - CY)
            if y < CY and abs(d - R_OUT1) < 1.2: c.px(x, y, RING_HI)        # lit top rim
            if y > CY and abs(d - R_OUT0) < 1.2: c.px(x, y, RING_SH)        # shaded inner rim
    for x0, x1 in ((86, 135), (W_ - 136, W_ - 87)):                          # ledge undersides in shade
        for x in range(x0, x1 + 1): c.px(x, 112, RING_SH)
        for x in range(x0 + 3, x1 - 2): c.px(x, 120, RING_SH)
    return c


if __name__ == '__main__':
    draw().native().save('out/bossring2.png')
