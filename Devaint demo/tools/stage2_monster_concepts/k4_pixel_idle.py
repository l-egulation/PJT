"""Animate only selected original K4 pixels; no image generation or resampling."""
import base64, hashlib, json, re, shutil, zipfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_k4_pixel_idle.json'
OUT=ROOT/'outputs/waterway/k4-pixel-idle-v1-20261031'
BASE=ROOT/'outputs/waterway/pixel-base-step1-20261031'
N=Image.Resampling.NEAREST

def dump(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rgb(c):return tuple(bytes.fromhex(c))
def isin(a,colors):return np.any(np.stack([np.all(a[:,:,:3]==rgb(c),axis=2) for c in colors]),axis=0)&(a[:,:,3]>0)
def shift(a,dy):
    out=np.zeros_like(a)
    if dy==-1:out[:-1]=a[1:]
    elif dy==0:out[:]=a
    else:raise ValueError('Only original or one pixel upward is permitted')
    return out

def masks(a,spec):
    face=np.zeros(a.shape[:2],bool);x1,y1,x2,y2=spec['face']['bounds']
    face[y1:y2,x1:x2]=isin(a,spec['face']['colors'])[y1:y2,x1:x2]
    # Include the dark visor opening in the rigid face plate, not just its eye.
    for start,end,left in spec['face']['visorRows']:
        for y in range(start,end+1):
            xs=np.where(face[y])[0];assert len(xs)
            face[y,left:xs.max()+1]=True
    shoulders=np.zeros_like(face);coral=isin(a,spec['shoulders']['colors'])
    for x1,y1,x2,y2 in spec['shoulders']['bounds']:
        shoulders[y1:y2,x1:x2]=coral[y1:y2,x1:x2]
    # The destination cannot overwrite black straps, hands, metal or tank.
    can_receive=coral|(a[:,:,3]==0)
    shoulder_dest=shift(shoulders,-1)&can_receive
    return face,shoulders,shoulder_dest

def move_region(a,result,source,dest):
    vacant=source&~dest
    under=shift(a,-1)
    result[vacant]=under[vacant]
    result[dest]=shift(a,-1)[dest]

def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));source=ROOT/spec['source']
    assert sha(source)==spec['sourceSha256'],'The baseline changed; review masks before rebuilding.'
    for folder in ['references','frames','enlarged','previews','masks','source']:(OUT/folder).mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,OUT/'references/base-frame.png');shutil.copy2(BASE/'K4/native/base-indexed.png',OUT/'references/base-indexed.png')
    shutil.copy2(BASE/'K4/palette.json',OUT/'references/palette.json');shutil.copy2(BASE/'references/C-game.png',OUT/'references/C-game.png')
    shutil.copy2(SPEC,OUT/'motion-spec.json');shutil.copy2(SPEC,OUT/'source'/SPEC.name);shutil.copy2(Path(__file__),OUT/'source/k4_pixel_idle.py')
    original=Image.open(source).convert('RGBA');bbox=original.getbbox();crop=original.crop(bbox);a=np.array(crop)
    assert original.size==tuple(spec['canvas']) and list(bbox[:2])==spec['artOrigin'] and crop.size==tuple(spec['visibleSize'])
    face,shoulders,sdest=masks(a,spec);fdest=shift(face,-1)
    assert not np.any((face|fdest)&(shoulders|sdest))
    allowed=face|fdest|shoulders|sdest;fixed=~allowed
    for name,mask in [('face',face),('shoulders',shoulders),('allowed-change',allowed),('fixed',fixed)]:Image.fromarray(mask.astype(np.uint8)*255).save(OUT/'masks'/f'{name}.png')
    frames=[];counts=[];cropframes=[]
    for i,key in enumerate(spec['keys']):
        b=a.copy()
        if key['shoulderDy']==-1:move_region(a,b,shoulders,sdest)
        if key['faceDy']==-1:move_region(a,b,face,fdest)
        assert np.array_equal(b[fixed],a[fixed]),'A fixed pixel changed'
        # All body/tank colors outside the explicitly selected face/caps are locked.
        assert np.array_equal(b[60:],a[60:]),'Lower body or equipment changed'
        before_colors={tuple(v) for v in a.reshape(-1,4)};assert {tuple(v) for v in b.reshape(-1,4)}<=before_colors
        changed=np.any(b!=a,axis=2);counts.append(int(changed.sum()))
        cropped=Image.fromarray(b);frame=original.copy();frame.paste(cropped,bbox[:2]);frames.append(frame);cropframes.append(cropped)
        frame.save(OUT/'frames'/f'idle-{i:02}.png');cropped.save(OUT/'frames'/f'idle-{i:02}-cropped.png')
        for scale in [4,8]:frame.resize((frame.width*scale,frame.height*scale),N).save(OUT/'enlarged'/f'idle-{i:02}-{scale}x.png')
    assert frames[0].tobytes()==original.tobytes()
    assert len({f.tobytes() for f in frames})==4
    durations=[k['durationMs'] for k in spec['keys']]
    frames[0].save(OUT/'previews/idle-native.apng',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=0,blend=0)
    frames[0].save(OUT/'previews/idle-native.webp',save_all=True,append_images=frames[1:],duration=durations,loop=0,lossless=True,exact=True)
    # A fixed crop for all keys, with integer enlargement, never individual fitting.
    shown=[]
    for im in cropframes:
        canvas=Image.new('RGBA',(108,136));canvas.alpha_composite(im,(12,12));shown.append(canvas.resize((432,544),N))
    shown[0].save(OUT/'previews/idle-4x.webp',save_all=True,append_images=shown[1:],duration=durations,loop=0,lossless=True,exact=True)
    gif=[]
    for im in shown:
        bg=Image.new('RGBA',im.size,'#26313b');bg.alpha_composite(im);gif.append(bg.convert('RGB'))
    gif[0].save(OUT/'previews/idle-4x.gif',save_all=True,append_images=gif[1:],duration=durations,loop=0,optimize=False,disposal=2)
    sheet=Image.new('RGBA',(384*4,224));contact=Image.new('RGBA',(108*4*3,136*3+50),'#26313b');d=ImageDraw.Draw(contact);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
    for i,f in enumerate(frames):
        sheet.alpha_composite(f,(384*i,0));im=cropframes[i].resize((84*3,112*3),N);contact.alpha_composite(im,(i*324+36,86))
        d.text((i*324+12,12),f'{i} {spec["keys"][i]["name"]}',font=font,fill='#efdfc8')
    sheet.save(OUT/'frames/idle-strip.png');contact.save(OUT/'previews/key-poses.png')
    overlay=Image.new('RGBA',crop.size);oa=np.array(overlay);oa[face]=[107,205,234,200];oa[shoulders]=[242,180,100,200]
    maskview=Image.new('RGBA',crop.size,'#26313b');maskview.alpha_composite(crop);maskview.alpha_composite(Image.fromarray(oa));maskview.resize((672,896),N).save(OUT/'previews/moving-regions.png')
    # Existing map is immutable; artwork only changes inside the selected K4 pixels.
    scene=Image.open(OUT/'references/C-game.png').convert('RGBA');maps=[]
    for c in cropframes:
        bg=scene.copy();bg.alpha_composite(c,(350,286-c.height));maps.append(bg)
    maps[0].save(OUT/'previews/map-idle.webp',save_all=True,append_images=maps[1:],duration=durations,loop=0,lossless=True,exact=True)
    validation={'baseHashUnchanged':sha(source)==spec['sourceSha256'],'frameZeroEqualsBase':True,'changesPerKey':counts,'allChangesInsideFaceAndShoulderMask':True,'allOtherRgbaPixelsIdentical':True,'newColors':0,'nativePixelOffsetMax':1,'cycleMs':sum(durations),'uniqueFrames':4,'noNewImageGeneration':True,'browserUnityValidation':False}
    for name in ['idle-native.apng','idle-native.webp']:
        decoded=Image.open(OUT/'previews'/name);assert decoded.n_frames==4
        for i,f in enumerate(frames):
            decoded.seek(i);assert decoded.convert('RGBA').tobytes()==f.tobytes()
    dump(OUT/'validation.json',validation)
    browser=[base64.b64encode((OUT/'frames'/f'idle-{i:02}.png').read_bytes()).decode('ascii') for i in range(4)]
    page(spec,browser,counts)
    (OUT/'README.md').write_text('''# 등짐 분사수 국소 픽셀 대기 시안

기본본의 얼굴 마스크·양어깨 상단만 최대1px 위로 이동한다. 네 키를1.5초로 재생하며 어깨가 얼굴보다180ms 먼저 움직인다. 발·하체·등통·끈·손·호스·노즐 등 선택 영역 밖 RGBA는 모든 프레임에서 원본과 동일하다. 새 이미지 생성·리사이즈·새 색 추가 없이 원본 픽셀을 복사했고, 이동 후 빈 경계에는 바로 아래 행의 기존 픽셀을 노출한다.

`index.html`: 원본/대기 나란히,1배·4배·8배, 일시정지와 개별 키 확인.
`frames`:384×224 네 PNG와 스트립. 첫 프레임은 승인 기본본과 동일.
`enlarged`:4배/8배 고정 캔버스 PNG. `previews`:투명 APNG·WebP,4배 GIF,기존 맵 합성.
`masks`:얼굴·어깨·허용 변경 영역·고정 영역. 마스크 좌표는84×112 기본 그림 기준.
`references`와`motion-spec.json`:원본·팔레트·영역·프레임 시간 보존.

등짐 분사수 한 마리의 검토 시안이다. 다른 일곱 명·이동·공격·Unity 작업은 수행하지 않았다. 맵 이미지는 합성 미리보기이며 실제 게임 캡처가 아니다.
''',encoding='utf-8')
    zip_path=OUT/'k4-pixel-idle-v1-20261031.zip'
    with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(OUT.rglob('*')):
            if p.is_file() and p!=zip_path and p.name!='coordinate-review.png':z.write(p,str(p.relative_to(OUT)))
    print(json.dumps(validation,ensure_ascii=False))

def page(spec,pngs,counts):
    data=json.dumps({'images':pngs,'keys':spec['keys']},ensure_ascii=False)
    script='''const data=DATA;let images=[],playing=true,current=0,last=0,scale=4;
const base=document.querySelector('#base'),anim=document.querySelector('#anim');
function draw(){for(const [canvas,img] of [[base,images[0]],[anim,images[current]]]){const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,108,136);ctx.drawImage(img,138,68,108,136,0,0,108,136);canvas.style.width=(108*scale)+'px';canvas.style.height=(136*scale)+'px'}document.querySelector('#state').textContent=current+' / '+data.keys[current].name;document.querySelector('#step').value=current}
function tick(now){if(playing&&now-last>=data.keys[current].durationMs){current=(current+1)%images.length;last=now;draw()}requestAnimationFrame(tick)}
Promise.all(data.images.map(src=>new Promise(resolve=>{let i=new Image();i.onload=()=>resolve(i);i.src='data:image/png;base64,'+src}))).then(v=>{images=v;draw();last=performance.now();requestAnimationFrame(tick)});
document.querySelector('#play').onclick=()=>{playing=!playing;last=performance.now();document.querySelector('#play').textContent=playing?'일시정지':'재생'};
document.querySelector('#step').oninput=e=>{playing=false;document.querySelector('#play').textContent='재생';current=Number(e.target.value);draw()};
document.querySelector('#zoom').onchange=e=>{scale=Number(e.target.value);draw()};'''.replace('DATA',data)
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐 분사수 · 1px 대기</title><style>body{background:#17212a;color:#ece4d8;font:17px/1.6 system-ui;margin:0}main{max-width:1120px;margin:auto;padding:28px}h1{font-size:30px}a{color:#edbf87}.row{display:flex;gap:24px;flex-wrap:wrap}.panel{background:#26313b;padding:20px;overflow:auto}canvas,img{image-rendering:pixelated}button,select{padding:9px 16px;margin-right:12px;background:#e9c797;border:0;border-radius:5px;color:#24313b}button{cursor:pointer}.scroll{overflow:auto}p.note{border-left:4px solid #e2b273;padding-left:16px}.links{display:flex;gap:22px;flex-wrap:wrap}details{margin-top:30px}img.board{max-width:100%}</style><main><h1>등짐 분사수 · 얼굴과 어깨만1px</h1><p>기존 픽셀 기본본을 복사하여 선택한 영역만 움직였습니다. 장비·발·하체·손·끈은 고정입니다.</p><p class="note">한 마리 검토 시안 /1.5초 루프 /4개 키 /새 그림 생성 없음. 원본1px 이동이4배 화면에서는4px로 보입니다.</p><p><button id="play">일시정지</button><label>확대 <select id="zoom"><option value="1">실제1배</option><option value="4" selected>4배</option><option value="8">8배</option></select></label><label>키 <input id="step" type="range" min="0" max="3" value="0" step="1"></label> <span id="state"></span></p><div class="row"><div class="panel"><b>기본본 · 고정</b><br><canvas id="base" width="108" height="136"></canvas></div><div class="panel"><b>대기 · 재생</b><br><canvas id="anim" width="108" height="136"></canvas></div></div><p class="links"><a download href="k4-pixel-idle-v1-20261031.zip">PNG·모션 전체 ZIP</a><a href="previews/idle-4x.gif">4배 GIF</a><a href="previews/idle-native.apng">원본 투명 APNG</a><a href="previews/idle-native.webp">원본 투명 WebP</a></p><h2>실제 화면 크기 비교</h2><p>960×540 기존 맵 위 합성 미리보기입니다. 실제 게임 캡처가 아닙니다.</p><div class="scroll"><img width="960" height="540" src="previews/map-idle.webp"></div><details><summary>움직인 부분·개별 키 확인</summary><p>하늘색=얼굴 마스크, 주황색=양어깨. 이동과 경계 노출에 필요한 범위 밖은 원본 RGBA와 일치합니다.</p><img width="336" height="448" src="previews/moving-regions.png"><img class="board" src="previews/key-poses.png"><p><a href="motion-spec.json">영역·시간</a> · <a href="validation.json">고정 픽셀 검사</a> · <a href="../pixel-base-step1-20261031/index.html">8명 기본본</a></p></details></main><script>'''+script+'</script></html>'
    (OUT/'index.html').write_text(html,encoding='utf-8');(OUT/'source/preview.js').write_text(script,encoding='utf-8')

if __name__=='__main__':build()
