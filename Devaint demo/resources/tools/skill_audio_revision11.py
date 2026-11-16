"""A single one-second smoke hiss and impact-free continuous water releases."""
import copy
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m
from skill_audio_revision7 import FAMILIES


def revise(p,archive,source):
    assert p['revision']==10
    rate=m.RATE;audio=p['audio'];groups=p['sharedGroups'];old=copy.deepcopy(p['actions']);before=copy.deepcopy(groups)
    def band(x,low,high):
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+n]
    def rms(x):return np.sqrt(np.mean(x*x))+1e-12
    def smooth(x,width):
        return np.convolve(np.pad(x,(width//2,width//2),mode='edge'),np.ones(width)/width,mode='valid')[:len(x)]
    def fade(x,attack,tail):
        y=x.copy();a=round(attack*rate);b=round(tail*rate)
        y[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2
        y[-b:]*=np.cos(np.linspace(0,np.pi/2,b))**2
        return y
    def reg(x,origin,edit):
        raw=m.wav_bytes(x);k='r11/'+m.sha(raw)[:20];file=k.replace('/','-')+'.wav';(source/'audio'/file).write_bytes(raw);y=m.decode(raw)
        audio[k]={**m.builder.audio_payload(raw,file),'gain':1,'peak':max(map(abs,y)),
            'peakTime':m.sliding_peak(y),'peakEnvelopeStep':.01,
            'peakEnvelope':[max(map(abs,y[j:j+480])) for j in range(0,len(y),480)],'source':origin,'edit':edit}
        return k
    hisses=[]
    for i in range(10):
        t=np.arange(rate)/rate;white=np.random.default_rng(11100+i).normal(size=rate)
        body=band(white,1600+60*i,250);body/=rms(body)
        air=band(white,3900+100*i,850);air/=rms(air)
        shift=1-np.exp(-t/.12)
        y=(body*(.60-.40*shift)+air*(.40+.35*shift))*np.exp(-t/.85)
        y=fade(y,.035,.25);y*=.26/max(abs(y))
        hisses.append(reg(y,{'procedural':'one continuous band-limited air release; no recorded pop or impact'},
            dict(mode='one-second-smoke-hiss',durationSeconds=1,attackSeconds=.035,tailFadeSeconds=.25,
                 repeat=False,burst=False,sustainLoop=False)))
    # Use continuous water AFTER each source's strongest attack. None of the
    # former water-hit, cartoon bubble, explosion, or thud files feeds this mix.
    entries=([f'Water/water_splash_object_body_{i:02}.wav' for i in range(1,6)]
             +[f'Magic_Spells/water_spell_tidal_wave_drown_{i:02}.wav' for i in range(1,5)]
             +['Magic_Spells/water_spell_wave_crash_01.wav'])
    with zipfile.ZipFile(archive) as z:
        for i,entry in enumerate(entries):
            raw=z.read(entry);original=np.asarray(m.decode(raw))
            peak_time=m.sliding_peak(original.tolist())
            start=round((peak_time+.090)*rate)
            recording=original[start:start+round(1.35*rate)]
            assert len(recording)>round(.8*rate)
            for family in FAMILIES:
                big=family=='large_explosion';short=family=='triple_explosion'
                speed=.92 if big else 1.06 if short else 1
                x=np.interp(np.arange(0,len(recording)-1,speed),np.arange(len(recording)),recording)
                x=band(x,4200+50*i,250)
                # Level out individual splats before adding a single continuous
                # release envelope. No isolated percussive accent is mixed in.
                local=np.sqrt(smooth(x*x,round(.012*rate))+1e-10)
                target=np.percentile(local,50)
                gain=np.minimum(1,(target/np.maximum(local,1e-8))**.85)
                x*=smooth(gain,round(.006*rate));x/=rms(x)
                t=np.arange(len(x))/rate;tau=.080 if short else .145 if big else .115
                hold=.055 if short else .12 if big else .085
                y=x*np.exp(-np.maximum(0,t-hold)/tau)
                y=fade(y,.030,.08)
                # End after six quiet 10-ms windows, retaining the natural tail.
                bins=[max(abs(y[j:j+480])) for j in range(0,len(y),480)]
                threshold=max(abs(y))*10**(-66/20)
                for j in range(40,len(bins)-6):
                    if max(bins[j:j+6])<threshold:y=y[:(j+6)*480];break
                y=fade(y,.001,.03);y*=(.30 if short else .44 if big else .36)/max(abs(y))
                k=reg(y,{'continuousWater':{'archiveEntry':entry,'sha256':m.sha(raw),
                    'strongestAttackSeconds':peak_time,'startSample':start,'rate':speed},
                    'previousCandidate':before[family]['variants'][i]['audio']},
                    dict(mode='water-release-without-impact',family=family,removedSourceAttack=True,
                         attackSeconds=.030,highpass=250,lowpass=4200+50*i,decaySeconds=tau,
                         bubbleUsed=False,impactUsed=False,addedReverb=False))
                groups[family]['variants'][i]=dict(audio=k,gain=.72 if short else .8,
                    name=f'{"짧은" if short else "큰" if big else ""} 물 분출·퍼짐 {i+1:02}'.strip())
    for family in FAMILIES:groups[family]['revision']=11
    changed=[];preserved={}
    for a in p['actions']:
        bomb=any(l.get('group') in FAMILIES for r in a['candidates'] for l in r['layers'])
        if not bomb and a['id']!='dagger_range':continue
        changed.append(a['id']);a['candidateRevision']=11
        reason='첫 충격을 제외한 연속 물 분출·퍼짐 · 타격·버블 팝 없음' if bomb else '푸쉬이이이 분출음 1초만 · 터지는 소리와 지속 루프 없음'
        a['note']=a['reviewReason']=reason
        if a['id']=='dagger_range' and 'sustain' in a.get('scenario',{}):
            a['scenario']['sustain']['label']='연막 효과 유지 (소리는 1초)'
        for i,r in enumerate(a['candidates']):
            if a['id']=='dagger_range':
                r['layers']=[dict(id='smoke',name='푸쉬이이이 · 1초 분출',audio=hisses[i],times=[0],
                    gain=1,anchor='activation',align='onset')];r['mixed']=False
            else:
                for l in r['layers']:
                    if l.get('group') in FAMILIES:
                        v=groups[l['group']]['variants'][i];l.update(audio=v['audio'],gain=v['gain'],name='충격 없는 물 분출·퍼짐')
                    else:preserved[f'{a["id"]}/{i+1}/{l["id"]}']=copy.deepcopy(l)
            r['id']=a['id']+f'-v11-{i+1:02}';r['reason']=reason;r['name']=f'{i+1:02} · '+' → '.join(l['name'] for l in r['layers'])
    p.update(revision=11,reviewedActions=changed,revision11PreservedLayers=preserved,
        revision11UnchangedActions={a['id']:a for a in old if a['id'] not in changed})


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,args.archive,source)
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json',dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset,'audio.gamemaster-skill-audition','revision 11: one-second smoke hiss, impact-free water releases; stars preserved; not applied')
    print('Updated 10 one-second hisses and 30 continuous water releases; decoy and laser preserved.')
