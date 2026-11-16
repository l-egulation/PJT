"""Extract complete orb formation/release cells and composite unchanged approved H3 float."""
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import binary_fill_holes

from b3_walk_return import dump, white
from k4_d_pixel_walk import ROOT, N, sha, sample
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_h3_orb_return.json'


def bounds(im):
    a=np.array(im.convert('RGBA'));y,x=np.where((a[:,:,:3].min(2)<230)&(a[:,:,3]>0))
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)] if len(x) else None


def merged_segments(frames,times):
    result=[]
    for frame,time in zip(frames,times):
        pixels=frame.convert('RGBA').tobytes()
        if result and result[-1][0]==pixels:result[-1]=(pixels,result[-1][1]+time)
        else:result.append((pixels,time))
    return result


def check_animation(path,frames,times):
    im=Image.open(path);decoded=[];ds=[]
    for i in range(im.n_frames):
        im.seek(i);im.load();decoded.append(im.convert('RGBA').copy());ds.append(round(im.info['duration']))
    assert merged_segments(decoded,ds)==merged_segments(frames,times),str(path)


def restore_filled_material(frame,original,palette,scale,offset,grid):
    """Restore enclosed source-colored surfaces, never genuinely white formation gaps."""
    a=np.array(frame);source=np.array(original.convert('RGBA'))
    holes=binary_fill_holes(a[:,:,3]>0)&(a[:,:,3]==0)
    yy,xx=np.where(holes);coverage=np.zeros(len(xx),int);votes=np.zeros((len(xx),len(palette)),int)
    for dy in range(grid):
        sy=np.floor((yy+(dy+.5)/grid-offset[1])/scale).astype(int)
        for dx in range(grid):
            sx=np.floor((xx+(dx+.5)/grid-offset[0])/scale).astype(int)
            valid=(sx>=0)&(sy>=0)&(sx<source.shape[1])&(sy<source.shape[0])
            q=source[np.clip(sy,0,source.shape[0]-1),np.clip(sx,0,source.shape[1]-1)]
            fg=valid&(q[:,3]>0)&(q[:,:3].min(1)<230)
            material=((q[:,:3,None].astype(int)-palette.T[None].astype(int))**2).sum(1).argmin(1)
            coverage+=fg
            for c in range(len(palette)):votes[:,c]+=fg&(material==c)
    restore=coverage>=int(np.ceil(grid*grid*.5));mask=np.zeros(a.shape[:2],bool)
    a[yy[restore],xx[restore],:3]=palette[votes[restore].argmax(1)]
    a[yy[restore],xx[restore],3]=255;mask[yy[restore],xx[restore]]=True
    return Image.fromarray(a),mask


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];body_source=ROOT/s['bodyFloat']
    for name in ('source','all-frames','selected-source','native','native-indexed','white-native','enlarged-8x','masks','references/body-float/native','projectile-reference','previews'):
        (out/name).mkdir(parents=True,exist_ok=True)
    src=out/'source'/Path(s['sourceFile']).name
    if not src.exists():shutil.copy2(s['sourceFile'],src)
    assert sha(src)==sha(Path(s['sourceFile']))
    sheet=Image.open(src);w,h=s['cellSize'];cols,rows=s['grid'];assert sheet.size==(w*cols,h*rows)
    hashes=[];boxes=[]
    for i in range(cols*rows):
        x,y=i%cols*w,i//cols*h;cell=sheet.crop((x,y,x+w,y+h)).convert('RGBA')
        path=out/'all-frames'/f'source-{i+1:02}.png';cell.save(path)
        assert Image.open(path).tobytes()==cell.tobytes();hashes.append(sha(path));boxes.append(bounds(cell))
    assert boxes[14][0]==0 and boxes[15][0]==0 and all(x is None for x in boxes[16:])
    ref=boxes[s['referenceSourceOneBased']-1]
    scale=s['targetReferenceHeight']/(ref[3]-ref[1]);center=s['orbCenter']
    offset=[center[0]-(ref[0]+ref[2])/2*scale,center[1]-(ref[1]+ref[3])/2*scale]
    palette=np.array(s['paletteRgb'],np.uint8);pal=np.concatenate([np.zeros((1,3),np.uint8),palette])
    frames=[];times=[];mapping=[];phases=[]
    for phase in s['phases']:
        assert len(phase['sourceOneBased'])==len(phase['durationsMs']);start=sum(times);keys=[]
        for number,time in zip(phase['sourceOneBased'],phase['durationsMs']):
            key=len(frames)+1;name=f'orb-{key:02}.png';source=out/'all-frames'/f'source-{number:02}.png'
            shutil.copy2(source,out/'selected-source'/name)
            original=Image.open(source).convert('RGBA')
            frame=sample(original,palette,scale,offset,s)
            frame,restored=restore_filled_material(frame,original,palette,scale,offset,s['supersampleGrid'])
            mask=np.zeros((*restored.shape,4),np.uint8);mask[restored]=[230,40,130,255];Image.fromarray(mask).save(out/'masks'/name)
            frame.save(out/'native'/name);white(frame).save(out/'white-native'/name)
            frame.resize((s['canvas'][0]*8,s['canvas'][1]*8),N).save(out/'enlarged-8x'/name)
            a=np.array(frame);ix=np.zeros(a.shape[:2],np.uint8)
            for c,rgb in enumerate(palette,1):ix[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=c
            indexed=Image.fromarray(ix);indexed.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));indexed.info['transparency']=0;indexed.save(out/'native-indexed'/name)
            assert indexed.convert('RGBA').tobytes()==frame.tobytes()
            assert Image.open(out/'enlarged-8x'/name).tobytes()==frame.resize((1024,768),N).tobytes()
            assert set(np.unique(a[:,:,3]))<={0,255}
            assert set(map(tuple,np.unique(a[a[:,:,3]>0,:3],axis=0)))<=set(map(tuple,palette))
            bb=frame.getbbox()
            if bb:assert 0<bb[0]<bb[2]<128 and 0<bb[1]<bb[3]<96
            mapping.append({'key':key,'phase':phase['id'],'phaseName':phase['name'],'sourceOneBased':number,'durationMs':time,
                            'png':f'native/{name}','sha256':sha(out/'native'/name),'sourceSha256':hashes[number-1],
                            'bbox':list(bb) if bb else None,'opaquePixels':int((a[:,:,3]>0).sum()),'sourceMaterialPixelsRestored':int(restored.sum())})
            frames.append(frame);times.append(time);keys.append(key)
        phases.append({**phase,'startMs':start,'endMs':sum(times),'keysOneBased':keys})
    assert len(frames)==14 and sum(times)==1950 and frames[-1].getbbox() is None
    assert all(f.getbbox() for f in frames[:-1])
    assert all(row['sourceOneBased'] not in (1,15,16) for row in mapping)
    # This is only an uncropped fully formed projectile appearance reference.
    formed_key=next(i for i,r in enumerate(mapping) if r['sourceOneBased']==9)
    projectile=frames[formed_key].crop((32,16,96,80));assert projectile.size==(64,64)
    assert np.count_nonzero(np.array(projectile)[:,:,3])==np.count_nonzero(np.array(frames[formed_key])[:,:,3])
    projectile.save(out/'projectile-reference/full-orb.png')
    white(projectile).resize((512,512),N).save(out/'projectile-reference/full-orb-white-8x.png')
    input_dir=ROOT/s['inputHandoff']
    for p,name in [(input_dir/'prompt-orb-en.txt','input-prompt.txt'),(input_dir/'manifest.json','input-manifest.json'),
                   (input_dir/'orb/native/ink-orb.png','input-orb-64.png'),(SPEC,'return-spec.json'),
                   (ROOT/s['combatReference'],'combat-patterns.json'),(ROOT/s['mapReference'],'C-game.png')]:shutil.copy2(p,out/'references'/name)
    handoff=json.loads((out/'references/input-manifest.json').read_text(encoding='utf-8'))
    assert [sum(p['durationsMs']) for p in s['phases']]==[round(p['seconds']*1000) for p in handoff['phaseProposal']]
    body_info=json.loads((body_source/'manifest.json').read_text(encoding='utf-8'))
    shutil.copy2(body_source/'manifest.json',out/'references/body-float/manifest.json')
    body_files=sorted((body_source/'native').glob('*.png'));assert len(body_files)==16
    body=[];body_hashes=[]
    for p in body_files:
        target=out/'references/body-float/native'/p.name;shutil.copy2(p,target);assert sha(p)==sha(target)
        body.append(Image.open(p).convert('RGBA'));body_hashes.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p)})
    assert sum(body_info['durationsMs'])==1600
    assert sum(times)+s['reviewGapMs']==s['previewCycleMs']==2*body_info['cycleMs']
    def pick(ds,time):
        remaining=time%sum(ds)
        for i,duration in enumerate(ds):
            if remaining<duration:return i
            remaining-=duration
        raise AssertionError('timeline')
    events={0,s['previewCycleMs']};acc=0
    for duration in times:acc+=duration;events.add(acc)
    for repeat in range(2):
        acc=repeat*body_info['cycleMs']
        for duration in body_info['durationsMs']:events.add(acc);acc+=duration
    points=sorted(events);merged=[];merged_times=[];merged_mapping=[]
    position=[s['orbCenterInBodyCanvas'][0]-center[0],s['orbCenterInBodyCanvas'][1]-center[1]]
    scene=Image.open(out/'references/C-game.png').convert('RGBA');assert scene.size==(960,540)
    maps=[]
    for start,end in zip(points,points[1:]):
        bi=pick(body_info['durationsMs'],start);oi=pick(times,start) if start<1950 else len(frames)-1
        composition=Image.new('RGBA',tuple(s['bodyCanvas']));composition.alpha_composite(frames[oi],tuple(position));composition.alpha_composite(body[bi])
        occupied=np.array(body[bi])[:,:,3]>0
        assert np.array_equal(np.array(composition)[occupied],np.array(body[bi])[occupied])
        merged.append(composition);merged_times.append(end-start)
        merged_mapping.append({'startMs':start,'endMs':end,'bodyKey':bi+1,'orbKey':oi+1})
        m=scene.copy();m.alpha_composite(composition,tuple(s['mapOffset']));maps.append(m)
    assert sum(merged_times)==3200
    for name,poses,ds in [('orb-native.webp',frames,times),('orb-native.apng',frames,times)]:
        animate(poses,ds,out/'previews'/name);check_animation(out/'previews'/name,poses,ds)
    orb_white=[white(f).resize((768,576),N).convert('RGBA') for f in frames]
    animate(orb_white+[orb_white[-1]],times+[s['reviewGapMs']],out/'previews/orb-white-6x.webp')
    union=Image.new('RGBA',(384,224))
    for f in merged:union.alpha_composite(f)
    bb=union.getbbox();crop=[bb[0]-10,bb[1]-10,bb[2]+10,bb[3]+10]
    views=[white(f.crop(crop)).resize(((crop[2]-crop[0])*4,(crop[3]-crop[1])*4),N).convert('RGBA') for f in merged]
    animate(views,merged_times,out/'previews/body-plus-orb-white-4x.webp')
    animate(maps,merged_times,out/'previews/map.webp')
    check_animation(out/'previews/body-plus-orb-white-4x.webp',views,merged_times)
    board(frames,mapping,out/'previews/selected-14.png')
    info={**s,'sourceSha256':sha(src),'sourceFrameHashes':hashes,'sourceBounds':boxes,'sourceSize':list(sheet.size),
          'frameCount':14,'visibleFrameCount':13,'attackMsProposal':1950,'durationsMs':times,'mapping':mapping,'phases':phases,
          'scale':scale,'offset':offset,'formedVisibleSize':[projectile.getbbox()[2]-projectile.getbbox()[0],projectile.getbbox()[3]-projectile.getbbox()[1]],
          'bodyFloatHashes':body_hashes,'bodyPixelsChanged':0,'bodyTimingChanged':False,'bodyDurationsMs':body_info['durationsMs'],
          'compositeTimeline':merged_mapping,'bodyPreviewCrop':crop,'orbOffsetInBodyCanvas':position,'runtimeIntegration':'not-performed',
          'limitation':'Formation has intentional disconnected clusters and gaps. Source15/16 are clipped and excluded. Source13->14 moves farther than the8px prompt; the128x96 export preserves it without rescaling. Projectile appearance is reference only; continued arc/impact/hazard are separate.'}
    dump(out/'manifest.json',info);dump(out/'sequence.json',mapping);dump(out/'composite-timeline.json',merged_mapping);page(out,info)
    (out/'README.md').write_text('''# 늘어진 수의 · 반환 잉크 구 생성/발사

받은3840×2160 시트를6×6/640×360의36셀로 무변형 분리한다.1번 참고와 왼쪽이 잘린15/16번은 원본에 보존하고 제외한다. 생성2~9번8장850ms·분리/발사10~14번5장200ms·빈17번900ms로14장1950ms를 선정한다. PNG에는 시간이 없어 기존 제안에 맞춘 검토 타이밍이다. 원본의13→14 이동 폭은 입력의8px보다 크며 그대로 유지한다. 이후 연속 비행/착탄/장판은 미제작이다.

완성9번 높이40px를 기준으로 한 배율·오프셋과 기존4색/이진 알파를 사용한다.64×64 입력보다 여백이 큰128×96 캔버스로 동작/꼬리를 보존하며 공 자체 크기는 늘리지 않는다. 형성 중 작은 덩어리와 빈 공간은 의도된 원본 모양이므로 최대 연결성/일괄 구멍 채우기/잡점 삭제를 적용하지 않는다. 흰 매트 혼합을 분리하고 팔레트 재질 투표로 픽셀화한다. 닫힌 내부 영역에서 원본16개 샘플 중 절반 이상에 실제 색이 있는 누락 면만 복구하고 마스크를 보존한다. 원본의 진짜 흰 공간과 외곽은 확장하지 않는다.14 native/indexed/흰배경/정수8배와 무손실 WebP/APNG, 원본/선정셀·프롬프트·기본 구·시간표·ZIP을 제공한다.

본체 B 부유16장은 바이트 그대로 복사한다. 공 중심[64,48]을 본체 캔버스[134,99]에 대응시키고 두 시간표의 모든 경계를 합쳐 합성한다. 본체는 빈 회복 중에도 기존1600ms 주기로 계속 부유하고 구가 옷에 닿으면 본체가 앞에 그려져 본체 픽셀을 보존한다. 확인용 추가 대기1250ms로 합성 반복을3200ms/부유2주기에 맞춘다. 이는1950ms 공격 시간과 별개다.

완성9번을64×64로 자른 비행체 외형 참고를 별도 제공하되 게임 투사체/탄도를 구현했다고 표현하지 않는다. 실제 맵은 기존960×540 배경/플레이어를 변경하지 않은 합성 미리보기이며 실제 게임 캡처가 아니다. 공 모션/픽셀 채택·게임 피해/장판/Unity 적용은 아직 없다.
''',encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','sourceCells':36,'selectedKeys':14,'visibleKeys':13,'paletteColors':4,
         'binaryAlpha':True,'indexedRgbaExact':True,'nearest8xExact':True,'canvas':[128,96],'attackMsProposal':1950,
         'sourceClippedExcluded':[15,16],'body16ByteIdentical':True,'bodyTimingUnchanged':True,'bodyOpaquePixelsPreservedInComposites':True,
         'webpApngPixelsAndTiming':'decoded-exact-after-adjacent-identical-merges','reviewLoopMs':3200,'reviewGapMs':1250,
         'browserQA':'not-run','Unity':'not-run'})
    package(out)
    print(json.dumps({'keys':14,'attackMs':1950,'scale':scale,'formedSize':info['formedVisibleSize'],'compositeSegments':len(merged),'excludedClipped':[15,16]}))


def board(frames,mapping,path):
    w,h=128*3,96*3;result=Image.new('RGB',(w*4,(h+46)*4),'white');draw=ImageDraw.Draw(result);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
    for i,(f,r) in enumerate(zip(frames,mapping)):
        x,y=i%4*w,i//4*(h+46);result.paste(white(f).resize((w,h),N),(x,y+46))
        draw.text((x+5,y+2),f'키{i+1:02} · 원본{r["sourceOneBased"]} · {r["durationMs"]}ms',font=font,fill='#25242b')
        draw.text((x+5,y+23),r['phaseName'],font=font,fill='#80586e')
    result.save(path)


def package(out):
    target=out/'H3-orb-return-pixel.zip'
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and p.name not in {'diagnostic.json','overview.png','commit-message.txt'}:z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(target) as z:assert z.testzip() is None


def page(out,info):
    js='''const DATA=REPLACE;let playing=true,time=0,prev=null,rate=1,mode='composite',orb=[],body=[];const canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d');
function key(ds,t){let rem=t%ds.reduce((n,x)=>n+x,0);for(let i=0;i<ds.length;i++){if(rem<ds[i])return i;rem-=ds[i]}return ds.length-1}
function draw(t,forced){const oi=forced===undefined?(t<DATA.attackMsProposal?key(DATA.durationsMs,t):DATA.frameCount-1):forced,bi=key(DATA.bodyDurationsMs,t);const s=+document.querySelector('#zoom').value;ctx.imageSmoothingEnabled=false;if(mode==='orb'){canvas.width=128;canvas.height=96;ctx.drawImage(orb[oi],0,0)}else{const tmp=document.createElement('canvas');tmp.width=384;tmp.height=224;const c=tmp.getContext('2d');c.imageSmoothingEnabled=false;c.drawImage(orb[oi],...DATA.orbOffsetInBodyCanvas);c.drawImage(body[bi],0,0);const b=DATA.bodyPreviewCrop;canvas.width=b[2]-b[0];canvas.height=b[3]-b[1];ctx.drawImage(tmp,b[0],b[1],canvas.width,canvas.height,0,0,canvas.width,canvas.height)}canvas.style.width=canvas.width*s+'px';const r=DATA.mapping[oi];document.querySelector('#step').value=oi;document.querySelector('#label').textContent='키'+(oi+1)+' · '+r.phaseName+' · 원본'+r.sourceOneBased+' · '+r.durationMs+'ms'+(t>=DATA.attackMsProposal?' · 확인용 대기':'')}
function tick(t){if(prev===null)prev=t;if(playing){time+=(t-prev)*rate;draw(time%DATA.previewCycleMs)}prev=t;requestAnimationFrame(tick)}
function load(src){return new Promise((resolve,reject)=>{let im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=src})}
Promise.all([Promise.all(DATA.mapping.map(r=>load(r.png))),Promise.all(DATA.bodyFloatHashes.map((_,i)=>load('references/body-float/native/float-'+String(i+1).padStart(2,'0')+'.png')))]).then(ims=>{[orb,body]=ims;draw(0);requestAnimationFrame(tick)});
document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'정지':'재생'};document.querySelector('#restart').onclick=()=>{time=0;prev=null;draw(0)};document.querySelector('#rate').onchange=e=>rate=+e.target.value;document.querySelector('#mode').onchange=e=>{mode=e.target.value;draw(time%DATA.previewCycleMs)};document.querySelector('#zoom').onchange=()=>draw(time%DATA.previewCycleMs);document.querySelector('#bg').onchange=e=>document.querySelector('.viewport').style.background=e.target.value;document.querySelector('#step').oninput=e=>{playing=false;document.querySelector('#play').textContent='재생';time=DATA.durationsMs.slice(0,+e.target.value).reduce((n,x)=>n+x,0);draw(time,+e.target.value)};
'''.replace('REPLACE',json.dumps(info,ensure_ascii=False))
    (out/'preview.js').write_text(js,encoding='utf-8')
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>늘어진 수의 · 반환 잉크 구 픽셀</title><style>body{margin:0;background:#eee;color:#28252c;font:16px/1.7 system-ui}main{max-width:1150px;padding:28px;margin:auto}section{background:white;padding:20px;margin:20px 0}h1{font-size:28px}h2{font-size:22px}img,canvas{image-rendering:pixelated}img{max-width:100%;height:auto}.viewport{overflow:auto;padding:12px}button,select{padding:8px;margin:4px}input{width:100%}a{color:#80586e}</style><main><h1>늘어진 수의 · 잉크 구 생성/발사</h1><p>본체 B 부유16장은 그대로 두고, 받은 구를 기존4색·완성 높이40px로 픽셀화한 검토안입니다. 아직 채택 전입니다.</p><p><a download href="H3-orb-return-pixel.zip">픽셀 PNG·원본·참조·시간표 ZIP</a> · <a href="manifest.json">규격·원본 대응</a> · <a href="sequence.json">선정 순서</a></p><section><h2>본체 앞 합성 / 구 단독</h2><p>생성850ms + 발사200ms + 빈 회복900ms = 1.95초. 추가 확인 대기1.25초는 공격 시간에 포함하지 않습니다. 합성 반복3.2초는 기존 본체 부유2주기와 맞췄습니다.</p><button id="play">정지</button><button id="restart">처음부터</button><select id="mode"><option value="composite">본체 앞 합성</option><option value="orb">구 단독</option></select><select id="rate"><option value="1">1배속</option><option value="0.5">0.5배속</option><option value="0.25">0.25배속</option></select><select id="zoom"><option value="1">실제1배</option><option value="2">2배</option><option value="3" selected>3배</option><option value="4">4배</option></select><select id="bg"><option value="white">흰 배경</option><option value="#25252b">어두운 배경</option></select><div class="viewport"><canvas></canvas></div><p id="label"></p><input id="step" type="range" min="0" max="13" value="0"><p><a href="previews/body-plus-orb-white-4x.webp">본체 합성 확대 재생</a> · <a href="previews/orb-white-6x.webp">구 단독 확대 재생</a> · <a href="previews/orb-native.apng">구 원배율 APNG</a></p></section><section><h2>선정14키 · 보이는13장 + 빈 회복</h2><img src="previews/selected-14.png" alt="구 생성과 발사 선정키"><p>참고1번과 왼쪽이 잘린15~16번은 제외하고 원본에 보존했습니다.2~14번과 빈17번을 사용합니다. 형성 중 덩어리 사이의 빈 공간은 유지했습니다.</p></section><section><h2>완성 공 외형 참고</h2><img src="projectile-reference/full-orb-white-8x.png" alt="완성된 공의64×64 외형 참고"><p>완성9번을 잘리지 않게 따로 보존한 참고이며 실제 게임 비행/판정을 구현한 결과가 아닙니다.</p></section><section><h2>기존 맵 합성 · 960×540</h2><img src="previews/map.webp" alt="기존 맵과 본체 앞 잉크 구 합성"><p>합성 미리보기이며 실제 게임 캡처가 아닙니다. 본체/배경/플레이어 픽셀은 바꾸지 않았습니다.</p></section><p>원본13→14번의 이동은 요청8px보다 큽니다. 구를 줄이거나 누락 부분을 새로 그리지 않고128×96 여백에 담았습니다. 완성 공 높이40px·기존4색·이진 알파·정수8배이며 본체 부유는 빈 회복 중에도 계속됩니다. 실제 이후 비행·착탄·지속피해 장판·Unity 연결은 별도입니다.</p><script src="preview.js"></script></main></html>''',encoding='utf-8')


if __name__=='__main__':build()
