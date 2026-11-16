"""Extract the returned M3 sheet and publish synchronized pixel walk options."""
from pathlib import Path
import json
import shutil
import zipfile
from html import escape

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import label
from k4_d_pixel_walk import ROOT, N, sha, sample, tidy
from k4_d_pixel_cleanup import fill_holes
from b3_walk_return import dump, white
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_m3_walk_candidates.json'


def bounds(im):
    a=np.array(im);parts,_=label(a[:,:,:3].min(2)<225);sizes=np.bincount(parts.ravel());sizes[0]=0
    y,x=np.where(sizes[parts]>128)
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]


def durations(count,total):
    points=[round(total*i/count) for i in range(count+1)]
    return [b-a for a,b in zip(points,points[1:])]


def repair_needle(a,spec):
    # Connect only short internal row gaps; never move or lengthen the needle tip.
    a=a.copy();x0,y0,x1,y1=spec['needleRepairZone'];limit=spec['maxNeedleRowGap'];count=0
    for y in range(y0,y1):
        row=a[y,:,3]>0;x=x0
        while x<x1:
            if row[x]:x+=1;continue
            start=x
            while x<x1 and not row[x]:x+=1
            if start>x0 and x<x1 and x-start<=limit and row[start-1] and row[x]:
                for p in range(start,x):a[y,p]=a[y,start-1] if p-start<x-p else a[y,x]
                count+=x-start
    return a,count


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];base=ROOT/spec['base']
    for folder in ('source','all-frames','pixel-frames','previews','references','masks'):(out/folder).mkdir(parents=True,exist_ok=True)
    src=out/'source'/Path(spec['sourceFile']).name
    if not src.exists():shutil.copy2(spec['sourceFile'],src)
    Image.MAX_IMAGE_PIXELS=100000000;sheet=Image.open(src);w,h=spec['cellSize'];cols,rows=spec['grid'];assert sheet.size==(w*cols,h*rows)
    all_bounds=[];hashes=[]
    for i in range(cols*rows):
        x,y=i%cols*w,i//cols*h;im=sheet.crop((x,y,x+w,y+h)).convert('RGBA');p=out/'all-frames'/f'frame-{i:03}.png';im.save(p);all_bounds.append(bounds(im));hashes.append(sha(p))
    basic=Image.open(base/'native/base-frame.png').convert('RGBA');b=np.array(basic);palette=np.unique(b[b[:,:,3]>0,:3],axis=0);assert len(palette)==9
    reference=all_bounds[spec['scaleReferenceFrameZeroBased']];scale=spec['targetReferenceHeight']/(reference[3]-reference[1])
    lo,hi=spec['anchorReferenceRange'];anchor_boxes=np.array(all_bounds[lo:hi+1]);anchor=[float(np.median((anchor_boxes[:,0]+anchor_boxes[:,2])/2)),float(np.median(anchor_boxes[:,3]))]
    offset=[192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
    for name,path in [('base-frame.png',base/'native/base-frame.png'),('palette.json',base/'palette.json'),('input-prompt.txt',ROOT/spec['inputPrompt'])]:shutil.copy2(path,out/'references'/name)
    unique=sorted({i for c in spec['candidates'] for i in range(c['start'],c['end']+1)});images={};mapping={}
    for i in unique:
        raw=sample(Image.open(out/'all-frames'/f'frame-{i:03}.png').convert('RGBA'),palette,scale,offset,spec)
        clean=tidy(raw,basic);a,holes=fill_holes(np.array(clean),spec['pinholeCleanup']);a,needle_count=repair_needle(a,spec);a,more_holes=fill_holes(a,spec['pinholeCleanup']);holes+=more_holes;f=Image.fromarray(a);images[i]=f;f.save(out/'pixel-frames'/f'frame-{i:03}.png')
        changed=np.any(np.array(raw)!=a,axis=2);mask=np.zeros_like(a);mask[changed]=[220,30,140,255];Image.fromarray(mask).save(out/'masks'/f'frame-{i:03}.png')
        bbox=f.getbbox();mapping[str(i)]={'sourceIndexZeroBased':i,'sourceOneBased':i+1,'sourceSha256':hashes[i],'bbox':list(bbox),'visibleSize':[bbox[2]-bbox[0],bbox[3]-bbox[1]],'cleanupPixels':int(changed.sum()),'filledHoles':holes,'needleGapPixelsFilled':needle_count}
        print('M3',i+1,mapping[str(i)]['visibleSize'],flush=True)
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
    (out/'README.md').write_text('''# 먹침필경사 걷기 후보 A~D

받은12800×7200 원본은 source에 보존하고100개 셀을 all-frames에 무변형 분리한다. A는0기준15~32의18장, B는34~50의17장, C는74~88의15장으로 각각0.8초다. D는 A와 같은18장을1초로 재생한다. 새 동작이나 보간을 생성하지 않으며 속도와 구간을 구분해 검토한다. PNG에는 시간이 없어 모든 시간은 검토 제안이다.

최초 입력 자세의 높이59px로 하나의 배율을 정하고, 반환 보행 전체의 중앙 위치·지면 기준으로 하나의 오프셋을 정한다. 동일한 배율·오프셋을 모든 후보에 사용하며 매 프레임 움직이지 않는다. 반환 보행은 입력보다 다리를 편 자세이므로 유효 높이는59px로 고정되지 않는다. 기존9색·384×224·이진 알파와 정수4배를 사용하고 작은 잡점과4px 이하 내부 빈 픽셀을 정리한다. 큰 다리 여백은 보존한다. 원본의 침·몸·다리 형태 변화는 남는 검토 대상이다.

후보별 native/indexed/흰 비교/4배/원본 셀과 무손실 루프·키판·접합판을 제공한다. A와 D의 native 픽셀과 순서는 같고 시간만 다르다. 화면은 동시 재생·정지·0.5배속·확대·접합부 비교와 후보별 키 선택을 제공한다. 접합부6장 모드의 바깥 반복은 실제 전체 루프 경계가 아니며 가운데 마지막→처음 전환을 비교한다. 사용자 선택 전이며 등짐분사수·쐐기보행기 채택본과 Unity 통합·전투 규칙은 변경하지 않는다.
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
    with zipfile.ZipFile(out/'m3-walk-candidates-20261101.zip','w',zipfile.ZIP_DEFLATED) as archive:
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
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>먹침필경사 · 걷기 후보 A~D</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1500px;margin:auto;padding:24px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}section{min-width:0;background:white;padding:18px}.viewport{background:white;overflow:auto}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.controls{position:sticky;top:0;background:#eeeef2f7;padding:10px;z-index:1}button,select{padding:8px;margin:3px}a{color:#815149}input{width:100%}h2{font-size:22px}@media(max-width:850px){.grid{grid-template-columns:1fr}}</style><main><h1>먹침필경사 · 걷기 후보 A~D</h1><p>A/B/C는 서로 다른 보행 구간을 같은0.8초로 비교합니다. D는 A와 같은 픽셀18장을1초로 늦춘 속도 비교입니다. 우선 추천은 A이며 아직 채택하지 않았습니다. 100장 전체가 아니라 각 후보의 선정 프레임만 재생합니다.</p><p>모두 기존9색·384×224·같은 배율과 위치 기준입니다. 최초 참고 자세 높이59px에 맞췄으며 반환 보행에서 다리를 더 펴는 높이 변화는 남겼습니다. 작은 빈 픽셀과 흰 외곽 잡점은 정리했습니다. PNG에 시간이 없어 각 시간은 검토 제안입니다.</p><p><a download href="m3-walk-candidates-20261101.zip">후보 PNG·4배·원본 셀·참조 ZIP</a> · <a href="manifest.json">공통 규격·팔레트·원본 대응</a></p><div class="controls"><button id="play">전체 정지</button><button id="restart">동시에 처음부터</button><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option></select><select id="scale"><option value="1">실제1배</option><option value="2">2배</option><option value="3" selected>3배</option><option value="4">4배</option><option value="8">8배</option></select><select id="mode"><option value="full">전체 반복</option><option value="seam">마지막3장 → 처음3장만</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 배경</option><option value="#708b92">푸른 배경</option></select></div><div class="grid">'''+''.join(cards)+'''</div><p>접합부6장 모드에서는 가운데 마지막→처음 전환을 비교하세요. 여섯 번째→첫 번째는 전체 루프의 실제 경계가 아닙니다. 원본의 침·다리 형태 변화는 남아 있을 수 있으며 새 포즈 생성·보간·파츠 합성·프레임별 확대축소는 하지 않았습니다. 캐릭터 이동 아트 검토본이며 게임 캡처·Unity 적용 결과가 아닙니다. 등짐분사수와 쐐기보행기 채택본은 유지합니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


if __name__=='__main__':build()
