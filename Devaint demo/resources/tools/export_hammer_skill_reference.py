"""Export exact current hammer holding artwork and an Aether skill prompt.

No pose synthesis, sprite repaint, combat edit, or Unity deployment occurs.
The large reference preserves the supplied RGBA pixels by nearest resizing.
"""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
from PIL import Image
from assets import Store

ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parent
ASSET=ROOT/'characters/ink/weapons/hammer'
SET='user-hammer-default-skill-reference-20261101'
PRIOR='user-hammer-basic-color-motion-20261101'
PROMPT='''ONE DRAMATIC, FULL-BODY OVERHEAD HAMMER SLAM. Make the action LARGE and forceful. The three essential readable silhouettes are: HAMMER THROWN DEEP BEHIND THE HEAD, TORSO FOLDED FORWARD AT THE WAIST, and BOTH FEET CLEARLY AIRBORNE DURING THE DOWNSTROKE. These are required poses, not subtle suggestions.

Use the supplied image for the exact character and weapon design and the initial/final holding pose ONLY. Do not keep the weapon in its front holding position during the attack. Use the entire generated clip for one complete action; leave idle immediately and return to idle only at the end. No long idle padding.

1. HUGE BACKSWING: from the holding pose, swing both arms up and DEEPLY BACK BEHIND THE CROWN. The hammer HEAD must visibly pass behind the hood and shoulders, on the rear/left side of the body, not merely rise a little in front of the face. Lift the elbows high. Open the chest and arch the upper body BACKWARD, forming a strong backward-loaded silhouette. Feet stay grounded while loading the weight. Briefly show this fully cocked pose.

2. EXPLOSIVE DOWNSTROKE: whip the hammer from behind the head, over the crown and DOWN into the space just in front. Drive this with the ENTIRE BODY, not just the wrists. Fold sharply FORWARD FROM THE WAIST, about 50–70 degrees from upright. The hood and shoulders plunge forward and down, the hips remain behind, and the chest approaches a near-horizontal angle. Keep the body anatomically connected; this is a waist hinge, not a squash or shrink.

At the SAME MOMENT, BOTH FEET LEAVE THE GROUND BASELINE with a clearly visible gap of about 15–20 percent of the character's height. The knees bend and the feet trail behind the hips as the torso folds forward. The body becomes airborne DURING the attacking downstroke. Do not substitute a separate jump before swinging, and do not keep either foot pinned down.

3. SINGLE CRUSHING IMPACT: the hammer strikes low in front while the torso is still strongly folded and both feet remain briefly airborne. Hold this dramatic contact silhouette for a short beat. ONE hammer contact only.

4. HEAVY LANDING AND RECOVERY: let the body's weight follow the hammer down. Land both feet at the original baseline, absorb the force through bent knees, then deliberately recover to the exact reference holding pose. The body landing is not another hammer hit. No second swing or repeated bounce.

Keep the camera locked in orthographic side view, facing right, with no horizontal travel, zoom or crop. Keep the full backswing and airborne pose inside the frame. Preserve the character's size, hood, robe panels, narrow black trousers and exactly two scarf tails; the tails whip upward/back with the downstroke, then settle naturally. NO waist belt, buckle, black skirt or added outline. Both hands grip the same rigid black handle throughout. Preserve the exact hammer silhouette and size without bending or stretching.

Colors: black #1B1616, robe #FDEEDA, robe shadow #D2B396, scarf #025673 and #02648C, eyes and existing accents #EBA516. Preserve the reference raster/sprite style. Pure flat white background, no floor, shadow, enemy, VFX, text or checkerboard. Show the impact through the large body mechanics. Return one continuous animation, not a contact sheet. Avoid a small front lift, a timid wrist tap, a mostly upright torso, hidden foot lift or repeated standing poses.'''


def branch():
    assert subprocess.check_output(['git','-C',str(REPO),'branch','--show-current'],text=True).strip()=='feat/story-unity-essentials'
    assert subprocess.check_output(['git','-C',str(ROOT),'branch','--show-current'],text=True).strip()=='feat/basic-sword-normal-weapon'


def dump(path,data):
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')


def main():
    branch();store=Store(ROOT)
    path=ASSET/'runtime'/PRIOR/'detail4x/attack_2_09.png'
    raw=store.read({'asset':'characters.ink.weapons.hammer','path':path.relative_to(ROOT).as_posix()})
    original_digest=hashlib.sha256(raw).hexdigest()
    designpath=REPO/'data/combat/melee_weapon_design.json'
    unitypath=REPO/'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json'
    designraw=designpath.read_bytes();unityraw=unitypath.read_bytes()
    design=json.loads(designraw);unity=json.loads(unityraw)
    form=next(f for f in design['forms'] if f['id']=='hammer')
    spec=next(s for s in form['specials'] if s['id']=='hammer_default')
    actual=next(s for s in next(f for f in unity['forms'] if f['id']=='hammer')['specials'] if s['id']=='hammer_default')
    assert spec['activation_time']==actual['activation']==0.4
    assert spec['motion_end']==actual['end']==0.95
    assert spec['cooldown']==actual['cooldown']==6
    assert spec['hits'][0]['damage']==actual['hits'][0]['damage']==40
    source=ASSET/'source'/SET;preview=ASSET/'preview'/SET
    assert not source.exists() and not preview.exists()
    source.mkdir(parents=True);preview.mkdir(parents=True)
    (source/'holding-input-896x768.png').write_bytes(raw)
    im=Image.open(path).convert('RGBA')
    crop=(64,256,832,640)
    box=im.getbbox();assert crop[0]<=box[0] and crop[1]<=box[1] and crop[2]>=box[2] and crop[3]>=box[3]
    cropped=im.crop(crop)
    wide=cropped.resize((6144,3072),Image.Resampling.NEAREST)
    for suffix,image in [('6144x3072',wide),('4096x2048',wide.resize((4096,2048),Image.Resampling.NEAREST))]:
        transparent=preview/f'start-{suffix}-transparent.png'
        white=preview/f'start-{suffix}-white.png'
        image.save(transparent)
        bg=Image.new('RGBA',image.size,'white');bg.alpha_composite(image);bg.convert('RGB').save(white)
        shutil.copy2(transparent,preview/f'end-{suffix}-transparent.png')
        shutil.copy2(white,preview/f'end-{suffix}-white.png')
    (source/'aether-prompt-en.txt').write_text(PROMPT+'\n',encoding='utf8')
    instructions='''망치 기본 스킬 — 횡강타 / 에테르 입력

시작 이미지와 끝 이미지에 같은 holding 이미지를 사용한다.
기본 입력: preview/user-hammer-default-skill-reference-20261101/start-6144x3072-white.png
끝 입력: preview/user-hammer-default-skill-reference-20261101/end-6144x3072-white.png
업로드 크기 제한이 있을 때만 4096x2048 흰 배경 쌍으로 바꾼다. 두 규격을 시작·끝에 섞지 않는다.
투명본은 보존·다른 도구 입력용이다. 에테르에는 먼저 흰 배경본을 사용한다.
파일 원본을 업로드한다. 대화의 축소 미리보기를 캡처해 쓰지 않는다.

동작: 대기 → 망치 머리를 후드·어깨 뒤로 깊게 넘기고 상체를 뒤로 젖히기 → 허리를 앞으로 크게 접으며 양발이 확실히 뜨는 강한 내리찍기 → 짧은 접촉 유지 → 묵직한 착지 → 대기.
생성 단계에서는 큰 백스윙·허리 접힘·확실한 체공이 온전히 나오는 것을 우선한다. 긴 대기 반복 없이 클립 전체를 한 동작으로 사용한다. 결과를 받은 뒤 프레임 선택과 Unity 연결에서 타격0.40초/종료0.95초를 맞춘다.
최종 채택 프레임 수는 생성 결과를 본 뒤 정한다. 아직 새 스킬 모션을 생성하거나 승인·Unity 적용한 상태가 아니다.
필수 실루엣: 1) 망치 머리가 머리·어깨 뒤로 넘어간 큰 백스윙 2) 내리치는 순간 상체가 수직에서 약50~70도 앞으로 숙여진 허리 접힘 3) 양발 밑에 키의 약15~20% 간격이 보이는 체공. 준비 때 먼저 점프하지 않고, 실제 내리치는 순간 몸이 뜨며 발이 뒤따라야 한다.
배경 제거와 후처리를 위해 흰 배경, 이펙트 없는 몸체 모션을 우선 요청한다.
English prompt: aether-prompt-en.txt 내용을 한 번에 붙여 넣는다.

기존 게임 수치 — 변경 없음:
피해40 / 타격0.40초 / 활성0.12초 / 종료0.95초 / 쿨타임6초.
일반 적 밀기1.5unit·0.2초. 보스는 피해만. 캐릭터 지상 수평 이동 없음.
방향은 windup_start, 수직은 normal이다. 기본 공격 시험 공속0.8175와 마지막 타 후딜0.2초를 이 스킬에 넣지 않는다.
게임 문서와 데이터의 기본 스킬 명칭·기존 판정은 아직 횡강타다. 이번 수정은 제작용 아트 동작 요청이며, 실제 점프·새로운 범위 판정·스킬 수치 변경을 적용한 것이 아니다.
'''
    (source/'사용방법.txt').write_text(instructions,encoding='utf8')
    mapping={'purpose':'Aether skill input only; direct crop/resize of the latest palette-corrected holding frame',
        'input':path.relative_to(ROOT).as_posix(),'inputSha256':original_digest,
        'inputCanvas':[896,768],'inputPivot':[384,604],'inputPpu':128,'cropLTRB':crop,
        'largeCanvas':[6144,3072],'integerScaleFromDetail':8,'largePivot':[2560,2784],
        'smallerCanvas':[4096,2048],'smallerPivot':[2560*2/3,1856],
        'visibleCharacterAndHammerLarge':[ (box[2]-box[0])*8,(box[3]-box[1])*8 ],
        'startAndEndIdentical':True,'resampling':'nearest-neighbor; no pixel repaint or new colors',
        'restoreToDetail':'For 6144 reference: divide coordinates by 8 then add (64,256) to restore the 896x768 detail canvas. For 4096: divide by 16/3 then add (64,256).',
        'restoreToNative':'After restoring the detail canvas, scale by 1/4 into 224x192, pivot (96,151), PPU32. Deployment approval and metadata are separate.',
        'skill':spec,'unityDefaultMatches':True,
        'artMotionPlan':{'status':'user-requested; generation-pending','action':'one dramatic full-body overhead slam with hammer thrown deep behind hood and shoulders',
            'bodyLift':'both feet clearly airborne during downstroke, 15-20% of body height; grounded backswing then firm landing',
            'torso':'arch backward on backswing, then hinge forward 50-70 degrees from upright during attack',
            'horizontalRoot':'fixed','timing':{'contact':0.4,'end':0.95},
            'gameplay':'existing horizontal-skill definition unchanged; no new jump mechanics or runtime binding applied'},
        'approval':'reference-only; skill motion not generated','gameIntegration':'not-deployed'}
    dump(source/'reference-mapping.json',mapping)
    metadata=[ASSET/'asset.json',ROOT/'catalog.json',ROOT/'generated-assets.json']
    snapshots={p:p.read_bytes() for p in metadata};asset,catalog,generated=[json.loads(snapshots[p]) for p in metadata]
    assert SET not in asset.get('generatedSets',{})
    files=sorted(p for d in [source,preview] for p in d.rglob('*') if p.is_file())
    for p in files:
        rel=p.relative_to(ASSET).as_posix();assert rel not in asset['files'];asset['files'].append(rel)
        name=p.relative_to(ROOT).as_posix();assert not any(x['path']==name for x in generated['files'])
        b=p.read_bytes();generated['files'].append({'path':name,'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()})
    asset.setdefault('generatedSets',{})[SET]={'source':source.relative_to(ASSET).as_posix(),'preview':preview.relative_to(ASSET).as_posix(),
        'approval':'reference-only; skill-motion-pending','gameIntegration':'not-deployed','motionId':'hammer_default',
        'referenceFrom':path.relative_to(ASSET).as_posix(),'startEndSame':True,'motionCanvas':[6144,3072]}
    next(x for x in catalog['assets'] if x['id']==asset['id'])['fileCount']=len(asset['files'])
    branch()
    for p in metadata:assert p.read_bytes()==snapshots[p],'Metadata edited concurrently: '+str(p)
    for p,d in zip(metadata,[asset,catalog,generated]):dump(p,d)
    store=Store(ROOT)
    for p in files:assert store.read({'asset':asset['id'],'path':p.relative_to(ROOT).as_posix()})==p.read_bytes()
    for suffix in ['6144x3072','4096x2048']:
        for bg in ['white','transparent']:
            assert (preview/f'start-{suffix}-{bg}.png').read_bytes()==(preview/f'end-{suffix}-{bg}.png').read_bytes()
    # At exact scale eight, all RGBA input pixels must be recoverable.
    assert wide.resize(cropped.size,Image.Resampling.NEAREST).tobytes()==cropped.tobytes()
    assert hashlib.sha256(path.read_bytes()).hexdigest()==original_digest
    assert designpath.read_bytes()==designraw and unitypath.read_bytes()==unityraw
    branch();print(json.dumps({'preview':str(preview),'prompt':str(source/'aether-prompt-en.txt'),'registeredFiles':len(files),
        'canvas':[6144,3072],'visibleCharacterPixels':mapping['visibleCharacterAndHammerLarge'],'sameStartEnd':True},ensure_ascii=False))


if __name__=='__main__':main()
