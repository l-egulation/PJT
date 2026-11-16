"""Convert selected D source cells to the approved logical grid and existing K4 palette."""
from pathlib import Path
import hashlib,json,shutil,zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label,binary_dilation,convolve
from walk_review import animate,review_board

ROOT=Path(__file__).resolve().parents[2]
SPEC=ROOT/'data/presentation/stage2_k4_d_pixel_walk.json'
N=Image.Resampling.NEAREST


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def foreground_bounds(im):
    a=np.array(im);parts,_=label(a[:,:,:3].min(2)<230);sizes=np.bincount(parts.ravel());sizes[0]=0;y,x=np.where(sizes[parts]>512)
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]


def palette_materials(im,palette,config):
    a=np.array(im.convert('RGBA'));packed=a[:,:,:3].astype(np.uint32);packed=packed[:,:,0]|(packed[:,:,1]<<8)|(packed[:,:,2]<<16)
    values,inverse=np.unique(packed,return_inverse=True);q=np.stack([values&255,(values>>8)&255,(values>>16)&255],axis=1).astype(np.float32);p=palette.astype(np.float32)
    error=((q[:,None]-p[None])**2).mean(2);nearest=error.argmin(1);opaque=np.sqrt(error.min(1))
    v=p-255;alpha=np.clip(((q[:,None]-255)*v[None]).sum(2)/(v*v).sum(1)[None],0,1)
    fit=255+alpha[:,:,None]*v[None];residual=np.sqrt(((q[:,None]-fit)**2).mean(2));best=residual.argmin(1);best_error=residual[np.arange(len(q)),best];best_alpha=alpha[np.arange(len(q)),best]
    contaminated=(best_alpha<config['alphaMinimum'])&(opaque-best_error>config['matteAdvantage'])
    keep=((opaque<=config['opaqueRmsMax'])|((best_alpha>=config['alphaMinimum'])&(best_error<config['compositeRmsMax'])))&~contaminated&(q.min(1)<245)
    material=np.where((opaque-best_error>5)&(best_alpha>=config['alphaMinimum']),best,nearest)
    return material[inverse].reshape(a.shape[:2]),(keep[inverse].reshape(a.shape[:2])&(a[:,:,3]>0))


def sample(im,palette,scale,offset,spec):
    material,opaque=palette_materials(im,palette,spec['matte']);w,h=spec['canvas'];g=spec['supersampleGrid'];votes=np.zeros((h,w,len(palette)),np.uint8);coverage=np.zeros((h,w),np.uint8)
    yy,xx=np.indices((h,w))
    for ystep in range(g):
        sy=np.floor((np.arange(h)+(ystep+.5)/g-offset[1])/scale).astype(int)
        for xstep in range(g):
            sx=np.floor((np.arange(w)+(xstep+.5)/g-offset[0])/scale).astype(int)
            valid=(sy[:,None]>=0)&(sy[:,None]<material.shape[0])&(sx[None,:]>=0)&(sx[None,:]<material.shape[1]);ys=np.clip(sy,0,material.shape[0]-1);xs=np.clip(sx,0,material.shape[1]-1)
            fg=valid&opaque[ys[:,None],xs[None,:]];index=material[ys[:,None],xs[None,:]];coverage+=fg.astype(np.uint8)
            for color in range(len(palette)):votes[:,:,color]+=(fg&(index==color)).astype(np.uint8)
    index=votes.argmax(2);alpha=coverage>=int(np.ceil(g*g*spec['coverageMinimum']))
    # Eye gold is a thin intentional detail, not a palette-noise speck.
    gold=np.where(np.all(palette==[234,170,22],axis=1))[0]
    if len(gold):index[(votes[:,:,gold[0]]>=4)&alpha]=gold[0]
    a=np.zeros((h,w,4),np.uint8);a[:,:,:3]=palette[index];a[:,:,3]=alpha.astype(np.uint8)*255;a[~alpha]=0
    return Image.fromarray(a)


def tidy(im,base):
    a=np.array(im).copy();b=np.array(base);kernel=np.ones((3,3),int)
    base_solid=b[:,:,3]>0;support=convolve(base_solid.astype(int),kernel,mode='constant');protect=binary_dilation(base_solid&(support<=4),iterations=2)
    solid=a[:,:,3]>0;parts,n=label(solid)
    for i in range(1,n+1):
        mask=parts==i
        if mask.sum()<=2 and not np.any(mask&protect):a[mask]=0
    solid=a[:,:,3]>0;neighbors=convolve(solid.astype(int),kernel,mode='constant')-solid;a[solid&(neighbors<=1)&~protect]=0
    solid=a[:,:,3]>0;interior=convolve(solid.astype(int),kernel,mode='constant')==9;source=a.copy()
    for color in np.unique(source[source[:,:,3]>0],axis=0):
        if tuple(color[:3])==(234,170,22):continue
        parts,n=label(np.all(source==color,axis=2))
        for i in range(1,n+1):
            mask=parts==i
            if mask.sum()>2 or not np.all(interior[mask]):continue
            ring=binary_dilation(mask)&solid&~mask;colors,counts=np.unique(source[ring],axis=0,return_counts=True)
            if len(colors) and counts.max()>=5:a[mask]=colors[counts.argmax()]
    return Image.fromarray(a)


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));source=ROOT/spec['source'];base=ROOT/spec['base'];out=ROOT/spec['output']
    for sub in ('native','native-indexed','white-native','enlarged-4x','previews','references/source-frames'):(out/sub).mkdir(parents=True,exist_ok=True)
    choice=json.loads((source/'D/manifest.json').read_text(encoding='utf-8'));assert choice['id']=='D' and choice['frameCount']==20
    basic=Image.open(base/'native/base-frame.png').convert('RGBA');base_pixels=np.array(basic);palette=np.unique(base_pixels[base_pixels[:,:,3]>0,:3],axis=0);assert len(palette)==11
    shutil.copy2(base/'native/base-frame.png',out/'references/base-frame.png');shutil.copy2(base/'palette.json',out/'references/palette.json');shutil.copy2(source/'D/manifest.json',out/'references/selected-D.json')
    originals=[Image.open(source/p).convert('RGBA') for p in choice['nativePaths']];boxes=np.array([foreground_bounds(im) for im in originals]);scale=spec['targetMedianVisibleHeight']/float(np.median(boxes[:,3]-boxes[:,1]));anchor=[float(np.median((boxes[:,0]+boxes[:,2])/2)),float(np.median(boxes[:,3]))];offset=[192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
    frames=[];mapping=[];pal=np.concatenate([np.zeros((1,3),np.uint8),palette],axis=0)
    for i,(original,srcpath) in enumerate(zip(originals,choice['nativePaths'])):
        f=tidy(sample(original,palette,scale,offset,spec),basic);f.save(out/'native'/f'walk-{i+1:02}.png');f.resize((1536,896),N).save(out/'enlarged-4x'/f'walk-{i+1:02}.png');white=Image.new('RGBA',f.size,'white');white.alpha_composite(f);white.convert('RGB').save(out/'white-native'/f'walk-{i+1:02}.png');frames.append(f)
        a=np.array(f);indexed=np.zeros(a.shape[:2],np.uint8)
        for color,rgb in enumerate(palette,1):indexed[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=color
        ind=Image.fromarray(indexed);ind.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));ind.info['transparency']=0;ind.save(out/'native-indexed'/f'walk-{i+1:02}.png')
        assert ind.convert('RGBA').tobytes()==f.tobytes()
        src=source/srcpath;shutil.copy2(src,out/'references/source-frames'/f'original-{i+1:02}.png');bb=f.getbbox();mapping.append({'key':i+1,'sourceIndexZeroBased':choice['indices'][i],'sourceSha256':sha(src),'visibleSize':[bb[2]-bb[0],bb[3]-bb[1]],'bbox':list(bb),'opaqueColorsUsed':len(np.unique(a[a[:,:,3]>0,:3],axis=0))});print('pixel',i+1,mapping[-1]['visibleSize'],flush=True)
    ds=choice['durations'];animate(frames,ds,out/'previews/walk-native.webp');animate(frames,ds,out/'previews/walk-native.apng')
    union=Image.new('RGBA',(384,224))
    for f in frames:union.alpha_composite(f)
    bb=union.getbbox();box=(bb[0]-10,bb[1]-10,bb[2]+10,bb[3]+10);white4=[]
    for f in frames:
        im=f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N);white=Image.new('RGBA',im.size,'white');white.alpha_composite(im);white4.append(white)
    animate(white4,ds,out/'previews/walk-white-4x.webp')
    review_board(frames,list(range(20)),box,5,3,out/'previews/all-20-dark.png');review_board(frames,[0,3,6,9,12,15,18,19],box,4,3,out/'previews/eight-poses-dark.png')
    # Static white key board keeps only the character's11 colors plus the white viewing background.
    cw,ch=box[2]-box[0],box[3]-box[1];board=Image.new('RGB',(cw*3*5,(ch*3+26)*4),'white');draw=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',16)
    for i,f in enumerate(frames):
        im=f.crop(box).resize((cw*3,ch*3),N);bg=Image.new('RGBA',im.size,'white');bg.alpha_composite(im);x,y=i%5*cw*3,i//5*(ch*3+26);board.paste(bg.convert('RGB'),(x,y+26));draw.text((x+5,y+3),f'{i+1:02} / {ds[i]}ms',font=font,fill='black')
    board.save(out/'previews/all-20-white.png')
    info={**spec,'choiceStatus':'D-selected-by-user','pixelConversionStatus':'review-not-approved','sourceIndices':choice['indices'],'durationsMs':ds,'cycleMs':sum(ds),'paletteRgb':palette.tolist(),'scale':scale,'offset':offset,'sourceAnchor':anchor,'sourceBounds':boxes.tolist(),'baseHash':sha(base/'native/base-frame.png'),'mapping':mapping,'previewCrop':list(box)};(out/'manifest.json').write_text(json.dumps(info,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');shutil.copy2(SPEC,out/'pixel-spec.json')
    map_preview(out,frames,ds)
    page(out,info)
    (out/'README.md').write_text('''# 등짐분사수 D · 기존11색 픽셀 변환

사용자가 고른 D20장의 순서와30/55ms 타이밍을 유지한다. 원본은 references/source-frames에 보존하고 기존 기본본·팔레트·D명세를 함께 보존한다. native는384×224 투명 RGBA, native-indexed는 같은 그림의 고정 팔레트 PNG, white-native는 흰 배경 비교, enlarged-4x는 정확한4배다. 배율과 오프셋은20장에 하나씩만 사용한다. 기존112px 몸 높이의 중앙값을 기준으로 맞추며 걸음에 따른 높이 변화는 유지한다.

원본 색과 흰 배경 혼합을 분리하고 각 논리 픽셀의16개 원본 점에서 전경 비율·기존11색 재질 투표로 변환한다. 새 색·보간·두꺼운 테두리·부위 합성·프레임별 확대축소는 없다. 작은 잡점과 내부 색 잡음을 제한적으로 정리하며 원본 생성 모션 자체의 형태 변화는 남을 수 있다. 선택한 D 움직임은 승인 방향이고 새 픽셀 변환본은 아트 검토 단계다. 게임 통합·전투 속도 확정은 별도다.
''',encoding='utf-8')
    package(out)


def package(out):
    with zipfile.ZipFile(out/'k4-D-pixel-walk-20261031.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip':z.write(p,p.relative_to(out).as_posix())


def map_preview(out,frames,ds):
    reference=ROOT/'outputs/waterway/pixel-base-step1-20261031/references'
    shutil.copy2(reference/'C-game.png',out/'references/C-game.png')
    shutil.copy2(reference/'assets/approved-player.png',out/'references/approved-player.png')
    bg=Image.open(reference/'C-game.png').convert('RGBA');scene=[]
    for f in frames:
        im=bg.copy();im.alpha_composite(f,(238,94));scene.append(im)
    animate(scene,ds,out/'previews/map-native.webp')


def page(out,info):
    ds=info['durationsMs'];frames=[f'walk-{i+1:02}.png' for i in range(20)]
    js='''const durations=DS,total=durations.reduce((a,b)=>a+b,0);const box=BOX;const images=[];let playing=true,key=0,previous=null,elapsed=0,scale=4;const canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d');function draw(){const im=images[key],w=box[2]-box[0],h=box[3]-box[1];canvas.width=w;canvas.height=h;canvas.style.width=w*scale+'px';canvas.style.height=h*scale+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(im,box[0],box[1],w,h,0,0,w,h);document.querySelector('#key').value=key;document.querySelector('#label').textContent=(key+1)+' /20 · '+durations[key]+'ms'}Promise.all(FRAMES.map(src=>new Promise(r=>{const im=new Image();im.onload=()=>r(im);im.src='native/'+src}))).then(arr=>{images.push(...arr);draw();requestAnimationFrame(tick)});function tick(t){if(previous===null)previous=t;if(playing){elapsed=(elapsed+t-previous)%total;let rem=elapsed,next=0;while(next<durations.length-1&&rem>=durations[next]){rem-=durations[next];next++}if(next!==key){key=next;draw()}}previous=t;requestAnimationFrame(tick)}document.querySelector('#play').onclick=()=>{playing=!playing;previous=performance.now();document.querySelector('#play').textContent=playing?'정지':'재생'};document.querySelector('#key').oninput=e=>{playing=false;document.querySelector('#play').textContent='재생';key=+e.target.value;elapsed=durations.slice(0,key).reduce((a,b)=>a+b,0);draw()};document.querySelector('#scale').onchange=e=>{scale=+e.target.value;draw()};document.querySelector('#bg').onchange=e=>document.querySelector('.viewport').style.background=e.target.value;'''.replace('DS',json.dumps(ds)).replace('FRAMES',json.dumps(frames)).replace('BOX',json.dumps(info['previewCrop']))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>등짐분사수 D · 11색 픽셀 보정</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1200px;margin:auto;padding:24px}a{color:#815149}.viewport{overflow:auto;background:white}canvas,img{image-rendering:pixelated}button,select{padding:8px;margin:5px}.board{max-width:100%}.loop{max-width:100%;display:block;margin:auto}</style><main><h1>선택 D · 등짐분사수11색 픽셀 변환</h1><p>D20장·총1초의 순서와 시간을 유지했습니다. 기존 기본본의 실제11색만 사용하고 몸 높이112px 기준으로 같은 배율을 적용했습니다. 캔버스384×224이며 발·등통 전체가 들어갑니다. 움직임 선택은 D, 이번 픽셀 변환본은 검토 단계입니다.</p><p><a download href="k4-D-pixel-walk-20261031.zip">20장 PNG·4배·팔레트 ZIP</a> · <a href="manifest.json">크기·색·시간 정보</a></p><img class="loop" src="previews/walk-white-4x.webp"><h2>1배·확대·외곽 확인</h2><button id="play">정지</button><select id="scale"><option value="1">실제1배</option><option value="4" selected>4배</option><option value="8">8배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 배경</option><option value="#708b92">푸른 배경</option></select><input id="key" type="range" min="0" max="19" value="0"><span id="label"></span><div class="viewport"><canvas></canvas></div><h2>실제 크기 · 기존 맵 합성</h2><img src="previews/map-native.webp" width="960" height="540"><p>기존 플레이어와 같은1배율입니다. 배경을 밝히거나 흐리지 않은 합성 미리보기이며 실제 게임 캡처가 아닙니다.</p><h2>20장 픽셀 프레임</h2><img class="board" src="previews/all-20-white.png"><p>투명 native PNG와 indexed PNG는 같은 픽셀이며 확대본은 최근접4배입니다. 원본·선택 기본본·팔레트는 references에 보존합니다. 기존 승인 플레이어와의 크기 기준은 유지하며 Unity 적용은 하지 않았습니다.</p></main><script>'''+js+'</script></html>',encoding='utf-8')


if __name__=='__main__':build()
