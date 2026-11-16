"""Lower and soften the S4/S5 hiss, preserving the leading puff and confirmed B3."""
import json
import re
import numpy as np
import render_basic_attack_audio as m


def main():
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';old=source/'smoke-release-02'
    out=source/'smoke-release-03';out.mkdir(exist_ok=True);rate=m.RATE
    previous=m.load(old/'mixes.json');decisions=m.load(source/'focused-selections.json')
    bomb=decisions['bombExplosion'];assert m.sha((asset/bomb['file']).read_bytes())==bomb['sha256']
    items=[];clips=[]
    for prior,num,low in zip(previous['items'],(6,7),(2900,2500)):
        raw=(old/prior['file']).read_bytes();assert m.sha(raw)==prior['sha256']
        x=np.asarray(m.decode(raw));assert len(x)==rate;t=np.arange(rate)/rate
        # Preserve the first 140 ms exactly. Gradually lower the pitch of the
        # remaining air, instead of retiming the initial puff.
        u=np.clip((t-.14)/.10,0,1);blend=u*u*(3-2*u)
        speed=1-.14*blend;positions=np.cumsum(speed)-speed[0]
        shifted=np.interp(positions,np.arange(len(x)),x)
        pad=rate//4;y=np.pad(shifted,(pad,pad));f=np.fft.rfftfreq(len(y),1/rate)
        shape=(1+(f/low)**8)**-.5
        dark=np.fft.irfft(np.fft.rfft(y)*shape,n=len(y))[pad:pad+len(x)]
        y=x*(1-blend)+dark*blend
        # No level normalization afterwards: the hiss should become softer.
        # One continuously declining envelope replaces the abrupt late fade.
        progress=np.clip((t-.22)/.78,0,1)
        envelope=np.cos(progress*np.pi/2)**2.2
        y*=envelope*(1-.08*blend)
        y[-1]=0
        encoded=m.wav_bytes(y);file=f'S{num}.wav';(out/file).write_bytes(encoded)
        samples=np.asarray(m.decode(encoded));clips.append(samples)
        items.append(dict(id=f'S{num}',basedOn=prior['id'],label='부드러운 압력 분출' if num==6 else '낮은 푸 + 부드러운 분출',
            file=file,duration=len(samples)/rate,sha256=m.sha(encoded),status='pending',
            source=dict(file='../smoke-release-02/'+prior['file'],sha256=prior['sha256']),
            edit=dict(preserveFrontSeconds=.14,transitionSeconds=.10,tailPitchRate=.86,tailLowpass=low,
                decayStartSeconds=.22,continuousDecayEndSeconds=1,decayCurve='cosine power 2.2',tailGain=.92,
                normalization=False,addedSound=False)))
    (out/'S6-S7-compare.wav').write_bytes(m.wav_bytes(np.concatenate([clips[0],np.zeros(rate),clips[1]])))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision3',status='pending; not applied',items=items,
        confirmedBomb=bomb,comparisonGapSeconds=1))
    decisions['smoke'].update(status='pending',currentCandidates=['S6','S7'],
        currentAudition='preview/smoke-release-03.html',basedOn=['S4','S5'])
    m.dump(source/'focused-selections.json',decisions)
    # Reuse the established comparison UI, with a distinct storage key and ids.
    page=(asset/'preview/smoke-release-02.html').read_text(encoding='utf8')
    page=page.replace('smoke-release-02','smoke-release-03').replace('S4','S6').replace('S5','S7')
    page=page.replace('압력 분출 중심','부드러운 압력 분출 · S4 수정').replace('낮은 푸 강조','낮은 푸 + 부드러운 분출 · S5 수정')
    page=page.replace('폭발음·점화음·기계 작동음 없이 가스 분출과 낮은 공기 방출을 조합한 청취 시안입니다.','앞의 푸는 유지하고 쉬이이의 음높이와 고역을 낮췄습니다. 0.22초부터 음량이 연속적으로 옅어지며 끝납니다.')
    page=page.replace('version:2','version:3').replace('focused-bomb-smoke-choice-02.json','focused-bomb-smoke-choice-03.json')
    (asset/'preview/smoke-release-03.html').write_text(page,encoding='utf8')
    assert m.sha((asset/bomb['file']).read_bytes())==bomb['sha256']
    m.register_asset(asset,'audio.gamemaster-skill-audition','bomb B3 user-confirmed; softer smoke S6/S7 pending; prior stars preserved; not applied')
    print(json.dumps(dict(bomb='B3 unchanged',smoke=[v['id'] for v in items],page=str(asset/'preview/smoke-release-03.html'))))


if __name__=='__main__':main()
