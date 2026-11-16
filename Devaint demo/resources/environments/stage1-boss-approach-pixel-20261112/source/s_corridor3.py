"""Approach colonnade, traced block by block from corridor-rear.png in the same clean style as the boss towers:
light joints, flat faces with lit top / shaded bottom edges, colours sampled from the painting.
One repeating module 220 x 104 dots (440 x 208 px); the top 4 dots tuck under the ceiling tile."""
from px import *
from pxref import shift
from s_tower import block, course, crack

F = (218, 207, 194, 255); BEAM = (214, 203, 190, 255); BAND = (194, 182, 169, 255); CAP = (204, 192, 179, 255)
J = (188, 176, 164, 255); JD = (170, 158, 147, 255)
W_, H_ = 220, 104
T = 4                                    # rows hidden under the ceiling


def draw():
    import s_tower
    s_tower.J, s_tower.JD = J, JD        # this module own joint tones
    s_tower.F, s_tower.MID, s_tower.HI = F, BEAM, CAP
    c = Canvas(W_, H_)
    # beam: long blocks, a joint on the seam so repeats meet on a stone edge
    course(c, 0, T + 10, 0, W_ - 1, [96, 168], BEAM)
    c.vl(0, 0, T + 10, J)
    crack(c, [(140, T + 3), (143, T + 6), (141, T + 9)], J)
    # moulding band: lit lip over a shaded under-face
    c.rect(0, T + 11, W_ - 1, T + 17, fill=BAND)
    c.hl(0, W_ - 1, T + 11, J); c.hl(0, W_ - 1, T + 12, shift(BAND, 22)); c.hl(0, W_ - 1, T + 13, shift(BAND, 12))
    c.hl(0, W_ - 1, T + 16, shift(BAND, -8)); c.hl(0, W_ - 1, T + 17, JD)
    # capital: two steps over the pier
    block(c, 62, T + 18, 152, T + 23, CAP)
    block(c, 66, T + 23, 148, T + 28, BAND)
    # pier: three courses of big stones
    course(c, T + 28, T + 62, 66, 148, [110], F)
    course(c, T + 62, H_ - 1, 66, 148, [], F)
    crack(c, [(141, T + 33), (137, T + 41), (139, T + 49), (134, T + 57)], J)
    crack(c, [(78, T + 70), (82, T + 79), (80, T + 88)], J)
    # the painting's own light and shade inside each stone (same module crop as the trace)
    from PIL import Image
    from pxref import add_shading
    import s_corridor2 as P
    src = Image.open(P.SRC).convert("RGBA").crop((P.X0, P.TOP, P.X0 + P.PERIOD, 724))
    pad = Image.new("RGBA", (src.width, src.height + round(src.height * T / 100)), (0, 0, 0, 0))
    pad.paste(src, (0, pad.height - src.height)); top = src.crop((0, 0, src.width, 6)).resize((src.width, pad.height - src.height))
    pad.paste(top, (0, 0))
    add_shading(c, [F, BEAM, CAP], pad, step=7, levels=2, gain=2.6, clean=3, smooth=7)
    # ---- final details: broken corners, a long crack, fallen stones at the pier's foot
    import s_tower as ST
    for x, y, sx, sy in ((67, T + 29, 1, 1), (147, T + 48, -1, 1), (93, T + 71, 1, 1), (35, T + 1, 1, 1), (167, T + 9, -1, -1)):
        ST.chip(c, x, y, sx, sy)
    crack(c, [(100, T + 32), (103, T + 37), (101, T + 42)], JD)
    declump(c, J, F); declump(c, JD, BAND)
    import s_tower as _ST
    _ST.draw_cracks()
    lint(c, colors=[J, JD], name='corridor3')
    from pxref import light_temperature
    light_temperature(c)
    # start the module at the pier's capital so the hall opens on a pier, not on a floating beam end
    START = 62
    rolled = Canvas(W_, H_)
    rolled.im.paste(c.im.crop((START, 0, W_, H_)), (0, 0)); rolled.im.paste(c.im.crop((0, 0, START, H_)), (W_ - START, 0))
    c = rolled
    return c


if __name__ == '__main__':
    draw().native().save('out/corridor3-module.png')
