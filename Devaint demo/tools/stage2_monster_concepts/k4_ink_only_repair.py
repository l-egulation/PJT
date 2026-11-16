"""Isolate returned ink; rebuild clipped native tips without replacing body art."""
import base64
import json
import math
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from attack_fx_polish import save_frame
from b3_walk_return import dump, white
from h3_orb_return import check_animation
from h3_slow_arc_impact import key_at
from k4_aether_spray_return import pixel_frame
from k4_d_pixel_walk import ROOT, N, sha
from walk_review import animate

SPEC = ROOT / 'data/presentation/stage2_k4_ink_only_repair.json'


def restore_tip(ink, widths, palette, seam):
    """Rasterize a closed continuation for each cut contour, on missing side only.

    Widths are authored per source frame/run, not generated particle motion.
    Tangent and pigment at each cut are taken from the existing ink. Exported
    patch PNGs are independent editable layers. No original pixel is replaced.
    """
    a = np.array(ink); patch = np.zeros_like(a)
    ys = np.where(a[:, seam, 3] > 0)[0]
    runs = [r for r in np.split(ys, np.where(np.diff(ys)>1)[0]+1) if len(r)]
    assert len(runs) == len(widths), ([(r[0],r[-1]) for r in runs], widths)
    for run, length in zip(runs, widths):
        lo, hi = int(run[0]), int(run[-1]); center = (lo+hi)/2; radius=(hi-lo+1)/2
        # Near-cut slope supports the existing falling ribbon, bounded so small
        # islands do not shoot away. Far tip closes with a heavy rounded edge.
        nearby = np.where(a[max(0,lo-4):hi+5, seam+4,3]>0)[0] + max(0,lo-4)
        tangent = np.clip((center-nearby.mean())/4,-.35,.35) if len(nearby) else 0
        pigment = a[lo:hi+1,seam+1,:3]
        for d in range(1,length+1):
            u=d/(length+1)
            sag = (3 if length<15 else 8) * u*u
            c = center + tangent*min(d,8) + sag
            r = radius*math.sqrt(max(0,1-u*u))*(1+.1*math.sin(math.pi*u))
            top=round(c-r+.5); bottom=round(c+r-.5)
            for y in range(max(0,top),min(a.shape[0]-1,bottom)+1):
                if seam-d<0: continue
                v=(y-top)/max(1,bottom-top)
                sample=pigment[min(len(pigment)-1,round(v*(len(pigment)-1)))]
                color=palette[0]
                # Continue cut highlights; taper them before the rounded edge.
                if np.array_equal(sample,palette[1]) and u<.74 and .07<v<.82:
                    color=palette[1]
                patch[y,seam-d,:3]=color;patch[y,seam-d,3]=255
    result=ink.copy(); result.alpha_composite(Image.fromarray(patch))
    assert np.array_equal(np.array(result)[:,seam:],a[:,seam:])
    return result, Image.fromarray(patch)


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];(out/'references').mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/s['source'],out/'references/aether-original.png');shutil.copy2(SPEC,out/'references/spec.json')
    assert sha(ROOT/s['source'])==sha(out/'references/aether-original.png')
    sheet=Image.open(ROOT/s['source']).convert('RGBA');palette=np.array(s['paletteRgb'],np.uint8)
    fx=[];before=[];patches=[];records=[];seam=s['effectPivot'][0]-s['scaledSourceNozzle'][0]
    cfg={'commonScale':s['scale'],'whiteCutoff':225,'minimumSourceComponentArea':12,'nativeCoverageThreshold':.4,'nativeCanvas':[400,224]}
    for n in range(s['sourceRangeOneBased'][0],s['sourceRangeOneBased'][1]+1):
        i=n-1;x=i%6*1280;y=i//6*720;cell=sheet.crop((x,y,x+1280,y+720))
        # The nozzle's leftmost metal starts at x~470. Everything from x468
        # rightward is excluded, before quantization, including the entire body.
        ImageDraw.Draw(cell).rectangle((s['sourceNozzleXExclusive'],0,1279,719),fill='white')
        f,_=pixel_frame(cell,cfg,palette);a=np.array(f);a[:,145:]=0
        ink=Image.new('RGBA',tuple(s['effectCanvas']));ink.alpha_composite(Image.fromarray(a),(seam,s['effectPivot'][1]-s['scaledSourceNozzle'][1]))
        widths=s['sourceEdgeRunExtensionWidths'].get(str(n),[])
        restored,patch=restore_tip(ink,widths,palette,seam) if widths else (ink.copy(),Image.new('RGBA',ink.size))
        k=len(fx)+1
        for folder,im in [('fx',restored),('before',ink),('repair-layers',patch)]:save_frame(im,out/folder/'rgba'/f'ink-{k:02}.png',palette)
        aa=np.array(restored);assert not aa[0,:,3].any() and not aa[-1,:,3].any() and not aa[:,0,3].any() and not aa[:,-1,3].any()
        assert not aa[:,s['effectPivot'][0]:,3].any(), 'Unexpected gun/body pixels to right of muzzle'
        fx.append(restored);before.append(ink);patches.append(patch)
        records.append({'key':k,'sourceOneBased':n,'restoredPixels':int((np.array(patch)[:,:,3]>0).sum()),'bbox':restored.getbbox(),'visibleSourcePixelsUnchanged':True})
    ds=[70 if n<=24 else 80 if n<=27 else 90 for n in range(5,32)]
    empty=Image.new('RGBA',tuple(s['effectCanvas']));fx.append(empty);before.append(empty);patches.append(empty);ds.append(s['emptyHoldMs'])
    save_frame(empty,out/'fx/rgba'/f'ink-{len(fx):02}.png',palette)
    bodydir=ROOT/s['body'];body_manifest=json.loads((bodydir/'manifest.json').read_text(encoding='utf-8'))
    body_ds=body_manifest['durationsMs'].copy();assert sum(body_ds[:9])==550
    body_ds[9]=sum(ds);body=[];body_records=[]
    for p in sorted((bodydir/'native').glob('*.png')):
        target=out/'body'/p.name;target.parent.mkdir(exist_ok=True);shutil.copy2(p,target)
        assert sha(p)==sha(target);body.append(Image.open(p).convert('RGBA'));body_records.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p)})
    motion=sum(body_ds);cycle=motion+s['reviewGapMs'];retract=550+sum(ds)
    bounds=sorted(set([0,cycle]+np.cumsum(body_ds).tolist()+(550+np.cumsum([0]+ds)).tolist()))
    world_nozzle=np.array(s['bodyOffset'])+s['bodyNozzle'];fxpos=tuple(world_nozzle-s['effectPivot'])
    composed=[];steps=[];durations=[]
    for t,end in zip(bounds,bounds[1:]):
        bk=key_at(body_ds,t);ek=key_at(ds,t-550) if 550<=t<retract else None
        im=Image.new('RGBA',(520,300));im.alpha_composite(body[bk],tuple(s['bodyOffset']))
        if ek is not None:im.alpha_composite(fx[ek],fxpos)
        composed.append(white(im).convert('RGBA'));durations.append(end-t);steps.append({'timeMs':t,'durationMs':end-t,'bodyKeyOneBased':bk+1,'inkKeyOneBased':ek+1 if ek is not None else None})
    animate(composed,durations,out/'existing-attack-native.webp');check_animation(out/'existing-attack-native.webp',composed,durations)
    animate([f.resize((1040,600),N) for f in composed],durations,out/'existing-attack-2x.webp')
    for name,frames in [('ink-only',fx),('before',before)]:
        imgs=[white(f).convert('RGBA') for f in frames];d=ds.copy();d[-1]+=500
        animate(imgs,d,out/f'{name}-native.webp');check_animation(out/f'{name}-native.webp',imgs,d)
        animate([f.resize((864,672),N) for f in imgs],d,out/f'{name}-3x.webp')
    board(out,fx,before,patches,seam)
    paths=[f'fx/rgba/ink-{i+1:02}.png' for i in range(len(fx))]
    manifest={**s,'sourceSha256':sha(ROOT/s['source']),'effectPaths':paths,'durationsMs':ds,'records':records,'bodyFiles':body_records,'body19ByteIdentical':True,'bodyDurationsMs':body_ds,'timeline':steps,'retractStartMs':retract,'cycleMs':cycle,'sourceBoundaryX':seam,'userSheetBodyUsed':False,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',manifest);page(out,manifest)
    (out/'README.md').write_text('''# 등짐분사수 잉크 전용 복원 시안

에테르 반환 시트의 본체/노즐은 제외하고 잉크만 분리한다. 기존 팔레트의 검정·회색 2색과 이진 알파로 축소한 후, 잘린 왼쪽 윤곽의 17프레임을 네이티브 픽셀로 연장해 닫았다. 원본에서 보이는 잉크 픽셀은 보존한다. 사라진 끝은 원본 복구가 아니라 새로 보충한 시안이며 repair-layers/에 별도로 보관한다. 실제 유체 시뮬레이션이 아니다.

본체는 기존 승인 A19장 PNG를 바이트 동일하게 복사해 사용한다. 준비550ms/회수700ms는 유지하고 잉크 종료까지 분사 자세만 연장한다. 흰 배경 합성 검토이며 실제 게임 캡처나 전투/히트박스 확정본이 아니다. H3·전투 데이터·Unity는 변경하지 않는다.
''',encoding='utf-8')
    with zipfile.ZipFile(out/'ink-only-and-existing-body.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix in {'.png','.json','.md'}:z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'ink-only-and-existing-body.zip') as z:assert z.testzip() is None
    dump(out/'validation.json',{'body19ByteIdentical':True,'userReturnedBodyUsed':False,'allowedColors':s['paletteRgb'],'binaryAlpha':True,'noCanvasEdgeClipping':True,'visibleSourcePixelsPreserved':True,'repairedFrames':sum(r['restoredPixels']>0 for r in records),'webpPixelsAndTimingExact':True,'browserQA':'not-run','Unity':'not-run'})
    print(json.dumps({'entry':str(out/'index.html'),'repairedFrames':sum(r['restoredPixels']>0 for r in records),'inkKeys':len(fx),'bodyPixelsChanged':0},ensure_ascii=False))


def board(out,fx,before,patches,seam):
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
    chosen=[6,8,12,16,19,20]  # source11/13/17/21/24/25
    im=Image.new('RGBA',(288*3*3,270*2),'white');d=ImageDraw.Draw(im)
    for j,k in enumerate(chosen):
        ox=j%3*864;oy=j//3*270
        for col,(name,f) in enumerate([('잘린 잉크',before[k]),('끝 복원',fx[k]),('추가한 픽셀',patches[k])]):
            x=ox+col*288;im.alpha_composite(f,(x,oy+40));d.text((x+8,oy+7),f'원본{k+5:02} · {name}',font=font,fill='#62505f')
            d.line((x+seam,oy+38,x+seam,oy+240),fill='#e0c2c2')
    im.save(out/'repair-comparison.png')
    one=Image.new('RGBA',(576*2,448),'white')
    one.alpha_composite(white(before[8]).convert('RGBA').resize((576,448),N),(0,0));one.alpha_composite(white(fx[8]).convert('RGBA').resize((576,448),N),(576,0));one.save(out/'tip-before-after-2x.png')


def page(out,m):
    # Embedded WebP avoids filesystem fetch restrictions for the two review clips.
    def uri(p):return 'data:image/webp;base64,'+base64.b64encode((out/p).read_bytes()).decode()
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>등짐분사수 · 잉크만 분리 / 잘린 끝 복원</title>
<style>body{margin:0;background:#ede9e2;color:#302b32;font:16px/1.7 system-ui}main{max-width:1120px;margin:auto;padding:24px}section{background:white;border:1px solid #cfc7bd;border-radius:10px;padding:20px;margin:20px 0}img{max-width:100%;height:auto;image-rendering:pixelated;display:block}button,a{color:#714e68}button{padding:10px;font:inherit;background:white;border:1px solid #bcb1b8}p{margin:10px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.note{color:#82536b}</style>
<main><h1>기존 공격 본체 + 분리한 잉크</h1><p>에테르에서 가져온 것은 잉크만 사용. 잘린 17장의 끝을 픽셀로 보충했습니다.</p><p class="note">복원 시안·합성 미리보기입니다. 실제 게임 캡처가 아닙니다.</p>
<section><h2>기존 승인 공격 모션에 부착</h2><img id="body" src="__BODY__" alt="기존19장 공격 모션과 복원 잉크 합성"><p>준비 → 분사 자세 유지 → 끝물 분리 → 기존 회수 모션. 본체 그림은 기존 승인본 그대로입니다.</p></section>
<section><h2>잉크만 확인</h2><button id="switch">잘린 상태 보기</button><button id="bg">어두운 배경</button><img id="ink" src="__INK__" alt="검정회색2색의 잉크 전용 애니메이션"><p>투명 PNG 28장(종료 빈 프레임 포함). 아래 판에서 복원 전/후와 추가 픽셀을 따로 확인할 수 있습니다.</p></section>
<section><h2>잘린 끝 비교</h2><img src="repair-comparison.png" alt="잉크 끝 원본과 복원 비교"><p>분홍 세로선이 원본의 잘린 경계입니다. 그 왼쪽만 보충했고, 오른쪽의 잉크는 그대로 보존했습니다. 누락된 끝의 모양은 검토용 재구성입니다.</p><a href="ink-only-and-existing-body.zip" download>투명 잉크·보정 레이어·기존 본체 ZIP</a></section></main>
<script>const repaired='__INK__',before='__BEFORE__';let showBefore=false;document.getElementById('switch').onclick=()=>{showBefore=!showBefore;document.getElementById('ink').src=showBefore?before:repaired;document.getElementById('switch').textContent=showBefore?'복원한 상태 보기':'잘린 상태 보기'};document.getElementById('bg').onclick=()=>{document.querySelectorAll('section').forEach(s=>s.style.background=s.style.background?'':'#928b96')};</script></html>'''
    # Animated review WebPs are white; do not offer misleading background toggle.
    html=html.replace('<button id="bg">어두운 배경</button>','').replace(";document.getElementById('bg').onclick=()=>{document.querySelectorAll('section').forEach(s=>s.style.background=s.style.background?'':'#928b96')}",'')
    (out/'index.html').write_text(html.replace('__BODY__',uri('existing-attack-2x.webp')).replace('__INK__',uri('ink-only-3x.webp')).replace('__BEFORE__',uri('before-3x.webp')),encoding='utf-8')


if __name__=='__main__':build()
