"""Prepare exact-pixel Aether input. No image API, combat or Unity writes."""
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = 'monsters/stage-02/boss-phase3-attacks-20261106'
DEST = ROOT / GROUP
SIZE = ROOT / 'monsters/stage-02/boss-motion-size-review-20261106'
CUT = ROOT / 'monsters/stage-02/boss-phase2-cutscene-20261106'


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def body_bounds(im):
    """Largest opaque connected component; measurement only, never deletes pixels."""
    alpha = im.getchannel('A')
    points = {(x, y) for y in range(im.height) for x in range(im.width) if alpha.getpixel((x, y)) == 255}
    components = []
    while points:
        todo = [points.pop()]
        part = []
        while todo:
            x, y = todo.pop()
            part.append((x, y))
            for p in [(x-1, y), (x+1, y), (x, y-1), (x, y+1)]:
                if p in points:
                    points.remove(p)
                    todo.append(p)
        components.append(part)
    largest = max(components, key=len)
    return [min(x for x, y in largest), min(y for x, y in largest), max(x for x, y in largest)+1, max(y for x, y in largest)+1]


PROMPT = """Create one non-looping body animation for a 2D side-scrolling pixel action game, using the supplied image as the exact first frame and identity reference. This is the approved fused Blue boss, not a redesign.

Locked camera, fixed framing, pure white #FFFFFF background throughout. Keep the same facing direction as the reference. Preserve the exact hood, rigid pale mask, blue and violet robe, crossed fabric bands, ONE existing arm and its normal-sized hand, wrapped legs, boots, palette and body proportions. Preserve the clear violet magical crossbow emblem floating above the head, with its existing shape and spacing; keep it stable and attached to the boss's head position. It is an energy emblem, not metal hardware. Keep the same crisp unoutlined pixel blocks and limited shading.

CRITICAL IDENTITY LOCK — MASK: the pale face is a rigid inanimate mask, never a human face. Keep its exact reference silhouette, surface markings and eye openings in every frame. No mouth, lips, teeth, tongue, nostrils, jaw articulation, talking, screaming, blinking or facial expressions. Do not reinterpret any existing mask shading as a mouth or animate it. The mask may move only as one rigid piece with the head.

CRITICAL ANATOMY LOCK — EXACTLY ONE ARM: this character has ONLY the single arm visible on the LEFT SIDE OF THE PROVIDED IMAGE, whose pale hand is raised beside the mask. The opposite arm is anatomically ABSENT, not hidden behind the body or under the robe. Never generate, reveal, regrow, imply or animate a second arm, elbow, forearm, wrist, hand, fingers or limb-shaped bulge, even inside a sleeve or as a shadow. Preserve the reference silhouette and flat robe drape on the missing-arm side. Do not create a stump or new costume opening. All casting is performed by the ONE existing visible arm only. Do not mirror the arm or add black outlines or enlarged limbs.

Action: a readable, restrained charge followed by a fast, weighty directional palm release for ONE homing magic arrow. Animate the character only. The arrow, charge glow, trails, magic circles, particles, hit and explosion effects will be composited separately; do not draw them into this clip. The existing eye-violet accents and overhead crossbow emblem remain, without additional effects.

Use the exposed pale hand that is ALREADY RAISED BESIDE THE MASK on the LEFT SIDE OF THE PROVIDED IMAGE, emerging from the violet-lined hanging sleeve. This is the character's ONLY hand and ONLY casting arm. Animate this same existing arm throughout. The opposite side remains armless in every frame; do not invent a hidden hand or a supporting arm for balance.

One action, approximately 1.8 seconds:
0.00-0.15 s: match and hold the supplied idle pose.
0.15-0.75 s: slowly draw that already-raised hand slightly inward toward the upper chest, flexing its elbow. Set the shoulder and use only a small backward torso weight shift; do not change body scale. Keep the mask facing the same direction. Make the preparation easy to read.
0.75-0.90 s: hold the charged anticipation pose, creating a brief visual pause.
0.90-1.00 s: snap that SAME hand outward toward the LEFT EDGE OF THE IMAGE in one short, decisive elbow extension, opening its palm toward the target. The release gesture finishes at 1.00 s. A restrained shoulder follow-through makes it weighty without a large punch or lunge.
1.00-1.20 s: let only the sleeve and robe settle slightly while the arm remains extended.
1.20-1.80 s: hold the final extended-palm casting pose still. Do not lower the hand. Do not return to idle. Do not loop or repeat the attack.

Both boots stay planted on the same ground line and at the same horizontal positions throughout. Natural knee and torso articulation is allowed, without sliding, scaling or deforming the boots. No walking, stepping, jumping, hovering, spinning, camera motion, zoom, extra characters or added props. Keep the full character and complete overhead crossbow within the generous white margins. Use stepped pixel movement rather than smooth vector rendering or motion blur. Do not interpolate into a different character design.
"""


def main():
    if DEST.exists():
        raise SystemExit('Refusing to overwrite an existing handoff group.')
    source = DEST / 'source'
    assets = DEST / 'preview/assets'
    assets.mkdir(parents=True)
    source.mkdir()
    refs = []
    table = json.loads((SIZE / 'runtime/motions.json').read_text(encoding='utf-8'))
    for key in ['fused-idle', 'fused-arrival', 'blue-roar', 'player']:
        motion = next(m for m in table['motions'] if m['id'] == key)
        frame = motion['frames'][0]
        original = SIZE / frame['file']  # JSON paths are relative to group root.
        assert digest(original) == frame['sha256'], key
        target = assets / f'{key}.png'
        shutil.copy2(original, target)
        im = Image.open(target).convert('RGBA')
        box = list(im.getchannel('A').getbbox())
        assert box == frame['bounds']
        body = body_bounds(im)
        refs.append({'id': key, 'file': f'preview/assets/{key}.png', 'source': original.relative_to(ROOT).as_posix(), 'sha256': digest(target), 'canvas': list(im.size), 'pivot': motion['pivot'], 'sourceScalePercentAlreadyBaked': motion['sourceScalePercent'], 'additionalScalePercent': 100, 'fullBounds': box, 'bodyBoundsLargestOpaqueComponent': body})
    shutil.copy2(CUT / 'preview/assets/room.png', assets / 'room.png')
    # The reference renderer covers a two-row leftover violet effect using intact floor pixels.
    # Preserve room.png verbatim and apply that same correction only in the HTML canvas.
    im = Image.open(assets / 'fused-idle.png').convert('RGBA')
    shutil.copy2(assets / 'fused-idle.png', source / 'approved-fused-idle-original.png')
    white = Image.new('RGBA', (512, 384), 'white')
    white.alpha_composite(im, (64, 64))
    start = white.convert('RGB').resize((1536, 1152), Image.Resampling.NEAREST)
    start.save(source / 'homing-start-white-3x.png')
    # Validate every 3x3 block, so white preparation never redraws reference pixels.
    assert start.resize((512, 384), Image.Resampling.NEAREST).tobytes() == white.convert('RGB').tobytes()
    palette = sorted({pixel[:3] for pixel in im.get_flattened_data() if pixel[3] == 255})
    dump(source / 'approved-fused-palette.json', {'source': 'approved-fused-idle-original.png', 'sourceSha256': digest(source / 'approved-fused-idle-original.png'), 'extraction': 'unique RGB of alpha==255 only, before white compositing', 'colors': [list(c) for c in palette]})
    (source / 'homing-aether-prompt.txt').write_text(PROMPT, encoding='utf-8')
    dump(source / 'handoff.json', {
        'format': 'stage2-phase3-aether-handoff-v1', 'status': 'input-prepared-motion-not-generated-not-unity-integrated',
        'mainCommit': '7f2fbe8ed', 'resourceCommit': '5490d3b741cf6416aa01ba136fe240e884f6fce6',
        'mainPinnedResources': 'faa32eab7ea6233d69c6aa6c2d2f1454273dad14',
        'referencePathBase': 'resource repository root', 'references': refs,
        'input': {'file': 'source/homing-start-white-3x.png', 'canvas': [1536, 1152], 'background': '#FFFFFF', 'interpolation': 'nearest-neighbor', 'integerScale': 3, 'sourceCanvas': [384, 256], 'sourcePivot': [192, 232], 'sourcePasteOffsetLogical': [64, 64], 'inputPivot': [768, 888], 'inputGroundY': 888, 'sourceScalePercentAlreadyBaked': 100, 'autoFitPerFrame': False, 'opaquePixelsRepainted': False},
        'endImage': None, 'endImageReason': 'Let Aether articulate the existing casting arm naturally; the prompt holds the final extended pose. No forced arrival/roar end pose.',
        'timelineProposalMs': {'idle': [0, 150], 'prepare': [150, 750], 'anticipationHold': [750, 900], 'release': [900, 1000], 'clothSettle': [1000, 1200], 'finalHold': [1200, 1800]},
        'runtimeContract': {'HomingTellSeconds': 1, 'releaseAtSeconds': 1, 'bodyClipIsNotProjectileLifetime': True, 'combatTuningChanged': False},
        'aetherSourceSheet': None, 'selectedSourceFrames': None, 'playbackSequence': None, 'perFrameDurationsMs': None,
        'futureMatteProcessing': {'status': 'not-applied-no-new-frames-yet', 'reference': 'monsters/stage-02/boss-phase2-cutscene-20261106/preview/bridge/motion.json#matteProcessing', 'copyScaleOffsetsOrFootRowsFromPreviousClip': False, 'classifyBackgroundAtOriginalResolutionBeforePaletteMapping': True, 'alphaPolicyBody': [0, 255]},
        'generation': {'mode': 'deterministic-approved-pixel-reuse', 'imageApiCalled': False},
        'room': {'source': 'monsters/stage-02/boss-phase2-cutscene-20261106/preview/assets/room.png', 'sha256': digest(assets / 'room.png'), 'display': 'reference-renderer two-row floor repair on canvas only'},
        'review': {'logicalScreen': [960, 540], 'playerVisibleHeight': 64, 'bossScale': 1, 'backgrounds': ['white', 'gray', 'black', 'approved-map'], 'scope': 'asset-review HTML/composite, not Unity capture'}
    })
    print(json.dumps(refs, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
