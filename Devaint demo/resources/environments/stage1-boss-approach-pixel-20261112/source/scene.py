"""Compose a 960x540 game-like screen from the adopted stage-1 art: paper sky, mountains/far/near village,
lawn tiles over a solid-cell grid (tile tops lifted 8px), the hero sprite, and any rear art layers."""
from pathlib import Path
from PIL import Image

WT = Path(r"C:/Users/SSAFY/Desktop/자율 프로젝트/03_개발/Worktrees/stage1-pixel-apply-20261112")
RES = WT / "resources/environments"
LAWN = RES / "stage1-pixel-lawn-wood-32/runtime"
NATIVE = RES / "stage1-pixel-background-native-20261111/runtime"
HERO = WT / "unity/Assets/Deviant/Combat/Resources/CharacterConfirmed/base_sword/F006.png"
PAPER = (244, 241, 233, 255)


def canonical(m):
    r = m & 15
    for i in range(4):
        if m & (1 << i) and m & (1 << ((i + 1) % 4)) and m & (16 << i): r |= 16 << i
    return r


SHAPES = sorted({canonical(m) for m in range(256)})
_atlas = None


def tile(mask, variant):
    global _atlas
    if _atlas is None: _atlas = Image.open(LAWN / "terrain-atlas.png").convert("RGBA")
    k = SHAPES.index(mask) * 4 + variant
    return _atlas.crop((k % 16 * 32, k // 16 * 32, k % 16 * 32 + 32, k // 16 * 32 + 32))


class Screen:
    """cells: list of strings, '#' solid; one char = 32px. The screen shows cols x rows cells from (0,0)."""
    def __init__(self, cells, w=960, h=540, oy=0):
        self.cells, self.w, self.h, self.oy = cells, w, h, oy
        self.im = Image.new("RGBA", (w, h), PAPER)

    def solid(self, x, y):
        y = max(0, min(y, len(self.cells) - 1)); row = self.cells[y]
        return row[max(0, min(x, len(row) - 1))] == '#'

    def layer(self, path, x, y, mirror_repeat=True):
        """paste a background band with its bottom-left at (x, y) screen px, repeating with mirrored copies"""
        im = Image.open(path).convert("RGBA")
        w = im.width; i = 0
        start = x - ((x // w) + 1) * w
        xx = start
        while xx < self.w:
            idx = round((xx - x) / w)
            piece = im.transpose(Image.FLIP_LEFT_RIGHT) if mirror_repeat and idx % 2 else im
            self.im.alpha_composite(piece, (xx, y - im.height)) if 0 <= y - im.height or True else None
            # fill below with the bottom row (continuous lower background)
            row = piece.crop((0, piece.height - 1, w, piece.height)).resize((w, max(0, self.h - y)))
            if self.h - y > 0: self.im.alpha_composite(row, (xx, y))
            xx += w

    def background(self, near_y=470, far_y=420, mount_y=400, near_x=-120, far_x=-60, mount_x=-200):
        self.layer(NATIVE / "mountains.png", mount_x, mount_y)
        self.layer(NATIVE / "far-village.png", far_x, far_y)
        self.layer(NATIVE / "near-village.png", near_x, near_y)

    def art(self, im, x, y):
        self.im.alpha_composite(im, (x, y))

    def terrain(self):
        dx = [0, 1, 0, -1, 1, 1, -1, -1]; dy = [-1, 0, 1, 0, -1, 1, 1, -1]
        for y in range(len(self.cells)):
            for x in range(len(self.cells[y])):
                if not self.solid(x, y): continue
                m = 0
                for i in range(8):
                    if self.solid(x + dx[i], y + dy[i]): m |= 1 << i
                mark = ((x * 73856093) ^ (y * 19349663)) & 0xffffffff; mark ^= mark >> 13
                self.im.alpha_composite(tile(canonical(m), mark % 4), (x * 32, y * 32 - 8 + self.oy))

    def hero(self, feet_x, feet_y, flip=False):
        im = Image.open(HERO).convert("RGBA")
        if flip: im = im.transpose(Image.FLIP_LEFT_RIGHT)
        px = 64 if not flip else 144 - 64
        self.im.alpha_composite(im, (feet_x - px, feet_y - 115))

    def save(self, p):
        self.im.convert("RGB").save(p)
