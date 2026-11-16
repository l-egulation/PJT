"""Prepare exact-pixel Aether inputs and the review page for the ArrowRain body motion."""
import hashlib
import html
import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-phase3-attacks-20261106'
SOURCE = GROUP / 'source/rain-aether'
PREVIEW = GROUP / 'preview/rain-input'

PROMPT = '''Create one non-looping summoning sequence for a 2D side-scrolling pixel action game: the boss calls a rain of arrows down from the sky, then detonates it. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it. The motion must feel SHARP, DISCIPLINED and COMMANDING: clear held poses and fast decisive snaps, not a soft continuous sway.

HIGHEST PRIORITY — KEEP THE APPROVED MASK EXACTLY:
The pale shape is a solid, inanimate mask, not a human face. Treat the reference head and mask as a locked sprite layer, not as a face to regenerate. Preserve the exact angular outer contour, pointed lower tip, eye openings, the small VIOLET eye accents, asymmetry and existing color blocks. Do not replace the violet eye accents with plain dark slits. Do not make the mask rounder, wider, shorter, symmetrical, more detailed or more human. NO mouth, lips, teeth, tongue, nostrils, new nose bridge, jaw movement, blinking, talking or expression. Do not interpret any dark shading as a mouth or draw new facial features.

Keep the head at the SAME position, scale, angle and facing direction throughout. No nodding, looking up at the sky, head tilt, turning, perspective change, squashing or stretching. Move the existing arm and sleeve around this fixed head. Do not pass the hand or forearm over the mask. If a gesture would require changing the mask or head angle, reduce the gesture instead. If a detail reference is supplied, it is an enlarged crop of this exact approved head, not another character or a replacement first frame.

ANATOMY LOCK — EXACTLY ONE ARM:
The ONLY arm is the one on the LEFT SIDE OF THE PROVIDED IMAGE, with the exposed pale hand already raised beside the mask. Use this same arm throughout. The opposite arm is anatomically ABSENT, not concealed under the robe. Never add or reveal a second arm, elbow, wrist, hand, fingers, sleeve bulge, limb-shaped shadow or stump. Keep the missing-arm side's robe drape unchanged. Do not mirror the existing arm or enlarge the hand.

Preserve the approved hood, blue-and-violet robe, crossed fabric bands, wrapped legs, boots, body proportions and limited pixel-art palette. Preserve the exact floating violet magical crossbow emblem above the head: the same recognizable shape, size, position and spacing, with no mechanical redesign, deformation, rotation or firing animation. Keep the whole character and emblem inside the frame.

ACTION — RAISE TO SUMMON, HOLD, THEN STRIKE DOWN TO DETONATE:
1. SUMMON: pull the hand in tight to the upper chest below the mask, then snap the same arm UP and slightly OUTWARD toward the upper-left of the IMAGE, about 70 degrees above horizontal, with the open palm facing the sky. At full extension the hand is at about the height of the top of the hood, clearly to the LEFT of the hood, and BELOW and OUTSIDE the floating crossbow emblem. The fingertips stay just below the lower edge of the crossbow emblem, never touching it. The hand and sleeve must never overlap the crossbow emblem or the mask.
2. HOLD: keep that raised summoning pose perfectly still while the arrows fall. The sleeve settles once and then stops. No wobbling, waving, pumping or repeated gestures.
3. DETONATE: close the raised hand into a tight fist, then snap the fist sharply DOWN toward the ground on the image-left side, ending with the arm extended diagonally down-left about 45 degrees below horizontal, fist closed. Do not swing it across the body.
4. END: hold that downward detonation pose. Do not raise the hand again, bring it back beside the mask or return to the idle pose.

The arm remains attached to its original image-left shoulder at all times. Never swap shoulders, mirror the character or use two hands. Keep secondary motion minimal and controlled: the torso stays upright and stable, the robe reacts only slightly at each snap and settles at once. Animate the body only. The arrows, warnings and explosions will be added separately.

One continuous sequence, approximately 3.0 seconds:
0.00–0.10 s: match the supplied raised-hand idle pose.
0.10–0.30 s: COIL the hand tight to the upper chest below the mask; brief tense pause.
0.30–0.40 s: SNAP the arm up into the summoning pose, locked at 0.40 s.
0.40–2.10 s: HOLD the raised open-palm summoning pose completely still.
2.10–2.35 s: close the raised hand into a fist, keeping the arm raised.
2.35–2.45 s: SNAP the fist down to the lower-left detonation pose, locked at 2.45 s.
2.45–3.00 s: hold the detonation pose; only the sleeve hem settles once.

Make the coil, the summoning pose, the fist and the detonation pose clearly readable as distinct poses. The fast snaps must still contain one or two real in-between drawings, never a teleporting pose swap and never motion blur, speed lines or smears. Keep the pixel shapes crisp.

Locked camera, fixed framing, pure white #FFFFFF background, same facing direction, same character size as the supplied image. Both boots stay planted at the same positions and ground line. Keep the torso stable and the head completely fixed. No walking, stepping, jumping, hovering, turning, camera motion, zoom, scale changes, extra characters or new props. Do not animate the missing-arm side as a second limb.

No visible arrows, projectiles, charge glow, trails, magic circles, particles, impacts or explosions. The existing overhead crossbow is an identity element, not the source of the arrows. Preserve the exact mask and single-arm anatomy in EVERY frame.
'''


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    SOURCE.mkdir(exist_ok=True)
    PREVIEW.mkdir(exist_ok=True)
    if (SOURCE / 'input.json').exists() and json.loads((SOURCE / 'input.json').read_text(encoding='utf-8')).get('aetherOutputReceived'):
        raise SystemExit('Aether output already received for this input. Preserve it and author a new prompt version.')
    start = SOURCE / 'rain-start-white-3x.png'
    shutil.copy2(GROUP / 'source/bounce-aether/bounce-start-white-3x.png', start)
    head = SOURCE / 'approved-head-detail-8x.png'
    shutil.copy2(GROUP / 'source/bounce-aether/approved-head-detail-8x.png', head)
    assert sha(start) == sha(GROUP / 'source/homing-start-white-3x.png')
    assert Image.open(start).size == (1536, 1152)
    (SOURCE / 'prompt.txt').write_text(PROMPT, encoding='utf-8')
    data = {
        'format': 'stage2-rain-aether-input-v1', 'status': 'input-ready-awaiting-user-Aether-output', 'pattern': 'ArrowRain',
        'startImage': start.name, 'startImageSha256': sha(start), 'sourceReference': '../approved-fused-idle-original.png',
        'endImage': None, 'endImageReason': 'The detonation end pose is described in the prompt; no forced end frame.',
        'sourceCanvas': [384, 256], 'inputCanvas': [1536, 1152], 'logicalPaddingCanvas': [512, 384], 'sourcePasteOffset': [64, 64],
        'integerNearestScale': 3, 'inputGroundY': 888, 'inputPivot': [768, 888], 'prompt': 'prompt.txt', 'suggestedDurationSeconds': 3.0,
        'bodyBeatsSeconds': {'summonLocked': 0.40, 'holdUntil': 2.10, 'detonateLocked': 2.45},
        'gameContract': {
            'source': 'data/combat/chapter2_rose_blue.json, RoseBlue/Patterns.cs, Runtime/RoseBlueEncounter.Patterns.cs @ main 7f2fbe8ed',
            'RainTell': 0.5, 'RainCount': 12, 'RainFall': 0.25, 'RainLandingSpan': 1.5, 'RainFuse': 0.2,
            'firstArrowSpawn': 0.5, 'landings': [0.75, 2.25], 'explosions': [2.45, 3.95],
            'arrowsFallFromArenaTop': True, 'startSide': 'random left/right (cast.FromRight)', 'bossFixedInPlace': True,
            'combatChanges': False},
        'directionChoice': 'Neutral vertical summon; the random start side is shown by the existing rain-start warning column. A side-pointing tell would need two generations because the character is never mirrored.',
        'referenceMainCommit': '7f2fbe8ed90aca1f943d124db52e637c32ae029b',
        'identityLocks': {'rigidMask': True, 'mouth': False, 'armCount': 1, 'existingArm': 'image-left raised hand beside mask',
                          'oppositeArm': 'anatomically absent', 'overheadCrossbow': 'preserve exact approved shape',
                          'headPositionScaleAngleFixed': True, 'armMayNotOccludeMask': True, 'handMayNotOverlapCrossbow': True},
        'handSpaceOnApproved': {'crossbowLeftPartBottomY': 44, 'hoodTopY': 54, 'hoodLeftEdgeXAtY60_70': [182, 176], 'freeZone': [140, 45, 175, 75], 'note': 'approved 384x256 canvas coordinates'},
        'headDetailReference': {'file': head.name, 'sourceCrop': [163, 50, 223, 112], 'integerNearestScale': 8, 'role': 'identity detail only; not first-frame image'},
        'aetherOutputReceived': False, 'selectedFrames': None, 'timingsFinalized': False, 'imageApiCalled': False, 'sourcePixelsRepainted': False,
    }
    (SOURCE / 'input.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    page = f'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>화살비 · Aether 입력</title><link rel="stylesheet" href="../review.css"><main>
<p class="eyebrow">STAGE 02 / PHASE 03 / MOTION 03</p><h1>순차 폭발 화살비 · 한 팔 소환</h1>
<p class="notice">유도 화살 D V2와 튕기는 화살 5연사 A 확정. 다음 동작의 Aether 입력 준비 화면입니다. 화살비 모션은 아직 생성되지 않았습니다.</p>
<p>화살 12발은 전장 위에서 떨어지고 시작 쪽은 매번 좌우 무작위입니다. 몸은 방향과 상관없이 위로 소환하고 아래로 내리찍어 터뜨립니다. 캐릭터를 뒤집지 않으므로 시작 방향은 기존 바닥 경고 기둥이 알려 줍니다.</p>
<p class="notice">가면·보라 눈빛·머리 위치를 고정하고 손으로 가리지 않습니다. 팔은 이미지 왼쪽 어깨의 팔 하나뿐입니다. 들어 올린 손은 후드 왼쪽 바깥, 석궁 아래에 두어 석궁과 겹치지 않게 합니다.</p>
<nav><a href="../../source/rain-aether/rain-start-white-3x.png" target="_blank">Aether 시작 이미지</a><a href="../../source/rain-aether/prompt.txt" target="_blank">영어 프롬프트 파일</a><a href="../../source/rain-aether/input.json" target="_blank">입력 규격</a><a href="../bounce-cleanup/">확정 5연사 A</a><a href="../homing-cleanup/">확정 유도 화살 D V2</a></nav>
<section><h2>시작 이미지</h2><p>승인 기본 자세 그대로 · 1536×1152 · 정수 3배 · 흰 배경 · 바닥 Y=888. 5연사 입력과 같은 파일입니다. 끝 이미지는 강제하지 않습니다.</p><img id="start" class="start" src="../../source/rain-aether/rain-start-white-3x.png" alt="입 없는 가면과 한 팔 형태를 유지한 승인 기본 자세"></section>
<section><h2>고정할 승인 가면</h2><p>승인 원본 머리를 픽셀 변경 없이 8배 확대한 확인용 참조입니다. 시작 이미지는 위 전신 이미지를 사용합니다.</p><img style="max-width:100%;image-rendering:pixelated" src="../../source/rain-aether/approved-head-detail-8x.png" alt="승인 원본 가면과 후드 확대 참조"></section>
<section><h2>몸 동작과 게임 시간표</h2><table><tr><th>시각</th><th>게임</th><th>몸</th></tr>
<tr><td>0.10~0.40초</td><td>0초 시작 쪽 경고 기둥</td><td>가슴 앞에서 감았다가 팔을 왼쪽 위 약 70°로 튕겨 올림 · 손바닥은 하늘로 · 0.40초 정지</td></tr>
<tr><td>0.40~2.10초</td><td>0.5초 첫 화살 → 2.0초 12번째 화살 · 착지 0.75~2.25초</td><td>소환 자세 그대로 유지</td></tr>
<tr><td>2.10~2.45초</td><td>2.45초 첫 폭발</td><td>올린 손을 주먹 쥐고 왼쪽 아래 약 45°로 내리찍음 · 2.45초 정지</td></tr>
<tr><td>2.45~3.00초</td><td>폭발 2.45~3.95초</td><td>내리찍은 자세 유지 · 기본 자세로 돌아가지 않음</td></tr></table>
<p>전투 수치·조준·발생 위치는 바꾸지 않습니다. 화살·경고·폭발은 본체에 넣지 않습니다.</p></section>
<section><h2>영어 Aether 프롬프트</h2><button id="copy">프롬프트 복사</button><pre id="prompt">{html.escape(PROMPT.rstrip(chr(10)))}</pre></section><p id="status" role="status">시작 이미지 확인 중…</p></main><script src="review.js"></script></html>'''
    (PREVIEW / 'index.html').write_text(page, encoding='utf-8')
    js = (GROUP / 'preview/bounce-input/review.js').read_text(encoding='utf-8').replace(' · A 확정', ' · Aether 결과 대기')
    assert 'Aether 결과 대기' in js
    (PREVIEW / 'review.js').write_text(js, encoding='utf-8')
    print(sha(start))


if __name__ == '__main__':
    main()
