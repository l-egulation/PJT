"""Deterministically pixelize approved static art and package Eter reference PNGs.

The user explicitly requested pixel conversion of existing images. No animation
or pose is invented in this script: only three built-in imagegen pose edits are
resampled. Five other silhouettes remain pixel-for-pixel unchanged.
"""
import hashlib, json, shutil, zipfile
from html import escape
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageChops
from scipy.ndimage import label, find_objects, convolve, binary_dilation
from pixel_base_prepare import ROOT, OUT, OLD, MOTION, SOURCE
from idle_motion_build import root_x

SPEC = ROOT/'data/presentation/stage2_pixel_base.json'
N = Image.Resampling.NEAREST
FONT = 'C:/Windows/Fonts/malgun.ttf'

def dump(p,data):p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def font(n):return ImageFont.truetype(FONT,n)

def extract(p,orb):
    a=np.array(Image.open(p).convert('RGBA'))
    labs,_=label(a[:,:,3]>=245)
    regions=[]
    for i,b in enumerate(find_objects(labs),1):
        if b is not None:regions.append((int((labs[b]==i).sum()),i,b))
    regions.sort(reverse=True)
    assert regions[0][0]>1000
    keep=[regions[0][1]]
    if orb:
        # Single new blue pose: detached bead is the second substantial component.
        extras=[r for r in regions[1:] if r[0]>150]
        assert len(extras)==1,[(r[0],r[2]) for r in extras]
        keep.append(extras[0][1])
    mask=np.isin(labs,keep)
    clean=np.zeros_like(a);clean[mask,:3]=a[mask,:3];clean[mask,3]=255
    im=Image.fromarray(clean);bbox=im.getbbox()
    return im.crop(bbox),list(bbox)

def pixelize(im,colors):
    """Nearest fixed palette, no dithering; preserve all occupied native pixels."""
    a=np.array(im);mask=a[:,:,3]>0
    palette=np.array([tuple(bytes.fromhex(c)) for c in colors],dtype=np.int32)
    rgb=a[mask,:3].astype(np.int32)
    distances=((rgb[:,None,:]-palette[None,:,:])**2).sum(axis=2)
    inds=distances.argmin(axis=1)
    index=np.full(mask.shape,-1,dtype=np.int16);index[mask]=inds
    # Flatten noisy generated shading inside material masses. Never move alpha
    # or smooth the silhouette; preserve dark face openings and tiny amber eyes.
    protected={0}
    if 'eaaa16' in colors:protected.add(colors.index('eaaa16'))
    interior=convolve(mask.astype(np.uint8),np.ones((3,3),dtype=np.uint8),mode='constant')==9
    for _ in range(2):
        votes=np.stack([convolve((index==i).astype(np.uint8),np.ones((3,3),dtype=np.uint8),mode='constant') for i in range(len(colors))])
        winner=votes.argmax(axis=0);best=votes.max(axis=0)
        own=np.take_along_axis(votes,np.maximum(index,0)[None,:,:],axis=0)[0]
        change=interior&(best>=5)&(own<=3)&~np.isin(index,list(protected))
        index[change]=winner[change]
    snapshot=index.copy()
    for color in range(len(colors)):
        if color in protected:continue
        labs,_=label(snapshot==color)
        for component,box in enumerate(find_objects(labs),1):
            if box is None:continue
            region=labs==component;area=int(region.sum())
            if area>10:continue
            border=binary_dilation(region)&mask&~region
            counts=np.bincount(snapshot[border],minlength=len(colors))
            if counts.max()==0:continue
            target=int(counts.argmax())
            # Do not spread ink into colored edge highlights or thin material tips.
            if target in protected:continue
            index[region]=target
    a[mask,:3]=palette[index[mask]];a[~mask]=0;a[mask,3]=255
    return Image.fromarray(a),palette

def indexed(im,colors,path):
    a=np.array(im);pal=[(0,0,0)]+[tuple(bytes.fromhex(c)) for c in colors]
    ids=np.zeros(a.shape[:2],dtype=np.uint8)
    for i,c in enumerate(pal[1:],1):ids[(a[:,:,3]>0)&np.all(a[:,:,:3]==c,axis=2)]=i
    out=Image.fromarray(ids).convert('P')
    flat=[v for c in pal for v in c];out.putpalette(flat+[0]*(768-len(flat)))
    out.save(path,transparency=0)
    assert np.array_equal(np.array(Image.open(path).convert('RGBA')),a)

def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));cw,ch=spec['canvas'];base=spec['baseline']
    manifest=[];sprites={};checks=[]
    for m in spec['assets']:
        ident=m['id'];p=OUT/ident
        for folder in ['references','native','eter','previews']:(p/folder).mkdir(parents=True,exist_ok=True)
        oldframe=OLD/ident/'native/idle-frame.png'
        shutil.copy2(oldframe,p/'references/previous-frame.png')
        if m['newPose']:
            source=p/'raw/generated.png';im,bbox=extract(source,ident=='I4-blue')
            im=im.resize((round(im.width*m['height']/im.height),m['height']),N)
            if im.getbbox()!=(0,0,im.width,im.height):
                # Keep physical height exact if nearest sampling misses an isolated extreme pixel.
                im=im.crop(im.getbbox());im=im.resize((round(im.width*m['height']/im.height),m['height']),N)
        else:
            source=OLD/ident/'native/idle-cropped.png';im=Image.open(source).convert('RGBA');bbox=list(im.getbbox())
            assert im.height==m['height']
            shutil.copy2(OLD/ident/'raw/generated.png',p/'references/original-static-generation.png')
            shutil.copy2(OLD/ident/'prompt.txt',p/'references/original-static-prompt.txt')
        shutil.copy2(source,p/'references/pixelization-input.png')
        im.save(p/'references/native-before-palette.png')
        before=np.array(im);px,palette=pixelize(im,m['palette']);sprites[ident]=px
        assert np.array_equal(before[:,:,3],np.array(px)[:,:,3])
        px.save(p/'native/base-cropped.png')
        previous=Image.open(oldframe).convert('RGBA');rb=previous.getbbox()
        referenceX=rb[0]+root_x(previous.crop(rb),ident)
        x=round(referenceX-root_x(px,ident));y=base-px.height
        assert x>=0 and x+px.width<=cw and y>=0
        frame=Image.new('RGBA',(cw,ch));frame.alpha_composite(px,(x,y));frame.save(p/'native/base-frame.png')
        indexed(frame,m['palette'],p/'native/base-indexed.png')
        indexed(px,m['palette'],p/'native/base-cropped-indexed.png')
        for factor in spec['exportScales']:
            frame.resize((cw*factor,ch*factor),N).save(p/'eter'/f'{ident}-base-frame-{factor}x.png')
            px.resize((px.width*factor,px.height*factor),N).save(p/'eter'/f'{ident}-base-cropped-{factor}x.png')
        a=np.array(px);used=sorted({tuple(c) for c in a[a[:,:,3]>0,:3]})
        dump(p/'palette.json',{'colors':['#'+c for c in m['palette']],'opaqueColorsUsed':len(used),'transparentIndex':0})
        gpl='GIMP Palette\nName: '+ident+'\nColumns: 4\n#\n'+'\n'.join(f'{r:3} {g:3} {b:3}\tcolor-{i+1}' for i,(r,g,b) in enumerate(palette))+'\n'
        (p/'palette.gpl').write_text(gpl,encoding='utf-8')
        row={**m,'source':str(source.relative_to(ROOT)),'sourceSha256':sha(source),'sourceBBox':bbox,'visibleSize':list(px.size),'canvas':[cw,ch],'baseline':base,'artOrigin':[x,y],'supportAnchor':[referenceX,base],'alphaValues':sorted(set(a[:,:,3].ravel().tolist())),'opaqueColorsBefore':len(np.unique(before[before[:,:,3]>0,:3],axis=0)),'opaqueColorsUsed':len(used),'frameSha256':sha(p/'native/base-frame.png'),'indexedSha256':sha(p/'native/base-indexed.png'),'newAnimationFrames':0}
        dump(p/'metadata.json',row);manifest.append(row)
        assert px.height==m['height'] and len(used)<=len(m['palette'])
        for factor in spec['exportScales']:
            large=np.array(Image.open(p/'eter'/f'{ident}-base-frame-{factor}x.png'))
            assert np.array_equal(large,np.repeat(np.repeat(np.array(frame),factor,axis=0),factor,axis=1))
        if not m['newPose']:
            assert px.size==im.size and np.array_equal(np.array(px)[:,:,3],before[:,:,3])
        checks.append({'id':ident,'exactHeight':True,'alphaUnchangedByPalette':True,'fixedPalette':True,'integerScaleBlocksExact':True,'indexedRgbaExact':True,'oldPoseSilhouettePreserved':not m['newPose']})
    board=Image.new('RGBA',(1920,1500),'#26313b');draw=ImageDraw.Draw(board)
    for i,m in enumerate(manifest):
        x=i%4*480;y=i//4*750;im=sprites[m['id']];factor=min(4,440//im.width,600//im.height)
        draw.text((x+20,y+18),f'{m["id"]} {m["name"]}',font=font(22),fill='#eee4d2')
        draw.text((x+20,y+52),f'{im.width}×{im.height}px · {m["opaqueColorsUsed"]}색 · {factor}배 표시',font=font(17),fill='#bfbdaf')
        shown=im.resize((im.width*factor,im.height*factor),N)
        board.alpha_composite(shown,(x+(480-shown.width)//2,y+690-shown.height))
    board.convert('RGB').save(OUT/'base-overview.png')
    # Same-scale roster; no per-character fitting or player redesign.
    roster=Image.new('RGBA',(960,540),'#26313b');d=ImageDraw.Draw(roster)
    playerfile=OUT/'references/assets/approved-player.png';player=Image.open(playerfile).convert('RGBA');player=player.crop(player.getbbox())
    d.text((18,12),'1단계 픽셀 기본본 / 모두 같은 1배율 · 플레이어 유효 높이64px',font=font(20),fill='#eee4d2')
    for ids,foot in [(['K4','B3','M3','H3','R-selected'],255),(['A3','I4-rose','I4-blue'],495)]:
        x=20;roster.alpha_composite(player,(x,foot-player.height));x+=90
        for ident in ids:
            im=sprites[ident];roster.alpha_composite(im,(x,foot-im.height));d.text((x,foot+8),ident,font=font(13),fill='#e1bf8b');x+=im.width+18
        assert x<960
    roster.convert('RGB').save(OUT/'native-roster.png')
    # Existing backgrounds include approved player; do not recolor or scale them.
    groups=[('melee',['K4','B3','M3'],[(65,286),(650,384),(430,286)]),('ranged-elite',['H3','R-selected','A3'],[(360,286),(480,286),(650,384)]),('boss',['I4-rose','I4-blue'],[(410,286),(680,384)])]
    scene=Image.open(OUT/'references/C-game.png').convert('RGBA')
    assert scene.size==(960,540)
    for name,ids,places in groups:
        result=scene.copy();coverage=Image.new('1',scene.size)
        for ident,(center,foot) in zip(ids,places):
            im=sprites[ident];xy=(center-im.width//2,foot-im.height)
            result.alpha_composite(im,xy)
            occupied=Image.new('1',scene.size);occupied.paste(im.getchannel('A').convert('1'),xy)
            coverage=ImageChops.logical_or(coverage,occupied)
        result.save(OUT/f'map-{name}.png')
        unoccupied=~np.array(coverage);assert np.array_equal(np.array(result)[unoccupied],np.array(scene)[unoccupied])
    # Pixel inspection card at integer scale; plain swatches test dark-background contrast.
    swatches=['#203134','#476374','#090e15'];read=Image.new('RGBA',(1920,960));dr=ImageDraw.Draw(read)
    for j,color in enumerate(swatches):
        dr.rectangle((0,j*320,1919,(j+1)*320-1),fill=color)
        for i,m in enumerate(manifest):
            im=sprites[m['id']];factor=1 if im.width>210 else 2;shown=im.resize((im.width*factor,im.height*factor),N)
            read.alpha_composite(shown,(i*240+(240-shown.width)//2,(j+1)*320-20-shown.height))
    read.save(OUT/'contrast-swatches.png')
    dump(OUT/'manifest.json',manifest);shutil.copy2(SPEC,OUT/'pixel-base-spec.json')
    dump(OUT/'validation.json',{'checks':checks,'mapBackgroundOutsideArtUnchanged':True,'playerOriginalSha256':sha(playerfile),'userSizeSha256':sha(SOURCE/'user-size-reference.json'),'newPoseCount':3,'staticBaseCount':8,'newAnimationFrames':0,'browserOrUnityValidation':False})
    page(manifest,spec)
    (OUT/'README.md').write_text('''# 픽셀 기본본 1단계

`index.html`에서 확인. 정지 기본본8장이며 대기·이동 모션이나 실제 게임 캡처가 아니다.

에테르 입력은 각 개체 `eter/ID-base-frame-4x.png`를 우선 사용. 더 큰 입력이 필요하면8x. 모두 같은384×224 논리 캔버스, 기준선192를 유지하며 확대본에는 배경·문자·격자가 없다. 개별 크롭 파일도 제공하나 연속 프레임 정렬에는 고정 캔버스를 권장한다.4x는1536×896,8x는3072×1792. 확대본1픽셀 블록은 각각4×4/8×8이다. 원본으로 돌릴 때는 정확한 역배율 최근접 축소를 사용한다. 에테르가 새로 그린 결과는 색/실루엣 보존 여부를 다시 확인해야 한다.

`native/base-indexed.png`는 실제 편집용 제한 팔레트 PNG, `base-frame.png`는 동일 RGBA. 투명/불투명만 있으며 안티앨리어싱·디더링 없이1px 격자에 고정했다. 원본 생성 그림과 프롬프트는 raw/references에 보존한다. 자세 변경3명만 새로 생성했고 다른5명은 정지 원본의 점유 픽셀을 그대로 유지한다.

투척수106px는 기존154px의 머리 위 물건을 내린 결과다. 남매102/127px는 분리 작업값. 사용자 크기 JSON과 승인 플레이어는 무변경이다.

다음 대기는 이 기본본을 복제하여 얼굴·어깨·주름 등 선택 영역만1px 단위로 조정한다. 발 접점과 장비 길이를 고정하고 매 프레임 재생성하지 않는다. 이번 묶음에는 새 대기 프레임이 없다. 기존 생성 대기 모션은 현재 제작 기준에서 제외하고 원본만 보존한다.
''',encoding='utf-8')
    bundle=OUT/'source';bundle.mkdir(exist_ok=True)
    for name in ['pixel_base_build.py','pixel_base_prepare.py','user-size-reference.json']:
        shutil.copy2(SOURCE/name,bundle/name)
    # Snapshot only the minimal dependencies used by the builder.
    for name in ['idle_motion_build.py','idle_motion.py','style_unification.py']:
        shutil.copy2(SOURCE/name,bundle/name)
    for name in ['stage2_pixel_base.json','stage2_idle_study.json']:
        shutil.copy2(ROOT/'data/presentation'/name,bundle/name)
    zpath=OUT/'pixel-base-step1-20261031.zip'
    with zipfile.ZipFile(zpath,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=zpath and '__pycache__' not in p.parts:z.write(p,str(p.relative_to(OUT)))
    print(json.dumps({'assets':[{k:m[k] for k in ['id','visibleSize','opaqueColorsBefore','opaqueColorsUsed']} for m in manifest],'zipBytes':zpath.stat().st_size},ensure_ascii=False))

def page(rows,spec):
    cards=[]
    for m in rows:
        i=m['id'];w,h=m['visibleSize'];colors=''.join(f'<span title="#{c}" style="background:#{c}"></span>' for c in m['palette'])
        cards.append(f'''<section id="{i}"><h2>{escape(m['name'])} <small>{i} · {escape(m['role'])}</small></h2><p>{escape(m['note'])}</p><p><b>{w}×{h}px</b> 유효 그림 · {m['opaqueColorsUsed']}색 · 투명/불투명만</p><div class="art"><img src="{i}/native/base-cropped.png" style="width:{w*4}px;height:{h*4}px" alt="{escape(m['name'])} 픽셀 기본자세"></div><div class="palette">{colors}</div><p class="links"><a download href="{i}/eter/{i}-base-frame-4x.png">에테르용4배 PNG</a><a download href="{i}/eter/{i}-base-frame-8x.png">8배 PNG</a><a download href="{i}/native/base-indexed.png">1배 편집용 PNG</a><a download href="{i}/eter/{i}-base-cropped-8x.png">여백 없는8배</a></p><details><summary>다음 대기에서 움직일 부분</summary><p>{escape(m['idlePlan'])}</p><p>아직 제작하지 않음. 이 기본본의 픽셀을 복제·국소 이동하는 방식으로 진행.</p></details></section>''')
    refs=''.join(f'<li><a href="{r["url"]}">{escape(r["title"])}</a> — {escape(r["application"])}</li>' for r in spec['references'])
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>1단계 · 픽셀 기본자세8명</title><style>
*{box-sizing:border-box}body{margin:0;background:#151d25;color:#e6e5dd;font:16px/1.65 system-ui,sans-serif}main{max-width:1280px;margin:auto;padding:36px 24px}h1{font-size:32px;margin:0 0 12px}h2{font-size:24px}small{font-size:15px;color:#b1c5ca;display:block}a{color:#edc187;text-underline-offset:4px}.intro,section{padding:24px;background:#23313b;border:1px solid #41515a;border-radius:12px;margin-bottom:24px}.tag{color:#dec6a0}nav{display:flex;gap:16px;flex-wrap:wrap;margin:18px 0}img{image-rendering:pixelated}img.board{max-width:100%;height:auto}.scroll{overflow:auto}.native{width:960px;height:540px;max-width:none}.art{overflow:auto;background:repeating-conic-gradient(#34414b 0% 25%,#2a3741 0% 50%) 0/24px 24px;padding:24px;text-align:center;min-height:260px}.art img{vertical-align:bottom;max-width:none}.links{display:flex;gap:20px;flex-wrap:wrap}.palette{display:flex;margin-top:16px}.palette span{display:block;width:28px;height:24px}details{border-top:1px solid #45545e;padding-top:12px}button{background:#e7c794;border:0;color:#1c2830;border-radius:6px;padding:8px 16px;cursor:pointer}.maps{display:flex;gap:10px;margin-bottom:12px}li{margin-bottom:12px}
</style><main><p class="tag">잉크 도시 아래 지하수로 · 정지 기본본 검토</p><h1>1단계 — 픽셀 기본자세8명</h1><div class="intro"><p>등짐 분사수·남매는 앞면이 보이는 사선 자세. 나머지5명은 기존 정지 자세와 실루엣을 유지했습니다. 각 그림을 실제 크기의1px 격자·제한 팔레트로 정리했습니다.</p><p><b>이번 단계는 기본 그림만 제공합니다.</b> 이전 생성 대기 루프는 제작 기준에서 제외했습니다. 다음 대기는 이 그림의 얼굴·어깨·옷자락 일부 픽셀을 움직여 만듭니다.</p><p><a download href="pixel-base-step1-20261031.zip">전체 다운로드</a> · <a href="README.md">전달 규격</a> · <a href="manifest.json">개체별 치수·팔레트</a></p><p>에테르에는 <b>4배 고정 캔버스 PNG</b>부터 넣으면 됩니다.8배도 제공합니다. 격자와 글씨는 다운로드 PNG에 없습니다. 원본384×224 / 4배1536×896 / 8배3072×1792. 이미지 자체를 다시 매끈하게 확대하지 않습니다.</p></div><nav>'''+''.join(f'<a href="#{m["id"]}">{escape(m["name"])}</a>' for m in rows)+'''</nav><h2>같은 실제 배율로 비교</h2><p>960×540 / 플레이어 그림66×64. 캔버스 여백과 몸 크기를 구분했습니다. 아래는 모두1배입니다.</p><div class="scroll"><img class="native" src="native-roster.png" alt="실제 배율 비교"></div><h2>기존 맵 합성</h2><p>원래 C구역 위에 놓은 디자인 합성 미리보기이며 실제 게임 캡처가 아닙니다. 원경·벽·검정 구조물 주변에서 색을 확인합니다. 배경의 밝기·색·선명도를 바꾸지 않았으며 검토용으로 몬스터를 위에 표시했습니다.</p><div class="maps"><button data-map="melee">근거리3명</button><button data-map="ranged-elite">원거리2명·엘리트</button><button data-map="boss">남매</button></div><div class="scroll"><img id="map" class="native" src="map-melee.png" alt="기존 맵 위 합성"></div><h2>개별 기본본 · 4배 표시</h2>'''+''.join(cards)+'''<h2>참고와 다음 제작 기준</h2><ul>'''+refs+'''</ul><p>기본본을 먼저 검토한 뒤 발·무기의 기준을 고정하고 국소 영역만 정수 픽셀로 움직입니다. 에테르가 만든 이동 결과도 이 기본본과 크기·색·발 기준을 다시 비교합니다. Unity 적용·전투 검증은 이번 단계에 포함하지 않습니다.</p></main><script>document.querySelectorAll('[data-map]').forEach(b=>b.addEventListener('click',()=>{document.getElementById('map').src='map-'+b.dataset.map+'.png'}));</script></html>'''
    (OUT/'index.html').write_text(html,encoding='utf-8')

if __name__=='__main__':build()
