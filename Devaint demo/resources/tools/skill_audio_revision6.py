"""Restore natural explosion tails without changing throws or event bindings."""
import copy
import zipfile
import numpy as np
import render_basic_attack_audio as m


def revise(p,archive,source):
    audio=p['audio'];groups=p['sharedGroups'];rate=m.RATE;z=zipfile.ZipFile(archive)
    before=copy.deepcopy(groups);raw_cache={}
    def read(key):return np.asarray(m.decode((source/'audio'/audio[key]['file']).read_bytes()))
    def filt(x,low,high=40):
        n=len(x);pad=rate//4;x=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(x),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(x)*shape,n=len(x))[pad:pad+n]
    def original(entry,low=3200,high=80,pitch=1,peak_start=False):
        raw=z.read(entry);x=np.asarray(m.decode(raw))
        if peak_start:start=max(0,round((m.sliding_peak(x.tolist())-.015)*rate))
        else:start=max(0,int(np.flatnonzero(abs(x)>max(abs(x))*.006)[0])-round(.006*rate))
        x=x[start:];x=np.interp(np.arange(0,len(x)-1,pitch),np.arange(len(x)),x)
        return filt(x,low,high),{'archiveEntry':entry,'sha256':m.sha(raw),'leadTrimSamples':start,'pitch':pitch,'lowpass':low,'highpass':high}
    def put(parts):
        n=max(len(x)+round(at*rate) for x,at,gain in parts);y=np.zeros(n)
        for x,at,gain in parts:
            start=round(at*rate);y[start:start+len(x)]+=x*gain
        return y
    def finalize(x,oldkey,kind,i,tau,hold,minimum):
        # One continuous decay, including the previously discarded original tail.
        t=np.arange(len(x))/rate;env=np.exp(-np.maximum(0,t-hold)/tau);x=x*env
        # Quiet diffuse tail bridges internal component endings; no audible repeats.
        ir_n=round(.55*rate);tt=np.arange(ir_n)/rate
        ir=filt(np.random.default_rng(6000+i).normal(size=ir_n),1500,150)*np.exp(-tt/.12)
        ir*=.22/max(np.linalg.norm(ir),1e-12);ir[:round(.012*rate)]=0
        size=1<<(len(x)+len(ir)-2).bit_length();wet=np.fft.irfft(np.fft.rfft(x,size)*np.fft.rfft(ir,size),n=size)[:len(x)+len(ir)-1]
        y=np.pad(x,(0,len(wet)-len(x)))+wet
        # End only after >=60 ms has stayed below -65 dB of the actual peak.
        step=round(.01*rate);peak=max(abs(y));bins=np.array([max(abs(y[n:n+step])) for n in range(0,len(y),step)])
        end=len(y)
        for j in range(max(1,round(minimum/.01)),len(bins)-6):
            if max(bins[j:j+6])<peak*10**(-65/20):end=min(len(y),(j+6)*step);break
        y=y[:end];n=min(round(.025*rate),len(y)//4);y[:round(.004*rate)]*=np.sin(np.linspace(0,np.pi/2,round(.004*rate)))**2
        y[-n:]*=np.cos(np.linspace(0,np.pi/2,n))**2
        # Preserve each former candidate's peak and group gain, rather than making it louder.
        y*=max(abs(read(oldkey)))/max(abs(y));return y
    def register(x,origin,edit):
        raw=m.wav_bytes(x);key='r6/'+m.sha(raw)[:20];file=key.replace('/','-')+'.wav';(source/'audio'/file).write_bytes(raw);x=m.decode(raw)
        step=round(.01*rate)
        audio[key]={**m.builder.audio_payload(raw,file),'gain':1,'peak':max(map(abs,x)),'peakTime':m.sliding_peak(x),'peakEnvelopeStep':.01,'peakEnvelope':[max(map(abs,x[n:n+step])) for n in range(0,len(x),step)],'source':origin,'edit':edit}
        return key
    for i in range(10):
        old=before['triple_explosion']['variants'][i];pre_cut=audio[old['audio']]['source']['processedSource'];original_info=audio[pre_cut]
        entry=original_info['source']['archiveEntry'];e=original_info['edit'];x,origin=original(entry,e['lowpass'],e.get('highpass',35),e['pitch'],peak_start=True)
        x=finalize(x,old['audio'],'triple',i,.085,.035,.45)
        key=register(x,{'restoredOriginal':origin,'previousCandidate':old['audio']},dict(mode='natural-decay-explosion',family='triple',decaySeconds=.085,shortAttackLongQuietTail=True))
        groups['triple_explosion']['variants'][i]={**old,'audio':key,'name':f'짧은 앞소리 · 자연 감쇠 {i+1:02}'}

        old=before['normal_explosion']['variants'][i];parts=audio[old['audio']]['source']['parts'];bubble=read(parts[0]['audio'])
        splash_info=audio[parts[1]['audio']]['source'];splash,origin=original(splash_info['archiveEntry'])
        # Same water recording, but its complete tail is available to the final envelope.
        splash=np.asarray(m.level(splash.tolist(),-25));x=put([(bubble,0,1),(splash,.018,.55)])
        natural=x.copy();x=finalize(x,old['audio'],'normal',100+i,.19,.06,.65)
        key=register(x,{'restoredOriginal':origin,'parts':[dict(audio=parts[0]['audio'],onset=0,gain=1)],'previousCandidate':old['audio']},dict(mode='natural-decay-explosion',family='normal',decaySeconds=.19,removedSeparateDroplet=True))
        groups['normal_explosion']['variants'][i]={**old,'audio':key,'name':f'잉크 파열 · 이어지는 물꼬리 {i+1:02}'}

        old=before['large_explosion']['variants'][i];parts_big=audio[old['audio']]['source']['parts'];body=read(parts_big[1]['audio'])
        y=np.interp(np.arange(0,len(natural)-1,.88),np.arange(len(natural)),natural)
        y=put([(y,0,1),(body,.012,.4)]);y=finalize(y,old['audio'],'large',200+i,.23,.075,.8)
        big=register(y,{'restoredOriginal':origin,'parts':[dict(audio=parts[0]['audio'],onset=0,gain=1),dict(audio=parts_big[1]['audio'],onset=.012,gain=.4)],'previousCandidate':old['audio']},dict(mode='natural-decay-explosion',family='large',decaySeconds=.23))
        groups['large_explosion']['variants'][i]={**old,'audio':big,'name':f'큰 잉크 파열 · 자연 잔향 {i+1:02}'}
    for group in ['triple_explosion','normal_explosion','large_explosion']:groups[group]['revision']=6
    changed=[];preserved={}
    for a in p['actions']:
        if not any(l.get('group') in ['triple_explosion','normal_explosion','large_explosion'] for r in a['candidates'] for l in r['layers']):continue
        changed.append(a['id']);a['candidateRevision']=6
        a['note']+=' · 폭발은 원음의 꼬리를 복원해 자연 감쇠합니다.'
        for i,r in enumerate(a['candidates']):
            for l in r['layers']:
                if l.get('group') in ['triple_explosion','normal_explosion','large_explosion']:
                    v=groups[l['group']]['variants'][i];l.update(audio=v['audio'],gain=v['gain'],name='자연스럽게 잦아드는 폭발')
                else:preserved[f'{a["id"]}/{i+1}/{l["id"]}']=copy.deepcopy(l)
            r['id']=a['id']+f'-v6-{i+1:02}';r['name']=f'{i+1:02} · '+' → '.join(l['name'] for l in r['layers'])
    p.update(revision=6,reviewedActions=changed,preservedExplosionCompanions=preserved)
    z.close()
