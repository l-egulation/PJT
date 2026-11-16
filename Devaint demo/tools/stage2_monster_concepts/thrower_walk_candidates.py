"""Extract the returned thrower sheet and publish synchronized pixel walk options."""
from pathlib import Path
import json
import shutil
import zipfile
from html import escape

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label,binary_fill_holes,convolve
from k4_d_pixel_walk import ROOT, N, sha, sample, tidy
from k4_d_pixel_cleanup import fill_holes
from b3_walk_return import dump, white
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_thrower_walk_candidates.json'


def bounds(im):
    a=np.array(im);parts,_=label(a[:,:,:3].min(2)<225);sizes=np.bincount(parts.ravel());sizes[0]=0
    y,x=np.where(sizes[parts]>128)
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]


def durations(count,total):
    points=[round(total*i/count) for i in range(count+1)]
    return [b-a for a,b in zip(points,points[1:])]


def recover_interior(a,original,palette,scale,offset,spec):
    """Recover real source material rejected by the strict white-matte palette filter."""
    solid=a[:,:,3]>0;support=convolve(solid.astype(int),np.ones((3,3),int),mode='constant')
    candidates=(binary_fill_holes(solid)&~solid)|(~solid&(support>=6))
    yy,xx=np.where(candidates);source=np.array(original);g=spec['supersampleGrid'];votes=np.zeros((len(xx),len(palette)),int);coverage=np.zeros(len(xx),int)
    for dy in range(g):
        sy=np.floor((yy+(dy+.5)/g-offset[1])/scale).astype(int)
        for dx in range(g):
            sx=np.floor((xx+(dx+.5)/g-offset[0])/scale).astype(int)
            valid=(sx>=0)&(sy>=0)&(sx<source.shape[1])&(sy<source.shape[0]);q=source[np.clip(sy,0,source.shape[0]-1),np.clip(sx,0,source.shape[1]-1)]
            foreground=valid&(q[:,3]>0)&(q[:,:3].min(1)<spec['interiorRecovery']['sourceRgbMinMax'])
            nearest=((q[:,:3].astype(float)[:,None,:]-palette.astype(float)[None,:,:])**2).sum(2).argmin(1);coverage+=foreground
            for c in range(len(palette)):votes[:,c]+=(foreground&(nearest==c))
    restore=coverage>=int(np.ceil(g*g*spec['interiorRecovery']['minCoverage']));result=a.copy()
    result[yy[restore],xx[restore],:3]=palette[votes[restore].argmax(1)];result[yy[restore],xx[restore],3]=255
    return result,int(restore.sum())


def recover_cloth(a,original,palette,scale,offset,config):
    """Recover source-occupied clothing independently of palette alpha confidence."""
    region=np.zeros(a.shape[:2],bool);x0,y0,x1,y1=config['region'];region[y0:y1,x0:x1]=True
    yy,xx=np.where(region&(a[:,:,3]==0));source=np.array(original.convert('RGBA'));g=config['grid']
    votes=np.zeros((len(xx),len(palette)),int);coverage=np.zeros(len(xx),int)
    gray=np.array(config['grayPalette'],int)
    gray_indices=np.array([np.flatnonzero(np.all(palette==rgb,axis=1))[0] for rgb in gray])
    purple_index=int(np.flatnonzero(np.all(palette==config['purpleRgb'],axis=1))[0])
    for dy in range(g):
        sy=np.floor((yy+(dy+.5)/g-offset[1])/scale).astype(int)
        for dx in range(g):
            sx=np.floor((xx+(dx+.5)/g-offset[0])/scale).astype(int)
            valid=(sx>=0)&(sy>=0)&(sx<source.shape[1])&(sy<source.shape[0])
            rgba=source[np.clip(sy,0,source.shape[0]-1),np.clip(sx,0,source.shape[1]-1)];q=rgba[:,:3].astype(int)
            purple=(q[:,2]-q[:,1]>=config['purpleMinBlueOverGreen'])&(q[:,2]-q[:,0]>=config['purpleMinBlueOverRed'])
            neutral=(np.ptp(q,axis=1)<=config['neutralMaxChroma'])&(q.max(1)>=config['neutralMinPeak'])
            foreground=valid&(rgba[:,3]>0)&(q.min(1)<config['sourceRgbMinMax'])&(purple|neutral)
            # Color uncertainty changes the chosen existing material, never source occupancy.
            nearest=gray_indices[((q[:,None]-gray[None])**2).sum(2).argmin(1)]
            nearest[purple]=purple_index;coverage+=foreground
            for color in range(len(palette)):votes[:,color]+=(foreground&(nearest==color))
    restore=coverage>=int(np.ceil(g*g*config['minCoverage']));result=a.copy();mask=np.zeros(a.shape[:2],bool)
    result[yy[restore],xx[restore],:3]=palette[votes[restore].argmax(1)];result[yy[restore],xx[restore],3]=255;mask[yy[restore],xx[restore]]=True
    return result,mask


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];base=ROOT/spec['base']
    for folder in ('source','all-frames','pixel-frames','previews','references','masks'):(out/folder).mkdir(parents=True,exist_ok=True)
    src=out/'source'/Path(spec['sourceFile']).name
    if not src.exists():shutil.copy2(spec['sourceFile'],src)
    Image.MAX_IMAGE_PIXELS=100000000;sheet=Image.open(src);w,h=spec['cellSize'];cols,rows=spec['grid'];assert sheet.size==(w*cols,h*rows)
    all_bounds=[];hashes=[]
    for i in range(cols*rows):
        x,y=i%cols*w,i//cols*h;im=sheet.crop((x,y,x+w,y+h)).convert('RGBA');p=out/'all-frames'/f'frame-{i:03}.png';im.save(p);all_bounds.append(bounds(im));hashes.append(sha(p))
    basic=Image.open(base/'native/base-frame.png').convert('RGBA');b=np.array(basic);palette=np.unique(b[b[:,:,3]>0,:3],axis=0);assert len(palette)==12
    reference=all_bounds[spec['scaleReferenceFrameZeroBased']];scale=spec['targetReferenceHeight']/(reference[3]-reference[1])
    lo,hi=spec['anchorReferenceRange'];anchor_boxes=np.array(all_bounds[lo:hi+1]);anchor=[float(np.median((anchor_boxes[:,0]+anchor_boxes[:,2])/2)),float(np.median(anchor_boxes[:,3]))]
    offset=[192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
    for name,path in [('base-frame.png',base/'native/base-frame.png'),('palette.json',base/'palette.json'),('input-prompt.txt',ROOT/spec['inputPrompt'])]:shutil.copy2(path,out/'references'/name)
    unique=sorted({i for c in spec['candidates'] for i in range(c['start'],c['end']+1)});images={};mapping={}
    for i in unique:
        original=Image.open(out/'all-frames'/f'frame-{i:03}.png').convert('RGBA');raw=sample(original,palette,scale,offset,spec)
        a,recovered=recover_interior(np.array(raw),original,palette,scale,offset,spec);clean=tidy(Image.fromarray(a),basic);a,holes=fill_holes(np.array(clean),spec['pinholeCleanup'])
        # Keep open-eye gold consistent with the chosen base; preserve a source blink.
        x0,y0,x1,y1=spec['eyeTint']['region'];eye_region=a[y0:y1,x0:x1];eye_mask=np.all(eye_region[:,:,:3]==spec['eyeTint']['fromRgb'],axis=2)&(eye_region[:,:,3]>0);eye_region[eye_mask,:3]=spec['eyeTint']['toRgb']
        f=Image.fromarray(a);images[i]=f;f.save(out/'pixel-frames'/f'frame-{i:03}.png')
        changed=np.any(np.array(raw)!=a,axis=2);mask=np.zeros_like(a);mask[changed]=[220,30,140,255];Image.fromarray(mask).save(out/'masks'/f'frame-{i:03}.png')
        bbox=f.getbbox();mapping[str(i)]={'sourceIndexZeroBased':i,'sourceOneBased':i+1,'sourceSha256':hashes[i],'bbox':list(bbox),'visibleSize':[bbox[2]-bbox[0],bbox[3]-bbox[1]],'cleanupPixels':int(changed.sum()),'filledHoles':holes,'sourceBackedInteriorPixelsRecovered':recovered}
        print('THROWER',i+1,mapping[str(i)]['visibleSize'],flush=True)
    union=Image.new('RGBA',(384,224))
    for f in images.values():union.alpha_composite(f)
    bb=union.getbbox();box=[bb[0]-8,bb[1]-8,bb[2]+8,bb[3]+8];source_boxes=np.array([all_bounds[i] for i in unique]);source_box=[int(source_boxes[:,0].min())-12,int(source_boxes[:,1].min())-12,int(source_boxes[:,2].max())+12,int(source_boxes[:,3].max())+12]
    index_palette=np.concatenate([np.zeros((1,3),np.uint8),palette]);candidates=[]
    for choice in spec['candidates']:
        folder=out/choice['id']
        for sub in ('native','native-indexed','white-native','enlarged-4x','source','previews'):(folder/sub).mkdir(parents=True,exist_ok=True)
        indices=list(range(choice['start'],choice['end']+1));frames=[images[i] for i in indices];ds=durations(len(frames),choice['cycleMs']);originals=[]
        for k,(i,f) in enumerate(zip(indices,frames),1):
            name=f'walk-{k:02}.png';shutil.copy2(out/'pixel-frames'/f'frame-{i:03}.png',folder/'native'/name);white(f).save(folder/'white-native'/name);f.resize((1536,896),N).save(folder/'enlarged-4x'/name)
            a=np.array(f);ix=np.zeros(a.shape[:2],np.uint8)
            for color,rgb in enumerate(palette,1):ix[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=color
            indexed=Image.fromarray(ix);indexed.putpalette(index_palette.ravel().tolist()+[0]*(768-index_palette.size));indexed.info['transparency']=0;indexed.save(folder/'native-indexed'/name);assert indexed.convert('RGBA').tobytes()==f.tobytes()
            shutil.copy2(out/'all-frames'/f'frame-{i:03}.png',folder/'source'/name);originals.append(Image.open(folder/'source'/name).convert('RGBA'))
        animate(frames,ds,folder/'previews/native.webp');animate(frames,ds,folder/'previews/native.apng')
        crops=[white(f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N)).convert('RGBA') for f in frames]
        animate(crops,ds,folder/'previews/white-4x.webp');animate([f.crop(source_box) for f in originals],ds,folder/'previews/original.webp')
        board(frames,indices,list(range(len(frames))),box,4,2,folder/'previews/keys.png')
        board(frames,indices,list(range(len(frames)-3,len(frames)))+list(range(3)),box,3,3,folder/'previews/seam.png')
        row={**choice,'indices':indices,'frameCount':len(frames),'durationsMs':ds,'paths':[f'{choice["id"]}/native/walk-{k+1:02}.png' for k in range(len(frames))],'seamKeys':list(range(len(frames)-3,len(frames)))+list(range(3))}
        dump(folder/'manifest.json',row);candidates.append(row)
    info={**spec,'sourceSha256':sha(src),'sheetSize':list(sheet.size),'sourceFrameHashes':hashes,'allSourceBounds':all_bounds,'baseSha256':sha(base/'native/base-frame.png'),'paletteRgb':palette.tolist(),'scale':scale,'offset':offset,'sourceAnchor':anchor,'mapping':mapping,'previewCrop':box,'candidates':candidates,'selectedCandidate':None,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);shutil.copy2(SPEC,out/'candidate-spec.json');page(out,info)
    (out/'README.md').write_text('''# 신호투척수 걷기 후보 A~D

원본12800×7200 시트와100개 무변형 셀을 보존한다. A는0기준14~31의18장, B는31~47의17장, C는57~73의17장으로 각각0.9초다. D는 C와 같은17장을0.72초로 재생한다. 시간은 PNG에 없는 검토 제안이다.

최초 참고 자세의 높이106px에서 공통 배율을 정하고 보행 전체의 중앙 위치·지면에서 하나의 오프셋을 정한다. 모든 후보에 같은384×224·기존12색·이진 알파를 사용한다. 작은 잡점·4px 이하 빈 픽셀을 정리하고 색상만으로 새 테두리를 만들지 않는다. 반환 동작의 손·소품·허벅지·무릎·부츠를 그대로 사용하며 상체·하체 합성이나 프레임별 크기·위치 보정은 하지 않는다. 원본 소품·얼굴·다리의 형태 변화는 검토 대상이다.

후보별 PNG/indexed/흰 비교/정수4배/원본 셀·무손실 루프·키판·접합판과 참조·입력 프롬프트·해시·정리 마스크를 보존한다. C/D는 픽셀·순서가 같고 시간만 다르다. 동시 재생·정지·확대·배속·개별 키·접합6장 비교를 제공한다. 6장 모드에서는 가운데 마지막→처음을 비교하며 바깥 반복은 실제 전체 루프 경계가 아니다. 사용자 선택 전이고 K4/B3/M3 채택 프레임·전투 규칙·Unity 통합은 변경하지 않는다.
''',encoding='utf-8')
    package(out)


def board(frames,indices,keys,box,cols,scale,path):
    w,h=(box[2]-box[0])*scale,(box[3]-box[1])*scale;rows=(len(keys)+cols-1)//cols
    result=Image.new('RGB',(w*cols,(h+28)*rows),'white');draw=ImageDraw.Draw(result);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    for pos,key in enumerate(keys):
        x,y=pos%cols*w,pos//cols*(h+28);result.paste(white(frames[key].crop(box).resize((w,h),N)),(x,y+28));draw.text((x+4,y+2),f'{key+1:02} / 원본{indices[key]+1}',font=font,fill='black')
    result.save(path)


def package(out):
    allowed={'A','B','C','D','references','masks','manifest.json','candidate-spec.json','index.html','preview.js','README.md','validation.json'}
    with zipfile.ZipFile(out/'thrower-walk-candidates-20261101.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(out.rglob('*')):
            rel=path.relative_to(out)
            if path.is_file() and rel.parts[0] in allowed:archive.write(path,rel.as_posix())


def page(out,info):
    cards=[]
    for c in info['candidates']:
        ident=c['id'];cards.append(f'''<section><h2>{ident} · {escape(c['name'])}{' · 추천' if c['recommended'] else ''}</h2><p>{c['frameCount']}장 · {c['cycleMs']/1000:g}초 · 원본{c['start']+1}~{c['end']+1}번</p><p>{escape(c['description'])}</p><div class="viewport"><canvas data-id="{ident}"></canvas></div><p data-label="{ident}"></p><input data-step="{ident}" type="range" min="0" max="{c['frameCount']-1}" value="0"><p><a href="{ident}/previews/white-4x.webp">흰 배경 루프</a> · <a href="{ident}/previews/keys.png">선정 프레임판</a> · <a href="{ident}/previews/original.webp">원본 구간</a> · <a href="{ident}/manifest.json">개별 PNG 대응</a></p><details><summary>마지막3장 → 처음3장</summary><img src="{ident}/previews/seam.png"></details></section>''')
    js='''const rows=DATA,box=BOX;let playing=true,rate=1,scale=3,seams=false,clock=0,previous=null;const states=[];
function order(s){return seams?s.seamKeys:s.indices.map((_,i)=>i)}
function draw(s,key){s.key=key;const c=document.querySelector('[data-id="'+s.id+'"]'),ctx=c.getContext('2d'),w=box[2]-box[0],h=box[3]-box[1];c.width=w;c.height=h;c.style.width=w*scale+'px';c.style.height=h*scale+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(s.images[key],box[0],box[1],w,h,0,0,w,h);document.querySelector('[data-step="'+s.id+'"]').value=key;document.querySelector('[data-label="'+s.id+'"]').textContent='선정 '+(key+1)+' / '+s.frameCount+' · 원본 '+(s.indices[key]+1)+' · '+s.durationsMs[key]+'ms'+(key>=s.frameCount-3?' · 마지막':key<3?' · 시작':'')}
function choose(s,t){const seq=order(s),total=seq.reduce((n,k)=>n+s.durationsMs[k],0);let rem=t%total;for(const key of seq){if(rem<s.durationsMs[key])return key;rem-=s.durationsMs[key]}return seq[0]}
function tick(t){if(previous===null)previous=t;if(playing){clock+=(t-previous)*rate;for(const s of states){let k=choose(s,clock);if(k!==s.key)draw(s,k)}}previous=t;requestAnimationFrame(tick)}
Promise.all(rows.map(async s=>{s.images=await Promise.all(s.paths.map(src=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src})));s.key=-1;states.push(s);draw(s,0)})).then(()=>requestAnimationFrame(tick));
function reset(){clock=0;previous=performance.now();states.forEach(s=>draw(s,order(s)[0]))}
document.querySelector('#play').onclick=()=>{playing=!playing;previous=performance.now();document.querySelector('#play').textContent=playing?'전체 정지':'전체 재생'};
document.querySelector('#restart').onclick=reset;document.querySelector('#rate').onchange=e=>{rate=+e.target.value;reset()};document.querySelector('#mode').onchange=e=>{seams=e.target.value==='seam';reset()};document.querySelector('#scale').onchange=e=>{scale=+e.target.value;states.forEach(s=>draw(s,s.key))};document.querySelector('#bg').onchange=e=>document.querySelectorAll('.viewport').forEach(v=>v.style.background=e.target.value);
document.querySelectorAll('[data-step]').forEach(input=>input.oninput=()=>{playing=false;document.querySelector('#play').textContent='전체 재생';const s=states.find(s=>s.id===input.dataset.step);draw(s,+input.value)});'''.replace('DATA',json.dumps(info['candidates'])).replace('BOX',json.dumps(info['previewCrop']))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>신호투척수 · 걷기 후보 A~D</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:24px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}section{min-width:0;background:white;padding:18px}.viewport{background:white;overflow:auto}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.controls{position:sticky;top:0;background:#eeeef2f7;padding:10px;z-index:1}button,select{padding:8px;margin:3px}a{color:#815149}input{width:100%}h2{font-size:22px}@media(max-width:850px){.grid{grid-template-columns:1fr}}</style><main><h1>신호투척수 · 걷기 후보 A~D</h1><p>A/B/C는 서로 다른 보행 구간을 같은0.9초로 비교합니다. D는 C와 같은17장을0.72초로 빠르게 재생합니다. 우선 추천은 C이며 아직 채택하지 않았습니다. 100장 전체가 아니라 각 후보의 선정 프레임만 재생합니다.</p><p>모두 기존12색·384×224·같은 배율과 위치 기준입니다. 최초 참고 자세 높이106px에 맞췄으며 보행에 따른 높이 변화는 남겼습니다. 소품은 원본의 화면 오른쪽 손에 유지되고 양쪽 발 전체가 캔버스 안에 있습니다. 상체와 하체를 따로 합성하지 않았습니다. 작은 빈 픽셀과 흰 외곽 잡점은 정리했습니다. PNG에 시간이 없어 각 시간은 검토 제안입니다.</p><p><a download href="thrower-walk-candidates-20261101.zip">후보 PNG·4배·원본 셀·참조 ZIP</a> · <a href="manifest.json">공통 규격·팔레트·원본 대응</a></p><div class="controls"><button id="play">전체 정지</button><button id="restart">동시에 처음부터</button><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option></select><select id="scale"><option value="1">실제1배</option><option value="2">2배</option><option value="3" selected>3배</option><option value="4">4배</option><option value="8">8배</option></select><select id="mode"><option value="full">전체 반복</option><option value="seam">마지막3장 → 처음3장만</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 배경</option><option value="#708b92">푸른 배경</option></select></div><div class="grid">'''+''.join(cards)+'''</div><p>접합부6장 모드에서는 가운데 마지막→처음 전환을 비교하세요. 여섯 번째→첫 번째는 전체 루프의 실제 경계가 아닙니다. 원본의 소품·얼굴·다리 형태 변화는 남아 있을 수 있으며 새 포즈 생성·보간·파츠 합성·프레임별 확대축소는 하지 않았습니다. 캐릭터 이동 아트 검토본이며 게임 캡처·Unity 적용 결과가 아닙니다. 등짐분사수·쐐기보행기·먹침필경사 채택본은 유지합니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


def repair_selected_c():
    repair_spec=ROOT/'data/presentation/stage2_thrower_c_repair.json'
    spec=json.loads(repair_spec.read_text(encoding='utf-8'));source=ROOT/spec['source'];out=ROOT/spec['output'];out.mkdir(parents=True,exist_ok=True)
    info=json.loads((source/'manifest.json').read_text(encoding='utf-8'));choice=json.loads((source/'C/manifest.json').read_text(encoding='utf-8'));palette=np.array(info['paletteRgb'],np.uint8)
    for folder in ('native','native-indexed','white-native','enlarged-4x','source','references/before-native','previews','masks'):(out/folder).mkdir(parents=True,exist_ok=True)
    before_frames=[];frames=[];mapping=[];pal=np.concatenate([np.zeros((1,3),np.uint8),palette])
    for key,index in enumerate(choice['indices'],1):
        name=f'walk-{key:02}.png';before_path=source/'C/native'/name;before=Image.open(before_path).convert('RGBA');original_path=source/'C/source'/name;original=Image.open(original_path).convert('RGBA')
        a,mask=recover_cloth(np.array(before),original,palette,info['scale'],info['offset'],spec['clothRecovery']);frame=Image.fromarray(a);frames.append(frame);before_frames.append(before)
        frame.save(out/'native'/name);white(frame).save(out/'white-native'/name);frame.resize((1536,896),N).save(out/'enlarged-4x'/name)
        ix=np.zeros(a.shape[:2],np.uint8)
        for color,rgb in enumerate(palette,1):ix[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=color
        indexed=Image.fromarray(ix);indexed.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));indexed.info['transparency']=0;indexed.save(out/'native-indexed'/name);assert indexed.convert('RGBA').tobytes()==frame.tobytes()
        shutil.copy2(before_path,out/'references/before-native'/name);shutil.copy2(original_path,out/'source'/name)
        marked=np.zeros_like(a);marked[mask]=[220,30,140,255];Image.fromarray(marked).save(out/'masks'/name)
        mapping.append({'key':key,'sourceIndexZeroBased':index,'beforeSha256':sha(before_path),'originalSha256':sha(original_path),'filledClothPixels':int(mask.sum()),'filledCoordinatesXY':[[int(x),int(y)] for y,x in zip(*np.where(mask))]})
        print('C',key,'cloth recovered',int(mask.sum()),flush=True)
    for path in (source/'references').iterdir():
        if path.is_file():shutil.copy2(path,out/'references'/path.name)
    shutil.copy2(source/'manifest.json',out/'references/before-manifest.json');shutil.copy2(source/'C/manifest.json',out/'references/selected-C.json');shutil.copy2(repair_spec,out/'repair-spec.json')
    ds=choice['durationsMs'];box=info['previewCrop'];animate(frames,ds,out/'previews/clean-native.webp');animate(frames,ds,out/'previews/clean-native.apng')
    crops=[white(f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N)).convert('RGBA') for f in frames];animate(crops,ds,out/'previews/clean-white-4x.webp')
    board(frames,choice['indices'],list(range(17)),box,4,2,out/'previews/clean-17.png')
    pants_box=spec['detailCrop'];w,h=(pants_box[2]-pants_box[0])*8,(pants_box[3]-pants_box[1])*8;font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',16)
    details=Image.new('RGB',(w*3,(h+32)*4),'white');draw=ImageDraw.Draw(details)
    for row,key in enumerate([3,7,10,15]):
        original=Image.open(out/'source'/f'walk-{key:02}.png').convert('RGBA');s=info['scale'];ox,oy=info['offset'];sb=[(pants_box[0]-ox)/s,(pants_box[1]-oy)/s,(pants_box[2]-ox)/s,(pants_box[3]-oy)/s]
        panels=[original.crop(tuple(map(round,sb))).resize((w,h),N).convert('RGB')]+[white(f.crop(pants_box).resize((w,h),N)) for f in (before_frames[key-1],frames[key-1])]
        for col,panel in enumerate(panels):details.paste(panel,(col*w,row*(h+32)+32));draw.text((col*w+4,row*(h+32)+5),f'{key:02} / '+['SOURCE','BEFORE','REPAIRED'][col],font=font,fill='black')
    details.save(out/'previews/pants-source-before-after-8x.png')
    result={**spec,'sourceIndicesZeroBased':choice['indices'],'frameCount':17,'durationsMs':ds,'cycleMs':sum(ds),'canvas':[384,224],'paletteRgb':info['paletteRgb'],'previewCrop':box,'scale':info['scale'],'offset':info['offset'],'mapping':mapping,'filledClothPixels':sum(x['filledClothPixels'] for x in mapping),'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',result);repair_page(out,result)
    (out/'README.md').write_text('''# 선택 C · 바지 투명 틈 보정

C17장·0.9초·기존12색·384×224·위치를 유지한다. 이전 색 일치/흰 혼합 필터가 회색과 보라색 옷을 제거했고, 기존 내부 복원은 바깥으로 열린 틈을 후보에 포함하지 않아 바지가 비었다. 바지 영역의 원본16점에서 중성/보라 옷 색이 절반 이상 차지하면 실제 전경으로 복원한다. 중성 회색과 보라색은 각각 기존 팔레트의 옷 색 계열에서 고르며 색 불확실성을 투명도로 처리하지 않는다.

기존 불투명 픽셀·범위 밖·흰 배경과 다리 사이 여백은 유지한다. source와 references/before-native에 원본과 이전17장을 보존하며 좌표·마스크·해시를 제공한다. 새 색·상하체 합성·모션 변경·Unity 적용은 없다. C 동작은 사용자 선택이며 바지 보정본은 아트 검토 단계다.
''',encoding='utf-8')
    repair_package(out)


def repair_package(out):
    with zipfile.ZipFile(out/'thrower-C-cloth-clean-20261101.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip':archive.write(p,p.relative_to(out).as_posix())


def repair_page(out,info):
    js='''const data=DATA,pairs=[[],[]];let playing=true,key=0,elapsed=0,previous=null,scale=4;
function draw(){const box=data.previewCrop,w=box[2]-box[0],h=box[3]-box[1];document.querySelectorAll('canvas').forEach((c,i)=>{c.width=w;c.height=h;c.style.width=w*scale+'px';c.style.height=h*scale+'px';let ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.drawImage(pairs[i][key],box[0],box[1],w,h,0,0,w,h)});document.querySelector('#key').value=key;document.querySelector('#label').textContent=(key+1)+' /17'}
Promise.all(['references/before-native','native'].map(async(folder,i)=>{pairs[i]=await Promise.all(data.durationsMs.map((_,k)=>new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=folder+'/walk-'+String(k+1).padStart(2,'0')+'.png'})))})).then(()=>{draw();requestAnimationFrame(tick)});
function tick(t){if(previous===null)previous=t;if(playing){elapsed=(elapsed+t-previous)%data.cycleMs;let rem=elapsed,next=0;while(next<16&&rem>=data.durationsMs[next]){rem-=data.durationsMs[next];next++}if(next!==key){key=next;draw()}}previous=t;requestAnimationFrame(tick)}document.querySelector('#play').onclick=()=>{playing=!playing;previous=performance.now();document.querySelector('#play').textContent=playing?'정지':'재생'};document.querySelector('#key').oninput=e=>{playing=false;document.querySelector('#play').textContent='재생';key=+e.target.value;elapsed=data.durationsMs.slice(0,key).reduce((a,b)=>a+b,0);draw()};document.querySelector('#scale').onchange=e=>{scale=+e.target.value;draw()};document.querySelector('#bg').onchange=e=>document.querySelectorAll('.viewport').forEach(v=>v.style.background=e.target.value);'''.replace('DATA',json.dumps(info))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>선택 C · 바지 빈 픽셀 보정</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:24px}a{color:#815149}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.loop{display:block;margin:auto}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}.viewport{overflow:auto;background:white}section{min-width:0;background:white;padding:14px}button,select{padding:8px;margin:4px}@media(max-width:850px){.grid{grid-template-columns:1fr}}</style><main><h1>신호투척수 · 선택 C 바지 보정</h1><p>원본에 채워진 회색·보라 옷이 변환 필터에서 투명하게 빠진 문제를 수정했습니다. 원본의 옷 색 분포를 기준으로 바깥에 열린 틈도 복원합니다. C17장·0.9초·기존12색·크기·동작은 유지합니다.</p><p><a download href="thrower-C-cloth-clean-20261101.zip">보정17장·4배·원본·마스크 ZIP</a> · <a href="manifest.json">보정 좌표·시간·색 정보</a></p><img class="loop" src="previews/clean-white-4x.webp"><h2>이전 / 보정 · 같은 키 비교</h2><button id="play">정지</button><select id="scale"><option value="1">실제1배</option><option value="4" selected>4배</option><option value="8">8배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 배경</option></select><input id="key" type="range" min="0" max="16" value="0"><span id="label"></span><div class="grid"><section><h2>이전 C</h2><div class="viewport"><canvas></canvas></div></section><section><h2>바지 보정 C</h2><div class="viewport"><canvas></canvas></div></section></div><h2>원본 / 이전 / 보정 · 바지8배</h2><img src="previews/pants-source-before-after-8x.png"><details><summary>17장 전체 보기</summary><img src="previews/clean-17.png"></details><p>원래 채워진 픽셀과 바지 영역 밖은 유지했습니다. 실제 흰 배경과 다리 사이 여백은 채우지 않습니다. C 동작은 사용자 선택이며 보정본은 검토 단계입니다. 캐릭터 아트 미리보기이며 실제 게임 캡처·Unity 적용 결과가 아닙니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


if __name__=='__main__':
    import sys
    if '--repair-c' in sys.argv:repair_selected_c()
    else:build()
