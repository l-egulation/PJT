"""Near village band redrawn by hand in 2x2 dots (333 x 167), after the user's colour design: every roof slope is
1:1, 3/4-view houses (lit front, gable end in shade), paired 2x3 windows, a bell tower with two equal pillars,
trees as separate round clumps each with its own light and shade. The band repeats mirrored, so each end is half of
a symmetric house that the next copy completes. Walls run to the band's bottom and a back row of trees closes every
gap. The sketch band is this drawing in cream (make_bands_clean.py)."""
from PIL import Image
from village_px import Band

W, H = 333, 167
PAL = dict(wall=(240, 223, 196), wall_sh=(226, 208, 180), eave=(208, 190, 164), roof=(197, 154, 128), roof_sh=(176, 134, 110),
           win=(149, 164, 144), leaf_l=(174, 188, 162), leaf=(163, 177, 151), leaf_d=(149, 164, 144), trunk=(139, 128, 110))
BELL = (149, 164, 144)
b = Band(W, H, PAL); p = PAL
BOTTOM = H - 1

def bell_tower(cx, base):
    """two equal pillars, a 1:1 gable cap with a cross, the bell hung in the middle"""
    b.put(cx, base - 25, p['roof']); b.span(base - 24, cx - 1, cx + 1, p['roof']); b.put(cx, base - 23, p['roof'])
    for i in range(7): b.span(base - 22 + i, cx - 1 - i, cx + 1 + i, p['roof'])
    b.span(base - 15, cx - 9, cx + 9, p['roof']); b.span(base - 14, cx - 8, cx + 8, p['eave'])
    for px in (cx - 8, cx + 6):
        for y in range(base - 13, base + 1): b.put(px, y, p['wall']); b.put(px + 1, y, p['wall']); b.put(px + 2, y, p['wall_sh'])
    b.span(base - 13, cx - 5, cx + 5, p['eave']); b.put(cx - 5, base - 12, p['eave']); b.put(cx + 5, base - 12, p['eave'])
    b.put(cx, base - 12, BELL); b.span(base - 11, cx - 1, cx + 1, BELL); b.span(base - 10, cx - 1, cx + 1, BELL)
    b.span(base - 9, cx - 2, cx + 2, BELL); b.span(base - 8, cx - 2, cx + 2, BELL); b.span(base - 7, cx - 3, cx + 3, BELL); b.put(cx, base - 6, BELL)
    b.put(cx - 1, base - 10, p['leaf']); b.put(cx - 1, base - 9, p['leaf']); b.put(cx - 2, base - 8, p['leaf'])

SEAM = 34         # dots from each end kept for the one symmetric house the mirrored repeat completes
# ---- back row of trees between the seam zones
back = [(x, 128 + (x * 7) % 5, 8) for x in range(SEAM + 2, W - SEAM, 10)] + [(x, 141 + (x * 5) % 4, 8) for x in range(SEAM + 7, W - SEAM, 10)]
b.crowns(back)
b.crowns([(96, 106, 9), (88, 112, 7), (105, 112, 7), (97, 116, 6)], trunk=(96, 118, 128))
b.crowns([(150, 122, 7), (160, 118, 7), (166, 125, 5)])
b.crowns([(258, 122, 6), (266, 117, 6)])
# ---- back houses
b.home(40, 30, 19, 106, BOTTOM, chimney=50, wins=[(46, 124), (60, 124)])
b.home(112, 22, 15, 118, BOTTOM, gable_right=False, wins=[(131, 133)])
bell_tower(214, 112)
b.home(170, 60, 21, 112, BOTTOM, chimney=184, wins=[(178, 133), (196, 133), (216, 133)])
b.home(258, 16, 19, 114, BOTTOM, wins=[(263, 134)])
# ---- middle trees and houses
b.crowns([(74, 140, 7), (66, 145, 6), (82, 146, 6)])
b.crowns([(250, 140, 7), (242, 146, 6), (260, 147, 6)])
b.home(86, 14, 13, 128, BOTTOM, gable_right=False, wins=[(100, 145)])
b.home(150, 14, 17, 126, BOTTOM, wins=[(154, 145)])
# ---- front houses
b.home(40, 20, 17, 137, BOTTOM, wins=[(44, 155), (52, 155)])
b.home(262, 18, 17, 138, BOTTOM, gable_right=False, wins=[(282, 155)])
# ---- front shrubs between the seam zones
for cx, cy, r in [(84, 162, 5), (110, 160, 6), (122, 162, 5), (136, 160, 6), (188, 161, 6), (200, 163, 5),
                  (232, 161, 6), (244, 163, 5)]:
    b.crowns([(cx, cy, r)])
# ---- the two ends: halves of one wide symmetric house each (hip roof), completed by the mirrored next copy
b.edge_house('left', SEAM - 2, 108, BOTTOM, roof_rows=9, wins=[(10, 128), (20, 128), (10, 140), (20, 140)], door=(3, 150))
b.edge_house('right', SEAM - 2, 112, BOTTOM, roof_rows=9, wins=[(10, 132), (20, 132), (10, 144), (20, 144)], door=(3, 152))
b.hedge(156, 31)
b.fill_bottom(146, p['leaf_d'])
b.close_pinholes()
# the bottom rows in one tone: the combat stages stretch the band's last row down to the floor
b.img[-3:, :, :3] = p['leaf_d']; b.img[-3:, :, 3] = 255
img = b.img

if __name__ == '__main__':
    band = Image.fromarray(img, 'RGBA').resize((W * 2, H * 2), Image.NEAREST)
    band.save('out/near-redraw.png')
    bg = Image.new('RGBA', band.size, (188, 214, 230, 255)); bg.alpha_composite(band)
    bg.crop((0, 160, 666, 334)).resize((1332, 348), Image.NEAREST).save('out/near-redraw-view.png')
    # the mirrored repeat: this copy and the next, to check the ends make one house
    m = Image.new('RGBA', (1332, 334)); m.alpha_composite(bg); m.alpha_composite(bg.transpose(Image.FLIP_LEFT_RIGHT), (666, 0))
    m.crop((466, 160, 866, 300)).resize((1200, 420), Image.NEAREST).save('out/near-seam.png')
