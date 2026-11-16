"""Separate the short smoke puff from a longer, soft spreading tail."""
import argparse
import json
import zipfile
import numpy as np
import render_basic_attack_audio as m


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';out=source/'smoke-release-05';out.mkdir(exist_ok=True)
    decisions=m.load(source/'focused-selections.json');bomb=decisions['bombExplosion']
    assert m.sha((asset/bomb['file']).read_bytes())==bomb['sha256']
    previous=m.load(source/'smoke-release-02/mixes.json');rate=m.RATE;n=round(1.10*rate);items=[];clips=[]
    def rms(x):return np.sqrt(np.mean(x*x))+1e-12
    def band(x,low,high):
        length=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5*(1+(high/np.maximum(f,.001))**8)**-.5
        return np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+length]
    def write(file,x):
        raw=m.wav_bytes(x);(out/file).write_bytes(raw);return dict(file=file,sha256=m.sha(raw),duration=len(x)/rate)
    with zipfile.ZipFile(args.archive) as z:
        for prior,num,low in zip(previous['items'],(10,11),(2900,2500)):
            pressure,front=prior['sources'];raw=z.read(pressure['archiveEntry']);assert m.sha(raw)==pressure['sha256']
            gas=np.asarray(m.decode(raw));start=round(pressure['start']*rate)
            count=n-round(.025*rate);positions=start+np.arange(count)*.86
            gas=np.interp(positions,np.arange(len(gas)),gas);gas=band(gas,low,250)
            env=np.sqrt(np.convolve(gas*gas,np.ones(960)/960,mode='same')+1e-10)
            target=np.percentile(env,60);gas*=np.clip(target/np.maximum(env,1e-9),.35,2.2);gas/=rms(gas)
            t=np.arange(count)/rate
            u=np.clip(t/.030,0,1);attack=np.sin(u*np.pi/2)**2
            fade=np.cos(np.clip((t-.075)/(count/rate-.075),0,1)*np.pi/2)**2.2
            gas*=attack*fade*.50;gas[-1]=0
            spread=np.zeros(n);spread[round(.025*rate):]=gas
            raw=z.read(front['archiveEntry']);assert m.sha(raw)==front['sha256']
            puff=np.asarray(m.decode(raw));puff=band(puff,1800,230)
            puff=puff[round(.010*rate):round(.090*rate)].copy();assert len(puff)==round(.08*rate)
            puff[:384]*=np.sin(np.linspace(0,np.pi/2,384))**2
            puff[-1440:]*=np.cos(np.linspace(0,np.pi/2,1440))**2
            puff=puff/rms(puff)*prior['edit']['frontGain']
            mix=spread.copy();mix[:len(puff)]+=puff
            active=mix[abs(mix)>max(abs(mix))*.03];gain=min(.075/rms(active),.46/max(abs(mix)))
            mix*=gain;puff*=gain;spread*=gain
            parts=dict(puff=write(f'S{num}-puff.wav',puff),spread=write(f'S{num}-spread.wav',spread))
            record=write(f'S{num}.wav',mix);clips.append(np.asarray(m.decode((out/record['file']).read_bytes())))
            items.append(dict(id=f'S{num}',**record,status='pending',basedOn=prior['id'],sources=prior['sources'],parts=parts,
                edit=dict(puffDuration=.08,spreadStart=.025,totalDuration=1.1,spreadPitchRate=.86,spreadLowpass=low,
                    puffGain=prior['edit']['frontGain'],spreadGain=.50,commonGain=float(gain),
                    spreadAttack=.030,spreadDecay='continuous cosine power 2.2',noNewSound=True)))
    write('S10-S11-compare.wav',np.concatenate([clips[0],np.zeros(rate),clips[1]]))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision5',status='pending; not applied',items=items,
        confirmedBomb=bomb,comparisonGapSeconds=1))
    decisions['smoke'].update(currentCandidates=['S10','S11'],currentAudition='preview/smoke-release-05.html',
        basedOn=['S8','S9'],structure=dict(puffSeconds=.08,totalSeconds=1.1,spreadStart=.025))
    m.dump(source/'focused-selections.json',decisions)
    page=(asset/'preview/smoke-release-04.html').read_text(encoding='utf8')
    page=page.replace('smoke-release-04','smoke-release-05').replace('S8','S10').replace('S9','S11')
    page=page.replace('짧은 압력 분출 · S6 수정','짧은 푸 · 긴 퍼짐').replace('낮은 푸 + 짧은 분출 · S7 수정','낮은 푸 · 긴 퍼짐')
    page=page.replace('각 0.6초','각 1.1초 · 앞의 푸 0.08초').replace('<p>0.6초</p>','<p>푸 0.08초 · 전체 1.1초</p>')
    page=page.replace('낮고 부드러운 음색은 유지하고 길이를 1초에서 0.6초로 줄였습니다. 0.10초부터 자연스럽게 옅어집니다. 재생 속도는 바꾸지 않았습니다.',
        '앞의 푸는 0.08초로 짧게, 뒤의 쉬이이는 전체 1.1초까지 길게 퍼지도록 분리했습니다. 두 소리는 겹쳐 이어지고 낮고 부드럽게 사라집니다.')
    for num in (10,11):
        marker=f'<button data-id="S{num}">'
        details=f'<details><summary>앞의 푸 / 뒤의 퍼짐 따로 듣기</summary><p>푸 · 0.08초</p><audio controls preload="none" src="../source/smoke-release-05/S{num}-puff.wav"></audio><p>퍼짐</p><audio controls preload="none" src="../source/smoke-release-05/S{num}-spread.wav"></audio></details>'
        page=page.replace(marker,details+marker)
    page=page.replace('version:4','version:5').replace('focused-bomb-smoke-choice-04.json','focused-bomb-smoke-choice-05.json')
    (asset/'preview/smoke-release-05.html').write_text(page,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition','bomb B3 user-confirmed; smoke S10/S11 short puff and long spread pending; not applied')
    print(json.dumps(dict(smoke=['S10','S11'],puffSeconds=.08,totalSeconds=1.1,bomb='B3 unchanged')))


if __name__=='__main__':main()
