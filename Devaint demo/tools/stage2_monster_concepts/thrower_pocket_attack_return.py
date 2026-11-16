"""Select the returned held-hand throw and pocket retrieval; export body-only pixels."""
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import binary_dilation, label

from b3_walk_return import dump, white
from k4_d_pixel_walk import ROOT, N, sha, sample, tidy
from k4_d_pixel_cleanup import fill_holes
from k4_attack_return import board
from thrower_walk_candidates import recover_cloth
from walk_review import animate

SPEC = ROOT/'data/presentation/stage2_thrower_pocket_attack_return.json'


def body_part(im, fringe):
    a = np.array(im.convert('RGBA'))
    ids, _ = label((a[:,:,:3].min(2)<230)&(a[:,:,3]>0))
    counts = np.bincount(ids.ravel()); counts[0] = 0
    main = ids == counts.argmax()
    yy, xx = np.where(main)
    box = [int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
    keep = binary_dilation(main,iterations=fringe)
    body = a.copy(); body[~keep] = [255,255,255,255]
    detached = a.copy(); detached[keep] = [255,255,255,255]
    return Image.fromarray(body), Image.fromarray(detached), box, int(((ids>0)&~keep).sum())


def decoded(path):
    im=Image.open(path); result=[]
    for i in range(im.n_frames):
        im.seek(i); im.load(); result.append((im.convert('RGBA').copy(),round(im.info['duration'])))
    return result


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8')); out=ROOT/s['output']; base=ROOT/s['base']
    for folder in ('source','all-frames','selected-source','body-source','detached-source','native',
                   'native-indexed','white-native','enlarged-4x','masks','references','previews'):
        (out/folder).mkdir(parents=True,exist_ok=True)
    src=out/'source'/Path(s['sourceFile']).name
    if not src.exists(): shutil.copy2(s['sourceFile'],src)
    assert sha(src)==sha(Path(s['sourceFile']))
    Image.MAX_IMAGE_PIXELS=100000000
    sheet=Image.open(src); w,h=s['cellSize']; cols,rows=s['grid']
    assert sheet.size==(cols*w,rows*h)
    hashes=[]
    for i in range(cols*rows):
        x,y=i%cols*w,i//cols*h; cell=sheet.crop((x,y,x+w,y+h)).convert('RGBA')
        p=out/'all-frames'/f'source-{i+1:03}.png';cell.save(p);hashes.append(sha(p))
        assert Image.open(p).tobytes()==cell.tobytes()
    basic=Image.open(base/'native/base-frame.png').convert('RGBA'); b=np.array(basic)
    palette=np.unique(b[b[:,:,3]>0,:3],axis=0); assert len(palette)==12
    reference=Image.open(out/'all-frames'/f'source-{s["scaleReferenceSourceOneBased"]:03}.png')
    _,_,box,_=body_part(reference,s['bodyComponentFringeSourcePixels'])
    scale=s['targetReferenceHeight']/(box[3]-box[1])
    offset=[192-(box[0]+box[2])/2*scale,s['baseline']-box[3]*scale]
    cloth=json.loads((ROOT/s['clothRecoverySpec']).read_text(encoding='utf-8'))['clothRecovery']
    references={'base-frame.png':base/'native/base-frame.png','palette.json':base/'palette.json',
                'input-prompt.txt':ROOT/s['inputHandoff']/'prompt-en.txt',
                'input-manifest.json':ROOT/s['inputHandoff']/'manifest.json',
                'cloth-recovery-spec.json':ROOT/s['clothRecoverySpec'],
                'C-game.png':ROOT/s['mapReference'],'presentation-spec.json':SPEC}
    for name,p in references.items():shutil.copy2(p,out/'references'/name)
    handoff=json.loads((out/'references/input-manifest.json').read_text(encoding='utf-8'))
    assert [sum(p['durationsMs']) for p in s['phases']]==[round(p['seconds']*1000) for p in handoff['phaseProposal']]
    pal=np.concatenate([np.zeros((1,3),np.uint8),palette])
    frames=[]; mapping=[]; phases=[]; ds=[]
    for phase in s['phases']:
        assert len(phase['sourceOneBased'])==len(phase['durationsMs'])
        start=sum(ds); keys=[]
        for number,time in zip(phase['sourceOneBased'],phase['durationsMs']):
            key=len(frames)+1; name=f'attack-{key:02}.png'
            original=Image.open(out/'all-frames'/f'source-{number:03}.png').convert('RGBA')
            original.save(out/'selected-source'/name)
            if number in s['removeDetachedProjectileFromSourceOneBased']:
                body,detached,_,removed=body_part(original,s['bodyComponentFringeSourcePixels'])
                detached.save(out/'detached-source'/name)
            else:body=original;removed=0
            body.save(out/'body-source'/name)
            raw=sample(body,palette,scale,offset,s); clean=tidy(raw,basic)
            a,cloth_mask=recover_cloth(np.array(clean),body,palette,scale,offset,cloth)
            a,holes=fill_holes(a,s['pinholeCleanup']);frame=Image.fromarray(a)
            frame.save(out/'native'/name);white(frame).save(out/'white-native'/name)
            frame.resize((1536,896),N).save(out/'enlarged-4x'/name)
            ix=np.zeros(a.shape[:2],np.uint8)
            for c,rgb in enumerate(palette,1):ix[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=c
            indexed=Image.fromarray(ix);indexed.putpalette(pal.ravel().tolist()+[0]*(768-pal.size))
            indexed.info['transparency']=0;indexed.save(out/'native-indexed'/name)
            assert indexed.convert('RGBA').tobytes()==frame.tobytes()
            assert Image.open(out/'enlarged-4x'/name).tobytes()==frame.resize((1536,896),N).tobytes()
            assert set(np.unique(a[:,:,3]))<={0,255}
            assert set(map(tuple,np.unique(a[a[:,:,3]>0,:3],axis=0)))<=set(map(tuple,palette))
            bb=frame.getbbox();assert 0<bb[0]<bb[2]<384 and 0<bb[1]<bb[3]<224
            changed=np.any(a!=np.array(raw),axis=2);mask=np.zeros_like(a);mask[changed]=[230,40,130,255]
            Image.fromarray(mask).save(out/'masks'/name)
            frames.append(frame);ds.append(time);keys.append(key)
            mapping.append({'key':key,'phase':phase['id'],'phaseName':phase['name'],'sourceOneBased':number,
                 'sourceSha256':hashes[number-1],'png':f'native/{name}','sha256':sha(out/'native'/name),
                 'durationMs':time,'bbox':list(bb),'cleanupPixels':int(changed.sum()),
                 'clothPixelsRecovered':int(cloth_mask.sum()),'holesFilled':holes,
                 'detachedSourcePixelsRemoved':removed})
        phases.append({**phase,'startMs':start,'endMs':sum(ds),'keysOneBased':keys})
    assert len(frames)==19 and sum(ds)==1450
    assert frames[0].tobytes()==frames[-1].tobytes()
    union=Image.new('RGBA',(384,224))
    for f in frames:union.alpha_composite(f)
    bb=union.getbbox();crop=[bb[0]-10,bb[1]-10,bb[2]+10,bb[3]+10]
    crops=[white(f.crop(crop).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N)).convert('RGBA') for f in frames]
    for name,poses,times in [('attack-native.webp',frames,ds),('attack-native.apng',frames,ds),
              ('attack-white-4x.webp',crops,ds),('review-white-4x.webp',crops+[crops[-1]],ds+[s['previewGapMs']])]:
        animate(poses,times,out/'previews'/name)
    for ext in ('webp','apng'):
        got=decoded(out/'previews'/f'attack-native.{ext}')
        assert len(got)==19
        for (actual,time),wanted,duration in zip(got,frames,ds):
            assert actual.tobytes()==wanted.tobytes() and time==duration
    scene=Image.open(out/'references/C-game.png').convert('RGBA'); assert scene.size==(960,540)
    maps=[]
    for f in frames:
        c=scene.copy();c.alpha_composite(f,tuple(s['mapOffset']));maps.append(c)
    animate(maps+[maps[-1]],ds+[s['previewGapMs']],out/'previews/map.webp')
    board(frames,mapping,crop,list(range(19)),out/'previews/selected-19.png')
    board(frames,mapping,crop,[8,9,10,12,13,14,15,16,17],out/'previews/pocket-and-release.png')
    info={**s,'sourceSha256':sha(src),'sourceFrameHashes':hashes,'sheetSize':list(sheet.size),
          'frameCount':19,'attackMsProposal':1450,'durationsMs':ds,'mapping':mapping,
          'phases':phases,'scale':scale,'offset':offset,'scaleReferenceBodyBounds':box,
          'paletteRgb':palette.tolist(),'previewCrop':crop,'conceptApproval':'pocket-retrieval-liked-by-user',
          'releaseMsProposal':sum(ds[:s['releaseKeyOneBased']-1]),'runtimeIntegration':'not-performed',
          'limitation':'Source body shape, prop shape and foot movement retained. Separated flight fragments are source reference, not finalized gameplay projectiles.'}
    dump(out/'manifest.json',info);dump(out/'sequence.json',mapping);page(out,info)
    (out/'README.md').write_text('''# 신호투척수 · 주머니 재보급 공격19장

사용자는 주머니에서 다음 물체를 꺼내는 컨셉을 선호했다. 받은12800×7200 PNG를100셀로 무변형 분리하고 준비6장400ms·고정2장200ms·투척3장150ms·빈손/주머니 회복8장700ms로19장을 고른다. 마지막에는 원본2번 중립을 재사용해 시작과 같은 픽셀로 끝난다. 총1450ms와 반복 간격600ms는 별개이며 PNG에는 시간이 없어 제작 검토 제안이다. 전체 픽셀/속도/프레임 선택은 아직 사용자 승인 전이다.

실제 중립2번 높이를106논리px에 맞추는 한 배율과 위치,384×224·실제 기본본12색·이진 알파·정수4배를 사용한다. 소스의 폭/얼굴/발/소품 변형은 남기며 프레임별 등록/크기 보정이나 새 포즈·보간을 생성하지 않는다. 기존 바지 회복기를 사용해 원본에 있는 회색/보라색 옷 면이 팔레트 변환에서 빠지지 않도록 보강하고 작은 내부 빈 픽셀/잡점을 정리한다. 정리 마스크와 원본을 함께 보존한다.

명시한 원본26~30번에서 가장 큰 연결 본체와2원본px 외곽 여유를 남겨 독립 비행체를 분리한다. 실제 선정본은26/27/29번을 처리한다. 원본 셀/분리 전 선정셀은 그대로 남기고 body-source와 detached-source를 별도로 보존한다. 비행 조각은 참고이며 완성 투사체/게임 탄도가 아니다. 놓기 기준은 키11 시작680ms의 검토 표시이며 판정을 확정하지 않는다.

본체 native/indexed/흰배경/4배·원본/선정셀·참조/프롬프트·시간표·키판·WebP/APNG·맵합성과 ZIP을 제공한다. 맵은 기존960×540 배경/플레이어를 변경하지 않은 합성 미리보기이며 실제 게임 캡처가 아니다. 주머니 동작 선호와 실제 모션 전체 채택을 구분하며 전투/Unity/피해 판정은 변경하지 않는다.
''',encoding='utf-8')
    if s['approval']=='approved-by-user':
        p=out/'README.md'
        p.write_text(p.read_text(encoding='utf-8').replace('전체 픽셀/속도/프레임 선택은 아직 사용자 승인 전이다.','사용자가 선정19장의 픽셀/프레임 선택과1450ms 박자를 채택했다.').replace('주머니 동작 선호와 실제 모션 전체 채택을 구분하며','본체 모션 채택과 게임 적용을 구분하며'),encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','sourceCells':100,'selectedFrames':19,'actualPaletteColors':12,
         'canvas':[384,224],'binaryAlpha':True,'indexedRgbaExact':True,'nearest4xExact':True,
         'webpApngPixelsAndTimesExact':True,'attackMsProposal':1450,'reviewGapMs':600,
         'startEndPixelsIdentical':True,'detachedProjectileSources':[26,27,29],
         'browserQA':'not-run','Unity':'not-run'})
    with zipfile.ZipFile(out/'thrower-pocket-attack-19.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and p.name not in {'diagnostic.json','overview-first50.png','commit-message.txt'}:
                z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'thrower-pocket-attack-19.zip') as z:assert z.testzip() is None
    print(json.dumps({'frames':19,'durationMs':1450,'scale':scale,'crop':crop,'releaseMs':info['releaseMsProposal']}))


def page(out,info):
    js='''const DATA=REPLACE;const c=document.querySelector('canvas'),ctx=c.getContext('2d');let images=[],playing=true,time=0,prev=null,rate=1;
function draw(k){const box=DATA.previewCrop;c.width=box[2]-box[0];c.height=box[3]-box[1];c.style.width=c.width*+document.querySelector('#zoom').value+'px';ctx.imageSmoothingEnabled=false;ctx.drawImage(images[k],box[0],box[1],c.width,c.height,0,0,c.width,c.height);const r=DATA.mapping[k];document.querySelector('#step').value=k;document.querySelector('#label').textContent='키'+(k+1)+' · '+r.phaseName+' · 원본'+r.sourceOneBased+' · '+r.durationMs+'ms'+(k+1===DATA.releaseKeyOneBased?' · 물체 놓기 기준':'')}
function reset(){time=0;prev=null;draw(0)}
function tick(t){if(prev===null)prev=t;if(playing){time+=(t-prev)*rate;let rem=time%(DATA.attackMsProposal+DATA.previewGapMs),k=DATA.frameCount-1;for(let i=0;i<DATA.frameCount;i++){if(rem<DATA.durationsMs[i]){k=i;break}rem-=DATA.durationsMs[i]}draw(k)}prev=t;requestAnimationFrame(tick)}
Promise.all(DATA.mapping.map(r=>new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=r.png}))).then(ims=>{images=ims;reset();requestAnimationFrame(tick)});
document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'정지':'재생'};document.querySelector('#restart').onclick=reset;document.querySelector('#rate').onchange=e=>rate=+e.target.value;document.querySelector('#zoom').onchange=()=>draw(+document.querySelector('#step').value);document.querySelector('#step').oninput=e=>{playing=false;document.querySelector('#play').textContent='재생';time=DATA.durationsMs.slice(0,+e.target.value).reduce((n,x)=>n+x,0);draw(+e.target.value)};document.querySelector('#bg').onchange=e=>document.querySelector('.viewport').style.background=e.target.value;document.querySelector('#phase').onchange=e=>{if(e.target.value==='all'){reset();return}const p=DATA.phases.find(p=>p.id===e.target.value);playing=false;document.querySelector('#play').textContent='재생';time=p.startMs;draw(p.keysOneBased[0]-1)};
'''.replace('REPLACE',json.dumps(info,ensure_ascii=False))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>신호투척수 · 주머니 재보급19장</title><style>body{margin:0;background:#eee;color:#28252c;font:16px/1.7 system-ui}main{max-width:1150px;padding:28px;margin:auto}section{background:white;padding:20px;margin:20px 0}h1{font-size:28px}h2{font-size:22px}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.viewport{overflow:auto;padding:12px}button,select{padding:8px;margin:4px}input{width:100%}a{color:#814d69}</style><main><h1>신호투척수 · 던지고 주머니에서 다시 꺼내기</h1><p>물체를 쥔 팔로 준비·투척 → 빈손 회복 → 주머니에서 다음 물체 꺼내기. 주머니 컨셉 선호를 반영한 선정19장 검토이며 전체 모션은 아직 채택 전입니다.</p><p><a download href="thrower-pocket-attack-19.zip">PNG·원본·참조·시간표 ZIP</a> · <a href="manifest.json">원본 대응·규격</a> · <a href="sequence.json">선정 순서</a></p><section><h2>본체 픽셀 미리보기</h2><p>준비400ms · 고정200ms · 투척150ms · 빈손/재보급700ms = 1.45초. 반복 간격600ms는 확인용입니다. 키11 시작680ms를 놓기 기준으로 표시하며 게임 판정은 확정하지 않습니다.</p><button id="play">정지</button><button id="restart">처음부터</button><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option><option value="0.25">0.25배속</option></select><select id="zoom"><option value="1">실제1배</option><option value="2">2배</option><option value="3" selected>3배</option><option value="4">4배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#25252b">어두운 배경</option></select><select id="phase"><option value="all">처음부터</option><option value="aim">준비 확인</option><option value="lock">고정 확인</option><option value="throw">놓기 확인</option><option value="recovery">주머니 회복 확인</option></select><div class="viewport"><canvas></canvas></div><p id="label"></p><input id="step" type="range" min="0" max="18" value="0"><p><a href="previews/review-white-4x.webp">확대 재생</a> · <a href="previews/attack-native.apng">원배율 APNG</a></p></section><section><h2>놓기·빈손·다시 꺼내기</h2><img src="previews/pocket-and-release.png" alt="놓기와 주머니 재보급 선정키"></section><section><h2>선정19장</h2><img src="previews/selected-19.png" alt="선정19장과 단계/원본 번호"></section><section><h2>기존 맵 합성 · 960×540</h2><img src="previews/map.webp" alt="기존 맵에 합성한 신호투척수 공격"><p>합성 미리보기이며 실제 게임 캡처가 아닙니다. 기존 배경과 플레이어는 변경하지 않았습니다.</p></section><p>기존 실제12색·384×224·이진 알파·정수4배입니다. 실제 중립2번 높이106px를 기준으로 한 배율/위치를 사용하며 원본 폭·발·얼굴·소품의 형태 변화는 남습니다. 마지막에는 원본2번 중립을 재사용합니다. 분리된 비행체는 본체에서 제거하고 원본/분리 참고를 보존했습니다. 새로운 포즈나 보간은 만들지 않았고 투사체/피해 판정/Unity 연결은 별도입니다.</p><script src="preview.js"></script></main></html>''',encoding='utf-8')


    if info['approval']=='approved-by-user':
        p=out/'index.html'
        p.write_text(p.read_text(encoding='utf-8').replace('주머니 컨셉 선호를 반영한 선정19장 검토이며 전체 모션은 아직 채택 전입니다.','사용자가 선정19장의 픽셀과1.45초 박자를 채택했습니다. 투사체와 게임 적용은 별도입니다.'),encoding='utf-8')


if __name__=='__main__':build()
