"""Cut the original cylinder at native size; preserve rigid pixels through release."""
import json
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import binary_dilation

from b3_walk_return import dump,white
from h3_orb_return import check_animation
from h3_slow_arc_impact import key_at
from k4_d_pixel_walk import ROOT,N,sha
from projectile_material_review import save_pixel
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_thrower_original_prop.json'


def centered(patch):
    f=Image.new('RGBA',(64,64));f.alpha_composite(patch,(32-patch.width//2,32-patch.height//2));return f


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];src=ROOT/s['approvedAttack']
    for folder in ('references','native','previews','body-original','body-display','masks'):(out/folder).mkdir(parents=True,exist_ok=True)
    info=json.loads((src/'manifest.json').read_text(encoding='utf-8'));pal=np.array(info['paletteRgb'],np.uint8)
    base=Image.open(ROOT/s['base']).convert('RGBA');mask=Image.new('L',base.size);ImageDraw.Draw(mask).polygon(s['visiblePropPolygonNative'],fill=255)
    a=np.array(base);keep=(np.array(mask)>0)&(a[:,:,3]>0);cut=np.zeros_like(a);cut[keep]=a[keep]
    full=Image.fromarray(cut);box=full.getbbox();visible_cutout=centered(full.crop(box))
    repair=np.zeros(cut.shape[:2],bool)
    for y,x0,x1 in s['occlusionRepairRowsNative']:repair[y,x0:x1+1]=True
    before=cut.copy();cut[repair]=s['occlusionRepairRgb']+[255]
    assert np.array_equal(cut[~repair],before[~repair])
    surface_changed=np.any(cut!=before,axis=2);repair_mask=np.zeros_like(cut);repair_mask[surface_changed]=[230,40,130,255]
    Image.fromarray(repair_mask).save(out/'masks/upper-edge-repair.png')
    full=Image.fromarray(cut);assert full.getbbox()==box
    patch=full.crop(box);prop=centered(patch)
    flight=centered(patch.transpose(Image.Transpose.ROTATE_90))
    assert sorted(map(tuple,np.array(prop)[np.array(prop)[:,:,3]>0]))==sorted(map(tuple,np.array(flight)[np.array(flight)[:,:,3]>0]))
    records=[save_pixel(visible_cutout,out/'native/prop-visible-cutout.png',pal,[32,32]),save_pixel(prop,out/'native/prop-original.png',pal,[32,32]),save_pixel(flight,out/'native/prop-flight-fixed.png',pal,[32,32])]
    mask.save(out/'masks/original-prop-selection.png');shutil.copy2(ROOT/s['base'],out/'references/approved-base.png');shutil.copy2(SPEC,out/'references/presentation-spec.json')
    shutil.copy2(ROOT/s['rejectedProjectile'],out/'references/rejected-round-projectile.png')
    shutil.copy2(src/'manifest.json',out/'references/approved-attack-manifest.json')
    body=[];body_hashes=[];corrections=[]
    for row in info['mapping']:
        old=src/row['png'];target=out/'body-original'/old.name;shutil.copy2(old,target);assert sha(old)==sha(target)
        frame=Image.open(old).convert('RGBA');original=np.array(frame);c=next((c for c in s['heldCorrections'] if c['keyOneBased']==row['key']),None)
        if c:
            x0,y0,x1,y1=c['oldPropRoi'];roi=np.zeros(original.shape[:2],bool);roi[y0:y1,x0:x1]=True
            purple=np.all(original[:,:,:3]==[97,76,105],axis=2)&(original[:,:,3]>0)
            # Keep the existing glove and the2px wrist fringe above the sleeve.
            wrist=binary_dilation(purple,iterations=2)&np.all(original[:,:,:3]==[87,67,56],axis=2)
            foreground=(purple|wrist)&roi
            edited=original.copy();edited[roi&~foreground]=0;frame=Image.fromarray(edited)
            cx,cy=c['propCenterNative'];frame.alpha_composite(flight,(cx-32,cy-32));result=np.array(frame);result[foreground]=original[foreground];frame=Image.fromarray(result)
            changed=np.any(result!=original,axis=2);prop_region=np.zeros_like(roi);px,py=cx-32,cy-32;prop_region[py:py+64,px:px+64]=np.array(flight)[:,:,3]>0
            assert not np.any(changed&~(roi|prop_region));assert np.array_equal(result[foreground],original[foreground])
            m=np.zeros_like(result);m[changed]=[230,40,130,255];Image.fromarray(m).save(out/'masks'/old.name)
            corrections.append({'keyOneBased':row['key'],'changedPixels':int(changed.sum()),'handPurpleAndWristPreserved':True,'outsideExplicitPropAreasUnchanged':True,'propCenterNative':[cx,cy]})
        else:assert frame.tobytes()==Image.open(old).convert('RGBA').tobytes()
        frame.save(out/'body-display'/old.name);body.append(frame);body_hashes.append(sha(target))
    scene=Image.open(src/'references/C-game.png').convert('RGBA');off=info['mapOffset'];cx,cy=s['heldCorrections'][-1]['propCenterNative'];spawn=[cx+off[0],cy+off[1]]
    assert sum(info['durationsMs'][:10])==s['releaseMs']==680
    final_y=s['groundYWorldIllustration']-(flight.getbbox()[3]-32)
    bounds={0,1450,s['previewLoopMs'],680,1480};acc=0
    for d in info['durationsMs']:acc+=d;bounds.add(acc)
    bounds.update(range(680,1481,50));bounds=sorted(bounds);plains=[];maps=[];durations=[];timeline=[]
    for ms,end in zip(bounds,bounds[1:]):
        k=key_at(info['durationsMs'],ms);layer=Image.new('RGBA',(960,540));layer.alpha_composite(body[k],tuple(off));center=None
        if 680<=ms<1480:
            u=(ms-680)/800;center=[round(spawn[0]+(165-spawn[0])*u),round(spawn[1]+(final_y-spawn[1])*u-4*80*u*(1-u))]
            layer.alpha_composite(flight,(center[0]-32,center[1]-32))
        plain=Image.new('RGBA',(960,540),'white');ImageDraw.Draw(plain).line((125,288,510,288),fill='#b4a991');plain.alpha_composite(layer);plain=plain.crop((125,80,510,310));mapping=scene.copy();mapping.alpha_composite(layer)
        plains.append(plain);maps.append(mapping);durations.append(end-ms);timeline.append({'timeMs':ms,'durationMs':end-ms,'bodyKey':k+1,'projectileCenterWorld':center,'projectilePng':'native/prop-flight-fixed.png' if center else None})
    animate(plains,durations,out/'previews/review-native.webp');check_animation(out/'previews/review-native.webp',plains,durations)
    animate([f.resize((1155,690),N) for f in plains],durations,out/'previews/review-white-3x.webp');animate(maps,durations,out/'previews/map.webp')
    proof(out,base,prop,flight,plains,timeline)
    manifest={**s,'sourceSha256':sha(ROOT/s['base']),'sourceBounds':list(box),'visibleSize':[patch.width,patch.height],'outputAssets':records,'sourcePixelsOutsideRepairExact':True,'upperEdgePixelsRepaired':int(surface_changed.sum()),'nativeResampling':False,'newColorsAdded':False,'wholeFlightSpriteIdentical':True,'spawnCenterWorld':spawn,'bodyOriginalHashes':body_hashes,'displayCorrections':corrections,'bodyTimingUnchanged':True,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',manifest);dump(out/'timeline.json',timeline)
    page(out)
    dump(out/'validation.json',{'result':'pass','pixelsOutsideRepairExact':True,'upperEdgePixelsRepaired':int(surface_changed.sum()),'repairRowsOpaqueExistingOchre':True,'unrepairedCutoutPreserved':True,'visibleSize':[patch.width,patch.height],'noSquashStretchOrFlightRotation':True,'exactQuarterTurnPixelHistogram':True,'binaryAlphaPaletteIndexedExact':True,'nativePreviewPixelsAndTimeExact':True,'approved19BodyFilesPreserved':True,'displayCorrectionsOnlyKeys9And10':True,'existingHandAndWristPreserved':True,'releaseMs':680,'browserQA':'not-run','Unity':'not-run'})
    (out/'README.md').write_text('''# 신호투척수 원형 물체 복원 · 검토용

둥글게 변한 원본26번 발사 후 그림 대신 승인 기본본의 가시 원통형 물체를 native 픽셀 좌표의 명시 마스크로 분리한다. 사용자 요청에 따라 손에 가려진 윗면 홈/남은 갈색 손 부분만 명시4행의 기존 황토색으로 채운다. 수정 마스크 바깥은 픽셀 그대로며 새 색/크기 변형은 없다.64×64 캔버스·중심[32,32]. prop-visible-cutout.png는 보정 전 분리본, prop-original.png는 윗면을 채운 원형, prop-flight-fixed.png는 정확한 반시계90도 회전이다. 비행 전체에서 같은 PNG를 사용한다.

마지막 쥔 자세 키9/10의 물체 부분도 같은 강체 PNG로 교체해 발사 전후 둥글어지는 변화를 줄인다. 보라색 손/손목2px 여유와 명시 영역 바깥은 보존한다. 승인19장 원본과 새 body-display를 나누며 타이밍1450ms/놓기680ms는 그대로다. 새 모양/정지 방향은 검토 전이다. 기존 수의나 다른 공격 파일은 수정하지 않는다. 비행800ms 곡사는 합성 예시이며 게임 탄도/충돌/피해/Unity 적용은 별도다.
''',encoding='utf-8')
    package(out);print(json.dumps({'visibleSize':[patch.width,patch.height],'flightSpriteConstant':True,'heldKeysCorrected':[9,10],'spawn':spawn}))


def proof(out,base,prop,flight,plains,timeline):
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',18)
    old=Image.open(out/'references/rejected-round-projectile.png').convert('RGBA');board=Image.new('RGB',(1536,550),'white');d=ImageDraw.Draw(board)
    for i,(f,title) in enumerate([(Image.open(out/'native/prop-visible-cutout.png').convert('RGBA'),'분리 직후 · 손에 가려진 홈'),(prop,'보정 · 기존 황토색으로 윗면 연결'),(flight,'비행 · 동일 비율의 고정 강체')]):
        board.paste(white(f).resize((512,512),N),(i*512,38));d.text((i*512+8,5),title,font=font,fill='#80586e')
    board.save(out/'previews/prop-comparison.png');enlarged=white(prop).resize((512,512),N)
    enlarged.save(out/'previews/original-prop-white-8x.png');enlarged.save(out/'previews/prop-complete-white-8x.png')
    context=white(base.crop((138,80,246,194))).resize((648,684),N);context.save(out/'previews/approved-context-6x.png')
    result=Image.new('RGB',(2310,490),'white');d=ImageDraw.Draw(result)
    for i,ms in enumerate((640,680,730)):
        k=next(k for k,r in enumerate(timeline) if r['timeMs']<=ms<r['timeMs']+r['durationMs']);result.paste(white(plains[k]).resize((770,460),N),(i*770,30));d.text((i*770+8,3),str(ms)+'ms',font=font,fill='#80586e')
    result.save(out/'previews/release-continuity.png')


def page(out):
    (out/'index.html').write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>신호투척수 · 원래 물체 그대로</title><style>body{margin:0;background:#eee;color:#29252f;font:17px/1.7 system-ui}main{max-width:1155px;margin:auto;padding:24px}section{background:white;padding:20px;margin:20px 0}img{max-width:100%;image-rendering:pixelated}a{color:#80586e}</style><main><h1>신호투척수 · 원래 물체 그대로</h1><p>원본은 짙은 마개가 붙은 길쭉한 원통형 물체입니다. 둥글게 바뀐 발사 후 프레임 대신 승인 기본본의 픽셀을 직접 분리하고, 손에 가려져 홈처럼 비었던 윗면은 기존 황토색으로 채웠습니다. 비행 중에는 같은 PNG를 유지합니다. 새 보정은 사용자 검토 전입니다.</p><p><a href="thrower-original-prop.zip" download>원형/비행 PNG·원본·마스크·시간표 ZIP</a> · <a href="manifest.json">규격</a></p><section><h2>원래 손에 든 형태와 분리본</h2><img src="previews/approved-context-6x.png"><img src="previews/original-prop-white-8x.png"><p>35×25 원형에서 윗면 홈/손에 가려진 부분만 기존 황토색으로 메웠습니다. 보정 전 분리본과 수정 마스크도 보존했습니다. 비행용은 정확한90도 회전으로 손을 든 방향에 맞춘25×35입니다. 크기를 줄이거나 눌러 변형하지 않습니다.</p><img src="previews/prop-comparison.png"></section><section><h2>놓기 전후 연결</h2><img src="previews/review-white-3x.webp"><img src="previews/release-continuity.png"><p>발사 직전 키9/10의 물체도 같은 고정 형상으로 맞췄습니다. 보라색 손과 손목/주변 픽셀을 유지하고 승인 원본은 따로 보존했습니다. 놓기680ms와 본체1450ms 박자는 같습니다.</p></section><section><h2>기존 맵 합성</h2><img src="previews/map.webp"><p>합성 미리보기입니다. 비행800ms는 비교용 예시이며 실제 게임 캡처나 Unity 적용 결과가 아닙니다. 수의 보정은 <a href="../projectile-material-review-20261101/index.html">기존 비교 페이지</a>에서 그대로 볼 수 있습니다.</p></section></main></html>''',encoding='utf-8')


def package(out):
    with zipfile.ZipFile(out/'thrower-original-prop.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and not p.name.startswith(('diagnostic','prop-trace','commit-message')):z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'thrower-original-prop.zip') as z:assert z.testzip() is None


if __name__=='__main__':build()
