"""Fixed single-event skills, restored triple throws, and clean ink/water bursts."""
import copy
import zipfile
import numpy as np
import render_basic_attack_audio as m


def revise(p,archive,source):
    audio=p['audio'];actions={a['id']:a for a in p['actions']};old=copy.deepcopy(actions);groups=p['sharedGroups'];rate=m.RATE
    z=zipfile.ZipFile(archive);names=z.namelist()
    def bank(prefix):
        found=sorted(n for n in names if n.startswith(prefix) and n.endswith('.wav'));assert found,prefix
        return found
    def pcm(key):return np.asarray(m.decode((source/'audio'/audio[key]['file']).read_bytes()))
    def filt(x,low,high=35):
        size=len(x);pad=rate//5;x=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(x),1/rate)
        shape=(1+(f/low)**8)**-.5
        if high:shape*=(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(x)*shape,n=len(x))[pad:pad+size]
    def fade(x,attack=.008,tail=.08):
        x=x.copy();a=min(len(x)//3,round(attack*rate));b=min(len(x)//2,round(tail*rate))
        x[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2;x[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
        return x
    def reg(x,origin,edit,db=None):
        if db is not None:x=m.level(x.tolist(),db)
        raw=m.wav_bytes(x);key='r5/'+m.sha(raw)[:20];file=key.replace('/','-')+'.wav';(source/'audio'/file).write_bytes(raw);out=m.decode(raw)
        audio[key]={**m.builder.audio_payload(raw,file),'peak':max(map(abs,out)),'peakTime':m.sliding_peak(out),'gain':1,'source':origin,'edit':dict(edit,activeRmsDb=db)}
        return key
    def take(entries,i,seconds,low,high=40,db=-24):
        entry=entries[i%len(entries)];raw=z.read(entry);x=np.asarray(m.decode(raw));x=filt(x,low,high)
        active=np.flatnonzero(abs(x)>max(abs(x))*.012);at=max(0,int(active[0])-round(.006*rate));x=x[at:at+round(seconds*rate)]
        # Remove isolated sample spikes without saturating or distorting the waveform.
        padded=np.pad(x,(2,2),mode='edge');x=np.median(np.lib.stride_tricks.sliding_window_view(padded,5),axis=1)
        return reg(fade(x),{'archiveEntry':entry,'sha256':m.sha(raw)},dict(mode='clean-source-body',lowpass=low,highpass=high,seconds=seconds),db)
    def pressure(i,seconds=.23,base=110,air=.13,db=-23):
        t=np.arange(round(seconds*rate))/rate;u=t/seconds
        hz=base*(1.45-.65*(1-np.exp(-u*8)));phase=2*np.pi*np.cumsum(hz)/rate
        x=(np.sin(phase)+.16*np.sin(phase*2))*np.exp(-u*5)
        noise=filt(np.random.default_rng(5100+i).normal(size=len(t)),1400,220);noise/=max(np.std(noise),1e-9)
        x+=air*noise*np.exp(-u*13)
        return reg(fade(x,.009,.085),{'procedural':'smooth pressure pulse with brief band-limited air','userRecordingUsed':False},dict(mode='clean-pressure',seconds=seconds,baseHz=base,airGain=air),db)
    def mix(parts,label,db):
        n=max(round(t*rate)+len(pcm(k)) for k,t,g in parts);x=np.zeros(n)
        for key,t,g in parts:
            at=round(t*rate);v=pcm(key);x[at:at+len(v)]+=v*g
        return reg(fade(x),{'composite':label,'parts':[dict(audio=k,onset=t,gain=g) for k,t,g in parts]},dict(mode=label),db)
    def layer(id,name,key,times,gain=1,anchor='absolute',align='peak',**kw):return dict(id=id,name=name,audio=key,times=times,gain=gain,anchor=anchor,align=align,**kw)
    def shared(id,name,group,i,times,anchor='impactDemo'):
        v=groups[group]['variants'][i];return layer(id,name,v['audio'],times,v['gain'],anchor,'onset',group=group)

    # Revision 02 left this skill's v1 throw recipe unchanged. Resolve the exact
    # original cuts by provenance instead of trying to undo later processing.
    def original_cut(entry,tail):
        # Several tail requests can produce the same short WAV. The last metadata
        # write may therefore show another tail; the content hash is authoritative.
        samples=m.crop_transient(m.decode(z.read(entry)),after=tail,before=.045,lowpass=6500)
        raw=m.wav_bytes(m.level(samples,-22));key='cut/'+m.sha(raw)[:20];assert key in audio,(entry,tail)
        return key
    bomb_selection=next(w for w in p['weapons'] if w['id']=='bomb')['selection']
    first=original_cut(bomb_selection['archiveEntry'],.3)
    mains=list(dict.fromkeys([first]+[original_cut(f'Whooshes/whoosh_swish_high_big_{i:02}.wav',.32) for i in range(1,5)]))
    for speed in [.88,1.12]:
        if len(mains)>=5:break
        x=pcm(first).tolist();shifted=[]
        for j in range(int((len(x)-1)/speed)):
            at=j*speed;n=int(at);fraction=at-n;shifted.append(x[n]*(1-fraction)+x[n+1]*fraction)
        key='cut/'+m.sha(m.wav_bytes(m.level(shifted,-22)))[:20];assert key in audio;mains.append(key)
    assert len(mains)==5
    detail=[original_cut(f'Whooshes/whoosh_swish_high_fast_{i:02}.wav',.24) for i in range(1,5)]
    restored=[]
    for i in range(10):
        ls=[dict(id='main',name='02 시안 · 3연속 투척',audio=mains[i%5],times=actions['bomb_basic_special']['markers'],gain=1,offsetMs=0)]
        if i>=5:ls.append(dict(id='detail',name='02 시안 · 보조 움직임',audio=detail[(i-5)%4],times=[actions['bomb_basic_special']['impact']],gain=.42,offsetMs=0))
        restored.append(ls)
    old_normal=copy.deepcopy(groups['normal_explosion']['variants'])
    triples=[];normal=[];large=[]
    water=bank('Water/water_splash_small_item_');drops=bank('Water/water_drop_drip_single_')
    for i in range(10):
        v=old_normal[i];x=pcm(v['audio']);n=min(len(x),round((.20+.006*i)*rate));x=x[:n]
        x=fade(x,.005,.095)
        key=reg(x,{'processedSource':v['audio'],'sha256':audio[v['audio']]['sha256']},dict(mode='short-current-explosion',seconds=n/rate,tailFadeSeconds=.095))
        triples.append(dict(audio=key,gain=v['gain'],name=f'짧은 기존 폭발 {i+1:02}'))
        splash=take(water,i,.30+.008*i,3200,120,-25)
        droplet=take(drops,i+3,.17,2400,180,-29)
        bubble=pressure(i,.19,base=210+i*9,air=.045,db=-24)
        key=mix([(bubble,0,1),(splash,.018,.55),(droplet,.06,.22)],'ink-water-balloon-burst',-28)
        normal.append(dict(audio=key,gain=.8,name=f'잉크 물풍선 {i+1:02}'))
        body=pressure(100+i,.36,base=100+i*2,air=.04,db=-27)
        x=pcm(key);x=np.interp(np.arange(0,len(x)-1,.88),np.arange(len(x)),x)
        stretch=reg(x,{'processedSource':key,'sha256':audio[key]['sha256']},dict(mode='larger-liquid-body',rate=.88))
        big=mix([(stretch,0,1),(body,.012,.4)],'large-ink-water-burst',-25)
        large.append(dict(audio=big,gain=.8,name=f'큰 잉크 물풍선 {i+1:02}'))
    groups['triple_explosion']={'label':'3연속 전용 짧은 폭발','scope':'기존 폭발의 짧고 부드러운 꼬리','revision':5,'variants':triples}
    groups['normal_explosion']={'label':'잉크 폭탄 공통 파열','scope':'폭탄 기본공격 새 후보 · 폭발 수리검','revision':5,'variants':normal}
    groups['large_explosion']={'label':'큰 잉크 폭탄 공통 파열','scope':'대형 폭탄 · 착지 시한폭탄 · 미사일','revision':5,'variants':large}
    reasons={
      'base_sword_follow_up':'강베기 선택과 소리·음량·개별 편집 공유 · 잔상 후속음 없음',
      'whip_follow_up':'04 시안 1번 감기 한 가지로 고정 · 후속음 없음',
      'bomb_basic_special':'02 시안 투척 복원 · 기존 폭발만 짧고 부드럽게',
      'dagger_range':'거친 폭발 원음 없이 깨끗한 짧은 펑',
      'shield_follow_up':'시전음 없이 묵직한 설치 한 번',
      'launcher_range_special':'낮은 몸통이 있는 두꺼운 레이저 · 폭발·지직거림 없음',
      'launcher_move_special':'산탄총 몸통과 매끈한 압력음 · 거친 꼬리 제거',
      'launcher_basic_review':'짧고 깨끗한 발사 · 거친 고역·꼬리 제거',
      'bomb_basic_review':'물풍선처럼 짧게 터지는 잉크 폭탄',
      'shuriken_range_special':'폭탄 기본공격 후보와 같은 잉크 파열',
      'bomb_range_special':'무거운 투척 뒤 큰 잉크 물풍선 파열',
      'bomb_followup_special':'착지 후 1.5초 뒤 큰 잉크 물풍선 파열',
      'bomb_move_special':'비행 중 무음 · 마지막 큰 잉크 파열만'}
    for id in list(p.get('frozenLayers',{})):
        if id.split('/')[0] in reasons:del p['frozenLayers'][id]
    for aid,reason in reasons.items():
        a=actions[aid];a.update(candidateRevision=5,reviewReason=reason,note=reason)
        if aid=='base_sword_follow_up':a.update(linkedAction='base_sword_default',fixedCandidate=True)
        if aid=='whip_follow_up':a.update(fixedCandidate=True,sourceFixedCandidate=1)
        recipes=[]
        for i,prior in enumerate(old[aid]['candidates']):
            at=a['impact']
            if aid=='base_sword_follow_up':
                strong=actions['base_sword_default']['candidates'][i];ls=copy.deepcopy(strong['layers'])
                for l in ls:l['times']=[t-actions['base_sword_default']['impact']+at for t in l['times']]
            elif aid=='whip_follow_up':ls=[copy.deepcopy(old[aid]['candidates'][0]['layers'][0])]
            elif aid=='bomb_basic_special':ls=copy.deepcopy(restored[i])+[shared('explosion','짧은 기존 폭발','triple_explosion',i,[t-at for t in a['markers']])]
            elif aid=='dagger_range':
                pop=pressure(200+i,.21+.008*i,base=120+4*i,air=.11,db=-22)
                ls=[layer('burst','깨끗한 연막탄 · 펑',pop,[at]),copy.deepcopy(prior['layers'][1])]
            elif aid=='shield_follow_up':
                rock=take(bank('Impacts_Smashable/rock_impact_heavy_slam_'),i,.34,1500,40,-23)
                weight=pressure(300+i,.30,base=90+i*2,air=.025,db=-23)
                key=mix([(rock,0,.65),(weight,0,.7)],'heavy-decoy-placement',-19)
                ls=[layer('place','묵직한 미끼 설치',key,[at],.9)]
            elif aid=='launcher_range_special':
                seconds=.46+.018*i;t=np.arange(round(seconds*rate))/rate;u=t/seconds;freq=(230+10*i)+(850+45*i)*np.exp(-u*3.8)
                phase=2*np.pi*np.cumsum(freq)/rate;x=(.7*np.sin(phase)+.8*np.sin(phase/2+.18)+.10*np.sin(phase*2))*np.exp(-u*1.65)
                key=reg(fade(x,.025,.20),{'procedural':'rounded two-octave phase-continuous laser','userRecordingUsed':False},dict(mode='weighted-laser',seconds=seconds,subOctaveGain=.8,attackSeconds=.025,tailFadeSeconds=.20),-18)
                ls=[layer('laser','두꺼운 레이저',key,[at],1,align='onset')]
            elif aid in ['launcher_move_special','launcher_basic_review']:
                shotgun=aid=='launcher_move_special';entries=bank('Guns_Weapons/Guns/gun_shotgun_shot_') if shotgun else bank('Guns_Weapons/Guns/weapon_cannon_shot_')
                raw=take(entries,i,.20 if shotgun else .16,1100 if shotgun else 1000,100,-27)
                body=pressure(400+i,.28 if shotgun else .20,base=(95 if shotgun else 145)+i*3,air=.12 if shotgun else .055,db=-21)
                key=mix([(body,0,1),(raw,.004,.35 if shotgun else .22)],'clean-shotgun' if shotgun else 'clean-launcher',-19 if shotgun else -22)
                ls=[layer('shotgun' if shotgun else 'release','깨끗한 산탄 발사' if shotgun else '깨끗한 기본 발사',key,[at],.95)]
            else:
                ls=copy.deepcopy(prior['layers'])
                for l in ls:
                    if l.get('group') in ['normal_explosion','large_explosion']:
                        v=groups[l['group']]['variants'][i];l.update(audio=v['audio'],gain=v['gain'],name='잉크 물풍선 파열')
            recipes.append(dict(number=i+1,id=aid+f'-v5-{i+1:02}',name=('고정 · 1번 감기만' if aid=='whip_follow_up' else '강베기 선택 공유' if aid=='base_sword_follow_up' else f'{i+1:02} · '+' → '.join(l['name'] for l in ls)),layers=ls,mixed=len(ls)>1,reason=reason))
        a['candidates']=recipes
    p.update(revision=5,reviewedActions=list(reasons),restoredTripleThrowLayers=restored,fixedWhipLayer=copy.deepcopy(old['whip_follow_up']['candidates'][0]['layers'][0]),referencePolicy='References describe a direction only. No user recording or other game audio is sampled.')
    z.close()
