"""새 디자인 3페이즈 Blue(그림자 얼굴·두 팔·발톱 장갑)의 Aether 입력을 만든다.

- 기준: boss-phase3-redesign-20261107/runtime/idle/00.png (384x256, 몸 178px)
- 시작·끝 그림: 1920x1080 흰 바탕, 5배 nearest. 발 y 1000 (떠오르기만 1070 으로 내려 머리 위 여백 확보)
- 게임 시간표: unity Chapter2/RoseBlue/Tuning.g.cs (유도 1초, 튕김 0.5초 간격 5발, 화살비 0.5초 뒤 1.5초, 광역 0.5초+2초 확장 뒤 6발)

    python -I tools/build_stage2_phase3_aether_inputs.py
"""
import hashlib
import io
import json
import shutil
import subprocess
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-redesign-20261107'
TEST = ROOT.parents[2] / '04_테스트' / '3페이즈 Aether 입력'
IDLE = GROUP / 'runtime/idle/00.png'
ROAR = 'monsters/stage-02/boss-motion-size-review-20261106/runtime/blue-roar/00.png'

FRAME = """HIGHEST PRIORITY — KEEP THE FRAMING EXACTLY LIKE THE FIRST FRAME:
Every frame uses the same camera, zoom and character size in pixels as the supplied first frame, with the same wide white margins. Do NOT zoom in, enlarge, crop, reframe, centre-crop or scale the character at any point, including the very first animated frame. The hood, both clawed hands and both boots stay fully visible with white space around them in every frame. Keep the same coarse pixel-art resolution and simple shapes as the first frame."""

FACE = """HIGHEST PRIORITY — THE FACE IS A SHADOW:
Inside the hood there is only a solid black void with two small glowing violet eyes. Never draw skin, a mask, a mouth, a nose, teeth, a jaw or any other facial feature. The two violet eyes keep their size, spacing and color in every frame; they never blink or change shape."""

IDENT = """Preserve the exact phase-3 Blue design from the supplied image: a tall, menacing figure in a long deep-navy hooded robe with violet-magenta lining and a high violet collar, a magenta sash at the waist, wrapped brown forearms, dark gloves ending in sharp claws with glowing violet tips, and dark boots with violet trim. He has TWO arms and two clawed hands. Same palette, proportions and limited pixel-art shading. He carries no bow and no weapon: all of his attacks are magic thrown from his clawed hands."""

NOFX = """NO DRAWN EFFECTS: no arrows, bolts, orbs, projectiles, magic circles, glow bursts, energy, trails, smoke, dust, particles, speed lines or ground shadow. The game adds every effect itself. Pure white #FFFFFF background in every frame. Locked camera, fixed framing, no zoom, no extra characters or props. Draw real in-between poses, never motion blur or smears. Keep the shadow face with only the two violet eyes in EVERY frame."""

TRANSFORM_LOCK = """HIGHEST PRIORITY — FIXED CAMERA: use the START image as the exact first frame and the END image as the exact last frame. The camera, zoom and white margins never change. Do not zoom in, crop or reframe. The character stays at the same horizontal position; his size changes only from the START size to the END size. Keep the same coarse pixel-art resolution and simple shapes."""

GROUNDED = 'Both boots stay on the same ground line unless the action below says otherwise. No walking, no jumping.'

CLIPS = [
    {
        'id': 'homing', 'name': '유도 화살', 'start': 'idle',
        'head': 'Create one non-looping attack sequence for a 2D side-scrolling pixel action game: the boss gathers dark magic between his claws and releases one homing magic bolt toward the image-left, then returns to his standing pose. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it.',
        'action': """ACTION — GATHER, RELEASE, RETURN:
1. GATHER: he brings both clawed hands together in front of his chest, claws curled around an empty space as if cradling a sphere. His shoulders hunch and the robe hem stirs.
2. HOLD: a brief tense pause, the claws trembling slightly.
3. RELEASE: in one sharp snap he thrusts ONE open clawed palm forward toward the LEFT of the IMAGE at chest height while the other hand pulls back to his side. His body leans into the push.
4. FOLLOW: he holds the extended palm still.
5. RETURN: he relaxes back into the exact standing pose of the first frame and holds it.

One continuous sequence, approximately 2.0 seconds:
0.00–0.10 s: match the supplied standing pose.
0.10–0.70 s: GATHER.
0.70–0.90 s: HOLD.
0.90–1.00 s: RELEASE, palm fully extended at 1.00 s.
1.00–1.30 s: FOLLOW.
1.30–1.80 s: RETURN.
1.80–2.00 s: hold, identical to the supplied first frame.""",
    },
    {
        'id': 'bounce', 'name': '튕기는 화살 5연사', 'start': 'idle',
        'head': 'Create one non-looping attack sequence for a 2D side-scrolling pixel action game: facing the viewer, the boss slashes the air with ONE clawed hand five times in quick succession, alternating between two diagonals like swish, swish, swish, then returns to his standing pose. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it. Keep it simple, fast and sharp.',
        'action': """ACTION — FIVE ONE-HANDED DIAGONAL SWIPES (v4):
He keeps FACING THE VIEWER the whole time, exactly like the first frame: the body and hood stay front-on, he never turns sideways or into profile. The feet stay planted. The other arm stays relaxed at his side.
Only ONE hand attacks: his right claw (the hand on the image-left side). It swipes five times, alternating between two diagonals:
- Swipe 1, 3 and 5: from his opposite shoulder DOWN-AND-OUT toward the LOWER-LEFT of the IMAGE (a backhand diagonal slash, claws open).
- Swipe 2 and 4: from low by his hip UP-AND-OUT toward the UPPER-LEFT of the IMAGE (a rising diagonal slash, claws open).
Each swipe is one quick, clean swish: a tiny wind-up, a fast slash through the air, a brief stop at the end of the slash, then straight into the next swipe from where the hand stopped. The robe sleeve snaps with each swipe; the torso only twists slightly and stays facing front.
Count them: EXACTLY FIVE swipes, one every 0.5 seconds. Not three, not four.

One continuous sequence, approximately 3.2 seconds:
0.00–0.10 s: match the supplied standing pose.
0.10–0.40 s: raise the right claw to the opposite shoulder.
0.50 s: SWIPE 1 ends (down to the lower-left).
1.00 s: SWIPE 2 ends (up to the upper-left).
1.50 s: SWIPE 3 ends (down to the lower-left).
2.00 s: SWIPE 4 ends (up to the upper-left).
2.50 s: SWIPE 5 ends (down to the lower-left).
2.50–2.70 s: hold the last swipe.
2.70–3.20 s: return to the exact standing pose of the first frame and hold it.
The claws are EMPTY: no slash trail, no arc, no glow, no spark at the fingertips.""",
    },
    {
        'id': 'rain', 'name': '순차 폭발 화살비', 'start': 'idle',
        'head': 'Create one non-looping summoning sequence for a 2D side-scrolling pixel action game: the boss raises both claws to the sky to call down a rain of magic arrows, then slams his fists down to detonate them and returns to his standing pose. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it. The motion must feel COMMANDING: clear held poses and decisive snaps.',
        'action': """ACTION — SUMMON, HOLD, DETONATE, RETURN:
1. SUMMON: he dips slightly with the claws low, then throws BOTH arms straight up overhead, claws spread wide toward the sky. The hood stays at the same angle; he does not look up.
2. HOLD: he keeps that summoning pose perfectly still while the arrows fall; the robe hem stirs gently.
3. DETONATE: he clenches both raised hands into fists, then slams both fists down to hip level in one sharp motion, knees bending slightly.
4. RETURN: he straightens back into the exact standing pose of the first frame and holds it.

One continuous sequence, approximately 3.4 seconds:
0.00–0.10 s: match the supplied standing pose.
0.10–0.30 s: dip, claws low.
0.30–0.50 s: arms snap up, locked at 0.50 s.
0.50–2.10 s: HOLD the summoning pose.
2.10–2.35 s: clench the raised fists.
2.35–2.45 s: slam the fists down, locked at 2.45 s.
2.45–2.80 s: hold the slam.
2.80–3.40 s: RETURN to the standing pose and hold it.""",
    },
    {
        'id': 'fan', 'name': '확장 광역기', 'start': 'idle',
        'head': 'Create one non-looping area-attack sequence for a 2D side-scrolling pixel action game: the boss spreads his arms wide in a dominant stance while his power swells, then pushes both claws forward to unleash it and returns to his standing pose. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it. Make it look powerful through STILLNESS and CONTROL, not by waving the arms.',
        'action': """ACTION — SPREAD, SWELL, PUSH, RETURN:
1. SPREAD: he steps into a wide, low stance and sweeps both arms out to the sides, claws spread wide, and locks there.
2. SWELL: he holds that wide pose perfectly still while only his robe hem, sleeves and collar billow upward and outward as if lifted by a rising wind.
3. PUSH: in one sharp motion he thrusts both clawed palms forward toward the LEFT of the IMAGE and holds them there while the cloth whips back once and settles.
4. RETURN: he brings his feet back together and returns to the exact standing pose of the first frame, then holds it.

One continuous sequence, approximately 4.6 seconds:
0.00–0.10 s: match the supplied standing pose.
0.10–0.50 s: SPREAD, locked at 0.50 s.
0.50–2.50 s: SWELL, arms motionless, cloth billowing.
2.50–2.60 s: PUSH, palms fully forward at 2.60 s.
2.60–3.80 s: hold the push.
3.80–4.40 s: RETURN.
4.40–4.60 s: hold, identical to the supplied first frame.""",
    },
    {
        'id': 'hover', 'name': '떠오르기·떠 있기·착지', 'start': 'idle', 'feet': 1070,
        'head': 'Create one non-looping movement sequence for a 2D side-scrolling pixel action game: the boss levitates up off the ground, floats in the air, then sinks back down and lands. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it. The motion must feel EERIE and weightless, like being lifted by invisible strings. It is NOT a jump: no crouch, no push-off, no bounce, no bending of the knees to spring.',
        'action': """ACTION — RISE, FLOAT, SINK, LAND:
1. RISE: from the standing pose he slowly lifts straight up off the ground. His feet leave the floor with the toes pointing down and the legs hanging loose. The robe hem hangs and sways, the claws relax slightly away from his sides. He rises about one sixth of his own height and stays at the same horizontal position.
2. FLOAT: he hovers in the air with a very slow, small up-and-down bob while the robe hem ripples gently. This part should be able to repeat smoothly.
3. SINK: he slowly lowers straight down.
4. LAND: his boots touch the same ground line softly and he settles into the exact standing pose of the first frame, then holds it.
This clip is the only one where he moves up and down inside the frame; keep the camera and his size fixed.

One continuous sequence, approximately 3.0 seconds:
0.00–0.20 s: match the supplied standing pose.
0.20–1.00 s: RISE.
1.00–2.20 s: FLOAT.
2.20–2.80 s: SINK.
2.80–3.00 s: LAND and hold, identical to the supplied first frame.""",
        'grounded': False,
    },
    {
        'id': 'stun', 'name': '기절 2초', 'start': 'idle',
        'head': 'Create one non-looping reaction sequence for a 2D side-scrolling pixel action game: the boss is struck by his own homing bolt, is stunned for two seconds, then recovers. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it.',
        'action': """ACTION — HIT, DAZED, RECOVER:
Both boots stay planted in place the whole time. Do NOT lift or move the feet, no stepping, no falling, no kneeling.
1. HIT: his head and shoulders snap back and his arms jerk out from his body.
2. DAZED: he hunches forward, head drooping, both clawed hands hanging loose. He sways slowly from side to side twice from the waist up.
3. RECOVER: he straightens up and returns to the exact standing pose of the first frame, then holds it.

One continuous sequence, approximately 2.3 seconds:
0.00–0.10 s: match the supplied standing pose.
0.10–0.30 s: HIT.
0.30–1.80 s: DAZED, two slow sways.
1.80–2.10 s: RECOVER.
2.10–2.30 s: hold, identical to the supplied first frame.""",
    },
    {
        'id': 'defeat', 'name': '처치 쓰러짐', 'start': 'idle',
        'head': 'Create one non-looping defeat sequence for a 2D side-scrolling pixel action game: the boss is finally beaten, staggers, drops to his knees and collapses face down on the floor. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it.',
        'action': """ACTION — STAGGER, KNEEL, COLLAPSE:
1. STAGGER: he jolts back, one clawed hand clutching his chest.
2. KNEEL: his knees buckle and he drops onto both knees, head bowed.
3. COLLAPSE: he falls forward toward the LEFT of the IMAGE onto his hands, then slumps flat face down on the floor, the robe spreading around him.
4. FADE: in the last frames the two violet eyes dim until they are dark. He lies completely still.
He stays at the same horizontal position; he falls in place, not across the frame.

One continuous sequence, approximately 2.6 seconds:
0.00–0.10 s: match the supplied standing pose.
0.10–0.50 s: STAGGER.
0.50–1.10 s: KNEEL.
1.10–1.80 s: COLLAPSE.
1.80–2.20 s: the eyes dim.
2.20–2.60 s: lie still.""",
        'grounded': False,
    },
    {
        'id': 'transform', 'name': '흡수 뒤 변신·등장 (컷씬)', 'start': 'roar', 'end': 'idle',
        'head': 'Create one non-looping transformation sequence for a 2D side-scrolling pixel action game cutscene. START: the blue-hooded boss in the START image, floating with his head thrown back mid-roar after absorbing his partner. END: his new, darker form in the END image. Animate him changing from the START design into the END design and landing in the END pose.',
        'action': """ACTION — TRANSFORM AND LAND:
1. Still floating with his head back, his body tenses and his fists clench.
2. He changes step by step: the light-blue hood and coat darken into deep navy with violet-magenta lining and a high violet collar, the coat lengthens down to the floor, a magenta sash appears at the waist, his pale face sinks into a black shadow inside the hood and two small violet eyes ignite, his hands become dark gloves with sharp violet-tipped claws, and he grows taller to the size of the END image.
3. He lowers his head, lands on the same ground line and settles exactly into the END standing pose, then holds it.
The change happens in the character's own shapes and colors, drawn as clean pixel shapes; no glow, light burst, smoke or particles.

One continuous sequence, approximately 2.4 seconds:
0.00–0.20 s: match the START image.
0.20–0.50 s: tense, fists clench.
0.50–1.60 s: the transformation.
1.60–2.10 s: lower the head and land.
2.10–2.40 s: hold, identical to the END image.""",
        'grounded': False,
    },
]


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def big(img, feet):
    b = img.resize((1920, 1280), Image.NEAREST)
    top = 232 * 5 - feet
    out = Image.new('RGBA', (1920, 1080), 'white')
    out.alpha_composite(b.crop((0, top, 1920, top + 1080)))
    return out.convert('RGB')


def roar_image():
    raw = subprocess.run(['git', 'show', f'origin/master:{ROAR}'], cwd=ROOT, capture_output=True, check=True).stdout
    raw = subprocess.run(['git', 'lfs', 'smudge'], cwd=ROOT, input=raw, capture_output=True, check=True).stdout
    return raw


def main():
    TEST.mkdir(parents=True, exist_ok=True)
    idle = Image.open(IDLE).convert('RGBA')
    roar_raw = roar_image()
    roar = Image.open(io.BytesIO(roar_raw)).convert('RGBA')
    for i, c in enumerate(CLIPS, 1):
        d = GROUP / 'source' / f'aether-{c["id"]}'
        d.mkdir(parents=True, exist_ok=True)
        if c['id'] == 'transform':
            # 시작은 옛 파란 Blue(창백한 얼굴)라 얼굴·크기 고정문을 쓰지 않는다
            parts = [c['head'], TRANSFORM_LOCK, 'END design: ' + IDENT.replace('from the supplied image', 'in the END image'), c['action'], NOFX.replace(' Keep the shadow face with only the two violet eyes in EVERY frame.', ' The pale face never gets a mouth or expression; once it sinks into shadow, only the two violet eyes remain.')]
        else:
            parts = [c['head'], FRAME, FACE, IDENT]
            if c.get('grounded', True):
                parts.append(GROUNDED)
            parts += [c['action'], NOFX]
        prompt = '\n\n'.join(parts) + '\n'
        (d / 'prompt.txt').write_text(prompt, encoding='utf-8')
        prefix = f'3페이즈_{i}_{c["name"].replace(" ", "").replace("·", "-").replace("(", "_").replace(")", "")}'
        shutil.copyfile(d / 'prompt.txt', TEST / f'{prefix}-프롬프트.txt')
        feet = c.get('feet', 1000)
        info = {'format': 'stage2-phase3-aether-input-v1', 'status': 'prompt-ready-awaiting-aether-output', 'name': c['name'], 'images': {}}
        for role, label in (('start', '시작'), ('end', '끝')):
            which = c.get(role)
            if not which:
                continue
            src = roar if which == 'roar' else idle
            img = big(src, feet if which == 'idle' else 1000)
            img.save(d / f'{role}-white-5x-1920.png')
            shutil.copyfile(d / f'{role}-white-5x-1920.png', TEST / f'{prefix}-{label}.png')
            info['images'][role] = {'source': ROAR if which == 'roar' else 'runtime/idle/00.png',
                                    'sha256': hashlib.sha256(roar_raw).hexdigest() if which == 'roar' else sha(IDLE),
                                    'layout': f'1920x1080 white, 5x nearest, feet y {feet if which == "idle" else 1000}'}
        (d / 'input.json').write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(prefix, list(info['images']))


if __name__ == '__main__':
    main()
