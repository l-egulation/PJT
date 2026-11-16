# 폭탄 스킬 프롬프트 v2(2026-11-09). 사용자가 인형 설명 페이지(04_테스트/폭탄 스킬 자세 설명)에서 정한 자세:
#   01 공중에서 앞발 뒷발 앞발 번갈아 뻥뻥뻥 차기 / 02 들기는 짧고 힘차게, 무게감은 던진 뒤 / 03 껴안으며 웅크리기 → 다이빙 두 단계
#   03b 공중제비 없이 튕겨 펼침 → 웅크려 떨어짐 → 한 손 짚고 착지 / 04 가슴 앞에서 손만 꼼지락 → 작게 툭 던짐(태엽은 이펙트 A 시계 눈금 고리)
# 게임 시간: 01 차기 0.20·0.35·0.50 끝 0.90 / 02 던짐 0.30 끝 0.90 / 03 0.20 에 미사일 / 03b 공중 0.22 + 회복 0.30 / 04 던짐 0.20 끝 0.60

BOMB_KICK = """Use the supplied image for the exact character and bomb design and the initial/final holding pose ONLY. The bomb is part of the character art: one round black (#1B1616) bomb with a small yellow (#EBA516) cap and a short black curled fuse on top. Keep its shape and colors. In this skill the THREE tossed bombs are drawn while they hang in the air in front of the character, each one exactly like the held bomb. The moment a foot hits a bomb, that bomb DISAPPEARS and is NOT drawn flying away (the game draws the projectile separately). After the third kick there is NO bomb anywhere in the picture, not flying, not on the ground, not as a small dot."""

SKILLS = {
'01-triple-kick': """TRIPLE BOMB KICK - toss three bombs up, hop, and kick them away one after another in mid-air with alternating legs: FRONT foot, BACK foot, FRONT foot, like a quick flurry of volley kicks. Bang, bang, bang: the three kicks are quick and evenly spaced. This is a KICKING skill: the hands never throw the bombs forward.

THE THREE BOMBS: in this skill only, the rear hand starts holding three identical bombs bunched together (each the same size and design as the held bomb).

1. TOSS: from the holding pose, dip slightly and flick the rear hand straight up, popping the three bombs up into the air just in front of the face, bunched loosely one above the other. The hand opens empty.
2. HOP + KICK ONE: spring up into a small hop in place (the feet leave the ground by about a quarter of the body height), chamber the FRONT knee and snap a front-leg volley kick straight forward at chest height; the foot meets the lowest bomb just in front of the body and that bomb vanishes. Both arms fly up and out for balance, the body leans back a little.
3. KICK TWO: still in the air, switch legs like a scissor kick: pull the front leg back and snap the BACK leg forward at chest height into the second bomb, which vanishes.
4. KICK THREE: switch again and snap the FRONT leg forward and higher in the strongest kick, leaning back the most, into the last bomb, which vanishes.
5. LANDING: drop and land on both feet in a crouch with the knees bent, the arms coming down, the scarf tails flaring; hold a short beat.
6. RECOVER: stand up and return to the exact holding pose (one bomb back in the rear hand only in the final holding pose).

Each kick is a quick snap and retract, not a slow swing. Required readable silhouettes: the hand flicking the bombs up, the front-leg volley, the back-leg scissor kick, the high final kick, and the crouched landing.""",

'02-big-bomb-heave': """BIG BOMB HEAVE - lift ONE BIG bomb with BOTH hands over the head and heave it forward like a soccer throw-in. The lift is QUICK and powerful; the heaviness shows AFTER the throw, when the weight drags the body forward.

THE BIG BOMB: in this skill only, the held bomb is replaced by one BIG bomb of the same design (round black body, small yellow cap, short black curled fuse) about TWICE the diameter of the normal bomb, held with BOTH hands. It appears at the very first frame of the wind-up in place of the small bomb.

1. GRAB: bend the knees deep and grab the big bomb with both hands at hip height, the body leaning forward over it.
2. LIFT: in one strong quick motion, lift it up and back over and behind the head, the back arched, the elbows bent; hold the top for one beat.
3. HEAVE: step the front foot forward and throw the big bomb forward with both arms together from behind the head; release above the head in front, both arms fully extended forward.
4. STAGGER (the longest part): the weight carries the body forward: bend far forward at the waist with both empty arms swinging down in front, take a small stumbling half-step to catch the balance (the feet stay at the same spot on screen), and hold the bent-over pose for a clear beat.
5. RECOVER: straighten up and return to the exact holding pose with the normal small bomb back in the rear hand.

Required readable silhouettes: the deep two-handed grab, the big bomb high behind the head, the two-armed overhead release, and the long bent-over stagger.""",

'03-missile-launch': """MISSILE LAUNCH (start of the missile transformation) - in two quick moves the character hugs the bomb into a launch crouch and then dives forward flat like a rocket. The game hides the character right after the last frame and replaces it with a missile, so this clip does NOT return to the idle pose.

1. HUG + CROUCH (one move): bring the bomb in front of the chest, hug it with BOTH hands and at the same time drop into a very low coiled crouch, the body leaning forward, the head looking right, the scarf tails lifting up behind.
2. DIVE: explode forward into a long flat horizontal dive: the whole body stretched straight out to the right, both arms extended forward holding the bomb at the very front like the nose of a rocket, the legs straight back together, the scarf tails streaming straight back. Hold this flat dive pose for the last few frames.

The clip ENDS in the flat dive pose. Do not return to the holding pose. If an end image is required, ignore it and keep the dive pose at the end.
Required readable silhouettes: the low crouch hugging the bomb, and the long flat rocket dive.""",

'03b-missile-emerge': """MISSILE EMERGE (end of the missile transformation) - the character pops out of the explosion, is blown back a little through the air, and lands. NO somersault, NO flip. The game draws the explosion separately.

1. BLOWN OUT: start in the air with the arms and legs flung out wide, the body leaning back as if pushed back by the blast, the scarf tails flung out.
2. FALL: pull the knees up and the arms in, curling a little while dropping.
3. LANDING: land on both feet in a deep crouch with ONE hand touching the ground in front, the other arm back for balance.
4. RECOVER: push up and return to the exact holding pose with the bomb back in the rear hand. The bomb appears in the hand only in the final holding pose; before that both hands are empty.

Keep the feet position on screen the same from the landing to the end. Required readable silhouettes: the open spread pose leaning back, the curled fall, and the deep one-hand landing crouch.""",

'04-timed-bomb-fiddle': """TIMED BOMB FIDDLE - a small, calm action: the character holds the bomb in front of the chest and fiddles with its cap with both hands, as if winding a timer, then gives it a small, short underhand toss forward. The body stays almost still; only the hands and forearms move. This is NOT an attack throw: no wind-up, no big arm swing, no lunge.

1. BRING: bring the bomb from the hip up in front of the chest and hold it with BOTH hands.
2. FIDDLE: the fingers and hands make small quick twisting wiggles at the bomb's cap, two or three little alternating twists, like winding a small clock. The body, legs and head stay still; maybe a tiny nod.
3. TOSS: the front hand gives the bomb a small, short underhand toss forward from chest height; the hand stays low and ends just in front of the chest, open and empty.
4. RECOVER: return to the exact holding pose (the bomb is back in the rear hand only in the final holding pose).

Required readable silhouettes: both hands together on the bomb in front of the chest, the small twisting hand wiggles, and the short little toss. Keep it small and quick.""",
}
