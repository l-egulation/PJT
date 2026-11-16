"""Pressure-fed particle ink, draining before the unchanged nozzle retracts."""
import base64
import json
import math
from pathlib import Path
import shutil
import zipfile

import numpy as np
from PIL import Image,ImageDraw,ImageFont
from scipy.spatial import cKDTree

from attack_fx_polish import save_frame,decode_webp,data_uri
from b3_walk_return import dump,white
from h3_slow_arc_impact import key_at
from k4_d_pixel_walk import ROOT,N,sha
from walk_review import animate

HERE=Path(__file__).resolve().parent
SPEC=ROOT/'data/presentation/stage2_k4_pressure_spray.json'


def pressure(t,s,emission=.65):
    if t<0 or t>=emission:return 0.
    if t<s['overshootEndSeconds']:
        u=t/s['overshootEndSeconds'];return 1+(s['pressureOvershoot']-1)*(1-u)**2
    if t<s['plateauEndSeconds']:return 1.
    u=(t-s['plateauEndSeconds'])/(emission-s['plateauEndSeconds'])
    return (1-u*u*(3-2*u))**2


def pair_forces(pos,vel,mass,s):
    """Internal forces are equal/opposite, including viscous momentum exchange."""
    force=np.zeros_like(pos)
    if len(pos)<2:return force
    pairs=cKDTree(pos).query_pairs(s['neighbourRadiusPx'],output_type='ndarray')
    if not len(pairs):return force
    i,j=pairs.T;delta=pos[j]-pos[i];distance=np.linalg.norm(delta,axis=1);direction=delta/np.maximum(distance[:,None],1e-8)
    weight=(1-distance/s['neighbourRadiusPx'])**2
    reduced=mass[i]*mass[j]/(mass[i]+mass[j])
    spring=s['cohesionPerSecondSquared']*(distance-s['restDistancePx'])[:,None]*direction
    viscous=s['viscosityPerSecond']*(vel[j]-vel[i])
    f=reduced[:,None]*weight[:,None]*(spring+viscous)
    np.add.at(force,i,f);np.add.at(force,j,-f)
    return force


def floor_at(x,s):
    floor=np.full(np.shape(x),s['groundSegmentsRelativeToNozzle'][-1][2],float)
    for left,right,y in s['groundSegmentsRelativeToNozzle']:floor[(x>=left)&(x<right)]=y
    return floor


def simulate(s,emission_ms=650,sample_ms=50):
    rng=np.random.default_rng(s['seed']);dt=s['stepSeconds'];g=s['pixelsPerUnit']*s['gravityUnitsPerSecondSquared']
    # x,y,vx,vy,area,birth,contact-age; area acts as 2D mass at unit density.
    particles=np.empty((0,7));carry=0.;emitted=0.;emitted_count=0;contact_times=[];snapshots=[];trace=[]
    sample_steps=round(sample_ms/1000/dt);max_steps=round(s['maxSeconds']/dt)
    for step in range(max_steps+1):
        t=step*dt;p=pressure(t,s,emission_ms/1000);speed=s['baseExitSpeedPxPerSecond']*math.sqrt(p)
        if step%sample_steps==0:
            visible=particles.copy();snapshots.append(visible);trace.append({'relativeMs':round(t*1000),'pressure':p,'exitSpeedPxPerSecond':speed,'liveParticles':len(particles),'emittedArea':emitted,'pendingArea':carry})
        if step==max_steps:break
        if p>0:
            carry+=s['apertureWidthPx']*speed*dt
            count=int(carry/s['packetAreaPxSquared'])
            if count:
                mass=np.full(count,s['packetAreaPxSquared']);carry-=mass.sum();emitted+=mass.sum();emitted_count+=count
                y=rng.uniform(-s['apertureWidthPx']/2,s['apertureWidthPx']/2,count)
                angle=rng.uniform(-.055,.055,count)
                born=np.column_stack([np.zeros(count),y,-speed*np.cos(angle),speed*np.sin(angle),mass,np.full(count,t),np.full(count,-1.)])
                particles=np.vstack([particles,born])
        if not len(particles):continue
        air=particles[:,6]<0;indices=np.flatnonzero(air)
        if len(indices):
            pos=particles[air,:2];vel=particles[air,2:4];mass=particles[air,4]
            force=pair_forces(pos,vel,mass,s)
            acceleration=force/mass[:,None];acceleration[:,1]+=g
            acceleration-=s['quadraticDragPerPx']*np.linalg.norm(vel,axis=1)[:,None]*vel
            vel+=acceleration*dt;old=pos.copy();pos+=vel*dt
            wx,wy0,wy1=s['pitLeftWallRelativeToNozzle']
            wall=(old[:,0]>wx)&(pos[:,0]<=wx)&(pos[:,1]>wy0)&(pos[:,1]<wy1)
            pos[wall,0]=wx;vel[wall,0]=np.maximum(0,-vel[wall,0]*.03)
            floor=floor_at(pos[:,0],s);contact=(pos[:,1]>=floor)&(vel[:,1]>0)
            pos[contact,1]=floor[contact];vel[contact]=0
            particles[indices,:2]=pos;particles[indices,2:4]=vel
            particles[indices[contact],6]=t
            contact_times.extend([t]*int(contact.sum()))
        expired=(particles[:,6]>=0)&(t-particles[:,6]>=s['contactVisualSeconds'])
        particles=particles[~expired]
    assert not len(particles),'Simulation must drain before the final snapshot'
    return snapshots,trace,{'emittedParticles':emitted_count,'emittedArea':emitted,'unemittedRemainderArea':carry,'lastContactMs':round(max(contact_times)*1000),'dtSeconds':dt,'gravityPxPerSecondSquared':g}


def raster(snapshot,time,spec):
    s=spec['simulation'];w,h=spec['effectCanvas'];pivot=np.array(spec['effectPivotPx']);density=np.zeros((h,w),float)
    for x,y,vx,vy,mass,birth,contact in snapshot:
        radius=s['renderKernelRadiusPx'];fade=1. if contact<0 else max(0,1-(time-contact)/s['contactVisualSeconds'])
        if fade<=0:continue
        center=np.array([x,y])+pivot
        x0=max(0,math.floor(center[0]-radius));x1=min(w,math.ceil(center[0]+radius+1));y0=max(0,math.floor(center[1]-radius));y1=min(h,math.ceil(center[1]+radius+1))
        if x0>=x1 or y0>=y1:continue
        yy,xx=np.mgrid[y0:y1,x0:x1];q=((xx-center[0])**2+(yy-center[1])**2)/radius**2
        # Compact normalized deposition kernel: volume/mass feeds density.
        kernel=np.maximum(0,1-q)**2*3/(math.pi*radius**2)
        density[y0:y1,x0:x1]+=mass*fade*kernel
    solid=density>=s['renderDensityThreshold'];a=np.zeros((h,w,4),np.uint8)
    palette=np.array(spec['paletteRgb'],np.uint8);a[solid,:3]=palette[0];a[solid,3]=255
    # The liquid reads through its changing mass/silhouette; no artificial
    # stripe pattern or bright fringe is overlaid on the simulated flow.
    return Image.fromarray(a)


def build():
    spec=json.loads(SPEC.read_text(encoding='utf-8'));out=ROOT/spec['output'];(out/'references').mkdir(parents=True,exist_ok=True)
    shutil.copy2(SPEC,out/'references/presentation-spec.json')
    src=ROOT/spec['bodySource'];body_info=json.loads((src/'manifest.json').read_text(encoding='utf-8'));original_ds=body_info['durationsMs'];assert sum(original_ds[:9])==550 and original_ds[9]==650
    body=[];source_records=[]
    for p in sorted((src/'native').glob('*.png')):
        target=out/'body'/p.name;target.parent.mkdir(exist_ok=True);shutil.copy2(p,target);assert sha(p)==sha(target);body.append(Image.open(target).convert('RGBA'));source_records.append({'source':p.relative_to(ROOT).as_posix(),'copy':target.relative_to(out).as_posix(),'sha256':sha(p)})
    snaps,trace,simulation=simulate(spec['simulation'],spec['emissionMs'],spec['sampleMs'])
    effects=[raster(f,i*spec['sampleMs']/1000,spec) for i,f in enumerate(snaps)]
    last_visible=max(i for i,f in enumerate(effects) if f.getbbox());effects=effects[:last_visible+2];trace=trace[:len(effects)]
    drain_ms=(len(effects)-1)*spec['sampleMs'];assert drain_ms>spec['emissionMs'] and effects[-1].getbbox() is None
    retract=spec['windupMs']+drain_ms
    ds=original_ds.copy();ds[9]=drain_ms;motion=sum(ds);cycle=motion+spec['reviewGapMs']
    paths=[];palette=np.array(spec['paletteRgb'],np.uint8);mask_records=[]
    states={f'sample_{i:02}':state for i,state in enumerate(snaps[:len(effects)])}
    (out/'fx').mkdir(exist_ok=True);np.savez_compressed(out/'fx/particle-states.npz',**states)
    for i,f in enumerate(effects):
        path=out/'fx/native'/f'pressure-{i:02}.png';save_frame(f,path,palette);paths.append(path.relative_to(out).as_posix())
        # Pixel-accurate visual coverage plus its AABB; neither is a game collider.
        mask=f.getchannel('A');p=out/'fx/coverage'/path.name;p.parent.mkdir(exist_ok=True);mask.save(p)
        mask_records.append({'relativeMs':i*spec['sampleMs'],'bboxLocalPx':f.getbbox(),'emitting':i*spec['sampleMs']<spec['emissionMs']})
    dump(out/'fx/particle-trace.json',{'simulation':simulation,'samples':trace})
    dump(out/'fx/coverage.json',{'status':'visual-coverage-only-not-runtime-hitbox','pivotPx':spec['effectPivotPx'],'frames':mask_records})
    world_nozzle=np.array(spec['bodyOffsetWorld'])+spec['nozzleNativePx'];fxpos=tuple(world_nozzle-np.array(spec['effectPivotPx']))
    scene=Image.open(ROOT/spec['map']).convert('RGBA');times=set(np.cumsum([0]+ds).tolist()+[cycle]);times.update(range(550,retract+1,50));times=sorted(times)
    plains=[];maps=[];durations=[];rows=[]
    for t,end in zip(times,times[1:]):
        k=key_at(ds,t);layer=Image.new('RGBA',(960,540));index=(t-550)//50 if 550<=t<retract else None
        if index is not None:layer.alpha_composite(effects[index],fxpos)
        layer.alpha_composite(body[k],tuple(spec['bodyOffsetWorld']));plain=Image.new('RGBA',(960,540),'white');plain.alpha_composite(layer)
        mapping=scene.copy();mapping.alpha_composite(layer);plains.append(plain.crop((110,145,510,380)));maps.append(mapping);durations.append(end-t)
        rows.append({'timeMs':t,'durationMs':end-t,'bodyKey':k+1,'effectKey':index,'emitting':550<=t<1200,'visibleDrain':1200<=t<retract,'retracting':retract<=t<motion})
    animate(plains,durations,out/'review-native.webp');animate([f.resize((800,470),N) for f in plains],durations,out/'review-white-2x.webp');animate(maps,durations,out/'map.webp')
    # Preserve the approved H3 result by byte copy; do not recompute its frames.
    hsrc=ROOT/spec['approvedH3'];(out/'approved-h3/native').mkdir(parents=True,exist_ok=True);hrecords=[]
    for p in sorted((hsrc/'native').glob('*.png'))+[hsrc/'review-native.webp',hsrc/'before-after-4x.webp',hsrc/'timeline.json']:
        rel=('native/'+p.name) if p.suffix=='.png' else p.name;target=out/'approved-h3'/rel;shutil.copy2(p,target);assert sha(p)==sha(target);hrecords.append({'path':rel,'sha256':sha(p)})
    info={**spec,'simulationResult':simulation,'bodyPaths':[r['copy'] for r in source_records],'bodyDurationsMs':ds,'originalBodyDurationsMs':original_ds,'effectPaths':paths,'effectDurationsMs':[50]*len(effects),'effectCoverage':mask_records,'effectVisibleEndRelativeMs':drain_ms,'retractStartMs':retract,'motionMs':motion,'cycleMs':cycle,'timeline':rows,'bodySources':source_records,'h3Approval':'approved-by-user','h3Copies':hrecords,'runtimeIntegration':'not-performed'}
    dump(out/'manifest.json',info);dump(out/'timeline.json',rows)
    board(effects,trace,out)
    page(info,out)
    (out/'README.md').write_text('''# 압력이 빠지는 잉크 분사 · 디자인/합성 검토

index.html은 기존 몸체에 물리 계산한 검정 잉크를 붙인 독립 검토 화면이다. 초기 압력 과도에서 묵직하게 분출하고 공급 압력이 줄면 출구 속도가 낮아진다. 나온 유체 입자는 중력/항력/근거리 응집·점성 힘으로 이동한다. 실제 입자 상태는 fx/particle-states.npz, 압력/속도/분출량은 fx/particle-trace.json에 보존한다. 색은 기존 캐릭터 검정, 알파는0/255다. 노즐 버팀 키는 끝물까지 유지한 뒤 기존700ms 회수 키를 재생한다. 기존19장 PNG는 바이트 그대로다.

속도는 v=v0*sqrt(relativePressure), 분출량은 aperture*v*dt다. 중력은32px/unit·9.81unit/s², 항력 가속도는-k*|v|*v이며 응집/점성은 입자 쌍에 같은 크기·반대 방향 힘을 준다. 단위는 화면에 맞춘 정규화 모델이고 실제 잉크 점도 측정/비압축성 Navier-Stokes 전체 해석은 아니다. 맵의 플랫폼/홈 단면은 이 미리보기용으로만 작성했으며 Unity 지형 충돌이 아니다.

공급650ms 뒤에도 이미 나온 잉크는 낙하한다. 이 때문에 노즐 유지/미리보기 총시간은 이전보다 늘었다. 이 값은 시각 시안이며 기존 전투650ms 판정·피해 수치·새 히트박스를 적용하지 않는다. 끝물 낙하를 전투 판정에 연결할지는 후속 통합에서 정한다. fx/coverage의 각 픽셀 마스크와 AABB는 시각 커버리지 자료다. 지면에 닿는 짧은 방울은 사라지며 잔류 장판을 만들지 않는다.

늘어진 수의27장 생성 보정은 사용자 채택이다. approved-h3/는 기존 결과를 바이트 복사했으며 본체/생성/비행/착탄/장판을 다시 계산하지 않았다. 이전 분사 시안은 보존하고 새 분사는 검토 대기로 둔다. 실제 게임 캡처나 게임/Unity 적용 결과가 아니다.
''',encoding='utf-8')
    dump(out/'validation.json',{'result':'pass','sourceBody19ByteIdentical':True,'holdEndsAfterInkDrains':True,'emissionMs':650,'visualDrainMs':drain_ms-650,'h3Approval':'approved-by-user','h3CopiesByteIdentical':True,'browserQA':'not-run','Unity':'not-run'})
    with zipfile.ZipFile(out/'pressure-spray-assets.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(out.rglob('*')):
            if p.is_file() and p.suffix in ('.png','.json','.npz','.md'):z.write(p,p.relative_to(out).as_posix())
    with zipfile.ZipFile(out/'pressure-spray-assets.zip') as z:assert z.testzip() is None
    print(json.dumps({'effectKeys':len(effects),'emissionMs':650,'drainMs':drain_ms,'retractStartMs':retract,'motionMs':motion,'cycleMs':cycle,'h3':'approved-and-byte-identical'}))


def board(effects,trace,out):
    indices=list(range(0,len(effects)-1,3))+[len(effects)-1];w,h=320,220;font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',15)
    pic=Image.new('RGBA',(w*4,h*math.ceil(len(indices)/4)),'white');d=ImageDraw.Draw(pic)
    for cell,i in enumerate(indices):
        x=cell%4*w;y=cell//4*h;pic.alpha_composite(effects[i],(x+32,y+28));d.text((x+10,y+3),f'{i*50}ms  P {trace[i]["pressure"]:.2f}',font=font,fill='#80586e')
    pic.save(out/'pressure-keys.png')


def page(info,out):
    images={p:data_uri(out/p) for p in info['effectPaths']+info['bodyPaths']};images['map']=data_uri(ROOT/info['map']);info={**info,'images':images}
    for key,rel in [('fullWebp','review-white-2x.webp'),('h3Webp','approved-h3/review-native.webp')]:info[key]='data:image/webp;base64,'+base64.b64encode((out/rel).read_bytes()).decode('ascii')
    script=(HERE/'k4_pressure_spray.js').read_text(encoding='utf-8');html=(HERE/'k4_pressure_spray.html').read_text(encoding='utf-8').replace('/*__DATA__*/','window.PRESSURE_SPRAY='+json.dumps(info,ensure_ascii=False).replace('</','<\\/')+';').replace('/*__VIEWER__*/',script.replace('</','<\\/'))
    (out/'index.html').write_text(html,encoding='utf-8')


if __name__=='__main__':build()
