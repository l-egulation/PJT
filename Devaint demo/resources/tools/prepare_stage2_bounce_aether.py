"""Record user approval of homing V2 and prepare exact-pixel bounce inputs."""
import hashlib
import html
import json
import shutil
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
GROUP=ROOT/'monsters/stage-02/boss-phase3-attacks-20261106'
SOURCE=GROUP/'source/bounce-aether'
PREVIEW=GROUP/'preview/bounce-input'

PROMPT='''Create one non-looping, five-shot casting sequence for a 2D side-scrolling pixel action game. Use the supplied image as the exact first frame and approved character design. Animate this same character; do not redesign it.

HIGHEST PRIORITY — KEEP THE APPROVED MASK EXACTLY:
The pale shape is a solid, inanimate mask, not a human face. Treat the reference head and mask as a locked sprite layer, not as a face to regenerate. Preserve the exact angular outer contour, pointed lower tip, eye openings, violet eye accents, asymmetry and existing color blocks. Do not make it rounder, wider, shorter, symmetrical, more detailed or more human. NO mouth, lips, teeth, tongue, nostrils, new nose bridge, jaw movement, blinking, talking or expression. Do not interpret any dark shading as a mouth or draw new facial features.

Keep the head at the SAME position, scale, angle and facing direction throughout. No nodding, head tilt, turning toward the firing side, perspective change, squashing or stretching. Move the existing arm and sleeve around this fixed head. Do not pass the hand or forearm over the mask. If a gesture would require changing the mask or head angle, reduce the gesture instead. If a detail reference is supplied, it is an enlarged crop of this exact approved head, not another character or a replacement first frame.

ANATOMY LOCK — EXACTLY ONE ARM:
The ONLY arm is the one on the LEFT SIDE OF THE PROVIDED IMAGE, with the exposed pale hand already raised beside the mask. Use this same arm for every shot. The opposite arm is anatomically ABSENT, not concealed under the robe. Never add or reveal a second arm, elbow, wrist, hand, fingers, sleeve bulge, limb-shaped shadow or stump. Keep the missing-arm side's robe drape unchanged. Do not mirror the existing arm or enlarge the hand.

Preserve the approved hood, blue-and-violet robe, crossed fabric bands, wrapped legs, boots, body proportions and limited pixel-art palette. Preserve the exact floating violet magical crossbow emblem above the head: the same recognizable shape, size and spacing, with no mechanical redesign, deformation or firing animation. Keep the whole character and emblem inside the frame.

ACTION — ALTERNATING DIAGONAL RELEASES WITH THE SAME ARM:
Perform five distinct diagonal casting gestures, alternating between the UPPER-LEFT and UPPER-RIGHT of the IMAGE, approximately 35–45 degrees above horizontal. The order is UPPER-LEFT, UPPER-RIGHT, UPPER-LEFT, UPPER-RIGHT, UPPER-LEFT. Do not perform five straight-forward palm pushes.

Use the ONE existing arm for both directions. For an upper-left release, extend it diagonally outward from its original shoulder. For an upper-right release, sweep that SAME forearm across the upper chest, BELOW the mask, and extend the same hand diagonally toward the upper-right. The arm remains attached to its original image-left shoulder at all times. Never swap shoulders, mirror the character, invent an opposite-side arm or use two hands. Keep the head fixed, the missing-arm side unchanged, and the palm clear of the mask. Use compact elbow motion, a crisp wrist flick and restrained sleeve follow-through. Do not turn the body to face the opposite side.

Animate the body only. The arrows and all effects will be added separately. Connect the alternating directions with a natural low arc across the chest, not an instantaneous pose swap or a repeated reset to idle.

One continuous sequence, approximately 2.9 seconds:
0.00–0.10 s: match the supplied raised-hand idle pose.
0.10–0.40 s: draw the same hand a little inward toward the upper chest, bending the elbow to prepare.
0.40–0.50 s: FIRST release toward the UPPER-LEFT, finishing at 0.50 s.
0.50–0.90 s: recoil slightly and sweep the same forearm across the chest below the mask.
0.90–1.00 s: SECOND release toward the UPPER-RIGHT, finishing at 1.00 s.
1.00–1.40 s: bring that same hand back through the low chest-level arc.
1.40–1.50 s: THIRD release toward the UPPER-LEFT, finishing at 1.50 s.
1.50–1.90 s: smoothly prepare the opposite diagonal with the same arm.
1.90–2.00 s: FOURTH release toward the UPPER-RIGHT, finishing at 2.00 s.
2.00–2.40 s: bring that same hand back toward the original side.
2.40–2.50 s: FIFTH release toward the UPPER-LEFT, finishing at 2.50 s.
2.50–2.65 s: let the sleeve settle slightly while the hand remains extended.
2.65–2.90 s: hold the final upper-left casting pose. Do not lower the hand, return to idle, begin a sixth shot or loop the sequence.

Maintain connected motion and useful in-between poses through every recoil and release. Do not replace the movement with a few frozen poses, skip transitional poses or add motion blur. Keep the pixel shapes crisp.

Locked camera, fixed framing, pure white #FFFFFF background, same facing direction. Both boots stay planted at the same positions and ground line. Keep the torso stable and the head completely fixed. No walking, stepping, jumping, hovering, turning, camera motion, zoom, scale changes, extra characters or new props. Do not animate the missing-arm side as a second limb.

No visible arrows, projectiles, charge glow, trails, magic circles, particles, impacts or explosions. The existing overhead crossbow is an identity element, not the source of the shots. Preserve the exact mask and single-arm anatomy in EVERY frame.
'''


def dump(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    SOURCE.mkdir(exist_ok=True);PREVIEW.mkdir(exist_ok=True)
    prior=SOURCE/'previous/prompt-v1-forward.txt'
    prior.parent.mkdir(exist_ok=True)
    if not prior.exists() and (SOURCE/'prompt.txt').exists():
        prior.write_bytes((SOURCE/'prompt.txt').read_bytes())
    # Aether uses the same approved base design, not an invented intermediate pose.
    source=GROUP/'source/homing-start-white-3x.png'
    target=SOURCE/'bounce-start-white-3x.png'
    shutil.copy2(source,target)
    assert sha(source)==sha(target)
    assert Image.open(target).size==(1536,1152)
    approved=Image.open(GROUP/'source/approved-fused-idle-original.png').convert('RGBA')
    head=approved.crop((163,50,223,112))
    head_white=Image.new('RGBA',head.size,'white');head_white.alpha_composite(head)
    head_white.resize((480,496),Image.Resampling.NEAREST).save(SOURCE/'approved-head-detail-8x.png')
    (SOURCE/'prompt.txt').write_text(PROMPT,encoding='utf-8')
    dump(SOURCE/'input.json',{'format':'stage2-bounce-aether-input-v1','status':'input-ready-awaiting-user-Aether-output','pattern':'Bounce','startImage':'bounce-start-white-3x.png','startImageSha256':sha(target),'sourceReference':'../approved-fused-idle-original.png','endImage':None,'endImageReason':'Let the compact repeated cast articulate naturally; hold the fifth release without forcing a homing end pose.','sourceCanvas':[384,256],'inputCanvas':[1536,1152],'logicalPaddingCanvas':[512,384],'sourcePasteOffset':[64,64],'integerNearestScale':3,'inputGroundY':888,'inputPivot':[768,888],'prompt':'prompt.txt','suggestedDurationSeconds':2.9,'releaseSeconds':[.5,1,1.5,2,2.5],'gameContract':{'BounceTell':.5,'BounceCount':5,'shotOrigin':'existing hand','combatChanges':False},'referenceMainCommit':'7f2fbe8ed90aca1f943d124db52e637c32ae029b','identityLocks':{'rigidMask':True,'mouth':False,'armCount':1,'existingArm':'image-left raised hand beside mask','oppositeArm':'anatomically absent','overheadCrossbow':'preserve exact approved shape'},'aetherOutputReceived':False,'selectedFrames':None,'timingsFinalized':False,'imageApiCalled':False,'sourcePixelsRepainted':False})
    config_path=SOURCE/'input.json';config=json.loads(config_path.read_text(encoding='utf-8'))
    config.update(format='stage2-bounce-aether-input-v2',visualReleaseDirections=['upper-left','upper-right','upper-left','upper-right','upper-left'],diagonalDegreesAboveHorizontal=[35,45],headDetailReference={'file':'approved-head-detail-8x.png','sourceCrop':[163,50,223,112],'integerNearestScale':8,'role':'identity detail only; not first-frame image','sourcePixelsRepainted':False},previousPrompt='previous/prompt-v1-forward.txt')
    config['identityLocks'].update(headPositionScaleAngleFixed=True,maskPixelLayoutLocked=True,armMayNotOccludeMask=True,oneArmCrossesChestForOppositeDirection=True)
    config['reviewRequirement']='Reject changed mask designs; inspect received native output against approved head crop. If necessary reuse the approved mask pixels at fixed alignment, preserving original generated source.'
    config['gameContract']['visualPoseDirectionsAreNotCombatAimChanges']=True
    dump(config_path,config)
    motion_path=GROUP/'runtime/homing-d-v2/motion.json'
    motion=json.loads(motion_path.read_text(encoding='utf-8'));motion['status']='user-approved-D-V2-not-unity-integrated';motion['approval']={'userSelected':'D V2','scope':'timing, transparent frames and refined outlines','userRequest':'좋아 V2로 확정하고 다음 공격 모션 진행하자! 가면이랑 외팔 형태는 그대로 유지해줘'};dump(motion_path,motion)
    handoff_path=GROUP/'source/handoff.json';handoff=json.loads(handoff_path.read_text(encoding='utf-8'));handoff['status']='homing-D-V2-approved-bounce-input-ready';handoff['approvedHomingMotion']='runtime/homing-d-v2/motion.json';handoff['nextAttackInput']='source/bounce-aether/input.json';dump(handoff_path,handoff)
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>튕기는 화살 5연사 · Aether 입력</title><link rel="stylesheet" href="../review.css"><main>
<p class="eyebrow">STAGE 02 / PHASE 03 / MOTION 02</p><h1>튕기는 화살 · 한 팔 5연사</h1>
<p class="notice">유도 화살 D V2 확정. 다음 동작의 Aether 입력 준비 화면입니다. 새 연사 모션은 아직 생성되지 않았습니다.</p>
<p>↖ 왼쪽 위 → ↗ 오른쪽 위 → ↖ 왼쪽 위 → ↗ 오른쪽 위 → ↖ 왼쪽 위. 같은 팔 하나로 양쪽 사선을 0.5초 간격으로 번갈아 시전합니다.</p>
<p class="notice">가면의 윤곽·눈구멍·보라 눈빛·색 덩어리를 고정합니다. 머리 위치·크기·각도도 고정하고 손으로 가리지 않습니다. 오른쪽으로 쏠 때도 원래 이미지 왼쪽 어깨에 연결된 팔 하나만 가슴 앞을 가로지릅니다. 반대쪽 팔은 원래 없습니다.</p>
<nav><a href="../../source/bounce-aether/bounce-start-white-3x.png" target="_blank">Aether 시작 이미지</a><a href="../../source/bounce-aether/prompt.txt" target="_blank">영어 프롬프트 파일</a><a href="../homing-cleanup/">확정 유도 화살 D V2 재생</a><a href="../../source/bounce-aether/input.json" target="_blank">입력 규격</a></nav>
<section><h2>시작 이미지</h2><p>승인 기본 자세 그대로 · 1536×1152 · 정수 3배 · 흰 배경 · 바닥 Y=888. 끝 이미지는 강제하지 않습니다.</p><img id="start" class="start" src="../../source/bounce-aether/bounce-start-white-3x.png" alt="입 없는 가면과 한 팔 형태를 유지한 승인 기본 자세"></section>
<section><h2>고정할 승인 가면</h2><p>승인 원본 머리를 픽셀 변경 없이 8배 확대한 확인용 참조입니다. 시작 이미지는 위 전신 이미지를 사용합니다. 가면이 달라진 생성 결과는 이 기준으로 다시 확인합니다.</p><img style="max-width:100%;image-rendering:pixelated" src="../../source/bounce-aether/approved-head-detail-8x.png" alt="승인 원본 가면과 후드 확대 참조"></section>
<section><h2>다섯 번의 방출</h2><p>0.50 ↖ / 1.00 ↗ / 1.50 ↖ / 2.00 ↗ / 2.50 ↖. 머리를 돌리지 않고 팔만 가슴 앞의 낮은 곡선으로 연결합니다. 다섯 번째 방출 뒤 끝 자세를 유지합니다. 화살·반사·백색화·폭발 효과는 본체에 넣지 않습니다.</p></section>
<section><h2>영어 Aether 프롬프트</h2><button id="copy">프롬프트 복사</button><pre id="prompt">'''+html.escape(PROMPT)+'''</pre></section><p id="status" role="status">시작 이미지 확인 중…</p></main><script src="review.js"></script></html>'''
    (PREVIEW/'index.html').write_text(page,encoding='utf-8')
    (PREVIEW/'review.js').write_text("""const start=document.querySelector('#start'),status=document.querySelector('#status');start.decode().then(()=>status.textContent=`시작 이미지 ${start.naturalWidth}×${start.naturalHeight} 로딩 완료 · 영어 프롬프트 준비 완료 · Aether 결과 대기`).catch(()=>status.textContent='시작 이미지 로딩 실패');document.querySelector('#copy').onclick=async()=>{const prompt=document.querySelector('#prompt');try{await navigator.clipboard.writeText(prompt.textContent);document.querySelector('#copy').textContent='복사 완료';}catch{const range=document.createRange();range.selectNodeContents(prompt);const selected=window.getSelection();selected.removeAllRanges();selected.addRange(range);document.querySelector('#copy').textContent='선택됨 · Ctrl+C로 복사';}};\n""",encoding='utf-8')
    print('PASS: Homing D V2 approval recorded; exact approved start pixels reused; Bounce input and prompt ready.')


if __name__=='__main__':main()
