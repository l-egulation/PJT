import numpy as np
from PIL import Image
from scene import Screen
from colorize import SKY

import sys
C = "out/color5/"
SKYC = (188, 214, 230)
def frame(t, colour=True):
    cells = ['.' * 31] * 10 + ['.' * 6 + '#' * 6 + '.' * 19] + ['.' * 31] * 3 + ['#' * 31] * 3
    sc = Screen(cells); sc.im = Image.new("RGBA", (960, 540), SKYC + (255,))
    # clouds behind everything, drifting right
    for i, (x, y) in enumerate(((60, 60), (420, 110), (760, 40), (250, 190))):
        c = Image.open(C + f"sky/clouds/clouds-{i}.png")
        xx = int((x + t * 6 * (1 + i % 2)) % 1160) - 200
        sc.im.alpha_composite(c, (xx, y))
    for name, x, y in (("mountains", -200, 400), ("far-village", -60, 420), ("near-village", -120, 448)):
        im = Image.open(C + f"{name}.png"); w = im.width; xx = x - w
        while xx < 960:
            idx = round((xx - x) / w); piece = im.transpose(Image.FLIP_LEFT_RIGHT) if idx % 2 else im
            sc.im.alpha_composite(piece, (xx, y - im.height))
            row = piece.crop((0, piece.height - 1, w, piece.height)).resize((w, 540 - y)); sc.im.alpha_composite(row, (xx, y)); xx += w
    # birds fly left with a flap cycle 0,1,2,1
    for i, (x, y) in enumerate(((900, 80), (700, 150), (1100, 120))):
        f = [0, 1, 2, 1][(t + i) % 4]
        b = Image.open(C + f"sky/birds/birds-{[2, 4, 1][i]}-{f}.png").resize((64, 32), Image.NEAREST)
        sc.im.alpha_composite(b, (int(x - t * 9) % 1100 - 70, y))
    sc.terrain(); sc.hero(420, 448)
    return sc.im.convert("RGB")

if __name__ == "__main__":
    shots = []
    for sky in ((188, 214, 230), (214, 232, 238)):
        SKYC = sky; shots.append(frame(0))
    s = Image.new("RGB", (968 * 2 - 8, 540), "white"); s.paste(shots[0], (0, 0)); s.paste(shots[1], (968, 0)); s.save("out/color5-scene.png")
    SKYC = (188, 214, 230)
    frames = [frame(t) for t in range(0, 48)]
    small = [f.resize((480, 270), Image.NEAREST) for f in frames]
    frames[0].save("out/color5-motion.gif", save_all=True, append_images=frames[1:], duration=100, loop=0)
