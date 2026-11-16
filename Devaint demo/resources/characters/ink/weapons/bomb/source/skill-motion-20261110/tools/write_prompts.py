# 폭탄 스킬 몸 모션 에테르 프롬프트(2026-11-09). 수리검 v4·레이피어와 같은 머리말(CHARACTER/SIZE/BODY/ACTING/EYES)에 스킬 문단을 붙인다.
# 지금 게임은 폭탄 스킬 4개가 평타 던지기(bomb/attack1)를 그대로 써서 다섯 동작이 같은 그림이다 → 스킬마다 실루엣이 다른 몸 모션을 새로 뽑는다.
from pathlib import Path
OUT = Path(__file__).resolve().parent.parent / '1_프롬프트·입력'; OUT.mkdir(exist_ok=True)

HEAD = """CHARACTER LOCK (most important rule, applies to every frame): this is the SAME existing character as in the supplied image, not a new design. Copy the character exactly from the supplied image and only change the pose.
- Small chibi proportions exactly as in the image: the hooded head is about as tall as the whole body below it. Do not make the character taller, older, slimmer, more realistic or more detailed.
- Hood: plain cream (#FDEEDA) hood with tan (#D2B396) shading and one soft pointed tip falling to the back. No ears, horns, hair, mask, hat or extra folds.
- Face: a solid black (#1B1616) oval face inside the hood with ONLY two small vertical yellow (#EBA516) eye slits. No mouth, nose, pupils, eyebrows, eye whites or skin.
- Scarf: one blue scarf (#025673 / #02648C) around the neck with one small square yellow clasp at the front, and exactly two scarf tails flowing behind.
- Robe: a simple cream knee-length robe with tan side shading, small round black hands, narrow black trousers, short yellow boots with flat black soles.
- Same pixel size, same pixel density, same flat colors and same blocky sprite look as the supplied image in every frame. Never repaint the character in a smoother, painterly, anime or 3D style, never add gradients, highlights, textures, outlines or new colors, and never change the face, clothing, colors or proportions between frames.
If a pose is hard, simplify the pose; never change the character's look to make it easier.

SIZE LOCK: the character and the bomb keep exactly the same on-screen size and the same feet position as in the supplied image in every frame. The first and the last frame must match the supplied image in size and position. Never zoom in, scale up or re-frame the character, even for small poses such as crouching or kneeling.

BODY RULES (every frame): exactly two arms and two small round black hands, one at the end of each arm. Never draw a third hand, an extra fist, a floating hand or a duplicated arm. The head always faces RIGHT in side view: the black face oval with both yellow eye slits stays visible on the right side of the hood in every frame. Never turn the head toward the viewer, away from the viewer, or show the back of the hood; the torso may twist, lean and tumble, but the head keeps looking forward to the right.

ACTING: the motion must be dynamic and readable at small size: a strong anticipation, full extension at the key moment, a clear weight shift with bent knees and a body lean, overshoot and follow-through, and the scarf tails whipping with the motion. Avoid stiff upright poses; most frames must look clearly different from the idle pose. This skill must have its OWN silhouette and must NOT look like the plain one-arm overhand bomb toss of the basic attack.

EYES: the two eyes are always the same two small vertical yellow slits as in the supplied image. Never squint, slant, close or reshape them (no angry, sleepy or blinking eyes).
"""

BOMB = """Use the supplied image for the exact character and bomb design and the initial/final holding pose ONLY. The bomb is part of the character art: one round black (#1B1616) bomb with a small yellow (#EBA516) cap and a short black curled fuse on top, held in the rear hand at hip height. Keep its shape and colors; never redesign, recolor or duplicate it unless this prompt says so. Once a bomb LEAVES the hand it is NOT drawn flying (the game draws the projectile separately): after a release there is NO bomb anywhere in the picture, not flying, not on the ground, not as a small dot. The space in front of the character stays empty white."""

TAIL = """Keep the camera locked in orthographic side view, facing right, with no horizontal travel, zoom or crop. The character stays at the same horizontal spot on screen for the whole clip (the game moves the character; the animation does not). Keep the whole body and the bomb inside the frame at all times. Preserve the character's size, hood, robe panels, narrow black trousers and exactly two scarf tails; the tails react to the motion, then settle. NO waist belt, buckle, black skirt or added outline. Colors: black #1B1616, robe #FDEEDA, robe shadow #D2B396, scarf #025673 and #02648C, eyes and existing accents #EBA516. Preserve the reference raster/sprite style. Pure flat white background, no floor, shadow, enemy, target, VFX, explosion, smoke, sparks, fire, motion lines, afterimages, text or checkerboard. Return one continuous animation, not a contact sheet."""

FULL = "Use the entire generated clip for one complete action; leave idle immediately and return to the exact holding pose only at the end. No long idle padding."

import sys; sys.path.insert(0, str(Path(__file__).resolve().parent))
from skills_v2 import SKILLS, BOMB_KICK   # v2(11-09). v1 문단은 1_프롬프트·입력/v1/ 에 결과 파일로 보존

for name, body in SKILLS.items():
    end = '' if name in ('03-missile-launch',) else FULL
    text = '\n'.join([HEAD, body, '', BOMB + ' ' + end, '', TAIL]) + '\n'
    (OUT / f'{name}-prompt-en.txt').write_text(text, encoding='utf-8')
print(sorted(p.name for p in OUT.glob('*-prompt-en.txt')))
