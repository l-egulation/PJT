"""Soft transients, continuous beds and hit-triggered review events.

Requires numpy. Reference measurements describe the recorded mix, not isolated SFX.
"""
import copy
import math
import subprocess
import zipfile
from pathlib import Path
import numpy as np
import render_basic_attack_audio as m


def revise(payload, archive_path, source, reference_video=None, ffmpeg=None, include_reference=True):
    audio=payload['audio']; actions={a['id']:a for a in payload['actions']}
    old=copy.deepcopy(actions); groups=payload['sharedGroups']; cache={}; pcms={}
    z=zipfile.ZipFile(archive_path); names=z.namelist(); rate=m.RATE

    def bank(prefix):
        found=[n for n in names if n.startswith(prefix) and n.endswith('.wav')]
        assert found,prefix
        return sorted(found)

    def samples(key):
        if key not in pcms:pcms[key]=np.asarray(m.decode((source/'audio'/audio[key]['file']).read_bytes()))
        return pcms[key]

    def register(x,origin,edit):
        raw=m.wav_bytes(x); key='r3/'+m.sha(raw)[:20]; name=key.replace('/','-')+'.wav'
        (source/'audio'/name).write_bytes(raw); y=np.asarray(m.decode(raw));pcms[key]=y
        audio[key]={**m.builder.audio_payload(raw,name),'peak':float(np.max(np.abs(y))),'peakTime':m.sliding_peak(y.tolist()),'gain':1,'source':origin,'edit':edit}
        return key

    def filtered(x,low,high=35):
        # Smooth spectral rolloff avoids sharp metallic/noisy transients and filter ringing.
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        curve=1/np.sqrt(1+(f/low)**8)
        if high:curve*=1/np.sqrt(1+(high/np.maximum(f,.001))**8)
        return np.fft.irfft(np.fft.rfft(y)*curve,n=len(y))[pad:pad+n]

    def tailfade(x,attack=.006,release=.12):
        x=x.copy();a=min(len(x)//4,round(attack*rate));b=min(len(x)//2,round(release*rate))
        x[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2
        x[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
        return x

    def transient(entries,i,low=1500,db=-21,pitch=1,length=.5,attack=.006,release=.15,full=False,high=35):
        entry=entries[i%len(entries)];pitch*=1+.035*(i//len(entries))
        sig=(entry,i,low,db,pitch,length,attack,release,full,high)
        if sig in cache:return cache[sig]
        raw=z.read(entry);x=np.asarray(m.decode(raw))
        threshold=max(abs(x))*.006;start=max(0,int(np.flatnonzero(abs(x)>threshold)[0])-round(.008*rate))
        if not full:
            peak=m.sliding_peak(x.tolist());start=max(0,round((peak-.015)*rate))
        x=x[start:start+round(length*pitch*rate)]
        x=np.interp(np.arange(0,max(1,len(x)-1),pitch),np.arange(len(x)),x)
        x=tailfade(filtered(x,low,high),attack,release)
        key=register(m.level(x.tolist(),db),{'archiveEntry':entry,'sha256':m.sha(raw)},dict(mode='natural-tail',lowpass=low,highpass=high,pitch=pitch,activeRmsDb=db,tailFadeSeconds=release,attackSeconds=attack))
        cache[sig]=key;return key

    def process(key,low=750,pitch=.78,db=-22,length=None):
        x=samples(key);x=np.interp(np.arange(0,len(x)-1,pitch),np.arange(len(x)),x)
        if length:x=x[:round(length*rate)]
        x=tailfade(filtered(x,low),.008,.13)
        return register(m.level(x.tolist(),db),{'processedSource':key,'sha256':audio[key]['sha256']},dict(mode='rounded-body',lowpass=low,pitch=pitch,activeRmsDb=db))

    def mix(parts,label,db=-22):
        count=max(len(samples(k))+round(at*rate) for k,at,gain in parts);x=np.zeros(count)
        for k,at,gain in parts:
            n=round(at*rate);x[n:n+len(samples(k))]+=samples(k)*gain
        return register(m.level(tailfade(x).tolist(),db),{'composite':label,'parts':[dict(audio=k,onset=at,gain=gain) for k,at,gain in parts]},dict(mode='layered-event',activeRmsDb=db))

    def continuous(entries,i,low=750,high=45,db=-26,hum=False,spin=False):
        """Four-second periodic spectral bed; no repeated one-shot attacks or silent joins."""
        entry=entries[i%len(entries)];raw=z.read(entry);x=np.asarray(m.decode(raw))
        n=4*rate;size=4096;power=np.zeros(size//2+1);count=0
        for start in np.linspace(0,max(0,len(x)-size),min(32,max(1,len(x)//size))).astype(int):
            frame=np.pad(x[start:start+size],(0,max(0,size-len(x[start:start+size]))));power+=abs(np.fft.rfft(frame*np.hanning(size)))**2;count+=1
        f=np.fft.rfftfreq(n,1/rate);mag=np.sqrt(np.interp(f,np.fft.rfftfreq(size,1/rate),power/max(1,count)))
        mag*=1/np.sqrt(1+(f/low)**8);mag*=1/np.sqrt(1+(high/np.maximum(f,.001))**8);mag[0]=0
        rng=np.random.default_rng(3100+i+int(low));phase=rng.uniform(-np.pi,np.pi,len(f))
        y=np.fft.irfft(mag*np.exp(1j*phase),n=n)
        # Periodic 150ms RMS leveling removes amplitude holes without restarting samples.
        window=np.zeros(n);width=round(.15*rate);window[:width//2]=1/width;window[-width//2:]=1/width
        env=np.sqrt(np.maximum(1e-15,np.fft.irfft(np.fft.rfft(y*y)*np.fft.rfft(window),n=n)))
        y/=env
        t=np.arange(n)/rate
        if hum:
            # Integer number of cycles in 4 seconds, stable rather than tremolo/chopped.
            fundamental=70+i*3
            y=.16*y+np.sin(2*np.pi*fundamental*t)+.18*np.sin(2*np.pi*fundamental*2*t+.4)
        if spin:y*=.78+.22*np.cos(2*np.pi*(12+i*.5)*t)
        key=register(m.level(y.tolist(),db),{'archiveEntry':entry,'sha256':m.sha(raw)},dict(mode='continuous-spectrum',periodSeconds=4,lowpass=low,highpass=high,stableHum=hum,rotationHz=12+i*.5 if spin else None,activeRmsDb=db,seed=3100+i+int(low)))
        return key

    def layer(id,name,key,times,gain=1,anchor='absolute',align='peak',**extra):return dict(id=id,name=name,audio=key,times=times,gain=gain,anchor=anchor,align=align,**extra)
    def shared(id,name,group,times,i,anchor='impactDemo'):
        v=groups[group]['variants'][i%len(groups[group]['variants'])]
        return layer(id,name,v['audio'],times,v['gain'],anchor,'onset',group=group)

    cannon=bank('Guns_Weapons/Guns/weapon_cannon_shot_')
    small=bank('Explosion_Fire_Gas/explosion_small_')
    body=bank('Punches/punch_general_body_impact_')
    air=bank('Whooshes/whoosh_swish_high_fast_')+bank('Whooshes/whoosh_swish_med_fast_')+bank('Whooshes/whoosh_object_pass_small_')
    deep=bank('Whooshes/whoosh_low_deep_')
    softwind=bank('Magic_Spells/wind_blizzard_storm_spell_airy_')
    humbank=bank('Backgrounds/background_hum_air_fan_device_loop_')+bank('Backgrounds/background_room_interior_hum_loop_')
    normal=[];large=[];throws=[]
    base_peak,base_rms,_=m.stats(samples(payload['basicExplosion']).tolist());base_peak*=.8;base_rms*=.8
    for i in range(10):
        key=transient(small,i,low=1000+i*70,db=-22,length=.55,release=.20)
        peak,rms,_=m.stats(samples(key).tolist());gain=min(base_rms/rms,base_peak*1.3/peak)
        normal.append(dict(audio=key,gain=gain,name=f'기본 폭발 {i+1:02} · 낮은 펑'))
        big=process(key,low=850+i*45,pitch=.74,db=-20)
        bp,br,_=m.stats(samples(big).tolist());bg=min(base_rms*10**(4/20)/br,base_peak*2/bp)
        large.append(dict(audio=big,gain=bg,name=f'큰 폭발 {i+1:02} · 둥근 저음'))
        throw=transient(deep,i,low=2800,db=-21,length=.27,release=.10)
        throws.append(dict(audio=throw,gain=.9,name=f'큰 수리검 투척 {i+1:02}'))
    groups['normal_explosion']={'label':'기본 폭탄 계열 폭발','scope':'폭탄 기본공격 후보 · 3연속 폭탄 · 폭발 수리검','revision':3,'variants':normal}
    groups['large_explosion']={'label':'큰 폭탄 공통 폭발','scope':'대형 폭탄 · 착지 시한폭탄 · 미사일','revision':3,'variants':large}
    groups['shuriken_throw']={'label':'큰 수리검 공통 투척','scope':'큰 관통 수리검 · 전진 회전 수리검','revision':3,'variants':throws}

    for wid in ['launcher','bomb']:
        w=next(w for w in payload['weapons'] if w['id']==wid);b=w['basics'][0];speed=w['defaultSpeed'];current=w['currentVariants']['attack1']
        a=dict(id=wid+'_basic_review',weapon=wid,name='기본공격 새 후보',isBasicReview=True,summary='영상의 짧고 낮은 발음에 맞춘 기본공격 비교 · 승인 전',duration=b['duration']/speed,impact=b['impact']/speed,markers=[b['impact']/speed],clip=w['clips']['attack1'],effect=w['effects'].get('attack1'),motion=wid+'/attack1',current=current,currentTiming={'nominalDuration':audio[current]['nominalDuration']},currentRate=audio[current]['nominalDuration']/(b['duration']/speed),previewDuration=b['duration']/speed+.4)
        actions[a['id']]=a;payload['actions'].append(a)

    reasons={
        'base_sword_follow_up':'직접 베기 뒤 짧고 빠른 잔상 베기 · 물·포털 소리 제거',
        'whip_follow_up':'감기 뒤 한 번 팡 · 조이는 마찰음 제거',
        'rapier_default':'충전 소리 없이 찌르기만',
        'dagger_range':'작은 펑 이후 잔잔하게 유지되는 연막',
        'shield_default':'이전 1번을 바탕으로 낮고 둥근 퉁',
        'shield_range':'전개음 없이 부드러운 웅 유지음만',
        'shield_movement':'쇳소리·반복 타격 없이 연속으로 밀어붙이는 바람',
        'shield_follow_up':'시전 뒤 둔탁한 설치음 · 날카로운 금속음 제거',
        'greatsword_range':'소환 뒤 낮은 쿵 · 금속 칼날 울림 제거',
        'launcher_basic_review':'영상 참고 · 짧고 낮은 포 발사',
        'launcher_basic_special':'충전 뒤 한 번의 둥근 포 발사',
        'launcher_range_special':'레이저 꼬리를 자연스럽게 감쇠 · 짧게 자르지 않음',
        'launcher_move_special':'산탄총의 몸통을 살리고 날카로운 잡음 억제',
        'launcher_followup_special':'기존 뽕 유지 · 지지직 대신 낮고 작은 퍼퍼벙',
        'bomb_basic_review':'기본 투척 유지 · 영상 참고 작은 폭발 후보',
        'bomb_basic_special':'3번 투척과 기본 폭탄 계열 폭발',
        'bomb_range_special':'무거운 투척 뒤 둥근 큰 폭발',
        'bomb_followup_special':'착지 후 신관 1.5초 뒤 공통 큰 폭발',
        'bomb_move_special':'변신·비행 소리 없이 마지막 폭발만',
        'shuriken_basic_special':'전진 회전 수리검과 같은 큰 수리검 투척',
        'shuriken_range_special':'폭탄 기본공격 후보와 같은 폭발',
        'shuriken_move_special':'투척 → 생존한 적 적중 → 순간이동 → 5초 뒤 복귀',
        'shuriken_followup_special':'큰 관통 수리검과 같은 투척 · 초당 12~16.5회 회전 질감'}
    for aid,reason in reasons.items():
        a=actions[aid];a.update(candidateRevision=3,reviewReason=reason,note=reason);a.setdefault('scenario',{})
        activation=a['impact']
        if aid=='rapier_default':a['scenario']={}
        if aid=='dagger_range':a['scenario']={'sustain':dict(min=.3,max=5,default=3,actual=3,label='연막 유지 시간')}
        if aid in ['bomb_basic_review','bomb_basic_special']:a['scenario']={'impactDelay':dict(min=.2,max=3,default=1,label='투척 후 충돌 청취 시간')}
        if aid=='shuriken_move_special':
            a['scenario']={'impactDelay':dict(min=.05,max=.55,default=.25,label='투척 후 첫 적중 청취 시간')}
            a['previewDuration']=activation+.25+5+.7
            a['note']+=' · 적중이 수락되고 적이 살아남은 경우의 청취 예시입니다. 빗나가거나 적이 죽으면 순간이동하지 않습니다.'
        if aid=='bomb_move_special':a['note']+=' · 비행 중 무음. 청취용 비행 시간 끝에 폭발합니다.'
        if aid=='shield_movement':a['note']+=' · 게임 이동 0.3초 / 기본 청취 예시 3초.'
        if aid=='bomb_basic_review':a['note']+=' · 현재음 11번은 현재 투척과 현재 기본 폭발을 함께 재생합니다.'
        recipes=[]
        for i in range(10):
            prior=old.get(aid,{}).get('candidates',[None]*10)[i]
            if aid=='base_sword_follow_up':
                first=prior['layers'][0]['audio'];echo=transient(air,i,low=5500,high=450,db=-24,pitch=1.2,length=.20,release=.065)
                ls=[layer('slash','직접 베기',first,[a['markers'][0]]),layer('echo','짧은 잔상 베기',echo,[a['markers'][1]],.95)]
            elif aid=='whip_follow_up':
                pop=transient(body+small,i,low=2400,db=-22,length=.23,release=.10)
                ls=[layer('wrap','처음 감기',prior['layers'][0]['audio'],[a['markers'][0]]),layer('pop','매듭 · 팡',pop,[a['markers'][1]],.9)]
            elif aid=='rapier_default':
                release=copy.deepcopy(prior['layers'][1]);release.update(anchor='absolute',times=[activation])
                if i>=5:release['audio']=process(release['audio'],low=6500,pitch=1.035,db=-22)
                ls=[release]
            elif aid=='dagger_range':
                pop=normal[i]['audio'];bed=continuous(softwind,i,low=1100,high=150,db=-35)
                ls=[layer('burst','작은 연막탄 · 펑',pop,[activation],normal[i]['gain']*.8),layer('smoke','잔잔한 연막 유지',bed,[0],1,'activation','onset',loop=True,duration='sustain',fadeSeconds=.25)]
            elif aid=='shield_default':
                ring=process(old[aid]['candidates'][0]['layers'][0]['audio'],low=650+i*45,pitch=.48+i*.025,db=-25)
                thunk=transient(body,i,low=800+i*35,db=-23,pitch=.85,length=.32)
                key=mix([(ring,0,.18),(thunk,0,1)],'rounded parry from previous candidate 1',-23)
                ls=[layer('parry','받아치기 · 퉁',key,[0],.9,'impactDemo')]
            elif aid=='shield_range':
                key=continuous(humbank,i,low=450+i*20,db=-26,hum=True)
                ls=[layer('field','부드러운 보호막 · 웅',key,[0],1,'activation','onset',loop=True,duration='sustain',fadeSeconds=.20)]
            elif aid=='shield_movement':
                key=continuous(softwind,i,low=900+i*80,db=-24)
                ls=[layer('push','연속으로 밀어붙이는 바람',key,[0],1,'activation','onset',loop=True,duration='sustain',fadeSeconds=.15)]
            elif aid=='shield_follow_up':
                cast=copy.deepcopy(prior['layers'][0]);place=transient(body,i,low=900,db=-23,pitch=.8,length=.30)
                ls=[cast,layer('place','둔탁한 미끼 설치',place,[activation],.9)]
            elif aid=='greatsword_range':
                summon=transient(deep,i,low=2300,db=-25,length=.25);hit=transient(body+small,i,low=900,db=-20,pitch=.78,length=.45,release=.18)
                ls=[layer('summon','검 소환 · 낮은 바람',summon,[max(0,activation-.2)],.75,align='onset'),layer('impact','내려찍기 · 쿵',hit,[activation])]
            elif aid in ['launcher_basic_review','launcher_basic_special']:
                key=transient(cannon,i,low=1100+i*80,high=100 if aid.endswith('review') else 70,db=-21 if aid.endswith('review') else -19,pitch=1.2 if aid.endswith('review') else 1.04,length=.42,release=.17)
                ls=[] if aid.endswith('review') else [copy.deepcopy(prior['layers'][0])]
                ls.append(layer('release','짧은 포 발사',key,[0],.9,'release'))
            elif aid=='launcher_range_special':
                # Return to the source WAV: fading an already cropped 110ms clip cannot restore its tail.
                node=audio[prior['layers'][0]['audio']]['source']
                while isinstance(node,dict):node=node.get('origin',node.get('archiveEntry',node.get('processedSource')))
                while node in audio:node=audio[node]['source']
                assert node in names,node
                key=transient([node],i,low=3200+i*90,db=-24,length=1.2,release=.28,full=True)
                ls=[layer('laser','자연스럽게 잦아드는 레이저',key,[activation],.9)]
            elif aid=='launcher_move_special':
                key=transient(bank('Guns_Weapons/Guns/gun_shotgun_shot_')+bank('Guns_Weapons/Guns/gun_shotgun_sawed_off_shot_'),i,low=1600+i*70,db=-20,length=.50,release=.20)
                ls=[layer('shotgun','둥근 산탄총 발사',key,[activation],.95)]
            elif aid=='launcher_followup_special':
                launch=copy.deepcopy(prior['layers'][0]);pop=transient(small,i,low=950+i*40,db=-26,length=.27,release=.12)
                cluster=mix([(pop,0,.7),(pop,.13,.55),(pop,.27,.85)],'soft grenade triple blast',-27)
                ls=[launch,layer('explosion','낮고 작은 퍼퍼벙',cluster,[0],.8,'impactDemo','onset')]
            elif aid=='bomb_basic_review':
                throw=actions[aid]['current'];clip=audio[throw]
                ls=[layer('throw','확정 기본 투척',throw,[0],.8,align='onset',rate=actions[aid]['currentRate']),shared('explosion','기본 폭탄 폭발','normal_explosion',[0],i)]
            elif aid=='bomb_basic_special':
                throw=transient(deep,i,low=2400,db=-24,length=.25)
                ls=[layer('throw','폭탄 3연속 투척',throw,a['markers'],.8),shared('explosion','기본 폭탄 계열 폭발','normal_explosion',[t-activation for t in a['markers']],i)]
            elif aid in ['bomb_range_special','bomb_followup_special']:
                ls=[copy.deepcopy(prior['layers'][0]),shared('explosion','공통 큰 폭발','large_explosion',[1.5 if aid=='bomb_followup_special' else 0],i)]
            elif aid=='bomb_move_special':ls=[shared('explosion','마지막 큰 폭발만','large_explosion',[0],i,anchor='sustainEnd')]
            elif aid=='shuriken_basic_special':ls=[shared('throw','큰 수리검 투척','shuriken_throw',[activation],i,anchor='absolute')]
            elif aid=='shuriken_range_special':
                throw=copy.deepcopy(prior['layers'][0]);throw.pop('group',None);throw['name']='폭발 수리검 투척'
                ls=[throw,shared('explosion','기본 폭탄과 같은 폭발','normal_explosion',[0],i)]
            elif aid=='shuriken_move_special':
                hit=transient(body,i,low=1800,db=-27,length=.12,release=.05)
                ls=[copy.deepcopy(prior['layers'][1]),layer('hit','첫 적중',hit,[0],.6,'impactDemo'),shared('teleport','적중 직후 순간이동','teleport',[.03],i),shared('return','5초 뒤 원위치 복귀','teleport',[5],i)]
            elif aid=='shuriken_followup_special':
                spin=continuous(bank('Whooshes/fan_spin_whoosh_'),i,low=3800,high=250,db=-23,spin=True)
                ls=[shared('throw','큰 수리검 투척','shuriken_throw',[activation],i,anchor='absolute'),layer('spin','빠른 연속 회전',spin,[0],.9,'activation','onset',loop=True,duration='sustain',fadeSeconds=.10)]
            else:raise AssertionError(aid)
            recipes.append(dict(number=i+1,id=aid+f'-v3-{i+1:02}',name=f'{i+1:02} · '+' → '.join(l['name'] for l in ls),layers=ls,mixed=len(ls)>1,reason=reason))
        a['candidates']=recipes
    # Keep the real explosion available when comparing the unchanged current basic attack.
    actions['bomb_basic_review']['currentExtras']=[dict(audio=payload['basicExplosion'],gain=.8,anchor='impactDemo',times=[0])]
    payload.update(revision=3,reviewedActions=list(reasons),defaultWeapon='base_sword')

    refdir=source/'reference';refdir.mkdir(exist_ok=True);original=refdir/'user-reference.mp4'
    if reference_video:
        raw=Path(reference_video).read_bytes()
        if original.exists():assert original.read_bytes()==raw,'Do not overwrite a different reference'
        else:original.write_bytes(raw)
    if original.exists() and include_reference:
        executable=str(ffmpeg) if ffmpeg else 'ffmpeg'
        result=subprocess.run([executable,'-v','error','-i',str(original),'-vn','-ac','1','-ar',str(rate),'-f','f32le','pipe:1'],check=True,stdout=subprocess.PIPE)
        x=np.frombuffer(result.stdout,dtype='<f4').astype(float);gain=.6/max(abs(x[:14*rate]));clips=[]
        for start,end in [(0,.9),(2.2,3.1),(4.75,5.65),(6.4,7.4)]:
            y=tailfade(x[round(start*rate):round(end*rate)]*gain,.01,.04)
            key=register(y,{'userReference':original.name,'sha256':m.sha(original.read_bytes()),'startSeconds':start,'endSeconds':end,'containsBackground':True},dict(mode='reference-excerpt',gain=gain))
            clips.append(dict(audio=key,label=f'영상 {start:g}–{end:g}초'))
        payload['reference']={'file':'source/reference/user-reference.mp4','sha256':m.sha(original.read_bytes()),'clips':clips,'note':'배경음이 포함된 영상 발췌입니다. 작은 녹음 음량은 같은 배율로 보정했습니다. 후보 원음으로 사용하지 않습니다.'}
    z.close()
