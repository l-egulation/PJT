# 발사기 스킬 몸 모션 에테르 프롬프트(2026-11-10). 인형 페이지(04_테스트/발사기 스킬 자세 설명)에서 사용자가 「이대로 가자」로 정한 자세.
# 지금 게임은 평타(볼)와 특수 넷이 모두 각 총의 attack_single(가볍게 한 번 쏨)을 써서 같은 그림이다.
# 총은 잉크마다 다르다: 모아쏘기 = 기본 총, 레이저 = 레이저 총, 샷건 = 산탄 총, 유탄 = 유탄 총. 입력 그림도 스킬마다 그 총을 든 대기 장이다.
# 게임 시간(data/combat/launcher_skills.json): 모아쏘기 준비 0.2 → 누르는 동안 0.2~0.8 모음 → 뗀 뒤 0.3 / 레이저 발사 0.15·끝 0.6 /
#   샷건 발사 0.15 → 반동 비행 0.31(뒤로 6칸, 높이 1칸, 게임이 옮김) → 끝 0.8 / 유탄 발사 0.2·끝 1.0
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

SIZE LOCK: the character and the gun keep exactly the same on-screen size and the same feet position as in the supplied image in every frame. The first and the last frame must match the supplied image in size and position. Never zoom in, scale up or re-frame the character, even for small poses such as crouching, kneeling or leaning back.

BODY RULES (every frame): exactly two arms and two small round black hands, one at the end of each arm. Never draw a third hand, an extra fist, a floating hand or a duplicated arm. The gun is held with BOTH hands the whole time and is never let go, never thrown and never duplicated. The head always faces RIGHT in side view: the black face oval with both yellow eye slits stays visible on the right side of the hood in every frame. Never turn the head toward the viewer, away from the viewer, or show the back of the hood; the torso may twist, lean and tilt, but the head keeps looking forward to the right. The gun always points to the RIGHT (forward), never backward.

ACTING: the motion must be dynamic and readable at small size: a clear anticipation, a strong recoil at the shot, a clear weight shift with bent knees and a body lean, overshoot and follow-through, and the scarf tails whipping with the motion. Avoid stiff upright poses; most frames must look clearly different from the idle pose. This skill must have its OWN silhouette and must NOT look like the plain light standing shot of the basic attack.

EYES: the two eyes are always the same two small vertical yellow slits as in the supplied image. Never squint, slant, close or reshape them (no angry, sleepy or blinking eyes).
"""

GUN = {
 'launcher': "the big black (#1B1616) launcher tube with a yellow (#EBA516) ring band, held across the hips",
 'launcher_laser': "the big black (#1B1616) laser cannon with a ribbed muzzle, a yellow (#EBA516) ring band and a small yellow light at the muzzle, held across the hips",
 'launcher_shotgun': "the big dark grey-black (#1B1616) shotgun with a boxy ribbed fore-grip and a yellow (#EBA516) band, held across the hips",
 'launcher_grenade': "the long black (#1B1616) grenade launcher with a yellow (#EBA516) band and a trigger guard under it, held across the hips",
}
def gun_text(g):
    return (f"Use the supplied image for the exact character and gun design and the initial/final idle pose ONLY. The gun is part of the character art: {GUN[g]}. "
            "Keep its exact shape, length, thickness and colors in every frame, including the dark grey shading on the gun; never redesign, shorten, lengthen, recolor or duplicate it. "
            "Draw NO bullet, ball, beam, laser, pellet, grenade, muzzle flash, smoke, sparks or shell casing: the game draws every shot and effect separately. "
            "Use the entire generated clip for one complete action; leave idle immediately and return to the exact idle pose only at the end. No long idle padding.")

TAIL = """Keep the camera locked in orthographic side view, facing right, with no horizontal travel, zoom or crop. The character stays at the same horizontal spot on screen for the whole clip (the game moves the character; the animation does not). Keep the whole body and the gun inside the frame at all times. Preserve the character's size, hood, robe panels, narrow black trousers and exactly two scarf tails; the tails react to the motion, then settle. NO waist belt, buckle, black skirt or added outline. Colors: black #1B1616, robe #FDEEDA, robe shadow #D2B396, scarf #025673 and #02648C, eyes and existing accents #EBA516, plus the gun's own dark greys from the supplied image. Preserve the reference raster/sprite style. Pure flat white background, no floor, shadow, enemy, target, VFX, explosion, smoke, sparks, fire, motion lines, afterimages, text or checkerboard. Return one continuous animation, not a contact sheet."""

SKILLS = {
'01-charge-shot': ('launcher', """CHARGE SHOT - the character braces low and wide, holds still while charging a big shot (trembling with effort), then fires and is knocked back by a heavy recoil.

1. BRACE: from the idle pose, step the feet wide apart (front foot forward, back foot back), bend both knees deep and lean the body slightly forward, pulling the gun in tight against the hip with both hands, the muzzle pointing straight forward at waist height.
2. CHARGE HOLD: hold this low braced pose for several frames with only a small tense tremble of the arms and shoulders (the body stays low and wide, the gun stays level). These frames will be looped while the button is held, so they must look almost the same.
3. FIRE AND RECOIL: the gun kicks up and back hard: the whole body is thrown backward, leaning far back with the chest open, the gun muzzle tilted up about 25 degrees, the front foot sliding forward and the back leg bracing behind; the scarf tails fly forward over the shoulders.
4. CATCH: settle back onto the back foot, bring the gun level again.
5. RECOVER: return to the exact idle pose.

Required readable silhouettes: the wide low brace with the gun at the hip, the near-identical charge hold frames, the big backward recoil lean with the gun kicked up."""),
'02-laser-beam': ('launcher_laser', """GUN LOCK (v3, most important for this skill): the cannon is NOT animated. In EVERY frame it is the exact same laser cannon as in the supplied image, copied pixel for pixel: the same short, very fat black tube with one yellow ring band near the back, black ribbed rings at the front and a small round yellow light inside the muzzle, the same size, the same angle (level, pointing straight right) and the same height at the hips as in the supplied image. Treat the cannon as a fixed sticker that only slides a little left or right with the body; never lift it, tilt it, rotate it, redraw it, lengthen it, or replace it with any other gun (no rifle, bazooka, blaster or shotgun). Only the body, legs, head and scarf move.

LASER BEAM - holding the cannon level at the hips exactly as in the idle pose, the character fires a long beam and is pushed back by it, straining for a long moment.

1. BRACE: from the idle pose, step the front foot forward into a wide stance and bend both knees; the cannon stays level at the hips.
2. FIRE: a short sharp jolt of the shoulders backward the instant it fires; the cannon stays level.
3. PUSHED BACK AND HOLDING: the upper body and head lean back against the push of the beam, the back leg bracing hard, the knees bent, the scarf tails and the hood tip streaming forward past the body; keep this straining pose for many frames with a small shaking tremble of the shoulders. The cannon stays level at the hips the whole time.
4. RELEASE: the strain ends, stand up straight.
5. RECOVER: return to the exact idle pose.

Required readable silhouettes: the wide braced stance, the long backward-leaning straining hold with the scarf streaming forward, and the cannon always identical and level at the hips."""),
'03-shotgun-blast-jump': ('launcher_shotgun', """SHOTGUN BLAST JUMP - the character fires the shotgun forward from a low crouch and the huge recoil throws the whole body backward through the air; it lands in a low skid.

1. CROUCH AIM: from the idle pose, drop into a low forward-leaning crouch, the shotgun held at the hip pointing straight forward and slightly down.
2. FIRE: the shotgun kicks hard.
3. BLOWN BACK (airborne): the body is thrown backward off the ground: leaning far back almost lying in the air, both legs swinging forward and up off the ground, knees bent, the scarf tails and hood tip flying forward, the shotgun still held with both hands and still pointing forward and a little up. Hold this airborne blown-back pose for a few frames (the game moves the body backward through the air).
4. LANDING SKID: land on both feet in a deep low crouch leaning forward, one foot ahead of the other as if skidding, the shotgun held low in front.
5. RECOVER: rise back to the exact idle pose.

The feet leave the ground only in the airborne part; the character stays at the same horizontal spot on screen (do not move it left or right). Required readable silhouettes: the low crouch aim, the airborne backward lean with the legs swung forward, the deep landing skid."""),
'04-grenade-kneel-reload': ('launcher_grenade', """GRENADE KNEEL AND RELOAD - the character drops to one knee, tilts the launcher up and fires a lobbed grenade, then reloads it with a pump action before standing up.

1. KNEEL: from the idle pose, drop to one knee: the BACK knee touches the ground, the front foot planted forward with the front knee bent up; tilt the launcher up so the muzzle points up and forward about 30-40 degrees, both hands on it.
2. FIRE: the launcher kicks up a little more and the shoulders jerk back.
3. LOWER: bring the launcher back down to level, still kneeling.
4. PUMP RELOAD: still kneeling, the FRONT hand slides the fore-grip of the launcher back toward the body and then forward again (a clear "clack-clack" pump action), the back hand on the grip; the body gives a small nod with each pump.
5. STAND: stand up from the kneel.
6. RECOVER: return to the exact idle pose.

Required readable silhouettes: the one-knee kneel with the launcher tilted up, the pump-back and pump-forward of the front hand along the barrel, the stand-up."""),
}

for name, (gun, body) in SKILLS.items():
    text = '\n'.join([HEAD, body, '', gun_text(gun), '', TAIL]) + '\n'
    (OUT / f'{name}-prompt-en.txt').write_text(text, encoding='utf-8')
print(sorted(p.name for p in OUT.glob('*-prompt-en.txt')))
