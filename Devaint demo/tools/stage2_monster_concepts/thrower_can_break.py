"""Show the existing waste-ink can fragmenting at contact without a lasting hazard."""
import json
import shutil
import zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont

from b3_walk_return import dump,white
from h3_orb_return import check_animation
from h3_slow_arc_impact import key_at
from k4_d_pixel_walk import ROOT,N,sha
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_thrower_can_break.json'


def export(im,path,palette):
    path.parent.mkdir(parents=True,exist_ok=True);im.save(path);a=np.array(im);solid=a[:,:,3]>0
    assert set(np.unique(a[:,:,3]))<={0,255}
    assert set(map(tuple,np.unique(a[solid,:3],axis=0)))<=set(map(tuple,palette))
    ix=np.zeros(a.shape[:2],np.uint8)
    for i,c in enumerate(palette,1):ix[solid&np.all(a[:,:,:3]==c,axis=2)]=i
    pal=np.vstack([np.zeros((1,3),np.uint8),palette]);p=Image.fromarray(ix);p.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));p.info['transparency']=0
    target=path.parent.parent/'indexed'/path.name;target.parent.mkdir(exist_ok=True);p.save(target);assert p.convert('RGBA').tobytes()==im.tobytes()
    target=path.parent.parent/'enlarged-6x'/path.name;target.parent.mkdir(exist_ok=True);im.resize((im.width*6,im.height*6),N).save(target)
    return {'path':path.relative_to(ROOT).as_posix(),'sha256':sha(path),'bbox':list(im.getbbox()) if im.getbbox() else None}


def split(prop,flight):
    patch=prop.crop(prop.getbbox());a=np.array(patch);yy,xx=np.indices(a.shape[:2]);opaque=a[:,:,3]>0
    seeds=np.array([[16,5],[29,11],[20,21]])
    groups=((xx[:,:,None]-seeds[:,0])**2+(yy[:,:,None]-seeds[:,1])**2).argmin(2)+1
    groups[xx<12]=0;parts=[];reassembled=np.zeros_like(np.array(flight));coverage=np.zeros(flight.size[::-1],int)
    fb=flight.getbbox()
    for i in range(4):
        piece=np.zeros_like(a);piece[(groups==i)&opaque]=a[(groups==i)&opaque]
        f=Image.new('RGBA',flight.size);f.alpha_composite(Image.fromarray(piece).transpose(Image.Transpose.ROTATE_90),(fb[0],fb[1]));parts.append(f)
        q=np.array(f);mask=q[:,:,3]>0;reassembled[mask]=q[mask];coverage+=mask
    assert np.array_equal(reassembled,np.array(flight)) and coverage.max()==1
    return parts


def fx_frame(s,parts,time_ms,key):
    im=Image.new('RGBA',tuple(s['effectCanvas']));d=ImageDraw.Draw(im);ax,ay=s['groundAnchorPx'];dark,purple=[tuple(c)+(255,) for c in s['inkRgb']]
    if time_ms<140:
        if time_ms<40:
            points=[(0,0),(-11,-3),(-17,-11),(-10,-8),(-5,-3),(-3,-16),(2,-9),(5,-3),(13,-13),(11,-4),(6,0)]
        else:
            points=[(0,-1),(-14,-6),(-19,-15),(-14,-14),(-9,-7),(-5,-20),(-1,-16),(2,-7),(12,-18),(14,-16),(9,-4)]
        d.polygon([(ax+x,ay+y) for x,y in points],fill=purple)
        d.polygon([(ax-7,ay-3),(ax,ay-9),(ax+7,ay-2),(ax+3,ay)],fill=dark)
    # Short independent ink drops; never draw a horizontal puddle asset.
    drops=[(-92,-165,300,3),(-50,-210,360,3),(45,-205,380,3),(95,-150,300,3),(130,-105,240,2),(-130,-90,240,2)]
    for i,(vx,vy,life,size) in enumerate(drops):
        if 40<=time_ms<life:
            t=(time_ms-20)/1000;x=round(ax+vx*t);y=round(ay+vy*t+350*t*t)
            if time_ms>280:size=2
            d.rectangle((x,y,x+size-1,y+size-1),fill=purple if i%2 else dark)
    for i,part in enumerate(parts):
        if time_ms>=s['fragmentLifetimesMs'][i]:continue
        bb=part.getbbox();patch=part.crop(bb);cx=(bb[0]+bb[2])/2-32;cy=(bb[1]+bb[3])/2-32
        turn=0 if time_ms<80 else (key+i)%4;patch=patch.rotate(90*turn,resample=N,expand=True)
        t=(time_ms+30)/1000;vx,vy=s['fragmentVelocitiesPxPerSecond'][i]
        x=round(ax+cx+vx*t-patch.width/2);y=round(ay-18+cy+vy*t+.5*s['fragmentGravityPxPerSecondSquared']*t*t-patch.height/2)
        assert x>=0 and y>=0 and x+patch.width<im.width and y+patch.height<=ay+1,(i,time_ms,x,y,patch.size)
        im.alpha_composite(patch,(x,y))
    return im


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];src=ROOT/s['source'];attack=ROOT/s['attackSource']
    for folder in ('references','previews','projectile/native','fragments/native','break/native','body-display'):(out/folder).mkdir(parents=True,exist_ok=True)
    info=json.loads((attack/'manifest.json').read_text(encoding='utf-8'));source_info=json.loads((src/'manifest.json').read_text(encoding='utf-8'));palette=np.array(info['paletteRgb'],np.uint8)
    for name in ('prop-original.png','prop-flight-fixed.png'):
        shutil.copy2(src/'native'/name,out/'projectile/native'/name);assert sha(src/'native'/name)==sha(out/'projectile/native'/name)
    prop=Image.open(src/'native/prop-original.png').convert('RGBA');flight=Image.open(src/'native/prop-flight-fixed.png').convert('RGBA');parts=split(prop,flight)
    part_records=[export(f,out/'fragments/native'/f'piece-{i+1:02}.png',palette) for i,f in enumerate(parts)]
    times=[];acc=0
    for d in s['effectDurationsMs']:times.append(acc);acc+=d
    fx=[fx_frame(s,parts,t,k) for k,t in enumerate(times)];assert fx[-1].getbbox() is None
    effects=[export(f,out/'break/native'/f'break-{i+1:02}.png',palette) for i,f in enumerate(fx)]
    scene=Image.open(attack/'references/C-game.png').convert('RGBA');body=[];hashes=[]
    for row in info['mapping']:
        p=src/'body-display'/f'attack-{row["key"]:02}.png';dest=out/'body-display'/p.name;shutil.copy2(p,dest);assert sha(p)==sha(dest);hashes.append(sha(dest));body.append(Image.open(dest).convert('RGBA'))
    timeline_source=json.loads((src/'timeline.json').read_text(encoding='utf-8'));contact=s['contactMsIllustration'];end_fx=contact+acc;total=end_fx+s['reviewGapMs']
    bounds={0,total,contact,end_fx};bounds.update(r['timeMs'] for r in timeline_source if r['timeMs']<contact);bounds.update(contact+t for t in times);bounds=sorted(bounds)
    plains=[];maps=[];durations=[];timeline=[]
    for ms,end in zip(bounds,bounds[1:]):
        k=key_at(info['durationsMs'],ms);layer=Image.new('RGBA',(960,540));layer.alpha_composite(body[k],tuple(info['mapOffset']));center=None;effect=None
        if 680<=ms<contact:
            row=next(r for r in timeline_source if r['timeMs']<=ms<r['timeMs']+r['durationMs']);center=row['projectileCenterWorld'];assert center
            layer.alpha_composite(flight,(center[0]-32,center[1]-32))
        elif contact<=ms<end_fx:
            effect=key_at(s['effectDurationsMs'],ms-contact);x,y=s['contactWorld'];ax,ay=s['groundAnchorPx'];layer.alpha_composite(fx[effect],(x-ax,y-ay))
        plain=Image.new('RGBA',(960,540),'white');ImageDraw.Draw(plain).line((105,288,510,288),fill='#b4a991');plain.alpha_composite(layer);plain=plain.crop((100,80,510,315));mapping=scene.copy();mapping.alpha_composite(layer)
        plains.append(plain);maps.append(mapping);durations.append(end-ms);timeline.append({'timeMs':ms,'durationMs':end-ms,'bodyKey':k+1,'projectileCenter':center,'breakKey':None if effect is None else effect+1})
    assert sum(durations)==total==2640
    animate(plains,durations,out/'previews/review-native.webp');check_animation(out/'previews/review-native.webp',plains,durations)
    animate([f.resize((1230,705),N) for f in plains],durations,out/'previews/throw-and-break-white-3x.webp');animate(maps,durations,out/'previews/map.webp')
    close=[]
    for f in fx:
        w=Image.new('RGBA',f.size,'white');ImageDraw.Draw(w).line((0,72,127,72),fill='#b4a991');w.alpha_composite(f);close.append(w)
    animate(close+[close[-1]],s['effectDurationsMs']+[600],out/'previews/break-close-native.webp');check_animation(out/'previews/break-close-native.webp',close+[close[-1]],s['effectDurationsMs']+[600])
    animate([f.resize((768,576),N) for f in close+[close[-1]]],s['effectDurationsMs']+[600],out/'previews/break-close-6x.webp')
    board(fx,times,out/'previews/break-keys.png')
    shutil.copy2(SPEC,out/'references/presentation-spec.json');shutil.copy2(src/'manifest.json',out/'references/prop-manifest.json');shutil.copy2(attack/'references/C-game.png',out/'references/C-game.png')
    dump(out/'manifest.json',{**s,'breakKeyStartMs':times,'effectMs':acc,'previewTotalMs':total,'fragmentAssets':part_records,'effectAssets':effects,'bodyFileHashes':hashes,'projectileFileSha256':sha(src/'native/prop-flight-fixed.png'),'partitionAllSourcePixelsExactlyOnce':True,'noRemainingPool':True,'sourcePropRepairPixels':source_info['upperEdgePixelsRepaired'],'runtimeIntegration':'not-performed'})
    dump(out/'timeline.json',timeline);dump(out/'validation.json',{'result':'pass','fragmentPartitionPixelExact':True,'allPartsNonOverlapping':True,'existingPaletteBinaryAlphaIndexedExact':True,'body19AndProjectileFileBytesUnchanged':True,'nativeAnimationPixelsAndDurationExact':True,'effectLastKeyEmpty':True,'noPoolAsset':True,'singleDirectHitRuleUnchanged':True,'browserQA':'not-run','Unity':'not-run'})
    page(out)
    (out/'README.md').write_text('''# 신호투척수 폐잉크통 · 파손 검토

현재 윗면을 채운 원통형 통/고정 비행 PNG와 본체 display19장을 바이트 그대로 복사한다. 통의 짙은 마개와 몸체3조각은 기존 픽셀을 겹침/누락 없이 분할하고 정수 이동/정확90도 회전으로 튀긴다. 깨지는8키560ms(마지막 빈 키 포함)와 기존2색 잉크 분출/방울을 합성한다. 잉크는 잠깐 흩어지고 소멸하며 바닥 웅덩이를 그리지 않는다. 비행 중 통 변형은 없다.

680ms 놓기/800ms 예시 비행 뒤1480ms에 통을 지우고 바닥 접점에 파손 효과를 표시한다. 본체1450ms 박자와 피해 제안은 유지한다. 직격1회이며 파편/잉크 추가 피해·폭발 피해·잔류 장판을 만들지 않는다.2640ms 전체 반복은 효과와600ms 확인 대기를 포함한다. 파손 시각 후보는 사용자 검토 전이고 실제 게임 판정/탄도/Unity 적용은 없다. 맵은 원본960×540의 합성 미리보기다.
''',encoding='utf-8')
    package(out);print(json.dumps({'breakKeys':8,'effectMs':560,'totalMs':2640,'fragmentCount':4,'pool':False}))


def board(frames,times,path):
    out=Image.new('RGB',(1536,640),'white');d=ImageDraw.Draw(out);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
    for i,f in enumerate(frames):
        x=i%4*384;y=i//4*320;out.paste(white(f).resize((384,288),N),(x,y+30));d.text((x+7,y+3),str(times[i])+'ms'+(' · 소멸' if i==7 else ''),font=font,fill='#80586e')
    out.save(path)


def page(out):
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>폐잉크통 · 투척과 파손</title><style>body{margin:0;background:#eee;color:#29252f;font:17px/1.7 system-ui}main{max-width:1230px;margin:auto;padding:24px}section{background:white;padding:20px;margin:20px 0}img{max-width:100%;image-rendering:pixelated}a{color:#80586e}</style><main><h1>폐잉크통이 깨지는 모습</h1><p>통이 닿으면 마개와 몸체3조각이 튀고 짙은 보라색 잉크가 짧게 흩어집니다. 잔류 장판 없이 사라집니다. 새 파손 모션은 검토 시안입니다.</p><p><a href="thrower-can-break.zip" download>파손 PNG·파편·현재 통/본체·시간표 ZIP</a> · <a href="manifest.json">규격</a></p><section><h2>깨지는 순간 확대</h2><img src="previews/break-close-6x.webp"><p>통 픽셀에서 직접 나눈4조각 + 기존색 잉크입니다.8키560ms의 마지막은 빈 키이며 반복 끝600ms는 확인 대기입니다.</p></section><section><h2>투척부터 파손까지</h2><img src="previews/throw-and-break-white-3x.webp"><p>680ms 놓기 → 800ms 예시 비행 → 1480ms 접촉/파손. 비행 중 통은 같은 PNG이며 본체와 박자를 바꾸지 않았습니다.</p></section><section><h2>기존 맵 합성</h2><img src="previews/map.webp"><p>합성 미리보기이며 실제 게임 캡처가 아닙니다. 현재 제안은 직격 피해1회, 파편/흩어진 잉크의 추가 피해와 장판은 없습니다. 실제 충돌/피해/Unity 연결은 별도입니다.</p></section><section><h2>파손 키</h2><img src="previews/break-keys.png"><a href="timeline.json">놓기/파손/소멸 시간표</a></section></main></html>''',encoding='utf-8')


def package(out):
    with zipfile.ZipFile(out/'thrower-can-break.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and not p.name.endswith('commit-message.txt'):z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'thrower-can-break.zip') as z:assert z.testzip() is None


if __name__=='__main__':build()
