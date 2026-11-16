"""Prepare exact-pixel Aether inputs and the review page for the ExpandingFan body motion."""
import hashlib
import html
import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
SOURCE = GROUP / 'source/fan-aether'
PREVIEW = GROUP / 'preview/fan-input'

PROMPT = '''Create one non-looping casting sequence for a 2D side-scrolling pixel action game: the boss unleashes its area attack: one eruption, a still arm inside a rising storm, then one release. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it. The motion must feel POWERFUL, CONTROLLED and DOMINANT: few, decisive arm movements and a lot of held stillness.

HIGHEST PRIORITY — KEEP THE FRAMING EXACTLY LIKE THE FIRST FRAME:
Every frame uses the same camera, zoom and character size as the supplied first frame. The character stays the SAME small size in pixels, at the SAME position, with the same wide white margins around it. Do NOT zoom in, enlarge, crop, reframe, push in, centre-crop or scale the character up at any point, including the very first animated frame. The floating crossbow emblem and both boots must remain fully visible with white space above the emblem and below the boots in every frame. Do not add detail by enlarging the character; keep the same coarse pixel-art resolution and the same simple shapes as the first frame.

HIGHEST PRIORITY — KEEP THE APPROVED MASK EXACTLY:
The pale shape is a solid, inanimate mask, not a human face. Treat the head and mask in the first frame as a locked sprite layer, not as a face to regenerate. Preserve the exact angular outer contour, pointed lower tip, eye openings, the small VIOLET eye accents, asymmetry and existing color blocks. Do not replace the violet eye accents with plain dark slits. Do not make the mask rounder, wider, shorter, symmetrical, more detailed or more human. ABSOLUTELY NO mouth, mouth line, lips, teeth, tongue, nostrils, nose shading, chin crease, jaw movement, blinking, talking or expression: the lower half of the mask is one smooth pale surface. Do not interpret any dark shading as a mouth or draw new facial features.

Keep the head at the SAME position, scale, angle and facing direction throughout. No nodding, looking up, head tilt, turning, perspective change, squashing or stretching. Move the existing arm and sleeve around this fixed head. Do not pass the hand or forearm over the mask. If a gesture would require changing the mask or head angle, reduce the gesture instead.

ANATOMY LOCK — EXACTLY ONE ARM:
The ONLY arm is the one on the LEFT SIDE OF THE PROVIDED IMAGE, with the exposed pale hand already raised beside the mask. Use this same arm throughout. The opposite arm is anatomically ABSENT, not concealed under the robe. Never add or reveal a second arm, elbow, wrist, hand, fingers, sleeve bulge, limb-shaped shadow or stump. The missing-arm side's robe may billow in the wind but must never suggest an arm or hand. Do not mirror the existing arm or enlarge the hand.

Preserve the approved hood, blue-and-violet robe, crossed fabric bands, wrapped legs, boots, body proportions and limited pixel-art palette. Preserve the exact floating violet magical crossbow emblem above the head: the same recognizable shape, size, position and spacing, with no mechanical redesign, deformation, rotation or firing animation. Keep the whole character and emblem inside the frame.

ACTION — ONE ERUPTION, A STILL ARM IN THE STORM, ONE RELEASE:
This is the boss's area attack. Make it look powerful through STILLNESS and CONTROL, not through waving the arm: after one big opening sweep the arm barely moves while the cloth rages around it. The character faces the LEFT of the IMAGE; "forward" means toward the image-left.
1. COIL: drop the hand low beside the hip on the image-left side, palm down, elbow bent, a tense gathering pause.
2. ERUPTION SWEEP: swing the same arm in one wide, powerful arc outward and upward to a straight horizontal reach toward the image-left, palm DOWN, fingers spread wide, and lock it there. At that instant the robe hem, the wide sleeve, the hood edge and the crossed fabric bands burst outward and upward in one big flare.
3. EYE OF THE STORM: the arm stays locked and perfectly still, straight out at shoulder height. Only the cloth moves: the robe hem, sleeve and loose fabric billow and ripple strongly upward and outward as if lifted by a rising wind. The calm, motionless arm against the raging cloth is the key image.
4. CLENCH: without moving the arm, turn the palm up and slowly close the spread fingers into a tight, trembling fist, as if crushing gathered power. The cloth rises to its highest.
5. RELEASE: in one sharp snap, thrust the fist a short distance forward and burst it open into a flat, spread palm facing forward; the cloth whips backward once.
6. DOMINATE: hold the open forward palm completely still while the cloth slowly settles back into place. No more arm gestures. Do not return to idle.

The arm makes only these three movements: the opening sweep, the slow clench and the single release. No waving, flailing, slashing, pointing repeatedly, circling, swinging back and forth or recocking. The arm remains attached to its original image-left shoulder at all times. Never swap shoulders, mirror the character or use two hands. Billowing cloth must never form a second arm, hand, elbow or limb-shaped shape; the missing-arm side shows only robe fabric. The torso stays upright and the head stays completely fixed while the cloth moves. Animate the body and its cloth only.

One continuous sequence, approximately 3.2 seconds:
0.00–0.10 s: match the supplied raised-hand idle pose.
0.10–0.30 s: COIL the hand low beside the hip; tense pause.
0.30–0.45 s: ERUPTION SWEEP to the horizontal palm-down reach, locked at 0.45 s; cloth bursts outward.
0.45–1.60 s: EYE OF THE STORM; the arm is motionless while the cloth billows strongly.
1.60–2.00 s: CLENCH slowly into a fist with the arm still extended; cloth at its highest.
2.00–2.10 s: RELEASE, the fist bursts open into a forward palm, locked at 2.10 s; cloth whips back.
2.10–3.20 s: DOMINATE, hold the open palm completely still while the cloth slowly settles.

NO DRAWN EFFECTS: draw NO wind, smoke, clouds, dust, debris, swirls, wind streaks, speed lines, glow or sparkles anywhere, not even faint grey or white ones. The background stays pure white #FFFFFF in every frame. The rising wind is shown ONLY by the movement of the character's own cloth.

Make the coil, the eruption reach, the clenched fist and the released palm clearly readable as distinct poses. The fast snaps must still contain one or two real in-between drawings, never a teleporting pose swap and never motion blur, speed lines or smears. Keep the pixel shapes crisp.

Locked camera, fixed framing, no zoom, pure white #FFFFFF background, same facing direction, exactly the same character size in pixels as the supplied first frame. Both boots stay planted at the same positions and ground line. Keep the torso stable and the head completely fixed; only cloth and the one arm move. No walking, stepping, jumping, hovering, turning, camera motion, zoom, scale changes, extra characters or new props. Do not animate the missing-arm side as a second limb.

No visible arrows, projectiles, field, whirlwind, wind, smoke, clouds, dust, charge glow, trails, magic circles, particles, impacts or explosions. The existing overhead crossbow is an identity element, not the source of the arrows. Preserve the exact mouthless mask and single-arm anatomy in EVERY frame.
'''


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    SOURCE.mkdir(exist_ok=True)
    PREVIEW.mkdir(exist_ok=True)
    if (SOURCE / 'input.json').exists() and json.loads((SOURCE / 'input.json').read_text(encoding='utf-8')).get('aetherOutputReceived'):
        raise SystemExit('Aether output already received for this input. Preserve it and author a new prompt version.')
    start = SOURCE / 'fan-start-white-3x.png'
    shutil.copy2(GROUP / 'source/rain-aether/rain-start-white-3x.png', start)
    head = SOURCE / 'approved-head-detail-8x.png'
    shutil.copy2(GROUP / 'source/rain-aether/approved-head-detail-8x.png', head)
    assert sha(start) == sha(GROUP / 'source/homing-start-white-3x.png')
    assert Image.open(start).size == (1536, 1152)
    (SOURCE / 'prompt.txt').write_text(PROMPT, encoding='utf-8')
    data = {
        'format': 'stage2-fan-aether-input-v4', 'promptVersion': 4, 'previousPrompts': ['previous/prompt-v1-zoomed-face.txt', 'previous/prompt-v2-framing-plain.txt', 'previous/prompt-v3-storm-flailing.txt'],
        'v4Reason': 'User on the v2/v3 results: "뭔가 다 ㄱㅊ은데 팔 휘적휘적은 없어도 될거 같은딩..! 궁극기가 뭔데!", then "동작을 저걸 빼고 다른 멋있는걸 넣으란 소리야 / 프롬프트를 새로 넣어달라는거지". v4 keeps the eruption sweep and cloth burst, removes the slashes and recocks, and uses a motionless arm in the billowing cloth, a slow clench and one release. The v3 output drew white smoke and wind swirls, so v4 forbids drawn effects more strongly.',
        'receivedAttempts': [{'folder': '../fan-aether-01/', 'prompt': 'previous/prompt-v2-framing-plain.txt', 'notes': 'no zoom (body 97x177 cell px, about 1:1 with approved); plain motion; preserved only'}, {'folder': '../fan-aether-02/', 'prompt': 'previous/prompt-v3-storm-flailing.txt', 'notes': 'arm flailing judged unnecessary; white smoke and swirls drawn in frames 06-23 (up to ~11.6k light-grey px); crossbow merges with smoke in 18; preserved only'}],
        'v3Reason': 'User: "광역기인데 동작도 더 안 화려 해도 될까?" v3 keeps the framing, mask and one-arm locks but makes it spectacular: wide eruption sweep from the hip, cloth billowing in a rising magic wind, a claw grasp of the storm, three slashing commands with cloth whipping back, then the wind settling.',
        'v2Reason': 'User: "갑자기 확대 돼 면서 얼굴도 생기고 이상해졌어". Received sheets that zoomed in from frame 01 (homing 1.39x, rain 1.53x full height vs input 207 px) drew a human face (rain mouth); the unzoomed bounce sheets did not. v2 adds a framing lock and drops the head-detail reference from the Aether input.', 'status': 'input-ready-awaiting-user-Aether-output', 'pattern': 'ExpandingFan',
        'startImage': start.name, 'startImageSha256': sha(start), 'sourceReference': '../approved-fused-idle-original.png',
        'endImage': None, 'endImageReason': 'The final forward pointing pose is described in the prompt; no forced end frame.',
        'sourceCanvas': [384, 256], 'inputCanvas': [1536, 1152], 'logicalPaddingCanvas': [512, 384], 'sourcePasteOffset': [64, 64],
        'integerNearestScale': 3, 'inputGroundY': 888, 'inputPivot': [768, 888], 'prompt': 'prompt.txt', 'suggestedDurationSeconds': 3.2,
        'bodyBeatsPromptSeconds': {'eruptionLocked': 0.45, 'stillUntil': 1.60, 'clenchUntil': 2.00, 'releaseLocked': 2.10},
        'retimeToGame': {'eruptionLocked': 0.5, 'stillUntil': 2.5, 'clench': [2.5, 3.0], 'releaseLocked': 3.0, 'holdUntil': 4.3, 'note': 'Aether draws about 3 s; the review retimes the received frames to the game clock without changing combat data.'},
        'gameContract': {
            'source': 'docs/design/chapter2-rose-blue-boss.md pattern 5, data/combat/chapter2_rose_blue.json, RoseBlue/Patterns.cs, Runtime/RoseBlueEncounter.Patterns.cs @ main 7f2fbe8ed',
            'FanTell': 0.5, 'FanExpandSeconds': 2, 'FanWidth': 24.375, 'FanCount': 6, 'FanCreateGap': 0.1, 'FanRadius': 2, 'FanArcDegrees': 120,
            'arrowsForm': [2.5, 2.6, 2.7, 2.8, 2.9, 3.0], 'fire': [3.0, 3.5, 3.6, 4.1, 4.2, 4.3], 'arrowsFormAt': 'Blue.Position + up*Blue.size.y + 2u arc, upper 120 degrees',
            'bossTeleportedToCentre': True, 'artFacesImageLeft': 'Face() sets flipX when facing right', 'combatChanges': False},
        'referenceMainCommit': '7f2fbe8ed90aca1f943d124db52e637c32ae029b',
        'identityLocks': {'rigidMask': True, 'mouth': False, 'mouthWasDrawnInRainOutput': True, 'armCount': 1,
                          'existingArm': 'image-left raised hand beside mask', 'oppositeArm': 'anatomically absent',
                          'overheadCrossbow': 'preserve exact approved shape', 'headPositionScaleAngleFixed': True,
                          'armMayNotOccludeMask': True, 'handMayNotOverlapCrossbow': True},
        'handSpaceOnApproved': {'crossbowLeftPartBottomY': 44, 'hoodTopY': 54, 'freeZone': [140, 45, 175, 75], 'note': 'approved 384x256 canvas coordinates'},
        'headDetailReference': {'file': head.name, 'sourceCrop': [163, 50, 223, 112], 'integerNearestScale': 8, 'role': 'review only; do NOT attach to Aether (suspected to pull the generation toward a zoomed, detailed face)'},
        'aetherOutputReceived': False, 'selectedFrames': None, 'timingsFinalized': False, 'imageApiCalled': False, 'sourcePixelsRepainted': False,
    }
    (SOURCE / 'input.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    page = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>광역기 · Aether 입력</title><link rel="stylesheet" href="../review.css"><main>
<p class="eyebrow">STAGE 02 / PHASE 03 / MOTION 04</p><h1>확장 광역기·부채꼴 화살 · 한 팔 시전</h1>
<p class="notice">유도 화살 D V2, 5연사 A, 화살비 A-x2 확정. 마지막 공격의 Aether 입력 준비 화면입니다. 광역기 모션은 아직 생성되지 않았습니다.</p>
<p>보스는 중앙으로 순간이동한 뒤 바닥 회오리 광역을 펼치고, 머리 위에 생긴 화살 6발을 1-2-3 박자로 플레이어에게 쏩니다. 보스 그림은 이미지 왼쪽이 앞이며 게임이 플레이어 쪽으로 좌우를 뒤집습니다.</p>
<p class="notice">프롬프트 v4: 팔을 휘적이는 동작을 빼고, 한 번 크게 펼친 뒤 팔은 멈춘 채 천만 폭풍에 휘날리다가, 천천히 주먹을 쥐고, 한 번 확 펴는 동작으로 바꿨습니다. v3 결과에 그려진 흰 연기·소용돌이를 막으려고 효과를 그리지 말라는 문장을 강화했습니다. <a href="../../source/fan-aether/previous/prompt-v3-storm-flailing.txt" target="_blank">v3</a> 보존.</p><p class="notice">구도 고정(v2부터): 캐릭터가 갑자기 확대되면서 얼굴(입)이 생기는 문제를 막으려고 맨 앞에 구도 고정 단락을 넣었습니다. 받은 시트 중 첫 장부터 확대된 유도 화살(1.39배)·화살비(1.53배)에서 얼굴이 사람처럼 그려졌고, 확대되지 않은 5연사에서는 생기지 않았습니다. <strong>머리 8배 확대 참조는 Aether에 넣지 마세요.</strong> 시작 이미지 한 장만 넣습니다. 이전 프롬프트는 <a href="../../source/fan-aether/previous/prompt-v1-zoomed-face.txt" target="_blank">v1</a>로 보존했습니다.</p>
<nav><a href="../../source/fan-aether/fan-start-white-3x.png" target="_blank">Aether 시작 이미지</a><a href="../../source/fan-aether/prompt.txt" target="_blank">영어 프롬프트 파일</a><a href="../../source/fan-aether/input.json" target="_blank">입력 규격</a><a href="../rain-sim/">확정 화살비 A-x2</a></nav>
<section><h2>시작 이미지</h2><p>승인 기본 자세 그대로 · 1536×1152 · 정수 3배 · 흰 배경 · 바닥 Y=888. 앞 공격들과 같은 파일입니다. 끝 이미지는 강제하지 않습니다.</p><img id="start" class="start" src="../../source/fan-aether/fan-start-white-3x.png" alt="입 없는 가면과 한 팔 형태를 유지한 승인 기본 자세"></section>
<section><h2>고정할 승인 가면 · 확인용</h2><p>승인 원본 머리를 픽셀 변경 없이 8배 확대한 확인용 참조입니다. 결과 검사에만 쓰며 Aether에는 넣지 않습니다.</p><img style="max-width:100%;image-rendering:pixelated" src="../../source/fan-aether/approved-head-detail-8x.png" alt="승인 원본 가면과 후드 확대 참조"></section>
<section><h2>몸 동작과 게임 시간표</h2><p>Aether는 3초 남짓으로 그리므로 프롬프트는 3.2초 박자로 쓰고, 받은 뒤 게임 시간에 맞춰 늘립니다.</p><table><tr><th>프롬프트</th><th>게임</th><th>몸</th></tr>
<tr><td>0.10~0.45초</td><td>0초 순간이동·회오리 전조 → 0.5초 광역 시작</td><td>엉덩이 옆에서 감았다가 큰 원호로 왼쪽 수평까지 펼침 · 천이 확 터짐</td></tr>
<tr><td>0.45~1.60초</td><td>0.5~2.5초 광역 확장</td><td>팔은 뻗은 채 꼼짝 않고, 로브·소매·후드 끝만 솟는 바람에 휘날림</td></tr>
<tr><td>1.60~2.00초</td><td>2.5~3.0초 머리 위 화살 6개 생성</td><td>팔은 그대로 두고 손바닥을 위로 돌려 천천히 떨리는 주먹으로 쥠</td></tr>
<tr><td>2.00~2.10초</td><td>3.0초 첫 발사</td><td>주먹을 짧게 앞으로 내지르며 손바닥을 확 폄 · 천이 한 번 뒤로 젖혀짐</td></tr>
<tr><td>2.10~3.20초</td><td>3.5~4.3초 나머지 발사</td><td>편 손바닥을 앞으로 내민 채 정지 · 천만 천천히 내려앉음</td></tr></table>
<p>전투 수치·조준·발생 위치는 바꾸지 않습니다. 광역·화살·효과는 본체에 넣지 않습니다.</p></section>
<section><h2>영어 Aether 프롬프트</h2><button id="copy">프롬프트 복사</button><pre id="prompt">{html.escape(PROMPT.rstrip(chr(10)))}</pre></section><p id="status" role="status">시작 이미지 확인 중…</p></main><script src="review.js"></script></html>'''
    (PREVIEW / 'index.html').write_text(page, encoding='utf-8')
    js = (GROUP / 'preview/rain-input/review.js').read_text(encoding='utf-8')
    js = js.replace(' · A-x2 확정', ' · Aether 결과 대기')
    assert 'Aether 결과 대기' in js
    (PREVIEW / 'review.js').write_text(js, encoding='utf-8')
    print(sha(start))


if __name__ == '__main__':
    main()
