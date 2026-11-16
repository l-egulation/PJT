"""Shorten the softened smoke releases to 0.6 seconds without speeding them up."""
import json
import numpy as np
import render_basic_attack_audio as m


def main():
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source'
    old=source/'smoke-release-03';out=source/'smoke-release-04';out.mkdir(exist_ok=True)
    previous=m.load(old/'mixes.json');decisions=m.load(source/'focused-selections.json');bomb=decisions['bombExplosion']
    assert m.sha((asset/bomb['file']).read_bytes())==bomb['sha256']
    items=[];clips=[];rate=m.RATE;n=round(.6*rate)
    for prior,num in zip(previous['items'],(8,9)):
        raw=(old/prior['file']).read_bytes();assert m.sha(raw)==prior['sha256']
        x=np.asarray(m.decode(raw));t=np.arange(n)/rate
        # Preserve playback rate and the first 100 ms. The ending is already
        # near zero before removing the remaining 400 ms of the previous file.
        progress=np.clip((t-.10)/.50,0,1)
        y=x[:n]*np.cos(progress*np.pi/2)**2.2;y[-1]=0
        raw=m.wav_bytes(y);file=f'S{num}.wav';(out/file).write_bytes(raw);clips.append(np.asarray(m.decode(raw)))
        items.append(dict(id=f'S{num}',basedOn=prior['id'],file=file,duration=.6,sha256=m.sha(raw),status='pending',
            source=dict(file='../smoke-release-03/'+prior['file'],sha256=prior['sha256']),
            edit=dict(preserveFrontSeconds=.1,decayStartSeconds=.1,decayEndSeconds=.6,
                      curve='cosine power 2.2',playbackRate=1,normalization=False,addedSound=False)))
    (out/'S8-S9-compare.wav').write_bytes(m.wav_bytes(np.concatenate([clips[0],np.zeros(rate),clips[1]])))
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision4',status='pending; not applied',items=items,
        confirmedBomb=bomb,comparisonGapSeconds=1))
    decisions['smoke'].update(currentCandidates=['S8','S9'],currentAudition='preview/smoke-release-04.html',basedOn=['S6','S7'])
    m.dump(source/'focused-selections.json',decisions)
    page=(asset/'preview/smoke-release-03.html').read_text(encoding='utf8')
    page=page.replace('smoke-release-03','smoke-release-04').replace('S6','S8').replace('S7','S9')
    page=page.replace('부드러운 압력 분출 · S4 수정','짧은 압력 분출 · S6 수정').replace('낮은 푸 + 부드러운 분출 · S5 수정','낮은 푸 + 짧은 분출 · S7 수정')
    page=page.replace('각 1초','각 0.6초').replace('<p>1초</p>','<p>0.6초</p>')
    page=page.replace('앞의 푸는 유지하고 쉬이이의 음높이와 고역을 낮췄습니다. 0.22초부터 음량이 연속적으로 옅어지며 끝납니다.',
        '낮고 부드러운 음색은 유지하고 길이를 1초에서 0.6초로 줄였습니다. 0.10초부터 자연스럽게 옅어집니다. 재생 속도는 바꾸지 않았습니다.')
    page=page.replace('version:3','version:4').replace('focused-bomb-smoke-choice-03.json','focused-bomb-smoke-choice-04.json')
    (asset/'preview/smoke-release-04.html').write_text(page,encoding='utf8')
    m.register_asset(asset,'audio.gamemaster-skill-audition','bomb B3 user-confirmed; 0.6-second smoke S8/S9 pending; prior stars preserved; not applied')
    print(json.dumps(dict(smoke=['S8','S9'],duration=.6,bomb='B3 unchanged')))


if __name__=='__main__':main()
