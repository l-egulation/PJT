"""Water-balloon materials only for bombs; soften the existing longer smoke pop."""
import copy
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m
from skill_audio_revision7 import FAMILIES


def revise(p,archive,source):
    assert p['revision']==9
    rate=m.RATE; audio=p['audio'];groups=p['sharedGroups'];old=copy.deepcopy(p['actions'])
    before=copy.deepcopy(groups);z=zipfile.ZipFile(archive)
    def pcm(k):return np.asarray(m.decode((source/'audio'/audio[k]['file']).read_bytes()))
    def spectral(x,shape):
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        return np.fft.irfft(np.fft.rfft(y)*shape(f),n=len(y))[pad:pad+n]
    def filt(x,low,high):
        return spectral(x,lambda f:(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5)
    def level(x):
        # Use the body rather than an isolated peak to balance the water layers.
        n=min(len(x),round(.22*rate)); return x/max(np.sqrt(np.mean(x[:n]**2)),1e-9)
    def take(entry,low,high,speed):
        raw=z.read(entry);x=np.asarray(m.decode(raw))
        at=max(0,round((m.sliding_peak(x[:round(.4*rate)].tolist())-.055)*rate))
        x=x[at:];x=np.interp(np.arange(0,len(x)-1,speed),np.arange(len(x)),x)
        return filt(x,low,high),dict(archiveEntry=entry,sha256=m.sha(raw),leadTrimSamples=at,rate=speed,lowpass=low,highpass=high)
    def env(x,hold,tau,attack):
        t=np.arange(len(x))/rate
        return x*(1-np.exp(-t/attack))*np.exp(-np.maximum(0,t-hold)/tau)
    def finish(x,peak):
        y=x.copy(); y[:192]*=np.sin(np.linspace(0,np.pi/2,192))**2
        bins=[max(abs(y[j:j+480])) for j in range(0,len(y),480)]
        for j in range(40,len(bins)-6):
            if max(bins[j:j+6])<max(abs(y))*10**(-66/20): y=y[:(j+6)*480];break
        n=min(1440,len(y)//5);y[-n:]*=np.cos(np.linspace(0,np.pi/2,n))**2
        return y*peak/max(abs(y))
    def mix(parts):
        y=np.zeros(max(len(x) for x,g in parts))
        for x,g in parts:y[:len(x)]+=x*g
        return y
    def reg(x,origin,edit):
        raw=m.wav_bytes(x);k='r10/'+m.sha(raw)[:20];file=k.replace('/','-')+'.wav';(source/'audio'/file).write_bytes(raw)
        y=m.decode(raw);audio[k]={**m.builder.audio_payload(raw,file),'gain':1,'peak':max(map(abs,y)),
            'peakTime':m.sliding_peak(y),'peakEnvelopeStep':.01,
            'peakEnvelope':[max(map(abs,y[j:j+480])) for j in range(0,len(y),480)],'source':origin,'edit':edit}
        return k
    entries=([f'Magic_Spells/water_spell_impact_hit_{i:02}.wav' for i in range(1,7)]
             +[f'Magic_Spells/water_blast_projectile_spell_{i:02}.wav' for i in range(1,5)])
    for i in range(10):
        for family in FAMILIES:
            big=family=='large_explosion';short=family=='triple_explosion';speed=.90 if big else 1.08 if short else 1
            wet,water_origin=take(entries[i],4200,110,speed)
            bubble,bubble_origin=take(f'Comedy_Cartoon/bubble_effect_{i%4+1:02}.wav',3000,170,speed)
            spread,spread_origin=take(f'Water/water_splash_object_body_{i%5+1:02}.wav',4000,200,speed)
            hold=.075 if short else .15 if big else .12;tau=.085 if short else .20 if big else .15
            wet=env(level(wet),hold,tau,.008)
            bubble=env(level(bubble),.04,.065 if short else .095,.009)
            spread=env(level(spread),hold,tau,.016)
            y=finish(mix([(wet,.75),(bubble,.24),(spread,.33)]),.30 if short else .44 if big else .36)
            k=reg(y,{'waterBurst':water_origin,'membraneBubble':bubble_origin,'waterSpread':spread_origin,
                'previousCandidate':before[family]['variants'][i]['audio']},
                dict(mode='water-balloon-rupture',family=family,waterGain=.75,bubbleGain=.24,spreadGain=.33,
                     explosionRecordingUsed=False,impactRecordingUsed=False,addedReverb=False,holdSeconds=hold,decaySeconds=tau))
            groups[family]['variants'][i]=dict(audio=k,gain=.72 if short else .8,name=f'{"짧은" if short else "큰" if big else ""} 물풍선 파열 {i+1:02}'.strip())
    for family in FAMILIES:groups[family]['revision']=10
    changed=[];preserved={}
    for a in p['actions']:
        bomb=any(l.get('group') in FAMILIES for r in a['candidates'] for l in r['layers'])
        if not bomb and a['id']!='dagger_range':continue
        changed.append(a['id']);a['candidateRevision']=10
        reason='물 파열·버블·퍼지는 물결만 조합 · 화약 폭발과 단단한 충격 원음 제거' if bomb else '긴 연막 파열 유지 · 첫 딱과 쨍한 고역 완화'
        a['note']=a['reviewReason']=reason
        for i,r in enumerate(a['candidates']):
            for l in r['layers']:
                if l.get('group') in FAMILIES:
                    v=groups[l['group']]['variants'][i];l.update(audio=v['audio'],gain=v['gain'],name='젖은 막이 터지는 물풍선')
                elif a['id']=='dagger_range' and l['id']=='burst':
                    oldkey=l['audio'];x=pcm(oldkey)
                    # Retain the full r9 tail. Remove sharpness locally and in
                    # the upper spectrum without speeding up or cutting the pop.
                    y=spectral(x,lambda f:.19+.81/(1+(f/1750)**4))
                    t=np.arange(len(y))/rate;y*=1-.68*np.exp(-t/.032)
                    n=round(.018*rate);y[:n]*=np.sin(np.linspace(0,np.pi/2,n))**2
                    y[-960:]*=np.cos(np.linspace(0,np.pi/2,960))**2
                    # Preserve overall level conservatively; don't restore the snap.
                    y*=min(1.15,np.sqrt(sum(x*x)/max(sum(y*y),1e-12)))
                    k=reg(y,{'processedSource':oldkey,'sha256':audio[oldkey]['sha256']},
                        dict(mode='rounded-long-smoke-pop',upperShelfHz=1750,upperGain=.19,attackSeconds=.018,
                             firstTransientGain=.32,transientRecoverySeconds=.032,retainedDuration=True))
                    l.update(audio=k,name='둥근 연막탄 파열 · 펑')
                else:preserved[f'{a["id"]}/{i+1}/{l["id"]}']=copy.deepcopy(l)
            r['id']=a['id']+f'-v10-{i+1:02}';r['reason']=reason;r['name']=f'{i+1:02} · '+' → '.join(l['name'] for l in r['layers'])
    p.update(revision=10,reviewedActions=changed,revision10PreservedLayers=preserved,
             revision10UnchangedActions={a['id']:a for a in old if a['id'] not in changed})
    z.close()


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,args.archive,source)
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json',dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset,'audio.gamemaster-skill-audition','revision 10: water-balloon sources and softer smoke; stars preserved; not applied')
    print('Updated 30 water-balloon bursts and 10 softer smoke pops; decoy, laser and timings preserved.')
