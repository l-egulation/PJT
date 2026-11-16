"""Smoke audition made exclusively from single gas-release recordings."""
import argparse
import copy
import io
import json
import re
import wave
import zipfile
import numpy as np
import render_basic_attack_audio as m


def revise(p, source, archive):
    assert p['revision']==13
    out=source/'smoke-release-10';out.mkdir(exist_ok=True)
    previous=copy.deepcopy(p['actions'])
    specs=[
        ('gas_leak_01_loop.wav',.25,1.10,2800,'공기 빠짐 · 얇게'),
        ('gas_leak_02_loop.wav',.25,1.10,2500,'공기 빠짐 · 낮게'),
        ('gas_leak_03_loop.wav',.15,1.10,3000,'공기 빠짐 · 넓게'),
        ('gas_leak_04_loop.wav',.20,1.10,2600,'공기 빠짐 · 옅게'),
        ('gas_leak_long_burst_01.wav',.20,1.15,2700,'긴 방출 · 완만하게'),
        ('gas_leak_med_burst_01.wav',.15,1.10,2400,'중간 방출 · 낮게'),
        ('gas_leak_med_burst_02.wav',.20,1.10,3100,'중간 방출 · 선명하게'),
        ('gas_leak_short_burst_01.wav',.03,.72,2800,'짧은 방출 · 0.72초'),
        ('gas_leak_short_burst_02.wav',.02,.47,2700,'짧은 방출 · 0.47초'),
        ('gas_leak_short_burst_03.wav',.03,.79,2500,'짧은 방출 · 0.79초'),
    ]
    items=[];clips=[]
    with zipfile.ZipFile(archive) as z:
        for number,(name,start,seconds,cutoff,label) in enumerate(specs,1):
            entry='Explosion_Fire_Gas/'+name;original=z.read(entry)
            x=np.asarray(m.decode(original));x=x[round(start*m.RATE):round((start+seconds)*m.RATE)]
            assert len(x)==round(seconds*m.RATE)
            pad=m.RATE//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/m.RATE)
            shape=(1+(f/cutoff)**6)**-.5*(1+(110/np.maximum(f,.001))**6)**-.5
            y=np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+len(x)]
            # Native playback rate and one continuous source. No layer, repeat,
            # motor/door/weapon/magic recording, generated noise, or pitch shift.
            t=np.linspace(0,1,len(y));y*=np.exp(-.9*t)
            attack=round(.065*m.RATE);release=round(min(.45,seconds*.6)*m.RATE)
            y[:attack]*=np.sin(np.linspace(0,np.pi/2,attack))**2
            y[-release:]*=np.cos(np.linspace(0,np.pi/2,release))**2
            y*=min(.045/max(np.sqrt(np.mean(y*y)),1e-9),.30/max(abs(y)))
            y[0]=y[-1]=0
            raw=m.wav_bytes(y);filename=f'smoke10-{number:02}.wav';key=f'r14/smoke-{number:02}'
            (out/filename).write_bytes(raw);(source/'audio'/filename).write_bytes(raw)
            decoded=np.asarray(m.decode(raw))
            origin=dict(archiveEntry=entry,sha256=m.sha(original),cropSeconds=[start,start+seconds])
            edit=dict(mode='single-gas-release',burst=False,repeat=False,playbackRate=1,
                syntheticNoise=False,layerCount=1,attackSeconds=.065,tailFadeSeconds=release/m.RATE,lowpass=cutoff,highpass=110)
            p['audio'][key]=dict(m.builder.audio_payload(raw,filename),gain=1,peak=float(max(abs(decoded))),
                peakTime=m.sliding_peak(decoded.tolist()),peakEnvelopeStep=.01,
                peakEnvelope=[float(max(abs(decoded[j:j+480]))) for j in range(0,len(decoded),480)],source=origin,edit=edit)
            items.append(dict(id=f'A{number:02}',number=number,label=label,file=filename,audio=key,
                sha256=m.sha(raw),duration=seconds,status='pending',sources=[origin],edit=edit))
            with wave.open(io.BytesIO(raw),'rb') as reader:
                params=reader.getparams();clips.append(reader.readframes(reader.getnframes()))
    for name,group in [('smoke10-all-10.wav',clips),('smoke10-01-05.wav',clips[:5]),('smoke10-06-10.wav',clips[5:])]:
        with wave.open(str(out/name),'wb') as writer:
            writer.setparams(params);writer.writeframes((b'\0'*(m.RATE*2)).join(group))
    a=next(a for a in p['actions'] if a['id']=='dagger_range')
    a.update(candidateRevision=14,note='공기 빠짐 원음 한 개씩 · 원래 재생속도 · 팡·반복·다른 재료 없음',reviewReason='금속 느낌 재검토 · 단일 가스 방출 녹음 10개')
    for r,item in zip(a['candidates'],items):
        r.update(id=f'dagger_range-v14-{item["number"]:02}',name=f'{item["number"]:02} · '+item['label'],reason=a['reviewReason'],mixed=False,
            layers=[dict(id='smoke',name=item['label'],audio=item['audio'],times=[0],gain=1,anchor='activation',align='onset')])
    p.update(revision=14,reviewedActions=['dagger_range'],revision14UnchangedActions={a['id']:a for a in previous if a['id']!='dagger_range'})
    p['reopenedReviews']['dagger_range']=14
    decisions=m.load(source/'focused-selections.json');smoke=decisions['smoke']
    smoke.update(status='pending; reopened by user',currentCandidates=[i['id'] for i in items],currentAudition='preview/smoke-release-10.html',
        instruction='쇠 느낌 없이 공기가 빠지듯 스르르 스스스',basedOn='single gas-release recording per candidate',
        structure=dict(burstSeconds=0,durationRangeSeconds=[.47,1.15],playbackRate=1))
    smoke.setdefault('rejected',[]).extend(['smoke-release-09/'+f'S{i:02}' for i in range(1,11)])
    m.dump(source/'focused-selections.json',decisions)
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision10',status='pending; not applied',items=items,comparisonGapSeconds=1))
    page=(source.parent/'preview/smoke-release-09.html').read_text(encoding='utf8')
    page=page.replace('smoke-release-09','smoke-release-10').replace('smoke09-','smoke10-')
    page=page.replace('연막탄 새 후보 10개','연막탄 · 공기 빠짐 10개').replace('연막탄 · 새 후보 10개','연막탄 · 공기 빠짐 10개')
    page=page.replace('팡 없이 1.1초 · 구매팩 공기 방출·분사 편집/조합 · 미정','공기 빠짐 녹음 한 개씩 · 원래 속도 · 부드러운 시작과 감쇠 · 미정')
    page=page.replace('전달한 다른 스킬의 별표는 확정으로 보존했습니다. 기존 연막은 기각했습니다.',
        '01~07은 약 1.1초, 08~10은 짧은 자연 방출입니다. 다른 스킬의 별표는 확정으로 보존했습니다.')
    olditems=m.load(source/'smoke-release-09/mixes.json')['items']
    for old,new in zip(olditems,items):page=page.replace(old['label'],new['label'])
    page=page.replace("revision:9,action:'dagger_range',candidateRevision:13", "revision:10,action:'dagger_range',candidateRevision:14")
    page=page.replace('smoke-sound-selections-09.json','smoke-sound-selections-10.json')
    files=json.dumps([dict(number=i['number'],file=i['file'],sha256=i['sha256']) for i in items])
    page=re.sub(r'files:\[.*?\]\.filter',lambda _: 'files:'+files+'.filter',page)
    (source.parent/'preview/smoke-release-10.html').write_text(page,encoding='utf8')
    return p


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,source,args.archive)
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json',dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset,'audio.gamemaster-skill-audition','revision 14: single gas-release sources only; smoke pending; other selections preserved; not applied')
    print('Created ten single-source gas-release candidates; native rate, no additional layers.')
