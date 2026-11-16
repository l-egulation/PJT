"""Slow the existing charge, illustrate a parabolic orb fall and pixel ink impact."""
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from b3_walk_return import dump, white
from h3_orb_return import check_animation
from k4_d_pixel_walk import ROOT,N,sha
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_h3_slow_arc_impact.json'


def quantize_impact(im,palette,width):
    a=np.array(im.convert('RGBA'));y,x=np.where(a[:,:,3]>=128)
    box=[int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]
    h=round((box[3]-box[1])*width/(box[2]-box[0]));q=np.array(im.crop(box).resize((width,h),Image.Resampling.BOX))
    indices=((q[:,:,:3,None].astype(int)-palette.T[None,None].astype(int))**2).sum(2).argmin(2)
    opaque=q[:,:,3]>=128;result=np.zeros_like(q);result[:,:,:3]=palette[indices];result[:,:,3]=opaque.astype(np.uint8)*255;result[~opaque]=0
    return Image.fromarray(result),box


def anchored(patch,canvas,anchor):
    im=Image.new('RGBA',tuple(canvas))
    im.alpha_composite(patch,(anchor[0]-patch.width//2,anchor[1]-patch.height+4))
    return im


def save_asset(frame,out,name,palette):
    path=out/'impact/native'/f'{name}.png';frame.save(path)
    frame.resize((frame.width*4,frame.height*4),N).save(out/'impact/enlarged-4x'/f'{name}.png')
    a=np.array(frame);assert set(np.unique(a[:,:,3]))<={0,255}
    assert set(map(tuple,np.unique(a[a[:,:,3]>0,:3],axis=0)))<=set(map(tuple,palette))
    ix=np.zeros(a.shape[:2],np.uint8)
    for c,rgb in enumerate(palette,1):ix[np.all(a[:,:,:3]==rgb,axis=2)&(a[:,:,3]>0)]=c
    colors=np.concatenate([np.zeros((1,3),np.uint8),palette]);ind=Image.fromarray(ix)
    ind.putpalette(colors.ravel().tolist()+[0]*(768-colors.size));ind.info['transparency']=0
    ind.save(out/'impact/native-indexed'/f'{name}.png');assert ind.convert('RGBA').tobytes()==frame.tobytes()
    return {'path':f'impact/native/{name}.png','sha256':sha(path),'bbox':list(frame.getbbox()) if frame.getbbox() else None}


def flight_center(s,projectile,time):
    u=(time-s['formationMs'])/s['flightMs'];x0,y0=s['startCenterWorld']
    bottom=projectile.getbbox()[3]-projectile.height//2
    y1=s['groundYWorld']-bottom
    return [round(x0+(s['landingXWorld']-x0)*u),round(y0+(y1-y0)*u-4*s['arcHeightPx']*u*(1-u))]


def stage(s,time):
    impact=s['formationMs']+s['flightMs'];burst=sum(s['burstKeyDurationsMs'])
    if time<s['formationMs']:return 'formation'
    if time<impact:return 'flight'
    if time<impact+burst:return 'impact'
    if time<impact+s['puddleMs']:return 'puddle'
    if time<impact+s['puddleMs']+s['fadeMs']:return 'fade'
    return 'review'


def key_at(ds,time):
    acc=0
    for i,d in enumerate(ds):
        acc+=d
        if time<acc:return i
    return len(ds)-1


def bubble_keys(s,puddle):
    """Edit low-resolution ink pixels directly; stagger three grow/pop cycles."""
    dark,purple,blue=[tuple(c)+ (255,) for c in s['impactPaletteRgb']]
    result=[];ax,ay=s['impactAnchor'];top=ay-4
    for i in range(12):
        f=puddle.copy();d=ImageDraw.Draw(f)
        for offset,phase in [(-34,0),(0,4),(34,8)]:
            k=(i+phase)%12;x=ax+offset
            if k<7:
                h=[2,3,5,7,8,6,3][k];w=[3,4,5,6,6,5,4][k]
                d.polygon([(x-w,top+2),(x-w,top-h+3),(x-w+2,top-h+1),(x-2,top-h),(x+2,top-h),(x+w,top-h+3),(x+w,top+2)],fill=purple)
                d.rectangle((x-w+2,top-h+2,x-w+3,top-h+3),fill=blue)
            elif k<10:
                rise=[9,11,8][k-7]
                d.rectangle((x-2,top-rise,x,top-rise+2),fill=purple)
                d.rectangle((x+4,top-4,x+5,top-3),fill=dark)
            else:
                d.rectangle((x-5,top+1,x+5,top+2),fill=blue)
        result.append(f)
    return result


def render(s,time,formation,projectile,bursts,puddles,fades,bodies,scene):
    layer=Image.new('RGBA',(960,540));kind=stage(s,time);impact=s['formationMs']+s['flightMs'];center=None;key=None
    if kind=='formation':
        key=key_at(s['formationDurationsMs'],time);f=formation[key]
        layer.alpha_composite(f,(s['startCenterWorld'][0]-64,s['startCenterWorld'][1]-48))
    elif kind=='flight':
        center=flight_center(s,projectile,time);key=0
        layer.alpha_composite(projectile,(center[0]-32,center[1]-32))
        assert center[1]+projectile.getbbox()[3]-32<=s['groundYWorld']
    else:
        pos=(s['landingXWorld']-s['impactAnchor'][0],s['groundYWorld']-s['impactAnchor'][1])
        if kind=='impact':key=key_at(s['burstKeyDurationsMs'],time-impact);layer.alpha_composite(bursts[key],pos)
        elif kind=='puddle':
            key=key_at(s['puddleBubbleDurationsMs'],(time-impact-sum(s['burstKeyDurationsMs']))%sum(s['puddleBubbleDurationsMs']))
            layer.alpha_composite(puddles[key],pos)
        elif kind=='fade':key=min(3,(time-impact-s['puddleMs'])//50);layer.alpha_composite(fades[key],pos)
    bi=(time//100)%16;layer.alpha_composite(bodies[bi],tuple(s['bodyOffsetWorld']))
    sample=scene.copy();sample.alpha_composite(layer)
    plain=Image.new('RGBA',(960,540),'white')
    ImageDraw.Draw(plain).line((200,s['groundYWorld'],485,s['groundYWorld']),fill='#b4a991',width=1)
    plain.alpha_composite(layer);plain=plain.crop(tuple(s['whitePreviewCrop']))
    return plain,sample,{'timeMs':time,'stage':kind,'bodyKey':bi+1,'assetKey':None if key is None else int(key)+1,'orbCenterWorld':center}


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];source=ROOT/s['orbReturn'];body_source=ROOT/s['bodyFloat']
    for folder in ('generated','formation/native','formation/enlarged-8x','projectile','impact/native','impact/native-indexed','impact/enlarged-4x','references/body-float/native','previews'):
        (out/folder).mkdir(parents=True,exist_ok=True)
    assert sum(s['formationDurationsMs'])==s['formationMs']==1600 and s['formationDurationsMs'][-1]==300
    formation=[];formation_hashes=[]
    for i in range(8):
        p=source/'native'/f'orb-{i+1:02}.png';target=out/'formation/native'/p.name
        shutil.copy2(p,target);assert sha(p)==sha(target);formation_hashes.append(sha(target))
        f=Image.open(target).convert('RGBA');formation.append(f);f.resize((1024,768),N).save(out/'formation/enlarged-8x'/p.name)
    shutil.copy2(source/'projectile-reference/full-orb.png',out/'projectile/full-orb.png')
    projectile=Image.open(out/'projectile/full-orb.png').convert('RGBA');assert projectile.size==(64,64)
    bodies=[];body_hashes=[]
    for p in sorted((body_source/'native').glob('*.png')):
        target=out/'references/body-float/native'/p.name;shutil.copy2(p,target);assert sha(p)==sha(target)
        bodies.append(Image.open(p).convert('RGBA'));body_hashes.append(sha(target))
    assert len(bodies)==16
    raw=out/'generated/impact-original.png';shutil.copy2(s['impactGeneratedSource'],raw)
    assert sha(raw)==sha(Path(s['impactGeneratedSource']))
    palette=np.array(s['orbPaletteRgb'],np.uint8);impact_palette=np.array(s['impactPaletteRgb'],np.uint8)
    small,raw_box=quantize_impact(Image.open(raw).convert('RGBA'),impact_palette,s['impactWidth'])
    peak=anchored(small,s['impactCanvas'],s['impactAnchor'])
    assert s['puddleWidthPx']==round(s['puddleWidthUnitsTrial']*s['pixelsPerUnit'])==112
    puddle_patch=small.crop((0,max(0,small.height-8),small.width,small.height))
    puddle_patch=puddle_patch.crop(puddle_patch.getbbox()).resize((s['puddleWidthPx'],8),N)
    puddle=anchored(puddle_patch,s['impactCanvas'],s['impactAnchor']);puddles=bubble_keys(s,puddle)
    assert puddle.getbbox()[2]-puddle.getbbox()[0]==s['puddleWidthPx']
    ball=projectile.crop(projectile.getbbox());squash=anchored(ball.resize((50,18),N),s['impactCanvas'],s['impactAnchor'])
    bursts=[squash]
    for sx,sy in [(0.65,0.6),(0.85,0.9),(1,1),(1,0.6),(0.95,0.35)]:
        frame=anchored(small.resize((round(small.width*sx),max(1,round(small.height*sy))),N),s['impactCanvas'],s['impactAnchor'])
        frame.alpha_composite(puddle);bursts.append(frame)
    fades=[anchored(puddle_patch.resize((w,8),N),s['impactCanvas'],s['impactAnchor']) for w in (112,80,40)]
    fades.append(Image.new('RGBA',tuple(s['impactCanvas'])))
    assets=[save_asset(f,out,f'burst-{i+1:02}',palette) for i,f in enumerate(bursts)]
    assets.append(save_asset(puddle,out,'puddle',palette))
    for i,f in enumerate(puddles):assets.append(save_asset(f,out,f'puddle-bubble-{i+1:02}',palette))
    dump(out/'impact/puddle-timeline.json',{'paths':[f'native/puddle-bubble-{i+1:02}.png' for i in range(12)],'durationsMs':s['puddleBubbleDurationsMs'],'loopMs':1200,'widthUnitsTrial':3.5,'pixelsPerUnit':32,'visibleWidthPx':112,'widthIsTotalNotRadius':True})
    for i,f in enumerate(fades):assets.append(save_asset(f,out,f'fade-{i+1:02}',palette))
    dump(out/'formation/timeline.json',{'paths':[f'formation/native/orb-{i+1:02}.png' for i in range(8)],'durationsMs':s['formationDurationsMs'],'formedHoldMs':300})
    for p,name in [(SPEC,'presentation-spec.json'),(source/'manifest.json','orb-return-manifest.json'),(ROOT/s['combatReference'],'combat-patterns.json'),(ROOT/s['mapReference'],'C-game.png')]:shutil.copy2(p,out/'references'/name)
    combat=json.loads((ROOT/s['combatReference']).read_text(encoding='utf-8'))
    h=next(m for m in combat['monsters'] if m['id']=='H3')['patterns'][0]
    assert round(h['phases'][0]['seconds']*1000)==s['formationMs'] and s['puddleMs']==3000
    scene=Image.open(out/'references/C-game.png').convert('RGBA');assert scene.size==(960,540)
    plains=[];maps=[];timeline=[]
    for time in range(0,s['previewCycleMs'],s['stepMs']):
        plain,mapping,row=render(s,time,formation,projectile,bursts,puddles,fades,bodies,scene)
        plains.append(plain);maps.append(mapping);timeline.append(row)
    ds=[s['stepMs']]*len(plains);assert sum(ds)==6400
    # The orbital sprite follows a real quadratic curve and reaches the floor exactly.
    start=flight_center(s,projectile,1600);end=flight_center(s,projectile,2600)
    assert start==s['startCenterWorld'] and end[0]==s['landingXWorld']
    assert end[1]+projectile.getbbox()[3]-32==s['groundYWorld']
    assert min(r['orbCenterWorld'][1] for r in timeline if r['stage']=='flight')<start[1]
    assert stage(s,2550)=='flight' and stage(s,2600)=='impact' and stage(s,5600)=='fade'
    assert all(r['bodyKey']==(r['timeMs']//100)%16+1 for r in timeline)
    animate(plains,ds,out/'previews/review-native.webp');check_animation(out/'previews/review-native.webp',plains,ds)
    animate([f.resize((f.width*3,f.height*3),N) for f in plains],ds,out/'previews/review-white-3x.webp')
    animate(maps,ds,out/'previews/map.webp')
    for time,name in [(1300,'formed'),(2100,'arc'),(2750,'impact'),(3200,'puddle')]:
        p,m,_=render(s,time,formation,projectile,bursts,puddles,fades,bodies,scene)
        p.resize((p.width*3,p.height*3),N).save(out/'previews'/f'{name}-white-3x.png');m.save(out/'previews'/f'{name}-map.png')
    board(bursts+[puddle],out/'previews/impact-keys.png')
    board(puddles,out/'previews/puddle-bubble-keys.png')
    animate([white(f).resize((640,256),N) for f in puddles],s['puddleBubbleDurationsMs'],out/'previews/puddle-bubbles-white-4x.webp')
    check_animation(out/'previews/puddle-bubbles-white-4x.webp',[white(f).resize((640,256),N) for f in puddles],s['puddleBubbleDurationsMs'])
    info={**s,'impactTimeMs':2600,'puddleEndMs':5600,'previewReviewGapMs':600,'bodyPixelsChanged':0,
          'actorPhaseTotalMsProposal':round(sum(p['seconds'] for p in h['phases'])*1000),
          'bodyFloatHashes':body_hashes,'bodyCycleMs':1600,'formationHashes':formation_hashes,'projectileSha256':sha(out/'projectile/full-orb.png'),
          'impactGenerationTool':'built-in image_gen','impactOriginalSha256':sha(raw),'impactOriginalBox':raw_box,
          'impactPromptSha256':sha(ROOT/s['impactGenerationPrompt']),
          'impactPixelVisibleSize':list(small.size),'impactAssets':assets,'timeline':timeline,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);dump(out/'trajectory-and-stages.json',timeline);page(out,info)
    (out/'README.md').write_text('''# 늘어진 수의 · 느린 생성 → 포물선 낙하 → 바닥 잉크 파열

반환 구의 생성8장 픽셀을 바이트 그대로 복사하고850→1600ms로 늘린다. 마지막 완성형을300ms 보여 준다. 원본 발사 꼬리 장면은 사용하지 않고 완성된38×40px 공을 정수 좌표로 이동시킨다. 예시 곡선은 시작[372,193]에서 실제 맵 예시 바닥[276,354]로1초 동안 상승 후 낙하하는 포물선이며 끝의 공 아랫부분이 바닥에 정확히 닿는다. 좌표/곡률/비행1초는 시안용이며 실제 게임 조준·충돌·탄도 구현이 아니다.

착탄 파열의 최대 정지 그림만 기본 image_gen으로 생성하고 원본·프롬프트·해시를 보존한다. 알파 점유로 폭80px/기존3색의 픽셀 후보로 환원한다. 기존 구 압착과 생성된 파열 그림의 최근접 크기 변형으로6키300ms를 만들고, 밑면에서 추출한 얕은 잉크 웅덩이를 이어 붙인다. derived keys는 시안이며 수작업 완성 FX나 생성 원본 모션이라고 표현하지 않는다. 파열 그림으로 폭발 추가 피해를 만들지 않는다.

장판 전체 폭은 프로젝트 PPU32 기준112px=3.5유닛으로 사용자3~4유닛 제안의 중간값이다. 반경이 아니며 실제 판정 폭은 미구현이다. 기존3색을 직접 픽셀 편집한12장/1200ms 반복으로 세 곳에서 잉크 방울이 번갈아 부풀고 터진다. 거품은 잔류 위험 표시이며 피해 범위 확장이 아니다. 장판은 기존3초 시험안에 맞춰 착탄2600ms부터5600ms까지 보이고200ms 동안 축소해 사라진다. 마지막600ms는 확인 대기이며 전체 반복6400ms/본체 부유4주기에 맞춘다. 본체 B16장/100ms·생성8장 픽셀/구 크기는 변경하지 않는다. 배경/플레이어를 바꾸지 않은 맵 합성과 흰 바닥선 비교를 제공하며 실제 게임 캡처가 아니다. 느린 생성·곡사 착탄·부글 장판 시각 시안은 사용자 채택이며 전투 피해·장판 판정/본 게임/Unity 적용은 하지 않는다.
''',encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','formation8ByteIdentical':True,'body16ByteIdentical':True,'bodyTimingMs':100,
         'formationMs':1600,'formedHoldMs':300,'flightMsIllustration':1000,'arcRisesBeforeFalling':True,
         'orbBottomTouchesGroundExactly':True,'impactAtMs':2600,'puddleLifetimeMsTrial':3000,
         'allSpritesBinaryAlpha':True,'impactColorsSubsetOfExisting4':True,'indexedRgbaExact':True,
         'decodedNativePreviewExact':True,'previewLoopMs':6400,'puddleWidthPx':112,'puddleWidthUnitsTrial':3.5,'pixelsPerUnit':32,
         'puddleBubbleKeys':12,'puddleBubbleLoopMs':1200,'decodedBubblePreviewExact':True,'browserQA':'not-run','Unity':'not-run'})
    package(out)
    print(json.dumps({'formationMs':1600,'impactMs':2600,'splashSize':list(small.size),'reviewMs':6400}))


def board(frames,path):
    result=Image.new('RGB',(160*4*4,286*((len(frames)+3)//4)),'white');draw=ImageDraw.Draw(result);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
    for i,f in enumerate(frames):
        x,y=i%4*640,i//4*286;result.paste(white(f).resize((640,256),N),(x,y+28));draw.text((x+8,y+2),('부글 '+str(i+1)) if len(frames)==12 else ('착탄 '+str(i+1) if i<6 else '잔류 장판'),font=font,fill='#80586e')
    result.save(path)


def package(out):
    with zipfile.ZipFile(out/'H3-slow-arc-impact.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and p.name!='commit-message.txt':z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'H3-slow-arc-impact.zip') as z:assert z.testzip() is None


def page(out,info):
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>늘어진 수의 · 느린 생성과 곡사 착탄</title><style>body{margin:0;background:#eee;color:#28252c;font:16px/1.7 system-ui}main{max-width:1150px;padding:28px;margin:auto}section{background:white;padding:20px;margin:20px 0}h1{font-size:28px}h2{font-size:22px}img{max-width:100%;height:auto;image-rendering:pixelated}a{color:#80586e}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}</style><main><h1>늘어진 수의 · 천천히 생성 → 포물선 → 바닥 파열</h1><p>생성1.6초와 완성형0.3초 버팀으로 전조를 길게 보여줍니다. 원본 발사 꼬리는 빼고 공 모양 그대로 상승 후 낙하해 바닥에서 잉크가 퍼집니다. 사용자가 채택한 시각 모션의 합성 미리보기입니다.</p><p><a download href="H3-slow-arc-impact.zip">독립 픽셀 PNG·원본·생성 프롬프트·시간표 ZIP</a> · <a href="manifest.json">규격/해시</a> · <a href="trajectory-and-stages.json">곡선/단계별 정수 좌표</a></p><section><h2>흰 바닥선 비교</h2><img src="previews/review-white-3x.webp" alt="느린 공 생성과 포물선 낙하, 바닥 잉크 파열"><p>0~1.6초 생성 → 1.6~2.6초 곡사 비행 → 2.6초 착탄/파열 → 전체 폭3.5유닛(112px)의 부글거리는3초 장판 → 소멸. 6.4초 반복의 마지막0.6초는 확인 대기입니다. 본체 부유16장은 계속 재사용합니다.</p></section><section><h2>기존 맵 합성 · 960×540</h2><img src="previews/map.webp" alt="기존 지하수로 바닥에 곡사 착탄하는 공"><p>합성 미리보기이며 실제 게임 캡처가 아닙니다. 배경/플레이어는 원본 그대로이고, 표시한 예시 궤적과 낙하지점은 게임 조준/충돌 구현을 뜻하지 않습니다.</p></section><section><h2>단계별 보기</h2><div class="grid"><div><p>느리게 생성·완성형 유지</p><img src="previews/formed-white-3x.png"></div><div><p>완성된 공의 포물선 낙하</p><img src="previews/arc-white-3x.png"></div><div><p>바닥에 닿아 낮게 퍼지는 잉크</p><img src="previews/impact-white-3x.png"></div><div><p>잔류 장판 · 전체 폭3.5유닛</p><img src="previews/puddle-white-3x.png"></div></div></section><section><h2>잔류 위험 표시 · 부글부글</h2><img src="previews/puddle-bubbles-white-4x.webp" alt="잉크 표면 세 곳에서 번갈아 부풀고 터지는 거품"><p>전체 폭3.5유닛 = 112px(PPU32). 사용자3~4유닛 제안의 중간값이며 반경이 아닙니다. 표면12장/1.2초 반복은 기존3색의 직접 픽셀 편집입니다. 솟는 방울은 잔류 위험을 보여주며 별도 피해 범위가 아닙니다. 지속 시간3초는 기존 시험안입니다.</p><img src="previews/puddle-bubble-keys.png" alt="부글거림12키"></section><section><h2>착탄 파열/장판 픽셀</h2><img src="previews/impact-keys.png"><p>최대 파열 정지 그림은 기본 image_gen으로 생성하고 기존3색으로 픽셀화했습니다. 최근접 변형으로 연결한 착탄6키와 잔류 장판은 검토 후보입니다. 화염/네온 폭발과 추가 폭발 피해는 넣지 않았습니다.</p></section><p>전투 기획의 생성 전조는1.6초 시험안으로 갱신했습니다. 장판 전체 폭3.5유닛/부글거림을 추가하고 기존 지속/피해 제안은 유지하며 본체/구 생성8장 픽셀과 크기는 바꾸지 않았습니다. 실제 피해/비행 충돌/장판 판정/Unity 연결은 아직 없습니다.</p></main></html>''',encoding='utf-8')


if __name__=='__main__':build()
