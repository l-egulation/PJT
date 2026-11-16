"""Native hammer and UI arrow, authored directly on integer pixel grids."""
from PIL import Image, ImageDraw, ImageFilter
from tutorial_sword_pixels import PALETTE

def hammer():
    image=Image.new('RGBA',(32,32))
    pen = ImageDraw.Draw(image)
    ivory = PALETTE['blade']
    pen.polygon([(3,27),(5,29),(21,13),(19,11)],fill=ivory)
    pen.polygon([(11,8),(16,3),(18,3),(29,14),(29,16),(24,21),(22,21),(11,10)],fill=ivory)
    return image

def curved_arrow():
    image=Image.new('RGBA',(32,24)); pen=ImageDraw.Draw(image)
    ivory=PALETTE['blade']
    # One clean 45-degree transition joins equally thick vertical/horizontal runs.
    # Each diagonal step advances exactly one pixel; there is no irregular arc.
    pen.polygon([(4,20),(4,12),(9,7),(22,7),(22,3),(28,9),
                 (22,15),(22,11),(11,11),(8,14),(8,20)],fill=ivory)
    return image

def variants():
    return {'hammer':hammer(),'curved-arrow':curved_arrow()}
