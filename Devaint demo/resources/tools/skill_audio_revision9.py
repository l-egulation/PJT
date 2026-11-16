"""Fuller smoke, installation-triggered layered decoys and spreading ink blasts."""
import copy
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m
from skill_audio_revision7 import FAMILIES


def revise(p,archive,source):
    assert p['revision']==8
    rate=m.RATE; audio=p['audio']; groups=p['sharedGroups']; old=copy.deepcopy(p['actions'])
    old_groups=copy.deepcopy(groups); z=zipfile.ZipFile(archive)
    def pcm(key):return np.asarray(m.decode((source/'audio'/audio[key]['file']).read_bytes()))
    def filt(x,low,high):
        n=len(x); pad=rate//4; y=np.pad(x,(pad,pad)); f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+n]
    def rms(x):return np.sqrt(np.mean(x*x))+1e-12
    def read(entry,low=4800,high=150,speed=1):
        raw=z.read(entry); x=np.asarray(m.decode(raw))
        active=np.flatnonzero(abs(x)>max(abs(x))*.03)
        start=max(0,int(active[0])-round(.003*rate))
        x=x[start:]; x=np.interp(np.arange(0,len(x)-1,speed),np.arange(len(x)),x)
        return filt(x,low,high),dict(archiveEntry=entry,sha256=m.sha(raw),leadTrimSamples=start,rate=speed,lowpass=low,highpass=high)
    def envelope(x,tau,hold,attack=.003):
        t=np.arange(len(x))/rate
        return x*(1-np.exp(-t/attack))*np.exp(-np.maximum(0,t-hold)/tau)
    def body_level(x):
        # Normalize the body, not the single loudest impact. A 12 ms local
        # envelope gently controls isolated spikes without clipping samples.
        width=round(.012*rate); power=np.convolve(x*x,np.ones(width)/width,mode='same')
        level=np.sqrt(power+1e-10); active=level[level>max(level)*.15]
        pivot=np.percentile(active,65) if len(active) else .1
        gain=np.minimum(1,(pivot/np.maximum(level,1e-9))**.55)
        y=x*gain
        return y/rms(y[:min(len(y),round(.25*rate))])
    def mix(parts):
        y=np.zeros(max(len(x) for x,g in parts))
        for x,g in parts:y[:len(x)]+=x*g
        return y
    def finish(x,peak):
        y=x.copy(); y[:96]*=np.sin(np.linspace(0,np.pi/2,96))**2
        bins=[max(abs(y[j:j+480])) for j in range(0,len(y),480)]; threshold=max(abs(y))*10**(-66/20)
        for j in range(40,len(bins)-6):
            if max(bins[j:j+6])<threshold:y=y[:(j+6)*480];break
        n=min(1440,len(y)//4); y[-n:]*=np.cos(np.linspace(0,np.pi/2,n))**2
        return y*peak/max(abs(y))
    def reg(x,origin,edit):
        raw=m.wav_bytes(x);key='r9/'+m.sha(raw)[:20];file=key.replace('/','-')+'.wav'
        (source/'audio'/file).write_bytes(raw);y=m.decode(raw)
        audio[key]={**m.builder.audio_payload(raw,file),'gain':1,'peak':max(map(abs,y)),
            'peakTime':m.sliding_peak(y),'peakEnvelopeStep':.01,
            'peakEnvelope':[max(map(abs,y[j:j+480])) for j in range(0,len(y),480)],'source':origin,'edit':edit}
        return key
    for i in range(10):
        for family in FAMILIES:
            large=family=='large_explosion'; triple=family=='triple_explosion'
            entry=f'Explosion_Fire_Gas/explosion_large_no_tail_{i%6+1:02}.wav'
            x,origin=read(entry,low=4600,high=135 if large else 185,speed=1.12 if triple else 1 if large else 1.06)
            tau=.075 if triple else .16 if large else .115
            hold=.060 if triple else .12 if large else .095
            x=body_level(x); x=envelope(x,tau,hold,.004)
            # A continuous rounded air release broadens the explosion. No
            # oscillator thump, repeated impacts, echo, or discrete water drop.
            t=np.arange(round(1.6*rate))/rate
            air=filt(np.random.default_rng(9000+i).normal(size=len(t)),3300+70*i,400)
            air=air/rms(air);air=envelope(air,tau,hold,.009)
            y=finish(mix([(x,.65),(air,.65)]),.30 if triple else .44 if large else .36)
            key=reg(y,{'primary':origin,'procedural':'single filtered-air release; no tonal pulse or repeated impacts',
                       'previousCandidate':old_groups[family]['variants'][i]['audio']},
                    dict(mode='spreading-ink-explosion',family=family,bodyGain=.65,airGain=.65,
                         holdSeconds=hold,decaySeconds=tau,addedReverb=False))
            groups[family]['variants'][i]=dict(audio=key,gain=.72 if triple else .8,
                name=f'{"짧게 퍼지는" if triple else "크게 퍼지는" if large else "퍼지는"} 잉크 폭발 {i+1:02}')
    for family in FAMILIES:groups[family]['revision']=9
    preserved={}; changed=[]
    for a in p['actions']:
        bomb=any(l.get('group') in FAMILIES for r in a['candidates'] for l in r['layers'])
        if not bomb and a['id'] not in ('dagger_range','shield_follow_up'):continue
        changed.append(a['id']);a['candidateRevision']=9
        reason='짧은 타격보다 퍼지는 파열과 몸통을 강조' if bomb else '먹안개 파열의 여운 확대 · 유지음 보존' if a['id']=='dagger_range' else '설치 성공 시각 0.20초부터 접촉·잉크 정착음을 겹쳐 재생 · 시전음 없음'
        a['note']=a['reviewReason']=reason
        if a['id']=='shield_follow_up':
            assert a['impact']==.2
            a['installationEvent']={'time':.2,'runtimeHook':'SpawnDecoy success','auditionOnly':True,
                'source':'data/combat/melee_weapon_design.json + CastActivationExecution.cs','startsAtInstallation':True}
        for i,r in enumerate(a['candidates']):
            if a['id']=='shield_follow_up':
                oldkey=r['layers'][0]['audio']; contact=pcm(oldkey)
                ground,origin=read(f'Impacts_Smashable/rock_impact_heavy_slam_{i%4+1:02}.wav',low=3200,high=100)
                ground=envelope(body_level(ground),.065,.03)
                contact=contact/rms(contact[:4800])
                k=reg(finish(mix([(contact,.7),(ground,.18)]),.43),
                    {'processedSource':oldkey,'ground':origin},dict(mode='layered-decoy-contact',addedReverb=False))
                liquid,origin=read(f'Water/water_splash_object_body_{i%5+1:02}.wav',low=3000,high=180)
                liquid=envelope(body_level(liquid),.11,.06,.012)
                settle=reg(finish(liquid,.25),{'primary':origin},dict(mode='decoy-ink-settling',addedReverb=False))
                r['layers']=[dict(id='place',name='설치 순간 · 묵직한 접촉',audio=k,times=[0],gain=.9,anchor='activation',align='onset'),
                             dict(id='settle',name='설치 직후 · 잉크 정착',audio=settle,times=[.018],gain=.62,anchor='activation',align='onset')]
                r['mixed']=True
            else:
                for l in r['layers']:
                    if l.get('group') in FAMILIES:
                        v=groups[l['group']]['variants'][i];l.update(audio=v['audio'],gain=v['gain'],name='퍼져 터지는 잉크')
                    elif a['id']=='dagger_range' and l['id']=='burst':
                        oldkey=l['audio']; info=audio[oldkey]['source']['primary']
                        x,origin=read(info['archiveEntry'],low=4800,high=150,speed=info['rate'])
                        x=finish(envelope(x,.18,.10),.34)
                        k=reg(x,{'primary':origin,'previousCandidate':oldkey},dict(mode='fuller-smoke-grenade',holdSeconds=.10,decaySeconds=.18,addedReverb=False))
                        l.update(audio=k,name='여운 있는 연막탄 파열 · 펑')
                    else:preserved[f'{a["id"]}/{i+1}/{l["id"]}']=copy.deepcopy(l)
            r['id']=a['id']+f'-v9-{i+1:02}';r['reason']=reason;r['name']=f'{i+1:02} · '+' → '.join(l['name'] for l in r['layers'])
    p.update(revision=9,reviewedActions=changed,revision9PreservedLayers=preserved,
        revision9UnchangedActions={a['id']:a for a in old if a['id'] not in changed})
    z.close()


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,args.archive,source)
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json',dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset,'audio.gamemaster-skill-audition','revision 9: fuller smoke, layered installation, spreading explosions; star confirmations preserved; not applied')
    print('Updated smoke, installation-triggered decoy layers and 30 explosion variants; laser preserved.')
