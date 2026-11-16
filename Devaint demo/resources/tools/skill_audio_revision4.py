"""Focused fourth review. No user recording is used in any candidate.

Keeps accepted stems byte-for-byte, removes rejected layers, and uses a
phase-continuous synthesized laser to avoid a blast embedded in a source WAV.
"""
import copy
import zipfile
import numpy as np
import render_basic_attack_audio as m


def revise(payload,archive_path,source):
    audio=payload['audio'];actions={a['id']:a for a in payload['actions']};rate=m.RATE
    old=copy.deepcopy(actions);z=zipfile.ZipFile(archive_path);names=z.namelist()
    def bank(prefix):
        result=sorted(n for n in names if n.startswith(prefix) and n.endswith('.wav'))
        assert result,prefix
        return result
    def read(key):return np.asarray(m.decode((source/'audio'/audio[key]['file']).read_bytes()))
    def register(x,origin,edit,db):
        x=m.level(x.tolist(),db);raw=m.wav_bytes(x);key='r4/'+m.sha(raw)[:20];file=key.replace('/','-')+'.wav'
        (source/'audio'/file).write_bytes(raw);x=m.decode(raw)
        audio[key]={**m.builder.audio_payload(raw,file),'peak':max(map(abs,x)),'peakTime':m.sliding_peak(x),'gain':1,'source':origin,'edit':dict(edit,activeRmsDb=db)}
        return key
    def filter(x,low,high=40):
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        curve=1/np.sqrt(1+(f/low)**8)
        if high:curve*=1/np.sqrt(1+(high/np.maximum(f,.001))**8)
        return np.fft.irfft(np.fft.rfft(y)*curve,n=len(y))[pad:pad+n]
    def fade(x,attack=.01,release=.10):
        x=x.copy();a=min(len(x)//3,round(attack*rate));b=min(len(x)//2,round(release*rate))
        x[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2;x[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
        return x
    def source_sound(entries,i,seconds=.35,low=4500,high=50,pitch=1,attack=.008,release=.12,db=-21,slice=False):
        entry=entries[i%len(entries)];raw=z.read(entry);x=np.asarray(m.decode(raw))
        x=filter(x,low,high)
        # Find the useful band AFTER filtering; low knocks cannot select a slicing landmark.
        if slice:
            width=round(.06*rate);energy=np.convolve(x*x,np.ones(1),'same')
            summed=np.concatenate(([0.],np.cumsum(energy)));energies=summed[width:]-summed[:-width]
            at=int(np.argmax(energies));start=max(0,at-round(.025*rate))
        else:
            threshold=max(abs(x))*.015;active=np.flatnonzero(abs(x)>threshold);start=max(0,int(active[0])-round(.006*rate))
        x=x[start:start+round(seconds*pitch*rate)];x=np.interp(np.arange(0,len(x)-1,pitch),np.arange(len(x)),x)
        if slice:
            # A continuous broad-band swish, rather than one low-frequency impact.
            n=len(x);width=min(n,round(.03*rate));kernel=np.ones(width)/width
            env=np.sqrt(np.maximum(1e-12,np.convolve(x*x,kernel,'same')))
            x=x/np.maximum(env,np.max(env)*.18)
            t=np.linspace(0,1,n);x*=np.sin(np.pi*t)**1.4
        return register(fade(x,attack,release),{'archiveEntry':entry,'sha256':m.sha(raw)},dict(mode='slicing-air' if slice else 'air-burst',lowpass=low,highpass=high,pitch=pitch,seconds=seconds),db)
    def layer(id,name,key,times,gain=1,anchor='absolute',align='peak',**kwargs):return dict(id=id,name=name,audio=key,times=times,gain=gain,anchor=anchor,align=align,**kwargs)
    def short_warm(key,i,push=False):
        x=read(key);seconds=(.32 if push else .48)+.014*i;n=round(seconds*rate)
        indices=np.cumsum(np.linspace(1.22,.87,n));y=np.interp(indices%len(x),np.arange(len(x)),x)
        y=filter(y,650 if push else 420,45);t=np.linspace(0,1,n)
        y*=np.sin(np.pi*t)**1.8
        return register(fade(y,.025,.10),{'processedSource':key,'sha256':audio[key]['sha256']},dict(mode='short-resonance',seconds=seconds,continuousSustain=False),-23 if push else -25)
    cloth=bank('Foley/foley_cloth_light_fast_movement_')
    pops=bank('Explosion_Fire_Gas/explosion_small_no_tail_')+bank('Explosion_Fire_Gas/explosion_small_short_')+bank('Explosion_Fire_Gas/explosion_small_0')
    earth=bank('Impacts_Smashable/rock_impact_heavy_slam_')
    rumble=bank('Impacts_Smashable/rock_earthquake_impact_')
    reasons={
      'base_sword_follow_up':'직접 베기 고정 · 후속은 금속·타격음 없는 짧은 싹',
      'whip_follow_up':'감기 고정 · 후속은 한 번의 공기 파열 팡',
      'dagger_range':'들리는 첫 펑 · 아주 낮고 잔잔한 연막 유지',
      'shield_range':'처음 생길 때 짧은 우웅만 · 유지음 없음',
      'shield_movement':'짧은 저음 웅만 · 마찰·지속음 없음',
      'shield_follow_up':'시전음 제거 · 기존 설치음만',
      'greatsword_range':'소환 고정 · 범위에 맞춘 두꺼운 지면 충격과 저음 꼬리',
      'launcher_basic_special':'충전 중 무음 · 기존 발사음 그대로',
      'launcher_range_special':'폭발 성분 없는 삐용 · 자연스러운 꼬리와 더 큰 음량',
      'shuriken_followup_special':'투척 고정 · 02 시안의 회전 소리를 12%만 빠르게'}
    frozen={}
    for aid,reason in reasons.items():
        a=actions[aid];a.update(candidateRevision=4,reviewReason=reason,note=reason)
        if aid in ['shield_range','shield_movement']:a['scenario']={}
        recipes=[]
        for i,prior in enumerate(old[aid]['candidates']):
            activation=a['impact'];ls=[]
            if aid=='base_sword_follow_up':
                slash=copy.deepcopy(prior['layers'][0]);key=source_sound(cloth,i+3,seconds=.20+.008*(i%4),low=7800,high=700,pitch=1.25,attack=.012,release=.045,db=-21,slice=True)
                ls=[slash,layer('echo','잔상 · 싹',key,[a['markers'][1]],.95)]
            elif aid=='whip_follow_up':
                wrap=copy.deepcopy(prior['layers'][0]);key=source_sound(pops,i,seconds=.25+.012*i,low=4000,high=80,pitch=1+.018*i,db=-20)
                ls=[wrap,layer('pop','매듭 · 팡',key,[a['markers'][1]],.95)]
            elif aid=='dagger_range':
                burst=source_sound(pops,i,seconds=.30,low=3200,high=80,pitch=1.15,db=-22)
                # Use a recorded long room/air bed, not synthesized broadband static.
                entries=bank('Backgrounds/background_hum_air_fan_device_loop_')+bank('Backgrounds/background_room_interior_hum_loop_')
                entry=entries[i%len(entries)];raw=z.read(entry);x=np.asarray(m.decode(raw));x=filter(x,300+i*10,65)
                n=min(len(x),4*rate);start=min(max(0,len(x)-n),i*rate//5);x=x[start:start+n]
                seam=min(rate//3,len(x)//5);t=np.linspace(0,1,seam);x[:seam]=x[-seam:]*(1-t)+x[:seam]*t;x=x[:-seam]
                bed=register(x,{'archiveEntry':entry,'sha256':m.sha(raw)},dict(mode='quiet-recorded-bed',lowpass=300+i*10,highpass=65,seamSeconds=seam/rate),-40)
                ls=[layer('burst','작은 연막탄 · 펑',burst,[activation]),layer('smoke','낮고 잔잔한 연막',bed,[.06],1,'activation','onset',loop=True,duration='sustain',fadeSeconds=.4)]
                a['note']+=' ' if i==0 else ''
            elif aid in ['shield_range','shield_movement']:
                base=old['shield_range']['candidates'][i]['layers'][0]['audio'];key=short_warm(base,i,push=aid=='shield_movement')
                ls=[layer('deploy' if aid=='shield_range' else 'push','짧은 우웅' if aid=='shield_range' else '짧은 방패 밀기 · 웅',key,[activation],1,align='onset')]
            elif aid=='shield_follow_up':ls=[copy.deepcopy(prior['layers'][1])]
            elif aid=='greatsword_range':
                summon=copy.deepcopy(prior['layers'][0]);impact=source_sound(earth,i,seconds=.62,low=2400,high=45,pitch=.9,db=-17)
                tail=source_sound(rumble,i,seconds=.8,low=700,high=35,pitch=.9,attack=.025,release=.25,db=-23)
                one=read(impact);two=read(tail);x=np.zeros(max(len(one),len(two)+round(.035*rate)));x[:len(one)]+=one;x[round(.035*rate):round(.035*rate)+len(two)]+=two*.65
                key=register(fade(x,.006,.18),{'composite':'wide ground impact','parts':[dict(audio=impact,onset=0,gain=1),dict(audio=tail,onset=.035,gain=.65)]},dict(mode='wide-ground-impact',noMetal=True),-16)
                ls=[summon,layer('impact','넓고 묵직한 쿵',key,[activation],1)]
            elif aid=='launcher_basic_special':ls=[copy.deepcopy(prior['layers'][1])]
            elif aid=='launcher_range_special':
                seconds=.38+.025*(i%5);t=np.arange(round(seconds*rate))/rate;u=t/seconds
                start=1300+95*i;end=330+22*(i%5);frequency=end+(start-end)*np.exp(-u*(3.2+.15*(i%3)))
                phase=2*np.pi*np.cumsum(frequency)/rate
                x=np.sin(phase)+.12*np.sin(2*phase+.2);x*=np.exp(-1.4*u)
                x=fade(x,.022,.17)
                key=register(x,{'procedural':'phase-continuous descending sine laser','userRecordingUsed':False},dict(mode='pure-laser-glide',startHz=start,endHz=end,seconds=seconds,harmonicGain=.12,attackSeconds=.022,tailFadeSeconds=.17),-17)
                ls=[layer('laser','선명한 삐용',key,[activation],1,align='onset')]
            elif aid=='shuriken_followup_special':
                throw=copy.deepcopy(prior['layers'][0]);pitch=1.2*(1+.055*(i//4));entry=f'Whooshes/fan_spin_whoosh_{i%4+1:02}.wav'
                key=next(k for k,v in audio.items() if k.startswith('r2/') and v.get('source',{}).get('archiveEntry')==entry and v.get('edit',{}).get('mode')=='loop' and v['edit'].get('length')==.4 and abs(v['edit']['pitch']-pitch)<1e-8)
                ls=[throw,layer('spin','02 회전음 · 1.12배',key,[0],.9,'activation','onset',loop=True,duration='sustain',rate=1.12,fadeSeconds=.12,previousRevision=2)]
            recipes.append(dict(number=i+1,id=aid+f'-v4-{i+1:02}',name=f'{i+1:02} · '+' → '.join(l['name'] for l in ls),layers=ls,mixed=len(ls)>1,reason=reason))
            # Machine-checkable preservation contract for the specifically retained parts.
            for part in ls:
                if (aid,part['id']) in [('whip_follow_up','wrap'),('base_sword_follow_up','slash'),('greatsword_range','summon'),('launcher_basic_special','release'),('shield_follow_up','place'),('shuriken_followup_special','throw')]:
                    original=next(l for l in prior['layers'] if l['id']==part['id']);assert part==original
                    frozen[f'{aid}/{i+1}/{part["id"]}']=dict(layer=original,sha256=audio[part['audio']]['sha256'])
        a['candidates']=recipes
    # Recordings remain archived on disk, but are not embedded, playable or selectable.
    payload.pop('reference',None)
    for key in list(audio):
        if audio[key].get('edit',{}).get('mode')=='reference-excerpt':del audio[key]
    payload.update(revision=4,reviewedActions=list(reasons),frozenLayers=frozen,referencePolicy='User recording is analysis-only; no recorded samples in candidates or playback UI.')
    z.close()
