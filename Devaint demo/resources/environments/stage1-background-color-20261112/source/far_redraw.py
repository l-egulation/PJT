"""Far village band redrawn in 2x2 dots (222 x 111 -> 444 x 222), after the user's colour design: two towers (a
domed one and one with a cross), pointed cypresses, a back row of roofs, a front row of houses and round trees.
Misty, low-contrast tones of the design; every slope 1:1 (cypress tips 1:2). Trees are separate round clumps,
each end is half of a symmetric house that the mirrored next copy completes, walls run to the bottom and a back
row of trees closes the gaps."""
from PIL import Image
from village_px import Band

PAL = dict(wall=(229, 219, 199), wall_sh=(220, 208, 189), eave=(214, 202, 184), roof=(215, 195, 181), roof_sh=(205, 184, 171),
           win=(189, 206, 193), trunk=(178, 196, 184), leaf_l=(208, 210, 194), leaf=(189, 206, 193), leaf_d=(178, 196, 184))
b = Band(222, 111, PAL)
p = PAL
import random
SEAM = 30          # dots from each end kept for the one symmetric house the mirrored repeat completes

def tree_row(y, seed, gap=(6, 10), r=(4, 7), jitter=3):
    """an uneven row of tree clumps between the seam zones (the edge houses stand there)"""
    rnd = random.Random(seed); x = SEAM + rnd.randint(0, 4); out = []
    while x < b.w - SEAM:
        out.append((x, y + rnd.randint(-jitter, jitter), rnd.randint(*r))); x += rnd.randint(*gap)
    out.sort(key=lambda c: c[1])
    b.crowns(out)

# back: cypresses and towers well inside the band
b.cypress(46, 62); b.cypress(112, 62); b.cypress(186, 70)
b.rect(60, 58, 72, 110, p['wall']); b.rect(70, 58, 72, 110, p['wall_sh'])            # domed tower
for i, (a_, z) in enumerate([(63, 69), (61, 71), (60, 72), (59, 73), (59, 73)]): b.span(51 + i, a_, z, p['roof'])
b.span(56, 58, 74, p['roof']); b.span(57, 60, 72, p['eave'])
b.windows(65, 62, 1, 4)
b.rect(153, 66, 163, 110, p['wall']); b.rect(161, 66, 163, 110, p['wall_sh'])          # cross tower
b.put(158, 54, p['roof']); b.span(55, 157, 159, p['roof']); b.put(158, 56, p['roof'])
for i in range(6): b.span(57 + i, 158 - 1 - i, 158 + 1 + i, p['roof'])
b.span(63, 151, 165, p['roof']); b.span(64, 153, 163, p['eave']); b.span(65, 153, 163, p['eave'])
b.windows(157, 69, 1, 3)
# back row of roofs
b.hip(34, 56, 64, 69, 110)
b.hip(78, 104, 64, 70, 110, wins=[(88, 73)])
b.hip(118, 146, 68, 73, 110, wins=[(128, 76)])
b.hip(168, 188, 70, 75, 110, wins=[(176, 78)])
# trees in front of the back walls
tree_row(83, 7)
tree_row(96, 11, gap=(8, 13))
# middle row of low houses
b.home(36, 16, 11, 81, 110, wins=[(40, 90)], win_h=2)
b.home(84, 14, 11, 82, 110, gable_right=False, wins=[(98, 91)], win_h=2)
b.home(124, 16, 13, 81, 110, gable_right=False, wins=[(140, 91)], win_h=2)
b.home(168, 14, 11, 84, 110, wins=[(172, 93)], win_h=2)
# front row of trees
tree_row(106, 23, r=(5, 7), jitter=2)
# the two ends: halves of one wide symmetric house each (hip roof), in front of the trees, completed by the
# mirrored next copy; nothing else stands in the seam zones
b.edge_house('left', SEAM - 2, 74, 110, roof_rows=7, wins=[(10, 86), (17, 86), (10, 94), (17, 94)], door=(3, 92))
b.edge_house('right', SEAM - 2, 76, 110, roof_rows=7, wins=[(10, 88), (17, 88), (10, 96), (17, 96)], door=(3, 94))
b.hedge(104, 37, r=(5, 7), gap=(6, 9))
b.fill_bottom(86, p['leaf_d'])
b.close_pinholes()
# the bottom rows in one tone: the combat stages stretch the band's last row down to the floor
b.img[-3:, :, :3] = p['leaf_d']; b.img[-3:, :, 3] = 255

if __name__ == '__main__':
    im = Image.fromarray(b.img, 'RGBA').resize((444, 222), Image.NEAREST)
    im.save('out/far-redraw.png')
    bg = Image.new('RGBA', im.size, (188, 214, 230, 255)); bg.alpha_composite(im)
    bg.crop((0, 90, 444, 222)).resize((1332, 396), Image.NEAREST).save('out/far-redraw-view.png')
