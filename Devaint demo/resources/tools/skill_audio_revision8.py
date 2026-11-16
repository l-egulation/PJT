"""Recorded bursts, a dry decoy impact, and brighter existing laser candidates."""
import copy
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m
from skill_audio_revision7 import FAMILIES


def revise(p,archive,source):
    assert p['revision']==7
    audio=p['audio']; groups=p['sharedGroups']; rate=m.RATE
    old_actions=copy.deepcopy(p['actions']); old_groups=copy.deepcopy(groups)
    contracts={}; preserved={}; z=zipfile.ZipFile(archive)

    def pcm(key): return np.asarray(m.decode((source/'audio'/audio[key]['file']).read_bytes()))

    def filt(x,low,high):
        n=len(x); pad=rate//4; y=np.pad(x,(pad,pad)); f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+n]

    def unit(x): return x/max(np.max(abs(x)),1e-12)

    def raw_hit(entry,low=5200,high=75,speed=1):
        raw=z.read(entry); x=np.asarray(m.decode(raw))
        # Locate the first direct transient, allowing a short lead into it.
        at=max(0,round((m.sliding_peak(x[:round(.22*rate)].tolist())-.010)*rate))
        x=x[at:]; x=np.interp(np.arange(0,len(x)-1,speed),np.arange(len(x)),x)
        return filt(x,low,high),dict(archiveEntry=entry,sha256=m.sha(raw),leadTrimSamples=at,rate=speed,lowpass=low,highpass=high)

    def decay(x,tau,hold=.035):
        t=np.arange(len(x))/rate
        return x*np.exp(-np.maximum(0,t-hold)/tau)

    def combine(parts):
        n=max(len(x) for x,g in parts); y=np.zeros(n)
        for x,g in parts: y[:len(x)]+=unit(x)*g
        return y

    def finish(y,target):
        y=y.copy(); y[:48]*=np.sin(np.linspace(0,np.pi/2,48))**2
        peak=max(abs(y)); step=480
        bins=[max(abs(y[j:j+step])) for j in range(0,len(y),step)]
        end=len(y)
        for j in range(25,len(bins)-6):
            if max(bins[j:j+6])<peak*10**(-66/20): end=(j+6)*step; break
        y=y[:end]; n=min(1200,len(y)//5)
        y[-n:]*=np.cos(np.linspace(0,np.pi/2,n))**2
        return y*target/max(abs(y))

    def reg(y,origin,edit):
        raw=m.wav_bytes(y); key='r8/'+m.sha(raw)[:20]; file=key.replace('/','-')+'.wav'
        (source/'audio'/file).write_bytes(raw); x=m.decode(raw)
        audio[key]={**m.builder.audio_payload(raw,file),'gain':1,'peak':max(map(abs,x)),
            'peakTime':m.sliding_peak(x),'peakEnvelopeStep':.01,
            'peakEnvelope':[max(map(abs,x[j:j+480])) for j in range(0,len(x),480)],
            'source':origin,'edit':edit}
        return key

    # These recordings contain an actual broadband explosive release. A tonal
    # pressure pulse or isolated water plop must not carry the explosion again.
    explosions=([f'Explosion_Fire_Gas/explosion_small_{i:02}.wav' for i in range(1,5)]
        +[f'Explosion_Fire_Gas/explosion_small_no_tail_{i:02}.wav' for i in range(1,5)]
        +[f'Explosion_Fire_Gas/explosion_small_short_{i:02}.wav' for i in range(1,3)])
    for i in range(10):
        for family in FAMILIES:
            large=family=='large_explosion'; triple=family=='triple_explosion'
            blast,origin=raw_hit(explosions[i],low=4800 if large else 5200,
                                 high=65 if large else 95,speed=.90 if large else 1.08 if triple else 1)
            tau=.065 if triple else .14 if large else .10
            blast=decay(blast,tau,.035 if triple else .055)
            liquid,wet_origin=raw_hit(f'Water/water_splash_object_body_{i%5+1:02}.wav',low=4200,high=180)
            liquid=decay(liquid,.065,.015)
            y=combine([(blast,1),(liquid,.12 if triple else .19)])
            y=finish(y,.30 if triple else .44 if large else .36)
            key=reg(y,{'primary':origin,'liquidTexture':wet_origin,
                       'previousCandidate':old_groups[family]['variants'][i]['audio']},
                    dict(mode='recorded-ink-explosion',family=family,primaryGain=1,
                         liquidGain=.12 if triple else .19,decaySeconds=tau,
                         addedReverb=False,tonalPressurePulse=False))
            groups[family]['variants'][i]=dict(audio=key,gain=.72 if triple else .8,
                name=f'{"짧은" if triple else "큰" if large else "잉크"} 폭발 · 펑 {i+1:02}')
    for family in FAMILIES: groups[family]['revision']=8

    changed=[]
    for a,old in zip(p['actions'],old_actions):
        bomb=any(l.get('group') in FAMILIES for r in a['candidates'] for l in r['layers'])
        if not bomb and a['id'] not in ('dagger_range','shield_follow_up','launcher_range_special'): continue
        changed.append(a['id']); a['candidateRevision']=8
        reason=('실제 폭발의 펑 + 작은 잉크 질감 · 투척과 사건 시각 유지' if bomb else
            '낮은 툭 대신 짧은 실제 연막 파열 · 유지음 보존' if a['id']=='dagger_range' else
            '가까운 미끼 설치 타격 · 시전음과 금속음 없음' if a['id']=='shield_follow_up' else
            '기존 레이저의 두께와 길이 유지 · 밝은 대역 강조')
        a['note']=reason; a['reviewReason']=reason
        for i,r in enumerate(a['candidates']):
            for l in r['layers']:
                tag=f'{a["id"]}/{i+1}/{l["id"]}'
                contract={k:copy.deepcopy(v) for k,v in l.items() if k not in ('audio','gain','name')}
                oldkey=l['audio']; key=None
                if l.get('group') in FAMILIES:
                    v=groups[l['group']]['variants'][i]; key=v['audio']; l['gain']=v['gain']; l['name']='잉크 폭발 · 펑'
                elif a['id']=='dagger_range' and l['id']=='burst':
                    x,origin=raw_hit(explosions[(i+8)%10],low=4800,high=150,speed=1.12+(i%3)*.04)
                    x=finish(decay(x,.068,.032),.34)
                    key=reg(x,{'primary':origin,'previousCandidate':oldkey},
                        dict(mode='smoke-grenade-burst',addedReverb=False,tonalPressurePulse=False))
                    l['name']='짧은 연막탄 파열 · 펑'
                elif a['id']=='shield_follow_up' and l['id']=='place':
                    x,origin=raw_hit(f'Miscellaneous/Object Deep Thud/impact_deep_thud_bounce_{i+1:02}.wav',low=4400,high=55)
                    # A single direct thud. Suppress secondary bounces and room tail.
                    x=finish(decay(x,.048,.025),.48)
                    key=reg(x,{'primary':origin,'previousCandidate':oldkey},
                        dict(mode='close-decoy-placement',addedReverb=False,secondaryBounceDecaySeconds=.048))
                    l['name']='가까운 미끼 설치 · 묵직한 타격'
                elif a['id']=='launcher_range_special' and l['id']=='laser':
                    x=pcm(oldkey); n=len(x); pad=rate//4
                    y=np.pad(x,(pad,pad)); f=np.fft.rfftfreq(len(y),1/rate)
                    # Equalize the existing waveform; retain pitch trajectory,
                    # duration and low octave rather than replacing the laser.
                    shelf=1+1.2*(f/650)**4/(1+(f/650)**4)
                    y=np.fft.irfft(np.fft.rfft(y)*shelf,n=len(y))[pad:pad+n]
                    y[:192]*=np.sin(np.linspace(0,np.pi/2,192))**2
                    y[-960:]*=np.cos(np.linspace(0,np.pi/2,960))**2
                    y*=np.sqrt(np.sum(x*x)/np.sum(y*y))
                    assert max(abs(y))<.9
                    key=reg(y,{'processedSource':oldkey,'sha256':audio[oldkey]['sha256']},
                        dict(mode='brighten-existing-laser',shelfHz=650,shelfDb=20*np.log10(2.2),
                             retainedDuration=True,retainedPitchTrajectory=True,addedAttack=False))
                    l['name']='밝고 두께 있는 레이저'
                if key:
                    l['audio']=key; contracts[tag]=contract
                else: preserved[tag]=copy.deepcopy(l)
            r['id']=a['id']+f'-v8-{i+1:02}'; r['reason']=reason
            r['name']=f'{i+1:02} · '+' → '.join(l['name'] for l in r['layers'])
    p.update(revision=8,reviewedActions=changed,revision8EventContracts=contracts,
        revision8PreservedLayers=preserved,
        revision8UnchangedActions={a['id']:a for a in old_actions if a['id'] not in changed})
    z.close()


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--archive',required=True)
    args=parser.parse_args(); asset=m.RES/'audio/gamemaster-skill-audition'; source=asset/'source'; page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,args.archive,source)
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json',dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset,'audio.gamemaster-skill-audition','revision 8: recorded bursts, close decoy, brighter laser; star confirmations preserved; not applied')
    print('Updated 60 audio candidates across 9 review items; timing and other sounds preserved.')
