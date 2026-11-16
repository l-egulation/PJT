"""Split generated idle keys, register clips without per-frame rescaling, publish."""
import base64, json, re, shutil, zipfile
from html import escape
import numpy as np
from scipy.ndimage import label, find_objects
from PIL import Image, ImageDraw, ImageFont
from idle_motion import OUT, PRIOR, ROOT, SOURCE, SPEC_PATH, spec, sha
from style_unification import FONT

N=Image.Resampling.NEAREST


def dump(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def split_characters(path,count,orb=False):
    raw=Image.open(path).convert('RGBA');a=np.array(raw);labs,_=label(a[:,:,3]>=245);regions=[]
    for i,b in enumerate(find_objects(labs),1):
        area=int((labs[b]==i).sum())
        if area>=32:regions.append({'area':area,'label':i,'box':[b[1].start,b[0].start,b[1].stop,b[0].stop]})
    bodies=sorted(regions,key=lambda r:r['area'],reverse=True)[:count]
    assert len(bodies)==count and min(r['area'] for r in bodies)>1000,path
    bodies.sort(key=lambda r:r['box'][0]);selected={r['label'] for r in bodies};result=[]
    for body in bodies:
        keep=[body['label']];x,y,x2,y2=body['box']
        if orb:
            possible=[r for r in regions if r['label'] not in selected and r['area']>150 and x-100< (r['box'][0]+r['box'][2])/2 <x+(x2-x)*.4 and y+(y2-y)*.1<(r['box'][1]+r['box'][3])/2<y+(y2-y)*.55]
            if possible:keep.append(max(possible,key=lambda r:r['area'])['label'])
        mask=np.isin(labs,keep);clean=np.zeros_like(a);clean[mask,:3]=a[mask,:3];clean[mask,3]=255
        im=Image.fromarray(clean);box=im.getbbox();result.append((im.crop(box),list(box)))
    return result


def root_x(im,ident):
    a=np.array(im);mask=a[:,:,3]>0
    if ident=='H3':
        c=a[:,:,:3].astype(float);ochre=mask&(np.indices(mask.shape)[0]<im.height*.35)&(c[:,:,0]>c[:,:,1]*1.12)&(c[:,:,1]>c[:,:,2]*1.2)
        if ochre.any():return float(np.where(ochre)[1].mean())
    region=mask[-3:,:].copy()
    if ident=='A3':region[:,:round(im.width*.38)]=False
    xs=np.where(region)[1]
    return float(xs.mean()) if len(xs) else im.width/2


def scale(im,factor):return im.resize((max(1,round(im.width*factor)),max(1,round(im.height*factor))),N)


def uri(path):return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode('ascii')


def build():
    data=spec();CW,CH=data['frameCanvas'];base=data['baseline'];font=lambda n:ImageFont.truetype(FONT,n)
    manifest=[];browser=[];motion_frames={};angle_images={}
    for m in data['assets']:
        ident=m['id'];p=OUT/ident;orb=ident=='I4-blue';rawparts=split_characters(p/'raw/idle.png',4,orb)
        for folder,pattern in [('frames','idle-*.png'),('enlarged','idle-*-8x.png')]:
            for stale in (p/folder).glob(pattern):
                assert stale.resolve().is_relative_to(OUT.resolve())
                stale.unlink()
        # Curate source keys; no independent resizing is used to conceal drawing drift.
        chosen=m.get('selectedSourceFrames',[0,1,2,3]);parts=[rawparts[i] for i in chosen]
        factor=m['height']/parts[0][0].height;frames=[];sizes=[];origins=[]
        registration_path=PRIOR/ident/'native/idle-frame.png'
        registration=Image.open(registration_path).convert('RGBA');rb=registration.getbbox()
        registration_x=rb[0]+root_x(registration.crop(rb),ident)
        shutil.copy2(registration_path,p/'references/registration-frame.png')
        for i,(im,bbox) in enumerate(parts):
            im=scale(im,factor);sizes.append(list(im.size));frame=Image.new('RGBA',(CW,CH));x=round(registration_x-root_x(im,ident));y=base-im.height
            assert x>=0 and x+im.width<=CW and y>=0,(ident,im.size,x,y)
            frame.alpha_composite(im,(x,y));frame.save(p/'frames'/f'idle-{i:02}.png');im.save(p/'frames'/f'idle-{i:02}-cropped.png');frames.append(frame);origins.append([x,y])
            frame.resize((CW*8,CH*8),N).save(p/'enlarged'/f'idle-{i:02}-8x.png')
        assert len({sha(p/'frames'/f'idle-{i:02}.png') for i in range(len(frames))})>=3,ident
        order=m.get('playbackOrder',data['playbackOrder']);play=[frames[i] for i in order];duration=m['frameMs']
        frames[0].save(p/'previews/idle-loop.png',save_all=True,append_images=play[1:],duration=[duration]*len(play),loop=0,disposal=0,blend=0)
        play[0].save(p/'previews/idle-loop.webp',save_all=True,append_images=play[1:],duration=duration,loop=0,lossless=True,exact=True)
        expanded=[f.resize((CW*4,CH*4),N) for f in play]
        expanded[0].save(p/'previews/idle-loop-4x.webp',save_all=True,append_images=expanded[1:],duration=duration,loop=0,lossless=True,exact=True)
        gifs=[]
        for f in expanded:
            bg=Image.new('RGBA',f.size,'#24313b');bg.alpha_composite(f);gifs.append(bg.convert('RGB'))
        gifs[0].save(p/'previews/idle-loop-4x.gif',save_all=True,append_images=gifs[1:],duration=duration,loop=0,disposal=2,optimize=False)
        sheet=Image.new('RGBA',(CW*len(frames),CH))
        contact=Image.new('RGBA',(CW*len(frames),CH+40),'#24313b');d=ImageDraw.Draw(contact)
        for i,f in enumerate(frames):sheet.alpha_composite(f,(CW*i,0));contact.alpha_composite(f,(CW*i,40));d.text((CW*i+12,5),f'{ident} / {i}',font=font(21),fill='#eeeee8')
        sheet.save(p/'frames/idle-strip.png');contact.convert('RGB').save(p/'previews/idle-contact.png');motion_frames[ident]=frames
        viewparts=split_characters(p/'raw/views.png',3,orb);viewfactor=m['height']/viewparts[2][0].height;views=[]
        viewboard=Image.new('RGBA',(1440,820),'#24313b');vd=ImageDraw.Draw(viewboard)
        for i,((im,bbox),view) in enumerate(zip(viewparts,data['views'])):
            im=scale(im,viewfactor)
            if ident=='B3' and view=='side-left':
                im=Image.open(PRIOR/'B3/native/idle-cropped.png').convert('RGBA')
            im.save(p/'views'/f'{view}.png');im.resize((im.width*8,im.height*8),N).save(p/'enlarged'/f'{view}-8x.png');views.append(im)
            factor_show=min(4,440/im.width,650/im.height);shown=scale(im,factor_show);viewboard.alpha_composite(shown,(i*480+(480-shown.width)//2,760-shown.height));vd.text((i*480+20,22),['정면','앞쪽 사선','왼쪽 측면'][i],font=font(25),fill='#eeeee8')
        viewboard.convert('RGB').save(p/'previews/three-views.png');angle_images[ident]=views
        inputs={kind:sha(p/'references'/f'{kind}-target.png') for kind in ['idle','views']}
        row={'id':ident,'name':m['name'],'role':m['role'],'status':'generated-idle-and-view-candidates','frameCanvas':[CW,CH],'referenceHeight':m['height'],'frameVisibleSizes':sizes,'origins':origins,'selectedSourceFrames':chosen,'uniformScaleForEntireClip':factor,'viewUniformScale':viewfactor,'playbackOrder':order,'frameMs':duration,'loopMs':duration*len(play),'sourceBBoxes':[v[1] for v in rawparts],'inputSha256':inputs,'rawSha256':{kind:sha(p/'raw'/f'{kind}.png') for kind in ['idle','views']},'registration':'Bottom support rows centered; original clip-wide scale preserved. H3 bottom refers to cloth. Reference registration, not runtime collider or finalized foot pivot.','limitations':'Generated motion study. Per-frame pixel drift and depth interpretation require art approval; no runtime integration.'}
        row['frameSampledSizes']=sizes
        row['frameVisibleSizes']=[[f.getbbox()[2]-f.getbbox()[0],f.getbbox()[3]-f.getbbox()[1]] for f in frames]
        row['registrationReferenceSha256']=sha(registration_path);row['registrationWorldX']=registration_x
        row['registration']='Support centroid aligned to previous handed-off idle-frame.png on the same canvas, baseline192. H3 uses the mask horizontal center and cloth bottom. Reference registration, not runtime collider or finalized foot pivot.'
        if ident=='B3':row['viewCuration']='Side reference retains the prior approved five-contact profile. Newly generated alternate side interpretation is preserved in raw/views.png.'
        if ident=='M3':row['geometryReview']='Generated idle needle is shorter relative to the body than the static input. Two correction attempts are preserved but rejected. Reconcile needle length with user walk frames before production approval.'
        manifest.append(row);dump(p/'metadata.json',row)
        browser.append(dict(id=ident,name=m['name'],role=m['role'],duration=duration,order=order,frames=[uri(p/'frames'/f'idle-{i:02}.png') for i in range(len(frames))]))
        with Image.open(p/'previews/idle-loop.png') as apng:assert apng.n_frames==len(play),(ident,apng.n_frames)
        assert len({f.tobytes() for f in frames})>=3
    # Labelled front-facing reference overview; equal scale per character, independent fit between cards.
    board=Image.new('RGBA',(1600,1600),'#24313b');d=ImageDraw.Draw(board)
    for i,m in enumerate(data['assets']):
        ident=m['id'];x,y=i%4*400,i//4*800;im=angle_images[ident][0];show=scale(im,min(4,360/im.width,630/im.height));board.alpha_composite(show,(x+(400-show.width)//2,y+735-show.height));d.text((x+16,y+20),ident+' '+m['name'],font=font(20),fill='#eeeee8');d.text((x+16,y+55),'정면 · 형태 검토',font=font(17),fill='#d4b989')
    board.convert('RGB').save(OUT/'front-overview.png')
    dboard=Image.new('RGBA',(1440,1640),'#24313b');dboard.alpha_composite(Image.open(OUT/'I4-rose/previews/three-views.png').convert('RGBA'),(0,0));dboard.alpha_composite(Image.open(OUT/'I4-blue/previews/three-views.png').convert('RGBA'),(0,820));dboard.convert('RGB').save(OUT/'siblings-three-views.png')
    native=Image.new('RGBA',(960,540),'#24313b');nd=ImageDraw.Draw(native);nd.text((20,12),'대기 모션 첫 프레임 · 동일1배율 / 플레이어66×64',font=font(21),fill='#eeeee8')
    player=Image.open(OUT/'references/assets/approved-player.png').convert('RGBA');player=player.crop(player.getbbox())
    for ids,foot in [(['K4','B3','M3','H3','R-selected'],230),(['A3','I4-rose','I4-blue'],495)]:
        x=20;native.alpha_composite(player,(x,foot-64));nd.text((x,foot+9),'플레이어',font=font(13),fill='#eeeee8');x+=92
        for ident in ids:
            im=motion_frames[ident][0];im=im.crop(im.getbbox());native.alpha_composite(im,(x,foot-im.height));nd.text((x,foot+9),ident,font=font(13),fill='#e9c089');x+=im.width+20
        assert x<=960,(ids,x)
    native.convert('RGB').save(OUT/'native-roster.png')
    dump(OUT/'manifest.json',manifest)
    cards=[]
    for m in data['assets']:
        ident=m['id'];review='<p class="note">필경사는 생성 대기에서 기준 그림보다 침 길이가 짧아진 상태입니다. 두 보정 시도도 원본에 보존했으며, 이동 프레임과 길이를 맞추는 픽셀 정리 전에는 양산본으로 채택하지 않습니다.</p>' if ident=='M3' else ''
        if ident=='B3':review='<p>측면 참고는 다섯 접점을 유지하는 기존 채택 그림을 사용했습니다. 새 정면·사선과 생성 원본을 함께 보존했습니다.</p>'
        cards.append(f'<section id="{ident}"><h2>{ident} {escape(m["name"])} / {escape(m["role"])}</h2><p>{escape(m.get("descriptionKo",m["motion"]))}</p>{review}<div class="motion" data-id="{ident}"><canvas width="384" height="224"></canvas><p class="state"></p></div><p><a href="{ident}/previews/idle-loop-4x.webp">4배 투명 WebP</a> · <a href="{ident}/previews/idle-loop-4x.gif">4배 GIF</a> · <a href="{ident}/previews/idle-loop.png">실제 크기 APNG</a> · <a href="{ident}/frames/idle-strip.png">원화 프레임 PNG</a> · <a href="{ident}/enlarged/idle-00-8x.png">첫 프레임8배</a></p><details><summary>정면·앞쪽 사선·측면과 개별8배 PNG</summary><img class="board" src="{ident}/previews/three-views.png"><p><a href="{ident}/enlarged/front-8x.png">정면8배</a> · <a href="{ident}/enlarged/three-quarter-left-8x.png">사선8배</a> · <a href="{ident}/enlarged/side-left-8x.png">측면8배</a></p></details><details><summary>대기 원화와 생성 입력</summary><div class="scroll"><img src="{ident}/previews/idle-contact.png"></div><p><a href="{ident}/raw/idle.png">생성 원본</a> · <a href="{ident}/idle-prompt.txt">대기 프롬프트</a> · <a href="{ident}/views-prompt.txt">방향 프롬프트</a></p></details></section>')
    script='''(()=>{const data=JSON.parse(document.getElementById('motion-data').textContent);let playing=true,age=0,last=performance.now();const loaded=[];const $=id=>document.getElementById(id);const load=src=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src;});Promise.all(data.map(async m=>{const images=await Promise.all(m.frames.map(load));const box=document.querySelector(`[data-id="${m.id}"]`),canvas=box.querySelector('canvas');loaded.push({...m,images,box,canvas,ctx:canvas.getContext('2d')});})).then(()=>{$('status').textContent='8명 반복 재생 준비 완료';});$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'일시정지':'재생';};$('reset').onclick=()=>{age=0;};function tick(now){if(playing)age+=(now-last)*Number($('speed').value);last=now;for(const m of loaded){const k=m.order[Math.floor(age/m.duration)%m.order.length],g=m.ctx;g.imageSmoothingEnabled=false;g.clearRect(0,0,384,224);g.drawImage(m.images[k],0,0);m.canvas.style.width=(384*Number($('zoom').value))+'px';m.canvas.style.height=(224*Number($('zoom').value))+'px';m.box.querySelector('.state').textContent=`원화 ${k+1}/${m.images.length} · ${m.duration}ms/단계 · ${m.order.length}단계 왕복 루프`; }requestAnimationFrame(tick);}requestAnimationFrame(tick);})();'''
    (SOURCE/'idle_motion_preview.js').write_text(script,encoding='utf-8')
    payload=json.dumps(browser,ensure_ascii=False).replace('<',r'\u003c')
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>8명 대기 루프 · 파란 남매 잉크술 · 세 방향</title><style>body{margin:0;background:#24313b;color:#eeeee8;font:17px/1.8 "Malgun Gothic",sans-serif}main{max-width:1400px;margin:auto;padding:28px}h1{font-size:28px}h2{font-size:23px}a{color:#b7ddeb}.board{width:100%;height:auto}section{border-top:1px solid #566773;padding:25px 0}.motion,.scroll{overflow-x:auto}.motion canvas,.scroll img{max-width:none;image-rendering:pixelated}button,select{padding:8px 12px;font:inherit}nav{position:sticky;top:0;background:#24313b;padding:12px 0;z-index:1;border-bottom:1px solid #566773}.note{border-left:4px solid #d4b989;padding-left:18px}summary{cursor:pointer;padding:10px 0}</style><main><h1>대기 모션8명 + 정면·사선·측면</h1><p class="note">기본 image_gen에서 새로 그린 개체별3~4장 원화를 왕복 재생하는 대기 루프 시안입니다. 몸 전체를 단순 확대·이동해 만든 모션이 아닙니다. 파란 남매는 작은 잉크 구체를 다루는 원거리 술사로 변경했습니다. 프레임은 클립당 같은 배율로 추출하며 프레임별 자동 키 맞춤을 하지 않았습니다. 생성 원화의 미세한 픽셀·장비·깊이 흔들림은 후속 정리 대상이고 최종 양산 승인본이 아닙니다. 이동·공격 모션·Unity 적용은 포함하지 않습니다.</p><p><a href="idle-motion-8-v1-20261031.zip">전체 PNG·반복 재생·세 방향·원본 ZIP</a> · <a href="README.md">전달 안내</a> · <a href="manifest.json">프레임·시간·크기 정보</a></p><nav><button id="play">일시정지</button> <button id="reset">처음부터</button> 속도 <select id="speed"><option value=".5">0.5배</option><option value="1" selected>1배</option><option value="1.5">1.5배</option></select> 화면 확대 <select id="zoom"><option value="1">1배</option><option value="2" selected>2배</option><option value="4">4배</option></select> <span id="status">이미지 준비 중</span></nav><details><summary>동일1배율 크기 비교</summary><div class="scroll"><img src="native-roster.png" width="960" height="540"></div></details><details><summary>파란 남매 변경과 남매 세 방향</summary><img class="board" src="siblings-three-views.png"></details><details><summary>8명 정면 모아 보기 · 칸별 확대 비교</summary><img class="board" src="front-overview.png"></details>'''+''.join(cards)+'<p>프레임 시간은 미리보기용 후보값이며 전투 수치가 아닙니다. 정면·사선·측면은 별도 디자인 참고 그림으로, 방향 전환 모션 프레임이 아닙니다. 배경 합성이나 실제 게임 캡처로 표현하지 않습니다.</p><script id="motion-data" type="application/json">'+payload+'</script><script>'+script+'</script></main></html>'
    (OUT/'index.html').write_text(page,encoding='utf-8')
    (OUT/'README.md').write_text('''# 대기 모션8명·세 방향 참고

각 폴더 `frames/idle-00.png`부터 시작하는384×224px PNG는 개체별3~4장의 생성 원화다. 클립 전체에 같은 배율을 적용하고 발 기준을 정렬했으며 프레임마다 높이를 다시 맞추지 않는다. 가로 위치는 이전에 전달한 idle-frame.png의 받침점에 맞추며 프레임별 유효 크기는 manifest.json을 따른다. 분사수·보행기는 첫 생성 원화의 크기 흔들림을 피하기 위해 뒤의3장을 사용하며 투척수는 물건 끝이 잘린 마지막 원화를 빼고 앞의3장을 쓴다. 원본4장은 raw/에 보존한다.

`previews/idle-loop.webp`·`idle-loop.png`(APNG)는 투명 배경으로 실제 반복 재생된다. `idle-loop-4x.webp`는4배 투명 확대, GIF는 짙은 단색 배경의 확인용이다. 왕복 순서와 단계별 시간은 metadata.json에 있다. 개별 PNG와8배 확대도 함께 제공한다.

`views/`는 정면·앞쪽 사선·왼쪽 측면 참고용 이미지다. 세 방향은 실제 방향 전환 애니메이션이 아니고 생성 모델의 깊이 해석을 검토할 자료다. 서로 다른 관점 그림을 대기 루프에 섞지 않는다. I4-blue는 석궁 보조를 대신해 한 손 위에 작은 잉크 구체를 띄우는 원거리 잉크술 방향이다. 기존 파란 남매와 공유 석궁 합본 원본은 보존한다.

현재는 생성 모션 시안이다. 미세한 장비·외곽 픽셀·가려진 지지대 해석 차이를 양산 전 정리하고 사용자 이동 프레임과 비교해야 한다. 캐릭터 실제 크기·무기 길이·팔과 지지대 수·그림체·색을 유지하는 것을 기준으로 검토한다. 움직임 시간은 검토 후보이고 Unity·전투 판정·이동·공격 모션은 제작하지 않았다. 원본·참조·프롬프트를 보존한다.

필경사는 생성 대기의 침 길이가 정지 기준보다 짧아진 제한이 있다. 두 보정 시도는 길이·비율 일치에 실패해 제외하고 원본에 보존했다. 이동 프레임과 침 길이를 맞춘 픽셀 정리 전에는 양산본으로 채택하지 않는다. 보행기 측면 참고는 다섯 접점을 유지하는 기존 채택 그림을 사용하며 새 생성 측면 해석은 raw/views.png에 보존한다.
''',encoding='utf-8')
    assert sha(SOURCE/'user-size-reference.json')=='a424c30cf9d13da134de87681b377b6cfdcb81e913fde95a8345ac7fc0d4f82d'
    archive=OUT/(OUT.name+'.zip');missing=[x for x in re.findall(r'(?:href|src)="([^"]+)"',page) if x!=archive.name and not (OUT/x).exists()];assert not missing,missing
    (OUT/'source').mkdir(exist_ok=True)
    for f in ['idle_motion.py','idle_motion_build.py','idle_motion_preview.js','current-choices.json']:shutil.copy2(SOURCE/f,OUT/'source'/f)
    shutil.copy2(ROOT/'data/combat/stage2_waterway_patterns.json',OUT/'source/combat-patterns.json')
    shutil.copy2(SPEC_PATH,OUT/'motion-spec.json')
    dump(OUT/'validation.json',{'clips':len(manifest),'uniqueDrawings':sum(len(m['selectedSourceFrames']) for m in manifest),'threeViewSets':8,'localLinksMissing':missing,'userSizeJsonPreserved':True,'uniformClipScaling':True,'playback':'APNG frame counts checked; genuine generated frames with >=3 unique PNGs/clip. No browser/Unity runtime validation.'})
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=archive:z.write(p,p.relative_to(OUT))
    print(json.dumps({'clips':len(manifest),'uniqueDrawings':sum(len(m['selectedSourceFrames']) for m in manifest),'views':24,'zipBytes':archive.stat().st_size,'out':str(OUT)},ensure_ascii=False))


if __name__=='__main__':build()
