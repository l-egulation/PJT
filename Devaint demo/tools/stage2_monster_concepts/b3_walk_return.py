"""Extract the returned B3 sheet and prepare a selected palette-limited walk review."""
from pathlib import Path
import json
import shutil
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from k4_d_pixel_walk import ROOT, N, sha, foreground_bounds, sample, tidy, map_preview
from k4_d_pixel_cleanup import fill_holes
from walk_review import animate

SPEC = ROOT/'data/presentation/stage2_b3_walk_return.json'


def dump(path, value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def white(im):
    bg=Image.new('RGBA',im.size,'white');bg.alpha_composite(im)
    return bg.convert('RGB')


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];base=ROOT/spec['base']
    for folder in ('source','all-frames','selected-source','native','native-indexed','white-native','enlarged-4x','previews','references','masks'):
        (out/folder).mkdir(parents=True,exist_ok=True)
    source=out/'source'/Path(spec['sourceFile']).name
    if not source.exists():shutil.copy2(spec['sourceFile'],source)
    Image.MAX_IMAGE_PIXELS=100000000
    w,h=spec['cellSize'];cols,rows=spec['grid']
    sheet=Image.open(source);assert sheet.size==(w*cols,h*rows)
    hashes=[]
    for i in range(cols*rows):
        p=out/'all-frames'/f'frame-{i:03}.png'
        # Always extract from the preserved source so reruns cannot inherit altered cells.
        x,y=i%cols*w,i//cols*h;sheet.crop((x,y,x+w,y+h)).convert('RGBA').save(p)
        hashes.append(sha(p))
    basic=Image.open(base/'native/base-frame.png').convert('RGBA');b=np.array(basic)
    palette=np.unique(b[b[:,:,3]>0,:3],axis=0);assert len(palette)==10
    shutil.copy2(base/'native/base-frame.png',out/'references/base-frame.png')
    shutil.copy2(base/'palette.json',out/'references/palette.json')
    shutil.copy2(ROOT/'outputs/waterway/walk-retry-white-20261031/B3/prompt-white.txt',out/'references/input-prompt.txt')
    anchor_boxes=np.array([foreground_bounds(Image.open(out/'all-frames'/f'frame-{i:03}.png')) for i in spec['scaleReferenceFrames']])
    scale=spec['targetReferenceHeight']/float(np.median(anchor_boxes[:,3]-anchor_boxes[:,1]))
    anchor=[float(np.median((anchor_boxes[:,0]+anchor_boxes[:,2])/2)),float(np.median(anchor_boxes[:,3]))]
    offset=[192-anchor[0]*scale,spec['baseline']-anchor[1]*scale]
    start,end=spec['selectedRangeInclusiveZeroBased'];indices=list(range(start,end+1));frames=[];mapping=[];originals=[]
    cumulative=[round(spec['cycleMsProposal']*i/len(indices)) for i in range(len(indices)+1)]
    durations=[b-a for a,b in zip(cumulative,cumulative[1:])]
    pal=np.concatenate([np.zeros((1,3),np.uint8),palette])
    for key,index in enumerate(indices,1):
        original=Image.open(out/'all-frames'/f'frame-{index:03}.png').convert('RGBA');originals.append(original)
        original.save(out/'selected-source'/f'walk-{key:02}.png')
        raw=sample(original,palette,scale,offset,spec);clean=tidy(raw,basic)
        a,holes=fill_holes(np.array(clean),spec['pinholeCleanup']);frame=Image.fromarray(a);frames.append(frame)
        name=f'walk-{key:02}.png';frame.save(out/'native'/name)
        white(frame).save(out/'white-native'/name);frame.resize((1536,896),N).save(out/'enlarged-4x'/name)
        index_map=np.zeros(a.shape[:2],np.uint8)
        for c,rgb in enumerate(palette,1):index_map[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=c
        indexed=Image.fromarray(index_map);indexed.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));indexed.info['transparency']=0;indexed.save(out/'native-indexed'/name)
        assert indexed.convert('RGBA').tobytes()==frame.tobytes()
        changed=np.any(np.array(raw)!=a,axis=2);mask=np.zeros_like(a);mask[changed]=[220,30,140,255];Image.fromarray(mask).save(out/'masks'/name)
        bbox=frame.getbbox();mapping.append({'key':key,'sourceIndexZeroBased':index,'sourceOneBased':index+1,'sourceSha256':hashes[index],
            'bbox':list(bbox),'visibleSize':[bbox[2]-bbox[0],bbox[3]-bbox[1]],'cleanupPixels':int(changed.sum()),'filledHoles':holes,'png':f'native/{name}'})
        print('B3',key,'source',index+1,'visible',mapping[-1]['visibleSize'],'cleanup',mapping[-1]['cleanupPixels'],flush=True)
    union=Image.new('RGBA',(384,224))
    for f in frames:union.alpha_composite(f)
    bb=union.getbbox();box=[bb[0]-8,bb[1]-8,bb[2]+8,bb[3]+8]
    animate(frames,durations,out/'previews/walk-native.webp');animate(frames,durations,out/'previews/walk-native.apng')
    crops=[white(f.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),N)).convert('RGBA') for f in frames]
    animate(crops,durations,out/'previews/walk-white-4x.webp')
    source_boxes=np.array([foreground_bounds(f) for f in originals]);sb=[int(source_boxes[:,0].min())-12,int(source_boxes[:,1].min())-12,int(source_boxes[:,2].max())+12,int(source_boxes[:,3].max())+12]
    animate([f.crop(sb) for f in originals],durations,out/'previews/selected-source.webp')
    board(frames,list(range(len(frames))),box,4,2,out/'previews/selected-19-white.png',mapping,durations)
    board(frames,[len(frames)-3,len(frames)-2,len(frames)-1,0,1,2],box,3,3,out/'previews/seam-white.png',mapping,durations)
    # Same fixed source geometry and source phase for a readable 1x baseline comparison.
    base_board=Image.new('RGB',(384*2,224*3),'white');draw=ImageDraw.Draw(base_board)
    for row,k in enumerate((0,6,12)):
        base_board.paste(white(basic),(0,row*224));base_board.paste(white(frames[k]),(384,row*224))
        draw.text((8,row*224+8),'BASE 180x100',fill='black');draw.text((392,row*224+8),f'WALK {k+1}',fill='black')
    base_board.resize((1536,1344),N).save(out/'previews/base-size-comparison-2x.png')
    map_preview(out,frames,durations)
    info={**spec,'sourceSha256':sha(source),'sheetSize':list(sheet.size),'extractedCount':100,'selectedCount':len(indices),'sourceIndicesZeroBased':indices,
          'sourceFrameHashes':hashes,'durationsMs':durations,'paletteRgb':palette.tolist(),'baseSha256':sha(base/'native/base-frame.png'),
          'scale':scale,'offset':offset,'sourceAnchor':anchor,'previewCrop':box,'mapping':mapping,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);shutil.copy2(SPEC,out/'walk-spec.json');page(out,info)
    (out/'README.md').write_text('''# 쐐기보행기 반환 걷기 · 픽셀 검토

source에 받은 원본 시트를 보존하고 all-frames에10×10 셀100장을 무변형 분리한다. selected-source는0기준40~58의 연속19장이다. 앞뒤 정지와 불필요한 반복 보행을 제외하고 지지대의 보행 위상과 본체가 이어지는 구간을 골랐다. PNG에는 시간이 없어 총1.3초·장당68/69ms는 검토 제안이다.

native는384×224 RGBA·이진 알파, native-indexed는 같은 픽셀의 고정10색 PNG, white-native는 흰 배경 비교, enlarged-4x는 정확한4배다. 기존 정지 기본본의 높이100px에 맞춰 반환 첫 정지 자세에서 배율·오프셋 하나를 정한 뒤 모든 보행에 유지한다. 걸음에 따른 실제 유효 영역 변화는 남는다. 기존10색만 사용하고 흰 혼합 가장자리·작은 잡점·4px 이하 내부 빈 픽셀을 정리한다. masks는 변환 직후에 비해 정리된 픽셀이다. 지지대 사이의 큰 여백은 보존한다.

화면은 선정19장만 재생하며 재생/정지·키 선택·확대·0.5배속·마지막/처음 비교를 제공한다. source 및 references에는 원본·기본본·팔레트·입력 프롬프트가 보존된다. 새 포즈 생성·보간·파츠 합성·프레임별 크기 조정은 없다. 반환 모션 자체의 판 모양·지지대 변화는 남을 수 있으며 이번 결과는 아트 검토본이다. 배경은 원본 그대로인 합성 미리보기이며 실제 게임 캡처가 아니다. 등짐분사수 채택본과 전투 규칙·Unity 통합은 변경하지 않는다.
''',encoding='utf-8')
    package(out)


def board(frames,keys,box,cols,scale,path,mapping,durations):
    w,h=(box[2]-box[0])*scale,(box[3]-box[1])*scale
    rows=(len(keys)+cols-1)//cols;result=Image.new('RGB',(w*cols,(h+28)*rows),'white');draw=ImageDraw.Draw(result)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    for pos,key in enumerate(keys):
        x,y=pos%cols*w,pos//cols*(h+28);result.paste(white(frames[key].crop(box).resize((w,h),N)),(x,y+28))
        draw.text((x+4,y+2),f'{key+1:02} / 원본 {mapping[key]["sourceOneBased"]} / {durations[key]}ms',font=font,fill='black')
    result.save(path)


def package(out):
    allowed={'selected-source','native','native-indexed','white-native','enlarged-4x','references','masks','index.html','preview.js','manifest.json','walk-spec.json','README.md','validation.json'}
    preview_names={'walk-native.webp','walk-native.apng','walk-white-4x.webp','selected-source.webp','selected-19-white.png','seam-white.png','base-size-comparison-2x.png','map-native.webp'}
    with zipfile.ZipFile(out/'b3-selected-pixel-walk-20261031.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(out.rglob('*')):
            rel=path.relative_to(out)
            if path.is_file() and (rel.parts[0] in allowed or (rel.parts[0]=='previews' and path.name in preview_names)):
                archive.write(path,rel.as_posix())


def page(out,info):
    js='''const data=DATA,images=[];let playing=true,key=0,elapsed=0,previous=null,rate=1,scale=4;
const c=document.querySelector('canvas'),ctx=c.getContext('2d'),box=data.previewCrop;
function draw(){let w=box[2]-box[0],h=box[3]-box[1];c.width=w;c.height=h;c.style.width=w*scale+'px';c.style.height=h*scale+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(images[key],box[0],box[1],w,h,0,0,w,h);document.querySelector('#key').value=key;document.querySelector('#label').textContent='선정 '+(key+1)+' / '+images.length+' · 원본 '+data.mapping[key].sourceOneBased+' · '+data.durationsMs[key]+'ms'}
Promise.all(data.mapping.map(row=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=row.png}))).then(arr=>{images.push(...arr);draw();requestAnimationFrame(tick)});
function tick(t){if(previous===null)previous=t;if(playing){elapsed=(elapsed+(t-previous)*rate)%data.cycleMsProposal;let rem=elapsed,next=0;while(next<data.selectedCount-1&&rem>=data.durationsMs[next]){rem-=data.durationsMs[next];next++}if(next!==key){key=next;draw()}}previous=t;requestAnimationFrame(tick)}
document.querySelector('#play').onclick=()=>{playing=!playing;previous=performance.now();document.querySelector('#play').textContent=playing?'정지':'재생'};
document.querySelector('#key').oninput=e=>{playing=false;document.querySelector('#play').textContent='재생';key=+e.target.value;elapsed=data.durationsMs.slice(0,key).reduce((a,b)=>a+b,0);draw()};
document.querySelector('#scale').onchange=e=>{scale=+e.target.value;draw()};document.querySelector('#rate').onchange=e=>rate=+e.target.value;document.querySelector('#bg').onchange=e=>document.querySelector('.viewport').style.background=e.target.value;'''.replace('DATA',json.dumps({k:info[k] for k in ('previewCrop','mapping','durationsMs','selectedCount','cycleMsProposal')}))
    (out/'preview.js').write_text(js,encoding='utf-8')
    thumbs=''.join(f'<a href="{r["png"]}">{r["key"]:02} · 원본{r["sourceOneBased"]}</a> ' for r in info['mapping'])
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>쐐기보행기 · 선정19장 픽셀 걷기</title><style>body{margin:0;background:#eee;color:#25242b;font:16px/1.7 system-ui}main{max-width:1200px;padding:24px;margin:auto}a{color:#815149}button,select{padding:8px;margin:4px}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.loop{display:block;margin:auto}.viewport{background:white;overflow:auto}.links a{display:inline-block;margin:5px}textarea{width:100%}</style><main><h1>쐐기보행기 · 선정19장 픽셀 걷기</h1><p>100개 셀 중 앞뒤 정지를 제외하고 원본41~59번의 연속19장을 골랐습니다. 아래 재생은 선정19장만 사용합니다. 기존10색·384×224 캔버스와 기본본 몸 높이100px 기준의 공통 배율을 유지했습니다. PNG에 시간 정보가 없어1.3초 주기는 검토 제안입니다.</p><p><a download href="b3-selected-pixel-walk-20261031.zip">선정 PNG·4배·원본 셀·참조 ZIP</a> · <a href="manifest.json">원본 대응·크기·색·시간</a></p><img class="loop" src="previews/walk-white-4x.webp"><h2>개별 키와 확대 확인</h2><button id="play">정지</button><select id="scale"><option value="1">실제1배</option><option value="2">2배</option><option value="4" selected>4배</option><option value="8">8배</option></select><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option></select><select id="bg"><option value="white">흰 배경</option><option value="#26313b">어두운 배경</option><option value="#708b92">푸른 배경</option></select><input id="key" type="range" min="0" max="18" value="0"><span id="label"></span><div class="viewport"><canvas></canvas></div><h2>마지막3장 → 처음3장</h2><img src="previews/seam-white.png"><h2>선정19장 · PNG</h2><img src="previews/selected-19-white.png"><div class="links">'''+thumbs+'''</div><h2>기존 기본본과 크기 비교 · 2배</h2><img src="previews/base-size-comparison-2x.png"><h2>실제 크기 · 기존 맵 합성</h2><img src="previews/map-native.webp" width="960" height="540"><p>원본960×540 배경과 승인 플레이어를 바꾸지 않은 합성 미리보기이며 실제 게임 캡처가 아닙니다.</p><details><summary>선정한 원본19장 재생 · 픽셀 변환 전</summary><img src="previews/selected-source.webp"><p>원본 셀은 selected-source에1280×720로 보존합니다. 이 화면은 보기용 공통 크롭입니다.</p></details><p>흰 혼합 가장자리·작은 잡점·내부 빈 픽셀을 기존 색으로 보정했습니다. 생성된 움직임의 지지대와 판 형태 변화는 검토 대상입니다. 새 포즈 생성·보간·파츠 합성·프레임별 확대축소는 하지 않았습니다. 등짐분사수 채택본은 유지하며 쐐기보행기는 아트 검토 단계입니다.</p></main><script src="preview.js"></script></html>''',encoding='utf-8')


if __name__=='__main__':build()
