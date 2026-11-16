"""Prepare built-in imagegen edits and package single idle-pose references."""
from pathlib import Path
from html import escape
import argparse, json, shutil, zipfile, re
from PIL import Image, ImageDraw, ImageFont
import numpy as np
from scipy.ndimage import label, find_objects
from style_unification import ROOT, SOURCE, FONT, sha

PRIOR = ROOT / 'outputs/waterway/user-sized-redraw-20261031'
OUT = ROOT / 'outputs/waterway/idle-reference-8-20261031'
ITEMS = {
 'I4-rose': {'name':'남매·자주색 전방', 'role':'보스·전방 견제', 'reference':'I4', 'height':102, 'pose':'Extract ONLY the shorter ROSE figure from Image1 and reconstruct its entire independent body. One rose-hooded humanoid, warm bone angular mask with horizontal dark eye opening, ragged dusty rose tunic, cream wrapped shins, brown boots and narrow black-ink waist. LEFT-facing side-profile calm planted idle, knees softly bent but NOT a lunge. This front sibling now supports the original SINGLE shared crossbow ALONE at waist height in a lowered horizontal carry, hands gripping it naturally without aiming, drawing the string or attacking. Preserve the ivory-brown crossbow design and its long forward span. Complete the formerly occluded torso, two arms, two legs and back plausibly. REMOVE the blue figure and ALL inter-body rib bridge; no half blue limbs, no severed edges and no hanging connector reaching off-canvas. No new armor, animal traits or weapon. Whole visible figure about102px tall; the bow extends sideways, not overhead.', 'note':'분리된 자주색 몸·석궁 휴대 대기. 기존 남매 합본127px 안의 키 차이를 참고한 개인102px 작업값이며 사용자 JSON의 별도 승인값은 아니다.'},
 'I4-blue': {'name':'남매·푸른색 후방', 'role':'보스·장전 및 사격 주도', 'reference':'I4', 'height':127, 'pose':'Extract ONLY the taller BLUE figure from Image1 and reconstruct its entire independent body. Exactly one blue-hooded humanoid, warm bone angular mask with horizontal dark eye opening, ragged faded blue tunic, dark waist, cream wrapped shins and brown boots. LEFT-facing pure side-profile calm planted idle, slight knee bend. Two arms relaxed with visible empty hands in front of the waist; it is the loading/rear support sibling AT REST, not reaching for an invisible bow and not holding a second crossbow or extra prop. Complete the formerly hidden abdomen, both forearms, two legs and back. REMOVE the rose figure, shared crossbow and ALL inter-body rib bridge. No severed edge or dangling connector reaching off-canvas. No new armor, animal traits, quiver or weapon. Whole visible figure127px tall, preserving the taller sibling identity.', 'note':'분리된 푸른색 몸·빈손 대기. 합본의 최고점127px를 개인 키 작업 기준으로 사용. 석궁은 전방 인물만 휴대하며 전투 시 공유한다.'},
 'K4': {'name':'등짐분사수', 'role':'근거리·접근 분사', 'reference':'K4', 'height':112, 'pose':'Change ONLY the pose into a calm grounded idle. Keep the lean slightly hunched body, coral jacket, grey wrapped legs, ONE tall narrow cream back tank, steel face plate with single narrow eye inset, and single short hose. Two feet planted with relaxed knees, not striding. Both hands naturally carry the nozzle lower at waist level with its mouth angled slightly downward and LEFT, not actively aiming. No spray, pressure swell, ink drops or attack anticipation. Preserve the back tank height and body scale; visible height112px.', 'note':'노즐을 낮춘 대기. 단일 등통·호스 유지. 이동 때 다리와 몸의 관계를 확인할 기준 자세.'},
 'B3': {'name':'쐐기보행기', 'role':'큰 근거리·전진 압박', 'reference':'B3', 'height':100, 'pose':'Change ONLY the pose to a motionless weight-bearing idle close to the reference. The low long ivory forward wedge, copper supports and tall muted eucalyptus rear plates stay identical. Preserve EXACTLY FIVE visible ground-contact feet including the small inner BROWN support behind the large copper leg. All five rest on one baseline. Do not merge, remove or add a contact. Body is at rest: no raised front shell, no crouched charge windup, no forward lunge. No animal eyes, skull anatomy or tail. Visible height100px, long wide proportions unchanged.', 'note':'다섯 접점이 같은 기준선에 놓인 하중 대기. 돌진·찍기 전조와 구분.'},
 'M3': {'name':'먹침필경사', 'role':'근거리·긴 찌르기', 'reference':'M3', 'height':59, 'pose':'Change ONLY the pose into a stationary LOW crouched idle. Preserve the mauve tented low torso, wine-plum bent legs, warm bone vertical face plate and extremely long thin SOLID stabbing mouth-plate pointing LEFT, with the same leg/appendage count. Legs are uncrossed and quietly braced; no walking step and no thrust. Keep the needle attached to the face, resting horizontally without extension, NOT a projectile, firearm or muzzle. No new arms, face or eyes. Visible height59px; preserve the very wide low overall aspect and tiny body compared to the long thin needle.', 'note':'발 교차 전의 낮은 대기. 긴 침을 포함한 전체 폭 유지. 이동 중 침을 발사체로 바꾸지 않는다.'},
 'H3': {'name':'늘어진수의', 'role':'원거리·잉크 공 투척', 'reference':'H3', 'height':151, 'pose':'Change ONLY the pose to a calm ominous hovering idle, not an attack. Preserve the very long narrow legless tapered ivory shroud, dusty mulberry inner folds, cool slate shadow planes, ochre T-slot mask and exactly two thin black-ink hands. LOWER the projecting hand: both hands hang loosely near waist/hip height separated slightly from the cloth so fingers are readable. No extended casting arm, gathered hands, ink orb or charging effect. The long cloth tip hangs vertically, no ground shadow or added feet. Face points LEFT in strict side profile. Visible height151px; do not make it broader or redesign its mask.', 'note':'두 손을 내린 부유 대기. 발 없이 수의 끝을 정렬 기준으로 쓰며 지면 보행 캐릭터로 바꾸지 않는다.'},
 'R-selected': {'name':'신호투척수', 'role':'원거리·물체 투척', 'reference':'R-selected', 'height':106, 'pose':'Change ONLY the attack-like pose into relaxed grounded idle. Keep the SAME short broad body, cream cap and dark face opening, two small warm eyes, orange apron, muted violet sleeves, warm grey trousers and ordinary boots. LOWER the raised arm and carry the SAME ochre canister with one hand beside the hip, below the cap; other hand relaxed. Two feet planted, elbows loosely bent, no windup or throwing motion. Exactly one canister, no new weapon or backpack. CRITICAL physical scale: the OLD total154px included the raised canister, but cap-to-soles BODY was about106px. The NEW idle whole figure should be about106px because the canister is lowered. Do NOT enlarge the body to154px or make the canister overhead just to fill the old bounds. Body proportions and physical body scale are unchanged.', 'note':'물건을 내린 대기106px. 이전154px는 머리 위 물건 포함 높이였으므로 몸의 크기는 유지하고 윗부분을 빈 여백으로 남긴다.'},
 'A3': {'name':'편측거구', 'role':'엘리트', 'reference':'A3', 'height':155, 'pose':'Change ONLY the pose to a stable heavy idle. Keep the wide bent legs and asymmetrical torso, honey-ochre broad upper garment/body, small pale inset face, sage-grey trousers, one HUGE slate-blue arm on image LEFT and a tiny right arm. The giant arm hangs HEAVILY down beside the front knee with a relaxed closed fist just above the foot baseline; no arm lifted to punch and no crouched rush. Small arm rests at the other side. Two feet grounded with slight knee bend, same width and mass, no extra spikes, armor or muscles. Face points LEFT. Visible height155px.', 'note':'큰 팔을 낮게 늘어뜨린 무게감 있는 대기. 작은 팔을 확대하거나 좌우 팔 비대칭을 없애지 않는다.'}
}

COMMON = '''Use case: identity-preserve / pixel-art pose edit.
Asset: ONE isolated STATIC IDLE KEY POSE for a2D side-scrolling pixel-action game. NOT an animation or sprite sheet. User will make locomotion frames separately.
Inputs: Image1 EDIT TARGET is the selected monster; preserve its identity, individual colors, materials, anatomy/appendage count and established proportions except the requested pose or the explicitly requested sibling separation. Image2 approved player is STYLE/PIXEL-DENSITY reference only: actual art66x64px in192x160 transparent canvas; do not redraw or insert player. Image3 sewer is READABILITY/palette reference only, never include background.
Facing: pure LEFT-facing side view used in side-scrolling gameplay, never three-quarter, front-facing or overhead. Full body and every held tip visible, comfortable true-transparent margins on all four sides; no detached duplicate equipment or alternate views.
Style: crisp economical outline-free pixel COLOR MASSES, limited2-3 broad flat shades per material, hard stair-step edges at the stated working pixel scale. This output may show the pixel drawing enlarged for inspection; use proportionate COARSE clusters for the target visible height, not fine illustration detail. Preserve warm broad bone/ochre/coral/mauve planes and cool grey-blue shadows, charcoal-violet ink internal voids. No black or colored perimeter strokes, no thin dark border tracing fabric/limbs/weapon, no bright rim or neon glow. Material fill touches transparent edge directly. Dark ink is broad internal anatomy/face/contact mass, not an outline. NO gradients, smoothing, anti-aliased fringe, realistic cloth,3D, vector curves, tiny texture or added details.
Mood: ink-contaminated neglected city sewer, not an animal redesign or friendly maintenance worker. Quiet idle, no attack windup, jump, cast, projectile, effect, smoke, foot dust, background, ground, cast shadow, grid, text, label, decorative frame or watermark. Exactly one character. True TRANSPARENT background.
'''


def prepare():
    OUT.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PRIOR/'references', OUT/'references', dirs_exist_ok=True)
    for ident,m in ITEMS.items():
        p=OUT/ident
        for sub in ['raw','references','native','enlarged','previews']: (p/sub).mkdir(parents=True,exist_ok=True)
        shutil.copy2(PRIOR/m['reference']/'cutouts/style-enlarged.png',p/'references/edit-target.png')
        prompt=COMMON+'\nSpecific required edit: '+m['pose']
        (p/'prompt.txt').write_text(prompt,encoding='utf-8')
        (SOURCE/f'idle-{ident}-prompt.txt').write_text(prompt,encoding='utf-8')
    print(json.dumps({'out':str(OUT),'assets':list(ITEMS)},ensure_ascii=False))


def resize_visible(im,height):
    im=im.crop(im.getchannel('A').getbbox())
    for _ in range(3):
        im=im.resize((max(1,round(im.width*height/im.height)),height),Image.Resampling.NEAREST)
        im=im.crop(im.getchannel('A').getbbox())
        if im.height==height:return im
    raise ValueError('visible height resampling did not converge')


def build():
    font=lambda n:ImageFont.truetype(FONT,n)
    manifest=[];sprites={};CW,CH,BASE=384,224,192
    for ident,m in ITEMS.items():
        p=OUT/ident;raw=Image.open(p/'raw/generated.png').convert('RGBA')
        assert raw.getchannel('A').getextrema()[0]==0,ident
        pixels=np.array(raw);labels,_=label(pixels[:,:,3]>=245)
        regions=sorted([(int((labels[b]==i).sum()),i,b) for i,b in enumerate(find_objects(labels),1)],reverse=True)
        area,component,box=regions[0];assert area>1000
        core=np.zeros_like(pixels);selected=labels==component;core[selected,:3]=pixels[selected,:3];core[selected,3]=255
        core_image=Image.fromarray(core);core_image=core_image.crop(core_image.getbbox())
        im=resize_visible(core_image,m['height']);w,h=im.size
        assert w<=CW-32 and h<=BASE-16,(ident,im.size)
        im.save(p/'native/idle-cropped.png');sprites[ident]=im
        frame=Image.new('RGBA',(CW,CH));x=(CW-w)//2;y=BASE-h;frame.alpha_composite(im,(x,y));frame.save(p/'native/idle-frame.png')
        for scale in [4,8]:
            im.resize((w*scale,h*scale),Image.Resampling.NEAREST).save(p/'enlarged'/f'idle-{scale}x.png')
            frame.resize((CW*scale,CH*scale),Image.Resampling.NEAREST).save(p/'enlarged'/f'idle-frame-{scale}x.png')
        row={'id':ident,'name':m['name'],'role':m['role'],'status':'idle-key-pose-candidate','visibleSize':list(im.size),'frameCanvas':[CW,CH],'registrationPoint':[CW//2,BASE],'artOrigin':[x,y],'registrationNote':'BBox-center/bottom reference only, not a Unity foot pivot or hurtbox. H3 bottom is cloth tip. Preserve this canvas and registration across the user-produced frames.','alphaDerivation':'Largest connected alpha>=245 core; RGB unpainted. Original generated RGBA preserved in raw/.','note':m['note'],'rawSha256':sha(p/'raw/generated.png'),'inputSha256':sha(p/'references/edit-target.png'),'inputSource':str(PRIOR/m['reference']/'cutouts/style-enlarged.png')}
        manifest.append(row)
        preview=Image.new('RGBA',(900,800),'#24313b');d=ImageDraw.Draw(preview);d.text((24,20),f'{ident} {m["name"]} · 대기 기준',font=font(25),fill='#eeeee8')
        scale=min(4,820/w,650/h);shown=im.resize((round(w*scale),round(h*scale)),Image.Resampling.NEAREST);preview.alpha_composite(shown,((900-shown.width)//2,740-shown.height));preview.convert('RGB').save(p/'previews/idle-large.png')
    order=['K4','B3','M3','H3','R-selected','A3','I4-rose','I4-blue']
    board=Image.new('RGBA',(1800,1600),'#24313b');d=ImageDraw.Draw(board)
    for i,ident in enumerate(order):
        x,y=i%4*450,i//4*800;m=ITEMS[ident];im=sprites[ident];scale=min(4,414/im.width,620/im.height);shown=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.NEAREST)
        d.text((x+18,y+20),f'{ident} {m["name"]}',font=font(20),fill='#eeeee8');d.text((x+18,y+55),m['role'],font=font(17),fill='#d5b98a');board.alpha_composite(shown,(x+(450-shown.width)//2,y+720-shown.height))
    board.convert('RGB').save(OUT/'idle-board-enlarged.png')
    native=Image.new('RGBA',(960,540),'#24313b');d=ImageDraw.Draw(native);d.text((20,12),'대기 기준 8명 · 동일 1배율 / 플레이어 실제 그림66×64',font=font(20),fill='#eeeee8')
    player=Image.open(OUT/'references/assets/approved-player.png').convert('RGBA');player=player.crop(player.getbbox())
    for group,base in [(['K4','B3','M3','H3','R-selected'],230),(['A3','I4-rose','I4-blue'],495)]:
        x=24;native.alpha_composite(player,(x,base-64));d.text((x,base+8),'플레이어64',font=font(13),fill='#eeeee8');x+=90
        for ident in group:
            im=sprites[ident];native.alpha_composite(im,(x,base-im.height));d.text((x,base+8),f'{ident} {im.height}px',font=font(13),fill='#efc98d');x+=im.width+25
        assert x<=960,(group,x)
    native.convert('RGB').save(OUT/'native-roster.png')
    # Two separate transparent files are composed for size review only, not fused back into a boss asset.
    duo=Image.new('RGBA',(960,540),'#24313b');duo.alpha_composite(player,(110,350-64));duo.alpha_composite(sprites['I4-rose'],(300,350-sprites['I4-rose'].height));duo.alpha_composite(sprites['I4-blue'],(540,350-sprites['I4-blue'].height));ImageDraw.Draw(duo).text((24,22),'분리된 남매 · 동일1배율 · 공유 석궁은 전방 인물이 휴대',font=font(22),fill='#eeeee8');duo.convert('RGB').save(OUT/'siblings-native.png')
    readme='''# 대기 기준 이미지 · 8명

일반 근거리3·원거리2, 엘리트1, 독립된 남매2의 **정측면 대기 기준 자세 각1장**이다. 대기 루프·이동 프레임·스프라이트 시트·Unity 적용은 제작하지 않았다. 기본 image_gen 편집 원본과 입력·프롬프트를 보존한다. 전달 PNG는 원본의 알파245 이상 연결 핵심 영역을 추출한 뒤 RGB 재채색 없이 최근접 축소·확대한 파생본이다.

## 이동 프레임 제작 입력

- 각 폴더 `enlarged/idle-8x.png`: 투명 배경의 그림만8배 최근접 확대. `idle-4x.png`도 제공한다.
- `native/idle-cropped.png`: 실제 크기 투명 PNG. `native/idle-frame.png`: 여백을 포함한384×224px 고정 캔버스. 확대 고정 캔버스는 `enlarged/idle-frame-8x.png`(3072×1792px)다.
- 같은 개체의 이동 프레임은 캔버스·기준점·키·무기 길이를 유지하고 프레임마다 자동 잘라내거나 몸을 다시 확대하지 않는다. 기준점은 캔버스(192,192)이며 그림 외곽의 중앙/하단 정렬용이다. 실제 Unity 발 피벗·몸 판정은 미설정이다.
- 프레임은 모두 왼쪽을 바라보고 투명 배경으로 제작한다. 테두리·미세 질감·새 장비를 추가하지 않는다. 발·손·지지대 수를 유지한다. H3는 발 없는 부유 이동, M3는 낮은 몸으로 발 교차, B3는 다섯 접점을 유지한다.
- 투척수는 들었던 물건을 내려 전체 높이106px로 보이지만 기존154px 그림에서 머리~발 몸 크기를 유지한 것이다. 154px로 다시 키우지 않는다.
- 남매는 자주색102px·푸른색127px의 분리 작업값이다. 기존 합본의 키 차이를 참고했으며 두 개인의 별도 크기 승인을 뜻하지 않는다. 원래 합본과 사용자 JSON은 보존한다.
- 남매는 대기 시 전방 인물만 석궁을 휴대하고 후방 인물은 빈손이다. 사격 시 함께 운용하는 방향이며 독립된 두 석궁을 새로 만들지 않는다. 기존 몸 사이 잉크 다리는 분리 이미지에서 제거했다.

프레임 수·FPS·이동속도·공격 판정은 이 기준 이미지로 확정하지 않는다. 반환한 이동 프레임을 기준으로 후속 정렬·루프 검토를 진행한다.
'''
    (OUT/'README.md').write_text(readme,encoding='utf-8')
    cards=[]
    for ident in order:
        m=ITEMS[ident];cards.append(f'<section id="{ident}"><h2>{ident} {escape(m["name"])} / {escape(m["role"])}</h2><p>{escape(m["note"])}</p><div class="art"><img src="{ident}/enlarged/idle-4x.png"></div><p><a href="{ident}/enlarged/idle-8x.png">그림8배 PNG</a> · <a href="{ident}/enlarged/idle-frame-8x.png">여백 포함8배 PNG</a> · <a href="{ident}/native/idle-cropped.png">실제 크기 PNG</a> · <a href="{ident}/raw/generated.png">생성 원본</a> · <a href="{ident}/prompt.txt">프롬프트</a></p></section>')
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>대기 기준8명 · 이동 프레임 제작용</title><style>body{background:#24313b;color:#eeeee8;margin:0;font:17px/1.8 "Malgun Gothic",sans-serif}main{max-width:1400px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:23px}a{color:#b0d8eb}.board{width:100%;height:auto}section{border-top:1px solid #566571;padding:24px 0}.art{overflow-x:auto;padding:20px 0}.art img{image-rendering:pixelated;max-width:none}.native{overflow-x:auto}.native img{max-width:none;image-rendering:pixelated}.note{border-left:4px solid #d5b98a;padding:0 18px}</style><main><h1>분리된 남매 + 몬스터 대기 기준8명</h1><p class="note">정측면 대기 기준 자세 각1장입니다. 이동 프레임·대기 루프는 제작하지 않았습니다. 개체별8배 투명 PNG와 여백 포함 고정 캔버스를 제공합니다. 확대 종합판은 각 칸에 맞춘 비교용이며 아래1배율에서 실제 크기를 확인하세요.</p><p><a href="idle-reference-8-20261031.zip">8명 전체 PNG·원본·참조·프롬프트 ZIP</a> · <a href="README.md">이동 프레임 제작 안내</a> · <a href="manifest.json">크기·정렬 기준</a></p><img class="board" src="idle-board-enlarged.png"><h2>동일1배율</h2><div class="native"><img src="native-roster.png" width="960" height="540"></div><p>투척수는 물건을 내리면서 전체 높이가 줄어들었고 몸 크기는 유지했습니다. 남매의 개인 높이는 합본을 참고한 분리 작업값입니다.</p>'''+''.join(cards)+'''<p>디자인·대기 기준 시안입니다. 최종 게임 캡처나 제작 완료 모션이 아닙니다. 테두리 없는 표현과 기존 형태·개체별 색을 유지하는 편집이며 이전 그림은 보존합니다.</p></main></html>'''
    (OUT/'index.html').write_text(page,encoding='utf-8')
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'source').mkdir(exist_ok=True);shutil.copy2(Path(__file__),OUT/'source/idle_references.py')
    shutil.copy2(SOURCE/'current-choices.json',OUT/'source/current-choices.json')
    shutil.copy2(ROOT/'data/combat/stage2_waterway_patterns.json',OUT/'source/combat-patterns.json')
    for row in manifest:
        ident=row['id'];assert sha(OUT/ident/'references/edit-target.png')==sha(Path(row['inputSource']))
        im=Image.open(OUT/ident/'native/idle-cropped.png');large=Image.open(OUT/ident/'enlarged/idle-8x.png');assert large.size==(im.width*8,im.height*8)
        assert large.resize(im.size,Image.Resampling.NEAREST).tobytes()==im.tobytes()
    archive=OUT/(OUT.name+'.zip')
    missing=[u for u in re.findall(r'(?:src|href)="([^"]+)"',page) if u!=archive.name and not (OUT/u).is_file()];assert not missing,missing
    config_hash=sha(SOURCE/'user-size-reference.json')
    assert config_hash=='a424c30cf9d13da134de87681b377b6cfdcb81e913fde95a8345ac7fc0d4f82d'
    (OUT/'validation.json').write_text(json.dumps({'assets':8,'singlePosePerAsset':True,'frameCanvas':[CW,CH],'nearestNeighborScales':[4,8],'eightTimesPixelRoundTrip':True,'priorArtInputHashesMatch':True,'userSizeJsonSha256':config_hash,'missingLinks':missing,'status':'Static idle reference candidates; no animation frames or runtime validation.'},ensure_ascii=False,indent=2),encoding='utf-8')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=archive:z.write(p,p.relative_to(OUT))
    print(json.dumps({'assets':len(manifest),'zip':str(archive),'bytes':archive.stat().st_size,'missingLinks':missing},ensure_ascii=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','build']);args=parser.parse_args()
    prepare() if args.command=='prepare' else build()
