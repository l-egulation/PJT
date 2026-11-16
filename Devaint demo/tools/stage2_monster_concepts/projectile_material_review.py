"""Retouch approved pixel material and extract the actual thrown prop without AI repainting."""
import json
import math
import shutil
import zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.ndimage import label,binary_fill_holes

from b3_walk_return import dump,white
from h3_orb_return import check_animation,restore_filled_material
from h3_slow_arc_impact import render,key_at
from k4_d_pixel_walk import ROOT,N,sha,sample
from walk_review import animate

SPEC=ROOT/'data/presentation/stage2_projectile_material_review.json'


def save_pixel(im,path,palette,pivot):
    path.parent.mkdir(parents=True,exist_ok=True);im.save(path)
    a=np.array(im);solid=a[:,:,3]>0
    assert set(np.unique(a[:,:,3]))<={0,255}
    assert set(map(tuple,np.unique(a[solid,:3],axis=0)))<=set(map(tuple,palette))
    bb=im.getbbox();assert bb and bb[0]>0 and bb[1]>0 and bb[2]<im.width and bb[3]<im.height
    assert not np.any(binary_fill_holes(solid)&~solid),'Unexpected internal transparent pixels'
    ix=np.zeros(a.shape[:2],np.uint8)
    for i,c in enumerate(palette,1):ix[solid&np.all(a[:,:,:3]==c,axis=2)]=i
    pi=Image.fromarray(ix);pal=np.vstack([np.zeros((1,3),np.uint8),palette]);pi.putpalette(pal.ravel().tolist()+[0]*(768-pal.size));pi.info['transparency']=0
    target=path.parent.parent/'indexed'/path.name;target.parent.mkdir(parents=True,exist_ok=True);pi.save(target)
    assert pi.convert('RGBA').tobytes()==im.tobytes()
    target=path.parent.parent/'enlarged-8x'/path.name;target.parent.mkdir(parents=True,exist_ok=True)
    im.resize((im.width*8,im.height*8),N).save(target)
    return {'path':path.relative_to(ROOT).as_posix(),'sha256':sha(path),'bbox':list(bb),'pivotPx':pivot,'opaquePixels':int(solid.sum())}


def dynamics(s):
    """q'' +2*zeta*w*q' +w*w*q =w*w*speed_load; semi-implicit fixed dt."""
    dt=s['modelStepSeconds'];w=s['restoringFrequencyRadiansPerSecond'];z=s['dampingRatio']
    q=0.;v=s['initialDeformationVelocity'];trace=[]
    for i in range(241):
        t=i/240.;u=t
        # Velocity from the approved quadratic center trajectory in px/second.
        vy=141-360+720*u;speed=math.hypot(-96,vy)
        target=s['speedLoad']*(speed/450.)**2
        trace.append({'ms':round(t*1000),'strain':q,'strainVelocity':v,'speedPxPerSecond':speed,'load':target})
        v+=(w*w*(target-q)-2*z*w*v)*dt;q+=v*dt
    return trace


def deform(source,model,u,s):
    a=np.array(source);yy,xx=np.indices(tuple(reversed(s['canvas'])));px,py=s['pivotPx']
    q=model['strain'];stretch=math.exp(q)
    velocity=np.array([-96.,141-360+720*u]);e=velocity/np.linalg.norm(velocity);n=np.array([-e[1],e[0]])
    x=xx-px;y=yy-py;A=x*e[0]+y*e[1];B=x*n[0]+y*n[1]
    taper=s['maximumTaper']*min(1,abs(q)/.45);radius=23.
    # A'=stretch*(a-taper*a*a/(2R)); inverse with positive Jacobian.
    if taper>1e-8:
        disc=1-2*taper*A/(stretch*radius);valid=disc>0
        aa=radius/taper*(1-np.sqrt(np.maximum(0,disc)))
    else:aa=A/stretch;valid=np.ones_like(A,dtype=bool)
    jac=stretch*(1-taper*aa/radius)
    bend=s['maximumBendPx']*min(1,abs(q)/.45)*(aa/radius)**2
    bb=(B-bend)*jac
    sx=np.rint(aa*e[0]+bb*n[0]+32).astype(int);sy=np.rint(aa*e[1]+bb*n[1]+32).astype(int)
    valid&=(sx>=0)&(sx<a.shape[1])&(sy>=0)&(sy<a.shape[0])&(jac>0)
    out=np.zeros((*yy.shape,4),np.uint8);out[valid]=a[sy[valid],sx[valid]]
    source_y,source_x=np.where(a[:,:,3]>0);new_y,new_x=np.where(out[:,:,3]>0)
    target=np.array([source_x.mean()-32+px,source_y.mean()-32+py])
    shift=np.rint(target-np.array([new_x.mean(),new_y.mean()])).astype(int)
    out=np.roll(out,(int(shift[1]),int(shift[0])),axis=(0,1))
    new_y,new_x=np.where(out[:,:,3]>0)
    assert np.max(np.abs(target-[new_x.mean(),new_y.mean()]))<=.501
    return Image.fromarray(out)


def board(frames,path,labels,scale=4):
    cols=5;cw=frames[0].width*scale;ch=frames[0].height*scale+32
    out=Image.new('RGB',(cols*cw,math.ceil(len(frames)/cols)*ch),'white');d=ImageDraw.Draw(out)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',17)
    for i,f in enumerate(frames):
        x=i%cols*cw;y=i//cols*ch;out.paste(white(f).resize((cw,ch-32),N),(x,y+32));d.text((x+5,y+3),labels[i],fill='#80586e',font=font)
    out.save(path)


def h3_build(s,out):
    src=ROOT/s['h3Approved'];info=json.loads((src/'manifest.json').read_text(encoding='utf-8'));p=s['h3']
    source=Image.open(src/'projectile/full-orb.png').convert('RGBA');pal=np.array(info['orbPaletteRgb'],np.uint8)
    original_pixels=int((np.array(source)[:,:,3]>0).sum());trace=dynamics(p)
    dest=out/'h3';dest.mkdir(exist_ok=True);shutil.copy2(src/'projectile/full-orb.png',dest/'approved-round-orb.png')
    poses=[];rows=[]
    for ms in range(0,1001,50):
        model=trace[round(ms/1000*240)];f=deform(source,model,ms/1000,p)
        r=save_pixel(f,dest/'native'/f'ink-flight-{ms//50+1:02}.png',pal,p['pivotPx'])
        r.update(relativeMs=ms,strain=model['strain'],areaRatio=r['opaquePixels']/original_pixels)
        assert abs(r['areaRatio']-1)<=p['areaTolerance'];poses.append(f);rows.append(r)
    dump(dest/'deformation-model.json',{'equation':"q''+2*zeta*omega*q'+omega^2*q=omega^2*load",'model':p,'trace':trace,'frames':rows})
    formation=[Image.open(src/'formation/native'/f'orb-{i:02}.png').convert('RGBA') for i in range(1,9)]
    bursts=[Image.open(src/'impact/native'/f'burst-{i:02}.png').convert('RGBA') for i in range(1,7)]
    puddles=[Image.open(src/'impact/native'/f'puddle-bubble-{i:02}.png').convert('RGBA') for i in range(1,13)]
    fades=[Image.open(src/'impact/native'/f'fade-{i:02}.png').convert('RGBA') for i in range(1,5)]
    bodies=[Image.open(src/'references/body-float/native'/f'float-{i:02}.png').convert('RGBA') for i in range(1,17)]
    scene=Image.open(src/'references/C-game.png').convert('RGBA');plains=[];maps=[];old_plains=[];timeline=[]
    bottom=poses[-1].getbbox()[3]-48;end_y=info['groundYWorld']-bottom
    for ms in range(0,6400,50):
        old_plain,old_map,row=render(info,ms,formation,source,bursts,puddles,fades,bodies,scene)
        old_plains.append(old_plain)
        if 1600<=ms<2600:
            k=(ms-1600)//50;u=(ms-1600)/1000;x0,y0=info['startCenterWorld']
            center=[round(x0+(info['landingXWorld']-x0)*u),round(y0+(end_y-y0)*u-4*info['arcHeightPx']*u*(1-u))]
            f=poses[k];assert center[1]+f.getbbox()[3]-48<=info['groundYWorld']
            layer=Image.new('RGBA',(960,540));layer.alpha_composite(f,(center[0]-48,center[1]-48));layer.alpha_composite(bodies[(ms//100)%16],tuple(info['bodyOffsetWorld']))
            mapping=scene.copy();mapping.alpha_composite(layer);plain=Image.new('RGBA',(960,540),'white');ImageDraw.Draw(plain).line((200,354,485,354),fill='#b4a991');plain.alpha_composite(layer);plain=plain.crop(tuple(info['whitePreviewCrop']))
            row.update(orbCenterWorld=center,deformationKey=k+1)
        else:plain,mapping=old_plain,old_map
        plains.append(plain);maps.append(mapping);timeline.append(row)
    durations=[50]*128
    animate(plains,durations,dest/'review-native.webp');check_animation(dest/'review-native.webp',plains,durations)
    animate([f.resize((855,810),N) for f in plains],durations,dest/'review-white-3x.webp');animate(maps,durations,dest/'map.webp')
    comparison=[]
    for old,new in zip(old_plains,plains):
        f=Image.new('RGBA',(570,270),'white');f.alpha_composite(old,(0,0));f.alpha_composite(new,(285,0));comparison.append(f.resize((1140,540),N))
    animate(comparison,durations,dest/'before-after.webp')
    flight=poses[:-1];animate([white(f).resize((576,576),N) for f in flight]+[white(poses[0]).resize((576,576),N)],[50]*20+[600],dest/'deformation-white-6x.webp')
    selected=[0,2,4,7,10,13,16,19,20]
    board([poses[i] for i in selected],dest/'flight-keys.png',[f'{i*50}ms' for i in selected])
    dump(dest/'trajectory.json',timeline)
    return {'status':'pending-user-review','flightKeys':21,'flightSampleCount':20,'frameMs':50,'areaRatios':[r['areaRatio'] for r in rows],
            'bodyPixelsChanged':0,'formationPixelsChanged':0,'impactPuddlePixelsChanged':0,'floorContactCenter':[info['landingXWorld'],end_y],
            'approvedSourceManifestSha256':sha(src/'manifest.json'),'originalProjectileSha256':sha(src/'projectile/full-orb.png'),'modelScope':p['modelScope']}


def thrower_build(s,out):
    src=ROOT/s['throwerApproved'];info=json.loads((src/'manifest.json').read_text(encoding='utf-8'));p=s['thrower'];dest=out/'thrower';dest.mkdir(exist_ok=True)
    pal=np.array(info['paletteRgb'],np.uint8);frames=[];records=[]
    for key in p['sourceKeysOneBased']:
        original=src/'detached-source'/f'attack-{key:02}.png';target=dest/'source'/original.name;target.parent.mkdir(exist_ok=True);shutil.copy2(original,target)
        original_image=Image.open(original).convert('RGBA')
        raw=sample(original_image,pal,info['scale'],info['offset'],info)
        raw,repair=restore_filled_material(raw,original_image,pal,info['scale'],info['offset'],info['supersampleGrid'])
        a=np.array(raw)
        labels,_=label(a[:,:,3]>0);counts=np.bincount(labels.ravel());counts[0]=0;keep=labels==counts.argmax();a[~keep]=0
        y,x=np.where(keep);box=[int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)];patch=Image.fromarray(a).crop(tuple(box))
        f=Image.new('RGBA',tuple(p['canvas']));f.alpha_composite(patch,(32-patch.width//2,32-patch.height//2));frames.append(f)
        r=save_pixel(f,dest/'extracted/native'/f'source-{info["mapping"][key-1]["sourceOneBased"]:03}.png',pal,p['pivotPx'])
        r.update(sourceKey=key,sourceOneBased=info['mapping'][key-1]['sourceOneBased'],sourceFileSha256=sha(original),approvedScale=info['scale'],nativeSourceBounds=box,sourceSupportedMaterialPixelsRecovered=int(repair.sum()));records.append(r)
    rotations=[]
    for i in range(4):
        # Rotate the patch, then re-center so odd/even dimensions never move the pivot.
        patch=frames[0].crop(frames[0].getbbox()).rotate(-90*i,resample=N,expand=True)
        f=Image.new('RGBA',(64,64));f.alpha_composite(patch,(32-patch.width//2,32-patch.height//2));rotations.append(f)
        save_pixel(f,dest/'projectile/native'/f'prop-{i+1:02}.png',pal,p['pivotPx'])
    body=[];body_hashes=[]
    for r in info['mapping']:
        original=src/r['png'];target=dest/'body-native'/original.name;target.parent.mkdir(exist_ok=True);shutil.copy2(original,target);assert sha(original)==sha(target)
        body.append(Image.open(original).convert('RGBA'));body_hashes.append(sha(target))
    assert sum(info['durationsMs'][:10])==p['releaseMs']==680
    scene=Image.open(src/'references/C-game.png').convert('RGBA');off=info['mapOffset'];spawn=p['spawnCenterWorld'];assert spawn==[p['heldCenterNative'][0]+off[0],p['heldCenterNative'][1]+off[1]]
    assert body[9].getpixel(tuple(p['heldCenterNative']))[3]==255
    final_center_y=p['groundYWorldIllustration']-(rotations[-1].getbbox()[3]-32)
    # Exact approved body pose boundaries, plus every projectile50ms; no retiming the body.
    boundaries=set([0,p['previewLoopMs'],1450,p['releaseMs'],p['releaseMs']+p['flightMsIllustration']]);acc=0
    for d in info['durationsMs']:acc+=d;boundaries.add(acc)
    boundaries.update(range(680,1481,50));bs=sorted(boundaries);plains=[];maps=[];durations=[];timeline=[]
    for ms,end in zip(bs,bs[1:]):
        k=key_at(info['durationsMs'],ms);layer=Image.new('RGBA',(960,540));layer.alpha_composite(body[k],tuple(off));center=None;spin=None
        if 680<=ms<1480:
            u=(ms-680)/800;spin=min(3,(ms-680)//200);f=rotations[spin]
            center=[round(spawn[0]+(165-spawn[0])*u),round(spawn[1]+(final_center_y-spawn[1])*u-4*80*u*(1-u))]
            layer.alpha_composite(f,(center[0]-32,center[1]-32))
        mapping=scene.copy();mapping.alpha_composite(layer);plain=Image.new('RGBA',(960,540),'white');ImageDraw.Draw(plain).line((130,288,500,288),fill='#b4a991');plain.alpha_composite(layer);plain=plain.crop((125,95,510,310))
        plains.append(plain);maps.append(mapping);durations.append(end-ms);timeline.append({'timeMs':ms,'durationMs':end-ms,'approvedBodyKey':k+1,'projectileCenterWorld':center,'projectileRotationKey':None if spin is None else spin+1})
    animate(plains,durations,dest/'review-native.webp');check_animation(dest/'review-native.webp',plains,durations)
    animate([f.resize((1155,645),N) for f in plains],durations,dest/'review-white-3x.webp');animate(maps,durations,dest/'map.webp')
    animate([white(f).resize((512,512),N) for f in rotations],[200]*4,dest/'projectile-white-8x.webp')
    board(rotations,dest/'projectile-keys.png',['0°','90°','180°','270°'],scale=6)
    board(frames,dest/'extracted-candidates.png',['원본26 · 사용','원본27 · 참고','원본29 · 참고'],scale=6)
    dump(dest/'extraction.json',records);dump(dest/'timeline.json',timeline)
    return {'status':'pending-user-review','sourceOneBased':26,'sourceCandidateCount':3,'projectileNativeSize':list(frames[0].size),'projectileVisibleSize':[frames[0].getbbox()[2]-frames[0].getbbox()[0],frames[0].getbbox()[3]-frames[0].getbbox()[1]],'pivotPx':[32,32],'rotationKeys':4,'body19ByteIdentical':True,'bodyHashes':body_hashes,'bodyAttackMs':1450,'releaseMs':680,'spawnCenterWorld':spawn,'trajectoryScope':'800ms illustrative arc only; not game physics or collision implementation'}


def page(out):
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>투사체 재질 보정 · 수의와 투척수</title><style>body{margin:0;background:#eee;color:#29252f;font:16px/1.7 system-ui}main{max-width:1155px;margin:auto;padding:24px}section{background:white;padding:20px;margin:20px 0}img{max-width:100%;height:auto;image-rendering:pixelated}a{color:#80586e}</style><main><h1>늘어진 잉크와 실제 투척 물체 · 검토 시안</h1><p>채택한 본체 모션과 색은 유지했습니다. 새 투사체 보정은 사용자 검토 전이며 기존 채택본을 덮어쓰지 않습니다. 아래 화면은 합성 미리보기입니다.</p><p><a href="projectile-material-review.zip" download>독립 PNG·모델/시간표·원본 추출·참조 ZIP</a> · <a href="manifest.json">규격</a></p><section><h2>늘어진 수의 · 점성 있는 덩어리</h2><img src="h3/review-white-3x.webp"><p>1.6초 생성은 그대로 두고, 던질 때 늘어짐 → 정점 부근 다시 뭉침 → 낙하하며 아래쪽 덩어리와 뒤쪽 목이 달라지는 변형을 추가했습니다. 중력 포물선과 발사 변형의 복원·감쇠를 분리합니다. 면적을 약7% 이내로 유지하는2D 픽셀 근사이며 정밀 유체 시뮬레이션이나 실제 잉크 물성값은 아닙니다. 생성/본체/착탄/장판 픽셀은 유지했습니다.</p><img src="h3/map.webp"><h3>비행 형상 확대</h3><img src="h3/deformation-white-6x.webp"><img src="h3/flight-keys.png"><p><a href="h3/deformation-model.json">복원·감쇠 모델과 키별 면적</a> · <a href="h3/trajectory.json">좌표/합성 시간표</a> · <a href="https://docs.blender.org/manual/en/5.1/physics/fluid/type/domain/liquid/diffusion.html">점성·표면장력 참고: Blender 공식 문서</a></p></section><section><h2>신호투척수 · 던지는 물체를 독립 투사체로</h2><img src="thrower/review-white-3x.webp"><p>채택한 원본26번에서 이미 손을 떠난 황토색 물체를 분리했습니다. 본체와 같은 배율·기존12색으로 추출하고 원본26번을 비행체로 사용합니다. 회전4키는 기존 픽셀을90도씩 돌린 것이며 다시 그린 물체가 아닙니다. 키11 시작680ms에 마지막 손 위 물체의 위치에서 생성해 본체에서 분리합니다. 이후800ms 포물선은 조준 투척을 보여주는 예시이고 실제 탄도/지형 충돌 구현은 아닙니다.</p><img src="thrower/projectile-white-8x.webp"><img src="thrower/projectile-keys.png"><img src="thrower/map.webp"><h3>원본에서 분리한3개 참고</h3><img src="thrower/extracted-candidates.png"><p><a href="thrower/extraction.json">원본 좌표·배율·해시</a> · <a href="thrower/timeline.json">놓기/본체/투사체 시간표</a></p></section><p>이미지 재생성 없이 기존 픽셀을 결정적으로 보정·추출했습니다. 본체19장/16장과 승인된 시간은 유지합니다. 게임에 쓸 독립 PNG·피벗을 준비했으며 Unity/게임 판정 연결은 수행하지 않았습니다.</p></main></html>'''
    (out/'index.html').write_text(html,encoding='utf-8')


def build():
    s=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/s['output'];out.mkdir(parents=True,exist_ok=True)
    h=h3_build(s,out);r=thrower_build(s,out)
    (out/'references').mkdir(exist_ok=True);shutil.copy2(SPEC,out/'references/presentation-spec.json')
    for name,path in [('h3-approved-manifest.json',ROOT/s['h3Approved']/'manifest.json'),('thrower-approved-manifest.json',ROOT/s['throwerApproved']/'manifest.json')]:shutil.copy2(path,out/'references'/name)
    dump(out/'manifest.json',{**s,'h3Result':h,'throwerResult':r,'runtimeIntegration':'not-performed'});page(out)
    p=out/'index.html';p.write_text(p.read_text(encoding='utf-8').replace('<h3>비행 형상 확대</h3>','<h3>왼쪽 기존 / 오른쪽 점성 보정</h3><img src="h3/before-after.webp"><h3>비행 형상 확대</h3>'),encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','h3AreaRatios':h['areaRatios'],'h3BinaryAlphaExisting4Colors':True,'allNativeIndexedExact':True,'allNativeNoInternalHoles':True,'approvedBodiesUnchanged':True,'throwerBody19ExactTiming':True,'throwerReleaseMs':680,'newGamePhysicsOrCollision':'not-implemented','browserQA':'not-run','Unity':'not-run'})
    with zipfile.ZipFile(out/'projectile-material-review.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix!='.zip':z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'projectile-material-review.zip') as z:assert z.testzip() is None
    print(json.dumps({'h3FlightKeys':h['flightKeys'],'h3AreaRange':[min(h['areaRatios']),max(h['areaRatios'])],'throwerVisibleSize':r['projectileVisibleSize'],'releaseMs':680}))


if __name__=='__main__':build()
