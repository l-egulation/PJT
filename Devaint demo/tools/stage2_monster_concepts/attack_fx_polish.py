"""Add a tunable ink jet and exact-palette charge inbetweens for art review."""
import base64
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image,ImageDraw
from scipy.ndimage import distance_transform_edt as edt, label

from b3_walk_return import dump,white
from h3_slow_arc_impact import key_at
from k4_d_pixel_walk import ROOT,N,sha
from walk_review import animate

HERE=Path(__file__).resolve().parent
SPEC=ROOT/'data/presentation/stage2_attack_fx_polish.json'


def inbetween(a,b,u,palette):
    """Advance hard pixels along region fronts without RGB/alpha blending.

    A plain SDF threshold changes whole one-pixel rings simultaneously. Rank
    crossings along the contours and budget changed pixels per sample instead,
    so additional frames actually advance the shape rather than repeat a hold.
    """
    if u==0:return a.copy()
    if u==1:return b.copy()
    am=a[:,:,3]>0;bm=b[:,:,3]>0;changed=np.any(a!=b,axis=2);yy,xx=np.where(changed)
    old_depth=edt(~am);new_depth=edt(~bm)
    for color in palette:
        ca=am&np.all(a[:,:,:3]==color,axis=2);cb=bm&np.all(b[:,:,:3]==color,axis=2)
        old_depth[ca]=edt(ca)[ca];new_depth[cb]=edt(cb)[cb]
    crossing=old_depth[yy,xx]/np.maximum(1,old_depth[yy,xx]+new_depth[yy,xx])
    by,bx=np.where(bm);angle=np.arctan2(yy-by.mean(),xx-bx.mean())
    order=np.lexsort((xx,yy,angle,crossing));selected=order[:round(u*len(order))]
    out=a.copy();out[yy[selected],xx[selected]]=b[yy[selected],xx[selected]]
    return out


def save_frame(f,path,palette):
    path.parent.mkdir(parents=True,exist_ok=True);f.save(path)
    a=np.array(f);solid=a[:,:,3]>0
    assert set(np.unique(a[:,:,3]))<={0,255}
    assert set(map(tuple,np.unique(a[solid,:3],axis=0)))<=set(map(tuple,palette))
    indices=np.zeros(a.shape[:2],np.uint8)
    for i,rgb in enumerate(palette,1):indices[solid&np.all(a[:,:,:3]==rgb,axis=2)]=i
    indexed=Image.fromarray(indices);colors=np.vstack([[0,0,0],palette]);indexed.putpalette(colors.ravel().tolist()+[0]*(768-colors.size));indexed.info['transparency']=0
    target=path.parent.parent/'indexed'/path.name;target.parent.mkdir(exist_ok=True);indexed.save(target)
    assert indexed.convert('RGBA').tobytes()==f.tobytes()
    return {'path':path.name,'sha256':sha(path),'rgbaSha256':hashlib.sha256(f.tobytes()).hexdigest()}


def decode_webp(path):
    im=Image.open(path);frames=[];durations=[]
    for i in range(im.n_frames):
        im.seek(i);im.load();frames.append(im.convert('RGBA'));durations.append(round(im.info['duration']))
    return frames,durations


def h3_build(s,out):
    dest=out/'h3';dest.mkdir(exist_ok=True);src=ROOT/s['approved'];latest=ROOT/s['latest']
    originals=[];palette=np.array(json.loads((src/'manifest.json').read_text(encoding='utf-8'))['orbPaletteRgb'],np.uint8)
    for i in range(8):
        p=src/'formation/native'/f'orb-{i+1:02}.png';target=out/'references'/f'h3-original-{i+1:02}.png';shutil.copy2(p,target)
        originals.append(np.array(Image.open(p).convert('RGBA')))
    starts=np.cumsum([0]+s['originalFormationDurationsMs'][:-1]).tolist();times=list(range(0,1300,s['inbetweenStepMs']))+[1300]
    frames=[];records=[]
    for i,t in enumerate(times):
        k=key_at(s['originalFormationDurationsMs'],t)
        u=0 if k==7 else (t-starts[k])/(starts[k+1]-starts[k])
        a=inbetween(originals[k],originals[min(k+1,7)],u,palette);f=Image.fromarray(a)
        record=save_frame(f,dest/'native'/f'formation-{i+1:02}.png',palette)
        record.update(timeMs=t,originalKey=k+1 if u==0 else None,sourcePair=[k+1,min(k+2,8)],blend=u)
        frames.append(f);records.append(record)
    durations=[b-a for a,b in zip(times,times[1:]+[1600])]
    assert len(frames)==27 and sum(durations)==1600 and durations[-1]==300
    for k,t in enumerate(starts):assert frames[times.index(t)].tobytes()==originals[k].tobytes()
    changes=lambda items:[int(np.any(np.array(a)!=np.array(b),axis=2).sum()) for a,b in zip(items,items[1:])]
    old_changes=changes([Image.fromarray(a) for a in originals]);new_changes=changes(frames)
    assert max(new_changes)<=100 and max(new_changes)<max(old_changes)/2
    before,ds=decode_webp(latest/'review-native.webp');assert sum(ds)==6400
    shutil.copy2(latest/'review-native.webp',out/'references/h3-latest-before.webp')
    bodies=[Image.open(src/'references/body-float/native'/f'float-{i:02}.png').convert('RGBA') for i in range(1,17)]
    after=[];scene=Image.open(ROOT/json.loads(SPEC.read_text(encoding='utf-8'))['map']).convert('RGBA');maps=[]
    for t in range(0,6400,50):
        old=before[key_at(ds,t)]
        if t<1600:
            f=Image.new('RGBA',(960,540),'white');ImageDraw.Draw(f).line((200,354,485,354),fill='#b4a991')
            f.alpha_composite(frames[key_at(durations,t)],(s['orbCenterWorld'][0]-64,s['orbCenterWorld'][1]-48))
            f.alpha_composite(bodies[(t//100)%16],tuple(s['bodyOffsetWorld']));f=f.crop(tuple(s['worldCrop']))
        else:f=old.copy();assert f.tobytes()==old.tobytes()
        after.append(f)
        a=np.array(f);a[np.all(a[:,:,:3]==255,axis=2)]=0;gy=354-s['worldCrop'][1];a[gy,np.all(a[gy,:,:3]==[180,169,145],axis=1)]=0
        mapping=scene.copy();mapping.alpha_composite(Image.fromarray(a),tuple(s['worldCrop'][:2]));maps.append(mapping)
    animate(after,[50]*128,dest/'review-native.webp');animate(maps,[50]*128,dest/'map.webp')
    animate([white(f).resize((512,384),N) for f in frames],durations,dest/'formation-4x.webp')
    comparison=[]
    for t in range(0,1600,50):
        a=white(Image.fromarray(originals[key_at(s['originalFormationDurationsMs'],t)]));b=white(frames[key_at(durations,t)])
        f=Image.new('RGB',(256,96),'white');f.paste(a,(0,0));f.paste(b,(128,0));comparison.append(f.resize((1024,384),N))
    animate(comparison,[50]*32,dest/'before-after-4x.webp')
    board=Image.new('RGBA',(1024,448),'white');draw=ImageDraw.Draw(board)
    for i,k in enumerate([0,2,5,9,13,17,21,26]):
        x=i%4*256;y=i//4*224;board.alpha_composite(frames[k].resize((256,192),N),(x,y+24));draw.text((x+8,y+4),str(k+1),fill='black')
    board.save(dest/'formation-keys.png')
    dump(dest/'timeline.json',{'durationsMs':durations,'frames':records,'releaseMs':1600,'fullHoldMs':300})
    return {'keys':27,'addedKeys':19,'durationsMs':durations,'paths':[f'h3/native/formation-{i+1:02}.png' for i in range(27)],'originalEightKeysExact':True,'maxChangedPixelsBefore':max(old_changes),'maxChangedPixelsAfter':max(new_changes),'postReleasePixelsAndTimingExact':True}


def k4_build(s,out):
    dest=out/'k4';dest.mkdir(exist_ok=True);src=ROOT/s['body'];meta=json.loads((src/'manifest.json').read_text(encoding='utf-8'))
    body=[];records=[]
    for p in sorted((src/'native').glob('*.png')):
        target=dest/'body'/p.name;target.parent.mkdir(exist_ok=True);shutil.copy2(p,target);assert sha(p)==sha(target)
        body.append(Image.open(p).convert('RGBA'));records.append({'source':p.relative_to(ROOT).as_posix(),'copy':target.relative_to(out).as_posix(),'sha256':sha(p)})
    ds=meta['durationsMs'];assert sum(ds[:9])==s['attackStartMs']==550 and ds[9]==650 and sum(ds)==1900
    raw=out/'references/ink-spray-generated-original.png';shutil.copy2(s['generatedSource'],raw)
    (out/'references/ink-spray-prompt.txt').write_text(s['generatedPrompt'],encoding='utf-8')
    a=np.array(Image.open(raw).convert('RGBA'));mask=a[:,:,3]>=128;labels,_=label(mask);counts=np.bincount(labels.ravel());counts[0]=0;mask=labels==counts.argmax()
    a[~mask]=0;source=Image.fromarray(a);source=source.crop(source.getbbox())
    small=np.array(source.resize((s['trialLengthPx'],s['trialHeightPx']),N));solid=small[:,:,3]>=128
    palette=np.array(s['paletteRgb'],np.uint8);q=np.zeros_like(small);q[solid,:3]=palette[(small[:,:,:3].mean(2)>74).astype(int)[solid]];q[solid,3]=255
    # Attach the thin root to the exact nozzle center; every particle stays in
    # the proposed rectangle. Shift only clusters, never the character pose.
    w,h=s['trialLengthPx'],s['trialHeightPx'];poses=[]
    yy,xx=np.indices((h,w))
    for i in range(s['effectKeys']):
        shift=np.rint(np.sin(xx/9+i*np.pi/4)*1.25*(1-xx/(w-1))).astype(int)
        sy=np.clip(yy-shift,0,h-1);f=q[sy,xx].copy()
        # Advect sparse dark-gray streaks toward the leading left edge.
        moving=q[yy,(xx+i*4)%w];valid=f[:,:,3]>0;f[valid,:3]=moving[valid,:3];f[valid&(moving[:,:,3]==0),:3]=palette[0]
        im=Image.fromarray(f);d=ImageDraw.Draw(im);d.line((w-8,h//2,w-1,h//2),fill=tuple(palette[0])+(255,),width=3)
        save_frame(im,dest/'native'/f'spray-{i+1:02}.png',palette);poses.append(im)
    animate([white(f).resize((w*4,h*4),N) for f in poses],[80]*8,dest/'spray-4x.webp')
    # Common world frames, preserving all body pixels and its original timing.
    boundaries=set(np.cumsum([0]+ds).tolist()+[2400]);boundaries.update(range(550,1200,s['effectKeyMs']));times=sorted(boundaries);plains=[];maps=[];durations=[];timeline=[]
    scene=Image.open(ROOT/json.loads(SPEC.read_text(encoding='utf-8'))['map']).convert('RGBA')
    nozzle=[a+b for a,b in zip(s['nozzleNativePx'],s['bodyOffsetWorld'])];rect=[nozzle[0]-w,nozzle[1]-h//2,w,h]
    for t,end in zip(times,times[1:]):
        k=key_at(ds,t);layer=Image.new('RGBA',(960,540));active=550<=t<1200
        if active:layer.alpha_composite(poses[((t-550)//80)%8],tuple(rect[:2]))
        layer.alpha_composite(body[k],tuple(s['bodyOffsetWorld']));plain=Image.new('RGBA',(960,540),'white');plain.alpha_composite(layer);mapping=scene.copy();mapping.alpha_composite(layer)
        plains.append(plain.crop((195,150,495,300)));maps.append(mapping);durations.append(end-t);timeline.append({'timeMs':t,'durationMs':end-t,'bodyKey':k+1,'attackActive':active,'effectKey':((t-550)//80)%8+1 if active else None,'trialHitboxWorld':rect if active else None})
    animate(plains,durations,dest/'review-native.webp');animate(maps,durations,dest/'map.webp')
    animate([f.resize((900,450),N) for f in plains],durations,dest/'review-white-3x.webp')
    dump(dest/'timeline.json',timeline)
    return {'paths':[f'k4/native/spray-{i+1:02}.png' for i in range(8)],'bodyPaths':[r['copy'] for r in records],'bodyDurationsMs':ds,'sourceRecords':records,'body19ByteIdentical':True,'sourceTool':s['sourceTool'],'defaultTrialHitboxWorld':rect,'hitboxStatus':s['hitboxStatus']}


def data_uri(path):return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode('ascii')


def page(s,results,out):
    files=set(results['h3']['paths']+results['k4']['paths']+results['k4']['bodyPaths'])
    data={**s,'results':results,'images':{p:data_uri(out/p) for p in sorted(files)}}
    data['images']['map']=data_uri(ROOT/s['map'])
    data['beforeWebp']='data:image/webp;base64,'+base64.b64encode((out/'h3/before-after-4x.webp').read_bytes()).decode('ascii')
    data['h3Webp']='data:image/webp;base64,'+base64.b64encode((out/'h3/review-native.webp').read_bytes()).decode('ascii')
    data['h3MapWebp']='data:image/webp;base64,'+base64.b64encode((out/'h3/map.webp').read_bytes()).decode('ascii')
    script=(HERE/'attack_fx_polish.js').read_text(encoding='utf-8')
    html=(HERE/'attack_fx_polish.html').read_text(encoding='utf-8').replace('/*__DATA__*/','window.ATTACK_FX='+json.dumps(data,ensure_ascii=False).replace('</','<\\/')+';').replace('/*__VIEWER__*/',script.replace('</','<\\/'))
    (out/'index.html').write_text(html,encoding='utf-8')


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];(out/'references').mkdir(parents=True,exist_ok=True);shutil.copy2(SPEC,out/'references/presentation-spec.json')
    result={'h3':h3_build(s['h3'],out),'k4':k4_build(s['k4'],out)}
    dump(out/'manifest.json',{**s,'results':result,'runtimeIntegration':'not-performed'})
    page(s,result,out);dump(out/'validation.json',{'result':'pass',**result,'browserQA':'not-run','Unity':'not-run'})
    (out/'README.md').write_text('''# 공격 이펙트 보정 · 디자인/합성 미리보기

index.html은 이미지/재생 코드가 내장된 독립 검토 화면이다. 분사 길이/높이/노즐 위치와 임시 사각 범위를 함께 조절하고 JSON으로 저장한다. 기본128×40px는 미리보기 시험값이며 게임 히트박스가 아니다. 기존19장/1900ms 본체와550~1200ms 분사 자세를 보존한다.650ms 동안만 검정 잉크8키를 재생한다. 잔류 장판이나 새 피해 규칙은 없다.

분사 정지 원본은 대화 기본 image_gen으로 생성하고 references/에 원본/프롬프트를 보존한다. 기존 캐릭터의 두 색·이진 투명도로 환원하여 정수 픽셀 변형/이동으로8키를 만든다. k4/body는 채택19장과 바이트 동일하다. k4/native는 독립 분사, k4/indexed는 같은 픽셀의 indexed PNG다.

수의는 원래8장/시점/4색을 보존하고19장 중간 키를 추가하여27장으로 연결한다. RGB/알파 블렌딩 없이 재질 경계에서 변경 픽셀 수를 균등하게 나눈다. 완성 구300ms·발사1600ms·전체6400ms를 유지한다. 발사 뒤 비행/착탄/장판은 최신 합성과 픽셀/시간이 같다. h3/native와 h3/indexed는 독립 생성 키, h3/timeline.json은 정확한 시간표다. 몸체 부유도 그대로다.

판정 범위와 새 효과는 사용자 검토 전이다. 실제 게임 캡처/런타임 충돌/피해/Unity 연결이 아니며 기존 채택 원본을 덮어쓰지 않는다.
''',encoding='utf-8')
    with zipfile.ZipFile(out/'attack-fx-pixel-assets.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix in ('.png','.json','.txt','.md') and p.name!='validation.json':z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'attack-fx-pixel-assets.zip') as z:assert z.testzip() is None
    print(json.dumps({'h3FormationKeys':27,'h3AddedKeys':19,'h3MaxPixelStep':[result['h3']['maxChangedPixelsBefore'],result['h3']['maxChangedPixelsAfter']],'k4FxKeys':8,'k4AttackWindowMs':[550,1200],'bodyPixelsChanged':0,'hitbox':'adjustable-review-only'}))


if __name__=='__main__':build()
