"""Connect painted oil effects and original material anchors to the held sprite."""
import base64
import json
from pathlib import Path
import shutil
import zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label

from attack_fx_polish import save_frame,data_uri
from b3_walk_return import dump,white
from h3_orb_return import check_animation
from h3_slow_arc_impact import key_at
from k4_d_pixel_walk import ROOT,N,sha
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_k4_oil_motion.json'
HERE=Path(__file__).resolve().parent


def cells(source,grid):
    result=[];cols,rows=grid
    for i in range(cols*rows):
        x0=round((i%cols)*source.width/cols);x1=round((i%cols+1)*source.width/cols)
        y0=round((i//cols)*source.height/rows);y1=round((i//cols+1)*source.height/rows)
        f=source.crop((x0,y0,x1,y1));a=np.array(f);mask=a[:,:,3]>=128
        parts,_=label(mask);counts=np.bincount(parts.ravel());counts[0]=0
        keep=counts[parts]>=9;keep&=parts>0;a[~keep]=0
        assert keep.any(),f'Generated cell {i+1} is empty'
        assert not keep[0].any() and not keep[-1].any() and not keep[:,0].any() and not keep[:,-1].any(),f'Generated cell {i+1} touches its cut boundary'
        main=parts==counts.argmax();yy,xx=np.where(main);right=int(xx.max());root=[right,float(yy[xx>=right-3].mean())]
        result.append({'image':Image.fromarray(a),'root':root,'sheetBox':[x0,y0,x1,y1]})
    return result


def quantize(f,palette,size):
    a=np.array(f.resize(tuple(size),N));mask=a[:,:,3]>=128
    k=((a[:,:,:3,None].astype(int)-palette.T[None,None].astype(int))**2).sum(2).argmin(2)
    out=np.zeros_like(a);out[mask,:3]=palette[k[mask]];out[mask,3]=255
    return Image.fromarray(out)


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];(out/'references').mkdir(parents=True,exist_ok=True)
    shutil.copy2(SPEC,out/'references/presentation-spec.json');source=Image.open(ROOT/s['generatedSheet']).convert('RGBA');raw=cells(source,s['grid']);palette=np.array(s['paletteRgb'],np.uint8)
    full=raw[s['fullGushGeneratedIndex']]['image'];scale=s['fullGushWidthPx']/(full.getbbox()[2]-full.getbbox()[0])
    fixed_root=np.median(np.array([c['root'] for c in raw[:7]]),axis=0)
    material=ROOT/s['material'];anchors=json.loads((material/'manifest.json').read_text(encoding='utf-8'))['results'];anchors={r['id']:r for r in anchors}
    fx=[];records=[]
    for i,c in enumerate(raw):
        target=out/'generated-cells'/f'cell-{i+1:02}.png';target.parent.mkdir(exist_ok=True);c['image'].save(target)
        f=Image.new('RGBA',tuple(s['effectCanvas']));anchor=s['anchorKeys'].get(str(i))
        if anchor:
            a=anchors[anchor];patch=Image.open(material/a['native']).convert('RGBA');root=a['rootNativePx'];f.alpha_composite(patch,tuple(np.array(s['effectPivotPx'])-root));source_path=(material/a['native']).relative_to(ROOT).as_posix()
            copy=out/'references'/f'anchor-{anchor}.png';shutil.copy2(material/a['native'],copy);assert sha(copy)==sha(material/a['native'])
            mode='original-material-pixels'
        else:
            size=[round(c['image'].width*scale),round(c['image'].height*scale)];patch=quantize(c['image'],palette,size)
            root=c['root'] if i<7 else fixed_root;root=np.rint(np.array(root)*scale).astype(int)
            pos=np.array(s['effectPivotPx'])-root
            # Full cell placement preserves detached drops' displacement; a
            # disconnected mass must not get snapped back onto the nozzle.
            f.alpha_composite(patch,tuple(pos));source_path=target.relative_to(ROOT).as_posix();mode='generated-painting-pixel-scale-and-align'
        r=save_frame(f,out/'fx/native'/f'oil-{i+1:02}.png',palette);r.update(source=source_path,sourceMode=mode,sourceCellOneBased=i+1,anchor=anchor,bbox=list(f.getbbox()) if f.getbbox() else None);fx.append(f);records.append(r)
    # Continue the painted detached drop with integer placement so it reaches
    # the visible channel floor rather than disappearing halfway through air.
    drop=fx[8].copy();drop_bounds=drop.getbbox();world_origin_y=s['bodyOffsetWorld'][1]+s['nozzleNativePx'][1]-s['effectPivotPx'][1]
    assert world_origin_y+drop_bounds[3]+s['dropFallOffsetsY'][-1]==s['landingGroundYWorld']
    for dy in s['dropFallOffsetsY']:
        f=Image.new('RGBA',tuple(s['effectCanvas']));f.alpha_composite(drop,(0,dy));r=save_frame(f,out/'fx/native'/f'oil-{len(fx)+1:02}.png',palette);r.update(source='fx/native/oil-09.png',sourceMode='painted-drop-integer-translation',offsetPx=[0,dy],anchor=None,bbox=list(f.getbbox()));fx.append(f);records.append(r)
    patch=drop.crop(drop_bounds).resize(tuple(s['contactDropSizePx']),N);ground=s['landingGroundYWorld']-world_origin_y;cx=(drop_bounds[0]+drop_bounds[2])//2
    f=Image.new('RGBA',tuple(s['effectCanvas']));f.alpha_composite(patch,(cx-patch.width//2,ground-patch.height));r=save_frame(f,out/'fx/native'/f'oil-{len(fx)+1:02}.png',palette);r.update(source='fx/native/oil-09.png',sourceMode='brief-painted-drop-contact-squash',anchor=None,bbox=list(f.getbbox()));fx.append(f);records.append(r)
    fx.append(Image.new('RGBA',tuple(s['effectCanvas'])));r=save_frame(fx[-1],out/'fx/native'/f'oil-{len(fx):02}.png',palette);r.update(sourceMode='empty-after-last-drop',anchor=None,bbox=None);records.append(r)
    ds=s['effectDurationsMs'];assert len(ds)==len(fx) and sum(ds[:7])==s['connectedSupplyMsIllustration']==650
    body_source=ROOT/s['body'];bi=json.loads((body_source/'manifest.json').read_text(encoding='utf-8'));body_ds=bi['durationsMs'].copy();assert sum(body_ds[:9])==550 and sum(body_ds[10:])==700
    body_ds[9]=sum(ds);body=[];body_records=[]
    for p in sorted((body_source/'native').glob('*.png')):
        target=out/'body'/p.name;target.parent.mkdir(exist_ok=True);shutil.copy2(p,target);assert sha(p)==sha(target);body.append(Image.open(p).convert('RGBA'));body_records.append({'source':p.relative_to(ROOT).as_posix(),'path':target.relative_to(out).as_posix(),'sha256':sha(p)})
    retract=550+sum(ds);motion=sum(body_ds);cycle=motion+s['reviewGapMs'];boundaries=set(np.cumsum([0]+body_ds).tolist()+[cycle]);boundaries.update((550+np.cumsum([0]+ds)).tolist());times=sorted(boundaries)
    scene=Image.open(ROOT/s['map']).convert('RGBA');world_nozzle=np.array(s['bodyOffsetWorld'])+s['nozzleNativePx'];fxpos=tuple(world_nozzle-s['effectPivotPx']);plains=[];maps=[];durations=[];timeline=[]
    for t,end in zip(times,times[1:]):
        body_key=key_at(body_ds,t);effect_key=key_at(ds,t-550) if 550<=t<retract else None;layer=Image.new('RGBA',(960,540))
        if effect_key is not None:layer.alpha_composite(fx[effect_key],fxpos)
        layer.alpha_composite(body[body_key],tuple(s['bodyOffsetWorld']));plain=Image.new('RGBA',(960,540),'white');plain.alpha_composite(layer);mapping=scene.copy();mapping.alpha_composite(layer)
        plains.append(plain.crop((110,145,510,380)));maps.append(mapping);durations.append(end-t);timeline.append({'timeMs':t,'durationMs':end-t,'bodyKey':body_key+1,'effectKey':effect_key,'phase':s['effectPhaseNames'][effect_key] if effect_key is not None else ('준비' if t<550 else '노즐 회수' if t<motion else '확인 대기')})
    animate(plains,durations,out/'review-native.webp');check_animation(out/'review-native.webp',plains,durations)
    animate([f.resize((800,470),N) for f in plains],durations,out/'review-white-2x.webp');animate(maps,durations,out/'map.webp')
    animate([white(f).resize((768,576),N) for f in fx],ds,out/'oil-only-3x.webp')
    # Keep the approved H3 material and full action as an exact reference copy.
    hsrc=ROOT/s['approvedH3'];(out/'approved-h3').mkdir(exist_ok=True)
    shutil.copy2(hsrc/'review-native.webp',out/'approved-h3/review-native.webp');assert sha(hsrc/'review-native.webp')==sha(out/'approved-h3/review-native.webp')
    board(fx,s['effectPhaseNames'],out)
    info={**s,'effectPaths':[f'fx/native/oil-{i+1:02}.png' for i in range(len(fx))],'effectRecords':records,'generatedCommonScale':scale,'generatedDetachedRootReferencePx':fixed_root.tolist(),'bodyPaths':[r['path'] for r in body_records],'bodyDurationsMs':body_ds,'bodySourceRecords':body_records,'retractStartMs':retract,'motionMs':motion,'cycleMs':cycle,'timeline':timeline,'h3ByteIdentical':True,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);dump(out/'timeline.json',timeline);page(info,out)
    (out/'README.md').write_text('''# 무거운 기름 분사 · 모션 합성 검토

기름 재질 방향을 바탕으로 기본 image_gen의9키 그림을 분리하고, 앞서 본 원형3개를 정확히 넣어16키로 연결한다. 원본/요청 프롬프트는 generated/와 references/에 보존한다. 코드는 원본 형태를 입자/유체로 대체하지 않으며 팔레트2색/이진 알파/정수 위치와 시간표를 맞춘다. 마지막에 그려진 방울은 정수 픽셀로 낙하시켜 예시 수로 바닥에 닿게 하고60ms 눌림/100ms 빈 버팀 뒤 회수한다. 잔류 장판은 아니다.

본체19장과 준비550ms/회수700ms는 기존 바이트/박자를 유지한다. 기름1380ms 동안 버팀을 유지하여1930ms에 회수를 시작한다. 전체 시각 동작2630ms/확인 간격 포함3130ms다. 처음 연결된 기름650ms와 끝물은 시각 타임라인이며 실제 게임 피해 시간은 변경하지 않는다. 수의 채택27장 보정의 전체 클립은 바이트 복사한다.

index.html은 이미지/코드 내장 미리보기다. 정지/속도/프레임 이동/흰·어두운 배경/원본 맵/시각 범위를 확인한다. 독립 fx/native·indexed, body, 시간표, 원본과 ZIP을 제공한다. 완성 모션의 사용자 채택·게임 충돌/히트박스·Unity 적용은 별도다. 실제 게임 캡처가 아니다.
''',encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','body19ByteIdentical':True,'acceptedMaterialThreeAnchorsExact':True,'h3ClipByteIdentical':True,'binaryAlphaExistingTwoColors':True,'emptyBufferBeforeRetractionMs':ds[-1],'nativeWebpPixelsAndTimingExact':True,'browserQA':'not-run','Unity':'not-run'})
    with zipfile.ZipFile(out/'oil-motion-assets.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix in ('.png','.json'):z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'oil-motion-assets.zip') as z:assert z.testzip() is None
    print(json.dumps({'oilKeys':len(fx),'oilMs':sum(ds),'retractStartMs':retract,'motionMs':motion,'cycleMs':cycle,'originalMaterialAnchors':3,'bodyPixelsChanged':0,'h3Changed':0}))


def board(frames,names,out):
    cols=4;w,h=384,310;pic=Image.new('RGBA',(w*cols,h*((len(frames)+cols-1)//cols)),'white');d=ImageDraw.Draw(pic);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
    for i,f in enumerate(frames):
        x=i%cols*w;y=i//cols*h;pic.alpha_composite(f.resize((384,288),N),(x,y+22));d.text((x+5,y),f'{i+1} · '+names[i],font=font,fill='#80586e')
    pic.save(out/'oil-keys.png')


def page(info,out):
    images={p:data_uri(out/p) for p in info['effectPaths']+info['bodyPaths']};images['map']=data_uri(ROOT/info['map']);data={**info,'images':images}
    data['h3Webp']='data:image/webp;base64,'+base64.b64encode((out/'approved-h3/review-native.webp').read_bytes()).decode('ascii')
    script=(HERE/'k4_oil_motion.js').read_text(encoding='utf-8');html=(HERE/'k4_oil_motion.html').read_text(encoding='utf-8').replace('/*__DATA__*/','window.OIL_MOTION='+json.dumps(data,ensure_ascii=False).replace('</','<\\/')+';').replace('/*__VIEWER__*/',script.replace('</','<\\/'));(out/'index.html').write_text(html,encoding='utf-8')


if __name__=='__main__':build()
