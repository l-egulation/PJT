"""Replace the leading whoosh with a short pop and darken/attenuate the spread."""
import argparse
import json
import zipfile
import numpy as np
import render_basic_attack_audio as m


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';old=source/'smoke-release-05'
    out=source/'smoke-release-06';out.mkdir(exist_ok=True);rate=m.RATE
    previous=m.load(old/'mixes.json');decisions=m.load(source/'focused-selections.json');bomb=decisions['bombExplosion']
    assert m.sha((asset/bomb['file']).read_bytes())==bomb['sha256']
    def spectral(x,shape):
        n=len(x);pad=rate//4;y=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        return np.fft.irfft(np.fft.rfft(y)*shape(f),n=len(y))[pad:pad+n]
    def write(file,x):
        raw=m.wav_bytes(x);(out/file).write_bytes(raw);return dict(file=file,sha256=m.sha(raw),duration=len(x)/rate)
    items=[];clips=[]
    with zipfile.ZipFile(args.archive) as z:
        for i,(prior,num,low) in enumerate(zip(previous['items'],(12,13),(1500,1300)),1):
            spread_raw=(old/prior['parts']['spread']['file']).read_bytes();assert m.sha(spread_raw)==prior['parts']['spread']['sha256']
            spread=np.asarray(m.decode(spread_raw))
            spread=spectral(spread,lambda f:(1+(f/low)**8)**-.5)*.5
            spread[:round(.025*rate)]=0;spread[-1]=0
            # Replace the old wind-like puff completely. Retain only a rounded,
            # short pressure burst from the purchased explosion recording.
            entry=f'Explosion_Fire_Gas/explosion_small_short_{i:02}.wav';raw=z.read(entry)
            original=np.asarray(m.decode(raw));at=max(0,round((m.sliding_peak(original[:9600].tolist())-.012)*rate))
            pop=original[at:at+round(.08*rate)].copy()
            def pop_shape(f):
                return (1+(f/3300)**8)**-.5*(1+(110/np.maximum(f,.001))**8)**-.5*(.5+.5*f*f/(f*f+220**2))
            pop=spectral(pop,pop_shape)
            attack=round(.004*rate);release=round(.035*rate)
            pop[:attack]*=np.sin(np.linspace(0,np.pi/2,attack))**2
            pop[-release:]*=np.cos(np.linspace(0,np.pi/2,release))**2
            pop*= (.30 if i==1 else .34)/max(abs(pop));pop[0]=pop[-1]=0
            mix=spread.copy();mix[:len(pop)]+=pop;assert max(abs(mix))<.6
            parts=dict(burst=write(f'S{num}-bang.wav',pop),spread=write(f'S{num}-spread.wav',spread))
            rec=write(f'S{num}.wav',mix);clips.append(np.asarray(m.decode((out/rec['file']).read_bytes())))
            items.append(dict(id=f'S{num}',**rec,status='pending',basedOn=prior['id'],parts=parts,
                sources=dict(burst=dict(archiveEntry=entry,sha256=m.sha(raw),startSample=at),
                             spread=dict(file='../smoke-release-05/'+prior['parts']['spread']['file'],sha256=prior['parts']['spread']['sha256'])),
                edit=dict(burstDuration=.08,burstAttack=.004,burstRelease=.035,burstLowpass=3300,
                          spreadLowpass=low,spreadGain=.5,spreadDuration=1.1,spreadStart=.025,
                          previousWhooshRemoved=True,postMixNormalization=False)))
    write('S12-S13-compare.wav',np.concatenate([clips[0],np.zeros(rate),clips[1]]))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision6',status='pending; not applied',items=items,confirmedBomb=bomb,comparisonGapSeconds=1))
    decisions['smoke'].update(currentCandidates=['S12','S13'],currentAudition='preview/smoke-release-06.html',basedOn=['S10','S11'],
        structure=dict(burstSeconds=.08,totalSeconds=1.1,spreadStart=.025))
    m.dump(source/'focused-selections.json',decisions)
    page=(asset/'preview/smoke-release-05.html').read_text(encoding='utf8')
    page=page.replace('smoke-release-05','smoke-release-06').replace('S10','S12').replace('S11','S13').replace('-puff.wav','-bang.wav')
    page=page.replace('짧은 푸 · 긴 퍼짐','짧은 팡 · 낮고 옅은 퍼짐').replace('낮은 푸 · 긴 퍼짐','또렷한 팡 · 낮고 옅은 퍼짐')
    page=page.replace('푸','팡')
    page=page.replace('앞의 팡는 0.08초로 짧게, 뒤의 쉬이이는 전체 1.1초까지 길게 퍼지도록 분리했습니다. 두 소리는 겹쳐 이어지고 낮고 부드럽게 사라집니다.',
        '앞소리를 짧은 팡으로 교체했습니다. 퍼짐의 음역을 더 낮추고 음량도 줄였으며, 전체 1.1초까지 부드럽게 옅어집니다.')
    page=page.replace('version:5','version:6').replace('focused-bomb-smoke-choice-05.json','focused-bomb-smoke-choice-06.json')
    (asset/'preview/smoke-release-06.html').write_text(page,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition','bomb B3 user-confirmed; smoke S12/S13 short pop with quieter low spread pending; not applied')
    print(json.dumps(dict(smoke=['S12','S13'],burstSeconds=.08,spreadGain=.5,totalSeconds=1.1,bomb='B3 unchanged')))


if __name__=='__main__':main()
