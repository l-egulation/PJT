"""Native tutorial silhouettes on the 32px grid."""
from PIL import Image, ImageDraw, ImageFilter

PALETTE = {'blade': '#f3ebd9'}


def sword():
    image = Image.new('RGBA', (32, 32))
    pen = ImageDraw.Draw(image)
    ivory = PALETTE['blade']
    pen.polygon([(3,27),(5,29),(14,20),(12,18)], fill=ivory)
    pen.polygon([(7,15),(9,13),(19,23),(17,25)], fill=ivory)
    pen.polygon([(11,18),(25,4),(27,4),(28,5),(28,6),(14,21)], fill=ivory)
    return image


def variants(filled=None):
    if filled is None:
        filled = sword()
    alpha = filled.getchannel('A')
    outline = filled.copy()
    outline.paste((0,0,0,0), (0,0,32,32), alpha.filter(ImageFilter.MinFilter(3)))
    broken = outline.copy()
    pen = ImageDraw.Draw(broken)
    pen.polygon([(17,0),(31,0),(31,17),(23,13),(22,10),(19,11),(18,8)], fill=(0,0,0,0))
    for xy in [(24,7),(27,4),(30,6),(24,14)]:
        pen.point(xy, fill=PALETTE['blade'])
    return {'sword':filled,'sketch':outline,'broken-sword':broken}
