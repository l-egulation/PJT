"""Cancel the vertical jump travel baked into weapon and village body jump frames by moving each frame's sprite pivot.

The game moves the body physically, so a jump frame must keep the feet on the pivot line as base_sword's frames do;
travel drawn into the picture adds a second, frame-stepped jump on top of the smooth physical one. Feet are found by
the boot colour. base_sword's per-frame boot offsets (feet tucked ~3 px while airborne) are the target pose; whatever a
form lifts beyond them is baked travel, and that frame's pivot moves up by the same amount (frame 1 keeps its pivot).
Only the Unity .meta pivots change; the approved PNG bytes stay as delivered.

    python tools/combat/pin_jump_feet.py          # rewrite pivots
    python tools/combat/pin_jump_feet.py --check  # fail if any pivot is stale
"""
import re
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
JUMP = ROOT / 'unity/Assets/Deviant/Combat/Resources/ApprovedTraversal/jump'
DRAWN_IN_PLACE = {'base_sword', 'whip'}  # reference pose / already without travel
VILLAGE_BODIES = (
    ROOT / 'unity/Assets/Deviant/Combat/Resources/UnarmedBody',
    ROOT / 'unity/Assets/Deviant/Hub/Resources/Hub/opening/human-hero',
)


def boot_bottom(png):
    image = Image.open(png).convert('RGBA'); width, height = image.size; pixels = image.load(); bottom = -1
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a > 200 and r > 190 and 110 < g < 215 and b < 90: bottom = y
    assert bottom >= 0, f'no boots found in {png}'
    return bottom


def frames(form):
    return sorted((JUMP / form).glob('*.png'))


def lifts(form):
    bottoms = [boot_bottom(png) for png in frames(form)]
    return [bottoms[0] - b for b in bottoms]


def pin_frame(png, want, check):
    meta = png.with_suffix('.png.meta'); text = meta.read_text(encoding='utf-8')
    m = re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)\}', text)
    if abs(float(m.group(2)) - want) <= 1e-6: return 0
    if not check:
        meta.write_text(text[:m.start()] + f'spritePivot: {{x: {m.group(1)}, y: {want}}}' + text[m.end():], encoding='utf-8', newline='\n')
    return 1


def main():
    check = '--check' in sys.argv
    pose = lifts('base_sword')
    stale = 0
    for form in sorted(p.name for p in JUMP.iterdir() if p.is_dir()):
        if form in DRAWN_IN_PLACE: continue
        pngs, lift = frames(form), lifts(form)
        assert len(pngs) == len(pose), form
        first = re.search(r'spritePivot: \{x: [^,]+, y: ([^}]+)\}', pngs[0].with_suffix('.png.meta').read_text(encoding='utf-8'))
        height = Image.open(pngs[0]).size[1]
        base = float(first.group(1)) * height
        for i in range(1, len(pngs)):
            want = (base + lift[i] - pose[i]) / height
            stale += pin_frame(pngs[i], want, check)
    # The human/opening and ink/village drawings also contain a full jump arc.
    # Keep the natural 0..3 pixel boot tuck; physical movement supplies the height.
    for body in VILLAGE_BODIES:
        idle = body / 'idle/F01.png'
        baseline = boot_bottom(idle)
        first = re.search(r'spritePivot: \{x: [^,]+, y: ([^}]+)\}', idle.with_suffix('.png.meta').read_text(encoding='utf-8'))
        height = Image.open(idle).size[1]
        base = float(first.group(1)) * height
        airborne = [body / f'jump/F{i:02}.png' for i in range(4, 10)]
        airborne += [body / f'fall-land/F{i:02}.png' for i in range(1, 6)]
        for png in airborne:
            want = (base + max(0, baseline - boot_bottom(png) - 3)) / height
            stale += pin_frame(png, want, check)
    print(('stale pivots: ' if check else 'updated pivots: ') + str(stale))
    if check and stale: sys.exit(1)


if __name__ == '__main__':
    main()
