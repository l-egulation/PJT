# 레이피어 평타 세 프롬프트. 공통 문단(CHARACTER LOCK·SIZE LOCK·BODY RULES·ACTING·끝 규칙)은 수리검 v4 를 레이피어에 맞게 고친 것.
from pathlib import Path
OUT = Path(__file__).resolve().parent.parent / '1_프롬프트·입력'
OUT.mkdir(exist_ok=True)
HEAD = """CHARACTER LOCK (most important rule, applies to every frame): this is the SAME existing character as in the supplied image, not a new design. Copy the character exactly from the supplied image and only change the pose.
- Small chibi proportions exactly as in the image: the hooded head is about as tall as the whole body below it. Do not make the character taller, older, slimmer, more realistic or more detailed.
- Hood: plain cream (#FDEEDA) hood with tan (#D2B396) shading and one soft pointed tip falling to the back. No ears, horns, hair, mask, hat or extra folds.
- Face: a solid black (#1B1616) oval face inside the hood with ONLY two small vertical yellow (#EBA516) eye slits. No mouth, nose, pupils, eyebrows, eye whites or skin.
- Scarf: one blue scarf (#025673 / #02648C) around the neck with one small square yellow clasp at the front, and exactly two scarf tails flowing behind.
- Robe: a simple cream knee-length robe with tan side shading, small round black hands, narrow black trousers, short yellow boots with flat black soles. The robe hangs straight from the scarf to the knees: NO belt, sash, rope, band or dark line around the waist, ever.
- Same pixel size, same pixel density, same flat colors and same blocky sprite look as the supplied image in every frame. Never repaint the character in a smoother, painterly, anime or 3D style, never add gradients, highlights, textures, outlines or new colors, and never change the face, clothing, colors or proportions between frames.
If a pose is hard, simplify the pose; never change the character's look to make it easier.

RAPIER LOCK: the rapier is part of the character art. It is the thin straight black (#1B1616) blade with a small black cross guard and grip exactly as in the supplied image. It is ALWAYS held in the same hand that holds it in the supplied image: never switch the sword to the other hand, never thrust with the empty hand, and never show the sword in both hands. Keep its length, thickness and shape in every frame: never make it thicker, wider, shorter, longer, curved, glowing or colored, never turn it into a broadsword, spear or stick, and never draw a second sword. The blade stays a clean straight line.

SIZE LOCK: the character and the rapier keep exactly the same on-screen size and the same feet position as in the supplied image in every frame. The first and the last frame must match the supplied image in size and position. Never zoom in, scale up or re-frame the character, even for low lunging poses.

BODY RULES (every frame): exactly two arms and two small round black hands, one at the end of each arm. Never draw a third hand, an extra fist, a floating hand or a duplicated arm. The rapier stays in its one sword hand for the whole clip; the other hand stays empty. The head always faces RIGHT in side view: the black face oval with both yellow eye slits stays visible on the right side of the hood in every frame. Never turn the head toward the viewer, away from the viewer, or show the back of the hood; the torso may twist and lean, but the head keeps looking forward to the right.

ACTING: this is a fast fencing attack and it must be dynamic and readable at small size: a short sharp anticipation (pull the blade back, coil the body), then an explosive thrust with the sword arm locked fully straight and the blade pointing at the target, a clear forward weight shift with bent knees and a body lean, a tiny overshoot, then a quick recovery. The scarf tails whip back with every forward push. Avoid stiff upright poses where only the arm moves; most frames must look clearly different from the idle pose.
"""
TAIL = """
Use the entire generated clip for this one move; leave the idle pose immediately and return to the exact idle pose only at the end. No idle padding at the start or the end.

Keep the camera locked in orthographic side view, facing right, with no horizontal travel, zoom or crop. The character stays at the same horizontal spot on screen for the whole clip (the game moves the character; the step happens in place). Keep the whole body and the whole rapier inside the frame at all times, including the blade tip at full extension. Preserve the character's size, hood, robe, narrow black trousers and exactly two scarf tails. NO waist belt, buckle, black skirt or added outline. Colors: black #1B1616, robe #FDEEDA, robe shadow #D2B396, scarf #025673 and #02648C, eyes and existing accents #EBA516. Preserve the reference raster/sprite style. Pure flat white background, no floor, shadow, enemy, target, VFX, sword trail, slash arc, sparks, motion lines, afterimages, text or checkerboard. Return one continuous animation, not a contact sheet.
"""
HITS = {
'00-basic-three-hit-combo': """
BASIC ATTACK - THREE-HIT RAPIER COMBO in one continuous motion: hit 1 flows straight into hit 2 and hit 2 flows straight into hit 3, like a fencer pressing forward. Never return to the idle pose between the hits: the end of each thrust becomes the wind-up of the next one. In the game hits 1 and 2 are very fast (0.22 seconds each) and hit 3 is heavier (0.36 seconds), so give hits 1 and 2 about the same number of frames and hit 3 clearly more.

1. HIT 1 - QUICK ADVANCE THRUST: from the idle pose, a short draw-back of the sword hand beside the chest with the blade level, then the front foot steps forward in a short quick advance and the sword arm shoots straight forward at chest height; the arm locks fully straight with the blade perfectly horizontal pointing right, the sword shoulder leads, the body leans forward about 15 degrees, the free hand swings back behind the hip.
2. HIT 2 - HIGH LINE DOWNWARD THRUST: without stopping, pull the blade back and up while the back foot slides up toward the front foot; the sword hand rises above the head with the tip pointing forward and slightly down; then the front foot stamps forward into a wider stance and the arm drives forward from high: the hand ends in front of and above the face, the arm locked straight, the blade angled about 20 degrees DOWNWARD; the body drops a little lower than in hit 1.
3. HIT 3 - DEEP LUNGE FINISHER: without stopping, pull the rapier all the way back so the sword hand is behind the chest with the blade level, sink very low on the bent back leg, the free arm rises behind the head (a clear wind-up); then explode into a full classic fencing lunge: the front foot shoots far forward with the front knee deeply bent, the back leg straightens fully with the back foot flat, the body drops very low and leans strongly forward, the sword arm and the horizontal blade form one long line at chest height reaching as far right as possible, the free arm extends straight back. Hold the lunge for a few frames; the scarf tails stream straight back.
4. RECOVER: push off the front foot, pull the body back up, draw the blade back and return to the exact idle pose.

Required readable silhouettes, in order: the level straight-arm thrust (hit 1), the high hand with the blade slanting down (hit 2), the low coiled wind-up, then the long low lunge (hit 3). The three thrusts must look clearly different from each other.
""",
'01-basic-hit1-quick-thrust': """
BASIC ATTACK HIT 1 - QUICK ADVANCE THRUST (the first, lightest hit of a three-hit rapier combo; very fast in the game, the hit lands about 45% into the motion).

1. ANTICIPATION (short): from the idle pose, draw the sword hand back beside the chest, the blade turns level and points forward at chest height, the knees bend and the weight sinks onto the back foot.
2. THRUST: the front foot steps forward in a short quick advance and the sword arm shoots straight forward at chest height; the arm locks fully straight with the blade perfectly horizontal and pointing right, the shoulders and hips turn sideways so the sword shoulder leads, the body leans forward about 15 degrees, the free hand swings back behind the hip for balance.
3. RECOVER: snap the blade back, pull the front foot back under the body and return to the exact idle pose.

Required readable silhouette: a compact straight-arm thrust with the horizontal blade reaching far to the right of the body, feet apart in a short fencing stance.
""",
'02-basic-hit2-high-line-thrust': """
BASIC ATTACK HIT 2 - HIGH LINE DOWNWARD THRUST (the second hit of the combo; as fast as hit 1 but with a clearly different silhouette, the hit lands about 45% into the motion).

1. ANTICIPATION (short): from the idle pose, the back foot slides up toward the front foot, the sword hand rises above the head and the blade tilts so its tip points forward and slightly down, the body coils back a little.
2. THRUST: the front foot stamps forward into a stance wider than hit 1 and the sword arm drives forward from high: the hand ends in front of and above the face, the arm locked straight, the blade angled about 20 degrees DOWNWARD toward the enemy's chest; the body leans forward and drops a little lower than in hit 1, the free hand is held back and low.
3. RECOVER: lower the sword hand, bring the feet back together and return to the exact idle pose.

Required readable silhouette: the sword hand high in front of the face with the straight blade slanting down and forward, clearly different from the level thrust of hit 1.
""",
'03-basic-hit3-deep-lunge-finisher': """
BASIC ATTACK HIT 3 - DEEP LUNGE FINISHER (the last and strongest hit of the combo; slower and heavier than hits 1 and 2, the hit lands about one third into the motion, then a longer recovery).

1. ANTICIPATION: from the idle pose, pull the rapier all the way back so the sword hand is behind the chest and the blade points forward level, sink very low on the bent back leg, the free arm rises up behind the head; a clear wind-up.
2. LUNGE: explode into a full classic fencing lunge: the front foot shoots far forward and plants with the front knee deeply bent, the back leg straightens fully behind with the back foot flat, the body drops very low and leans strongly forward, the sword arm locks fully straight with the horizontal blade at chest height reaching as far right as possible, and the free arm extends straight back behind for balance. The scarf tails stream straight back. Hold this lunge for a few frames.
3. RECOVER: push off the front foot, pull the body back up, draw the blade back and return step by step to the exact idle pose.

Required readable silhouettes: the low coiled wind-up, then the long low lunge with the front knee bent, the back leg straight, the sword arm and blade forming one long horizontal line to the right, and the free arm straight back.
""",
'04-special-charge-thrust': """
SPECIAL - CHARGED THRUST (the player holds the button to charge, then releases one powerful single thrust; in the game the charge hold lasts 0.2 to 0.8 seconds, the thrust lands right after the release and the recovery is short).

1. WIND-UP: from the idle pose, sink into a deep low guard: the back knee deeply bent, the weight on the back foot, the sword hand pulled back beside the hip with the blade level and pointing forward, the free arm raised up behind the head, the body coiled back.
2. CHARGE HOLD: hold this low coiled guard for several frames with only small tension changes (the body coils a little further back, the scarf tails lift and settle). These frames must be nearly identical so the game can repeat them while the button is held.
3. RELEASE THRUST: explode forward: the front foot shoots far forward, the body drives forward and low, the sword arm locks fully straight with the horizontal blade at chest height reaching as far right as possible, the free arm swings straight back. A bigger and more forceful thrust than any basic hit.
4. RECOVER: pull back up and return to the exact idle pose.

Required readable silhouettes: the low coiled charge guard (held), then the explosive long horizontal thrust.
""",
'05-special-long-needle': """
SPECIAL - LONG NEEDLE THRUST (a precise long-range piercing thrust; the game draws a long needle of ink in front of the blade, so the pose must aim perfectly straight; the hit lands at about 40% of the motion, then a calm recovery).

1. AIM: from the idle pose, turn the body fully sideways and plant the feet wide and still, the back knee bent; pull the sword hand back beside the cheek so the blade lies level at eye height pointing straight right like aiming an arrow, the free hand held forward lightly as a guide.
2. PIERCE: push the sword arm straight forward from the shoulder in one sharp line, the free hand snaps back, the body leans forward and the back leg straightens; the arm and the blade form one perfectly straight horizontal line at shoulder height.
3. HOLD: keep the fully extended straight line still for a few frames (the scarf tails settle back).
4. RECOVER: draw the blade back and return to the exact idle pose.

Required readable silhouettes: the aiming pose with the blade beside the cheek, then the perfectly straight extended arm-and-blade line at shoulder height. Feet stay planted; this is a piercing pose, not a lunge.
""",
'06-special-slip-thrust': """
SPECIAL - SLIP THRUST (the character vanishes and reappears right in front of the nearest enemy and stabs it; the game teleports the character at 0.10 seconds, so the clip has a very short departure, then the arrival stab).

1. DEPARTURE (very short, 2 or 3 frames): from the idle pose, dip low and turn slightly, the blade held low and back along the body, the scarf tails swirl around the body as if slipping away.
2. ARRIVAL STAB: the character is already low and close, as if just landed from a quick dash: the front knee bent, then a short sharp rising stab with the sword arm straightening forward and slightly upward at chest height, the free hand back. Compact, close range, much shorter reach than the basic hits.
3. RECOVER: rise and return to the exact idle pose.

Required readable silhouettes: the low swirling dip, then the compact low-to-chest stab. Never draw a second copy of the character, a ghost or an afterimage for the teleport; the game draws the teleport effect.
""",
'07-special-flash-flurry': """
SPECIAL - FLASH FLURRY (five lightning-fast thrusts from a planted low fencing stance, ending in a big lunge and a sharp salute; in the game the five hits land at 0.15, 0.27, 0.39, 0.51 and 0.63 seconds and the move ends at 0.95 seconds). The character stays on the ground the whole time: no jump, no hop, no floating.

1. SET (short): from the idle pose, snap into a wide low fencing stance: front knee bent forward, back leg bent, body turned sideways, the sword hand drawn back to the chest with the blade level and pointing right, the free hand raised behind the head.
2. FIVE THRUSTS: the sword arm fires five blindingly fast thrusts, snapping the blade back to the chest between each one. On every thrust the whole body pumps forward (the front knee bends deeper, the shoulders snap forward, the scarf tails whip back) and settles back between thrusts. Heights: thrust 1 at chest height, thrust 2 at face height, thrust 3 at chest height, thrust 4 at face height, all perfectly level or slightly upward; never point the blade downward.
3. FINAL LUNGE: the fifth thrust is a full deep lunge: the front foot shoots forward, the back leg straightens, the body drops low and leans far forward, the sword arm and the level blade form one long line reaching as far right as possible, the free arm flies straight back. Hold it for a few frames.
4. SALUTE: spring back up out of the lunge, snap the rapier up vertically in front of the face with the guard at chin height (a crisp fencing salute) for a moment, then lower it and return to the exact idle pose.

Required readable silhouettes: the low wide stance with clearly separate level thrusts at two heights, the long low final lunge, and the upright salute with the blade vertical in front of the face. Never draw multiple blades, a fan of blades or afterimages; there is only one rapier and one sword arm at any time.
""",
}
SEPARATE = ('01-basic-hit1-quick-thrust', '02-basic-hit2-high-line-thrust', '03-basic-hit3-deep-lunge-finisher')   # 따로 뽑던 판: 보존용으로 따로/ 에만 쓴다
(OUT / '따로뽑기-보존').mkdir(exist_ok=True)
for name, body in HITS.items():
    (OUT / ('따로뽑기-보존/' if name in SEPARATE else '') / f'{name}-prompt-en.txt').write_text(HEAD + body + TAIL, encoding='utf-8'); print(name)
