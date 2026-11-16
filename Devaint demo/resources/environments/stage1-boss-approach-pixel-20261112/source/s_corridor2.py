"""Approach colonnade from the reference painting (corridor-rear.png), one repeating module, own colours."""
from PIL import Image
from px import *
from pxref import pixelize
SRC = r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/map-redesign-research-20261110/resources/environments/stage1-clockroom-original-assets-20261112/source/corridor-rear.png"
X0, PERIOD, TOP = 600, 1000, 116


def draw(w=220, h=100, k=6):
    im = Image.open(SRC).convert("RGBA").crop((X0, TOP, X0 + PERIOD, 724))
    c = pixelize(im, w, h, k=k)
    # a joint on the seam so repeats meet on a stone edge
    for y in range(h):
        if c.get(0, y)[3] and c.get(w - 1, y)[3]: c.px(0, y, c.line_colour)
    # close the top: the painting's beam edge is half transparent, and the art tucks 8px under the ceiling tile
    out = Canvas(w, h + 4)
    out.im.alpha_composite(c.im, (0, 4))
    for y in range(5):
        for x in range(w): out.px(x, y, c.get(x, 1))
    out.line_colour, out.palette = c.line_colour, c.palette
    lint(out, colors=[c.line_colour], name='corridor2')
    return out


if __name__ == '__main__':
    c = draw(); c.native().save('out/corridor2-module.png'); print(c.palette, c.line_colour)
